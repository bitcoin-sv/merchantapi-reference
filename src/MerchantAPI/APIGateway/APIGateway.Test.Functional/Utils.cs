// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.Common.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  public class Utils
  {
    public static void WaitUntil(Func<bool> predicate)
    {
      for (int i = 0; i < 100; i++)
      {
        if (predicate())
        {
          return;
        }

        Thread.Sleep(100);  // see also BackgroundJobsMock.WaitForPropagation()
      }

      throw new Exception("Timeout - WaitUntil did not complete in allocated time");
    }

    public static async Task EnsureMapiIsConnectedToNodeAsync(string mapiUrl, string authAdmin, bool rearrangeNodes,
      string nodeHost, int nodePort, string nodeUser, string nodePassword, string remarkTest, string nodeZMQNotificationsEndpoint)
    {
      var adminClient = new HttpClient();
      adminClient.DefaultRequestHeaders.Add("Api-Key", authAdmin);
      string nodeUrl = mapiUrl + "api/v1/Node";
      var uri = new Uri(nodeUrl);
      Console.WriteLine($"Checking MapiUrl: (GET) node ...");

      var hostPort = $"{nodeHost}:{nodePort}";
      var nodesResult = await adminClient.GetAsync(nodeUrl);
      if (!nodesResult.IsSuccessStatusCode)
      {
        throw new Exception(
          $"Unable to retrieve existing node {hostPort}. Error: {nodesResult.StatusCode} {await nodesResult.Content.ReadAsStringAsync()}");
      }

      Console.WriteLine($"Bitcoind hostPort: { hostPort}. Checking node...");
      var nodes =
        HelperTools.JSONDeserialize<NodeViewModelGet[]>(await nodesResult.Content.ReadAsStringAsync());
      if (rearrangeNodes)
      {
        if (nodes.Any(x => string.Compare(x.Id, hostPort, StringComparison.InvariantCultureIgnoreCase) == 0))
        {

          Console.WriteLine($"Removing existing node {hostPort} from mAPI");

          var deleteResult = await adminClient.DeleteAsync(uri + "/" + hostPort);
          if (!deleteResult.IsSuccessStatusCode)
          {
            throw new Exception(
              $"Unable to delete existing node {hostPort}. Error: {deleteResult.StatusCode} {await deleteResult.Content.ReadAsStringAsync()}");
          }
          // delete always returns noContent, so we have to check with GET
          var getResult = await adminClient.GetAsync(uri + "/" + hostPort);
          if (getResult.IsSuccessStatusCode)
          {
            throw new Exception($"mAPI cache is out of sync, you have to restart mAPI!");
          }
        }

        Console.WriteLine($"Adding new node {hostPort} to mAPI");

        var newNode = new NodeViewModelCreate
        {
          Id = hostPort,
          Username = nodeUser,
          Password = nodePassword,
          Remarks = $"Node created by mAPI {remarkTest} Test at {DateTime.Now}",
          ZMQNotificationsEndpoint = nodeZMQNotificationsEndpoint
        };
        Console.WriteLine($"ZMQNotificationsEndpoint: { newNode.ZMQNotificationsEndpoint }");

        var newNodeContent = new StringContent(HelperTools.JSONSerialize(newNode, true),
          new UTF8Encoding(false), MediaTypeNames.Application.Json);

        var newNodeResult = await adminClient.PostAsync(uri, newNodeContent);

        if (!newNodeResult.IsSuccessStatusCode)
        {
          throw new Exception(
            $"Unable to create new {hostPort}. Error: {newNodeResult.StatusCode} {await newNodeResult.Content.ReadAsStringAsync()}");
        }
      }
      else
      {
        if (!nodes.Any(x => string.Compare(x.Id, hostPort, StringComparison.InvariantCultureIgnoreCase) == 0))
        {
          throw new Exception(
            $"Please add missing node: {hostPort} {nodeUser} {nodePassword} with ZMQNotificationsEndpoint: '{nodeZMQNotificationsEndpoint}'!'");
        }
      }

      await Task.Delay(TimeSpan.FromSeconds(1)); // Give mAPI some time to establish ZMQ subscriptions
    }
  }
}
