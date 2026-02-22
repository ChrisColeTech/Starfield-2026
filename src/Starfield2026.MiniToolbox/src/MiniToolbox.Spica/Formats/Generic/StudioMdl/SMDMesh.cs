using MiniToolbox.Spica.PICA.Converters;

using System.Collections.Generic;

namespace MiniToolbox.Spica.Formats.Generic.StudioMdl
{
    class SMDMesh
    {
        public string MaterialName;

        public List<PICAVertex> Vertices;

        public SMDMesh()
        {
            Vertices = new List<PICAVertex>();
        }
    }
}
