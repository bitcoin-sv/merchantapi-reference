// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Stress
{
  internal class Utils
  {
    public static async Task<BitcoindProcess> StartBitcoindWithTemplateDataAsync(string templateData, string bitcoindPath, string zmqEndpointIp, IHttpClientFactory httpClientFactory)
    {
      var testDataDir = CopyTemplateData(templateData);


      Console.WriteLine("Starting up bitcoind");

      if (string.IsNullOrEmpty(bitcoindPath))
      {
        bitcoindPath = Path.Combine(Directory.GetCurrentDirectory(), "bitcoind");
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
          bitcoindPath += ".exe";
        }
      }

      if (!File.Exists(bitcoindPath))
      {
        throw new Exception($"Can not start bitcoind. Expected bitcoind at location: {bitcoindPath}");
      }

      List<string> argumentList = new() { "-checklevel=0", "-checkblocks=0" }; // "Verifying blocks..." can take too long
      var bitcoind = new BitcoindProcess("127.0.0.1", bitcoindPath, testDataDir, 18444, 18332,
        string.IsNullOrEmpty(zmqEndpointIp) ? "127.0.0.1" : zmqEndpointIp, 28333,
        new NullLoggerFactory(), httpClientFactory, emptyDataDir: false, argumentList: argumentList);
      Console.WriteLine($"Bitcoind arguments:");

      Console.WriteLine(String.Join(" ", bitcoind.ArgumentList));

      long blocks = (await bitcoind.RpcClient.GetBlockchainInfoAsync()).Blocks;

      if (blocks == 0)
      {
        Console.WriteLine($"Warning: current active chain has 0 blocks. The submission of all transactions will probably fail. Check the content of template data directory - {templateData}");
      }

      return bitcoind;
    }

    private static string CopyTemplateData(string templateData)
    {
      if (!Directory.Exists(Path.Combine(templateData, "blocks")))
      {
        throw new Exception("Invalid templatePath - Directory pointed to by templatePath should contain 'blocks' sub-directory.");
      }

      string testDataDir = Path.Combine(Directory.GetCurrentDirectory(), "nodes", DateTime.Now.ToString("s").Replace(":", "-"));

      string destDir = Path.Combine(testDataDir, "regtest");
      Console.WriteLine($"Copying template data from directory {templateData} into temporary directory {destDir}");

      DirectoryCopy(templateData, destDir);
      return testDataDir;
    }

    private static void DirectoryCopy(string sourceDirName, string destDirName)
    {
      // Get the subdirectories for the specified directory.
      DirectoryInfo dir = new(sourceDirName);

      if (!dir.Exists)
      {
        throw new DirectoryNotFoundException(
          "Source directory does not exist or could not be found: "
          + sourceDirName);
      }


      var dirs = new Queue<(DirectoryInfo, string)>();
      dirs.Enqueue((dir, destDirName));

      while (dirs.TryDequeue(out var current))
      {
        var (currentSrcDir, currentDestDir) = current;

        Directory.CreateDirectory(currentDestDir);

        foreach (FileInfo file in currentSrcDir.GetFiles())
        {
          file.CopyTo(Path.Combine(currentDestDir, file.Name), false);
        }

        // Enqueue subdirectories for later
        foreach (var x in currentSrcDir.GetDirectories())
        {
          dirs.Enqueue((x, Path.Combine(currentDestDir, x.Name)));
        }
      }
    }

    public static async Task EnsureMapiIsConnectedToNodeAsync(string mapiUrl, string authAdmin, bool rearrangeNodes, BitcoindProcess bitcoind, string nodeHost, string nodeZMQNotificationsEndpoint)
    {
      string host;
      int port;
      if (!string.IsNullOrEmpty(nodeHost))
      {
        if (nodeHost.Contains(":"))
        {
          var hostPortArray = nodeHost.Split(':');
          host = hostPortArray[0];
          port = int.Parse(hostPortArray[1]);
        }
        else
        {
          host = nodeHost;
          port = bitcoind.RpcPort;
        }
      }
      else
      {
        host = bitcoind.Host;
        port = bitcoind.RpcPort;
      }

      await Functional.Utils.EnsureMapiIsConnectedToNodeAsync(
        mapiUrl, authAdmin, rearrangeNodes, 
        host, port, bitcoind.RpcUser, bitcoind.RpcPassword, "Stress",
        (!string.IsNullOrEmpty(nodeZMQNotificationsEndpoint) ? nodeZMQNotificationsEndpoint : $"tcp://{bitcoind.ZmqIp}:{bitcoind.ZmqPort}")
        );
    }



    public static async Task CheckFeeQuotesAsync(string jsonFile, string mapiUrl, string authAdmin)
    {
      if (string.IsNullOrEmpty(jsonFile))
      {
        return;
      }
      string jsonData = File.ReadAllText(jsonFile);
      // check json
      List<FeeQuote> feeQuotes = JsonConvert.DeserializeObject<List<FeeQuote>>(jsonData);

      var adminClient = new HttpClient();
      adminClient.DefaultRequestHeaders.Add("Api-Key", authAdmin);
      mapiUrl += "api/v1/FeeQuote";

      var uri = new Uri(mapiUrl);
      foreach (var feeQuote in feeQuotes)
      {
        var postFeeQuote = new FeeQuoteViewModelCreate(feeQuote);
        if (postFeeQuote.ValidFrom == DateTime.MinValue)
        {
          postFeeQuote.ValidFrom = null;
        }
        if (postFeeQuote.CreatedAt == DateTime.MinValue)
        {
          postFeeQuote.CreatedAt = DateTime.UtcNow;
        }
        var newFeeQuoteContent = new StringContent(HelperTools.JSONSerialize(postFeeQuote, true),
          new UTF8Encoding(false), MediaTypeNames.Application.Json);
        var newFeeQuoteResult = await adminClient.PostAsync(uri, newFeeQuoteContent);


        if (newFeeQuoteResult.IsSuccessStatusCode)
        {
          Console.WriteLine($"FeeQuote with identity '{ postFeeQuote.Identity ?? "" } { postFeeQuote.IdentityProvider ?? "" }' successfully added.");
        }
        else
        {
          throw new Exception(
  $"Unable to create new {feeQuote}. Error: {newFeeQuoteResult.StatusCode} { await newFeeQuoteResult.Content.ReadAsStringAsync() }");
        }
      }
    }
  }
}
