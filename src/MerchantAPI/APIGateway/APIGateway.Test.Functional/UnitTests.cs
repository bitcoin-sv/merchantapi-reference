using MerchantAPI.APIGateway.Domain.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.Altcoins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class UnitTests : TestBase
  {
    //private const string testPrivateKeyWif = "cNpxQaWe36eHdfU3fo2jHVkWXVt5CakPDrZSYguoZiRHSz9rq8nF";
    private const string testAddress = "msRNSw5hHA1W1jXXadxMDMQCErX1X8whTk";

    [TestInitialize]
    virtual public void TestInitialize()
    {
      base.Initialize(mockedServices: true);
    }

    [TestCleanup]
    virtual public void TestCleanup()
    {
      base.Cleanup();
    }

    [TestMethod]
    public async Task SubmitAndCheckMultipleDSRead()
    {
      var address = BitcoinAddress.Create(testAddress, Network.RegTest);

      var tx1 = BCash.Instance.Regtest.CreateTransaction();
      tx1.Inputs.Add(new TxIn(new OutPoint(new uint256(txC1Hash), 0)));
      tx1.Inputs.Add(new TxIn(new OutPoint(new uint256(txC1Hash), 1)));
      tx1.Outputs.Add(new TxOut(new Money(1000L), address));

      var tx2 = BCash.Instance.Regtest.CreateTransaction();
      tx2.Inputs.Add(new TxIn(new OutPoint(new uint256(txC2Hash), 0)));
      tx2.Inputs.Add(new TxIn(new OutPoint(new uint256(txC2Hash), 1)));
      tx2.Outputs.Add(new TxOut(new Money(100L), address));


      var txList = new List<Tx>() 
      { 
        CreateNewTx(tx1.GetHash().ToString(), tx1.ToHex(), false, null, true),
        CreateNewTx(tx2.GetHash().ToString(), tx2.ToHex(), false, null, true)
      };
      await TxRepositoryPostgres.InsertTxsAsync(txList, false);

      var txs = await TxRepositoryPostgres.GetTxsForDSCheckAsync(new List<byte[]> { tx1.GetHash().ToBytes(), tx2.GetHash().ToBytes() }, true);

      Assert.AreEqual(2, txs.Count());
      Assert.AreEqual(2, txs.First().TxIn.Count);
      Assert.AreEqual(2, txs.Last().TxIn.Count);

      var readTx1 = txs.SingleOrDefault(x => x.TxIn.Any(y => new uint256(y.PrevTxId) == new uint256(txC1Hash)));

      Assert.IsNotNull(readTx1);
      Assert.AreEqual(0, readTx1.OrderderInputs.First().N);
      Assert.AreEqual(1, readTx1.OrderderInputs.Last().N);

      var readTx2 = txs.SingleOrDefault(x => x.TxIn.Any(y => new uint256(y.PrevTxId) == new uint256(txC2Hash)));

      Assert.IsNotNull(readTx2);
      Assert.AreEqual(0, readTx2.OrderderInputs.First().N);
      Assert.AreEqual(1, readTx2.OrderderInputs.Last().N);
    }
  }
}
