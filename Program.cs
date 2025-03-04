using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using ShellProgressBar;

namespace LastFM.ReaderCore
{
    class Program
    {
        static TextInfo textInfo;

        static Program()
        {
            try
            {
                textInfo = CultureInfo.InvariantCulture.TextInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during static initialization: {0} - {1}", ex.Message, ex.StackTrace);
                throw;
            }
        }

        static async Task Main(string[] args)
        {
            await processStart();
        }

        static async Task processStart()
        {
            Console.WriteLine("Let's get started!");

            int pageSize = 200;
            string lastFMKey = LastFMConfig.getConfig("lastfmkey");

            // Setup base objects
            ICacheService cacheService = new InMemoryCacheService();
            JsonSerializer jsonSerializer = new JsonSerializer();
            ErrorLogger errorLogger = new ErrorLogger();

            using HttpClient httpClient = new HttpClient();
            LastFMClient lastFMClient = new LastFMClient(cacheService, errorLogger);
            lastFMClient.apiKey = lastFMKey;

            // Start processing LastFM data
            try
            {
                var user = Uri.EscapeDataString(LastFMConfig.getConfig("lastfmuser"));

                Console.WriteLine("Processing for user: " + user);

                var allTracks = new List<Track>();

                #if DEBUG
                    int totalPages = 1;
                #else
                    // Calls the API and gets the number of pages to grab
                    int totalPages = LastFMRunTime.getLastFMPages(user, pageSize, 1);
                #endif
  
                // Show number of pages to process
                Console.WriteLine(string.Format("Total pages to process: {0}", totalPages.ToString()));

                for (int i = 1; i < totalPages + 1; i++)
                {
                    var records = await LastFMRunTime.getLastFMRecordsByPage(user, pageSize, i);
                    allTracks.AddRange(records);
                    Console.WriteLine(string.Format("Page {0} of {1} processed", i.ToString(), totalPages.ToString()));
                };

                // Start corrections and add username    
                int trackProcessed = 0;
                int totalTracks = allTracks.Count;

                 // Initialize progress bar
                var options = new ProgressBarOptions
                {
                    ForegroundColor = ConsoleColor.Blue,
                    BackgroundColor = ConsoleColor.DarkBlue,
                    ProgressCharacter = '─'
                };

                // Do postprocessing
                using (var pbar = new ProgressBar(totalTracks, "Processing tracks", options))
                {
                    // Do postprocessing
                    foreach (var at in allTracks)
                    {
                        #if DEBUG
                            // Debug statement to print the current track being processed
                            Console.WriteLine($"Processing track: {at.name} by {at.artist.name}");
                        #endif
                        
                        // Set correct user
                        at.user = user;

                        // Get correct writing for artistname 
                        LastFMArtistCorrection ac = await lastFMClient.ArtistCorrectionAsync(at.artist.name);
                        var correctedArtist = ac.Corrections.Correction.Artist.name;

                        at.artist.name = (correctedArtist == null) ? at.artist.name : correctedArtist;

                        // Check genre for artist and add to output
                        LastFMArtistTag tag = await lastFMClient.ArtistTagAsync(at.artist.name);
                        var artistTag = (tag.Toptags.Tag.Length > 0) ? tag.Toptags.Tag[0].Name : "";

                        at.genre = textInfo.ToTitleCase(artistTag);

                        // Clean title
                        at.cleanTitle = LastFMRunTime.cleanseTitle(at.name);

                        // Convert Unix timestamp to local time and add scrobbletime
                        if (!string.IsNullOrEmpty(at.date?.uts))
                        {
                            var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(long.Parse(at.date.uts));
                            at.scrobbleTime = dateTimeOffset.LocalDateTime.ToString("o");
                        }

                        trackProcessed++;
                        pbar.Tick(trackProcessed);
                    }
                }

                await LastFMRunTime.WriteToBLOB(allTracks, user);

                Console.WriteLine("Done");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception Message: {0} - Exception: {1}", ex.Message, ex.StackTrace);
            }
        }
    }
}
