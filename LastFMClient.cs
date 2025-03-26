using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using LastFM.ReaderCore.Logging;

namespace LastFM.ReaderCore
{
    public class LastFMClient : BaseClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly RateLimiter _rateLimiter;
        private readonly RetryPolicy _retryPolicy;
        private readonly ICacheService _cacheService;
        private readonly IErrorLogger _errorLogger;
        private readonly Dictionary<string, string> _genreCache;
        private readonly SemaphoreSlim _genreCacheLock;
        public string apiKey { get; set; }

        public LastFMClient(ICacheService cacheService, IErrorLogger errorLogger)
            : base(cacheService, errorLogger, "https://ws.audioscrobbler.com/2.0/")
        {
            _httpClient = new HttpClient();
            _rateLimiter = new RateLimiter(3); // 3 requests per second as per LastFM limits
            _retryPolicy = new RetryPolicy(errorLogger: errorLogger);
            _cacheService = cacheService;
            _errorLogger = errorLogger;
            _genreCache = new Dictionary<string, string>();
            _genreCacheLock = new SemaphoreSlim(1, 1);
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

        public async Task<string> GetArtistGenreAsync(string artistName)
        {
            try
            {
                // Check memory cache first
                await _genreCacheLock.WaitAsync();
                try
                {
                    if (_genreCache.TryGetValue(artistName, out string cachedGenre))
                    {
                        return cachedGenre;
                    }
                }
                finally
                {
                    _genreCacheLock.Release();
                }

                // If not in cache, fetch from API
                var tag = await ArtistTagAsync(artistName);
                var genre = (tag.Toptags.Tag.Length > 0) ? tag.Toptags.Tag[0].Name : "";

                // Store in memory cache
                await _genreCacheLock.WaitAsync();
                try
                {
                    _genreCache[artistName] = genre;
                }
                finally
                {
                    _genreCacheLock.Release();
                }

                return genre;
            }
            catch (Exception ex)
            {
                _errorLogger?.LogError(ex, $"Error getting genre for artist: {artistName}");
                return "";
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _rateLimiter.Dispose();
        }
    }
}
