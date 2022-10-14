// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.BitcoinRpc.Responses;
using Microsoft.Extensions.Logging;
using NBitcoin.Crypto;
using Transaction = NBitcoin.Transaction;
using MerchantAPI.Common.Json;
using System.ComponentModel.DataAnnotations;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Authentication;
using MerchantAPI.Common.Exceptions;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using MerchantAPI.APIGateway.Domain.Models.Faults;
using NBitcoin.DataEncoders;
using System.Text;
using MerchantAPI.APIGateway.Domain.Models.APIStatus;
using System.Collections.ObjectModel;
using MerchantAPI.APIGateway.Domain.Metrics;
using Prometheus;

namespace MerchantAPI.APIGateway.Domain.Actions
{

  public class Mapi : IMapi
  {
    readonly IRpcMultiClient rpcMultiClient;
    readonly IFeeQuoteRepository feeQuoteRepository;
    readonly IBlockChainInfo blockChainInfo;
    readonly IMinerId minerId;
    readonly ILogger<Mapi> logger;
    readonly ITxRepository txRepository;
    protected readonly IClock clock;
    readonly AppSettings appSettings;
    protected readonly IFaultManager faultManager;
    protected readonly IFaultInjection faultInjection;
    readonly MapiMetrics mapiMetrics;
    readonly MempoolCheckerMetrics mempoolCheckerMetrics;

    static class ResultCodes
    {
      public const string Success = "success";
      public const string Failure = "failure";
    }

    private class CollidedWithComparer : IEqualityComparer<CollidedWith>
    {
      public bool Equals(CollidedWith t1, CollidedWith t2)
      {
        return t1.TxId == t2.TxId;
      }

      public int GetHashCode([DisallowNull] CollidedWith t)
      {
        return t.TxId.GetHashCode();
      }
    }

    public Mapi(
      IRpcMultiClient rpcMultiClient,
      IFeeQuoteRepository feeQuoteRepository,
      IBlockChainInfo blockChainInfo,
      IMinerId minerId,
      ITxRepository txRepository,
      ILogger<Mapi> logger,
      IClock clock,
      IOptions<AppSettings> appSettingOptions,
      IFaultManager faultManager,
      IFaultInjection faultInjection,
      MapiMetrics mapiMetrics,
      MempoolCheckerMetrics mempoolCheckerMetrics)
    {
      this.rpcMultiClient = rpcMultiClient ?? throw new ArgumentNullException(nameof(rpcMultiClient));
      this.feeQuoteRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
      this.blockChainInfo = blockChainInfo ?? throw new ArgumentNullException(nameof(blockChainInfo));
      this.minerId = minerId ?? throw new ArgumentNullException(nameof(minerId));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      appSettings = appSettingOptions.Value;
      this.faultManager = faultManager ?? throw new ArgumentNullException(nameof(faultManager));
      this.faultInjection = faultInjection ?? throw new ArgumentNullException(nameof(faultInjection));
      this.mapiMetrics = mapiMetrics ?? throw new ArgumentNullException(nameof(mapiMetrics));
      this.mempoolCheckerMetrics = mempoolCheckerMetrics ?? throw new ArgumentNullException(nameof(mempoolCheckerMetrics));
    }


    public static bool TryParseTransaction(byte[] transaction, out Transaction result)
    {
      try
      {
        result = HelperTools.ParseBytesToTransaction(transaction);
        return true;
      }
      catch (Exception)
      {
        result = null;
        return false;
      }
    }

    static bool IsDataOutput(byte[] scriptBytes)
    {
      // OP_FALSE OP_RETURN represents data outputs after Genesis was activated. 
      // There is no need to check for output.Value=0.
      // We do not care if somebody wants to burn some satoshis. 
      return scriptBytes.Length > 1 &&
             scriptBytes[0] == (byte)OpcodeType.OP_FALSE &&
             scriptBytes[1] == (byte)OpcodeType.OP_RETURN;
    }

    static bool IsDsntOutput(byte[] scriptBytes)
    {
      var scriptDsnt = new Script(OpcodeType.OP_FALSE);
      scriptDsnt += OpcodeType.OP_RETURN;
      scriptDsnt += Op.GetPushOp(Encoders.Hex.DecodeData(Const.DSNT_IDENTIFIER));

      var scriptDsntBytes = scriptDsnt.ToBytes();
      if (scriptBytes.Length < scriptDsntBytes.Length)
      {
        return false;
      }

      for (int i = 0; i < scriptDsntBytes.Length; i++)
      {
        if (scriptBytes[i] != scriptDsntBytes[i])
        {
          return false;
        }
      }
      return true;
    }

    /// <summary>
    /// Return description that can be safely returned to client without exposing internal details or null otherwise.
    /// </summary>
    /// <param name="exception"></param>
    static string GetSafeExceptionDescription(Exception exception)
    {
      return ((exception as AggregateException)?.GetBaseException() as RpcException)?.Message;
    }

    /// <summary>
    /// Collect previous outputs being spent by tx. Two sources are consulted
    ///  - batch of incoming transactions - outputs wil be found there in case of chained transactions
    ///  - node
    /// Exception is thrown if output can not be found or is already spent on node side
    /// This function does not check for single output that is spent multiple times inside additionalTx.
    /// This will be detected by the node itself and one of the transactions spending the output will be rejected
    /// </summary>
    /// <param name="tx"></param>
    /// <param name="additionalTxs">optional </param>
    /// <param name="rpcMultiClient"></param>
    /// <returns>
    ///   sum of al outputs being spent
    ///   array of all outputs sorted in the same order as tx.inputs
    /// </returns>
    public static async Task<(Money sumPrevOuputs, PrevOut[] prevOuts)> CollectPreviousOuputs(Transaction tx,
      IReadOnlyDictionary<uint256, byte[]> additionalTxs, IRpcMultiClient rpcMultiClient)
    {
      var parentTransactionsFromBatch = new Dictionary<uint256, Transaction>();
      var prevOutsNotInBatch = new List<OutPoint>(tx.Inputs.Count);

      foreach (var input in tx.Inputs)
      {
        var prevOut = input.PrevOut;
        if (parentTransactionsFromBatch.ContainsKey(prevOut.Hash))
        {
          continue;
        }

        // First try to find the output in batch of transactions  we are submitting
        if (additionalTxs != null && additionalTxs.TryGetValue(prevOut.Hash, out var txRaw))
        {

          if (TryParseTransaction(txRaw, out var t))
          {
            parentTransactionsFromBatch.TryAdd(prevOut.Hash, t);
            continue;
          }
          else
          {
            // Ignore parse errors. We might be able to get it from node.
          }
        }
        prevOutsNotInBatch.Add(prevOut);
      }

      Dictionary<OutPoint, PrevOut> prevOutsFromNode = null;

      // Fetch missing outputs from node
      if (prevOutsNotInBatch.Any())
      {
        var missing = prevOutsNotInBatch.Select(x => (txId: x.Hash.ToString(), N: (long)x.N)).ToArray();
        var prevOutsFromNodeResult = await rpcMultiClient.GetTxOutsAsync(missing, getTxOutFields);

        if (missing.Length != prevOutsFromNodeResult.TxOuts.Length)
        {
          throw new Exception(
            $"Internal error. Gettxouts RPC call should return exactly {missing.Length} elements, but it returned {prevOutsFromNodeResult.TxOuts.Length}");
        }

        // Convert results to dictionary for faster lookup.
        // Responses are returned in same order as requests were passed in, so we can use Zip() to merge them
        prevOutsFromNode = new Dictionary<OutPoint, PrevOut>(
          prevOutsNotInBatch.Zip(
            prevOutsFromNodeResult.TxOuts,
            (K, V) => new KeyValuePair<OutPoint, PrevOut>(K, V))
        );
      }

      Money sumPrevOuputs = Money.Zero;
      var resultPrevOuts = new List<PrevOut>(tx.Inputs.Count);
      foreach (var input in tx.Inputs)
      {
        var outPoint = input.PrevOut;
        PrevOut prevOut;

        // Check if UTXO is present in batch of incoming transactions
        if (parentTransactionsFromBatch.TryGetValue(outPoint.Hash, out var txFromBatch))
        {
          // we have found the input in input batch
          var outputs = txFromBatch.Outputs;


          if (outPoint.N > outputs.Count - 1)
          {
            prevOut = new PrevOut
            {
              Error = "Missing inputs - invalid output index"
            };
          }
          else
          {

            var output = outputs[outPoint.N];

            prevOut =
              new PrevOut
              {
                Error = null,
                // ScriptPubKey = null, // We do not use ScriptPUbKey
                ScriptPubKeyLength = output.ScriptPubKey.Length,
                Value = output.Value.ToDecimal(MoneyUnit.BTC),
                IsStandard = true,
                Confirmations = 0
              };
          }
        }
        else // ask the node for previous output
        {
          if (prevOutsFromNode == null || !prevOutsFromNode.TryGetValue(outPoint, out prevOut))
          {
            // This indicates internal error in node or mAPI
            throw new Exception($"Node did not return output {outPoint} that we have asked for");
          }
        }

        if (string.IsNullOrEmpty(prevOut.Error))
        {
          sumPrevOuputs += new Money((long)(prevOut.Value * Money.COIN));
        }

        resultPrevOuts.Add(prevOut);
      }

      return (sumPrevOuputs, resultPrevOuts.ToArray());
    }

