// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using CsvHelper;
using CsvHelper.Configuration;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using NBitcoin.Altcoins;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.SmokeTest
{
  class Program
  {
    static async Task<int> FindUTXOs(string configFileName, IHttpClientFactory httpClientFactory)
    {
      var (rpcClient, config) = await InitRpcClient(configFileName, httpClientFactory);

      var uTXOs = await rpcClient.ListUnspentAsync();

      var printFirst10UTxOs = uTXOs.OrderByDescending(x => x.Amount).Take(10);

      Console.WriteLine("Top 10 unspent transaction outputs (full list is saved to utxos.csv):");
      foreach (var uTxO in printFirst10UTxOs)
      {
        Console.WriteLine(uTxO);
      }

      GenerateUTXOCsv(uTXOs);

      return 0;
    }

    static async Task<int> GenerateBlocks(int numOfBlocks, string configFileName, IHttpClientFactory httpClientFactory)
    {
      var (rpcClient, _) = await InitRpcClient(configFileName, httpClientFactory);

      var blocks = await rpcClient.GenerateAsync(numOfBlocks);

      Console.WriteLine("Generated block:");
      foreach (var block in blocks)
      {
        Console.WriteLine(block);
      }

      return 0;
    }

    static async Task<int> SubmitTxs(string uTxId, int numOfOutputs, int chainLength, bool unconfirmedAncestor, int batchSize, string configFileName, IHttpClientFactory httpClientFactory)
    {
      List<(Transaction Tx, bool Parent)> txList = new();
      var (rpcClient, config) = await InitRpcClient(configFileName, httpClientFactory);

      await EnsureMapiIsConnectedToNodeAsync(config);

      var savedutxos = ReadUTXOCsv();

      if(!savedutxos.Any())
        savedutxos = await rpcClient.ListUnspentAsync();

      var utxoData = savedutxos.FirstOrDefault(x => x.TxId == uTxId);

      if (utxoData == null) {
        Console.WriteLine($"TxId ({uTxId}) is not valid, execute findutxos first and select one txId from the generated list.");
        return 1;
      }

      var tx = BCash.Instance.Regtest.CreateTransaction();

      var key = Key.Parse(await rpcClient.DumpPrivKeyAsync(utxoData.Address), Network.RegTest);

      var coin = new Coin(new OutPoint(new uint256(utxoData.TxId), utxoData.Vout),
           new TxOut(Money.Coins(utxoData.Amount), Script.FromHex(utxoData.ScriptPubKey)));
      
      long remainingSatoshis = coin.Amount.Satoshi;
      tx.Inputs.Add(new TxIn(coin.Outpoint));

      var outMoney = Money.Coins(utxoData.Amount).Satoshi / (numOfOutputs + 1);
      Key keyForChain = null;

      for (int n = 0; n < numOfOutputs; n++)
      {
        var bsvAddress = BitcoinAddress.Create(await rpcClient.GetNewAddressAsync(), Network.RegTest);

        if (n == 0)
          keyForChain = Key.Parse(await rpcClient.DumpPrivKeyAsync(bsvAddress.ToString()), Network.RegTest);

        tx.Outputs.Add(outMoney, bsvAddress);
        remainingSatoshis -= outMoney;
      }
      remainingSatoshis -= tx.GetVirtualSize() * 2;
      tx.Outputs.Add(Money.Satoshis(remainingSatoshis), BitcoinAddress.Create(utxoData.Address, Network.RegTest));

      tx.Sign(key.GetBitcoinSecret(Network.RegTest), coin);

      txList.Add((tx, true));
      txList.AddRange((await CreateChain2Async(rpcClient, tx, numOfOutputs, chainLength)).Select(x => (x, false)));

      if (unconfirmedAncestor)
      {
        var lastTx = txList.Last();
        foreach(var (Tx, Parent) in txList.SkipLast(1))
          await rpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(Tx.ToHex()), true, false, null);

        txList = new List<(Transaction Tx, bool Parent)>
        {
          lastTx
        };
      }

      var client = new HttpClient();

      var requests = txList.Select(x => new SubmitTransactionViewModel
      {
        RawTx = x.Tx.ToHex(),
        CallbackUrl = config.Callback.Url,
        CallbackToken = config.Callback.Token,
        DsCheck = config.Callback.DsCheck,
        MerkleProof = config.Callback.MerkleProof,
        MerkleFormat = config.Callback.MerkleFormat
      });
      
      

      if (batchSize == 1)
      {
        Console.WriteLine($"Sending transaction by transaction.");
        string mapiUrl = config.MapiConfig.MapiUrl + "mapi/tx";

        foreach (var req in requests)
        {
          var requestString = HelperTools.JSONSerialize(req, false);
          var response = await client.PostAsync(mapiUrl, new StringContent(requestString, new UTF8Encoding(false), MediaTypeNames.Application.Json));

          var responseAsString = await response.Content.ReadAsStringAsync();

          if (!response.IsSuccessStatusCode)
          {
            Console.WriteLine($"Error while submitting transaction request {responseAsString}");
          }
          else
          {
            var rEnvelope = HelperTools.JSONDeserialize<SignedPayloadViewModel>(responseAsString);
            var res = HelperTools.JSONDeserialize<SubmitTransactionResponseViewModel>(rEnvelope.Payload);

            if (res.ReturnResult == "success" && !string.IsNullOrEmpty(res.ResultDescription))
            {
              Console.WriteLine($"{res.Txid}:{res.ReturnResult}");
            }
            else
            {
              Console.WriteLine($"{res.Txid}:{res.ReturnResult} => {res.ResultDescription}");
            }
          }
        }
      }
      else if (batchSize > 1)
      {
        string mapiUrl = config.MapiConfig.MapiUrl + "mapi/txs";

        Console.WriteLine($"Sending transactions in batches of {batchSize}.");
        int batch = 1;
        double totalBatches =  Math.Ceiling((double)requests.Count() / batchSize);

        while (requests.Any())
        {
          Console.WriteLine($"\nBatch {batch++}/{totalBatches}.");
          var req = requests.Take(batchSize);

          var requestString = HelperTools.JSONSerialize(req, false);
          var response = await client.PostAsync(mapiUrl, new StringContent(requestString, new UTF8Encoding(false), MediaTypeNames.Application.Json));

          var responseAsString = await response.Content.ReadAsStringAsync();

          if (!response.IsSuccessStatusCode)
          {
            Console.WriteLine($"Error while submitting transaction request {responseAsString}");
          }
          else
          {
            var rEnvelope = HelperTools.JSONDeserialize<SignedPayloadViewModel>(responseAsString);
            var r = HelperTools.JSONDeserialize<SubmitTransactionsResponseViewModel>(rEnvelope.Payload);
            
            foreach(var res in r.Txs)
            {
              if (res.ReturnResult == "success" && !string.IsNullOrEmpty(res.ResultDescription))
              {
                Console.WriteLine($"{res.Txid}:{res.ReturnResult}");
              }
              else
              {
                Console.WriteLine($"{res.Txid}:{res.ReturnResult} => {res.ResultDescription}");
              }
            }
          }
          requests = requests.Skip(batchSize);
        }
      }

      return 1;
    }

    private static async Task<(RpcClient, SmokeConfig)> InitRpcClient(string configFileName, IHttpClientFactory httpClientFactory)
    {
      var config = HelperTools.JSONDeserializeNewtonsoft<SmokeConfig>(await File.ReadAllTextAsync(configFileName));

      var validationResults = new List<ValidationResult>();
      var validationContext = new ValidationContext(config, serviceProvider: null, items: null);
      if (!Validator.TryValidateObject(config, validationContext, validationResults, true))
      {
        var allErrors = string.Join(Environment.NewLine, validationResults.Select(x => x.ErrorMessage).ToArray());
        Console.WriteLine($"Invalid configuration {configFileName}. Errors: {allErrors}");
        return (null, null);
      }

      RpcClient rpcClient = new(RpcClientFactory.CreateAddress(config.Node.Host, config.Node.Port),
      new System.Net.NetworkCredential(config.Node.Username, config.Node.Password), new NullLoggerFactory().CreateLogger<RpcClient>(),
      httpClientFactory.CreateClient(config.Node.Host));
      return (rpcClient, config);
    }

    private static async Task EnsureMapiIsConnectedToNodeAsync(SmokeConfig config)
    {
      await Functional.Utils.EnsureMapiIsConnectedToNodeAsync(
        config.MapiConfig.MapiUrl, config.MapiConfig.AdminAuthorization, true,
        config.Node.Host, config.Node.Port, config.Node.Username, config.Node.Password,
        "Smoke", config.Node.ZMQ
      );
    }

    private async static Task<List<Transaction>> CreateChain2Async(RpcClient rpcClient, Transaction tx, int numOfOutputs, int chainLength)
    {
      var chainList = new List<Transaction>();

      if (chainLength < 1)
      {
        return chainList;
      }

      var chainQueue = new Queue<(int Level, Transaction Tx)>();
      chainQueue.Enqueue((0, tx));

      while (chainQueue.Any())
      {
        var (level, parentTx) = chainQueue.Dequeue();

        if (level == chainLength)
        {
          continue;
        }

        level++;

        foreach (var output in parentTx.Outputs.ToArray())
        {
          var remainingSatoshis = output.Value.Satoshi;
          Key key2Sign = Key.Parse(await rpcClient.DumpPrivKeyAsync(output.ScriptPubKey.GetDestinationAddress(Network.RegTest).ToString()), Network.RegTest);
          var lastCoin = new Coin(parentTx, output);
          var chainTx = BCash.Instance.Regtest.CreateTransaction();
          chainTx.Inputs.Add(new TxIn(lastCoin.Outpoint));
          var outMoney = output.Value.Satoshi / (numOfOutputs + 1);

          for (int n = 0; n < numOfOutputs; n++)
          {

            var bsvAddress = BitcoinAddress.Create(await rpcClient.GetNewAddressAsync(), Network.RegTest);
            chainTx.Outputs.Add(chainTx.Outputs.CreateNewTxOut(outMoney, bsvAddress));
            remainingSatoshis -= outMoney;
          }
          remainingSatoshis -= chainTx.GetVirtualSize() * 2;
          chainTx.Outputs.Add(Money.Satoshis(remainingSatoshis), output.ScriptPubKey.GetDestinationAddress(Network.RegTest));

          chainTx.Sign(key2Sign.GetBitcoinSecret(Network.RegTest), lastCoin);

          chainQueue.Enqueue((level, chainTx));
          chainList.Add(chainTx);
        }
      }

      return chainList;
    }

    static void GenerateUTXOCsv(IEnumerable<RpcListUnspent> utxos)
    {
      string statsFile = "utxos.csv";
      using var stream = File.Open(statsFile, FileMode.Create);
      using var writer = new StreamWriter(stream);
      CsvConfiguration conf = new(CultureInfo.InvariantCulture)
      {
        Delimiter = ";",
        DetectColumnCountChanges = true
      };

      using var csv = new CsvWriter(writer, conf);
      csv.WriteHeader<UTXOCsv>();
      csv.NextRecord();
      csv.WriteRecords(utxos);
      csv.Flush();
    }

    static IEnumerable<RpcListUnspent> ReadUTXOCsv()
    {
      string statsFile = "utxos.csv";
      try
      {
        using var stream = File.Open(statsFile, FileMode.Open);
        using var writer = new StreamReader(stream);

        CsvConfiguration conf = new(CultureInfo.InvariantCulture)
        {
          Delimiter = ";",
          DetectColumnCountChanges = true
        };
        using var csvReader = new CsvReader(writer, conf);
        return csvReader.GetRecords<RpcListUnspent>().ToList();
      }
      catch (FileNotFoundException)
      {
        return Array.Empty<RpcListUnspent>();
      }
    }


    static async Task<int> Main(string[] args)
    {
      var builder = new HostBuilder()
               .ConfigureServices((hostContext, services) =>
               {
                 services.AddHttpClient();
               }).UseConsoleLifetime();
      var host = builder.Build();

      var findutxosCommand = new Command("findutxos")
      {
        new Argument<string>(
          name: "configFileName",
          description: "Json config file containing configuration"
        )
        {
          Arity = new ArgumentArity(1,1)
        }
      };

      findutxosCommand.Description = "Find transactions with unspent outputs.";
      findutxosCommand.Handler = CommandHandler.Create(async (string configFileName) =>
        await FindUTXOs(configFileName, (IHttpClientFactory)host.Services.GetService(typeof(IHttpClientFactory))));

      var generateblocksCommand = new Command("generate")
      {
        new Argument<string>(
          name: "n",
          description: "Generate n block(s)."
        )
        {
          Arity = new ArgumentArity(1,1)
        },
        new Argument<string>(
          name: "configFileName",
          description: "Json config file containing configuration"
        )
        {
          Arity = new ArgumentArity(1,1)
        }
      };

      generateblocksCommand.Description = "Generate new block(s).";
      generateblocksCommand.Handler = CommandHandler.Create(async (int n, string configFileName) =>
        await GenerateBlocks(n, configFileName, (IHttpClientFactory)host.Services.GetService(typeof(IHttpClientFactory))));

      var submittxsCommand = new Command("submittxs")
      {
        new Argument<string>(
          name: "uTxId",
          description: "Id of an unspent transaction."
        )
        {
          Arity = new ArgumentArity(1,1)
        },
        new Argument<string>(
          name: "numOfOutputs",
          description: "Number of outputs."
        )
        {
          Arity = new ArgumentArity(1,1)
        },
        new Argument<string>(
          name: "chainLength",
          description: "Length of a chain of transactions."
        )
        {
          Arity = new ArgumentArity(1,1)
        },
        new Argument<string>(
          name: "unconfirmedAncestor",
          description: "Test unconfirmed ancestor [0-1]."
        )
        {
          Arity = new ArgumentArity(1,1)
        },
        new Argument<string>(
          name: "batchSize",
          description: "Size of a batch of transactions sent to mapi/tx."
        )
        {
          Arity = new ArgumentArity(0,1)
        },
        new Argument<string>(
          name: "configFileName",
          description: "Json config file containing configuration."
        )
        {
          Arity = new ArgumentArity(1,1)
        }
      };

      submittxsCommand.Description = "Generate transactions and submit them to mapi.";
      submittxsCommand.Handler = CommandHandler.Create(async (string uTxId, int numOfOutputs, int chainLength, int unconfirmedAncestor, int batchSize, string configFileName) =>
        await SubmitTxs(uTxId, numOfOutputs, chainLength, unconfirmedAncestor != 0, batchSize,  configFileName, (IHttpClientFactory)host.Services.GetService(typeof(IHttpClientFactory))));

      var rootCommand = new RootCommand
      {
        findutxosCommand,
        generateblocksCommand,
        submittxsCommand
      };

      rootCommand.Description = "mAPI smoke test";

      return await rootCommand.InvokeAsync(args);
    }
  }
}
