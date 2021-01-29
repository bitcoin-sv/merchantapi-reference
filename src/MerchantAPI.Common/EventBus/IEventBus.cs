// Copyright (c) 2020 Bitcoin Association

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
