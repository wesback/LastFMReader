using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LastFM.ReaderCore
{
    class Program
    {
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

                //Calls the API and gets the number of pages to grab
                int totalPages = LastFMRunTime.getLastFMPages(user, pageSize, page);
                
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
                });
                
                await LastFMRunTime.WriteToBLOB(allTracks, user);

                Console.WriteLine("Done");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something happened - " + ex.Message);
            }
        }
    }
}


