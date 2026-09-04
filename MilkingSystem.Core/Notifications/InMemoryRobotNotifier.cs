using System;
using System.Collections.Concurrent;
namespace MilkingSystem.Core.Notifications;

/// <summary>
/// In-memory implementation of IRobotNotifier.
/// 
/// TODO: This implementation is incomplete. Candidates should:
/// 1. Implement the Subscribe method to allow robots to receive notifications
/// 2. Implement NotifyMilkingCompleted to broadcast to all subscribers
/// 3. Implement WasRecentlyMilked to check if an animal was milked within the protection window
/// 4. Ensure thread-safety for concurrent access
/// </summary>
public class InMemoryRobotNotifier : IRobotNotifier
{
    // TODO: Add necessary fields for tracking subscriptions and recent milkings
    private readonly ConcurrentDictionary<Guid, Action<MilkingNotification>> _subscribers = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastMilkedTimestamps = new();
    private static readonly TimeSpan ProtectionWindow = TimeSpan.FromHours(6);
    
    public void NotifyMilkingCompleted(MilkingNotification notification)
    {
        _lastMilkedTimestamps.AddOrUpdate(
            notification.AnimalId,
            notification.Timestamp,
            (_, existing) => notification.Timestamp > existing ? notification.Timestamp : existing
        );

        foreach(var subscriber in _subscribers.Values)
        {
            try
            {
                subscriber?.Invoke(notification);
            }
            catch
            {
                //Swallowing exception to guarantee resilience across subscriber tasks
            }
        }
    }
        public bool WasRecentlyMilked(int animalId, int windowHours = 6)
        {
        //TimeSpan protectionWindow = TimeSpan.FromHours(windowHours);
        if (_lastMilkedTimestamps.TryGetValue(animalId, out var lastMilked))
        {
            return (DateTime.UtcNow - lastMilked) < TimeSpan.FromHours(windowHours);
        }
        return false;
    }
    // Helper class implementing IDisposable to handle unsubscription clean-up
        private class Unsubscriber : IDisposable
        {
            private readonly Action _unsubscribeAction;
            private bool _disposed;

            public Unsubscriber(Action unsubscribeAction)
            {
                _unsubscribeAction = unsubscribeAction;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _unsubscribeAction();
                    _disposed = true;
                }
            }
        }
    //}

    public IDisposable Subscribe(Action<MilkingNotification> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }
        var subscriptionId = Guid.NewGuid();
        _subscribers[subscriptionId] = handler;
        return new Unsubscriber(() => _subscribers.TryRemove(subscriptionId, out _));
    }

   
}
