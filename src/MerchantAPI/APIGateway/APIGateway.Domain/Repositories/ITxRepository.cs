// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Repositories
{
  public interface ITxRepository : IFaultTxRepository
  {
    Task<byte[][]> InsertOrUpdateTxsAsync(IList<Tx> transactions, bool areUnconfirmedAncestors, bool insertTxInputs = true, bool resubmit = false);

    Task<long?> InsertOrUpdateBlockAsync(Block block);

    Task<bool> SetOnActiveChainBlockAsync(long blockHeight, byte[] blockHash);

    Task InsertTxBlockAsync(IList<long> txInternalId, long blockInternalId);

    Task CheckAndInsertBlockDoubleSpendAsync(IEnumerable<TxWithInput> txWithInputs, long deltaBlockHeight, long blockInternalId);

    Task<int> InsertMempoolDoubleSpendAsync(long txInternalId, byte[] dsTxId, byte[] dsTxPayload);

    Task UpdateDsTxPayloadAsync(byte[] dsTxId, byte[] txPayload);

    Task SetBlockParsedForMerkleDateAsync(long blockInternalId);

    Task SetBlockParsedForDoubleSpendDateAsync(long blockInternalId);

    Task<int> InsertBlockDoubleSpendAsync(long txInternalId, byte[] blockhash, byte[] dsTxId, byte[] dsTxPayload);

    Task SetNotificationSendDateAsync(string notificationType, long txInternalId, long blockInternalId, byte[] dsTxId, DateTime sendDate);

    Task SetNotificationErrorAsync(byte[] txId, string notificationType, string errorMessage, int errorCount);

    Task MarkUncompleteNotificationsAsFailedAsync();

    Task<IEnumerable<NotificationData>> GetTxsToSendMerkleProofNotificationsAsync(long skip, long fetch);

    Task<NotificationData> GetTxToSendMerkleProofNotificationAsync(byte[] txId);

    Task<IEnumerable<(byte[] dsTxId, byte[] TxId)>> GetDSTxWithoutPayloadAsync(bool unconfirmedAncestors);

    Task InsertBlockDoubleSpendForAncestorAsync(byte[] ancestorTxId);

    Task<IEnumerable<NotificationData>> GetTxsToSendBlockDSNotificationsAsync();

    Task<NotificationData> GetTxToSendBlockDSNotificationAsync(byte[] txId);

    Task<IEnumerable<NotificationData>> GetTxsToSendMempoolDSNotificationsAsync();

    Task<IEnumerable<Tx>> GetTxsNotInCurrentBlockChainAsync(long blockInternalId);

    Task<IEnumerable<Tx>> GetTxsForDSCheckAsync(IEnumerable<byte[]> txExternalIds, bool checkDSAttempt);
    
    Task<Block> GetBestBlockAsync();
    
    Task<Block> GetBlockAsync(byte[] blockHash);

    Task<Tx> GetTransactionAsync(byte[] txId);

    Task<int> GetTransactionStatusAsync(byte[] txId);

    Task<Tx[]> GetMissingTransactionsAsync(string[] mempoolTxs, DateTime? resubmittedAt = null);

    Task UpdateTxStatus(IList<byte[]> txExternalIds, int txstatus);

    Task<List<NotificationData>> GetNotificationsWithErrorAsync(int errorCount, int skip, int fetch);

    Task<byte[]> GetDoublespendTxPayloadAsync(string notificationType, long txInternalId);

    Task<long?> GetTransactionInternalIdAsync(byte[] txId);

    Task<(int blocks, long txs, int mempoolTxs)> CleanUpTxAsync(DateTime fromDate, DateTime mempoolExpiredDate);

    Task<PrevTxOutput> GetPrevOutAsync(byte[] prevOutTxId, long prevOutN);

    Task<NotificationData[]> GetNotificationsForTestsAsync();
    
    Task<Block[]> GetUnparsedBlocksAsync();

    Task<bool> CheckIfBlockWasParsedAsync(long blockInternalId);

    Task<Tx[]> GetTxsWithFeeQuotesAsync(FeeQuote[] feeQuotes);

    Task<int> DeleteTxsWithFeeQuotesAsync(FeeQuote[] feeQuotes);
  }
}
