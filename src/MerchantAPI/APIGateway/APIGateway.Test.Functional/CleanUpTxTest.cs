// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Test.Functional.CleanUpTx;
using MerchantAPI.Common.Json;
using MerchantAPI.Common.Test.Clock;
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
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class CleanUpTxTest : BlockParserTestBase
  {
    readonly int cancellationTimeout = 30000; // 30 seconds
    int cleanUpTxAfterDays;
    int cleanUpTxAfterMempoolExpiredDays;
    CleanUpTxWithPauseHandlerForTest cleanUpTxService;

    [TestInitialize]
    override public void TestInitialize()
    {
      base.TestInitialize();

      cleanUpTxService = server.Services.GetRequiredService<CleanUpTxWithPauseHandlerForTest>();
      cleanUpTxAfterDays = AppSettings.CleanUpTxAfterDays.Value;
      cleanUpTxAfterMempoolExpiredDays = AppSettings.CleanUpTxAfterMempoolExpiredDays.Value;
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
      var blockStream = await RpcClient.GetBlockAsStreamAsync(await RpcClient.GetBestBlockHashAsync());
      var firstBlock = HelperTools.ParseByteStreamToBlock(blockStream);
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
      var txInternalId = await TxRepositoryPostgres.GetTransactionInternalIdAsync(new uint256(txHash).ToBytes());
      Assert.IsNotNull(txInternalId);
      var blocks = (await TxRepositoryPostgres.GetBlocksByTxIdAsync(txInternalId.Value));
      Assert.IsTrue(blockPresent ? blocks.Any() : !blocks.Any());
      return txInternalId.Value;
    }

    private async Task CheckTxNotPresentInDbAsync(string txHash, long txInternalId)
    {
      // tx and block are main tables - the other tables reference these two and have 'delete cascade constraint' declared  
      var txDeletedInternalId = await TxRepositoryPostgres.GetTransactionInternalIdAsync(new uint256(txHash).ToBytes());
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
      foreach(var tx in txList)
      {
        await CheckTxNotPresentInDbAsync(tx.TxExternalId.ToString(), tx.TxInternalId);
      }
    }

    private async Task CheckBlockNotPresentInDb(uint256 blockHash)
    {
      var firstBlockTest = await TxRepositoryPostgres.GetBlockAsync(blockHash.ToBytes());
      Assert.IsNull(firstBlockTest);
    }

    private async Task ResumeAndWaitForCleanup(MerchantAPI.Common.EventBus.EventBusSubscription<CleanUpTxTriggeredEvent> cleanUpTxTriggeredSubscription)
    {
      using CancellationTokenSource cts = new(cancellationTimeout);
      await cleanUpTxService.ResumeAsync(cts.Token);

      // wait for cleanUpTx service to finish execute
      await cleanUpTxTriggeredSubscription.ReadAsync(cts.Token);
    }


    [TestMethod]
    public async Task NoneTxToCleanUpCheck()
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = EventBus.Subscribe<CleanUpTxTriggeredEvent>();
      (List<Tx> txList, _) = await CreateAndInsertTxWithMempoolAsync();

      await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

      // check if everything in db is still present
      await CheckTxListPresentInDbAsync(txList);

    }

    [DataRow(TxStatus.NodeRejected)]
    [DataRow(TxStatus.SentToNode)]
    [DataRow(TxStatus.UnknownOldTx)]
    [DataRow(TxStatus.Accepted)]
    [DataRow(TxStatus.MissingInputsMaxRetriesReached)]
    [TestMethod]
    public async Task TxWithoutBlockCheckCleanUp(int txStatus)
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = EventBus.Subscribe<CleanUpTxTriggeredEvent>();

      var txList = await CreateAndInsertTxAsync(false, false, txStatus: txStatus);

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

        if (txStatus == TxStatus.Accepted)
        {
          await CheckTxListPresentInDbAsync(txList, false);
        }
        else
        {
          await CheckTxListNotPresentInDbAsync(txList);
        }

        cleanUpTxService.Pause();
      }

      // we clean up txs with mempool status later
      if (txStatus == TxStatus.Accepted)
      {
        using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterMempoolExpiredDays)))
        {
          await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

          // check if everything in db was cleared
          await CheckTxListNotPresentInDbAsync(txList);
        }
      }
    }

    [TestMethod]
    public async Task MerkleProofCheckCleanUp()
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = EventBus.Subscribe<CleanUpTxTriggeredEvent>();

      List<Tx> txList = await CreateAndInsertTxAsync(true, false);

      uint256 firstBlockHash = await InsertMerkleProof();
      WaitUntilEventBusIsIdle();

      await CheckTxListPresentInDbAsync(txList, true);
      await CheckBlockPresentInDbAsync(firstBlockHash);

      var merkleProofTxs = (await TxRepositoryPostgres.GetTxsToSendMerkleProofNotificationsAsync(0, 10000)).ToList();

      Assert.AreEqual(5, merkleProofTxs.Count);
      Assert.IsTrue(merkleProofTxs.Any(x => new uint256(x.TxExternalId) == new uint256(Tx2Hash)));

      foreach (var txWithMerkle in merkleProofTxs)
      {
        await TxRepositoryPostgres.SetNotificationSendDateAsync(CallbackReason.MerkleProof, txWithMerkle.TxInternalId, txWithMerkle.BlockInternalId, null, MockedClock.UtcNow);
      }

      merkleProofTxs = (await TxRepositoryPostgres.GetTxsToSendMerkleProofNotificationsAsync(0, 10000)).ToList();
      Assert.AreEqual(0, merkleProofTxs.Count);

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
      var cleanUpTxTriggeredSubscription = EventBus.Subscribe<CleanUpTxTriggeredEvent>();

      List<Tx> txList = await CreateAndInsertTxAsync(false, true);

      (_, _, var firstBlockHash) = await InsertDoubleSpend();

      await CheckTxListPresentInDbAsync(txList, true);
      await CheckBlockPresentInDbAsync(firstBlockHash);

      var doubleSpends = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();

      Assert.AreEqual(1, doubleSpends.Count);
      Assert.IsTrue(doubleSpends.Any(x => new uint256(x.TxExternalId) == new uint256(Tx2Hash)));

      foreach (var txDoubleSpend in doubleSpends)
      {
        await TxRepositoryPostgres.SetBlockDoubleSpendSendDateAsync(txDoubleSpend.TxInternalId, txDoubleSpend.BlockInternalId, txDoubleSpend.DoubleSpendTxId, MockedClock.UtcNow);
      }

      doubleSpends = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();
      Assert.AreEqual(0, doubleSpends.Count);

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

        // check if everything on active chain was cleared
        await CheckBlockNotPresentInDb(firstBlockHash);
        await CheckTxListNotPresentInDbAsync(txList.Where(x => x.TxExternalId != new uint256(Tx2Hash)).ToList());
        await CheckTxPresentInDbAsync(Tx2Hash);
      }

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterMempoolExpiredDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

        await CheckTxListNotPresentInDbAsync(txList.Where(x => x.TxExternalId == new uint256(Tx2Hash)).ToList());
      }
    }


    [TestMethod]
    public async Task DoubleSpendMempoolCheckCleanUp()
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = EventBus.Subscribe<CleanUpTxTriggeredEvent>();

      (List<Tx> txList, uint256 firstBlockHash) = await CreateAndInsertTxWithMempoolAsync(dsCheckMempool: true);

      var doubleSpendTx = Transaction.Parse(Tx2Hex, Network.Main);
      List<byte[]> dsTxId = new()
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
        Assert.AreEqual(TxStatus.Accepted, await TxRepositoryPostgres.GetTransactionStatusAsync(dsTx.TxExternalIdBytes));
      }
      var doubleSpends = (await TxRepositoryPostgres.GetTxsToSendMempoolDSNotificationsAsync()).ToList();
      Assert.AreEqual(1, doubleSpends.Count);

      foreach (var txDoubleSpend in doubleSpends)
      {
        await TxRepositoryPostgres.SetNotificationSendDateAsync(CallbackReason.DoubleSpendAttempt, txDoubleSpend.TxInternalId, -1, txDoubleSpend.DoubleSpendTxId, MockedClock.UtcNow);
      }

      doubleSpends = (await TxRepositoryPostgres.GetTxsToSendMempoolDSNotificationsAsync()).ToList();
      Assert.AreEqual(0, doubleSpends.Count);

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

        // check if everything in db was cleared
        await CheckBlockNotPresentInDb(firstBlockHash);
        await CheckTxListNotPresentInDbAsync(txList);
      }
    }

    [TestMethod]
    public async Task TestCleanUpWhenResubmitInProcess()
    {
      //arrange
      cleanUpTxService.Pause();
      var cleanUpTxTriggeredSubscription = EventBus.Subscribe<CleanUpTxTriggeredEvent>();

      Assert.IsNotNull(FeeQuoteRepository.GetFeeQuoteById(1));
      List<Tx> txList = await CreateAndInsertTxAsync(false, false, 3, txStatus: TxStatus.Accepted);

      var mapi = server.Services.GetRequiredService<IMapi>();
      var mempoolCalledAt = MockedClock.UtcNow;
      var mempoolTxs = Array.Empty<string>();
      var txs = await TxRepositoryPostgres.GetMissingTransactionsAsync(mempoolTxs, mempoolCalledAt);
      Assert.AreEqual(3, txs.Length);

      //act
      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpTxAfterMempoolExpiredDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);

        // check if everything in db was cleared
        await CheckTxListNotPresentInDbAsync(txList);

        // we consider all txs were successfully submitted to node
        // but cleanupTx BG service deleted them before update on database
        // we should get no errors even if we try to update txs that are no longer present
        await TxRepositoryPostgres.UpdateTxsOnResubmitAsync(null, txs.Select(x => new Tx
        {
          // on resubmit we only update submittedAt and txStatus
          TxInternalId = x.TxInternalId,
          TxExternalId = x.TxExternalId,
          SubmittedAt = MockedClock.UtcNow,
          TxStatus = x.TxStatus,
          PolicyQuoteId = x.PolicyQuoteId,
          UpdateTx = Tx.UpdateTxMode.TxStatusAndResubmittedAt
        }).ToList());

        // update of txsWithMissingInputs should also not raise any error
        await TxRepositoryPostgres.UpdateTxsOnResubmitAsync(null, txs.Select(x => new Tx
        {
          TxInternalId = x.TxInternalId,
          SubmittedAt = MockedClock.UtcNow,
          TxStatus = TxStatus.MissingInputsMaxRetriesReached
        }).ToList());
      }
    }
  }
}