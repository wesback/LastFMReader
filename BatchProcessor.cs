using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Auth;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace LastFM.ReaderCore
{
    public class BatchProcessor : IDisposable
    {
        private readonly CloudBlobContainer _container;
        private readonly string _username;
        private readonly IErrorLogger _errorLogger;
        private readonly RetryPolicy _retryPolicy;
        private readonly HashSet<string> _processedTrackIds;
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
            _processedTrackIds = new HashSet<string>();
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
            foreach (var track in tracks)
            {
                // Create a unique ID for the track using artist, name, and timestamp
                var trackId = $"{track.artist.name}_{track.name}_{track.date?.uts}";
                if (!_processedTrackIds.Contains(trackId))
                {
                    _processedTrackIds.Add(trackId);
                    _allTracks.Add(track);
                }
            }
        }

        public async Task FinalizeAsync()
        {
            try
            {
                var blobName = $"data/{_username}.json";
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

                // Serialize the data
                var jsonData = JsonConvert.SerializeObject(sortedTracks, new JsonSerializerSettings 
                { 
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                
                await _retryPolicy.ExecuteWithRetryAsync(
                    async () => 
                    {
                        await blobRef.UploadTextAsync(jsonData);
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

        public void Dispose()
        {
            _allTracks.Clear();
            _processedTrackIds.Clear();
        }
    }
} 