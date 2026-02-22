using System.Xml.Serialization;

namespace MiniToolbox.Spica.Formats.Generic.COLLADA
{
    public class DAEInput
    {
        [XmlAttribute] public string semantic;
        [XmlAttribute] public string source;
    }
}
