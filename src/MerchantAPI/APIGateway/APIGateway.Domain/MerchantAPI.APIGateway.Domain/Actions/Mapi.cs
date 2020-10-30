// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.BitcoinRpc.Responses;
using Microsoft.Extensions.Logging;
using NBitcoin.Crypto;
using Transaction = NBitcoin.Transaction;
using MerchantAPI.Common.Json;
using System.ComponentModel.DataAnnotations;
using System.Collections.ObjectModel;
using MerchantAPI.Common.Clock;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  
  public class Mapi : IMapi
  {
    IRpcMultiClient rpcMultiClient;
    IFeeQuoteRepository feeQuoteRepository;
    IBlockChainInfo blockChainInfo;
    IMinerId minerId;
    ILogger<Mapi> logger;
    ITxRepository txRepository;
    private readonly IClock clock;

    static class ResultCodes
    {
      public const string Success = "success";
      public const string Failure = "failure";
    }

    public Mapi(IRpcMultiClient rpcMultiClient, IFeeQuoteRepository feeQuoteRepository, IBlockChainInfo blockChainInfo, IMinerId minerId, ITxRepository txRepository, ILogger<Mapi> logger, IClock clock)
    {
      this.rpcMultiClient = rpcMultiClient ?? throw new ArgumentNullException(nameof(rpcMultiClient));
      this.feeQuoteRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
      this.blockChainInfo = blockChainInfo ?? throw new ArgumentNullException(nameof(blockChainInfo));
      this.minerId = minerId ?? throw new ArgumentException(nameof(minerId));
      this.txRepository = txRepository ?? throw new ArgumentException(nameof(txRepository));
      this.logger = logger ?? throw new ArgumentException(nameof(logger));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
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

    static bool IsDataOuput(TxOut output)
    {
      // OP_FALSE OP_RETURN represents data outputs after Genesis was activated. 
      // There is no need to check for output.Value=0.
      // We do not care if somebody wants to burn some satoshis. 
      var scriptBytes = output.ScriptPubKey.ToBytes(@unsafe: true); // unsafe == true -> make sure we do not modify the result

      return scriptBytes.Length > 1 &&
             scriptBytes[0] == (byte)OpcodeType.OP_FALSE &&
             scriptBytes[1] == (byte)OpcodeType.OP_RETURN;
    }

    /// <summary>
    /// Appends source to dest. Duplicates are ignored
    /// </summary>
    void AppendToDictionary<K, V>(Dictionary<K, V> source, Dictionary<K, V> dest)
    {
      foreach (var kv in source)
      {
        dest.TryAdd(kv.Key, kv.Value);
      }
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
    public static  async Task<(Money sumPrevOuputs, PrevOut[] prevOuts)> CollectPreviousOuputs(Transaction tx,
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
        if (additionalTxs!= null &&   additionalTxs.TryGetValue(prevOut.Hash, out var txRaw))
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
        var missing = prevOutsNotInBatch.Select(x => (txId: x.Hash.ToString(), N: (long) x.N)).ToArray();
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
            prevOut =  new PrevOut
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
    /// <param name="txBytesLength">Transactions</param>
    /// <param name="sumPrevOuputs">result of CollectPreviousOutputs. If previous outputs are not found there, the node is consulted</param>
    /// <returns></returns>
    public (bool okToMine, bool okToRely) CheckFees(Transaction transaction, long txBytesLength, Money sumPrevOuputs, FeeQuote feeQuote)
    {
      // This could leave some og the bytes unparsed. In this case we would charge for bytes at the ned of 
      // the stream that will not be published to the blockchain, but this is sender's problem.

      Money sumNewOuputs = Money.Zero;
      long dataBytes = 0;
      foreach (var output in transaction.Outputs)
      {
        sumNewOuputs += output.Value;
        if (output.Value < 0L)
        {
          throw new ExceptionWithSafeErrorMessage("Negative inputs are not allowed");
        }
        if (IsDataOuput(output))
        {
          dataBytes += output.ScriptPubKey.Length;
        }
      }


      long actualFee = (sumPrevOuputs - sumNewOuputs).Satoshi;
      long normalBytes = txBytesLength - dataBytes;

      long feesRequiredMining = 0;
      long feesRequiredRelay = 0;

      foreach (var fee in feeQuote.Fees)
      {
        if (fee.FeeType == "standard")
        {
          feesRequiredMining += (normalBytes * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes;
          feesRequiredRelay += (normalBytes * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes;
        }
        else if (fee.FeeType == "data")
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
    static readonly string[] getTxOutFields =  { "scriptPubKeyLen", "value", "isStandard", "confirmations" };
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

      var outpoints = transaction.Inputs.Select(
        x => (txId: x.PrevOut.Hash.ToString(), N: (long) x.PrevOut.N));

      
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
        if (item.output.Confirmations < consolidationParameters.MinConsolidationInputMaturity)
        {
          return false;
        }
        // Spam detection
        if (item.input.ScriptSig.Length > consolidationParameters.MaxConsolidationInputScriptSize)
        {
          return false;
        }
        if (!consolidationParameters.AcceptNonStdConsolidationInput && !item.output.IsStandard)
        {
          return false;
        }
        sumScriptPubKeySizesTxInputs += item.output.ScriptPubKeyLength;
      }

      long sumScriptPubKeySizesTxOutputs = transaction.Outputs.Sum(x => x.ScriptPubKey.Length);

      // Size in utxo db does not decrease enough for cons. transaction to be profitable 
      if (sumScriptPubKeySizesTxInputs < consolidationParameters.MinConsolidationFactor * sumScriptPubKeySizesTxOutputs)
      {
        return false;
      }

      return true;
    }
    public static (int failureCount, SubmitTransactionOneResponse[] responses) TransformRpcResponse(RpcSendTransactions rpcResponse, string[] allSubmitedTxIds)
    {

      // Track which transaction was already processed, so that we only return one response per txid:
      var processed = new Dictionary<string, object>(StringComparer.InvariantCulture);

      int failed = 0;
      var respones = new List<SubmitTransactionOneResponse>();
      if (rpcResponse.Invalid != null)
      {
        foreach (var invalid in rpcResponse.Invalid)
        {
          if (processed.TryAdd(invalid.Txid, null))
          {
            respones.Add(new SubmitTransactionOneResponse
            {
              Txid = invalid.Txid,
              ReturnResult = ResultCodes.Failure,
              ResultDescription =
                (invalid.RejectCode + " " + invalid.RejectReason).Trim(),

              ConflictedWith = invalid.CollidedWith?.Select(t => 
                new SubmitTransactionConflictedTxResponse { 
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

      if (rpcResponse.Evicted != null)
      {
        foreach (var evicted in rpcResponse.Evicted)
        {
          if (processed.TryAdd(evicted, null))
          {
            respones.Add(new SubmitTransactionOneResponse
            {
              Txid = evicted,
              ReturnResult = ResultCodes.Failure,
              ResultDescription =
                "evicted" // This only happens if mempool is full and contain no P2P transactions (which have low priority)
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
            respones.Add(new SubmitTransactionOneResponse
            {
              Txid = known,
              ReturnResult = ResultCodes.Success,
              ResultDescription =
                "Already known"
            });
          }
        }
      }

      // If a transaction is not present in response, then it was successfully accepted as a new transaction
      foreach (var txId in allSubmitedTxIds)
      {
        if (!processed.ContainsKey(txId))
        {
          respones.Add(new SubmitTransactionOneResponse
          {
            Txid = txId,
            ReturnResult = ResultCodes.Success,
          });

        }
      }

      return (failed, respones.ToArray());
    }


    public async Task<QueryTransactionStatusResponse> QueryTransaction(string id)
    {
      var currentMinerId = await minerId.GetCurrentMinerIdAsync();

      var (result, allTheSame, exception)= await rpcMultiClient.GetRawTransactionAsync(id);

      if (exception != null && result == null) // only report errors none of the nodes return result or if we got RpcExcpetion (such as as transaction not found)
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

      // report mixed errors if we got  mixed result or if we got some successful results and some RpcException.
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

      return new QueryTransactionStatusResponse
      {
        Timestamp = clock.UtcNow(),
        Txid = id,
        ReturnResult = "success",
        ResultDescription = null,
        BlockHash = result.Blockhash,
        BlockHeight = result.Blockheight,
        Confirmations = result.Confirmations,
        MinerID = currentMinerId
        //TxSecondMempoolExpiry 
      };


    }
    public async Task<SubmitTransactionResponse> SubmitTransactionAsync(SubmitTransaction request, UserAndIssuer user)
    {
      var responseMulti = await SubmitTransactionsAsync(new [] {request}, user);
      if (responseMulti.Txs.Length != 1)
      {
        throw new Exception("Internal error. Expected exactly 1 transaction in response but got {responseMulti.Txs.Length}");
      }

      var tx = responseMulti.Txs[0];
      return new SubmitTransactionResponse
      {
        Txid = tx.Txid,
        ReturnResult = tx.ReturnResult,
        ResultDescription = tx.ResultDescription,
        ConflictedWith = tx.ConflictedWith,
        Timestamp = responseMulti.Timestamp,
        MinerId = responseMulti.MinerId,
        CurrentHighestBlockHash = responseMulti.CurrentHighestBlockHash,
        CurrentHighestBlockHeight = responseMulti.CurrentHighestBlockHeight,
        TxSecondMempoolExpiry = responseMulti.TxSecondMempoolExpiry
      };
    }

    private int GetCheckFeesValue(bool okToMine, bool okToRelay)
    {
      // okToMine is more important than okToRelay
      return (okToMine ? 2 : 0) + (okToRelay ? 1 : 0);
    }

    private void AddFailureResponse(string txId, string errMessage, ref List<SubmitTransactionOneResponse> responses)
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
      logger.LogInformation($"Processing {request.Length} incoming transactions");
      // Take snapshot of current metadata and use use it for all transactions
      var info = blockChainInfo.GetInfo();
      var currentMinerId = await minerId.GetCurrentMinerIdAsync();
      var consolidationParameters = info.ConsolidationTxParameters;

      // Use the same quotes for all transactions in single request
      var quotes = feeQuoteRepository.GetValidFeeQuotesByIdentity(user).ToArray();
      if (quotes == null || !quotes.Any())
      {
        throw new Exception("No fee quotes available");
      }

      var responses = new List<SubmitTransactionOneResponse>();
      var transactionsToSubmit = new List<(string transactionId, SubmitTransaction transaction, bool allowhighfees, bool dontCheckFees)>();
      int failureCount = 0;

      IDictionary<uint256, byte[]> allTxs = new Dictionary<uint256, byte[]>();
      foreach (var oneTx in request)
      {
        if (oneTx.RawTx == null)
        {
          try
          {
            oneTx.RawTx = HelperTools.HexStringToByteArray(oneTx.RawTxString);
          }
          catch (Exception ex)
          {
            AddFailureResponse(null, ex.Message, ref responses);

            failureCount++;
            continue;

          }
        }
        uint256 txId = Hashes.DoubleSHA256(oneTx.RawTx);
        string txIdString = txId.ToString();

        if (allTxs.ContainsKey(txId))
        {
          AddFailureResponse(txIdString, "Transaction with this id occurs more than once within request", ref responses);

          failureCount++;
          continue;
        }

        var vc = new ValidationContext(oneTx);
        var errors = oneTx.Validate(vc);
        if (errors.Count() > 0)
        {
          AddFailureResponse(txIdString, string.Join(",", errors.Select(x => x.ErrorMessage)), ref responses);

          failureCount++;
          continue;
        }
        allTxs.Add(txId, oneTx.RawTx);
        bool okToMine = false;
        bool okToRelay = false;
        if (await txRepository.TransactionExistsAsync(txId.ToBytes()))
        {
          AddFailureResponse(txIdString, "Transaction already known", ref responses);

          failureCount++;
          continue;
        }

        Transaction transaction = null;
        CollidedWith[] colidedWith = {};
        Exception exception = null;
        string[] prevOutsErrors = { };
        try
        {
          transaction = HelperTools.ParseBytesToTransaction(oneTx.RawTx);

          if (transaction.IsCoinBase)
          {
            throw new ExceptionWithSafeErrorMessage("Invalid transaction - coinbase transactions are not accepted");
          }
          var (sumPrevOuputs, prevOuts) = await CollectPreviousOuputs(transaction, new ReadOnlyDictionary<uint256, byte[]>(allTxs), rpcMultiClient);

          prevOutsErrors = prevOuts.Where(x => !string.IsNullOrEmpty(x.Error)).Select(x => x.Error).ToArray();
          colidedWith = prevOuts.Where(x => x.CollidedWith != null).Select(x => x.CollidedWith).ToArray();

          if (IsConsolidationTxn(transaction, consolidationParameters, prevOuts))
          {
            (okToMine, okToRelay) = (true, true);
          }
          else
          {
            foreach (var feeQuote in quotes)
            {
              var (okToMineTmp, okToRelayTmp) =
                CheckFees(transaction, oneTx.RawTx.LongLength, sumPrevOuputs, feeQuote);
              if (GetCheckFeesValue(okToMineTmp, okToRelayTmp) > GetCheckFeesValue(okToMine, okToRelay))
              {
                // save best combination 
                (okToMine, okToRelay) = (okToMineTmp, okToRelayTmp);
              }
            }
          }

        }
        catch (Exception ex)
        {
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
          else // colidedWith !=null and there is no exception or prevOutsErrors is not empty
          {
            // return "Missing inputs" regardless of error returned from gettxouts (which is usually "missing")
            oneResponse.ResultDescription = "Missing inputs"; 
          }
          logger.LogError($"Can not calculate fee for {txIdString}. Error: {oneResponse.ResultDescription} Exception: {exception?.ToString() ?? ""}"); 
          

          responses.Add(oneResponse);
          failureCount++;
          continue;
        }

        // Transactions  was successfully analyzed
        if (!okToMine && !okToRelay)
        {
          AddFailureResponse(txIdString, "Not enough fees", ref responses);

          failureCount++;
        }
        else
        {
          bool allowHighFees = false;
          bool dontcheckfee = okToMine; 

          oneTx.TransactionInputs = transaction.Inputs.AsIndexedInputs().Select(x => new TxInput 
                                                                                    { 
                                                                                      N = x.Index, 
                                                                                      PrevN = x.PrevOut.N, 
                                                                                      PrevTxId = x.PrevOut.Hash.ToBytes() 
                                                                                    }).ToList();
          transactionsToSubmit.Add((txIdString, oneTx, allowHighFees, dontcheckfee));
        }
      }

      RpcSendTransactions rpcResponse;

      Exception submitException = null;
      if (transactionsToSubmit.Any())
      {
        // Submit all collected transactions in one call
        try
        {
          rpcResponse = await rpcMultiClient.SendRawTransactionsAsync(
            transactionsToSubmit.Select(x => (x.transaction.RawTx, x.allowhighfees, x.dontCheckFees))
              .ToArray());
        }
        catch (Exception ex)
        {
          submitException = ex;
          rpcResponse = null;
        }
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
        var unableToSubmit = transactionsToSubmit.Select(x =>
          new SubmitTransactionOneResponse
          {
            Txid = x.transactionId,
            ReturnResult = ResultCodes.Failure,
            ResultDescription = "Error while submitting transactions to the node" // do not expose detailed error message. It might contain internal IPS etc
          });

        logger.LogError($"Error while submitting transactions to the node {submitException}");
        responses.AddRange(unableToSubmit);
        result.Txs = responses.ToArray();
        result.FailureCount = result.Txs.Length; // all of the transactions have failed

        return result;
      }
      else // submitted without error
      {
        var (submitFailureCount, transformed ) = TransformRpcResponse(rpcResponse,
          transactionsToSubmit.Select(x => x.transactionId).ToArray());
        responses.AddRange(transformed);
        result.Txs = responses.ToArray();
        result.FailureCount = failureCount + submitFailureCount;

        var successfullTxs = transactionsToSubmit.Where(x => transformed.Any(y => y.ReturnResult == ResultCodes.Success && y.Txid == x.transactionId));
        await txRepository.InsertTxsAsync(successfullTxs.Select(x => new Tx
        {
          CallbackToken = x.transaction.CallBackToken,
          CallbackUrl = x.transaction.CallBackUrl,
          CallbackEncryption = x.transaction.CallBackEncryption,
          DSCheck = x.transaction.DsCheck,
          MerkleProof = x.transaction.MerkleProof,
          TxExternalId = new uint256(x.transactionId),
          TxPayload = x.transaction.RawTx,
          ReceivedAt = clock.UtcNow(),
          TxIn = x.transaction.TransactionInputs
        }).ToList());

        return result;
      }

    }
  }
}
