using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Auth;
using Newtonsoft.Json;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace LastFM.ReaderCore
{
    public class BatchProcessor : IDisposable
    {
        private readonly CloudBlobContainer _container;
        private readonly string _username;
        private readonly IErrorLogger _errorLogger;
        private readonly RetryPolicy _retryPolicy;
        private readonly List<Track> _allTracks;

        public BatchProcessor(string storageAccount, string storageKey, string username, IErrorLogger errorLogger = null)
        {
            var blobCreds = new StorageCredentials(storageAccount, storageKey);
            var storageUri = new Uri($"https://{storageAccount}.blob.core.windows.net/");
            var blobClient = new CloudBlobClient(storageUri, blobCreds);
            _container = blobClient.GetContainerReference("lastfmdata");
            _username = username;
            _errorLogger = errorLogger;
            _retryPolicy = new RetryPolicy(errorLogger: errorLogger);
            _allTracks = new List<Track>();
        }

        public async Task InitializeAsync()
        {
            await _retryPolicy.ExecuteWithRetryAsync(
                async () => 
                {
                    await _container.CreateIfNotExistsAsync();
                    return true;
                },
                "Initialize container"
            );
        }

        public async Task ProcessBatchAsync(IEnumerable<Track> tracks)
        {
            _allTracks.AddRange(tracks);
        }

        public async Task FinalizeAsync()
        {
            try
            {
                var blobName = $"data/{_username}/tracks.json.gz";
                var blobRef = _container.GetBlockBlobReference(blobName);

                // Sort tracks by scrobble time, handling null values
                var sortedTracks = _allTracks
                    .Where(t => t != null) // Filter out any null tracks
                    .OrderByDescending(t => t.scrobbleTime ?? DateTime.MinValue.ToString("o")) // Use MinValue for null scrobbleTimes
                    .ToList();

                if (!sortedTracks.Any())
                {
                    _errorLogger?.LogError(null, "No tracks to upload");
                    return;
                }

                // Compress and serialize the data
                var jsonData = JsonConvert.SerializeObject(sortedTracks, new JsonSerializerSettings 
                { 
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                var compressedData = CompressString(jsonData);
                
                await _retryPolicy.ExecuteWithRetryAsync(
                    async () => 
                    {
                        await blobRef.UploadFromStreamAsync(new MemoryStream(compressedData));
                        return true;
                    },
                    "Upload final tracks file"
                );

                _errorLogger?.LogError(null, $"Successfully uploaded {sortedTracks.Count} tracks to {blobName}");
            }
            catch (Exception ex)
            {
                _errorLogger?.LogError(ex, $"Error uploading final tracks file for user {_username}");
                throw;
            }
        }

        private byte[] CompressString(string input)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    msi.CopyTo(gs);
                }
                return mso.ToArray();
            }
        }

        public void Dispose()
        {
            _allTracks.Clear();
        }
    }
} 