using System;
using System.Threading;
using System.Threading.Tasks;

namespace LastFM.ReaderCore
{
    public class RateLimiter
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxRequestsPerSecond;
        private readonly TimeSpan _interval;
        private readonly object _lock = new object();
        private DateTime _lastRequestTime;

        public RateLimiter(int maxRequestsPerSecond = 5)
        {
            _maxRequestsPerSecond = maxRequestsPerSecond;
            _interval = TimeSpan.FromSeconds(1.0 / maxRequestsPerSecond);
            _semaphore = new SemaphoreSlim(1, 1);
            _lastRequestTime = DateTime.MinValue;
        }

        public async Task WaitForTokenAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - _lastRequestTime;

                if (timeSinceLastRequest < _interval)
                {
                    var delay = _interval - timeSinceLastRequest;
                    await Task.Delay(delay);
                }

                _lastRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
} 