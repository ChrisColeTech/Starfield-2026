using System.Xml.Serialization;

namespace MiniToolbox.Spica.Formats.Generic.COLLADA
{
    public class DAEArray
    {
        [XmlAttribute] public string id;

        [XmlAttribute] public uint count;

        [XmlText] public string data;
    }
}
