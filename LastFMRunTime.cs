using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.Storage.Auth;

namespace LastFM.ReaderCore
{
    public class LastFMRunTime
    {
        static string lastFMKey = LastFMConfig.getConfig("lastfmkey");
        static string storageAccount = LastFMConfig.getConfig("storageaccount");
        static string storageKey = LastFMConfig.getConfig("storagekey");
        static CleaningRule cleaningRules;
        private static readonly HttpClient client = new HttpClient();

        static LastFMRunTime()
        {
           var rules = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "CleaningRules.json"));
           cleaningRules = JsonConvert.DeserializeObject<CleaningRule>(rules);
        }

        public static async Task<IEnumerable<Track>> getLastFMRecordsByPage(string userName, int pageSize, int page)
        {
            var url = $"http://ws.audioscrobbler.com/2.0/?method=user.getRecentTracks&user={userName}&api_key={lastFMKey}&format=json&limit={pageSize}&page={page}&extended=1";
            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
            var content = response.Content.ReadAsStringAsync().Result;

            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<LastFMRecord>(content);
            if (deserialized != null && deserialized.recenttracks != null && deserialized.recenttracks.track != null)
            {
                return deserialized.recenttracks.track;
            }
            else return new List<Track>();
        }

        public static int getLastFMPages(string userName, int pageSize, int page)
        {
            var url = $"http://ws.audioscrobbler.com/2.0/?method=user.getRecentTracks&user={userName}&api_key={lastFMKey}&format=json&limit={pageSize}&page={page}&extended=1";
            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
            var content = response.Content.ReadAsStringAsync().Result;
            var des = Newtonsoft.Json.JsonConvert.DeserializeObject<LastFMRecord>(content);
            return int.Parse(des.recenttracks.attr.totalPages);
        }

        public static async Task WriteToBLOB(List<Track> allTracks, string username)
        {
            var blobCreds = new StorageCredentials(storageAccount, storageKey);
            var storageUri = new Uri(@"https://" + storageAccount + ".blob.core.windows.net/");
            var blobstorageclient = new Microsoft.Azure.Storage.Blob.CloudBlobClient(storageUri, blobCreds);
            var containerRef = blobstorageclient.GetContainerReference("lastfmdata");

            await containerRef.CreateIfNotExistsAsync();
            var blobRef = containerRef.GetBlockBlobReference(string.Format("data/{0}.json", username));


            var allTracksSerialized = JsonConvert.SerializeObject(allTracks);

            var blobstream = await blobRef.OpenWriteAsync();

            using (var sw = new System.IO.StreamWriter(blobstream))
            {
                sw.Write(allTracksSerialized);
                sw.Close();
            }
            blobstream.Close();
        }

        public static string cleanseTitle(string title)
        {
            string cleanTitle = title;

            foreach (Rule r in cleaningRules.Rules.Rule)
            {
                if (r.IsRegEx)
                {
                    cleanTitle = cleanseWithRegEx(cleanTitle, r.OldValue);
                }
                else
                {
                    cleanTitle = cleanTitle.Replace(r.OldValue, r.NewValue);           
                }
            }

            return cleanTitle.TrimEnd();
        }
        private static string cleanseWithRegEx(string title, string pattern)
        {
            Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            return rgx.Replace(title, "");
        }
    }
}
