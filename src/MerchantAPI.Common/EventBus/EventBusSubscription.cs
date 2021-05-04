// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.Common.EventBus
{

  public class EventBusSubscription
  {

    protected long processingEvent;
    public bool ProcessingEvent => Interlocked.Read(ref processingEvent) > 0;

  }

  public class EventBusSubscription<T> : EventBusSubscription
    where T : IntegrationEvent
  {
    ChannelReader<T> reader;

    public EventBusSubscription(ChannelReader<T> reader)
    {
      this.reader = reader;
    }
    public  Task<T> ReadAsync(CancellationToken cancellationToken)
    {
      return reader.ReadAsync(cancellationToken).AsTask(); // Convert from Value task to ordinary tasks to that has less restrictions
    }

    public async Task ProcessEventsAsync<L>(CancellationToken cancellationToken, ILogger<L> logger, Func<T, Task> process)
    {
      do
      {
        try
        {
          var result = await ReadAsync(cancellationToken); // This can throw cancellation exception
          
          Interlocked.Increment(ref processingEvent);
          try
          {
            await process(result);
          }
          finally
          {
            Interlocked.Decrement(ref processingEvent);
          }
        }
        catch (OperationCanceledException)
        {
          try
          {
            logger?.LogInformation("Processing of event queue stopped due to cancellation");
          }
          catch (Exception)
          {
            // this can throw since logger might already be dispocsed during shut down
          }

          break;
        }
        catch (Exception e)
        {
          logger?.LogError($"Error while processing an event from event queue. Will ignore error and continue with next event. Error {e}");
        }
      } while (true);
    }
  }
}
