// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.Common.BitcoinRpc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Models.Events;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class BlockParserTest : TestBase
  {
    const string Tx1Hash = "0dca20f17212114df792f64d385f5d72071d3969690474861a2e051efa72360d";
    const string Tx1Hex = "0100000002634d89c459a14c71867b33465114cf912e176761d393bfb11d41483191665fee5c0100006a47304402204af84a19bfb029ae3b9d934fde28b30d0b002dfa28804beb41b5c49770d800c002204358d0ee75e83626af6f02cc4dd10c74dd04902f73fc0a8853e97c41ca22812b412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff5216a100f5813731cdd1d4655e15ca5d5861b297d87ec07b1c94ce0ec2ed1d19010000006a47304402200855a77a74e1ce5a5f1afefba2f75ee5236b978743c0eee6eb43dcefbbe7d20902204e0c531e9e3c8e3d8167e60eaf455b223f5a847061170fc299bf31e5d6cbbbcf412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff02de0555d2030000001976a914145a10632a980a82e38a9bc45e21a0bcb915943b88accd92f10b9a0000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";
    const string Tx2Hash = "14f898bbeacc008277968dc89dd07121c19fa9f7d448f413e1047e9a4eba80e5";
    const string Tx2Hex = "01000000010d3672fa1e052e1a8674046969391d07725d5f384df692f74d111272f120ca0d010000006a473044022056b1f56de9e20c915d40dd45635efabace52e258b0b727446822e35ad732f95902202ba2aecc8f6f003a009d7125bbec4ac6ba21fd8e8ac41db9d1c5ccbd633f953f412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff0287f037c9030000001976a914299bc9e2afb881d43b2c3a3616161f1ac8960f4688ac3aa0b942960000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";
    const string Tx3Hash = "14e5caef831cfd3e6153435f0400216e4e49a1b1cb31dc279cda36a870a22a97";
    const string Tx3Hex = "0100000001e580ba4e9a7e04e113f448d4f7a99fc12171d09dc88d96778200cceabb98f814010000006a473044022029a0dd66ed2013815a16e8cbb69be866b2b414d4dcd808ec4a33f8cc9525c71102201896fa2b16cd8d6125970bb2319eefd4120766ae5617a6e4fc20eb3c862a8235412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff02dce4d628080000001976a914145a10632a980a82e38a9bc45e21a0bcb915943b88ac46b7e2198e0000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";
    const string Tx4Hash = "4f05e15c05f43f6777319e8b6af96cfd0eee95a46ee67211108e4e7cf6388a53";
    const string Tx4Hex = "0100000001972aa270a836da9c27dc31cbb1a1494e6e2100045f4353613efd1c83efcae514010000006a47304402200642260fdbc75904f68ea5d2812697ebb7514ae803875fcb478badd08a3df48102207ece1d7fab2282ac043cad7ab240b7197d312836b45275491340b68e29133c5c412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff0250b342e3030000001976a914299bc9e2afb881d43b2c3a3616161f1ac8960f4688acea01a0368a0000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";
    const string Tx5Hash = "56d4bdc3683290fa8927e26f9b9bf8bb1b2486a4917e700ec0dc466c141e2279";
    const string Tx5Hex = "0100000001538a38f67c4e8e101172e66ea495ee0efd6cf96a8b9e3177673ff4055ce1054f010000006b4830450221008c77794d317e7eb41af8ce0d16edd5c6506353cf5b273abd6cddf2773eca3c2f02204838aefa8b290b3279b2dea5d49b4a9569f7c9a283fed2ee534bcbc1d948f6b0412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff021e5b8016040000001976a914299bc9e2afb881d43b2c3a3616161f1ac8960f4688acc0a41f20860000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";

    IRpcClient RpcClient;

    [TestInitialize]
    public void TestInitialize()
    {
      base.Initialize(mockedServices: true);
      var mocNode = new Node(0, "mockNode0", 0, "mockuserName", "mockPassword", "This is a mock node",
      (int)NodeStatus.Connected, null, null);

      _ = Nodes.CreateNodeAsync(mocNode).Result;

      var node = NodeRepository.GetNodes().First();
      RpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);
    }

    [TestCleanup]
    public void TestCleanup()
    {
      base.Cleanup();
    }

    [TestMethod]
    public async Task MerkleProofCheck()
    {
      IList<Tx> txList = new List<Tx>();
      txList.Add(CreateNewTx(Tx1Hash, Tx1Hex, true, false));
      txList.Add(CreateNewTx(Tx2Hash, Tx2Hex, true, false));
      txList.Add(CreateNewTx(Tx3Hash, Tx3Hex, true, false));
      txList.Add(CreateNewTx(Tx4Hash, Tx4Hex, true, false));
      txList.Add(CreateNewTx(Tx5Hash, Tx5Hex, true, false));
      await TxRepositoryPostgres.InsertTxsAsync(txList);

      var blockHex = await RpcClient.GetBlockAsBytesAsync(await RpcClient.GetBestBlockHashAsync());
      var firstBlock = NBitcoin.Block.Load(blockHex, Network.Main);
      var block = firstBlock.CreateNextBlockWithCoinbase(firstBlock.Transactions.First().Outputs.First().ScriptPubKey.GetDestinationPublicKeys().First(), new Money(50, MoneyUnit.MilliBTC), new ConsensusFactory());

      var tx = Transaction.Parse(Tx1Hex, Network.Main);
      block.AddTransaction(tx);
      tx = Transaction.Parse(Tx2Hex, Network.Main);
      block.AddTransaction(tx);
      tx = Transaction.Parse(Tx3Hex, Network.Main);
      block.AddTransaction(tx);
      tx = Transaction.Parse(Tx4Hex, Network.Main);
      block.AddTransaction(tx);
      tx = Transaction.Parse(Tx5Hex, Network.Main);
      block.AddTransaction(tx);

      rpcClientFactoryMock.AddKnownBlock((await RpcClient.GetBlockCountAsync()) + 1, block.ToBytes());
      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      PublishBlockHashToEventBus(await rpcClient.GetBestBlockHashAsync());

      var dbRecords = (await TxRepositoryPostgres.GetTxsToSendMerkleProofNotificationsAsync(0, 10000)).ToList();

      Assert.AreEqual(5, dbRecords.Count());
      Assert.IsTrue(dbRecords.Any(x => new uint256(x.TxExternalId) == new uint256(Tx1Hash)));
      Assert.IsTrue(dbRecords.Any(x => new uint256(x.TxExternalId) == new uint256(Tx2Hash)));
      Assert.IsTrue(dbRecords.Any(x => new uint256(x.TxExternalId) == new uint256(Tx3Hash)));
      Assert.IsTrue(dbRecords.Any(x => new uint256(x.TxExternalId) == new uint256(Tx4Hash)));
      Assert.IsTrue(dbRecords.Any(x => new uint256(x.TxExternalId) == new uint256(Tx5Hash)));
    }

    [TestMethod]
    public async Task DoubleSpendCheck()
    {
      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      IList<Tx> txList = new List<Tx>();
      txList.Add(CreateNewTx(Tx1Hash, Tx1Hex, false, true));
      txList.Add(CreateNewTx(Tx2Hash, Tx2Hex, false, true));
      txList.Add(CreateNewTx(Tx3Hash, Tx3Hex, false, true));
      txList.Add(CreateNewTx(Tx4Hash, Tx4Hex, false, true));
      txList.Add(CreateNewTx(Tx5Hash, Tx5Hex, false, true));
      await TxRepositoryPostgres.InsertTxsAsync(txList);


      var tx = Transaction.Parse(Tx1Hex, Network.Main);
      long forkHeight = await CreateAndPublishNewBlock(rpcClient, null, tx);

      var tx2 = Transaction.Parse(Tx2Hex, Network.Main);
      await CreateAndPublishNewBlock(rpcClient, null, tx2);

      tx = Transaction.Parse(Tx3Hex, Network.Main);
      await CreateAndPublishNewBlock(rpcClient, null, tx);

      tx = Transaction.Parse(Tx4Hex, Network.Main);
      await CreateAndPublishNewBlock(rpcClient, null, tx);

      tx = Transaction.Parse(Tx5Hex, Network.Main);
      await CreateAndPublishNewBlock(rpcClient, null, tx);


      // Use already inserted tx2 with changing only Version so we get new TxId
      var doubleSpendTx = Transaction.Parse(Tx2Hex, Network.Main);
      doubleSpendTx.Version = 2;
      doubleSpendTx.GetHash();
      await CreateAndPublishNewBlock(rpcClient, forkHeight, doubleSpendTx);

      var dbRecords = (await TxRepositoryPostgres.GetTxsToSendBlockDSNotificationsAsync()).ToList();

      Assert.AreEqual(1, dbRecords.Count());
      Assert.AreEqual(doubleSpendTx.GetHash(), new uint256(dbRecords[0].DoubleSpendTxId));
      Assert.AreEqual(doubleSpendTx.Inputs.First().PrevOut.Hash, tx2.Inputs.First().PrevOut.Hash);
      Assert.AreEqual(doubleSpendTx.Inputs.First().PrevOut.N, tx2.Inputs.First().PrevOut.N);
      Assert.AreNotEqual(doubleSpendTx.GetHash(), tx2.GetHash());
    }

    [TestMethod]
    public async Task TooLongForkCheck()
    {
      Assert.AreEqual(20, AppSettings.MaxBlockChainLengthForFork);
      IList<Tx> txList = new List<Tx>();
      txList.Add(CreateNewTx(Tx1Hash, Tx1Hex, false, true));
      txList.Add(CreateNewTx(Tx2Hash, Tx2Hex, false, true));
      await TxRepositoryPostgres.InsertTxsAsync(txList);

      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      long blockCount;
      do
      {
        var tx = Transaction.Parse(Tx1Hex, Network.Main);
        blockCount = await CreateAndPublishNewBlock(rpcClient, null, tx);
      }
      while (blockCount < 20);

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
    public async Task DoubleMerkleProofCheck()
    {
      IList<Tx> txList = new List<Tx>();
      txList.Add(CreateNewTx(Tx1Hash, Tx1Hex, true, true));
      txList.Add(CreateNewTx(Tx2Hash, Tx2Hex, true, true));
      await TxRepositoryPostgres.InsertTxsAsync(txList);

      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      var blockCount = await CreateAndPublishNewBlock(rpcClient, null);

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
  }
}
