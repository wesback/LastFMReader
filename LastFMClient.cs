using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LastFM.ReaderCore
{
    public class LastFMClient : BaseClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly RateLimiter _rateLimiter;
        private readonly RetryPolicy _retryPolicy;
        public string apiKey { get; set; }

        public LastFMClient(ICacheService cache, IErrorLogger errorLogger)
            : base(cache, errorLogger, "https://ws.audioscrobbler.com/2.0/")
        {
            _httpClient = new HttpClient();
            _rateLimiter = new RateLimiter(3); // 3 requests per second as per LastFM limits
            _retryPolicy = new RetryPolicy(errorLogger: errorLogger);
        }

        public async Task<LastFMArtistCorrection> ArtistCorrectionAsync(string artist)
        {
            var prefix = "corr";
            var cacheKey = $"{prefix}_{artist}";
            var cachedData = _cache.Get<LastFMArtistCorrection>(cacheKey);

            if (cachedData != null)
            {
                return cachedData;
            }
            
            await _rateLimiter.WaitForTokenAsync();
            
            return await _retryPolicy.ExecuteWithRetryAsync(
                async () =>
                {
                    var url = $"https://ws.audioscrobbler.com/2.0/?method=artist.getcorrection&api_key={apiKey}&format=json&artist={Uri.EscapeDataString(artist)}";
                    var response = await _httpClient.GetStringAsync(url);
                    var result = JsonConvert.DeserializeObject<LastFMArtistCorrection>(response);
                    _cache.Set(cacheKey, result);
                    return result;
                },
                $"Artist correction for {artist}"
            );
        }

        public async Task<LastFMArtistTag> ArtistTagAsync(string artist)
        {
            var prefix = "tag";
            var cacheKey = $"{prefix}_{artist}";
            var cachedData = _cache.Get<LastFMArtistTag>(cacheKey);

            if (cachedData != null)
            {
                return cachedData;
            }

            await _rateLimiter.WaitForTokenAsync();

            return await _retryPolicy.ExecuteWithRetryAsync(
                async () =>
                {
                    var url = $"https://ws.audioscrobbler.com/2.0/?method=artist.gettoptags&api_key={apiKey}&format=json&artist={Uri.EscapeDataString(artist)}";
                    var response = await _httpClient.GetStringAsync(url);
                    var result = JsonConvert.DeserializeObject<LastFMArtistTag>(response);
                    _cache.Set(cacheKey, result);
                    return result;
                },
                $"Artist tags for {artist}"
            );
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _rateLimiter.Dispose();
        }
    }
}
