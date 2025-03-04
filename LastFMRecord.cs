using Newtonsoft.Json;

namespace LastFM.ReaderCore
{
    public class LastFMRecord
    {
        public Recenttracks recenttracks { get; set; }
    }

    public class Recenttracks
    {
        public Track[] track { get; set; }

        [JsonProperty(PropertyName = "@attr")]
        public Attr attr { get; set; }
    }

    public class Attr
    {
        public string user { get; set; }
        public string page { get; set; }
        public string perPage { get; set; }
        public string totalPages { get; set; }
        public string total { get; set; }
    }

    public class Track
    {
        public Artist artist { get; set; }
        public string loved { get; set; }
        public string name { get; set; }
        public string streamable { get; set; }
        public string mbid { get; set; }
        public Album album { get; set; }
        public string url { get; set; }
        public Image[] image { get; set; }
        public Date date { get; set; }
        public string user { get; set; }
        public string genre { get; set; }
        public string cleanTitle { get; set; }
        public string scrobbleTime { get; set; }
    }

    public class Artist
    {
        public string name { get; set; }
        public string mbid { get; set; }
        public string url { get; set; }
        public Image[] image { get; set; }
    }

    public class Image
    {
        public string text { get; set; }
        public string size { get; set; }
    }

    public class Album
    {
        public string text { get; set; }
        public string mbid { get; set; }
    }

    public class Date
    {
        public string text { get; set; }
        public string uts { get; set; }
    }
/*
    public class Image1
    {
        public string text { get; set; }
        public string size { get; set; }
    }
    */

}
