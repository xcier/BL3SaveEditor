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
            serializer.Serialize(writer, value);
        }
    }
}
