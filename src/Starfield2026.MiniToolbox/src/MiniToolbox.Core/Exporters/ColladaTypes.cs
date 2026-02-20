using System.Globalization;
using System.Xml;
using OpenTK.Mathematics;

namespace MiniToolbox.Core.Exporters
{
    /// <summary>
    /// COLLADA XML DOM types for building DAE documents.
    /// Adapted from drp-to-dae ColladaExporter.cs — switched to OpenTK.Mathematics.
    /// </summary>

    public enum SemanticType
    {
        POSITION,
        VERTEX,
        NORMAL,
        TEXCOORD,
        COLOR,
        WEIGHT,
        JOINT,
        INV_BIND_MATRIX
    }

    public class ColladaGeometry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public ColladaMesh Mesh { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("geometry");
            node.Attributes.Append(Attr(doc, "id", Id));
            node.Attributes.Append(Attr(doc, "name", Name));
            parent.AppendChild(node);
            Mesh.Write(doc, node);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaMesh
    {
        public List<ColladaSource> Sources { get; set; } = new();
        public ColladaVertices Vertices { get; set; } = new();
        public List<ColladaPolygons> Polygons { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("mesh");
            parent.AppendChild(node);
            foreach (var s in Sources) s.Write(doc, node);
            Vertices.Write(doc, node);
            foreach (var p in Polygons) p.Write(doc, node);
        }
    }

    public class ColladaSource
    {
        public string Id { get; set; } = "";
        public float[]? Data { get; set; }
        public string[]? DataString { get; set; }
        public int Stride { get; set; } = 3;
        public List<string> AccessorParams { get; set; } = new();
        public bool IsNameArray { get; set; } = false;

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("source");
            node.Attributes.Append(Attr(doc, "id", Id));
            parent.AppendChild(node);

            if (IsNameArray)
            {
                var arr = doc.CreateElement("Name_array");
                arr.Attributes.Append(Attr(doc, "id", Id + "-array"));
                arr.Attributes.Append(Attr(doc, "count", (DataString?.Length ?? 0).ToString()));
                arr.InnerText = DataString != null ? string.Join(" ", DataString) : "";
                node.AppendChild(arr);
            }
            else
            {
                var arr = doc.CreateElement("float_array");
                arr.Attributes.Append(Attr(doc, "id", Id + "-array"));
                arr.Attributes.Append(Attr(doc, "count", (Data?.Length ?? 0).ToString()));
                arr.InnerText = Data != null ? string.Join(" ", Array.ConvertAll(Data, Fmt)) : "";
                node.AppendChild(arr);
            }

            var tc = doc.CreateElement("technique_common");
            node.AppendChild(tc);

            var accessor = doc.CreateElement("accessor");
            accessor.Attributes.Append(Attr(doc, "source", "#" + Id + "-array"));
            accessor.Attributes.Append(Attr(doc, "count", ((Data?.Length ?? DataString?.Length ?? 0) / Math.Max(1, Stride)).ToString()));
            accessor.Attributes.Append(Attr(doc, "stride", Stride.ToString()));
            tc.AppendChild(accessor);

            foreach (var p in AccessorParams)
            {
                var pa = doc.CreateElement("param");
                pa.Attributes.Append(Attr(doc, "name", p));
                if (p == "TRANSFORM") pa.Attributes.Append(Attr(doc, "type", "float4x4"));
                else if (IsNameArray) pa.Attributes.Append(Attr(doc, "type", "Name"));
                else pa.Attributes.Append(Attr(doc, "type", "float"));
                accessor.AppendChild(pa);
            }
        }

