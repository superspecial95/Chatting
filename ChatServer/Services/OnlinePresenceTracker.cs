using System.Collections.Concurrent;

namespace ChatServer.Services;

/// <summary>Prevents one disconnected window from marking a still-connected user offline.</summary>
public sealed class OnlinePresenceTracker
{
    private readonly ConcurrentDictionary<int, int> _connections = new();

    public bool AddConnection(int userId)
    {
        return _connections.AddOrUpdate(userId, 1, (_, count) => count + 1) == 1;
    }

    public bool RemoveConnection(int userId)
    {
        while (_connections.TryGetValue(userId, out int count))
        {
            if (count > 1 && _connections.TryUpdate(userId, count - 1, count))
            {
                return false;
            }

            if (count == 1 && _connections.TryRemove(userId, out _))
            {
                return true;
            }
        }

        return false;
    }
}
