using MiniToolbox.Spica.Serialization;
using MiniToolbox.Spica.Serialization.Attributes;

namespace MiniToolbox.Spica.Formats.CtrGfx.Animation
{
    public class GfxAnimFloat : ICustomSerialization
    {
        [Ignore] private GfxFloatKeyFrameGroup _Value;

        public GfxFloatKeyFrameGroup Value => _Value;

        public GfxAnimFloat()
        {
            _Value = new GfxFloatKeyFrameGroup();
        }

        void ICustomSerialization.Deserialize(BinaryDeserializer Deserializer)
        {
            GfxAnimVector.SetVector(Deserializer, _Value);
        }

        bool ICustomSerialization.Serialize(BinarySerializer Serializer)
        {
            GfxAnimVector.WriteVector(Serializer, _Value);

            return true;
        }
    }
}
