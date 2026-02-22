using System.Xml.Serialization;

namespace MiniToolbox.Spica.Formats.Generic.COLLADA
{
    public class DAEMaterial
    {
        [XmlAttribute] public string id;
        [XmlAttribute] public string name;

        public DAEInstanceEffect instance_effect = new DAEInstanceEffect();
    }

    public class DAEInstanceEffect
    {
        [XmlAttribute] public string url;
    }
}
