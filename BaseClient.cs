using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LastFM.ReaderCore
{
    public class BaseClient
    {
        private readonly HttpClient _httpClient;
        protected ICacheService _cache;
        protected IErrorLogger _errorLogger;

        public BaseClient(ICacheService cache, IErrorLogger errorLogger, string baseUrl)
        {
            _cache = cache;
            _errorLogger = errorLogger;
            _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private void LogError(Uri baseUrl, HttpRequestMessage request, HttpResponseMessage response)
        {
            // Get the values of the parameters passed to the API
            string parameters = string.Join(", ", request.Headers.Select(x => x.Key + "=" + string.Join(",", x.Value)).ToArray());

            // Set up the information message with the URL, the status code, and the parameters.
            string info = "Request to " + baseUrl.AbsoluteUri + request.RequestUri + " failed with status code " + response.StatusCode + ", parameters: "
                + parameters + ", and content: " + response.Content.ReadAsStringAsync().Result;

            // Acquire the actual exception
            Exception ex = new Exception(info);

            // Log the exception and info message
            _errorLogger.LogError(ex, info);
        }

        private void TimeoutCheck(HttpRequestMessage request, HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                LogError(_httpClient.BaseAddress, request, response);
            }
        }

        public async Task<HttpResponseMessage> ExecuteAsync(HttpRequestMessage request)
        {
            var response = await _httpClient.SendAsync(request);
            TimeoutCheck(request, response);
            return response;
        }

        public async Task<T> GetAsync<T>(HttpRequestMessage request) where T : new()
        {
            var response = await ExecuteAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(content);
            }
            else
            {
                LogError(_httpClient.BaseAddress, request, response);
                return default(T);
            }
        }

        public async Task<T> GetFromCacheAsync<T>(HttpRequestMessage request, string cacheKey) where T : class, new()
        {
            var item = _cache.Get<T>(cacheKey);
            if (item == null) // If the cache doesn't have the item
            {
                var response = await ExecuteAsync(request); // Get the item from the API call
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    item = JsonConvert.DeserializeObject<T>(content);
                    _cache.Set(cacheKey, item); // Set that item into the cache so we can get it next time
                }
                else
                {
                    LogError(_httpClient.BaseAddress, request, response);
                    return default(T);
                }
            }

            return item;
        }
    }
}
