using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace LastFM.ReaderCore
{
    class Program
    {
        static IMemoryCache tagCache = new MemoryCache(new MemoryCacheOptions());
        static IMemoryCache correctionCache = new MemoryCache(new MemoryCacheOptions());
        static TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
        static void Main(string[] args)
        {
             processStart().GetAwaiter().GetResult();
        }

        static async Task processStart()
        {
            Console.WriteLine ("Let's get started!");

            int pageSize = 200;
            int page = 1;

            try
            {
                var user = LastFMConfig.getConfig("lastfmuser");

                Console.WriteLine("Processing for user: " + user);
                
                var allTracks = new List<Track>();

                #if DEBUG
                    int totalPages = 27;
                #else
                    //Calls the API and gets the number of pages to grab
                    int totalPages = LastFMRunTime.getLastFMPages(user, pageSize, page);
                #endif    
                //Show number of pages to process
                Console.WriteLine(string.Format("Total pages to process: {0}", totalPages.ToString()));
                
                for (int i = 25; i < totalPages+1; i++)
                {
                    var records = LastFMRunTime.getLastFMRecordsByPage(user, pageSize, i);
                    allTracks.AddRange(records);
                    Console.WriteLine(string.Format("Page {0} of {1} processed", i.ToString(), totalPages.ToString()));
                };

                //Start corrections and add username    
                int trackProcessed = 0;
                int totalTracks = allTracks.Count;
              
                //Add username to every row
                allTracks.ForEach(at =>
                    {

                        if (at.user == null)
                            at.user = user;

                        //Get correct writing for artistname      
                        at.artist.name = getArtistCorrection(at.artist.name);
              
                        //Check genre for artist and add to output
                        at.genre = getArtistTag(at.artist.name);

                        //Clean title
                        at.cleanTitle = LastFMRunTime.cleanseTitle(at.name);

                        trackProcessed++;
                        Console.Write("\rProcessed {0} of {1}", trackProcessed, totalTracks);
                    });

                await LastFMRunTime.WriteToBLOB(allTracks, user);

                Console.WriteLine("Done");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something happened - " + ex.Message);
            }
        }
        
        static string getArtistTag(string artist)
        {
            string topTag;

            // set if found in cache, if not call the api
            bool found = tagCache.TryGetValue(artist, out topTag);

            if (found == false)
            {
                topTag = LastFMRunTime.getLastFMArtistTag(artist);
                var result = tagCache.Set(artist, topTag);
            }

            // set proper casing before returning
            return textInfo.ToTitleCase(topTag);
        }

        static string getArtistCorrection(string artist)
        {
            string correctedArtist;

            // set if found in cache, if not call the api
            bool found = correctionCache.TryGetValue(artist, out correctedArtist);

            if (found == false)
            {
                correctedArtist = LastFMRunTime.getLastFMArtistCorrection(artist);
                var result = correctionCache.Set(artist, correctedArtist);
            }

            return correctedArtist;
        }
    }
}


