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
using System.Globalization;
using System.Linq;
using System.Collections.Concurrent;

namespace LastFM.ReaderCore
{
    public class LastFMRunTime
    {
        static string lastFMKey = LastFMConfig.getConfig("lastfmkey");
        static string storageAccount = LastFMConfig.getConfig("storageaccount");
        static string storageKey = LastFMConfig.getConfig("storagekey");
        static CleaningRule cleaningRules;
        private static readonly HttpClient client = new HttpClient();
        private static readonly ConcurrentDictionary<string, Regex> regexCache = new();

        static LastFMRunTime()
        {
            var rulesPath = Path.Combine(Directory.GetCurrentDirectory(), "CleaningRules.json");
            var rulesJson = File.ReadAllText(rulesPath);
            cleaningRules = JsonConvert.DeserializeObject<CleaningRule>(rulesJson);
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
            try
            {
                var blobCreds = new StorageCredentials(storageAccount, storageKey);
                var storageUri = new Uri(@"https://" + storageAccount + ".blob.core.windows.net/");
                var blobstorageclient = new Microsoft.Azure.Storage.Blob.CloudBlobClient(storageUri, blobCreds);
                var containerRef = blobstorageclient.GetContainerReference("lastfmdata");

                await containerRef.CreateIfNotExistsAsync();
                var blobRef = containerRef.GetBlockBlobReference(string.Format("data/{0}.json", username));

                var allTracksSerialized = JsonConvert.SerializeObject(allTracks, new JsonSerializerSettings());

                var blobstream = await blobRef.OpenWriteAsync();

                using (var sw = new StreamWriter(blobstream))
                {
                    sw.Write(allTracksSerialized);
                    sw.Close();
                }
                blobstream.Close();
            }
            catch (Exception ex)
            {
                // Handle the globalization-invariant mode issue.
                Console.Error.WriteLine("An error occurred: " + ex.Message);
                throw;
            }
        }
        public static string CleanseTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title) || cleaningRules?.Rules?.Rule == null)
                return title;

            string cleanTitle = title;

            // Sort rules by sequence once
            foreach (var rule in cleaningRules.Rules.Rule.OrderBy(r => r.Sequence))
            {
                if (rule.IsRegEx)
                {
                    var regex = regexCache.GetOrAdd(rule.OldValue,
                        key => new Regex(key, RegexOptions.IgnoreCase | RegexOptions.Compiled));

                    cleanTitle = regex.Replace(cleanTitle, rule.NewValue ?? "");
                }
                else
                {
                    cleanTitle = cleanTitle.Replace(rule.OldValue, rule.NewValue ?? "");
                }
            }

            return cleanTitle.Trim();
        }
    }
}
