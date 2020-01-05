using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;

namespace LastFM.ReaderCore
{
    class Program
    {
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
            string lastFMKey = LastFMConfig.getConfig("lastfmkey");

            InMemoryCache cache = new InMemoryCache();
            JsonSerializer jsonSerializer = new JsonSerializer();
            ErrorLogger errorLogger = new ErrorLogger();

            LastFMClient lastFMClient = new LastFMClient(cache, jsonSerializer, errorLogger);
            lastFMClient.apiKey = lastFMKey;

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
                        LastFMArtistCorrection ac = lastFMClient.artistCorrection(at.artist.name);
                        var correctedArtist = ac.Corrections.Correction.Artist.name;

                        at.artist.name = (correctedArtist == null) ? at.artist.name : correctedArtist;
            
                        //Check genre for artist and add to output
                        LastFMArtistTag tag = lastFMClient.artistTag(at.artist.name);
                        var artistTag = (tag.Toptags.Tag.Length > 0) ? tag.Toptags.Tag[0].Name : "";

                        at.genre = textInfo.ToTitleCase(artistTag);

                        //Clean title
                        at.cleanTitle = LastFMRunTime.cleanseTitle(at.name);

                        trackProcessed++;

                        //Console.WriteLine("Processed {0} of {1}", trackProcessed, totalTracks);
                    });

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


