using System.Collections.Concurrent;

namespace LastFM.ReaderCore
{
    public class InMemoryCacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();

        public void Set(string key, object value)
        {
            _cache[key] = value;
        }

        public object Get(string key)
        {
            _cache.TryGetValue(key, out var value);
            return value;
        }

        public T Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        public bool Contains(string key)
        {
            return _cache.ContainsKey(key);
        }
    }
}