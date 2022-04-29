// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.DataEncoders;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  public class BlockParserTestBase : TestBase
  {
    // Tx1Hex -> Tx2Hex -> Tx3Hex -> Tx4Hex -> Tx5Hex
    protected const string Tx1Hash = "0dca20f17212114df792f64d385f5d72071d3969690474861a2e051efa72360d";
    protected const string Tx1Hex = "0100000002634d89c459a14c71867b33465114cf912e176761d393bfb11d41483191665fee5c0100006a47304402204af84a19bfb029ae3b9d934fde28b30d0b002dfa28804beb41b5c49770d800c002204358d0ee75e83626af6f02cc4dd10c74dd04902f73fc0a8853e97c41ca22812b412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff5216a100f5813731cdd1d4655e15ca5d5861b297d87ec07b1c94ce0ec2ed1d19010000006a47304402200855a77a74e1ce5a5f1afefba2f75ee5236b978743c0eee6eb43dcefbbe7d20902204e0c531e9e3c8e3d8167e60eaf455b223f5a847061170fc299bf31e5d6cbbbcf412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff02de0555d2030000001976a914145a10632a980a82e38a9bc45e21a0bcb915943b88accd92f10b9a0000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";
    protected const string Tx2Hash = "14f898bbeacc008277968dc89dd07121c19fa9f7d448f413e1047e9a4eba80e5";
    protected const string Tx2Hex = "01000000010d3672fa1e052e1a8674046969391d07725d5f384df692f74d111272f120ca0d010000006a473044022056b1f56de9e20c915d40dd45635efabace52e258b0b727446822e35ad732f95902202ba2aecc8f6f003a009d7125bbec4ac6ba21fd8e8ac41db9d1c5ccbd633f953f412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff0287f037c9030000001976a914299bc9e2afb881d43b2c3a3616161f1ac8960f4688ac3aa0b942960000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";
    protected const string Tx3Hash = "14e5caef831cfd3e6153435f0400216e4e49a1b1cb31dc279cda36a870a22a97";
    protected const string Tx3Hex = "0100000001e580ba4e9a7e04e113f448d4f7a99fc12171d09dc88d96778200cceabb98f814010000006a473044022029a0dd66ed2013815a16e8cbb69be866b2b414d4dcd808ec4a33f8cc9525c71102201896fa2b16cd8d6125970bb2319eefd4120766ae5617a6e4fc20eb3c862a8235412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff02dce4d628080000001976a914145a10632a980a82e38a9bc45e21a0bcb915943b88ac46b7e2198e0000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";
    protected const string Tx4Hash = "4f05e15c05f43f6777319e8b6af96cfd0eee95a46ee67211108e4e7cf6388a53";
    protected const string Tx4Hex = "0100000001972aa270a836da9c27dc31cbb1a1494e6e2100045f4353613efd1c83efcae514010000006a47304402200642260fdbc75904f68ea5d2812697ebb7514ae803875fcb478badd08a3df48102207ece1d7fab2282ac043cad7ab240b7197d312836b45275491340b68e29133c5c412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff0250b342e3030000001976a914299bc9e2afb881d43b2c3a3616161f1ac8960f4688acea01a0368a0000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";
    protected const string Tx5Hash = "56d4bdc3683290fa8927e26f9b9bf8bb1b2486a4917e700ec0dc466c141e2279";
    protected const string Tx5Hex = "0100000001538a38f67c4e8e101172e66ea495ee0efd6cf96a8b9e3177673ff4055ce1054f010000006b4830450221008c77794d317e7eb41af8ce0d16edd5c6506353cf5b273abd6cddf2773eca3c2f02204838aefa8b290b3279b2dea5d49b4a9569f7c9a283fed2ee534bcbc1d948f6b0412103f6aa4988d41e9522f33a7b71fba4e4f3cbc268409c4e581ce1b35560966c5495ffffffff021e5b8016040000001976a914299bc9e2afb881d43b2c3a3616161f1ac8960f4688acc0a41f20860000001976a91480ed030d83dd64545bc73b0074d35e0f6752b65888ac00000000";

    protected IRpcClient RpcClient;

    [TestInitialize]
    virtual public void TestInitialize()
    {
      base.Initialize(mockedServices: true);

      AddMockNode(0);

      var node = NodeRepository.GetNodes().First();
      RpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);
    }

    [TestCleanup]
    virtual public void TestCleanup()
    {
      base.Cleanup();
      rpcClientFactoryMock.CleanupBigBlocks();
    }

    protected async Task<List<Tx>> CreateAndInsertTxAsync(bool merkleProof, bool dsCheck, int? limit = null, string[] hashes = null, int txStatus = TxStatus.Accepted)
    {
      if (hashes == null)
      {
        hashes = new string[] { Tx1Hash, Tx2Hash, Tx3Hash, Tx4Hash, Tx5Hash };
      }
      string[] hexes = new string[] { Tx1Hex, Tx2Hex, Tx3Hex, Tx4Hex, Tx5Hex };
      if (limit == null)
      {
        limit = hashes.Length;
      }
      List<Tx> txList = new();
      for (int i = 0; i < limit; i++)
      {
        txList.Add(CreateNewTx(hashes[i], hexes[i], merkleProof, null, dsCheck, txStatus));
      }

      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txList, false);

      return txList;
    }

    public async Task<List<Transaction>> Transaction16mbList(int txsCount, bool merkleProof=false, bool dsCheck=false)
    {
      var stream = new MemoryStream(Encoders.Hex.DecodeData(File.ReadAllText(@"Data/16mb_tx.txt")));
      var bStream = new BitcoinStream(stream, false)
      {
        MaxArraySize = unchecked((int)uint.MaxValue)
      };
      var tx = Transaction.Create(Network.Main);

      tx.ReadWrite(bStream);

      var txId = tx.GetHash(int.MaxValue).ToString();
      _ = await CreateAndInsertTxAsync(merkleProof, dsCheck, 1, new string[] { txId.ToString() });

      List<Transaction> txs = new();
      for (int i = 0; i < txsCount; i++)
      {
        txs.Add(tx);
      }
      return txs;
    }

    public async Task<uint256> InsertMerkleProof()
    {
      var blockStream = await RpcClient.GetBlockAsStreamAsync(await RpcClient.GetBestBlockHashAsync());
      var firstBlock = HelperTools.ParseByteStreamToBlock(blockStream);
      var block = firstBlock.CreateNextBlockWithCoinbase(firstBlock.Transactions.First().Outputs.First().ScriptPubKey.GetDestinationPublicKeys().First(), new Money(50, MoneyUnit.MilliBTC), new ConsensusFactory());
      var firstBlockHash = firstBlock.GetHash();

      var tx = Transaction.Parse(Tx1Hex, Network.Main);
      block.Transactions[0] = tx;
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


      return firstBlockHash;
    }

    public async Task<(Transaction doubleSpendTx, Transaction originalTx,  uint256 firstBlockHash)> InsertDoubleSpend()
    {
      var node = NodeRepository.GetNodes().First();
      var rpcClient = (Mock.RpcClientMock)rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);

      var (_, firstBlockHash) = await CreateAndPublishNewBlockAsync(rpcClient, null, null);

      var tx = Transaction.Parse(Tx1Hex, Network.Main);
      var (forkHeight, _) = await CreateAndPublishNewBlockAsync(rpcClient, null, tx, true);

      var tx2 = Transaction.Parse(Tx2Hex, Network.Main);
      await CreateAndPublishNewBlockAsync(rpcClient, null, tx2, true);

      tx = Transaction.Parse(Tx3Hex, Network.Main);
      await CreateAndPublishNewBlockAsync(rpcClient, null, tx, true);

      tx = Transaction.Parse(Tx4Hex, Network.Main);
      await CreateAndPublishNewBlockAsync(rpcClient, null, tx, true);

      tx = Transaction.Parse(Tx5Hex, Network.Main);
      var (_, blockHash) = await CreateAndPublishNewBlockAsync(rpcClient, null, tx, true);
      PublishBlockHashToEventBus(blockHash);

      // Use already inserted tx2 with changing only Version so we get new TxId
      var doubleSpendTx = Transaction.Parse(Tx2Hex, Network.Main);
      doubleSpendTx.Version = 2;
      doubleSpendTx.GetHash();
      await CreateAndPublishNewBlockAsync(rpcClient, forkHeight, doubleSpendTx);

      return (doubleSpendTx, tx2, new uint256(firstBlockHash));
    }


  }
}
