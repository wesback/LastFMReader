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
            var url = $"http://ws.audioscrobbler.com/2.0/?method=artist.getcorrection&api_key={apiKey}&format=json&artist={artist}";

            var response = await _httpClient.GetStringAsync(url);
            return JsonConvert.DeserializeObject<LastFMArtistCorrection>(response);
        }

        public async Task<LastFMArtistTag> ArtistTagAsync(string artist)
        {
            var prefix = "tag";
            var url = $"http://ws.audioscrobbler.com/2.0/?method=artist.gettoptags&api_key={apiKey}&format=json&artist={artist}";

            var response = await _httpClient.GetStringAsync(url);
            return JsonConvert.DeserializeObject<LastFMArtistTag>(response);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
