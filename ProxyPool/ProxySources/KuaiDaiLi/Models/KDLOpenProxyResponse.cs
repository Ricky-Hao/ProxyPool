using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProxyPool.ProxySources.KuaiDaiLi.Models
{
    public class KDLOpenProxyResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = null!;

        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonConverter(typeof(KDLOpenProxyResponseDataConverter))]
        [JsonPropertyName("data")]
        public KDLOpenProxyResponseData Data { get; set; } = null!;
    }

    public class KDLOpenProxyResponseData
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("proxy_list")]
        public List<string> Proxies { get; set; } = new();
    }

    public class KDLOpenProxyResponseDataConverter : JsonConverter<KDLOpenProxyResponseData>
    {
        public override KDLOpenProxyResponseData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var data = new KDLOpenProxyResponseData();
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var key = reader.GetString();
                        reader.Read();
                        if (key == "count")
                        {
                            data.Count = reader.GetInt32();
                        }
                        else if (key == "proxy_list" && reader.TokenType == JsonTokenType.StartArray)
                        {
                            reader.Read();
                            while (reader.TokenType != JsonTokenType.EndArray)
                            {
                                if (reader.GetString() is string value)
                                    data.Proxies.Add(value);
                                reader.Read();
                            }
                        }
                        else
                            throw new Exception($"Unknown Json property name for KDLOpenProxyResponseData: {key}");
                    }
                }
            }
            return data;
        }

        public override void Write(Utf8JsonWriter writer, KDLOpenProxyResponseData value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }

}
