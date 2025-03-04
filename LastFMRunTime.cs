using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.Storage.Auth;
using System.Text;

namespace LastFM.ReaderCore
{
    public class LastFMRunTime
    {
        static string lastFMKey = LastFMConfig.getConfig("lastfmkey");
        static string storageAccount = LastFMConfig.getConfig("storageaccount");
        static string storageKey = LastFMConfig.getConfig("storagekey");
        static CleaningRule cleaningRules;
        private static readonly HttpClient client = new HttpClient();
        private static Dictionary<string, Regex> regexCache = new Dictionary<string, Regex>();

        static LastFMRunTime()
        {
           var rules = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "CleaningRules.json"));
           cleaningRules = JsonConvert.DeserializeObject<CleaningRule>(rules);
        }

        public static async Task<IEnumerable<Track>> getLastFMRecordsByPage(string userName, int pageSize, int page)
        {
            var url = $"https://ws.audioscrobbler.com/2.0/?method=user.getRecentTracks&user={userName}&api_key={lastFMKey}&format=json&limit={pageSize}&page={page}&extended=1";
            int retryCount = 0;
            int maxRetries = 3;
            int delay = 1000; // Initial delay in milliseconds

            while (retryCount < maxRetries)
            {
                try
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();

                    var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<LastFMRecord>(content);
                    if (deserialized != null && deserialized.recenttracks != null && deserialized.recenttracks.track != null)
                    {
                        return deserialized.recenttracks.track;
                    }
                    else
                    {
                        return new List<Track>();
                    }
                }
                catch (HttpRequestException)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        throw;
                    }
                    await Task.Delay(delay);
                    delay *= 2; // Exponential backoff
                }
            }

            return new List<Track>();
        }

        public static int getLastFMPages(string userName, int pageSize, int page)
        {
            var url = $"https://ws.audioscrobbler.com/2.0/?method=user.getRecentTracks&user={userName}&api_key={lastFMKey}&format=json&limit={pageSize}&page={page}&extended=1";
            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
            var content = response.Content.ReadAsStringAsync().Result;
            var des = Newtonsoft.Json.JsonConvert.DeserializeObject<LastFMRecord>(content);
            return int.Parse(des.recenttracks.attr.totalPages);
        }

        public static async Task WriteToBLOB(List<Track> allTracks, string username)
        {
             // Set the environment variable to disable globalization-invariant mode
            Environment.SetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "false");
            
            var blobCreds = new StorageCredentials(storageAccount, storageKey);
            var storageUri = new Uri(@"https://" + storageAccount + ".blob.core.windows.net/");
            var blobstorageclient = new Microsoft.Azure.Storage.Blob.CloudBlobClient(storageUri, blobCreds);
            var containerRef = blobstorageclient.GetContainerReference("lastfmdata");

            await containerRef.CreateIfNotExistsAsync();
            var blobRef = containerRef.GetBlockBlobReference(string.Format("data/{0}.json", username));


            var allTracksSerialized = JsonConvert.SerializeObject(allTracks);

            var blobstream = await blobRef.OpenWriteAsync();

            using (var sw = new StreamWriter(blobstream))
            {
                sw.Write(allTracksSerialized);
                sw.Close();
            }
            blobstream.Close();
        }

        public static string cleanseTitle(string title)
        {
            StringBuilder cleanTitle = new StringBuilder(title);
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            Parallel.ForEach(cleaningRules.Rules.Rule, parallelOptions, (r) =>
            {
                if (r.IsRegEx)
                {
                    lock (regexCache)
                    {
                        cleanTitle = new StringBuilder(cleanseWithRegEx(cleanTitle.ToString(), r.OldValue));
                    }
                }
                else
                {
                    lock (cleanTitle)
                    {
                        cleanTitle.Replace(r.OldValue, r.NewValue);
                    }
                }
            });

            return cleanTitle.ToString().TrimEnd();
        }

        private static string cleanseWithRegEx(string title, string pattern)
        {
            if (!regexCache.ContainsKey(pattern))
            {
                regexCache[pattern] = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }

            return regexCache[pattern].Replace(title, "");
        }
    }
}
