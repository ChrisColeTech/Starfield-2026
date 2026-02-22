using System.Xml.Serialization;

namespace MiniToolbox.Spica.Formats.Generic.COLLADA
{
    public class DAEGeometry
    {
        [XmlAttribute] public string id;
        [XmlAttribute] public string name;

        public DAEMesh mesh = new DAEMesh();
    }
}
