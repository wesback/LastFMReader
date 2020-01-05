using Newtonsoft.Json;

namespace LastFM.ReaderCore
{
    public partial class LastFMArtistTag
    {
        [JsonProperty("toptags")]
        public Toptags Toptags { get; set; }

    }

    public partial class Toptags
    {
        [JsonProperty("tag")]
        public Tag[] Tag { get; set; }

        [JsonProperty("@attr")]
        public Attr Attr { get; set; }
    }

    public partial class ArtistAttr
    {
        [JsonProperty("artist")]
        public string Artist { get; set; }
    }

    public partial class Tag
    {
        [JsonProperty("count")]
        public long Count { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

}
