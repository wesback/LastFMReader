using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;

namespace LastFM.ReaderCore
{
    public class RetryPolicy
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;
        private readonly double _backoffMultiplier;
        private readonly IErrorLogger _errorLogger;

        public RetryPolicy(int maxRetries = 3, TimeSpan? initialDelay = null, double backoffMultiplier = 2.0, IErrorLogger errorLogger = null)
        {
            _maxRetries = maxRetries;
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            _backoffMultiplier = backoffMultiplier;
            _errorLogger = errorLogger;
        }

        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, string operationName)
        {
            var currentRetry = 0;
            var currentDelay = _initialDelay;

            while (true)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (ShouldRetry(ex))
                {
                    currentRetry++;
                    if (currentRetry > _maxRetries)
                    {
                        _errorLogger?.LogError(ex, $"Max retries ({_maxRetries}) exceeded for operation: {operationName}");
                        throw;
                    }

                    _errorLogger?.LogError(ex, $"Retry {currentRetry} of {_maxRetries} for operation: {operationName}. Waiting {currentDelay.TotalSeconds} seconds...");
                    await Task.Delay(currentDelay);
                    currentDelay = TimeSpan.FromTicks((long)(currentDelay.Ticks * _backoffMultiplier));
                }
            }
        }

        private bool ShouldRetry(Exception ex)
        {
            return ex switch
            {
                HttpRequestException => true,
                StorageException storageEx => storageEx.RequestInformation?.HttpStatusCode == 429 || // Too Many Requests
                                            storageEx.RequestInformation?.HttpStatusCode == 503 || // Service Unavailable
                                            storageEx.RequestInformation?.HttpStatusCode == 500,   // Internal Server Error
                _ => false
            };
        }
    }
} 