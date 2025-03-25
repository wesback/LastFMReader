using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;

namespace LastFM.ReaderCore
{
    public class CacheItem<T>
    {
        public T Value { get; set; }
        public DateTime ExpirationTime { get; set; }
        public long Size { get; set; }
    }

    public class InMemoryCacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, CacheItem<object>> _cache = new ConcurrentDictionary<string, CacheItem<object>>();
        private readonly long _maxCacheSize;
        private long _currentCacheSize;
        private readonly TimeSpan _defaultExpiration;
        private readonly object _sizeLock = new object();

        public InMemoryCacheService(long maxCacheSizeMB = 100, TimeSpan? defaultExpiration = null)
        {
            _maxCacheSize = maxCacheSizeMB * 1024 * 1024; // Convert MB to bytes
            _defaultExpiration = defaultExpiration ?? TimeSpan.FromHours(1);
        }

        public void Set(string key, object value)
        {
            Set(key, value, _defaultExpiration);
        }

        public void Set(string key, object value, TimeSpan? expiration = null)
        {
            var expirationTime = DateTime.UtcNow.Add(expiration ?? _defaultExpiration);
            var size = GetObjectSize(value);
            
            var cacheItem = new CacheItem<object>
            {
                Value = value,
                ExpirationTime = expirationTime,
                Size = size
            };

            _cache.AddOrUpdate(key, cacheItem, (_, __) => cacheItem);
            
            lock (_sizeLock)
            {
                _currentCacheSize += size;
                CleanupIfNeeded();
            }
        }

        public object Get(string key)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                if (DateTime.UtcNow > item.ExpirationTime)
                {
                    Remove(key);
                    return null;
                }
                return item.Value;
            }
            return null;
        }

        public T Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                if (DateTime.UtcNow > item.ExpirationTime)
                {
                    Remove(key);
                    return default;
                }
                if (item.Value is T typedValue)
                {
                    return typedValue;
                }
            }
            return default;
        }

        public bool Contains(string key)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                if (DateTime.UtcNow > item.ExpirationTime)
                {
                    Remove(key);
                    return false;
                }
                return true;
            }
            return false;
        }

        private void Remove(string key)
        {
            if (_cache.TryRemove(key, out var item))
            {
                lock (_sizeLock)
                {
                    _currentCacheSize -= item.Size;
                }
            }
        }

        private void CleanupIfNeeded()
        {
            if (_currentCacheSize > _maxCacheSize)
            {
                var expiredKeys = _cache.Where(kvp => DateTime.UtcNow > kvp.Value.ExpirationTime)
                                      .Select(kvp => kvp.Key)
                                      .ToList();

                foreach (var key in expiredKeys)
                {
                    Remove(key);
                }

                if (_currentCacheSize > _maxCacheSize)
                {
                    // If still too large, remove oldest items
                    var oldestKeys = _cache.OrderBy(kvp => kvp.Value.ExpirationTime)
                                         .Select(kvp => kvp.Key)
                                         .Take(10)
                                         .ToList();

                    foreach (var key in oldestKeys)
                    {
                        Remove(key);
                    }
                }
            }
        }

        private long GetObjectSize(object obj)
        {
            // Simple size estimation - can be improved based on actual object types
            if (obj == null) return 0;
            if (obj is string str) return str.Length * 2; // UTF-16
            if (obj is Array arr) return arr.Length * 8; // Rough estimate
            return 100; // Default size for other objects
        }
    }
}