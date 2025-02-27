using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public class JsonSerializer
{
    private readonly Newtonsoft.Json.JsonSerializer _serializer;

    public JsonSerializer()
    {
        ContentType = "application/json";
        _serializer = new Newtonsoft.Json.JsonSerializer
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Unspecified
        };
    }

    public JsonSerializer(Newtonsoft.Json.JsonSerializer serializer)
    {
        ContentType = "application/json";
        _serializer = serializer;
    }

    public string Serialize(object obj)
    {
        using (var stringWriter = new StringWriter())
        {
            using (var jsonTextWriter = new JsonTextWriter(stringWriter))
            {
                jsonTextWriter.Formatting = Formatting.Indented;
                jsonTextWriter.QuoteChar = '"';

                _serializer.Serialize(jsonTextWriter, obj);

                var result = stringWriter.ToString();
                return result;
            }
        }
    }

    public string DateFormat { get; set; }
    public string RootElement { get; set; }
    public string Namespace { get; set; }
    public string ContentType { get; set; }

    public async Task<T> Deserialize<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        using (var stringReader = new StringReader(content))
        {
            using (var jsonTextReader = new JsonTextReader(stringReader))
            {
                return _serializer.Deserialize<T>(jsonTextReader);
            }
        }
    }
}