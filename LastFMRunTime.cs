﻿using System;
using System.Collections.Generic;
using RestSharp;
using Microsoft.WindowsAzure.Storage.Auth;
using System.Threading.Tasks;

namespace LastFM.ReaderCore
{
    public class LastFMRunTime
    {
        static string lastFMKey = LastFMConfig.getConfig("lastfmkey");
        static string storageAccount = LastFMConfig.getConfig("storageaccount");
        static string storageKey = LastFMConfig.getConfig("storagekey");

        public static IEnumerable<Track> getLastFMRecordsByPage(string userName, int pageSize, int page)
        {
            var client = new RestClient("http://ws.audioscrobbler.com/2.0/");
            var request = new RestRequest("", Method.POST);

            request.AddQueryParameter("method", "user.getRecentTracks");
            request.AddQueryParameter("user", userName);
            request.AddQueryParameter("api_key", lastFMKey);
            request.AddQueryParameter("format", "json");
            request.AddQueryParameter("limit", Convert.ToString(pageSize));
            request.AddQueryParameter("page", Convert.ToString(page));
            request.AddQueryParameter("extended", "1");

            var response = client.Execute(request);
            var content = response.Content;

            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<LastFMRecord>(content);
            if (deserialized != null && deserialized.recenttracks != null && deserialized.recenttracks.track != null)
            {
                return deserialized.recenttracks.track;
            }
            else return new List<Track>();
        }

        public static int getLastFMPages(string userName, int pageSize, int page)
        {
            var client = new RestClient("http://ws.audioscrobbler.com/2.0/");
            var request = new RestRequest("", Method.POST);

            request.AddQueryParameter("method", "user.getRecentTracks");
            request.AddQueryParameter("user", userName);
            request.AddQueryParameter("api_key", lastFMKey);
            request.AddQueryParameter("format", "json");
            request.AddQueryParameter("limit", Convert.ToString(pageSize));
            request.AddQueryParameter("page", Convert.ToString(page));
            request.AddQueryParameter("extended", "1");

            var response = client.Execute<List<LastFMRecord>>(request);
            var content = response.Content; // raw content as string
            var des = Newtonsoft.Json.JsonConvert.DeserializeObject<LastFMRecord>(content);
            return int.Parse(des.recenttracks.attr.totalPages);


        }

        public static async Task WriteToBLOB(List<Track> allTracks, string username)
        {
            var blobCreds = new StorageCredentials(storageAccount, storageKey);
            var storageUri = new Uri(@"https://" + storageAccount + ".blob.core.windows.net/");
            var blobstorageclient = new Microsoft.WindowsAzure.Storage.Blob.CloudBlobClient(storageUri, blobCreds);
            var containerRef = blobstorageclient.GetContainerReference("lastfmdata");

            await containerRef.CreateIfNotExistsAsync();
            var blobRef = containerRef.GetBlockBlobReference(string.Format("data/{0}.json", username));


            var allTracksSerialized = Newtonsoft.Json.JsonConvert.SerializeObject(allTracks);

            var blobstream = await blobRef.OpenWriteAsync();

            using (var sw = new System.IO.StreamWriter(blobstream))
            {
                sw.Write(allTracksSerialized);
                sw.Close();
            }
            blobstream.Close();
        }

        public static string getLastFMArtistTag(string artist)
        {
            var client = new RestClient("http://ws.audioscrobbler.com/2.0/");
            var request = new RestRequest("", Method.POST);

            request.AddQueryParameter("method", "artist.gettoptags");
            request.AddQueryParameter("api_key", lastFMKey);
            request.AddQueryParameter("format", "json");
            request.AddQueryParameter("artist", artist);


            var response = client.Execute(request);
            var content = response.Content;

            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<LastFMArtistTags>(content);
            if (deserialized != null && deserialized.Toptags.Tag.Length > 0)
            {
                return deserialized.Toptags.Tag[0].Name;
            }
            else return "";
        }
    }
}
