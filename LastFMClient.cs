using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LastFM.ReaderCore
{
    public class LastFMClient : BaseClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        public string apiKey { get; set; }

        public LastFMClient(ICacheService cache, IErrorLogger errorLogger)
            : base(cache, errorLogger, "http://ws.audioscrobbler.com/2.0/")
        {
            _httpClient = new HttpClient();
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
            
            string response = null;
            try
            {
                var url = $"http://ws.audioscrobbler.com/2.0/?method=artist.getcorrection&api_key={apiKey}&format=json&artist={artist}";
                response = await _httpClient.GetStringAsync(url);
                var result = JsonConvert.DeserializeObject<LastFMArtistCorrection>(response);

                _cache.Set(cacheKey, result);
                return result;
            }
            catch (JsonSerializationException ex)
            {
                Console.WriteLine("Error deserializing JSON response: " + ex.Message);
                Console.WriteLine("JSON Response: " + response);
                throw;
            }
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

            string response = null;
            try
            {
                var url = $"http://ws.audioscrobbler.com/2.0/?method=artist.gettoptags&api_key={apiKey}&format=json&artist={artist}";
                response = await _httpClient.GetStringAsync(url);
                var result = JsonConvert.DeserializeObject<LastFMArtistTag>(response);

                _cache.Set(cacheKey, result);
                return result;
            }
            catch (JsonSerializationException ex)
            {
                Console.WriteLine("Error deserializing JSON response: " + ex.Message);
                Console.WriteLine("JSON Response: " + response);
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