    /// <summary>
    /// Check if specified transaction meets fee policy
    /// </summary>
    /// <param name="Transaction">transaction</param>
    /// <param name="DsCheck">dsCheck. Only validate dsCheck output, if true.</param>
    /// <param name="Warnings">warnings. DsCheck warnings.</param>
    /// <param name="TxStatus">txStatus. Status of transaction in mAPI.</param>
    /// <returns>Sum of new outputs and dataBytes count, if tx was not yet accepted by mAPI. Otherwise only check if DSNT output is present.</returns>
    public static (Money sumNewOutputs, long dataBytes) CheckOutputsSumAndValidateDs(Transaction transaction, bool dsCheck, int txStatus, List<string> warnings)
    {
      // This could leave some of the bytes unparsed. In this case we would charge for bytes at the end of 
      // the stream that will not be published to the blockchain, but this is sender's problem.

      Money sumNewOutputs = Money.Zero;
      long dataBytes = 0;
      bool dsntOutput = false;
      foreach (var output in transaction.Outputs)
      {
        sumNewOutputs += output.Value;
        if (output.Value < 0L)
        {
          throw new ExceptionWithSafeErrorMessage("Negative inputs are not allowed");
        }

        var scriptBytes = output.ScriptPubKey.ToBytes(@unsafe: true); // unsafe == true -> make sure we do not modify the result
        if (IsDataOutput(scriptBytes))
        {
          dataBytes += output.ScriptPubKey.Length;
          if (dsCheck && IsDsntOutput(scriptBytes))
          {
            dsntOutput = true;
            if (txStatus >= TxStatus.SentToNode)
            {
              // we are only interested in dsnt output warnings
              return (-1L, -1L);
            }
          }
        }
      }

      if (dsCheck && !dsntOutput)
      {
        warnings.Add(Warning.MissingDSNT);
      }

      return (sumNewOutputs, dataBytes);
    }

