using MiniToolbox.Trpak.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiniToolbox.Trpak.Flatbuffers.Utils;
using MiniToolbox.Core.Utils;

namespace MiniToolbox.Trpak.Flatbuffers.Converters
{
    public class QuaternionConverter : JsonConverter<PackedQuaternion>
    {
        public override PackedQuaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var quatdict = JsonSerializer.Deserialize<Dictionary<string, float>>(ref reader, options);
            Quaternion quaternion = new Quaternion(quatdict["X"], quatdict["Y"], quatdict["Z"], quatdict["W"]);
            return quaternion.Pack();
        }

        public override void Write(Utf8JsonWriter writer, PackedQuaternion value, JsonSerializerOptions options)
        {
            Quaternion quaternion = value.Unpack();
            JsonSerializer.Serialize(writer, quaternion.ToDictionary(), options);
        }
    }

}
