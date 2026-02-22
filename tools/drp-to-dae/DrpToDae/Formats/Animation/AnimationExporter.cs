using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using VbnSkeleton = DrpToDae.Formats.VBN.VBN;
using VbnBone = DrpToDae.Formats.VBN.Bone;

namespace DrpToDae.Formats.Animation
{
    public static class AnimationExporter
    {
        public static void Export(string inputPath, string outputFolder)
        {
            Export(inputPath, outputFolder, null);
        }

        public static void Export(string inputPath, string outputFolder, VbnSkeleton? skeleton)
        {
            Directory.CreateDirectory(outputFolder);

            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            AnimationData animation = OMOReader.Read(inputPath);

            // Resolve bone hashes to VBN bone names so animation targets match model.dae
            if (skeleton != null)
                ResolveBoneNames(animation, skeleton);

            string jsonPath = Path.Combine(outputFolder, $"{baseName}.json");
            ExportToJson(animation, jsonPath);

            string daePath = Path.Combine(outputFolder, $"{baseName}_anim.dae");
            ExportToCollada(animation, daePath, skeleton);
        }

        private static void ResolveBoneNames(AnimationData animation, VbnSkeleton skeleton)
        {
            // Build hash→name lookup from VBN bone IDs (stored directly in VBN file)
            var idToName = new Dictionary<uint, string>();
            foreach (var bone in skeleton.Bones)
            {
                idToName[bone.BoneId] = bone.Name;
            }

            int resolved = 0, unresolved = 0;
            foreach (var bone in animation.Bones)
            {
                // bone.Hash == -1 means "no hash" sentinel; all other values (including those
                // with the high bit set) are valid 32-bit bone IDs from the OMO file.
                if (bone.Hash != -1 && idToName.TryGetValue((uint)bone.Hash, out string? name))
                {
                    bone.Name = name;
                    resolved++;
                }
                else
                {
                    unresolved++;
                }
            }
        }


        public static void ExportToJson(AnimationData animation, string outputPath)
        {
            var data = new
            {
                name = animation.Name,
                frameCount = animation.FrameCount,
                bones = new List<object>()
            };

            foreach (var bone in animation.Bones)
            {
                var boneData = new
                {
                    name = bone.Name,
                    hash = bone.Hash,
                    boneIndex = bone.BoneIndex,
                    rotationType = bone.RotationType.ToString(),
                    position = bone.HasPositionAnimation ? new
                    {
                        x = ExportKeyGroup(bone.XPos),
                        y = ExportKeyGroup(bone.YPos),
                        z = ExportKeyGroup(bone.ZPos)
                    } : null,
                    rotation = bone.HasRotationAnimation ? new
                    {
                        x = ExportKeyGroup(bone.XRot),
                        y = ExportKeyGroup(bone.YRot),
                        z = ExportKeyGroup(bone.ZRot),
                        w = ExportKeyGroup(bone.WRot)
                    } : null,
                    scale = bone.HasScaleAnimation ? new
                    {
                        x = ExportKeyGroup(bone.XScale),
                        y = ExportKeyGroup(bone.YScale),
                        z = ExportKeyGroup(bone.ZScale)
                    } : null
                };
                data.bones.Add(boneData);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(outputPath, json);
        }

        private static List<object> ExportKeyGroup(KeyGroup group)
        {
            var keys = new List<object>();
            foreach (var key in group.Keys)
            {
                keys.Add(new
                {
                    frame = key.Frame,
                    value = key.Value,
                    interpolation = key.InterpolationType.ToString()
                });
            }
            return keys;
        }

        public static void ExportToCollada(AnimationData animation, string outputPath)
        {
            ExportToCollada(animation, outputPath, null);
        }

        public static void ExportToCollada(AnimationData animation, string outputPath, VbnSkeleton? skeleton)
        {
            XNamespace ns = "http://www.collada.org/2005/11/COLLADASchema";

            // Full COLLADA scene with JOINT nodes + matrix-based animation
            // Matches the format Blender's OpenCOLLADA importer expects
            var root = new XElement(ns + "COLLADA",
                new XAttribute("version", "1.4.1"),
                CreateAsset(),
                CreateLibraryAnimations(animation, ns),
                CreateLibraryVisualScenes(animation, ns, skeleton),
                CreateScene(ns)
            );

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                root
            );

            doc.Save(outputPath);
        }

