// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.Common.EventBus
{
  public class InMemoryEventBus : IEventBus
  {
    readonly Dictionary<Type, List<EventBusSubscription>> subscriptions = new Dictionary<Type, List<EventBusSubscription>>();

    private readonly Dictionary<EventBusSubscription, object> subscription2Channel =
      new Dictionary<EventBusSubscription, object>();

    ILogger<InMemoryEventBus> logger;
    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public EventBusSubscription<T> Subscribe<T>() where T : IntegrationEvent
    {

      lock (subscriptions)
      {

        if (!subscriptions.TryGetValue(typeof(T), out var list))
        {
          list = new List<EventBusSubscription>();
          subscriptions.Add(typeof(T), list);
        }

        var channel = Channel.CreateUnbounded<T>();
        var subscription = new EventBusSubscription<T>(channel);
        subscription2Channel.Add(subscription, channel);

        list.Add(subscription);
        logger.LogInformation($"Added subscription to {typeof(T).Name}");
        return subscription;
      }

    }

    public bool ProcessingIsIdle() // only for unit tests
    {
      bool IsEventInProcessing()
      {
        lock (subscriptions)
        {
          foreach (var kv in subscriptions)
          {
            foreach (var subscription in kv.Value)
            {
              if (subscription.ProcessingEvent)
              {
                
                return true;
              }
            }
          }
        }
        return false;
      }


      for (int i = 0; i < 100; i++)
      {
        if (IsEventInProcessing())
        {
          return false;
        }

        // if we are not processing events we might still be in Read or somebody is just about to write into the queue 
        // That's why we wait up to a second (100*10) to make sure, that everything is idle
        Thread.Sleep(10);
      }

      return true;

    }
    public void WaitForIdle()
    {
      while (!ProcessingIsIdle())
      {

      }

    }

    public bool TryUnsubscribe<T>(EventBusSubscription<T> subscription) where T : IntegrationEvent
    {
      if (subscription == null)
      {
        return false;
      }
      lock (subscriptions)
      {
        if (!subscriptions.TryGetValue(typeof(T), out var list))
        {
          return false; 
        }

        var result = list.Remove(subscription);
        subscription2Channel.Remove(subscription);
        if (result)
        {
          logger.LogInformation($"Removed subscription to {typeof(T).Name}");
        }
        return result;
      }

    }

    public void Publish<T>(T @event)
    {
      lock (subscriptions)
      {
        if (subscriptions.TryGetValue(@event.GetType(), out var list))
        {
          foreach (var s in list)
          {
            if (!((Channel<T>)subscription2Channel[s]).Writer.TryWrite(@event)) 
            {
              // Should not happen, since we are using unbounded channels
              logger.LogError($"Unexpected error - can not write to EventBusChannel");
            }
          }
        }
      }
    }
  }

}
