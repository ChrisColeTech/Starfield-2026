using MiniToolbox.Spica.Formats.Common;
using MiniToolbox.Spica.Serialization;
using MiniToolbox.Spica.Serialization.Attributes;

namespace MiniToolbox.Spica.Formats.CtrGfx
{
    public class GfxStringUtf8 : ICustomSerialization
    {
        [Ignore] private string Str;

        public GfxStringUtf8() { }

        public GfxStringUtf8(string Str)
        {
            this.Str = Str;
        }

        public override string ToString()
        {
            return Str ?? string.Empty;
        }

        void ICustomSerialization.Deserialize(BinaryDeserializer Deserializer)
        {
            Str = Deserializer.Reader.ReadNullTerminatedStringUtf8();
        }

        bool ICustomSerialization.Serialize(BinarySerializer Serializer)
        {
            Serializer.Writer.WriteNullTerminatedStringUtf8(Str);

            return true;
        }
    }
}