        private static string Fmt(float f) => f.ToString("0.######", CultureInfo.InvariantCulture);
        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaVertices
    {
        public string Id { get; set; } = "";
        public List<ColladaInput> Inputs { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("vertices");
            node.Attributes.Append(Attr(doc, "id", Id));
            parent.AppendChild(node);
            foreach (var i in Inputs) i.Write(doc, node);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaPolygons
    {
        public int Count { get; set; }
        public int[]? RemappedIndices { get; set; }
        public string MaterialSymbol { get; set; } = "";
        public List<ColladaInput> Inputs { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("triangles");
            node.Attributes.Append(Attr(doc, "count", Count.ToString()));
            if (!string.IsNullOrEmpty(MaterialSymbol))
                node.Attributes.Append(Attr(doc, "material", MaterialSymbol));
            parent.AppendChild(node);
            foreach (var i in Inputs) i.Write(doc, node);
            var p = doc.CreateElement("p");
            p.InnerText = RemappedIndices != null ? string.Join(" ", RemappedIndices) : "";
            node.AppendChild(p);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaInput
    {
        public SemanticType Semantic { get; set; }
        public string Source { get; set; } = "";
        public int Offset { get; set; }
        public int Set { get; set; } = -1;

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("input");
            node.Attributes.Append(Attr(doc, "semantic", Semantic.ToString()));
            node.Attributes.Append(Attr(doc, "source", Source));
            node.Attributes.Append(Attr(doc, "offset", Offset.ToString()));
            if (Set >= 0) node.Attributes.Append(Attr(doc, "set", Set.ToString()));
            parent.AppendChild(node);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaMaterial
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string EffectUrl { get; set; } = "";

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("material");
            node.Attributes.Append(Attr(doc, "id", Id));
            if (!string.IsNullOrEmpty(Name)) node.Attributes.Append(Attr(doc, "name", Name));
            parent.AppendChild(node);
            var inst = doc.CreateElement("instance_effect");
            inst.Attributes.Append(Attr(doc, "url", EffectUrl));
            node.AppendChild(inst);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaImage
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string InitFrom { get; set; } = "";

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("image");
            node.Attributes.Append(Attr(doc, "id", Id));
            node.Attributes.Append(Attr(doc, "name", Name));
            parent.AppendChild(node);
            var init = doc.CreateElement("init_from");
            init.InnerText = InitFrom;
            node.AppendChild(init);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaEffect
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string SurfaceSid { get; set; } = "";
        public string SamplerSid { get; set; } = "";
        public string ImageId { get; set; } = "";

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("effect");
            node.Attributes.Append(Attr(doc, "id", Id));
            node.Attributes.Append(Attr(doc, "name", Name));
            parent.AppendChild(node);

            var profile = doc.CreateElement("profile_COMMON");
            node.AppendChild(profile);

            var np1 = doc.CreateElement("newparam"); np1.Attributes.Append(Attr(doc, "sid", SurfaceSid)); profile.AppendChild(np1);
            var surface = doc.CreateElement("surface"); surface.Attributes.Append(Attr(doc, "type", "2D")); np1.AppendChild(surface);
            var initFrom = doc.CreateElement("init_from"); initFrom.InnerText = ImageId; surface.AppendChild(initFrom);

            var np2 = doc.CreateElement("newparam"); np2.Attributes.Append(Attr(doc, "sid", SamplerSid)); profile.AppendChild(np2);
            var sampler = doc.CreateElement("sampler2D"); np2.AppendChild(sampler);
            var src = doc.CreateElement("source"); src.InnerText = SurfaceSid; sampler.AppendChild(src);

            var technique = doc.CreateElement("technique"); technique.Attributes.Append(Attr(doc, "sid", "COMMON")); profile.AppendChild(technique);
            var phong = doc.CreateElement("phong"); technique.AppendChild(phong);
            var diffuse = doc.CreateElement("diffuse"); phong.AppendChild(diffuse);
            var texture = doc.CreateElement("texture");
            texture.Attributes.Append(Attr(doc, "texture", SamplerSid));
            texture.Attributes.Append(Attr(doc, "texcoord", "CHANNEL0"));
            diffuse.AppendChild(texture);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaController
    {
        public string Id { get; set; } = "";
        public ColladaSkin Skin { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("controller");
            node.Attributes.Append(Attr(doc, "id", Id));
            parent.AppendChild(node);
            Skin.Write(doc, node);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaSkin
    {
        public string Source { get; set; } = "";
        public Matrix4 BindShapeMatrix { get; set; } = Matrix4.Identity;
        public List<ColladaSource> Sources { get; set; } = new();
        public ColladaJoints Joints { get; set; } = new();
        public ColladaVertexWeights VertexWeights { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("skin");
            node.Attributes.Append(Attr(doc, "source", Source));
            parent.AppendChild(node);
            var matrix = doc.CreateElement("bind_shape_matrix");
            matrix.InnerText = FmtMat(BindShapeMatrix);
            node.AppendChild(matrix);
            foreach (var s in Sources) s.Write(doc, node);
            Joints.Write(doc, node);
            VertexWeights.Write(doc, node);
        }

        internal static string FmtMat(Matrix4 m)
        {
            return $"{Fmt(m.M11)} {Fmt(m.M21)} {Fmt(m.M31)} {Fmt(m.M41)} " +
                   $"{Fmt(m.M12)} {Fmt(m.M22)} {Fmt(m.M32)} {Fmt(m.M42)} " +
                   $"{Fmt(m.M13)} {Fmt(m.M23)} {Fmt(m.M33)} {Fmt(m.M43)} " +
                   $"{Fmt(m.M14)} {Fmt(m.M24)} {Fmt(m.M34)} {Fmt(m.M44)}";
        }

        private static string Fmt(float f) => f.ToString("G", CultureInfo.InvariantCulture);
        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaJoints
    {
        public List<ColladaInput> Inputs { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("joints");
            parent.AppendChild(node);
            foreach (var i in Inputs) i.Write(doc, node);
        }
    }

    public class ColladaVertexWeights
    {
        public int Count { get; set; }
        public List<ColladaInput> Inputs { get; set; } = new();
        public int[]? VCount { get; set; }
        public int[]? V { get; set; }

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("vertex_weights");
            node.Attributes.Append(Attr(doc, "count", Count.ToString()));
            parent.AppendChild(node);
            foreach (var i in Inputs) i.Write(doc, node);
            var vc = doc.CreateElement("vcount"); vc.InnerText = VCount != null ? string.Join(" ", VCount) : ""; node.AppendChild(vc);
            var v = doc.CreateElement("v"); v.InnerText = V != null ? string.Join(" ", V) : ""; node.AppendChild(v);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }

    public class ColladaNode
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string NodeType { get; set; } = "NODE";
        public Matrix4 Transform { get; set; } = Matrix4.Identity;

        public string InstanceType { get; set; } = "";
        public string InstanceUrl { get; set; } = "";
        public string MaterialSymbol { get; set; } = "";
        public string MaterialTarget { get; set; } = "";
        public string SkeletonRootId { get; set; } = "";

        public List<ColladaNode> Children { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("node");
            node.Attributes.Append(Attr(doc, "id", Id));
            node.Attributes.Append(Attr(doc, "name", Name));
            node.Attributes.Append(Attr(doc, "type", NodeType));
            if (NodeType == "JOINT") node.Attributes.Append(Attr(doc, "sid", Name));
            parent.AppendChild(node);

            var matrix = doc.CreateElement("matrix");
            matrix.Attributes.Append(Attr(doc, "sid", "transform"));
            matrix.InnerText = ColladaSkin.FmtMat(Transform);
            node.AppendChild(matrix);

            if (!string.IsNullOrEmpty(InstanceType))
            {
                var inst = doc.CreateElement(InstanceType);
                inst.Attributes.Append(Attr(doc, "url", InstanceUrl));
                node.AppendChild(inst);

                if (InstanceType == "instance_controller" && !string.IsNullOrEmpty(SkeletonRootId))
                {
                    var skel = doc.CreateElement("skeleton");
                    skel.InnerText = "#" + SkeletonRootId;
                    inst.AppendChild(skel);
                }

                if (!string.IsNullOrEmpty(MaterialSymbol) && !string.IsNullOrEmpty(MaterialTarget))
                {
                    var bindMat = doc.CreateElement("bind_material"); inst.AppendChild(bindMat);
                    var tc = doc.CreateElement("technique_common"); bindMat.AppendChild(tc);
                    var im = doc.CreateElement("instance_material");
                    im.Attributes.Append(Attr(doc, "symbol", MaterialSymbol));
                    im.Attributes.Append(Attr(doc, "target", MaterialTarget));
                    tc.AppendChild(im);
                    var bvi = doc.CreateElement("bind_vertex_input");
                    bvi.Attributes.Append(Attr(doc, "semantic", "CHANNEL0"));
                    bvi.Attributes.Append(Attr(doc, "input_semantic", "TEXCOORD"));
                    bvi.Attributes.Append(Attr(doc, "input_set", "0"));
                    im.AppendChild(bvi);
                }
            }

            foreach (var child in Children) child.Write(doc, node);
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v) { var a = doc.CreateAttribute(n); a.Value = v; return a; }
    }
}