    /// <summary>
    /// Check if specified transaction meets fee policy
    /// </summary>
    /// <param name="txBytesLength">Transactions</param>
    /// <param name="sumPrevOuputs">result of CollectPreviousOutputs. If previous outputs are not found there, the node is consulted</param>
    /// <param name="sumNewOutputs">Sum of new outputs.</param>
    /// <param name="dataBytes">DataBytes count.</param>
    /// <param name="feeQuote">FeeQuote. Valid for this user.</param>
    /// <returns></returns>
    public static (bool okToMine, bool okToRely) CheckFees(long txBytesLength, Money sumPrevOuputs, Money sumNewOutputs, long dataBytes, FeeQuote feeQuote)
    {
      long actualFee = (sumPrevOuputs - sumNewOutputs).Satoshi;
      long normalBytes = txBytesLength - dataBytes;

      long feesRequiredMining = 0;
      long feesRequiredRelay = 0;

      foreach (var fee in feeQuote.Fees)
      {
        if (fee.FeeType == Const.FeeType.Standard)
        {
          feesRequiredMining += (normalBytes * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes;
          feesRequiredRelay += (normalBytes * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes;
        }
        else if (fee.FeeType == Const.FeeType.Data)
        {
          feesRequiredMining += (dataBytes * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes;
          feesRequiredRelay += (dataBytes * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes;
        }
      }

      bool okToMine = actualFee >= feesRequiredMining;
      bool okToRelay = actualFee >= feesRequiredRelay;
      return (okToMine, okToRelay);
    }

    // NOTE: we do retrieve scriptPubKey from getUtxos - we do not need it and it might be large
    static readonly string[] getTxOutFields = { "scriptPubKeyLen", "value", "isStandard", "confirmations" };

    public static bool IsConsolidationTxn(Transaction transaction, ConsolidationTxParameters consolidationParameters, PrevOut[] prevOuts)
    {

      // The consolidation factor zero disables free consolidation txns
      if (consolidationParameters.MinConsolidationFactor == 0)
      {
        return false;
      }

      if (transaction.IsCoinBase)
      {
        return false;
      }

      // The transaction does not decrease #UTXO enough
      if (transaction.Inputs.Count < consolidationParameters.MinConsolidationFactor * transaction.Outputs.Count)
      {
        return false;
      }

      long sumScriptPubKeySizesTxInputs = 0;

      // combine input with corresponding output it is spending
      var pairsInOut = transaction.Inputs.Zip(prevOuts,
        (i, o) =>
          new
          {
            input = i,
            output = o
          });

      foreach (var item in pairsInOut)
      {
        // Transaction has less than minConsInputMaturity confirmations
        if (item.output.Confirmations < consolidationParameters.MinConfConsolidationInput)
        {
          return false;
        }
        // Spam detection
        if (item.input.ScriptSig.Length > consolidationParameters.MaxConsolidationInputScriptSize)
        {
          return false;
        }
        if (!consolidationParameters.AcceptNonStdConsolidationInput && !item.output.IsStandard.Value)
        {
          return false;
        }
        sumScriptPubKeySizesTxInputs += item.output.ScriptPubKeyLength.Value;
      }

      long sumScriptPubKeySizesTxOutputs = transaction.Outputs.Sum(x => x.ScriptPubKey.Length);

      // Size in utxo db does not decrease enough for cons. transaction to be profitable 
      if (sumScriptPubKeySizesTxInputs < consolidationParameters.MinConsolidationFactor * sumScriptPubKeySizesTxOutputs)
      {
        return false;
      }

      return true;
    }

    public static (int failureCount, SubmitTransactionOneResponse[] responses) TransformRpcResponse(RpcSendTransactions rpcResponse, (string tx, string[] warnings)[] allSubmitedTxIds)
    {

      // Track which transaction was already processed, so that we only return one response per txid:
      var processed = new Dictionary<string, object>(StringComparer.InvariantCulture);

      int failed = 0;
      var responses = new List<SubmitTransactionOneResponse>();
      if (rpcResponse.Invalid != null)
      {
        foreach (var invalid in rpcResponse.Invalid)
        {
          if (processed.TryAdd(invalid.Txid, null))
          {
            // ignore RejectCodes - should we add duplicate for resubmit?
            if (invalid.RejectCode.HasValue && NodeRejectCode.MapiSuccessCodes.Contains(invalid.RejectCode.Value))
            {
              responses.Add(new SubmitTransactionOneResponse
              {
                Txid = invalid.Txid,
                ReturnResult = ResultCodes.Success,
                ResultDescription = NodeRejectCode.ResultAlreadyKnown
              });
            }
            else
            {
              var rejectCodeAndReason = NodeRejectCode.CombineRejectCodeAndReason(invalid.RejectCode, invalid.RejectReason);
              responses.Add(new SubmitTransactionOneResponse
              {
                Txid = invalid.Txid,
                ReturnResult = ResultCodes.Failure,
                ResultDescription =
                 NodeRejectCode.MapiRetryCodesAndReasons.Any(x => rejectCodeAndReason.StartsWith(x)) ?
                 NodeRejectCode.MapiRetryMempoolErrorWithDetails(rejectCodeAndReason) : rejectCodeAndReason,
                ConflictedWith = invalid.CollidedWith?.Select(t =>
                  new SubmitTransactionConflictedTxResponse
                  {
                    Txid = t.Txid,
                    Size = t.Size,
                    Hex = t.Hex
                  }
                ).ToArray()
              });

              failed++;
            }
          }
        }
      }

      if (rpcResponse.Evicted != null)
      {
        foreach (var evicted in rpcResponse.Evicted)
        {
          if (processed.TryAdd(evicted, null))
          {
            responses.Add(new SubmitTransactionOneResponse
            {
              Txid = evicted,
              ReturnResult = ResultCodes.Failure,
              // This only happens if mempool is full and contain no P2P transactions (which have low priority)
              ResultDescription = NodeRejectCode.MapiRetryMempoolErrorWithDetails(NodeRejectCode.Evicted),
              Warnings = allSubmitedTxIds.Single(x => x.tx == evicted).warnings,

            });
            failed++;
          }
        }
      }


      if (rpcResponse.Known != null)
      {
        foreach (var known in rpcResponse.Known)
        {
          if (processed.TryAdd(known, null))
          {
            responses.Add(new SubmitTransactionOneResponse
            {
              Txid = known,
              ReturnResult = ResultCodes.Success,
              ResultDescription = NodeRejectCode.ResultAlreadyKnown,
              Warnings = allSubmitedTxIds.Single(x => x.tx == known).warnings
            });
          }
        }
      }

      // If a transaction is not present in response, then it was successfully accepted as a new transaction
      foreach (var (txId, warnings) in allSubmitedTxIds)
      {
        if (!processed.ContainsKey(txId))
        {
          responses.Add(new SubmitTransactionOneResponse
          {
            Txid = txId,
            ReturnResult = ResultCodes.Success,
            Warnings = warnings
          });

        }
      }

      return (failed, responses.ToArray());
    }


    public async Task<QueryTransactionStatusResponse> QueryTransactionAsync(string id, bool merkleProof, string merkleFormat)
    {
      var currentMinerId = await minerId.GetCurrentMinerIdAsync();

      var (result, allTheSame, exception) = await rpcMultiClient.GetRawTransactionAsync(id);

      if (exception != null && result == null) // only report errors none of the nodes return result or if we got RpcException (such as as transaction not found)
      {
        return new QueryTransactionStatusResponse
        {
          Timestamp = clock.UtcNow(),
          Txid = id,
          ReturnResult = "failure",
          ResultDescription = GetSafeExceptionDescription(exception),
          MinerID = currentMinerId,
        };
      }

      // report mixed errors if we got mixed result or if we got some successful results and some RpcException.
      // Ordinary exception might indicate connectivity problems, so we skip them
      if (!allTheSame || (exception as AggregateException)?.GetBaseException() is RpcException)
      {
        return new QueryTransactionStatusResponse
        {
          Timestamp = clock.UtcNow(),
          Txid = id,
          ReturnResult = "failure",
          ResultDescription = "Mixed results",
          MinerID = currentMinerId,
        };
      }

      RpcGetMerkleProof proof1 = null;
      RpcGetMerkleProof2 proof2 = null;
      if (result.Blockhash != null && merkleProof)
      {
        if (merkleFormat == MerkleFormat.TSC)
        {
          proof2 = await rpcMultiClient.GetMerkleProof2Async(result.Blockhash, id);
        }
        else
        {
          proof1 = await rpcMultiClient.GetMerkleProofAsync(id, result.Blockhash);
        }
      }

      return new QueryTransactionStatusResponse
      {
        Timestamp = clock.UtcNow(),
        Txid = id,
        ReturnResult = "success",
        ResultDescription = null,
        BlockHash = result.Blockhash,
        BlockHeight = result.Blockheight,
        Confirmations = result.Confirmations,
        MinerID = currentMinerId,
        MerkleFormat = merkleFormat,
        MerkleProof = proof1,
        MerkleProof2 = proof2,
        //TxSecondMempoolExpiry
      };
    }

    public async Task<SubmitTransactionResponse> SubmitTransactionAsync(SubmitTransaction request, UserAndIssuer user)
    {
      var responseMulti = await SubmitTransactionsAsync(new[] { request }, user);
      if (responseMulti.Txs.Length != 1)
      {
        throw new Exception($"Internal error. Expected exactly 1 transaction in response but got {responseMulti.Txs.Length}");
      }

      var tx = responseMulti.Txs[0];
      return new SubmitTransactionResponse
      {
        Txid = tx.Txid,
        ReturnResult = tx.ReturnResult,
        ResultDescription = tx.ResultDescription,
        Timestamp = responseMulti.Timestamp,
        MinerId = responseMulti.MinerId,
        CurrentHighestBlockHash = responseMulti.CurrentHighestBlockHash,
        CurrentHighestBlockHeight = responseMulti.CurrentHighestBlockHeight,
        TxSecondMempoolExpiry = responseMulti.TxSecondMempoolExpiry,
        Warnings = tx.Warnings,
        ConflictedWith = tx.ConflictedWith
      };
    }

    private static int GetCheckFeesValue(bool okToMine, bool okToRelay)
    {
      // okToMine is more important than okToRelay
      return (okToMine ? 2 : 0) + (okToRelay ? 1 : 0);
    }

    private static void AddFailureResponse(string txId, string errMessage, ref List<SubmitTransactionOneResponse> responses)
    {
      var oneResponse = new SubmitTransactionOneResponse
      {
        Txid = txId,
        ReturnResult = ResultCodes.Failure,
        ResultDescription = errMessage
      };

      responses.Add(oneResponse);
    }

    public async Task<SubmitTransactionsResponse> SubmitTransactionsAsync(IEnumerable<SubmitTransaction> requestEnum, UserAndIssuer user)
    {
      var request = requestEnum.ToArray();
      mapiMetrics.RequestSum.Inc(1);
      if (user != null)
      {
        mapiMetrics.TxAuthenticatedUser.Inc(request.Length);
      }
      else
      {
        mapiMetrics.TxAnonymousUser.Inc(request.Length);
      }
      // Take snapshot of current metadata and use use it for all transactions
      var info = await blockChainInfo.GetInfoAsync();
      var currentMinerId = await minerId.GetCurrentMinerIdAsync();
      var consolidationParameters = info.ConsolidationTxParameters;

      // Use the same quotes for all transactions in single request
      var quotes = feeQuoteRepository.GetValidFeeQuotesByIdentity(user).ToArray();
      if (quotes == null || !quotes.Any())
      {
        throw new Exception("No fee quotes available");
      }

      var responses = new List<SubmitTransactionOneResponse>();

      var transactionsToSubmit = new List<TransactionToSubmit>();

      int failureCount = 0;

      IDictionary<uint256, byte[]> allTxs = new Dictionary<uint256, byte[]>();
      HashSet<string> txsToUpdate = new();

      foreach (var oneTx in request)
      {
        if (! await ValidateTxAsync(user, oneTx, responses, allTxs, txsToUpdate, transactionsToSubmit, quotes, consolidationParameters))
        {
          failureCount++;
        }
      }

      logger.LogTrace($"TransactionsToSubmit: {transactionsToSubmit.Count}: {string.Join("; ", transactionsToSubmit.Select(x => x.TransactionId))} ");

      RpcSendTransactions rpcResponse;

      Exception submitException = null;

      var saveTxsBeforeSendToNode = new List<TransactionToSubmit>();
      if (transactionsToSubmit.Any())
      {
        if (!appSettings.DontInsertTransactions.Value &&
            user != null)
        {
          saveTxsBeforeSendToNode = transactionsToSubmit.Where(x => x.TxStatus < TxStatus.SentToNode).ToList();
          var insertedTxs = (await txRepository.InsertOrUpdateTxsAsync(Faults.DbFaultComponent.MapiBeforeSendToNode,
            saveTxsBeforeSendToNode.Select(x => new Tx
            {
              CallbackToken = x.Transaction.CallbackToken,
              CallbackUrl = x.Transaction.CallbackUrl,
              CallbackEncryption = x.Transaction.CallbackEncryption,
              DSCheck = x.Transaction.DsCheck,
              MerkleProof = x.Transaction.MerkleProof,
              MerkleFormat = x.Transaction.MerkleFormat,
              TxExternalId = new uint256(x.TransactionId),
              TxPayload = x.Transaction.RawTx,
              ReceivedAt = clock.UtcNow(),
              TxIn = x.Transaction.TransactionInputs,
              TxStatus = TxStatus.SentToNode,
              UpdateTx = txsToUpdate.Contains(x.TransactionId) ? Tx.UpdateTxMode.UpdateTx : Tx.UpdateTxMode.Insert,
              PolicyQuoteId = x.PolicyQuote != null ? x.PolicyQuote.Id : quotes.First().Id,
              Policies = x.PolicyQuote?.Policies,
              OkToMine = x.DontCheckFees,
              SetPolicyQuote = x.PolicyQuote != null
            }).ToList(), false, false, true)).Select(x => new uint256(x)).ToList();
          insertedTxs.ForEach(x => txsToUpdate.Add(x.ToString()));
        }

        mapiMetrics.TxSentToNode.Inc(transactionsToSubmit.Count);

        // Submit all collected transactions in one call 
        (rpcResponse, submitException) = await SendTransactions(transactionsToSubmit, Faults.FaultType.SimulateSendTxsMapi);
      }
      else
      {
        // Simulate empty response
        rpcResponse = new RpcSendTransactions();
      }

      // Initialize common fields
      var result = new SubmitTransactionsResponse
      {
        Timestamp = clock.UtcNow(),
        MinerId = currentMinerId,
        CurrentHighestBlockHash = info.BestBlockHash,
        CurrentHighestBlockHeight = info.BestBlockHeight,
        // TxSecondMempoolExpiry
        // Remaining of the fields are initialized bellow

      };

      if (submitException != null)
      {
        mapiMetrics.TxSubmitException.Inc(transactionsToSubmit.Count);
        logger.LogError($"Error while submitting transactions to the node {submitException}");
        // All of the transactions have failed - return error 500 so that user knows, he must retry,
        // but do not expose detailed error message. It might contain internal IPS etc.
        throw new Exception(
          $"Error while submitting transactions to the node - no response or error returned.");
      }
      else // submitted without error
      {
        var (submitFailureCount, transformed) = TransformRpcResponse(rpcResponse,
          transactionsToSubmit.Select(x => (x.TransactionId, x.Warnings.ToArray())).ToArray());

        responses.AddRange(transformed);

        var successfullTxs = transactionsToSubmit.Where(x => transformed.Any(y => y.ReturnResult == ResultCodes.Success && y.Txid == x.TransactionId));
        mapiMetrics.TxAcceptedByNode.Inc(successfullTxs.Count());
        mapiMetrics.TxRejectedByNode.Inc(submitFailureCount);

        if (!appSettings.DontInsertTransactions.Value)
        {
          logger.LogDebug($"Starting with InsertOrUpdateTxsAsync: {successfullTxs.Count()}: {string.Join("; ", successfullTxs.Select(x => x.TransactionId))} (TransactionsToSubmit: {transactionsToSubmit.Count})");

          var watch = System.Diagnostics.Stopwatch.StartNew();
          await txRepository.InsertOrUpdateTxsAsync(Faults.DbFaultComponent.MapiAfterSendToNode, successfullTxs.Select(x => new Tx
          {
            CallbackToken = x.Transaction.CallbackToken,
            CallbackUrl = x.Transaction.CallbackUrl,
            CallbackEncryption = x.Transaction.CallbackEncryption,
            DSCheck = x.Transaction.DsCheck,
            MerkleProof = x.Transaction.MerkleProof,
            MerkleFormat = x.Transaction.MerkleFormat,
            TxExternalId = new uint256(x.TransactionId),
            TxPayload = x.Transaction.RawTx,
            ReceivedAt = clock.UtcNow(),
            TxIn = x.Transaction.TransactionInputs,
            SubmittedAt = clock.UtcNow(),
            TxStatus = x.TxStatus < TxStatus.UnknownOldTx ? TxStatus.Accepted : x.TxStatus,
            UpdateTx = txsToUpdate.Contains(x.TransactionId) ?
                  (x.TxStatus < TxStatus.UnknownOldTx && user == null ? Tx.UpdateTxMode.UpdateTx : Tx.UpdateTxMode.TxStatusAndResubmittedAt) : Tx.UpdateTxMode.Insert,
            PolicyQuoteId = x.PolicyQuote != null ? x.PolicyQuote.Id : quotes.First().Id,
            Policies = x.PolicyQuote?.Policies,
            OkToMine = x.DontCheckFees,
            SetPolicyQuote = x.PolicyQuote != null
          }).ToList(), false, true);
          // if transaction is sent in parallel in two batches, only the first processed tx is saved
          // maybe we could:
          // 1) add on conflict do nothing and return inserted + updated, return failure for missing
          // 2) or select all in batch in db + recheck here if the provided parameters match and return failure ...

          long unconfirmedAncestorsCount = 0;
          var txsWithAncestors = Array.Empty<string>();
          if (rpcResponse.Unconfirmed != null)
          {
            txsWithAncestors = rpcResponse.Unconfirmed.Select(x => x.Txid).ToArray();
            List<Tx> unconfirmedAncestors = new();
            foreach (var unconfirmed in rpcResponse.Unconfirmed)
            {
              unconfirmedAncestors.AddRange(unconfirmed.Ancestors.Select(u => new Tx
              {
                TxExternalId = new uint256(u.Txid),
                ReceivedAt = clock.UtcNow(),
                TxIn = u.Vin.Select(i => new TxInput()
                {
                  PrevTxId = (new uint256(i.Txid)).ToBytes(),
                  PrevN = i.Vout
                }).ToList(),
                TxStatus = TxStatus.Accepted,
                PolicyQuoteId = quotes.First().Id
              })
              );
            }
            // unconfirmedAncestors are only inserted, not updated
            await txRepository.InsertOrUpdateTxsAsync(Faults.DbFaultComponent.MapiUnconfirmedAncestors, unconfirmedAncestors, true);
            unconfirmedAncestorsCount += unconfirmedAncestors.Count;
          }
          // sendrawtxs only returns unconfirmed ancestors on first call
          // if tx was accepted by a node in a previous submit, sendrawtxs returns no ancestors
          // and we have to get them with GetMempoolAncestors
          var txsWithMissingMempoolAncestors = successfullTxs
              .Where(x => x.ListUnconfirmedAncestors && x.TxStatus >= TxStatus.SentToNode && !txsWithAncestors.Contains(x.TransactionId))
              .Select(x => (x.TransactionId, x.PolicyQuote, x.TxStatus)).ToArray();
          foreach (var (transactionId, policyQuote, txstatus) in txsWithMissingMempoolAncestors)
          {
            var (success, count) = await InsertMissingMempoolAncestors(transactionId, policyQuote != null ? policyQuote.Id : quotes.First().Id);
            if (!success)
            {
              responses.RemoveAll(x => x.Txid == transactionId);
              AddFailureResponse(transactionId, NodeRejectCode.UnconfirmedAncestorsError, ref responses);

              failureCount++;
              continue;
            }
            unconfirmedAncestorsCount += count;
          }
          watch.Stop();

          logger.LogDebug($"Finished with InsertTxsAsync: {successfullTxs.Count()} found unconfirmedAncestors {unconfirmedAncestorsCount} took {watch.ElapsedMilliseconds} ms.");

          if (saveTxsBeforeSendToNode.Any())
          {
            var rejectedTxs = saveTxsBeforeSendToNode.Where(x => transformed.Any(y => y.ReturnResult == ResultCodes.Failure && y.Txid == x.TransactionId));
            await txRepository.UpdateTxStatus(rejectedTxs.Select(x => new uint256(x.TransactionId).ToBytes()).ToArray(), TxStatus.NodeRejected);
          }
        }

        result.Txs = responses.ToArray();
        result.FailureCount = failureCount + submitFailureCount;
        mapiMetrics.TxResponseFailure.Inc(result.FailureCount);
        mapiMetrics.TxResponseSuccess.Inc(result.Txs.Length - result.FailureCount);
        return result;
      }
    }

    private async Task<bool> ValidateTxAsync(UserAndIssuer user, 
                                             SubmitTransaction oneTx, 
                                             List<SubmitTransactionOneResponse> responses, 
                                             IDictionary<uint256, byte[]> allTxs, 
                                             HashSet<string> txsToUpdate,
                                             List<TransactionToSubmit> transactionsToSubmit,
                                             FeeQuote[] quotes,
                                             ConsolidationTxParameters consolidationParameters)
    {
      StringBuilder txLog = new();
      if (!string.IsNullOrEmpty(oneTx.MerkleFormat) && !MerkleFormat.ValidFormats.Any(x => x == oneTx.MerkleFormat))
      {
        AddFailureResponse(null, $"Invalid merkle format {oneTx.MerkleFormat}. Supported formats: {String.Join(",", MerkleFormat.ValidFormats)}.", ref responses);
        return false;
      }

      if ((oneTx.RawTx == null || oneTx.RawTx.Length == 0) && string.IsNullOrEmpty(oneTx.RawTxString))
      {
        AddFailureResponse(null, $"{nameof(SubmitTransaction.RawTx)} is required", ref responses);
        return false;
      }

      if (oneTx.RawTx == null)
      {
        try
        {
          oneTx.RawTx = HelperTools.HexStringToByteArray(oneTx.RawTxString);
        }
        catch (Exception ex)
        {
          AddFailureResponse(null, ex.Message, ref responses);
          return false;
        }
      }
      uint256 txId = Hashes.DoubleSHA256(oneTx.RawTx);
      string txIdString = txId.ToString();
      txLog.AppendLine($"Processing transaction: {txIdString}");

      if (oneTx.MerkleProof && (appSettings.DontParseBlocks.Value || appSettings.DontInsertTransactions.Value))
      {
        AddFailureResponse(txIdString, $"Transaction requires merkle proof notification but this instance of mAPI does not support callbacks", ref responses);
        return false;
      }

      if (oneTx.DsCheck && (appSettings.DontParseBlocks.Value || appSettings.DontInsertTransactions.Value))
      {
        AddFailureResponse(txIdString, $"Transaction requires double spend notification but this instance of mAPI does not support callbacks", ref responses);
        return false;
      }

      if (allTxs.ContainsKey(txId))
      {
        AddFailureResponse(txIdString, "Transaction with this id occurs more than once within request", ref responses);
        return false;
      }

      var vc = new ValidationContext(oneTx);
      var errors = oneTx.Validate(vc);
      if (errors.Any())
      {
        AddFailureResponse(txIdString, string.Join(",", errors.Select(x => x.ErrorMessage)), ref responses);
        return false;
      }
      allTxs.Add(txId, oneTx.RawTx);
      bool okToMine = false;
      bool okToRelay = false;
      PolicyQuote selectedQuote = null;
      List<string> warnings = new();
      var txStatus = await txRepository.GetTransactionStatusAsync(txId.ToBytes());

      if (txStatus > TxStatus.NotPresentInDb)
      {
        txsToUpdate.Add(txIdString);
        // if txstatus NotInDb or NodeRejected, proceed with regular feeQuote calculation
        if (txStatus != TxStatus.NodeRejected)
        {
          // for other skip feeQuote calculation
          if (txStatus == TxStatus.SentToNode)
          {
            logger.LogInformation($"Transaction {txIdString} marked as SentToNode. Will resubmit to node.");
          }
          else if (appSettings.ResubmitKnownTransactions.Value)
          {
            logger.LogInformation($"Transaction {txIdString} already known (txstatus={txStatus}. Will resubmit to node.");
          }

          var tx = await txRepository.GetTransactionAsync(txId.ToBytes());
          if (oneTx.CallbackUrl != tx.CallbackUrl ||
              oneTx.MerkleProof != tx.MerkleProof ||
              oneTx.DsCheck != tx.DSCheck ||
              ((txStatus != TxStatus.UnknownOldTx) && (user?.Identity != tx.Identity || user?.IdentityProvider != tx.IdentityProvider))
             )
          {
            AddFailureResponse(txIdString, "Transaction already submitted with different parameters.", ref responses);
            return false;
          }

          // check for warnings
          PolicyQuote policyQuote = new() { Id = tx.PolicyQuoteId.Value, Policies = tx.Policies };
          var txParsed = HelperTools.ParseBytesToTransaction(oneTx.RawTx);
          bool listUnconfirmedAncestors = await FillInputsAndListUnconfirmedAncestorsAsync(oneTx, txParsed);
          CheckOutputsSumAndValidateDs(txParsed, oneTx.DsCheck, txStatus, warnings);

          if (txStatus == TxStatus.UnknownOldTx)
          {
            if (!appSettings.ResubmitKnownTransactions.Value)
            {
              responses.Add(new SubmitTransactionOneResponse
              {
                Txid = txIdString,
                ReturnResult = ResultCodes.Success,
                ResultDescription = NodeRejectCode.ResultAlreadyKnown,
                Warnings = warnings.ToArray()
              });
              return true;
            }
            // we don't have actual user or feeQuote saved for the unknownOldTxs
            // and we cannot always define feequote (valid feeQuote can be expired or sumPrevOuputs = 0)
            // so we resend it to node as with dontcheckfees
            transactionsToSubmit.Add(new(txIdString, oneTx, false, true, false, null, txStatus, warnings));
          }
          else if (txStatus >= TxStatus.Accepted && !appSettings.ResubmitKnownTransactions.Value)
          {
            if (listUnconfirmedAncestors)
            {
              var (success, count) = await InsertMissingMempoolAncestors(txIdString, tx.PolicyQuoteId.Value);
              if (!success)
              {
                AddFailureResponse(txIdString, NodeRejectCode.UnconfirmedAncestorsError, ref responses);
                return false;
              }
            }
            responses.Add(new SubmitTransactionOneResponse
            {
              Txid = txIdString,
              ReturnResult = ResultCodes.Success,
              ResultDescription = NodeRejectCode.ResultAlreadyKnown,
              Warnings = warnings.ToArray()
            });
          }
          else
          {
            transactionsToSubmit.Add(new (txIdString, oneTx, false, tx.OkToMine, listUnconfirmedAncestors, tx.SetPolicyQuote ? policyQuote : null, txStatus, warnings));
          }
          return true;
        }
      }

      Transaction transaction = null;
      CollidedWith[] colidedWith = Array.Empty<CollidedWith>();
      Exception exception = null;
      string[] prevOutsErrors = Array.Empty<string>();
      try
      {
        transaction = HelperTools.ParseBytesToTransaction(oneTx.RawTx);

        if (transaction.IsCoinBase)
        {
          throw new ExceptionWithSafeErrorMessage("Invalid transaction - coinbase transactions are not accepted");
        }
        var (sumPrevOuputs, prevOuts) = await CollectPreviousOuputs(transaction, new ReadOnlyDictionary<uint256, byte[]>(allTxs), rpcMultiClient);

        prevOutsErrors = prevOuts.Where(x => !string.IsNullOrEmpty(x.Error)).Select(x => x.Error).ToArray();
        colidedWith = prevOuts.Where(x => x.CollidedWith != null && !String.IsNullOrEmpty(x.CollidedWith.Hex)).Select(x => x.CollidedWith).Distinct(new CollidedWithComparer()).ToArray();
        txLog.AppendLine($"CollectPreviousOuputs for {txIdString} returned {prevOuts.Length} prevOuts ({prevOutsErrors.Length} prevOutsErrors, {colidedWith.Length} colidedWith).");

        if (appSettings.CheckFeeDisabled.Value)
        {
          txLog.AppendLine("No checkFees, CheckFeeDisabled.");
          (okToMine, okToRelay) = (true, true);
        }
        else
        {
          (Money sumNewOutputs, long dataBytes) = CheckOutputsSumAndValidateDs(transaction, oneTx.DsCheck, txStatus, warnings);

          if (warnings.Any())
          {
            txLog.AppendLine($"CheckOutputsSumAndValidateDs returned warnings: '{string.Join(",", warnings)}'.");
          }
          foreach (var policyQuote in quotes)
          {
            if (IsConsolidationTxn(transaction, policyQuote.GetMergedConsolidationTxParameters(consolidationParameters), prevOuts))
            {
              txLog.AppendLine($"Determined as ConsolidationTxn.");
              (okToMine, okToRelay, selectedQuote) = (true, true, policyQuote);
              break;
            }
            var (okToMineTmp, okToRelayTmp) =
              CheckFees(oneTx.RawTx.LongLength, sumPrevOuputs, sumNewOutputs, dataBytes, policyQuote);
            if (GetCheckFeesValue(okToMineTmp, okToRelayTmp) > GetCheckFeesValue(okToMine, okToRelay))
            {
              // save best combination 
              (okToMine, okToRelay, selectedQuote) = (okToMineTmp, okToRelayTmp, policyQuote);
            }
          }
          txLog.AppendLine($"Finished with CheckFees calculation for {txIdString} and {quotes.Length} quotes: " +
            $"{(okToMine, okToRelay, selectedQuote?.PoliciesDict == null ? "" : string.Join(";", selectedQuote.PoliciesDict.Select(x => x.Key + "=" + x.Value)))}.");
        }
        logger.LogDebug(txLog.ToString());
      }
      catch (Exception ex)
      {
        logger.LogInformation(txLog.ToString());
        exception = ex;
      }

      if (exception != null || colidedWith.Any() || transaction == null || prevOutsErrors.Any())
      {

        var oneResponse = new SubmitTransactionOneResponse
        {
          Txid = txIdString,
          ReturnResult = ResultCodes.Failure,
          // Include non null ConflictedWith only if a collision has been detected
          ConflictedWith = !colidedWith.Any() ? null : colidedWith.Select(
            x => new SubmitTransactionConflictedTxResponse
            {
              Txid = x.TxId,
              Size = x.Size,
              Hex = x.Hex,
            }).ToArray()
        };

        if (oneResponse.ConflictedWith != null && oneResponse.ConflictedWith.Any(c => c.Txid == oneResponse.Txid))
        {
          // Transaction already in the mempool
          // should result in "success known"
          (okToMine, okToRelay) = (true, true);
        }
        else
        {
          if (transaction is null)
          {
            oneResponse.ResultDescription = "Can not parse transaction";
          }
          else if (exception is ExceptionWithSafeErrorMessage)
          {
            oneResponse.ResultDescription = exception.Message;
          }
          else if (exception != null)
          {
            oneResponse.ResultDescription = "Error fetching inputs";
          }
          else
          {
            // return "Missing inputs" regardless of error returned from gettxouts (which is usually "missing")
            oneResponse.ResultDescription = "Missing inputs";
          }
          logger.LogError($"Can not calculate fee for {txIdString}. Error: {oneResponse.ResultDescription} Exception: {exception?.ToString() ?? ""}");


          responses.Add(oneResponse);
          return false;
        }
      }

      // Transaction was successfully analyzed
      if (!okToMine && !okToRelay)
      {
        AddFailureResponse(txIdString, "Not enough fees", ref responses);
        return false;
      }
      bool allowHighFees = false;
      bool dontcheckfee = okToMine;
      bool unconfirmedAncestors = await FillInputsAndListUnconfirmedAncestorsAsync(oneTx, transaction);

      transactionsToSubmit.Add(new(txIdString, oneTx, allowHighFees, dontcheckfee, unconfirmedAncestors, selectedQuote, txStatus, warnings));
      return true;
    }

    private async Task<(bool, int)> InsertMissingMempoolAncestors(string txId, long policyQuoteId)
    {
      if (appSettings.DontInsertTransactions.Value)
      {
        return (true, 0);
      }
      // situation where tx is present in db, but unconfirmedancestors are missing, should be rare
      // we can only get mempool ancestors of transaction, that is present in mempool
      try
      {
        var mempoolAncestors = await rpcMultiClient.GetMempoolAncestors(txId);
        List<Tx> unconfirmedAncestors = new();
        unconfirmedAncestors.AddRange(mempoolAncestors.Transactions.Select(u => new Tx
        {
          TxExternalId = new uint256(u.Key),
          ReceivedAt = clock.UtcNow(),
          TxIn = u.Value.Depends.Select((input, i) => new TxInput()
          {
            PrevTxId = (new uint256(input)).ToBytes(),
            PrevN = i
          }).ToList(),
          TxStatus = TxStatus.Accepted,
          PolicyQuoteId = policyQuoteId
        })
        );
        logger.LogInformation($"GetMempoolAncestors returned {unconfirmedAncestors.Count} transactions.");
        await txRepository.InsertOrUpdateTxsAsync(Faults.DbFaultComponent.MapiUnconfirmedAncestors, unconfirmedAncestors, true);
        return (true, unconfirmedAncestors.Count);
      }
      catch (RpcException ex)
      {
        logger.LogInformation($"GetMempoolAncestors finished with rpcException for {txId}: {ex.Message}");
        return (ex.Code == -5, 0); // -5 is code for error: Transaction not in mempool
      }
      catch (Exception ex)
      {
        logger.LogInformation($"GetMempoolAncestors finished with exception for {txId}: {ex.Message}");
        return (false, 0);
      }
    }

    private async Task<(RpcSendTransactions, Exception)> SendTransactions(
      List<TransactionToSubmit> transactionsToSubmit,
      Faults.FaultType faultType)
    {
      return await SendRawTransactions(
         transactionsToSubmit.Select(x => (x.Transaction.RawTx, x.AllowHighFees, x.DontCheckFees, x.ListUnconfirmedAncestors, x.PolicyQuote?.PoliciesDict))
            .ToArray(), faultType);
    }

    public virtual async Task<(RpcSendTransactions, Exception)> SendRawTransactions(
        (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactions, Faults.FaultType faultType)
    {
      RpcSendTransactions rpcResponse;
      Exception submitException = null;

      // Submit all collected transactions in one call
      try
      {
        var simulateSendTxsResponse = await faultInjection.SimulateSendTxsResponseAsync(faultType);
        if (simulateSendTxsResponse != null)
        {
          submitException = new("Node returned error.");
          switch (simulateSendTxsResponse)
          {
            case Faults.SimulateSendTxsResponse.NodeFailsWhenSendRawTxs:
              return (null, submitException);
            case Faults.SimulateSendTxsResponse.NodeReturnsNonStandard:
              return (
                IFaultMapi.CreateRpcInvalidResponse(transactions.Select(x => x.transaction).ToArray(),
                                        NodeRejectCode.MapiRetryCodesAndReasons[0]),
                null);
            case Faults.SimulateSendTxsResponse.NodeReturnsInsufficientFee:
              return (
                IFaultMapi.CreateRpcInvalidResponse(transactions.Select(x => x.transaction).ToArray(),
                                        NodeRejectCode.MapiRetryCodesAndReasons[1]),
                null);
            case Faults.SimulateSendTxsResponse.NodeReturnsMempoolFull:
              return (
                IFaultMapi.CreateRpcInvalidResponse(transactions.Select(x => x.transaction).ToArray(),
                                        NodeRejectCode.MapiRetryCodesAndReasons[2]),
                null);
            case Faults.SimulateSendTxsResponse.NodeReturnsMempoolFullNonFinal:
              return (
                IFaultMapi.CreateRpcInvalidResponse(transactions.Select(x => x.transaction).ToArray(),
                                        NodeRejectCode.MapiRetryCodesAndReasons[3]),
                null);
            case Faults.SimulateSendTxsResponse.NodeReturnsEvicted:
              return (IFaultMapi.CreateRpcEvictedResponse(transactions.Select(x => x.transaction).ToArray()), null);
            case Faults.SimulateSendTxsResponse.NodeFailsAfterSendRawTxs:
              // returns success but txs are immediately lost from mempool
              return (new RpcSendTransactions(), null);
            default:
              throw new Exception("Invalid SimulateSendTxsResponse.");
          }
        }

        rpcResponse = await rpcMultiClient.SendRawTransactionsAsync(transactions);
      }
      catch (Exception ex)
      {
        submitException = ex;
        rpcResponse = null;
      }
      return (rpcResponse, submitException);
    }

    private async Task<bool> FillInputsAndListUnconfirmedAncestorsAsync(SubmitTransaction oneTx, Transaction transaction)
    {
      oneTx.TransactionInputs = transaction.Inputs.AsIndexedInputs().Select(x => new TxInput
      {
        N = x.Index,
        PrevN = x.PrevOut.N,
        PrevTxId = x.PrevOut.Hash.ToBytes()
      }).ToList();
      if (oneTx.DsCheck)
      {
        foreach (TxInput txInput in oneTx.TransactionInputs)
        {
          var prevOut = await txRepository.GetPrevOutAsync(txInput.PrevTxId, txInput.PrevN);
          if (prevOut == null)
          {
            return true;
          }
        }
      }
      return false;
    }


    public virtual async Task<(bool success, List<long> txsWithMissingInputs)> ResubmitMissingTransactionsAsync(string[] mempoolTxs, DateTime? resubmittedAt, int batchSize = 1000)
    {
      Tx[] txs;
      using (mempoolCheckerMetrics.GetMissingTransactionsDuration.NewTimer())
      {
        txs = await txRepository.GetMissingTransactionsAsync(mempoolTxs, resubmittedAt);
      }
      // split processing into smaller batches
      int nBatches = (int)Math.Ceiling((double)txs.Length / batchSize);
      int submitSuccessfulCount = 0;
      int submitFailureIgnored = 0;
      List<long> txsWithMissingInputs = new();
      logger.LogDebug($"ResubmitMissingTransactions: missing {txs.Length} -> nBatches: {nBatches}, batchsize: {batchSize}");
      mempoolCheckerMetrics.TxMissing.Inc(txs.Length);

      // we have to submit all txs in order
      // if node accepted tx2 before tx1, tx1 can be resubmitted successfully in the next resubmit round
      for (int n = 0; n < nBatches; n++)
      {
        var txsToSubmit = txs.Skip(n * batchSize).Take(batchSize).ToArray();
        (byte[] transaction, bool allowhighfees, bool dontCheckFee, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactions;
        // we allow certain errors - check with prevOut, do not submit this
        if (appSettings.MempoolCheckerMissingInputsRetries.Value == 0)
        {
          // maybe we could also simplify and limit MempoolCheckerMissingInputsRetries to min = 1
          IDictionary<uint256, byte[]> allTxs = new Dictionary<uint256, byte[]>();
          foreach (var tx in txsToSubmit)
          {
            allTxs.Add(tx.TxExternalId, tx.TxPayload);
            var transaction = HelperTools.ParseBytesToTransaction(tx.TxPayload);
            try
            {
              var (sumPrevOuputs, prevOuts) = await CollectPreviousOuputs(transaction, new ReadOnlyDictionary<uint256, byte[]>(allTxs), rpcMultiClient);

              var prevOutsErrors = prevOuts.Where(x => !string.IsNullOrEmpty(x.Error)).Select(x => x.Error).ToArray();
              var colidedWith = prevOuts.Where(x => x.CollidedWith != null && !String.IsNullOrEmpty(x.CollidedWith.Hex)).Select(x => x.CollidedWith).Distinct(new CollidedWithComparer()).ToArray();
              if (colidedWith.Any() || prevOutsErrors.Any())
              {
                txsWithMissingInputs.Add(tx.TxInternalId);
              }
            }
            catch (Exception ex)
            {
              logger.LogDebug($"ResubmitMissingTransactions: Error fetching inputs ({ex.Message})");
            }
          }
          transactions = txsToSubmit.Where(x => !txsWithMissingInputs.Contains(x.TxInternalId)).Select(x => (x.TxPayload, false, x.OkToMine, false, x.PoliciesDict)).ToArray();
          if (!transactions.Any())
          {
            continue;
          }
        }
        else
        {
          transactions = txsToSubmit.Select(x => (x.TxPayload, false, x.OkToMine, false, x.PoliciesDict)).ToArray();
        }

        var (rpcResponse, submitException) = await SendRawTransactions(transactions, Faults.FaultType.SimulateSendTxsMempoolChecker);
        if (submitException != null)
        {
          logger.LogError($"Error while resubmitting transactions: {submitException}");
        }
        else
        {
          // update successful resubmits
          var (_, transformed) = TransformRpcResponse(rpcResponse,
            txsToSubmit.Select(x => (x.TxExternalId.ToString(), Array.Empty<string>())).ToArray());
          var successfullTxs = txsToSubmit.Where(x => transformed.Any(y => y.ReturnResult == ResultCodes.Success && y.Txid == x.TxExternalId.ToString()));
          submitSuccessfulCount += successfullTxs.Count();
          await txRepository.UpdateTxsOnResubmitAsync(Faults.DbFaultComponent.MempoolCheckerUpdateTxs, successfullTxs.Select(x => new Tx
          {
            // on resubmit we only update submittedAt and txStatus
            TxInternalId = x.TxInternalId,
            TxExternalId = x.TxExternalId,
            SubmittedAt = clock.UtcNow(),
            TxStatus = x.TxStatus,
            PolicyQuoteId = x.PolicyQuoteId,
            UpdateTx = Tx.UpdateTxMode.TxStatusAndResubmittedAt
          }).ToList());

          // we allow certain errors
          txsWithMissingInputs.AddRange(txsToSubmit.Where(x => transformed.Any(
            y => y.ReturnResult == ResultCodes.Failure && NodeRejectCode.IsResponseOfTypeMissingInputs(y.ResultDescription) && y.Txid == x.TxExternalId.ToString())
          ).Select(x => x.TxInternalId));

          foreach (var response in transformed.Where
            (
            x => x.ReturnResult == ResultCodes.Failure &&
            !(NodeRejectCode.IsResponseOfTypeMissingInputs(x.ResultDescription) ||
               x.ResultDescription.StartsWith(NodeRejectCode.MapiRetryMempoolError))
            )
          )
          {
            // unexpected failures (e.g. node settings changed) - this failure will probably persist on resubmit
            logger.LogWarning($"ResubmitMempoolTransactions: {response.Txid} failed with {response.ResultDescription}. Ignored.");
            submitFailureIgnored++;
          }
        }
      }
      int failures = txs.Length - submitSuccessfulCount - submitFailureIgnored - txsWithMissingInputs.Count;
      logger.LogInformation(@$"ResubmitMempoolTransactions: resubmitted {txs.Length} txs = successful: {submitSuccessfulCount}, 
failures: {failures}, submitFailureIgnored: {submitFailureIgnored}, missing inputs: {txsWithMissingInputs.Count}.");
      mempoolCheckerMetrics.TxResponseSuccess.Inc(submitSuccessfulCount);
      mempoolCheckerMetrics.TxResponseFailure.Inc(failures);

      return (failures == 0, txsWithMissingInputs);
    }

    public SubmitTxStatus GetSubmitTxStatus()
    {
      return new SubmitTxStatus(mapiMetrics.RequestSum.Value, mapiMetrics.TxAuthenticatedUser.Value, mapiMetrics.TxAnonymousUser.Value,
        mapiMetrics.TxSentToNode.Value, mapiMetrics.TxAcceptedByNode.Value, mapiMetrics.TxRejectedByNode.Value, mapiMetrics.TxSubmitException.Value,
        mapiMetrics.TxResponseSuccess.Value, mapiMetrics.TxResponseFailure.Value);
    }

    public async Task<TxOutsResponse> GetTxOutsAsync(IEnumerable<(string txId, long n)> utxos, string[] returnFields, bool includeMempool)
    {
      var currentMinerId = await minerId.GetCurrentMinerIdAsync();

      var (result, allTheSame, exception) = await rpcMultiClient.GetTxOutsAsync(utxos, returnFields, includeMempool);

      if (exception != null && result == null) // only report errors none of the nodes return result or if we got RpcException
      {
        return new TxOutsResponse
        {
          Timestamp = clock.UtcNow(),
          ReturnResult = "failure",
          ResultDescription = GetSafeExceptionDescription(exception),
          MinerID = currentMinerId,
        };
      }

      // report mixed errors if we got mixed result or if we got some successful results and some RpcException.
      // Ordinary exception might indicate connectivity problems, so we skip them
      if (!allTheSame || (exception as AggregateException)?.GetBaseException() is RpcException)
      {
        return new TxOutsResponse
        {
          Timestamp = clock.UtcNow(),
          ReturnResult = "failure",
          ResultDescription = "Mixed results",
          MinerID = currentMinerId,
        };
      }

      return new TxOutsResponse
      {
        Timestamp = clock.UtcNow(),
        ReturnResult = "success",
        ResultDescription = null,
        MinerID = currentMinerId,
        TxOuts = result.TxOuts.Select(t => new TxOutResponse()
        {
          Error = t.Error,
          CollidedWith = t.CollidedWith == null ? null : new TxOutCollidedWith() { TxId = t.CollidedWith.TxId, Size = t.CollidedWith.Size, Hex = t.CollidedWith.Hex },
          ScriptPubKey = t.ScriptPubKey,
          ScriptPubKeyLen = t.ScriptPubKeyLength,
          Value = t.Value,
          IsStandard = t.IsStandard,
          Confirmations = t.Confirmations
        }).ToArray()
      };
    }

  }
}
