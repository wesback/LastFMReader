using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace LastFM.ReaderCore
{
    class Program
    {
        static IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
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
                    int totalPages = 1;
                #else
                    //Calls the API and gets the number of pages to grab
                    int totalPages = LastFMRunTime.getLastFMPages(user, pageSize, page);
                #endif    
                //Show number of pages to process
                Console.WriteLine(string.Format("Total pages to process: {0}", totalPages.ToString()));
                
                for (int i = 1; i < totalPages+1; i++)
                {
                    var records = LastFMRunTime.getLastFMRecordsByPage(user, pageSize, i);
                    allTracks.AddRange(records);
                }
                
                //Add username to every row
                allTracks.ForEach(at =>
                {
                    if (at.user == null)
                        at.user = user;
                    
                    //Check genre for artist and add to output
                    at.genre = getArtistTag(at.artist.name);
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
            bool found = cache.TryGetValue(artist, out topTag);

            if (found == false)
            {
                topTag = LastFMRunTime.getLastFMArtistTag(artist);
                var result = cache.Set(artist, topTag);
            }

            // set proper casing before returning
            return textInfo.ToTitleCase(topTag);
        }
    }
}


