// Copyright (c) 2020 Bitcoin Association

using Dapper;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Block = MerchantAPI.APIGateway.Domain.Models.Block;

namespace MerchantAPI.APIGateway.Infrastructure.Repositories
{
  public class TxRepositoryPostgres : ITxRepository
  {
    private readonly string connectionString;

    public TxRepositoryPostgres(IConfiguration configuration)
    {
      connectionString = configuration["ConnectionStrings:DBConnectionString"];
    }

    private NpgsqlConnection GetDbConnection()
    {
      var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());

      return connection;
    }

    public static void EmptyRepository(string connectionString)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText =
        "TRUNCATE Tx, Block, TxMempoolDoubleSpendAttempt, TxBlockDoubleSpend, TxBlock, TxInput";
      connection.Execute(cmdText, null);
    }

    public async Task<long> InsertBlockAsync(Block block)
    {
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
INSERT INTO Block (blockTime, blockHash, prevBlockHash, blockHeight, onActiveChain)
VALUES (@blockTime, @blockHash, @prevBlockHash, @blockHeight, @onActiveChain)
ON CONFLICT (blockHash) DO NOTHING
RETURNING blockInternalId;
";
      var blockInternalId = await connection.ExecuteScalarAsync<long>(cmdText, new 
        { 
          blockTime = block.BlockTime, 
          blockHash = block.BlockHash,
          prevBlockHash = block.PrevBlockHash, 
          blockHeight = block.BlockHeight, 
          onActiveChain = block.OnActiveChain
        });
      await transaction.CommitAsync();

      return blockInternalId;
    }

    public async Task CheckAndInsertBlockDoubleSpendAsync(IEnumerable<TxWithInput> txWithInputsEnum, long deltaBlockHeight, long blockInternalId)
    {
      var txWithInputs = txWithInputsEnum.ToArray(); // Make sure that we donot enumerate multiple times
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      string cmdFindRootSplit = @"
SELECT MAX(maxbh.blockheight)
FROM 
(
  SELECT b.blockheight
  FROM block b 
  WHERE blockheight > (SELECT MAX(blockheight) FROM block) - @deltaBlockHeight
  GROUP BY b.blockheight
  HAVING COUNT(*) > 1
) maxbh;
";

      var heights = await transaction.Connection.ExecuteScalarAsync<long?>(cmdFindRootSplit, new { deltaBlockHeight });

      // There was no fork in previous x blocks as specified in deltaBlockHeight, so no need
      // to search for block double spends, because nodes should have found them and reject them
      if (!heights.HasValue)
      {
        return;
      }

      string cmdTempTable = @"
CREATE TEMPORARY TABLE BlockTxsWithInputs (
    txExternalId    BYTEA   NOT NULL,
    prevTxId        BYTEA,
    prev_n          BIGINT,
    blockInternalId  BIGINT
) ON COMMIT DROP;
";
      await transaction.Connection.ExecuteAsync(cmdTempTable);


      int index = 0;
      do
      {
        await transaction.Connection.ExecuteAsync("DELETE FROM BlockTxsWithInputs;");

        using (var txImporter = transaction.Connection.BeginBinaryImport(@"COPY BlockTxsWithInputs (txExternalId, prevTxId, prev_n, blockInternalId) FROM STDIN (FORMAT BINARY)"))
        {
          foreach (var txInput in txWithInputs)
          {
            index++;
            txImporter.StartRow();

            txImporter.Write(txInput.TxExternalId, NpgsqlTypes.NpgsqlDbType.Bytea);
            txImporter.Write(txInput.PrevTxId, NpgsqlTypes.NpgsqlDbType.Bytea);
            txImporter.Write(txInput.Prev_N, NpgsqlTypes.NpgsqlDbType.Bigint);
            txImporter.Write(blockInternalId, NpgsqlTypes.NpgsqlDbType.Bigint);

            // Let's search for block double spends in batches
            if (index % 20000 == 0 || index == txWithInputs.Count())
            {
              break;
            }
          }

          await txImporter.CompleteAsync();
        }

        string cmdInsertDS = @"
INSERT INTO TxBlockDoubleSpend (txInternalId, blockInternalId, dsTxId)
SELECT t.txInternalId, bin.blockInternalId, bin.txExternalId
FROM tx t 
INNER JOIN TxInput tin ON tin.txInternalId = t.txInternalId 
INNER JOIN BlockTxsWithInputs bin ON tin.prev_n = bin.prev_n and tin.prevTxId = bin.prevTxId
WHERE t.txExternalId <> bin.txExternalId;
";

        await transaction.Connection.ExecuteAsync(cmdInsertDS);
      }
      while (index < txWithInputs.Count());

      await transaction.CommitAsync();
    }

    public async Task InsertMempoolDoubleSpendAsync(long txInternalId, byte[] dsTxId, byte[] dsTxPayload)
    {
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
INSERT INTO TxMempoolDoubleSpendAttempt (txInternalId, dsTxId, dsTxPayload)
VALUES (@txInternalId, @dsTxId, @dsTxPayload)
ON CONFLICT (txInternalId, dsTxId) DO NOTHING;
";

      await connection.ExecuteAsync(cmdText, new { txInternalId, dsTxId, dsTxPayload });
      await transaction.CommitAsync();
    }

    private void AddToTxImporter(NpgsqlBinaryImporter txImporter, long txInternalId, byte[] txExternalId, byte[] txPayload, DateTime? receivedAt, string callbackUrl,
                                 string callbackToken, string callbackEncryption, bool? merkleProof, bool? dsCheck, long? n, byte[] prevTxId, long? prevN)
    {
      txImporter.StartRow();

      txImporter.Write(txInternalId, NpgsqlTypes.NpgsqlDbType.Bigint);
      txImporter.Write(txExternalId, NpgsqlTypes.NpgsqlDbType.Bytea);
      txImporter.Write((object)txPayload ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Bytea);
      txImporter.Write((object)receivedAt ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Timestamp);
      txImporter.Write((object)callbackUrl ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Varchar);
      txImporter.Write((object)callbackToken ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Varchar);
      txImporter.Write((object)callbackEncryption ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Varchar);
      txImporter.Write((object)merkleProof ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Boolean);
      txImporter.Write((object)dsCheck ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Boolean);
      txImporter.Write((object)n ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Bigint);
      txImporter.Write((object)prevTxId ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Bytea);
      txImporter.Write((object)prevN ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Bigint);
    }

    public async Task InsertTxsAsync(IList<Tx> transactions)
    {
      using var connection = GetDbConnection();

      long txInternalId;
      using (var seqTransaction = await connection.BeginTransactionAsync())
      {
        // Reserve sequence ids so no one else can use them
        txInternalId = seqTransaction.Connection.ExecuteScalar<long>("SELECT nextval('tx_txinternalid_seq');");
        seqTransaction.Connection.Execute($"SELECT setval('tx_txinternalid_seq', {txInternalId + transactions.Count});");
        seqTransaction.Commit();
      }

      using var transaction = await connection.BeginTransactionAsync();

      string cmdTempTable = @"
CREATE TEMPORARY TABLE TxTemp (
		txInternalId    BIGINT			NOT NULL,
		txExternalId    BYTEA			NOT NULL,
		txPayload			  BYTEA,
		receivedAt			TIMESTAMP,
		callbackUrl			VARCHAR(1024),    
		callbackToken		VARCHAR(256),
    callbackEncryption VARCHAR(1024),
		merkleProof			BOOLEAN,
		dsCheck				  BOOLEAN,
		n					      BIGINT,
		prevTxId			  BYTEA,
		prev_n				  BIGINT
) ON COMMIT DROP;
";
      await transaction.Connection.ExecuteAsync(cmdTempTable);

      using (var txImporter = transaction.Connection.BeginBinaryImport(@"COPY TxTemp (txInternalId, txExternalId, txPayload, receivedAt, callbackUrl, callbackToken, callbackEncryption, merkleProof,
                                                                                      dsCheck, n, prevTxId, prev_n) FROM STDIN (FORMAT BINARY)"))
      {
        foreach(var tx in transactions)
        {
          AddToTxImporter(txImporter, txInternalId, tx.TxExternalIdBytes, tx.TxPayload, tx.ReceivedAt, tx.CallbackUrl, tx.CallbackToken, tx.CallbackEncryption,  tx.MerkleProof, tx.DSCheck, null, null, null);

          foreach (var txIn in tx.TxIn)
          {
            AddToTxImporter(txImporter, txInternalId, tx.TxExternalIdBytes, null, null, null, null, null, null, null, txIn.N, txIn.PrevTxId, txIn.PrevN);
          }
          txInternalId++;
        }

        await txImporter.CompleteAsync();
      }

      string cmdText = @"
INSERT INTO Tx(txInternalId, txExternalId, txPayload, receivedAt, callbackUrl, callbackToken, callbackEncryption, merkleProof, dsCheck)
SELECT txInternalId, txExternalId, txPayload, receivedAt, callbackUrl, callbackToken, callbackEncryption, merkleProof, dsCheck
FROM TxTemp
WHERE txPayload IS NOT NULL;
";

      cmdText += @"
INSERT INTO TxInput(txInternalId, n, prevTxId, prev_n)
SELECT txInternalId, n, prevTxId, prev_n
FROM TxTemp
WHERE txPayload IS NULL;
";

      await transaction.Connection.ExecuteAsync(cmdText);
      await transaction.CommitAsync();
    }

    public async Task InsertTxBlockAsync(IList<long> txInternalIds, long blockInternalId)
    {
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      using (var txImporter = transaction.Connection.BeginBinaryImport(@"COPY TxBlock (txInternalId, blockInternalId) FROM STDIN (FORMAT BINARY)"))
      {
        foreach(var txId in txInternalIds)
        {
          txImporter.StartRow();

          txImporter.Write(txId, NpgsqlTypes.NpgsqlDbType.Bigint);
          txImporter.Write(blockInternalId, NpgsqlTypes.NpgsqlDbType.Bigint);
        }

        await txImporter.CompleteAsync();
      }

      await transaction.CommitAsync();
    }

    public async Task UpdateDsTxPayload(byte[] dsTxId, byte[] txPayload)
    {
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
UPDATE TxBlockDoubleSpend
SET DsTxPayload = @txPayload
WHERE dsTxId = @dsTxId;
";

      await transaction.Connection.ExecuteAsync(cmdText, new { dsTxId, txPayload });
      await transaction.CommitAsync();
    }

    public async Task<IEnumerable<byte[]>> GetDSTxWithoutPayload()
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT t.dsTxId 
FROM TxBlockDoubleSpend t 
WHERE t.DsTxPayload IS NULL;
";
      return await connection.QueryAsync<byte[]>(cmdText);
    }

    public async Task<IEnumerable<NotificationData>> GetTxsToSendBlockDSNotificationsAsync()
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT Tx.txInternalId, Block.blockInternalId, txExternalId, TxBlockDoubleSpend.dsTxId doubleSpendTxId, block.blockhash, block.blockheight, callbackUrl, callbackToken, callbackEncryption, dsTxPayload payload
FROM Tx
INNER JOIN TxBlockDoubleSpend ON Tx.txInternalId = TxBlockDoubleSpend.txInternalId
INNER JOIN Block ON block .blockinternalid = TxBlockDoubleSpend.blockinternalid 
WHERE sentDsNotificationAt IS NULL AND dsTxPayload IS NOT NULL 
ORDER BY callbackUrl;
";

      return await connection.QueryAsync<NotificationData>(cmdText);
    }

    public async Task<NotificationData> GetTxToSendBlockDSNotificationAsync(byte[] txId)
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT Tx.txInternalId, Block.blockInternalId, txExternalId, TxBlockDoubleSpend.dsTxId doubleSpendTxId, block.blockhash, block.blockheight, callbackUrl, callbackToken, callbackEncryption, dsTxPayload payload
FROM Tx
INNER JOIN TxBlockDoubleSpend ON Tx.txInternalId = TxBlockDoubleSpend.txInternalId
INNER JOIN Block ON block .blockinternalid = TxBlockDoubleSpend.blockinternalid 
WHERE sentDsNotificationAt IS NULL AND dsTxPayload IS NOT NULL AND txExternalId = @txId
ORDER BY callbackUrl;
";

      return await connection.QueryFirstOrDefaultAsync<NotificationData>(cmdText, new { txId });
    }

    public async Task<IEnumerable<NotificationData>> GetTxsToSendMempoolDSNotificationsAsync()
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT Tx.txInternalId, Tx.txExternalId, callbackUrl, callbackToken, callbackEncryption, dsTxId doubleSpendTxId, dsTxPayload payload
FROM Tx
INNER JOIN TxMempoolDoubleSpendAttempt ON Tx.txInternalId = TxMempoolDoubleSpendAttempt.txInternalId
WHERE sentDsNotificationAt IS NULL
ORDER BY callbackUrl;
";

      return await connection.QueryAsync<NotificationData>(cmdText);
    }

    public async Task<IEnumerable<NotificationData>> GetTxsToSendMerkleProofNotificationsAsync(long skip, long fetch)
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT Tx.txInternalId, Block.blockInternalId, txExternalId, block.blockhash, block.blockheight, callbackUrl, callbackToken, callbackEncryption
FROM Tx
INNER JOIN TxBlock ON Tx.txInternalId = TxBlock.txInternalId
INNER JOIN Block ON block .blockinternalid = TxBlock.blockinternalid 
WHERE sentMerkleProofAt IS NULL
OFFSET @skip ROWS
FETCH NEXT @fetch ROWS ONLY;
";

      return await connection.QueryAsync<NotificationData>(cmdText, new { skip, fetch} );
    }

    public async Task<NotificationData> GetTxToSendMerkleProofNotificationAsync(byte[] txId)
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT Tx.txInternalId, Block.blockInternalId, txExternalId, block.blockhash, block.blockheight, callbackUrl, callbackToken, callbackEncryption
FROM Tx
INNER JOIN TxBlock ON Tx.txInternalId = TxBlock.txInternalId
INNER JOIN Block ON block .blockinternalid = TxBlock.blockinternalid 
WHERE sentMerkleProofAt IS NULL AND txExternalId= @txId;
";

      return await connection.QueryFirstOrDefaultAsync<NotificationData>(cmdText, new { txId });
    }

    /// <summary>
    /// Search for all transactions that are not present in the current chain we are parsing.
    /// The transaction might already have a record in TxBlock table but from a different fork,
    /// which means we will need to add a new record to TxBlock if the same transaction is present 
    /// in new longer chain, to ensure we will send out the required notifications again
    /// </summary>
    public async Task<IEnumerable<Tx>> GetTxsNotInCurrentBlockChainAsync(long blockInternalId)
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT txInternalId, txExternalId TxExternalIdBytes
FROM Tx
WHERE NOT EXISTS 
(
  WITH RECURSIVE ancestorBlocks AS 
  (
    SELECT blockinternalid, blockheight , blockhash, prevblockhash
    FROM block 
    WHERE blockinternalid = @blockInternalId

    UNION 

    SELECT b1.blockinternalid, b1.blockheight, b1.blockhash, b1.prevblockhash
    FROM block b1
    INNER JOIN ancestorBlocks b2 ON b1.blockhash = b2.prevblockhash
  )
  SELECT 1 
  FROM ancestorBlocks 
  INNER JOIN TxBlock ON ancestorBlocks.blockinternalid = TxBlock.blockinternalid 
  WHERE txblock.txInternalId=Tx.txInternalId
);";

      return await connection.QueryAsync<Tx>(cmdText);
    }

    /// <summary>
    /// Records from DB contain merged data from 2 tables (Tx, TxInput), which will be split into 2 objects. Unique
    /// transactions (txId) are created first from the source set, subsequently records that still remain 
    /// must contain additional inputs for created transactions, so they are added to existing transactions
    /// as TxInput 
    /// </summary>
    private IEnumerable<Tx> TxWithInputDataToTx(HashSet<TxWithInput> txWithInputs)
    {
      var distinctItems = new HashSet<TxWithInput>(txWithInputs.Distinct().ToArray());
      HashSet<Tx> txSet = new HashSet<Tx>(distinctItems.Select(x =>
                                                               {
                                                                  return new Tx(x);
                                                               }), new TxComparer());
      txWithInputs.ExceptWith(distinctItems);

      foreach (var tx in txWithInputs)
      {
        if (txSet.TryGetValue(new Tx(tx), out var txFromSet))
        {
          txFromSet.TxIn.Add(new TxInput(tx));
        }
      }
      return txSet.ToList();

    }

    public async Task<IEnumerable<Tx>> GetTxsForDSCheckAsync(IEnumerable<byte[]> txExternalIds)
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT Tx.txInternalId, txExternalId, callbackUrl, callbackToken, callbackEncryption, Tx.txInternalId childId, n, prevTxId, prev_n
FROM Tx 
INNER JOIN TxInput on TxInput.txInternalId = Tx.txInternalId
WHERE dsCheck = true
      AND txExternalId = ANY(@externalIds)
      AND (SELECT COUNT(*) FROM TxBlockDoubleSpend WHERE TxBlockDoubleSpend.txInternalId = Tx.txInternalId) = 0;
