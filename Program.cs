using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using LastFM.ReaderCore.Logging;

namespace LastFM.ReaderCore
{
    class Program
    {
        static TextInfo textInfo;
        private static ILogger _logger;

        static Program()
        {
            try
            {
                CultureInfo cultureInfo = new CultureInfo("en-US");
                textInfo = cultureInfo.TextInfo;
                
                #if DEBUG
                    _logger = new StructuredLogger("logs/app.log", LogLevel.Debug);
                #else
                    _logger = new StructuredLogger("logs/app.log", LogLevel.Information);
                #endif
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during static initialization: {0} - {1}", ex.Message, ex.StackTrace);
                throw;
            }
        }

        static async Task Main(string[] args)
        {
            try
            {
                await processStart();
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Application failed to complete successfully", ex);
                throw;
            }
        }

        static async Task processStart()
        {
            _logger.LogInformation("Starting LastFM data processing");

            int pageSize = 200;
            string lastFMKey = LastFMConfig.getConfig("lastfmkey");
            string storageAccount = LastFMConfig.getConfig("storageaccount");
            string storageKey = LastFMConfig.getConfig("storagekey");

            // Setup base objects
            ICacheService cacheService = new InMemoryCacheService(maxCacheSizeMB: 500, defaultExpiration: TimeSpan.FromHours(24));
            JsonSerializer jsonSerializer = new JsonSerializer();
            ErrorLogger errorLogger = new ErrorLogger();

            using HttpClient httpClient = new HttpClient();
            LastFMClient lastFMClient = new LastFMClient(cacheService, errorLogger);
            lastFMClient.apiKey = lastFMKey;

            // Start processing LastFM data
            try
            {
                var user = Uri.EscapeDataString(LastFMConfig.getConfig("lastfmuser"));
                _logger.LogInformation($"Processing data for user: {user}");

                using var batchProcessor = new BatchProcessor(storageAccount, storageKey, user, errorLogger: errorLogger);
                await batchProcessor.InitializeAsync();

                #if DEBUG
                    int totalPages = 1;
                #else
                    int totalPages = LastFMRunTime.getLastFMPages(user, pageSize, 1);
                #endif

                _logger.LogInformation($"Total pages to process: {totalPages}");
                Console.WriteLine($"Starting to process {totalPages} pages...");

                var currentBatch = new List<Track>();
                int processedTracks = 0;
                var startTime = DateTime.UtcNow;
                var lastProgressUpdate = DateTime.UtcNow;

                for (int i = 1; i < totalPages + 1; i++)
                {
                    _logger.LogDebug($"Processing page {i} of {totalPages}");
                    
                    var records = await LastFMRunTime.getLastFMRecordsByPage(user, pageSize, i);
                    
                    foreach (var track in records)
                    {
                        try
                        {
                            // Set correct user
                            track.user = user;

                            // Process track sequentially to respect rate limits
                            var processedTrack = await ProcessTrackAsync(track, lastFMClient, textInfo);
                            if (processedTrack != null)
                            {
                                currentBatch.Add(processedTrack);
                                processedTracks++;

                                // Update progress every 5 seconds
                                if (DateTime.UtcNow - lastProgressUpdate >= TimeSpan.FromSeconds(5))
                                {
                                    var elapsed = DateTime.UtcNow - startTime;
                                    var tracksPerSecond = processedTracks / elapsed.TotalSeconds;
                                    Console.WriteLine($"\rProgress: {processedTracks} tracks processed ({tracksPerSecond:F2} tracks/sec)");
                                    lastProgressUpdate = DateTime.UtcNow;
                                }

                                // Process batch when it reaches the size limit
                                if (currentBatch.Count >= 1000)
                                {
                                    await batchProcessor.ProcessBatchAsync(currentBatch);
                                    currentBatch.Clear();
                                    _logger.LogInformation($"Processed {processedTracks} tracks so far...");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing track: {track.name} by {track.artist.name}", ex);
                        }
                    }

                    // Show progress after processing the page
                    Console.WriteLine($"\rCompleted page {i}/{totalPages} ({processedTracks} tracks processed)...");
                    _logger.LogInformation($"Completed page {i} of {totalPages}");
                }

                // Process any remaining tracks
                if (currentBatch.Any())
                {
                    await batchProcessor.ProcessBatchAsync(currentBatch);
                }

                // Finalize and upload all tracks
                await batchProcessor.FinalizeAsync();

                var totalTime = DateTime.UtcNow - startTime;
                var completionMessage = $"Completed processing {processedTracks} tracks in {totalTime.TotalMinutes:F2} minutes";
                _logger.LogInformation(completionMessage);
                Console.WriteLine($"\n{completionMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during data processing", ex);
                throw;
            }
        }

        private static async Task<Track> ProcessTrackAsync(Track track, LastFMClient lastFMClient, TextInfo textInfo)
        {
            try
            {
                // Get correct writing for artistname 
                LastFMArtistCorrection ac = await lastFMClient.ArtistCorrectionAsync(track.artist.name);
                var correctedArtist = ac.Corrections.Correction.Artist.name;
                track.artist.name = (correctedArtist == null) ? track.artist.name : correctedArtist;

                // Check genre for artist and add to output
                LastFMArtistTag tag = await lastFMClient.ArtistTagAsync(track.artist.name);
                var artistTag = (tag.Toptags.Tag.Length > 0) ? tag.Toptags.Tag[0].Name : "";
                track.genre = textInfo.ToTitleCase(artistTag);

                // Clean title
                track.cleanTitle = textInfo.ToTitleCase(LastFMRunTime.CleanseTitle(track.name));

                // Convert Unix timestamp to local time and add scrobbletime
                if (!string.IsNullOrEmpty(track.date?.uts))
                {
                    var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(long.Parse(track.date.uts));
                    track.scrobbleTime = dateTimeOffset.LocalDateTime.ToString("o");
                }

                return track;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing track: {track.name} by {track.artist.name}", ex);
                return null;
            }
        }
    }
}
