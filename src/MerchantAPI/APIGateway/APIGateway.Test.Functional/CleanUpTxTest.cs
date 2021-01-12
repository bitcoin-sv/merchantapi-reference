using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Test.Functional.CleanUpTx;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class CleanUpTxTest : BlockParserTestBase
  {
    readonly int cancellationTimeout = 30000; // 30 seconds
    int cleanUpTxAfterDays;
    CleanUpTxWithPauseHandlerForTest cleanUpTxService;

    [TestInitialize]
    override public void TestInitialize()
    {
      base.TestInitialize();

      cleanUpTxService = server.Services.GetRequiredService<CleanUpTxWithPauseHandlerForTest>();
      cleanUpTxAfterDays = AppSettings.CleanUpTxAfterDays;
    }

    [TestCleanup]
    override public void TestCleanup()
    {
      base.TestCleanup();
    }

    private async Task<(List<Tx> txList, uint256 firstBlockhash)> CreateAndInsertTxWithMempoolAsync(bool dsCheckMempool = false, bool addBlocks = true)
    {
      List<Tx> txList = await CreateAndInsertTxAsync(false, dsCheckMempool, 2);

      uint256 firstBlockHash = null;
      if (addBlocks)
      {
        firstBlockHash = await AddBlocks(dsCheckMempool);
      }

      await CheckTxListPresentInDbAsync(txList, addBlocks);

      return (txList, firstBlockHash);
    }

    private async Task<uint256> AddBlocks(bool dsCheckMempool)
    {
      long blockCount = await RpcClient.GetBlockCountAsync();
      var blockHex = await RestClient.GetBlockAsBytesAsync(await RpcClient.GetBestBlockHashAsync());
      var firstBlock = NBitcoin.Block.Load(blockHex, Network.Main);
      rpcClientFactoryMock.AddKnownBlock(blockCount++, firstBlock.ToBytes());
      PublishBlockHashToEventBus(await RpcClient.GetBestBlockHashAsync());
      var firstBlockHash = firstBlock.GetHash();

      var pubKey = firstBlock.Transactions.First().Outputs.First().ScriptPubKey.GetDestinationPublicKeys().First();
      var block1 = firstBlock.CreateNextBlockWithCoinbase(pubKey, new Money(50, MoneyUnit.MilliBTC), new ConsensusFactory());

      var tx = Transaction.Parse(Tx1Hex, Network.Main);
      block1.AddTransaction(tx);
      block1.Check();
      long forkHeight = blockCount++;
      rpcClientFactoryMock.AddKnownBlock(forkHeight, block1.ToBytes());
      PublishBlockHashToEventBus(await RpcClient.GetBestBlockHashAsync());

      var block2 = block1.CreateNextBlockWithCoinbase(pubKey, new Money(50, MoneyUnit.MilliBTC), new ConsensusFactory());
      var tx2 = Transaction.Parse(Tx2Hex, Network.Main);
      block2.AddTransaction(tx2);
      block2.Check();
      rpcClientFactoryMock.AddKnownBlock(blockCount++, block2.ToBytes());
      PublishBlockHashToEventBus(await RpcClient.GetBestBlockHashAsync());

      if (dsCheckMempool)
      {
        // Use already inserted tx2, but change Version so we get new hash
        var nextBlock = block1.CreateNextBlockWithCoinbase(pubKey, new Money(50, MoneyUnit.MilliBTC), new ConsensusFactory());
        var doubleSpendTx = Transaction.Parse(Tx2Hex, Network.Main);
        doubleSpendTx.Version = 2;
        doubleSpendTx.GetHash();
        nextBlock.AddTransaction(doubleSpendTx);
        nextBlock.Check();
        rpcClientFactoryMock.AddKnownBlock(forkHeight, nextBlock.ToBytes());
      }

      // check if created successfully
      await CheckBlockPresentInDbAsync(firstBlockHash);
      return firstBlockHash;
    }

    private async Task CheckBlockPresentInDbAsync(uint256 blockHash)
    {
      var firstBlockTest = await TxRepositoryPostgres.GetBlockAsync(blockHash.ToBytes()); // this block is present in db without linked tx 
      Assert.IsNotNull(firstBlockTest);
    }

    private async Task<long> CheckTxPresentInDbAsync(string txHash, bool blockPresent = true)
    {
      var txInternalId = await TxRepositoryPostgres.GetTransactionInternalId(new uint256(txHash).ToBytes());
      Assert.IsNotNull(txInternalId);
      var blocks = (await TxRepositoryPostgres.GetBlocksByTxIdAsync(txInternalId.Value));
      Assert.IsTrue(blockPresent ? blocks.Any() : !blocks.Any());
      return txInternalId.Value;
    }

    private async Task CheckTxNotPresentInDbAsync(string txHash, long txInternalId)
    {
      // tx and block are main tables - the other tables reference these two and have 'delete cascade constraint' declared  
      var txDeletedInternalId = await TxRepositoryPostgres.GetTransactionInternalId(new uint256(txHash).ToBytes());
      Assert.IsNull(txDeletedInternalId);
      var block = (await TxRepositoryPostgres.GetBlocksByTxIdAsync(txInternalId)).FirstOrDefault();
      Assert.IsNull(block);
    }

    private async Task CheckTxListPresentInDbAsync(List<Tx> txList, bool addedBlock = true)
    {
      for (int i = 0; i < txList.Count; i++)
      {
        // update internalId in txList
        txList[i].TxInternalId = await CheckTxPresentInDbAsync(new uint256(txList[i].TxExternalId).ToString(), addedBlock);
      }
    }

    private async Task CheckTxListNotPresentInDbAsync(List<Tx> txList)
    {
      await CheckTxNotPresentInDbAsync(Tx1Hash, txList[0].TxInternalId);
      await CheckTxNotPresentInDbAsync(Tx2Hash, txList[1].TxInternalId);
    }

    private async Task CheckBlockNotPresentInDb(uint256 blockHash)
    {
      var firstBlockTest = await TxRepositoryPostgres.GetBlockAsync(blockHash.ToBytes());
      Assert.IsNull(firstBlockTest);
    }

    private async Task ResumeAndWaitForCleanup(MerchantAPI.Common.EventBus.EventBusSubscription<CleanUpTxTriggeredEvent> cleanUpTxTriggeredSubscription)
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);
      await cleanUpTxService.ResumeAsync(cts.Token);

      // wait for cleanUpTx service to finish execute
      await cleanUpTxTriggeredSubscription.ReadAsync(cts.Token);
    }


    [TestMethod]
    public async Task NoneTxToCleanUpCheck()
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = eventBus.Subscribe<CleanUpTxTriggeredEvent>();
      (List<Tx> txList, _) = await CreateAndInsertTxWithMempoolAsync();

      await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

      // check if everything in db is still present
      await CheckTxListPresentInDbAsync(txList);

    }


    [TestMethod]
    public async Task TxWithoutBlockCheckCleanUp()
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = eventBus.Subscribe<CleanUpTxTriggeredEvent>();
      (List<Tx> txList, _) = await CreateAndInsertTxWithMempoolAsync(addBlocks: false);

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

        // check if everything in db was cleared
        await CheckTxListNotPresentInDbAsync(txList);
      }

    }

    [TestMethod]
    public async Task MerkleProofCheckCleanUp()
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = eventBus.Subscribe<CleanUpTxTriggeredEvent>();

      List<Tx> txList = await CreateAndInsertTxAsync(true, false);

      uint256 firstBlockHash = await InsertMerkleProof();
      WaitUntilEventBusIsIdle();

      await CheckTxListPresentInDbAsync(txList, true);
      await CheckBlockPresentInDbAsync(firstBlockHash);

      var merkleProofTxs = (await TxRepositoryPostgres.GetTxsToSendMerkleProofNotificationsAsync(0, 10000)).ToList();

      Assert.AreEqual(5, merkleProofTxs.Count());
      Assert.IsTrue(merkleProofTxs.Any(x => new uint256(x.TxExternalId) == new uint256(Tx2Hash)));

      foreach (var txWithMerkle in merkleProofTxs)
      {
        await TxRepositoryPostgres.SetNotificationSendDateAsync(CallbackReason.MerkleProof, txWithMerkle.TxInternalId, txWithMerkle.BlockInternalId, null, MockedClock.UtcNow);
      }

      merkleProofTxs = (await TxRepositoryPostgres.GetTxsToSendMerkleProofNotificationsAsync(0, 10000)).ToList();
      Assert.AreEqual(0, merkleProofTxs.Count());

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

        // check if everything in db was cleared
        await CheckBlockNotPresentInDb(firstBlockHash);
        await CheckTxListNotPresentInDbAsync(txList);
      }

    }


    [TestMethod]
    public async Task DoubleSpendCheckCleanUp()
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = eventBus.Subscribe<CleanUpTxTriggeredEvent>();

      List<Tx> txList = await CreateAndInsertTxAsync(false, true);

      (_, _, var firstBlockHash) = await InsertDoubleSpend();

      await CheckTxListPresentInDbAsync(txList, true);
      await CheckBlockPresentInDbAsync(firstBlockHash);

      var doubleSpends = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();

      Assert.AreEqual(1, doubleSpends.Count());
      Assert.IsTrue(doubleSpends.Any(x => new uint256(x.TxExternalId) == new uint256(Tx2Hash)));

      foreach (var txDoubleSpend in doubleSpends)
      {
        await TxRepositoryPostgres.SetBlockDoubleSpendSendDateAsync(txDoubleSpend.TxInternalId, txDoubleSpend.BlockInternalId, txDoubleSpend.DoubleSpendTxId, MockedClock.UtcNow);
      }

      doubleSpends = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();
      Assert.AreEqual(0, doubleSpends.Count());

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

        // check if everything in db was cleared
        await CheckBlockNotPresentInDb(firstBlockHash);
        await CheckTxListNotPresentInDbAsync(txList);
      }

    }



    [TestMethod]
    public async Task DoubleSpendMempoolCheckCleanUp()
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = eventBus.Subscribe<CleanUpTxTriggeredEvent>();

      (List<Tx> txList, uint256 firstBlockHash) = await CreateAndInsertTxWithMempoolAsync(dsCheckMempool: true);

      var doubleSpendTx = Transaction.Parse(Tx2Hex, Network.Main);
      List<byte[]> dsTxId = new List<byte[]>
        {
          doubleSpendTx.GetHash().ToBytes()
        };
      var txsWithDSCheck = (await TxRepositoryPostgres.GetTxsForDSCheckAsync(dsTxId, true)).ToArray();

      var txPayload = HelperTools.HexStringToByteArray(tx2Hex);
      foreach (var dsTx in txsWithDSCheck)
      {
        await TxRepositoryPostgres.InsertMempoolDoubleSpendAsync(
          dsTx.TxInternalId,
          dsTx.TxExternalIdBytes,
          txPayload);
      }
      var doubleSpends = (await TxRepositoryPostgres.GetTxsToSendMempoolDSNotificationsAsync()).ToList();
      Assert.AreEqual(1, doubleSpends.Count());

      foreach (var txDoubleSpend in doubleSpends)
      {
        await TxRepositoryPostgres.SetNotificationSendDateAsync(CallbackReason.DoubleSpendAttempt, txDoubleSpend.TxInternalId, -1, txDoubleSpend.DoubleSpendTxId, MockedClock.UtcNow);
      }

      doubleSpends = (await TxRepositoryPostgres.GetTxsToSendMempoolDSNotificationsAsync()).ToList();
      Assert.AreEqual(0, doubleSpends.Count());

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

        // check if everything in db was cleared
        await CheckBlockNotPresentInDb(firstBlockHash);
        await CheckTxListNotPresentInDbAsync(txList);
      }

    }

  }
}
