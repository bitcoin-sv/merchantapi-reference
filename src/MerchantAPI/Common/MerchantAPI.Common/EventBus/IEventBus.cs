// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.Common.EventBus
{
  public interface IEventBus
  {
    EventBusSubscription<T> Subscribe<T>() where T : IntegrationEvent;
    bool TryUnsubscribe<T>(EventBusSubscription<T> subscription) where T : IntegrationEvent;
    void Publish<T>(T @event);

    /// <summary>
    /// Returns when all queues are empty. Inefficient  - for unit testing  only
    /// </summary>
    /// <returns></returns>
    void WaitForIdle();
  }
}
