// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Rest.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;

namespace MerchantAPI.APIGateway.Test.Functional
{
  public class ZMQTestBase : MapiWithBitcoindTestBase
  {
    protected ZMQSubscriptionService zmqService;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      zmqService = server.Services.GetRequiredService<ZMQSubscriptionService>();
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
      InsertFeeQuote();

      // Wait until all events are processed to avoid race conditions - we need to  finish subscribing to ZMQ before checking for any received notifications
      WaitUntilEventBusIsIdle();
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }
    protected async Task RegisterNodesWithServiceAndWait(CancellationToken cancellationToken)
    {
      var subscribedToZMQSubscription = EventBus.Subscribe<ZMQSubscribedEvent>();

      // Register nodes with service
      RegisterNodesWithService(cancellationToken);

      // Wait for subscription event so we can make sure that service is listening to node
      _ = await subscribedToZMQSubscription.ReadAsync(cancellationToken);

      // Unsubscribe from event bus
      EventBus.TryUnsubscribe(subscribedToZMQSubscription);
    }

    private void RegisterNodesWithService(CancellationToken cancellationToken)
    {
      // Register all nodes with service
      var nodes = this.NodeRepository.GetNodes();
      foreach (var node in nodes)
      {
        EventBus.Publish(new NodeAddedEvent() { CreationDate = DateTime.UtcNow, CreatedNode = node });
      }
    }
  }
}
