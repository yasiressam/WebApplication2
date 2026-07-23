using System.Collections.Concurrent;

namespace WebApplication2.Services
{
    public class OnlineUsersTracker
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _connectionsByUserId = new();

        public void AddConnection(string userId, string connectionId)
        {
            var connections = _connectionsByUserId.GetOrAdd(userId, static _ => new ConcurrentDictionary<string, byte>());
            connections[connectionId] = 0;
        }

        public void RemoveConnection(string userId, string connectionId)
        {
            if (!_connectionsByUserId.TryGetValue(userId, out var connections))
            {
                return;
            }

            connections.TryRemove(connectionId, out _);

            if (connections.IsEmpty)
            {
                _connectionsByUserId.TryRemove(userId, out _);
            }
        }

        public int OnlineUsersCount => _connectionsByUserId.Count;

        public IReadOnlyCollection<string> GetOnlineUserIds()
        {
            return _connectionsByUserId.Keys.ToList();
        }
    }
}
