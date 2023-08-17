using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BL3Tools
{
    internal class ObjectToArrayConverter<TNew, TKey, TValue> : JsonConverter where TNew : IKeyValueJSON<TKey, TValue>, new()
    {
        public static bool PS4Format { get; set; }
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(reader.TokenType == JsonToken.StartObject) 
            {
                var instance = serializer.Deserialize<Dictionary<TKey, TValue>>(reader);
                return instance.Select(i => new TNew() { key = i.Key, value = i.Value }).ToList();
            }
            else if (reader.TokenType == JsonToken.StartArray)
            {
                return serializer.Deserialize(reader, objectType);
            }
            else
            {
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is List<TNew> keyValueList)
            {
                var dict = new Dictionary<TKey, TValue>();
                foreach (var val in keyValueList)
                {
                    dict[val.key] = val.value; // Using the assignment operator to handle potential duplicate keys
                }
                serializer.Serialize(writer, dict);
            }
            else
            {
                serializer.Serialize(writer, value);
            }
        }
    }
}
