// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.Common.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class BlockParserTest : BlockParserTestBase
  {

    [TestInitialize]
    override public void TestInitialize()
    {
      base.TestInitialize();
    }

    [TestCleanup]
    override public void TestCleanup()
    {
      base.TestCleanup();
    }


    [TestMethod]
    public async Task MerkleProofCheck()
    {
      _ = await CreateAndInsertTxAsync(true, false);

      _ = await InsertMerkleProof();
    }


    [TestMethod]
    public async Task DoubleSpendCheck()
    {

      _ = await CreateAndInsertTxAsync(false, true);

      (var doubleSpendTx, var tx2, _) = await InsertDoubleSpend();


      var dbRecords = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();

      Assert.AreEqual(1, dbRecords.Count);
      Assert.AreEqual(doubleSpendTx.GetHash(), new uint256(dbRecords[0].DoubleSpendTxId));
      Assert.AreEqual(doubleSpendTx.Inputs.First().PrevOut.Hash, tx2.Inputs.First().PrevOut.Hash);
      Assert.AreEqual(doubleSpendTx.Inputs.First().PrevOut.N, tx2.Inputs.First().PrevOut.N);
      Assert.AreNotEqual(doubleSpendTx.GetHash(), tx2.GetHash());
    }


    [TestMethod]
    public virtual async Task TooLongForkCheck()
    {
      Assert.AreEqual(20, AppSettings.MaxBlockChainLengthForFork);
      _ = await CreateAndInsertTxAsync(false, true, 2);

      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      long blockCount;
      string blockHash;
      do
      {
        var tx = Transaction.Parse(Tx1Hex, Network.Main);
        (blockCount, blockHash) = await CreateAndPublishNewBlock(rpcClient, null, tx, true);
      }
      while (blockCount < 20);
      PublishBlockHashToEventBus(blockHash);

      uint256 forkBlockHeight8Hash = uint256.Zero;
      uint256 forkBlockHeight9Hash = uint256.Zero;
      var nextBlock = NBitcoin.Block.Load(await rpcClient.GetBlockByHeightAsBytesAsync(1), Network.Main);
      var pubKey = new Key().PubKey;
      blockCount = 1;
      // Setup 2nd chain 30 blocks long that will not be downloaded completely (blockHeight=9 will be saved, blockheight=8 must not be saved)
      do
      {
        var tx = Transaction.Parse(Tx2Hex, Network.Main);
        var prevBlockHash = nextBlock.GetHash();
        nextBlock = nextBlock.CreateNextBlockWithCoinbase(pubKey, new Money(50, MoneyUnit.MilliBTC), new ConsensusFactory());
        nextBlock.Header.HashPrevBlock = prevBlockHash;
        nextBlock.AddTransaction(tx);
        nextBlock.Check();
        rpcClientFactoryMock.AddKnownBlock(blockCount, nextBlock.ToBytes());

        if (blockCount == 9) forkBlockHeight9Hash = nextBlock.GetHash();
        if (blockCount == 8) forkBlockHeight8Hash = nextBlock.GetHash();
        blockCount++;
      }
      while (blockCount < 30);
      PublishBlockHashToEventBus(await rpcClient.GetBestBlockHashAsync());

      Assert.IsNotNull(await TxRepositoryPostgres.GetBlockAsync(forkBlockHeight9Hash.ToBytes()));
      Assert.IsNull(await TxRepositoryPostgres.GetBlockAsync(forkBlockHeight8Hash.ToBytes()));
    }


    [TestMethod]
    public virtual async Task DoubleMerkleProofCheck()
    {
      _ = await CreateAndInsertTxAsync(true, true, 2);

      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      var (blockCount, _) = await CreateAndPublishNewBlock(rpcClient, null, null);

      NBitcoin.Block forkBlock = null;
      var nextBlock = NBitcoin.Block.Load(await rpcClient.GetBlockByHeightAsBytesAsync(0), Network.Main);
      var pubKey = nextBlock.Transactions.First().Outputs.First().ScriptPubKey.GetDestinationPublicKeys().First();
      // tx1 will be mined in block 1 and the notification has to be sent only once (1 insert into txBlock)
      var tx1 = Transaction.Parse(Tx1Hex, Network.Main);
      // tx2 will be mined in block 15 in chain 1 and in the block 20 in longer chain 2 and the notification 
      // has to be sent twice (2 inserts into txBlock)
      var tx2 = Transaction.Parse(Tx2Hex, Network.Main);
      // Setup first chain, 20 blocks long
      do
      {
        var prevBlockHash = nextBlock.GetHash();
        nextBlock = nextBlock.CreateNextBlockWithCoinbase(pubKey, new Money(50, MoneyUnit.MilliBTC), new ConsensusFactory());
        nextBlock.Header.HashPrevBlock = prevBlockHash;
        if (blockCount == 1)
        {
          nextBlock.AddTransaction(tx1);
        }
        if (blockCount == 9)
        {
          forkBlock = nextBlock;
        }
        if (blockCount == 15)
        {
          nextBlock.AddTransaction(tx2);
        }
        nextBlock.Check();
        rpcClientFactoryMock.AddKnownBlock(blockCount++, nextBlock.ToBytes());
      }
      while (blockCount < 20);
      PublishBlockHashToEventBus(await rpcClient.GetBestBlockHashAsync());

      nextBlock = forkBlock;
      blockCount = 10;
      // Setup second chain
      do
      {
        var prevBlockHash = nextBlock.GetHash();
        nextBlock = nextBlock.CreateNextBlockWithCoinbase(pubKey, new Money(50, MoneyUnit.MilliBTC), new ConsensusFactory());
        nextBlock.Header.HashPrevBlock = prevBlockHash;
        if (blockCount == 20)
        {
          nextBlock.AddTransaction(tx2);
        }
        nextBlock.Check();
        rpcClientFactoryMock.AddKnownBlock(blockCount++, nextBlock.ToBytes());
      }
      while (blockCount < 21);
      PublishBlockHashToEventBus(await rpcClient.GetBestBlockHashAsync());

      var merkleProofs = (await TxRepositoryPostgres.GetTxsToSendMerkleProofNotificationsAsync(0, 100)).ToArray();

      Assert.AreEqual(3, merkleProofs.Length);
      Assert.IsTrue(merkleProofs.Count(x => new uint256(x.TxExternalId).ToString() == Tx1Hash) == 1);
      // Tx2 must have 2 requests for merkle proof notification (blocks 15 and 20)
      Assert.IsTrue(merkleProofs.Count(x => new uint256(x.TxExternalId).ToString() == Tx2Hash) == 2);
      Assert.IsTrue(merkleProofs.Any(x => new uint256(x.TxExternalId).ToString() == Tx2Hash && x.BlockHeight == 15));
      Assert.IsTrue(merkleProofs.Any(x => new uint256(x.TxExternalId).ToString() == Tx2Hash && x.BlockHeight == 20));
    }

    [TestMethod]
    public void GetHashFromBigTransaction()
    {
      var stream = new MemoryStream(Encoders.Hex.DecodeData(File.ReadAllText(@"Data/big_tx.txt")));
      Assert.IsTrue(stream.Length > (1024 * 1024));
      var bStream = new BitcoinStream(stream, false)
      {
        MaxArraySize = unchecked((int)uint.MaxValue)
      };

      var tx = Transaction.Create(Network.Main);
      tx.ReadWrite(bStream);
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => tx.GetHash());
      Assert.IsTrue(tx.GetHash(int.MaxValue) != uint256.Zero);
    }


    [TestMethod]
    public virtual async Task TestSkipParsing()
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

      var block = await TxRepositoryPostgres.GetBestBlockAsync();

      // we publish same NewBlockAvailableInDB as before
      var block2Parse = block;
      EventBus.Publish(new NewBlockAvailableInDB
      {
        BlockDBInternalId = block2Parse.BlockInternalId,
        BlockHash = new uint256(block2Parse.BlockHash).ToString()
      });

      WaitUntilEventBusIsIdle();

      // best block must stay the same, since parsing was skipped
      var blockAfterRepublish = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.AreEqual(block.BlockInternalId, blockAfterRepublish.BlockInternalId);
      Assert.AreEqual(block.ParsedForMerkleAt, blockAfterRepublish.ParsedForMerkleAt);
      Assert.AreEqual(block.ParsedForDSAt, blockAfterRepublish.ParsedForDSAt);
    }

    [DataRow(750)] // block of size 2.1GB
    [DataRow(1500)] // block of size > 4 GB
    [TestMethod]
    public async Task TestBigBlocks(double txsCount)
    {
      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      var stream = new MemoryStream(Encoders.Hex.DecodeData(File.ReadAllText(@"Data/16mb_tx.txt")));
      var bStream = new BitcoinStream(stream, false)
      {
        MaxArraySize = unchecked((int)uint.MaxValue)
      };
      var tx = Transaction.Create(Network.Main);

      tx.ReadWrite(bStream);

      var txId = tx.GetHash(int.MaxValue).ToString();
      _ = await CreateAndInsertTxAsync(false, true, 2, new string[] { txId.ToString() });

      List<Transaction> txs = new();
      for (int i = 0; i < txsCount; i++)
      {
        txs.Add(tx);
      }

      (_, string blockHash) = await CreateAndPublishNewBlockWithTxs(rpcClient, null, txs.ToArray(), true, true);

      var block = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.IsFalse(HelperTools.AreByteArraysEqual(block.BlockHash, new uint256(blockHash).ToBytes()));

      PublishBlockHashToEventBus(blockHash);

      WaitUntilEventBusIsIdle();

      block = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.IsTrue(HelperTools.AreByteArraysEqual(block.BlockHash, new uint256(blockHash).ToBytes()));
      Assert.AreEqual(0, (await TxRepositoryPostgres.GetUnparsedBlocksAsync()).Length);

      // check if block was correctly parsed
      var blockStream = await RpcClient.GetBlockAsStreamAsync(await RpcClient.GetBestBlockHashAsync());
      var parsedBlock = HelperTools.ParseByteStreamToBlock(blockStream);
      Assert.AreEqual(txsCount + 1, parsedBlock.Transactions.Count);
    }
  }
}
