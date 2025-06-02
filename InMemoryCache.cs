using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

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

            var oldSize = 0L;
            _cache.AddOrUpdate(key, cacheItem, (_, oldItem) => 
            {
                oldSize = oldItem.Size;
                return cacheItem;
            });
            
            lock (_sizeLock)
            {
                _currentCacheSize += size - oldSize;
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
                // First pass: remove expired items
                var expiredKeys = _cache.Where(kvp => DateTime.UtcNow > kvp.Value.ExpirationTime)
                                      .Select(kvp => kvp.Key)
                                      .ToList();

                foreach (var key in expiredKeys)
                {
                    Remove(key);
                }

                // Second pass: if still over limit, remove items more aggressively
                if (_currentCacheSize > _maxCacheSize)
                {
                    var excessSize = _currentCacheSize - _maxCacheSize;
                    var targetRemovalCount = Math.Max(10, (int)(excessSize / 1000)); // Remove more items based on excess
                    
                    // Remove oldest items (closest to expiration)
                    var oldestKeys = _cache.OrderBy(kvp => kvp.Value.ExpirationTime)
                                         .Select(kvp => kvp.Key)
                                         .Take(targetRemovalCount)
                                         .ToList();

                    foreach (var key in oldestKeys)
                    {
                        Remove(key);
                        
                        // Stop early if we've freed enough space
                        if (_currentCacheSize <= _maxCacheSize * 0.8) // Target 80% of max
                            break;
                    }
                }
            }
        }

        private long GetObjectSize(object obj)
        {
            if (obj == null) return 0;
            
            // More accurate size estimation
            if (obj is string str) return str.Length * 2 + 24; // UTF-16 + object overhead
            if (obj is Array arr) return arr.Length * 8 + 24; // Rough estimate + overhead
            if (obj is ICollection<object> collection) return collection.Count * 8 + 24;
            
            // For complex objects, use a better heuristic based on JSON serialization size
            if (obj.GetType().IsClass && !obj.GetType().IsPrimitive)
            {
                try
                {
                    // Estimate based on JSON size (rough approximation of object complexity)
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
                    return json.Length * 4 + 100; // JSON size * 4 for object overhead + base overhead
                }
                catch
                {
                    // Fallback to larger default for complex objects
                    return 500;
                }
            }
            
            return 50; // Default size for primitives and simple objects
        }
    }
}