using System.Xml.Serialization;

namespace MiniToolbox.Spica.Formats.Generic.COLLADA
{
    public class DAEController
    {
        [XmlAttribute] public string id;
        [XmlAttribute] public string name;

        public DAESkin skin = new DAESkin();
    }
}
