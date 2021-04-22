// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Repositories
{
  public interface ITxRepository
  {
    Task InsertTxsAsync(IList<Tx> transactions, bool areUnconfirmedAncestors);

    Task<long?> InsertBlockAsync(Block block);

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

    Task<bool> TransactionExistsAsync(byte[] txId);
    
    Task<List<NotificationData>> GetNotificationsWithErrorAsync(int errorCount, int skip, int fetch);

    Task<byte[]> GetDoublespendTxPayloadAsync(string notificationType, long txInternalId);

    Task<long?> GetTransactionInternalId(byte[] txId);

    Task CleanUpTxAsync(DateTime fromDate);

  }
}
