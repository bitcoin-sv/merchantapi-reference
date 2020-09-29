// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Repositories
{
  public interface ITxRepository
  {
    Task InsertTxsAsync(IList<Tx> transactions);

    Task<long> InsertBlockAsync(Block block);

    Task InsertTxBlockAsync(IList<long> txInternalId, long blockInternalId);

    Task CheckAndInsertBlockDoubleSpendAsync(IEnumerable<TxWithInput> txWithInputs, long deltaBlockHeight, long blockInternalId);

    Task InsertMempoolDoubleSpendAsync(long txInternalId, byte[] dsTxId, byte[] dsTxPayload);

    Task UpdateDsTxPayload(byte[] dsTxId, byte[] txPayload);

    Task SetBlockParsedForMerkleDateAsync(long blockInternalId);

    Task SetBlockParsedForDoubleSpendDateAsync(long blockInternalId);

    Task SetMerkleProofSendDateAsync(long txInternalId, long blockInternalId, DateTime sendDate);

    Task SetBlockDoubleSpendSendDateAsync(long txInternalId, long blockInternalId, byte[] dsTxId, DateTime sendDate);

    Task SetMempoolDoubleSpendSendDateAsync(long txInternalId, byte[] dsTxId, DateTime sendDate);

    Task<IEnumerable<NotificationData>> GetTxsToSendMerkleProofNotificationsAsync(long skip, long fetch);

    Task<NotificationData> GetTxToSendMerkleProofNotificationAsync(byte[] txId);

    Task<IEnumerable<byte[]>> GetDSTxWithoutPayload();

    Task<IEnumerable<NotificationData>> GetTxsToSendBlockDSNotificationsAsync();

    Task<NotificationData> GetTxToSendBlockDSNotificationAsync(byte[] txId);

    Task<IEnumerable<NotificationData>> GetTxsToSendMempoolDSNotificationsAsync();

    Task<IEnumerable<Tx>> GetTxsNotInCurrentBlockChainAsync(long blockInternalId);

    Task<IEnumerable<Tx>> GetTxsForDSCheckAsync(IEnumerable<byte[]> txExternalIds);
    
    Task<Block> GetBestBlockAsync();
    
    Task<Block> GetBlockAsync(byte[] blockHash);
    Task<bool> TransactionExists(byte[] txId);
  }
}
