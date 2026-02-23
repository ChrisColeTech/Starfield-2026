#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Starfield2026.ModelLoader.DTOs;
using static Starfield2026.ModelLoader.Loaders.ColladaHelpers;

namespace Starfield2026.ModelLoader.Loaders;

public static class SkeletonLoader
{
    public static Skeleton Load(string daePath)
    {
        var doc = XDocument.Load(daePath);
        var bones = new List<Bone>();
        var visualScene = doc.Root!.Descendants(Col + "visual_scene").FirstOrDefault();
        if (visualScene != null)
            ParseJoints(visualScene, bones, -1);
        return new Skeleton(bones);
    }

    private static void ParseJoints(XElement parent, List<Bone> bones, int parentIndex)
    {
        foreach (var node in parent.Elements(Col + "node"))
        {
            string? type = node.Attribute("type")?.Value;
            if (type != "JOINT") continue;

            string name = node.Attribute("name")?.Value ?? "";
            string nodeId = node.Attribute("id")?.Value ?? name;
            var transform = ReadNodeTransform(node);

            int index = bones.Count;
            bones.Add(new Bone(index, name, nodeId, parentIndex, transform));

            ParseJoints(node, bones, index);
        }
    }
}
