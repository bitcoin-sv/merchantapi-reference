﻿// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models.APIStatus;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class BlockParserStatusTest : BlockParserTestBase
  {
    public IBlockParser blockParser;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      blockParser = server.Services.GetRequiredService<IBlockParser>();
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
    public void BlockParserStatusEmpty()
    {
      var status = blockParser.GetBlockParserStatus();

      Assert.AreEqual(0, status.BlocksProcessed);
      Assert.AreEqual(0, status.NumOfErrors);
      Assert.AreEqual(0, status.BlocksQueued);
      CheckBlockParserStatusNoBlocksParsed(status, 0);
    }

    private static void CheckBlockParserStatusNoBlocksParsed(BlockParserStatus status, long blocksProcessed)
    {
      Assert.AreEqual(0, status.BlocksParsed);
      Assert.AreEqual(0F, status.TotalBytes);
      Assert.AreEqual(0F, status.TotalTxs);
      Assert.AreEqual(0, status.TotalTxsFound);
      Assert.AreEqual(0, status.TotalDsFound);
      Assert.IsNull(status.BestBlockHash);
      Assert.IsNull(status.BestBlockHeight);
      Assert.IsNull(status.LastBlockHash);
      Assert.IsNull(status.LastBlockHeight);
      Assert.IsNull(status.LastBlockParsedAt);
      Assert.IsNull(status.LastBlockParseTime);
      Assert.IsNull(status.LastBlockInQueueAndParseTime);
      Assert.IsNull(status.AverageParseTime);
      Assert.IsNull(status.AverageTxParseTime);
      Assert.IsNull(status.AverageBlockDownloadSpeed);
      Assert.IsNull(status.MaxParseTime);
      Assert.AreEqual($@"Number of blocks successfully parsed: 0, ignored/duplicates: 0, parsing terminated with error: {blocksProcessed}. 
Number of blocks processed from queue is {blocksProcessed}, remaining: 0.", status.BlockParserDescription);
    }

    private static void CheckBlockParserStatusFilled(BlockParserStatus status, long blocksProcessed, long blocksParsed,
      int totalTxs = 0, int totalTxsFound = 0, int totalDsFound = 0, int numOfErrors = 0, int blocksQueued = 0)
    {
      Assert.AreEqual(blocksProcessed, status.BlocksProcessed);
      Assert.AreEqual(blocksParsed, status.BlocksParsed);
      Assert.IsTrue(status.TotalBytes > 0);
      Assert.AreEqual((ulong)totalTxs, status.TotalTxs);
      Assert.AreEqual(totalTxsFound, status.TotalTxsFound);
      Assert.AreEqual(totalDsFound, status.TotalDsFound);
      Assert.IsNotNull(status.BestBlockHash);
      Assert.IsNotNull(status.BestBlockHeight);
      Assert.IsNotNull(status.LastBlockHash);
      Assert.IsNotNull(status.LastBlockHeight);
      Assert.IsTrue(status.BestBlockHeight >= status.LastBlockHeight);
      if (status.BestBlockHeight == status.LastBlockHeight)
      {
        Assert.AreEqual(status.BestBlockHash, status.LastBlockHash);
      }
      else
      {
        Assert.AreNotEqual(status.BestBlockHash, status.LastBlockHash);
      }
      Assert.IsNotNull(status.LastBlockParsedAt);
      Assert.IsNotNull(status.LastBlockParseTime);
      Assert.IsNotNull(status.LastBlockInQueueAndParseTime);
      Assert.IsNotNull(status.AverageParseTime);
      Assert.IsNotNull(status.AverageTxParseTime);
      Assert.IsTrue(status.AverageBlockDownloadSpeed > 0);
      Assert.IsNotNull(status.MaxParseTime);
      Assert.AreEqual(numOfErrors, status.NumOfErrors);
      Assert.AreEqual(blocksQueued, status.BlocksQueued);
      Assert.AreEqual($@"Number of blocks successfully parsed: {blocksParsed}, ignored/duplicates: {(blocksProcessed - blocksParsed - numOfErrors)}, parsing terminated with error: {numOfErrors}. 
Number of blocks processed from queue is {blocksProcessed}, remaining: {blocksQueued}.", status.BlockParserDescription);
    }

    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [TestMethod]
    public async Task BlockParserStatusBlockWithOneTx(bool merkleProof, bool dsCheck)
    {
      await CreateAndPublishNewBlockAsync(RpcClient, null, null);

      var status = blockParser.GetBlockParserStatus();
      CheckBlockParserStatusFilled(status, 1, 1, totalTxs: 1, totalTxsFound: 0, totalDsFound: 0);

      await CreateAndInsertTxAsync(merkleProof, dsCheck, 1);
      var tx1 = Transaction.Parse(Tx1Hex, Network.Main);

      await CreateAndPublishNewBlockAsync(RpcClient, null, tx1, false);

      WaitUntilEventBusIsIdle();

      status = blockParser.GetBlockParserStatus();
      CheckBlockParserStatusFilled(status, 2, 2, totalTxs: 2, totalTxsFound: 1, totalDsFound: 0);
    }

    [TestMethod]
    public async Task BlockParserStatusMerkleProofCheck()
    {
      var txList = await CreateAndInsertTxAsync(true, false); // 5 txs

      _ = await InsertMerkleProof(); // firstblock (1 tx) + second block

      WaitUntilEventBusIsIdle();

      var status = blockParser.GetBlockParserStatus();

      CheckBlockParserStatusFilled(status, 2, 2, totalTxs: txList.Count + 1, totalTxsFound: txList.Count, totalDsFound: 0);
    }

    [TestMethod]
    public async Task BlockParserStatusDoubleSpendCheck()
    {
      await CreateAndPublishNewBlockAsync(RpcClient, null, null);

      var status = blockParser.GetBlockParserStatus();
      CheckBlockParserStatusFilled(status, 1, 1, totalTxs: 1, totalTxsFound: 0, totalDsFound: 0);

      var txList = await CreateAndInsertTxAsync(false, true); 
      Assert.AreEqual(5, txList.Count);

      await InsertDoubleSpend(); // 5 txs + 1 DS (each in its own block)

      WaitUntilEventBusIsIdle();

      var dbRecords = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();
      status = blockParser.GetBlockParserStatus();
      // last pushed block DS has height 2
      Assert.IsTrue(status.BestBlockHeight > status.LastBlockHeight);
      CheckBlockParserStatusFilled(status, 7, 7, totalTxs: 7, totalTxsFound: txList.Count, totalDsFound: dbRecords.Count);
    }

    [TestMethod]
    public async Task BlockParserStatusTestSkipParsing()
    {
      var node = NodeRepository.GetNodes().First();
      var rpcClient = (Mock.RpcClientMock)rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      await CreateAndPublishNewBlockAsync(rpcClient, null, null);
      var blockStream = await RpcClient.GetBlockAsStreamAsync(await RpcClient.GetBestBlockHashAsync());
      var firstBlock = HelperTools.ParseByteStreamToBlock(blockStream);

      Assert.AreEqual(0, (await TxRepositoryPostgres.GetUnparsedBlocksAsync()).Length);

      var status = blockParser.GetBlockParserStatus();
      CheckBlockParserStatusFilled(status, 1, 1, totalTxs: firstBlock.Transactions.Count);
      Assert.AreEqual((ulong)firstBlock.ToBytes().Length, status.TotalBytes);
      var lastBlockParsedAt = status.LastBlockParsedAt;

      var block = await TxRepositoryPostgres.GetBestBlockAsync();

      // we publish same NewBlockAvailableInDB as before
      var block2Parse = block;
      int i = 0;
      while (i < 1000)
      {
        PublishBlockToEventBus(block2Parse, waitUntilEventBusIsIdle: false);
        i++;
      }
      WaitUntilEventBusIsIdle();

      // best block must stay the same, since parsing was skipped
      var blockAfterRepublish = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.AreEqual(block.BlockInternalId, blockAfterRepublish.BlockInternalId);
      Assert.AreEqual(block.ParsedForMerkleAt, blockAfterRepublish.ParsedForMerkleAt);
      Assert.AreEqual(block.ParsedForDSAt, blockAfterRepublish.ParsedForDSAt);

      status = blockParser.GetBlockParserStatus();
      CheckBlockParserStatusFilled(status, 1001, 1, totalTxs: 1);
      Assert.AreEqual(lastBlockParsedAt, status.LastBlockParsedAt);
      Assert.AreEqual((ulong)firstBlock.ToBytes().Length, status.TotalBytes);
    }

    [TestMethod]
    public void BlockParserStatusTestInvalidBlock()
    {
      Domain.Models.Block block = new()
      {
          BlockHash = HelperTools.HexStringToByteArray("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f")
      };

      PublishBlockToEventBus(block);

      var status = blockParser.GetBlockParserStatus();
      Assert.AreEqual(1, status.BlocksProcessed);
      CheckBlockParserStatusNoBlocksParsed(status, 1);
    }

    [TestMethod]
    public async Task BlockParserStatusTestErrorFixed()
    {
      // arrange
      var blockStream = await RpcClient.GetBlockAsStreamAsync(await RpcClient.GetBestBlockHashAsync());
      var firstBlock = HelperTools.ParseByteStreamToBlock(blockStream);
      int blockHeight = 0;

      rpcClientFactoryMock.RemoveKnownBlock(firstBlock.GetHash());

      var dbBlock = new Domain.Models.Block
      {
        BlockHash = firstBlock.GetHash().ToBytes(),
        BlockHeight = blockHeight,
        BlockTime = System.DateTime.UtcNow,
        OnActiveChain = true,
        PrevBlockHash = firstBlock.Header.HashPrevBlock.ToBytes()
      };

      // insert block in DB (simulate successful NewBlockDiscoveredAsync)
      var blockId = await TxRepositoryPostgres.InsertOrUpdateBlockAsync(dbBlock);
      dbBlock.BlockInternalId = blockId.Value;

      // act
      PublishBlockToEventBus(dbBlock);

      var status = blockParser.GetBlockParserStatus();
      Assert.AreEqual(1, status.BlocksProcessed);
      CheckBlockParserStatusNoBlocksParsed(status, 1);

      rpcClientFactoryMock.AddKnownBlock(blockHeight, firstBlock.ToBytes());
      PublishBlockToEventBus(dbBlock);

      status = blockParser.GetBlockParserStatus();
      CheckBlockParserStatusFilled(status, 2, 1, totalTxs: firstBlock.Transactions.Count, numOfErrors: 1);
    }

    [TestCategory("Manual")]
    [DataRow(750, 2117649833L)] // block of size 2.1GB
    [DataRow(1500, 4235299583L)] // block of size > 4 GB
    [TestMethod]
    public async Task BlockParserStatusTestBigBlock(int txsCount, long blockBytes)
    {
      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      var txs = await Transaction16mbList(txsCount, dsCheck: true);

      await CreateAndPublishNewBlockAsync(RpcClient, null, null);

      (_, string bigBlockHash) = await CreateAndPublishNewBlockWithTxsAsync(rpcClient, null, txs.ToArray(), true, true);


      var block = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.IsFalse(HelperTools.AreByteArraysEqual(block.BlockHash, new uint256(bigBlockHash).ToBytes()));

      var blockStream = await RpcClient.GetBlockAsStreamAsync(new uint256(block.BlockHash).ToString());
      var firstBlock = HelperTools.ParseByteStreamToBlock(blockStream);
      int firstBlockTxsCount = firstBlock.Transactions.Count;
      ulong firstBlockBytes = (ulong)firstBlock.ToBytes().Length;

      var statusFirst = blockParser.GetBlockParserStatus();
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
      Assert.AreEqual(txsCount, parsedBlock.Transactions.Count);

      var status = blockParser.GetBlockParserStatus();
      Assert.AreEqual(status.BlocksDuplicated == 0 ? 2 : 3, status.BlocksProcessed); // 3 when running on slower host
      Assert.AreEqual(2, status.BlocksParsed);
      Assert.AreEqual(1, status.TotalTxsFound);

      var dbRecords = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();
      Assert.AreEqual(dbRecords.Count, status.TotalDsFound);
      Assert.AreEqual((ulong)(txsCount + firstBlockTxsCount), status.TotalTxs);
      Assert.AreEqual(1, status.LastBlockHeight);
      Assert.AreEqual((ulong)blockBytes + firstBlockBytes, status.TotalBytes);
      Assert.AreEqual(firstBlockParseTime + status.LastBlockParseTime, status.BlocksParseTime);
      Assert.AreEqual( (firstBlockParseTime + status.LastBlockParseTime) / 2, status.AverageParseTime);
      Assert.IsTrue(firstBlockDownloadSpeed < status.AverageBlockDownloadSpeed);
      Assert.AreEqual( (status.TotalBytes / (double)Const.Megabyte) / status.BlocksDownloadTime.TotalSeconds, status.AverageBlockDownloadSpeed);
    }
  }
}
