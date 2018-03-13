using System;
using System.Collections.Generic;

namespace LastFM.ReaderCore
{
    class Program
    {
        static void Main(string[] args)
        {
            int pageSize = 200;
            int page = 1;

            string[] users = LastFMConfig.getConfigArray("LastFMSettings:users", "name").ToArray();

            try
            {
                foreach (string user in users)
                {
                    Console.WriteLine("Processing for user: " + user);
                    var allTracks = new List<Track>();
                    int totalPages = LastFMRunTime.getLastFMPages(user, pageSize, page);

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
                    LastFMRunTime.WriteToBLOB(allTracks, user);
                }

                Console.WriteLine("Writing to blob storage now.");
                Console.WriteLine("Written to blob storage.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something happened - " + ex.Message);
            }
          
        }
    }
}


