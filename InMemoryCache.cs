using System.Collections.Generic;

namespace LastFM.ReaderCore
{
    public class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

        public T Get<T>(string key) where T : class
        {
            if (_cache.TryGetValue(key, out var value))
            {
                return (T)value;
            }
            return default;
        }
        public void Set(string key, object value)
        {
            _cache[key] = value;
        }
    }
}