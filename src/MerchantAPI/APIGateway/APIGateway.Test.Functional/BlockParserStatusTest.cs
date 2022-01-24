// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class BlockParserStatusTest : BlockParserTestBase
  {
    public BlockParser blockParser;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      blockParser = server.Services.GetRequiredService<BlockParser>();
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;

      // Wait until all events are processed to avoid race conditions
      WaitUntilEventBusIsIdle();
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    [TestMethod]
    public virtual async Task BlockParserStatusEmpty()
    {
      var status = await blockParser.GetBlockParserStatusAsync();

      Assert.AreEqual(0, status.BlocksProcessed);
      Assert.AreEqual(0, status.NumOfErrors);
      Assert.AreEqual(0, status.BlocksQueued);
      CheckBlockParserStatusNoBlocksParsed(status);
      Assert.AreEqual($@"Number of blocks successfully parsed: 0, ignored/duplicates: 0, parsing terminated with error: 0. 
Number of blocks processed from queue is 0, remaining: 0.", status.BlockParserDescription);
    }

    private static void CheckBlockParserStatusNoBlocksParsed(BlockParserStatus status)
    {
      Assert.AreEqual(0, status.BlocksParsed);
      Assert.AreEqual(0F, status.TotalBytes);
      Assert.AreEqual(0F, status.TotalTxs);
      Assert.AreEqual(0, status.TotalTxsFound);
      Assert.AreEqual(0, status.TotalDsFound);
      Assert.IsNull(status.LastBlockHash);
      Assert.IsNull(status.LastBlockHeight);
      Assert.IsNull(status.LastBlockParsedAt);
      Assert.IsNull(status.LastBlockParseTime);
      Assert.IsNull(status.LastBlockInQueueAndParseTime);
      Assert.IsNull(status.AverageParseTime);
      Assert.IsNull(status.AverageTxParseTime);
      Assert.IsNull(status.AverageBlockDownloadSpeed);
      Assert.IsNull(status.MaxParseTime);
    }

    private static void CheckBlockParserStatusFilled(BlockParserStatus status, long blocksProcessed, long blocksParsed,
      int totalTxs = 0, int totalTxsFound = 0, int totalDsFound = 0)
    {
      Assert.AreEqual(blocksProcessed, status.BlocksProcessed);
      Assert.AreEqual(blocksParsed, status.BlocksParsed);
      Assert.IsTrue(status.TotalBytes > 0);
      Assert.AreEqual((ulong)totalTxs, status.TotalTxs);
      Assert.AreEqual(totalTxsFound, status.TotalTxsFound);
      Assert.AreEqual(totalDsFound, status.TotalDsFound);
      Assert.IsNotNull(status.LastBlockHash);
      Assert.IsNotNull(status.LastBlockHeight);
      Assert.IsNotNull(status.LastBlockParsedAt);
      Assert.IsNotNull(status.LastBlockParseTime);
      Assert.IsNotNull(status.LastBlockInQueueAndParseTime);
      Assert.IsNotNull(status.AverageParseTime);
      Assert.IsNotNull(status.AverageTxParseTime);
      Assert.IsNotNull(status.AverageBlockDownloadSpeed);
      Assert.IsNotNull(status.MaxParseTime);
      Assert.AreEqual(0, status.NumOfErrors);
      Assert.AreEqual(0, status.BlocksQueued);
    }

    [TestMethod]
    public async Task BlockParserStatusMerkleProofCheck()
    {
      var txList = await CreateAndInsertTxAsync(true, false);

      _ = await InsertMerkleProof();

      WaitUntilEventBusIsIdle();

      var status = await blockParser.GetBlockParserStatusAsync();

      CheckBlockParserStatusFilled(status, 2, 2, totalTxs: txList.Count + 2, totalTxsFound: txList.Count, totalDsFound: 0);
    }

    [TestMethod]
    public async Task BlockParserStatusDoubleSpendCheck()
    {
      var txList = await CreateAndInsertTxAsync(false, true);

      await InsertDoubleSpend();

      WaitUntilEventBusIsIdle();

      var dbRecords = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();
      var status = await blockParser.GetBlockParserStatusAsync();

      CheckBlockParserStatusFilled(status, 7, 7, totalTxs: 13, totalTxsFound: txList.Count, totalDsFound: dbRecords.Count);
    }

    [TestMethod]
    public async Task BlockParserStatusTestSkipParsing()
    {
      var node = NodeRepository.GetNodes().First();
      var rpcClient = (Mock.RpcClientMock)rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      long blockCount = await RpcClient.GetBlockCountAsync();
      var blockStream = await RpcClient.GetBlockAsStreamAsync(await RpcClient.GetBestBlockHashAsync());
      var firstBlock = HelperTools.ParseByteStreamToBlock(blockStream);
      rpcClientFactoryMock.AddKnownBlock(blockCount++, firstBlock.ToBytes());

      var tx = Transaction.Parse(Tx1Hex, Network.Main);
      await CreateAndPublishNewBlock(rpcClient, null, tx, true);

      Assert.AreEqual(0, (await TxRepositoryPostgres.GetUnparsedBlocksAsync()).Length);

      var status = await blockParser.GetBlockParserStatusAsync();
      CheckBlockParserStatusFilled(status, 1, 1, totalTxs: firstBlock.Transactions.Count);
      Assert.AreEqual((ulong)firstBlock.ToBytes().Length, status.TotalBytes);
      var lastBlockParsedAt = status.LastBlockParsedAt;

      var block = await TxRepositoryPostgres.GetBestBlockAsync();

      // we publish same NewBlockAvailableInDB as before
      var block2Parse = block;
      int i = 0;
      while (i < 100)
      {
        PublishBlockToEventBus(block2Parse, waitUntilEventBusIsIdle: false);
        i++;
      }
      status = await blockParser.GetBlockParserStatusAsync();
      Assert.IsTrue(status.BlocksQueued > 0);
      WaitUntilEventBusIsIdle();

      // best block must stay the same, since parsing was skipped
      var blockAfterRepublish = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.AreEqual(block.BlockInternalId, blockAfterRepublish.BlockInternalId);
      Assert.AreEqual(block.ParsedForMerkleAt, blockAfterRepublish.ParsedForMerkleAt);
      Assert.AreEqual(block.ParsedForDSAt, blockAfterRepublish.ParsedForDSAt);

      status = await blockParser.GetBlockParserStatusAsync();
      Assert.AreEqual(status.BlocksQueued, 0);
      Assert.AreEqual(101, status.BlocksProcessed);
      Assert.AreEqual(1, status.BlocksParsed);
      Assert.AreEqual($@"Number of blocks successfully parsed: 1, ignored/duplicates: 100, parsing terminated with error: 0. 
Number of blocks processed from queue is 101, remaining: 0.", status.BlockParserDescription);
      Assert.AreEqual(lastBlockParsedAt, status.LastBlockParsedAt);
      Assert.AreEqual((ulong)firstBlock.ToBytes().Length, status.TotalBytes);
    }

    [TestMethod]
    public async Task BlockParserStatusTestInvalidBlock()
    {
      Domain.Models.Block block = new();
      block.BlockHash = HelperTools.HexStringToByteArray("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f");

      PublishBlockToEventBus(block);

      var status = await blockParser.GetBlockParserStatusAsync();
      Assert.AreEqual(1, status.BlocksProcessed);
      CheckBlockParserStatusNoBlocksParsed(status);
      Assert.AreEqual($@"Number of blocks successfully parsed: 0, ignored/duplicates: 0, parsing terminated with error: 1. 
Number of blocks processed from queue is 1, remaining: 0.", status.BlockParserDescription);
    }

    [TestCategory("Manual")]
    [TestMethod]
    public async Task BlockParserStatusTestBigBlock() // block of size > 4 GB
    {
      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);
      int txsCount = 1500;
      var txs = await Transaction16mbList(txsCount, dsCheck: true);

      (_, string bigBlockHash) = await CreateAndPublishNewBlockWithTxs(rpcClient, null, txs.ToArray(), true, true);

      var block = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.IsFalse(HelperTools.AreByteArraysEqual(block.BlockHash, new uint256(bigBlockHash).ToBytes()));

      var blockStream = await RpcClient.GetBlockAsStreamAsync(new uint256(block.BlockHash).ToString());
      var firstBlock = HelperTools.ParseByteStreamToBlock(blockStream);
      int firstBlockTxsCount = firstBlock.Transactions.Count;
      ulong firstBlockBytes = (ulong)firstBlock.ToBytes().Length;

      var statusFirst = await blockParser.GetBlockParserStatusAsync();
      Assert.AreEqual(1, statusFirst.BlocksProcessed);
      Assert.AreEqual(1, statusFirst.BlocksParsed);
      Assert.AreEqual((ulong)firstBlockTxsCount, statusFirst.TotalTxs);
      Assert.AreEqual(firstBlockBytes, statusFirst.TotalBytes);
      Assert.AreEqual(statusFirst.LastBlockParseTime, statusFirst.AverageParseTime);
      var firstBlockParseTime = statusFirst.LastBlockParseTime;
      var firstBlockDownloadSpeed = statusFirst.AverageBlockDownloadSpeed;

      PublishBlockHashToEventBus(bigBlockHash);

      WaitUntilEventBusIsIdle();

      block = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.IsTrue(HelperTools.AreByteArraysEqual(block.BlockHash, new uint256(bigBlockHash).ToBytes()));
      Assert.AreEqual(0, (await TxRepositoryPostgres.GetUnparsedBlocksAsync()).Length);

      // check if block was correctly parsed
      blockStream = await RpcClient.GetBlockAsStreamAsync(await RpcClient.GetBestBlockHashAsync());
      var parsedBlock = HelperTools.ParseByteStreamToBlock(blockStream);
      Assert.AreEqual(txsCount + 1, parsedBlock.Transactions.Count);

      var status = await blockParser.GetBlockParserStatusAsync();
      Assert.AreEqual(2, status.BlocksProcessed);
      Assert.AreEqual(2, status.BlocksParsed);
      Assert.AreEqual(1, status.TotalTxsFound);

      var dbRecords = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();
      Assert.AreEqual(dbRecords.Count, status.TotalDsFound);
      Assert.AreEqual((ulong)(txsCount + 1 + firstBlockTxsCount), status.TotalTxs);
      Assert.AreEqual(1, status.LastBlockHeight);
      Assert.AreEqual(4235299699 + firstBlockBytes, status.TotalBytes);
      Assert.AreEqual(firstBlockParseTime + status.LastBlockParseTime, status.BlocksParseTime);
      Assert.AreEqual( (firstBlockParseTime + status.LastBlockParseTime) / 2, status.AverageParseTime);
      Assert.IsTrue(firstBlockDownloadSpeed < status.AverageBlockDownloadSpeed);
      Assert.AreEqual( (status.TotalBytes / Const.Megabyte) / status.BlocksDownloadTime.TotalSeconds, status.AverageBlockDownloadSpeed);
    }
  }
}
