// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Faults;
using MerchantAPI.APIGateway.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional.Mock
{
  public class TxRepositoryMock : ITxRepository
  {
    // only simple operations are covered in tx mock
    // txRepositoryPostgres has many additional validation on database directly, that would be hard to maintain in mock

    readonly IFeeQuoteRepository feeQuoteRepository;
    readonly IFaultInjection faultInjection;
    readonly Dictionary<NBitcoin.uint256, Tx> _txs;
    int seq = 0;

    public TxRepositoryMock(IFeeQuoteRepository feeQuoteRepository, IFaultInjection faultInjection)
    {
      this.feeQuoteRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
      this.faultInjection = faultInjection ?? throw new ArgumentNullException(nameof(faultInjection));
      _txs = new();
    }

    public Task CheckAndInsertBlockDoubleSpendAsync(IEnumerable<TxWithInput> txWithInputs, long deltaBlockHeight, long blockInternalId)
    {
      throw new NotImplementedException();
    }

    public Task<bool> CheckIfBlockWasParsedAsync(long blockInternalId)
    {
      throw new NotImplementedException();
    }

    public Task<(int blocks, long txs, int mempoolTxs)> CleanUpTxAsync(DateTime fromDate, DateTime mempoolExpiredDate)
    {
      throw new NotImplementedException();
    }

    public Task<int> DeleteTxsWithFeeQuotesAsync(FeeQuote[] feeQuotes)
    {
      throw new NotImplementedException();
    }

    public Task<Block> GetBestBlockAsync()
    {
      return Task.FromResult((Block)null);
    }

    public Task<Block> GetBlockAsync(byte[] blockHash)
    {
      throw new NotImplementedException();
    }

    public Task<byte[]> GetDoublespendTxPayloadAsync(string notificationType, long txInternalId)
    {
      throw new NotImplementedException();
    }

    public Task<IEnumerable<(byte[] dsTxId, byte[] TxId)>> GetDSTxWithoutPayloadAsync(bool unconfirmedAncestors)
    {
      throw new NotImplementedException();
    }

    public Task<Tx[]> GetMissingTransactionsAsync(string[] mempoolTxs, DateTime? resubmittedAt = null)
    {
      throw new NotImplementedException();
    }

    public Task<NotificationData[]> GetNotificationsForTestsAsync()
    {
      throw new NotImplementedException();
    }

    public Task<List<NotificationData>> GetNotificationsWithErrorAsync(int errorCount, int skip, int fetch)
    {
      throw new NotImplementedException();
    }

    public Task<PrevTxOutput> GetPrevOutAsync(byte[] prevOutTxId, long prevOutN)
    {
      throw new NotImplementedException();
    }

    public Task<Tx> GetTransactionAsync(byte[] txId)
    {
      var tx = _txs.GetValueOrDefault(new NBitcoin.uint256(txId), null);
      return Task.FromResult(tx);
    }

    public Task<long?> GetTransactionInternalIdAsync(byte[] txId)
    {
      throw new NotImplementedException();
    }

    public Task<int> GetTransactionStatusAsync(byte[] txId)
    {
      var tx = _txs.GetValueOrDefault(new NBitcoin.uint256(txId), null);
      return Task.FromResult(tx == null ? TxStatus.NotPresentInDb : tx.TxStatus);
    }

    public Task<IEnumerable<Tx>> GetTxsForDSCheckAsync(IEnumerable<byte[]> txExternalIds, bool checkDSAttempt)
    {
      throw new NotImplementedException();
    }

    public Task<IEnumerable<Tx>> GetTxsNotInCurrentBlockChainAsync(long blockInternalId)
    {
      throw new NotImplementedException();
    }

    public Task<IEnumerable<NotificationData>> GetTxsToSendBlockDSNotificationsAsync()
    {
      throw new NotImplementedException();
    }

    public Task<IEnumerable<NotificationData>> GetTxsToSendMempoolDSNotificationsAsync()
    {
      throw new NotImplementedException();
    }

    public Task<IEnumerable<NotificationData>> GetTxsToSendMerkleProofNotificationsAsync(long skip, long fetch)
    {
      throw new NotImplementedException();
    }

    public Task<Tx[]> GetTxsWithFeeQuotesAsync(FeeQuote[] feeQuotes)
    {
      throw new NotImplementedException();
    }

    public Task<NotificationData> GetTxToSendBlockDSNotificationAsync(byte[] txId)
    {
      throw new NotImplementedException();
    }

    public Task<NotificationData> GetTxToSendMerkleProofNotificationAsync(byte[] txId)
    {
      throw new NotImplementedException();
    }

    public Task<Block[]> GetUnparsedBlocksAsync()
    {
      return Task.FromResult(Array.Empty<Block>());
    }

    public Task<int> InsertBlockDoubleSpendAsync(long txInternalId, byte[] blockhash, byte[] dsTxId, byte[] dsTxPayload)
    {
      throw new NotImplementedException();
    }

    public Task InsertBlockDoubleSpendForAncestorAsync(byte[] ancestorTxId)
    {
      throw new NotImplementedException();
    }

    public Task<int> InsertMempoolDoubleSpendAsync(long txInternalId, byte[] dsTxId, byte[] dsTxPayload)
    {
      throw new NotImplementedException();
    }

    public Task<long?> InsertOrUpdateBlockAsync(Block block)
    {
      throw new NotImplementedException();
    }

    public async Task<byte[][]> InsertOrUpdateTxsAsync(IList<Tx> transactions, bool areUnconfirmedAncestors, bool insertTxInputs = true, bool returnInsertedTransactions = false)
    {
      return await InsertOrUpdateTxsAsync(null, transactions, areUnconfirmedAncestors, insertTxInputs);
    }

    public async Task<byte[][]> InsertOrUpdateTxsAsync(Faults.DbFaultComponent? faultComponent, IList<Tx> transactions, bool areUnconfirmedAncestors, bool insertTxInputs = true, bool returnInsertedTransactions = false)
    {
      if ((areUnconfirmedAncestors && transactions.Any()) || (insertTxInputs && transactions.Any(x => x.DSCheck)))
      {
        throw new NotImplementedException();
      }
      List<byte[]> ids = new();
      foreach (Tx tx in transactions)
      {
        var txSaved = _txs.GetValueOrDefault(tx.TxExternalId, null);
        if (txSaved != null)
        {
          tx.TxInternalId = txSaved.TxInternalId;
        }
        else
        {
          tx.TxInternalId = seq++;
        }
        if (tx.PolicyQuoteId == null)
        {
          throw new Exception("Invalid feeQuote: has value null.");
        }
        if (feeQuoteRepository.GetFeeQuoteById(tx.PolicyQuoteId.Value) == null)
        {
          throw new Exception("Invalid feeQuote: Id not present.");
        }
        await faultInjection.FailBeforeSavingUncommittedStateAsync(faultComponent);

        _txs[tx.TxExternalId] = tx;

        await faultInjection.FailAfterSavingUncommittedStateAsync(faultComponent);

        if (returnInsertedTransactions)
        {
          ids.Add(tx.TxExternalIdBytes);
        }
      }
      return ids.ToArray();
    }

    public Task InsertTxBlockAsync(IList<long> txInternalId, long blockInternalId)
    {
      throw new NotImplementedException();
    }

    public Task MarkUncompleteNotificationsAsFailedAsync()
    {
      throw new NotImplementedException();
    }

    public Task SetBlockParsedForDoubleSpendDateAsync(long blockInternalId)
    {
      throw new NotImplementedException();
    }

    public Task SetBlockParsedForMerkleDateAsync(long blockInternalId)
    {
      throw new NotImplementedException();
    }

    public Task SetNotificationErrorAsync(byte[] txId, string notificationType, string errorMessage, int errorCount)
    {
      throw new NotImplementedException();
    }

    public Task SetNotificationSendDateAsync(string notificationType, long txInternalId, long blockInternalId, byte[] dsTxId, DateTime sendDate)
    {
      throw new NotImplementedException();
    }

    public Task<bool> SetOnActiveChainBlockAsync(long blockHeight, byte[] blockHash)
    {
      throw new NotImplementedException();
    }

    public Task UpdateDsTxPayloadAsync(byte[] dsTxId, byte[] txPayload)
    {
      throw new NotImplementedException();
    }

    public Task UpdateTxsOnResubmitAsync(Faults.DbFaultComponent? faultComponent, IList<Tx> transactions)
    {
      throw new NotImplementedException();
    }

    public Task UpdateTxStatus(IList<byte[]> txExternalIds, int txstatus)
    {
      throw new NotImplementedException();
    }
  }
}
