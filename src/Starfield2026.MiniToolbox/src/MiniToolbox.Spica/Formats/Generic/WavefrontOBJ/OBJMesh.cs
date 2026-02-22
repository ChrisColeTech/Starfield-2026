using MiniToolbox.Spica.PICA.Converters;

using System.Collections.Generic;

namespace MiniToolbox.Spica.Formats.Generic.WavefrontOBJ
{
    class OBJMesh
    {
        public bool HasPosition;
        public bool HasNormal;
        public bool HasTexCoord;

        public string MaterialName;

        public List<PICAVertex> Vertices;

        public OBJMesh(string MaterialName = "NoMaterial")
        {
            this.MaterialName = MaterialName;

            Vertices = new List<PICAVertex>();
        }
    }
}
