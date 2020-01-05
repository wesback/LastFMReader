using RestSharp;
using RestSharp.Deserializers;
using System;


namespace LastFM.ReaderCore
{
    public class LastFMClient : BaseClient 
    {
        public string apiKey { get; set; }

        public LastFMClient (ICacheService cache, IDeserializer serializer, IErrorLogger errorLogger)
            : base(cache, serializer, errorLogger, "http://ws.audioscrobbler.com/2.0/") { }

        public LastFMArtistCorrection artistCorrection(string artist)
        {

            var request = new RestRequest("", Method.POST);
            var prefix = "corr";

            request.AddQueryParameter("method", "artist.getcorrection");
            request.AddQueryParameter("api_key", apiKey);
            request.AddQueryParameter("format", "json");
            request.AddQueryParameter("artist", artist);

            return GetFromCache<LastFMArtistCorrection>(request, String.Concat(prefix, artist));
        }    

        public LastFMArtistTag artistTag(string artist)
        {

            var request = new RestRequest("", Method.POST);
            var prefix = "tag";

            request.AddQueryParameter("method", "artist.gettoptags");
            request.AddQueryParameter("api_key", apiKey);
            request.AddQueryParameter("format", "json");
            request.AddQueryParameter("artist", artist);


            return GetFromCache<LastFMArtistTag>(request, String.Concat(prefix, artist));
        }    
    }
}