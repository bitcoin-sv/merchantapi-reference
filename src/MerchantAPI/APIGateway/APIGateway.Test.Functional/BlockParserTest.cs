// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
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
  [TestCategory("TestCategoryNo1")]
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

    private async Task CheckOnActiveChain(uint256[] blockHashes, bool expectedOnActiveChain)
    {
      foreach (var blockHash in blockHashes)
      {
        var blockInDb = await TxRepositoryPostgres.GetBlockAsync(blockHash.ToBytes());
        Assert.AreEqual(expectedOnActiveChain, blockInDb.OnActiveChain);
      }
    }

    private uint256[] CreateForkAsync(string txHex, Block nextBlock, int chainLength, int firstBlockHeight = 1, long amount = 50)
    {
      var pubKey = new Key().PubKey;
      int blockCount = 0;
      List<uint256> forkBlockHashes = new();
      // Setup 2nd chain 'chainLength' blocks long
      do
      {
        var tx = Transaction.Parse(txHex, Network.Main);
        var prevBlockHash = nextBlock.GetHash();
        nextBlock = nextBlock.CreateNextBlockWithCoinbase(pubKey, new Money(amount, MoneyUnit.MilliBTC), new ConsensusFactory());
        nextBlock.Header.HashPrevBlock = prevBlockHash;
        nextBlock.AddTransaction(tx);
        nextBlock.Check();
        rpcClientFactoryMock.AddKnownBlock(blockCount + firstBlockHeight, nextBlock.ToBytes());

        forkBlockHashes.Add(nextBlock.GetHash());
        blockCount++;
      }
      while (blockCount < chainLength);

      PublishBlockHashToEventBus(forkBlockHashes.Last().ToString());

      return forkBlockHashes.ToArray();
    }

    [TestMethod]
    public async Task TooLongForkCheck()
    {
      Assert.AreEqual(20, AppSettings.MaxBlockChainLengthForFork);
      _ = await CreateAndInsertTxAsync(false, true, 2);

      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      long blockCount;
      string blockHash;
      List<uint256> blockHashes = new();
      do
      {
        var tx = Transaction.Parse(Tx1Hex, Network.Main);
        (blockCount, blockHash) = await CreateAndPublishNewBlockAsync(rpcClient, null, tx, true);
        blockHashes.Add(new uint256(blockHash));
      }
      while (blockCount < 20);
      PublishBlockHashToEventBus(blockHash);

      var blockInDb = await TxRepositoryPostgres.GetBlockAsync(new uint256(blockHash).ToBytes());
      Assert.AreEqual(20, blockInDb.BlockHeight);
      await CheckOnActiveChain(blockHashes.ToArray(), true);

      // Setup 2nd chain 30 blocks long that will not be downloaded completely
      // (blockHeight=10 will be saved, blockheight=9 must not be saved)
      int splitHeight = 1;
      var nextBlock = NBitcoin.Block.Load(await rpcClient.GetBlockByHeightAsBytesAsync(splitHeight), Network.Main);
      uint256[] forkBlockHashes = CreateForkAsync(Tx2Hex, nextBlock, 30);
      blockInDb = await TxRepositoryPostgres.GetBlockAsync(forkBlockHashes.Last().ToBytes());
      Assert.AreEqual(30, blockInDb.BlockHeight);

      int firstForkBlockSaved = 9;
      uint256 forkBlockHeight10Hash = forkBlockHashes[firstForkBlockSaved];
      blockInDb = await TxRepositoryPostgres.GetBlockAsync(forkBlockHeight10Hash.ToBytes());
      Assert.IsNotNull(blockInDb);
      Assert.AreEqual(10, blockInDb.BlockHeight);
      uint256 forkBlockHeight9Hash = forkBlockHashes[firstForkBlockSaved - 1];
      Assert.IsNull(await TxRepositoryPostgres.GetBlockAsync(forkBlockHeight9Hash.ToBytes()));

      await CheckOnActiveChain(forkBlockHashes.Skip(firstForkBlockSaved).ToArray(), true);
      // blocks stay with onActiveChain = true because of MaxBlockChainLengthForFork
      await CheckOnActiveChain(blockHashes.Take(firstForkBlockSaved).ToArray(), true);
      await CheckOnActiveChain(blockHashes.Skip(firstForkBlockSaved).ToArray(), false);
    }

    [TestMethod]
    public async Task DoubleReorgCheck()
    {
      // block 0
      // blockHashes: blocks with height 1 and 2
      // forkBlockHashes - fork from block 1: blocks 1A, 2A, 3A
      // forkBlockHashes - fork from block 2: blocks 1, 2, 3B, 4B
      _ = await CreateAndInsertTxAsync(false, true, 3);

      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      long blockCount;
      string blockHash;
      List<uint256> blockHashes = new();
      do
      {
        var tx = Transaction.Parse(Tx1Hex, Network.Main);
        (blockCount, blockHash) = await CreateAndPublishNewBlockAsync(rpcClient, null, tx, true);
        blockHashes.Add(new uint256(blockHash));
      }
      while (blockCount < 2);
      PublishBlockHashToEventBus(blockHash);

      var blockInDb = await TxRepositoryPostgres.GetBlockAsync(new uint256(blockHash).ToBytes());
      Assert.AreEqual(2, blockInDb.BlockHeight);
      await CheckOnActiveChain(blockHashes.ToArray(), true);
      var nextBlockOrigin = Block.Load(await rpcClient.GetBlockByHeightAsBytesAsync(2), Network.Main);

      int splitHeight = 1;
      var nextBlock = Block.Load(await rpcClient.GetBlockByHeightAsBytesAsync(splitHeight), Network.Main);
      uint256[] forkBlockHashes = CreateForkAsync(Tx2Hex, nextBlock, 3, firstBlockHeight: splitHeight + 1, 30);

      blockInDb = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.AreEqual(4, blockInDb.BlockHeight); // splitHeight(1) + 3 = 4
      await CheckOnActiveChain(forkBlockHashes.ToArray(), true);
      await CheckOnActiveChain(blockHashes.Take(splitHeight).ToArray(), true);
      await CheckOnActiveChain(blockHashes.Skip(splitHeight).ToArray(), false);

      uint256[] forkOriginBlockHashes = CreateForkAsync(Tx3Hex, nextBlockOrigin, 3, firstBlockHeight: 3);
      // add again original two blocks to rpcClientFactoryMock
      rpcClientFactoryMock.AddKnownBlock(splitHeight, nextBlock.ToBytes());
      rpcClientFactoryMock.AddKnownBlock(2, nextBlockOrigin.ToBytes());

      blockInDb = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.AreEqual(5, blockInDb.BlockHeight); // splitHeight(2) + 3 = 5
      await CheckOnActiveChain(forkOriginBlockHashes.ToArray(), true);
      await CheckOnActiveChain(forkBlockHashes.ToArray(), false);
      await CheckOnActiveChain(blockHashes.ToArray(), true);
    }

    [TestMethod]
    public async Task DoubleMerkleProofCheck()
    {
      _ = await CreateAndInsertTxAsync(true, true, 2);

      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      var (blockCount, _) = await CreateAndPublishNewBlockAsync(rpcClient, null, null);

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
      Assert.IsTrue(stream.Length > (Const.Megabyte));
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
    public async Task TestSkipParsing()
    {
      var node = NodeRepository.GetNodes().First();
      var rpcClient = (Mock.RpcClientMock)rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      await CreateAndPublishNewBlockAsync(rpcClient, null, null);

      Assert.AreEqual(0, (await TxRepositoryPostgres.GetUnparsedBlocksAsync()).Length);

      var block = await TxRepositoryPostgres.GetBestBlockAsync();

      // we publish same NewBlockAvailableInDB as before
      var block2Parse = block;
      PublishBlockToEventBus(block2Parse);

      // best block must stay the same, since parsing was skipped
      var blockAfterRepublish = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.AreEqual(block.BlockInternalId, blockAfterRepublish.BlockInternalId);
      Assert.AreEqual(block.ParsedForMerkleAt, blockAfterRepublish.ParsedForMerkleAt);
      Assert.AreEqual(block.ParsedForDSAt, blockAfterRepublish.ParsedForDSAt);
    }
  }
}