";
      var txData = new HashSet<TxWithInput>(await connection.QueryAsync<TxWithInput>(cmdText, new { externalIds = txExternalIds.ToArray() }));
      return TxWithInputDataToTx(txData);
    }

    public async Task<Block> GetBestBlockAsync()
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT b.blockinternalid, b.blocktime, b.blockhash, b.prevblockhash, b.blockheight, b.onactivechain
FROM block b 
ORDER BY blockheight DESC 
FETCH FIRST 1 ROW ONLY;
";

      var bestBlock = await connection.QueryFirstOrDefaultAsync<Block>(cmdText);
      return bestBlock;
    }

    public async Task<Block> GetBlockAsync(byte[] blockHash)
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT b.blockinternalid, b.blocktime, b.blockhash, b.prevblockhash, b.blockheight, b.onactivechain
FROM block b 
WHERE b.blockhash = @blockHash;
";

      var block = await connection.QueryFirstOrDefaultAsync<Block>(cmdText, new { blockHash });
      return block;
    }

    public async Task<bool> TransactionExists(byte[] txId)
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT COUNT(*)
FROM tx
WHERE tx.txexternalid = @txId;
";

      var foundTx = await connection.ExecuteScalarAsync<int>(cmdText, new { txId } );
      return foundTx > 0;
    }

    public async Task SetBlockDoubleSpendSendDateAsync(long txInternalId, long blockInternalId, byte[] dsTxId, DateTime sendDate)
    {
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
UPDATE TxBlockDoubleSpend SET sentDsNotificationAt=@sendDate
WHERE txInternalId=@txInternalId AND blockInternalId=@blockInternalId AND dsTxId=@dsTxId;
";

      await connection.ExecuteAsync(cmdText, new { txInternalId, blockInternalId, dsTxId, sendDate });
      await transaction.CommitAsync();
    }

    public async Task SetBlockParsedForDoubleSpendDateAsync(long blockInternalId)
    {
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
UPDATE Block SET parsedForDSAt=@parsedForDSAt
WHERE blockInternalId=@blockInternalId;
";

      await connection.ExecuteAsync(cmdText, new { blockInternalId, parsedForDSAt=DateTime.Now });
      await transaction.CommitAsync();
    }

    public async Task SetBlockParsedForMerkleDateAsync(long blockInternalId)
    {
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
UPDATE Block SET parsedForMerkleAt=@parsedForMerkleAt
WHERE blockInternalId=@blockInternalId;
";

      await connection.ExecuteAsync(cmdText, new { blockInternalId, parsedForMerkleAt = DateTime.Now });
      await transaction.CommitAsync();
    }

    public async Task SetMempoolDoubleSpendSendDateAsync(long txInternalId, byte[] dsTxId, DateTime sendDate)
    {
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
UPDATE TxMempoolDoubleSpendAttempt SET sentDsNotificationAt=@sendDate
WHERE txInternalId=@txInternalId AND dsTxId=@dsTxId;
";

      await connection.ExecuteAsync(cmdText, new { txInternalId, dsTxId, sendDate });
      await transaction.CommitAsync();
    }

    public async Task SetMerkleProofSendDateAsync(long txInternalId, long blockInternalId, DateTime sendDate)
    {
      using var connection = GetDbConnection();
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
UPDATE TxBlock SET sentMerkleProofAt=@sendDate
WHERE txInternalId=@txInternalId AND blockInternalId=@blockInternalId;
";

      await connection.ExecuteAsync(cmdText, new { txInternalId, blockInternalId, sendDate });
      await transaction.CommitAsync();
    }
  }
}