        private static string GetBoneId(KeyNode bone)
        {
            return !string.IsNullOrEmpty(bone.Name) ? $"{bone.Name}_id" : $"bone_{bone.Hash:X}";
        }

        private static XElement CreateAsset()
        {
            return new XElement("asset",
                new XElement("contributor",
                    new XElement("authoring_tool", "DrpToDae Animation Exporter")
                ),
                new XElement("created", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                new XElement("modified", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                new XElement("up_axis", "Y_UP")
            );
        }

        private static XElement CreateLibraryAnimations(AnimationData animation, XNamespace ns)
        {
            var animations = new List<XElement>();

            foreach (var bone in animation.Bones)
            {
                if (!bone.HasPositionAnimation && !bone.HasRotationAnimation && !bone.HasScaleAnimation)
                    continue;

                string boneId = GetBoneId(bone);
                string animId = $"{boneId}_transform";

                // Time source
                var timeValues = new StringBuilder();
                for (int i = 0; i < animation.FrameCount; i++)
                    timeValues.Append($"{(i / 30f).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} ");

                // Matrix output source — 16 floats per frame
                var matrixValues = new StringBuilder();
                for (int i = 0; i < animation.FrameCount; i++)
                {
                    float[] mat = ComputeTransformMatrix(bone, i);
                    for (int m = 0; m < 16; m++)
                        matrixValues.Append($"{mat[m].ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} ");
                }

                // Interpolation source
                var interpValues = new StringBuilder();
                for (int i = 0; i < animation.FrameCount; i++)
                    interpValues.Append("LINEAR ");

                string inputId = $"{animId}_input";
                string outputId = $"{animId}_output";
                string interpId = $"{animId}_interp";

                var animElement = new XElement("animation",
                    new XAttribute("id", animId),
                    new XAttribute("name", $"{bone.Name}_transform"),

                    // Time input source
                    new XElement("source",
                        new XAttribute("id", inputId),
                        new XElement("float_array",
                            new XAttribute("id", $"{inputId}-array"),
                            new XAttribute("count", animation.FrameCount),
                            timeValues.ToString().Trim()),
                        new XElement("technique_common",
                            new XElement("accessor",
                                new XAttribute("source", $"#{inputId}-array"),
                                new XAttribute("count", animation.FrameCount),
                                new XAttribute("stride", 1),
                                new XElement("param", new XAttribute("name", "TIME"), new XAttribute("type", "float"))
                            )
                        )
                    ),

                    // Matrix output source
                    new XElement("source",
                        new XAttribute("id", outputId),
                        new XElement("float_array",
                            new XAttribute("id", $"{outputId}-array"),
                            new XAttribute("count", animation.FrameCount * 16),
                            matrixValues.ToString().Trim()),
                        new XElement("technique_common",
                            new XElement("accessor",
                                new XAttribute("source", $"#{outputId}-array"),
                                new XAttribute("count", animation.FrameCount),
                                new XAttribute("stride", 16),
                                new XElement("param", new XAttribute("name", "TRANSFORM"), new XAttribute("type", "float4x4"))
                            )
                        )
                    ),

                    // Interpolation source
                    new XElement("source",
                        new XAttribute("id", interpId),
                        new XElement("Name_array",
                            new XAttribute("id", $"{interpId}-array"),
                            new XAttribute("count", animation.FrameCount),
                            interpValues.ToString().Trim()),
                        new XElement("technique_common",
                            new XElement("accessor",
                                new XAttribute("source", $"#{interpId}-array"),
                                new XAttribute("count", animation.FrameCount),
                                new XAttribute("stride", 1),
                                new XElement("param", new XAttribute("name", "INTERPOLATION"), new XAttribute("type", "name"))
                            )
                        )
                    ),

                    // Sampler
                    new XElement("sampler",
                        new XAttribute("id", $"{animId}_sampler"),
                        new XElement("input", new XAttribute("semantic", "INPUT"), new XAttribute("source", $"#{inputId}")),
                        new XElement("input", new XAttribute("semantic", "OUTPUT"), new XAttribute("source", $"#{outputId}")),
                        new XElement("input", new XAttribute("semantic", "INTERPOLATION"), new XAttribute("source", $"#{interpId}"))
                    ),

                    // Channel — targets the bone node via its sid (= bone name, not node id)
                    // COLLADA animation targets use the sid path: "BoneName/transform"
                    // The node sid matches bone.Name, NOT the boneId (which has _id suffix)
                    new XElement("channel",
                        new XAttribute("source", $"#{animId}_sampler"),
                        new XAttribute("target", $"{bone.Name}/transform")
                    )
                );

                animations.Add(animElement);
            }

            return new XElement("library_animations", animations);
        }

        /// <summary>
        /// Computes a 4x4 column-major transform matrix from the bone's animation at the given frame.
        /// Matrix = Translation * Rotation * Scale
        /// </summary>
        private static float[] ComputeTransformMatrix(KeyNode bone, int frame)
        {
            float tx = bone.XPos.HasAnimation ? bone.XPos.GetValue(frame) : 0;
            float ty = bone.YPos.HasAnimation ? bone.YPos.GetValue(frame) : 0;
            float tz = bone.ZPos.HasAnimation ? bone.ZPos.GetValue(frame) : 0;

            float sx = bone.XScale.HasAnimation ? bone.XScale.GetValue(frame) : 1;
            float sy = bone.YScale.HasAnimation ? bone.YScale.GetValue(frame) : 1;
            float sz = bone.ZScale.HasAnimation ? bone.ZScale.GetValue(frame) : 1;

            // Build rotation matrix from quaternion
            float qx = bone.XRot.HasAnimation ? bone.XRot.GetValue(frame) : 0;
            float qy = bone.YRot.HasAnimation ? bone.YRot.GetValue(frame) : 0;
            float qz = bone.ZRot.HasAnimation ? bone.ZRot.GetValue(frame) : 0;
            float qw = bone.WRot.HasAnimation ? bone.WRot.GetValue(frame) : 1;

            // Normalize quaternion
            float len = (float)Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
            if (len > 0.0001f) { qx /= len; qy /= len; qz /= len; qw /= len; }

            // Quaternion to rotation matrix components
            float x2 = qx + qx, y2 = qy + qy, z2 = qz + qz;
            float xx = qx * x2, xy = qx * y2, xz = qx * z2;
            float yy = qy * y2, yz = qy * z2, zz = qz * z2;
            float wx = qw * x2, wy = qw * y2, wz = qw * z2;

            // Build rotation matrix with scale applied
            // Matrix = T * R * S
            float r00 = (1 - yy - zz) * sx;
            float r01 = (xy - wz) * sy;
            float r02 = (xz + wy) * sz;
            float r10 = (xy + wz) * sx;
            float r11 = (1 - xx - zz) * sy;
            float r12 = (yz - wx) * sz;
            float r20 = (xz - wy) * sx;
            float r21 = (yz + wx) * sy;
            float r22 = (1 - xx - yy) * sz;

            // COLLADA uses column-major ordering (same as ColladaExporter.FormatMatrix)
            // Output: col0, col1, col2, col3
            return new float[]
            {
                r00, r10, r20, 0,   // column 0
                r01, r11, r21, 0,   // column 1
                r02, r12, r22, 0,   // column 2
                tx,  ty,  tz,  1    // column 3 (translation)
            };
        }

        private static XElement CreateLibraryVisualScenes(AnimationData animation, XNamespace ns, VbnSkeleton? skeleton)
        {
            var rootNodes = new List<XElement>();

            if (skeleton != null && skeleton.Bones.Count > 0)
            {
                // Build proper nested hierarchy from VBN skeleton
                // Find root bones (ParentIndex < 0 or ParentIndex >= Bones.Count)
                foreach (var bone in skeleton.Bones)
                {
                    if (bone.ParentIndex < 0 || bone.ParentIndex >= skeleton.Bones.Count)
                    {
                        // This is a root bone - recursively build its subtree
                        rootNodes.Add(CreateBoneNodeRecursive(bone, animation));
                    }
                }

                // Fallback: if no root bones found, use first bone as root
                if (rootNodes.Count == 0 && skeleton.Bones.Count > 0)
                {
                    rootNodes.Add(CreateBoneNodeRecursive(skeleton.Bones[0], animation));
                }
            }
            else
            {
                // Fallback: flat list if no skeleton provided (legacy behavior)
                foreach (var keyNode in animation.Bones)
                {
                    string boneId = GetBoneId(keyNode);
                    var node = new XElement("node",
                        new XAttribute("id", boneId),
                        new XAttribute("name", keyNode.Name),
                        new XAttribute("type", "JOINT"),
                        new XAttribute("sid", keyNode.Name),
                        new XElement("matrix",
                            new XAttribute("sid", "transform"),
                            "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1")
                    );
                    rootNodes.Add(node);
                }
            }

            return new XElement("library_visual_scenes",
                new XElement("visual_scene",
                    new XAttribute("id", "Scene"),
                    new XAttribute("name", "Scene"),
                    rootNodes
                )
            );
        }

        private static XElement CreateBoneNodeRecursive(VbnBone vbnBone, AnimationData animation)
        {
            string boneId = vbnBone.Name + "_id";

            // Compute local transform from bone's local TRS components
            var localTransform = ComputeLocalTransform(vbnBone);
            string matrixStr = FormatMatrix(localTransform);

            var node = new XElement("node",
                new XAttribute("id", boneId),
                new XAttribute("name", vbnBone.Name),
                new XAttribute("type", "JOINT"),
                new XAttribute("sid", vbnBone.Name),
                new XElement("matrix",
                    new XAttribute("sid", "transform"),
                    matrixStr)
            );

            // Recursively add children in nested structure
            foreach (var child in vbnBone.Children)
            {
                node.Add(CreateBoneNodeRecursive(child, animation));
            }

            return node;
        }

        private static Matrix4x4 ComputeLocalTransform(VbnBone bone)
        {
            var scale = Matrix4x4.CreateScale(bone.Sca);
            var rotation = Matrix4x4.CreateFromQuaternion(bone.Rot);
            var translation = Matrix4x4.CreateTranslation(bone.Pos);
            return scale * rotation * translation;
        }

        private static string FormatMatrix(Matrix4x4 m)
        {
            // COLLADA uses column-major ordering (same as ColladaExporter)
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.######} {1:0.######} {2:0.######} {3:0.######} " +
                "{4:0.######} {5:0.######} {6:0.######} {7:0.######} " +
                "{8:0.######} {9:0.######} {10:0.######} {11:0.######} " +
                "{12:0.######} {13:0.######} {14:0.######} {15:0.######}",
                m.M11, m.M21, m.M31, m.M41,
                m.M12, m.M22, m.M32, m.M42,
                m.M13, m.M23, m.M33, m.M43,
                m.M14, m.M24, m.M34, m.M44);
        }

        private static XElement CreateScene(XNamespace ns)
        {
            return new XElement("scene",
                new XElement("instance_visual_scene",
                    new XAttribute("url", "#Scene")
                )
            );
        }
    }
}
