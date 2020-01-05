// Borrowed code from https://exceptionnotfound.net/building-the-ultimate-restsharp-client-in-asp-net-and-csharp/

using System;
using Microsoft.Extensions.Caching.Memory;

namespace LastFM.ReaderCore
{

    public interface ICacheService
    {
        T Get<T>(string cacheKey) where T : class;
        void Set(string cacheKey, object item);
    }

    public class InMemoryCache : ICacheService
    {
        private MemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
        public T Get<T>(string cacheKey) where T : class
        {
            return _memoryCache.Get(cacheKey) as T;
        }
        public void Set(string cacheKey, object item)
        {
            if (item != null)
            {
                _memoryCache.Set(cacheKey, item);
            }
        }
    }
}