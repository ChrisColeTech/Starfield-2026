using OpenTK.Mathematics;
using MiniToolbox.Trpak.Flatbuffers.TR.Model;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MiniToolbox.Trpak.Decoders
{
    /// <summary>
    /// Data-only skeleton/armature decoded from TRSKL.
    /// Ported from gftool Armature.cs — all GL rendering code removed.
    /// </summary>
    public class TrinityArmature
    {
        public class Bone
        {
            public string Name { get; private set; }
            public Vector3 RestPosition { get; private set; }
            public Quaternion RestRotation { get; private set; }
            public Vector3 RestScale { get; private set; }
            public Vector3 RestEuler { get; private set; }
            public Matrix4 RestLocalMatrix { get; private set; }
            public Matrix4 RestInvParentMatrix { get; set; } = Matrix4.Identity;
            public Matrix4 InverseBindWorld { get; set; } = Matrix4.Identity;
            public Matrix4 JointInverseBindWorld { get; set; } = Matrix4.Identity;
            public bool HasJointInverseBind { get; set; }
            public bool UseSegmentScaleCompensate { get; set; }
            public int ParentIndex { get; set; }
            public bool Skinning { get; set; }
            public Bone? Parent { get; set; }
            public List<Bone> Children { get; } = new();

            // Mutable pose (used by animation)
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
            public Vector3 Scale { get; set; }

            public Bone(TRTransformNode node, bool skinning)
            {
                Name = node.Name;
                Position = new Vector3(node.Transform.Translate.X, node.Transform.Translate.Y, node.Transform.Translate.Z);
                RestEuler = new Vector3(node.Transform.Rotate.X, node.Transform.Rotate.Y, node.Transform.Rotate.Z);
                Rotation = FromEulerXYZ(RestEuler);
                Scale = new Vector3(node.Transform.Scale.X, node.Transform.Scale.Y, node.Transform.Scale.Z);
                RestPosition = Position;
                RestRotation = Rotation;
                RestScale = Scale;
                RestLocalMatrix = Matrix4.CreateScale(RestScale)
                                 * Matrix4.CreateFromQuaternion(RestRotation)
                                 * Matrix4.CreateTranslation(RestPosition);
                ParentIndex = node.ParentNodeIndex;
                Skinning = skinning;
                HasJointInverseBind = false;
                UseSegmentScaleCompensate = false;
            }

            public void AddChild(Bone bone)
            {
                bone.Parent = this;
                Children.Add(bone);
            }

            public void ResetPose()
            {
                Position = RestPosition;
                Rotation = RestRotation;
                Scale = RestScale;
            }

            private static Quaternion FromEulerXYZ(Vector3 euler)
            {
                var qx = Quaternion.FromAxisAngle(Vector3.UnitX, euler.X);
                var qy = Quaternion.FromAxisAngle(Vector3.UnitY, euler.Y);
                var qz = Quaternion.FromAxisAngle(Vector3.UnitZ, euler.Z);
                var q = qz * qy * qx;
                q.Normalize();
                return q;
            }
        }

        public List<Bone> Bones { get; } = new();
        public IReadOnlyList<int> ParentIndices => _parentIndices;
        private readonly List<int> _parentIndices = new();
        private int[] _jointInfoToNode = Array.Empty<int>();
        public int JointInfoCount => _jointInfoToNode.Length;
        public int BoneMetaCount => 0;
        private readonly int _skinningPaletteOffset;

        public TrinityArmature(TRSKL skel, string? sourcePath = null, bool useTrsklInverseBind = true)
        {
            _skinningPaletteOffset = skel.SkinningPaletteOffset;

            foreach (var transNode in skel.TransformNodes)
            {
                var bone = new Bone(transNode, skinning: false);
                Bones.Add(bone);
                _parentIndices.Add(transNode.ParentNodeIndex);
            }

            ApplyJointInfoFromTrskl(skel);
            ApplyJointInfoFromJson(sourcePath);

            for (int i = 0; i < Bones.Count; i++)
            {
                var parentIndex = Bones[i].ParentIndex;
                if (parentIndex >= 0 && parentIndex < Bones.Count && parentIndex != i)
                {
                    Bones[parentIndex].AddChild(Bones[i]);
                }
            }

            UpdateRestParentMatrices();
            ComputeInverseBindMatrices(useTrsklInverseBind);
        }

        #region Joint Info

        private void ApplyJointInfoFromTrskl(TRSKL skel)
        {
            if (skel.JointInfos == null || skel.JointInfos.Length == 0 || skel.TransformNodes == null || skel.TransformNodes.Length == 0)
                return;

            _jointInfoToNode = new int[skel.JointInfos.Length];
            for (int i = 0; i < _jointInfoToNode.Length; i++)
                _jointInfoToNode[i] = -1;

            int count = Math.Min(Bones.Count, skel.TransformNodes.Length);
            for (int i = 0; i < count; i++)
            {
                var node = skel.TransformNodes[i];
                int jointId = node.JointInfoIndex;
                if (jointId < 0 || jointId >= skel.JointInfos.Length)
                    continue;

                _jointInfoToNode[jointId] = i;
                ApplyTrsklJointInfoToBone(Bones[i], skel.JointInfos[jointId]);
            }
        }

        private static void ApplyTrsklJointInfoToBone(Bone bone, TRJointInfo joint)
        {
            bone.UseSegmentScaleCompensate = joint.SegmentScaleCompensate;
            bone.Skinning = joint.InfluenceSkinning;

            if (joint.InverseBindPoseMatrix != null)
            {
                bone.JointInverseBindWorld = CreateMatrixFromAxis(
                    new Vector3(joint.InverseBindPoseMatrix.AxisX.X, joint.InverseBindPoseMatrix.AxisX.Y, joint.InverseBindPoseMatrix.AxisX.Z),
                    new Vector3(joint.InverseBindPoseMatrix.AxisY.X, joint.InverseBindPoseMatrix.AxisY.Y, joint.InverseBindPoseMatrix.AxisY.Z),
                    new Vector3(joint.InverseBindPoseMatrix.AxisZ.X, joint.InverseBindPoseMatrix.AxisZ.Y, joint.InverseBindPoseMatrix.AxisZ.Z),
                    new Vector3(joint.InverseBindPoseMatrix.AxisW.X, joint.InverseBindPoseMatrix.AxisW.Y, joint.InverseBindPoseMatrix.AxisW.Z));
                bone.HasJointInverseBind = true;
            }
        }

        #endregion

        #region Matrix Computation

        private void UpdateRestParentMatrices()
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                var bone = Bones[i];
                if (bone.ParentIndex >= 0 && bone.ParentIndex < Bones.Count && bone.ParentIndex != i)
                    bone.RestInvParentMatrix = Matrix4.Invert(Bones[bone.ParentIndex].RestLocalMatrix);
                else
                    bone.RestInvParentMatrix = Matrix4.Identity;
            }
        }

        public Matrix4[] GetWorldMatrices()
        {
            var world = new Matrix4[Bones.Count];
            var computed = new bool[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
                world[i] = ComputeWorldMatrix(i, computed, world);
            return world;
        }

        private Matrix4 ComputeWorldMatrix(int index, bool[] computed, Matrix4[] world)
        {
            if (computed[index])
                return world[index];

            var bone = Bones[index];
            var local = Matrix4.CreateScale(bone.Scale)
                        * Matrix4.CreateFromQuaternion(bone.Rotation)
                        * Matrix4.CreateTranslation(bone.Position);

            if (bone.ParentIndex >= 0 && bone.ParentIndex < Bones.Count && bone.ParentIndex != index)
            {
                if (bone.UseSegmentScaleCompensate)
                {
                    var parent = Bones[bone.ParentIndex];
                    local *= Matrix4.CreateScale(
                        parent.Scale.X != 0f ? 1f / parent.Scale.X : 1f,
                        parent.Scale.Y != 0f ? 1f / parent.Scale.Y : 1f,
                        parent.Scale.Z != 0f ? 1f / parent.Scale.Z : 1f);
                }
                var parentWorld = ComputeWorldMatrix(bone.ParentIndex, computed, world);
                world[index] = local * parentWorld;
            }
            else
            {
                world[index] = local;
            }

            computed[index] = true;
            return world[index];
        }

        private void ComputeInverseBindMatrices(bool useTrsklInverseBind)
        {
            if (Bones.Count == 0)
                return;

            var bindWorld = new Matrix4[Bones.Count];
            var computed = new bool[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
                bindWorld[i] = ComputeBindWorld(i, useTrsklInverseBind, bindWorld, computed);

            for (int i = 0; i < Bones.Count; i++)
            {
                if (useTrsklInverseBind && Bones[i].HasJointInverseBind)
                    Bones[i].InverseBindWorld = Bones[i].JointInverseBindWorld;
                else
                    Bones[i].InverseBindWorld = Matrix4.Invert(bindWorld[i]);
            }
        }

        private Matrix4 ComputeBindWorld(int index, bool useTrsklInverseBind, Matrix4[] world, bool[] computed)
        {
            if (computed[index])
                return world[index];

            var bone = Bones[index];
            Matrix4 local;
            if (useTrsklInverseBind && bone.HasJointInverseBind)
                local = Matrix4.Invert(bone.JointInverseBindWorld);
            else
                local = bone.RestLocalMatrix;

            if (bone.ParentIndex >= 0 && bone.ParentIndex < Bones.Count && bone.ParentIndex != index)
            {
                if (bone.UseSegmentScaleCompensate)
                {
                    var parent = Bones[bone.ParentIndex];
                    local *= Matrix4.CreateScale(
                        parent.RestScale.X != 0f ? 1f / parent.RestScale.X : 1f,
                        parent.RestScale.Y != 0f ? 1f / parent.RestScale.Y : 1f,
                        parent.RestScale.Z != 0f ? 1f / parent.RestScale.Z : 1f);
                }
                var parentWorld = ComputeBindWorld(bone.ParentIndex, useTrsklInverseBind, world, computed);
                world[index] = local * parentWorld;
            }
            else
            {
                world[index] = local;
            }

            computed[index] = true;
            return world[index];
        }

        #endregion

        #region Skinning Palette / Mapping

        public int[] BuildSkinningPalette()
        {
            if (_jointInfoToNode == null || _jointInfoToNode.Length == 0)
                return Array.Empty<int>();

            var palette = new int[_jointInfoToNode.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                int nodeIndex = _jointInfoToNode[i];
                palette[i] = nodeIndex >= 0 ? nodeIndex : 0;
            }
            return palette;
        }

        public int MapJointInfoIndex(int jointInfoIndex)
        {
            if (jointInfoIndex < 0 || jointInfoIndex >= _jointInfoToNode.Length)
                return 0;
            int mapped = _jointInfoToNode[jointInfoIndex];
            return mapped >= 0 ? mapped : 0;
        }

        public int MapBoneMetaIndex(int boneMetaIndex)
        {
            return 0;
        }

        #endregion

        #region JSON Joint Info Override

        private void ApplyJointInfoFromJson(string? sourcePath)
        {
            var parseResult = LoadJointInfoFromJson(sourcePath);
            if (parseResult == null || parseResult.JointInfos.Count == 0)
                return;

            if (_jointInfoToNode == null || _jointInfoToNode.Length == 0)
            {
                _jointInfoToNode = new int[parseResult.JointInfos.Count];
                for (int i = 0; i < _jointInfoToNode.Length; i++)
                    _jointInfoToNode[i] = -1;
            }
            else if (parseResult.JointInfos.Count > _jointInfoToNode.Length)
            {
                int oldLen = _jointInfoToNode.Length;
                Array.Resize(ref _jointInfoToNode, parseResult.JointInfos.Count);
                for (int i = oldLen; i < _jointInfoToNode.Length; i++)
                    _jointInfoToNode[i] = -1;
            }

            if (parseResult.NodeNames.Length == 0 || parseResult.NodeJointInfoIds.Length == 0)
                return;

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int count = Math.Min(parseResult.NodeNames.Length, parseResult.NodeJointInfoIds.Length);
            for (int i = 0; i < count; i++)
            {
                var name = parseResult.NodeNames[i];
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                map[name] = parseResult.NodeJointInfoIds[i];
            }

            for (int i = 0; i < Bones.Count; i++)
            {
                var bone = Bones[i];
                if (!map.TryGetValue(bone.Name, out int jointId))
                    continue;

                if (jointId >= 0 && jointId < _jointInfoToNode.Length)
                    _jointInfoToNode[jointId] = i;

                ApplyJointInfoToBone(bone, parseResult, jointId);
            }
        }

        private static void ApplyJointInfoToBone(Bone bone, JointInfoParseResult parseResult, int jointId)
        {
            if (jointId < 0 || jointId >= parseResult.JointInfos.Count)
                return;

            var joint = parseResult.JointInfos[jointId];
            bone.UseSegmentScaleCompensate = joint.SegmentScaleCompensate;
            if (joint.HasInverseBind)
            {
                bone.JointInverseBindWorld = joint.InverseBind;
                bone.HasJointInverseBind = true;
            }
            bone.Skinning = joint.InfluenceSkinning;
        }

        private static JointInfoParseResult? LoadJointInfoFromJson(string? sourcePath)
        {
            var jsonPath = ResolveTrsklJsonPath(sourcePath);
            if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
                return null;

            try
            {
                var text = File.ReadAllText(jsonPath);
                text = Regex.Replace(text, "\\b([A-Za-z_][A-Za-z0-9_]*)\\b\\s*:", "\"$1\":");
                using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
                var root = doc.RootElement;

                var jointInfos = new List<JointInfoJson>();
                if (root.TryGetProperty("joint_info_list", out var jointList) &&
                    jointList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in jointList.EnumerateArray())
                    {
                        var info = new JointInfoJson
                        {
                            SegmentScaleCompensate = entry.TryGetProperty("segment_scale_compensate", out var ssc) && ssc.GetBoolean(),
                            InfluenceSkinning = !entry.TryGetProperty("influence_skinning", out var inf) || inf.GetBoolean()
                        };

                        if (entry.TryGetProperty("inverse_bind_pose_matrix", out var matrix))
                        {
                            if (TryParseAxisMatrix(matrix, out var inverse))
                            {
                                info.InverseBind = inverse;
                                info.HasInverseBind = true;
                            }
                        }

                        jointInfos.Add(info);
                    }
                }

                var nodeJointIds = Array.Empty<int>();
                var nodeNames = Array.Empty<string>();
                if (root.TryGetProperty("node_list", out var nodeList) &&
                    nodeList.ValueKind == JsonValueKind.Array)
                {
                    int cnt = nodeList.GetArrayLength();
                    nodeJointIds = new int[cnt];
                    nodeNames = new string[cnt];
                    int i = 0;
                    foreach (var node in nodeList.EnumerateArray())
                    {
                        nodeNames[i] = node.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty;
                        nodeJointIds[i] = node.TryGetProperty("joint_info_id", out var jid) ? jid.GetInt32() : -1;
                        i++;
                    }
                }

                return new JointInfoParseResult(jointInfos, nodeJointIds, nodeNames);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryParseAxisMatrix(JsonElement matrix, out Matrix4 result)
        {
            result = Matrix4.Identity;
            if (!matrix.TryGetProperty("axis_x", out var axisX) ||
                !matrix.TryGetProperty("axis_y", out var axisY) ||
                !matrix.TryGetProperty("axis_z", out var axisZ) ||
                !matrix.TryGetProperty("axis_w", out var axisW))
                return false;

            var x = ReadVector3Json(axisX);
            var y = ReadVector3Json(axisY);
            var z = ReadVector3Json(axisZ);
            var w = ReadVector3Json(axisW);
            result = CreateMatrixFromAxis(x, y, z, w);
            return true;
        }

        private static Vector3 ReadVector3Json(JsonElement element)
        {
            float x = element.TryGetProperty("x", out var vx) ? vx.GetSingle() : 0f;
            float y = element.TryGetProperty("y", out var vy) ? vy.GetSingle() : 0f;
            float z = element.TryGetProperty("z", out var vz) ? vz.GetSingle() : 0f;
            return new Vector3(x, y, z);
        }

        private static string? ResolveTrsklJsonPath(string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;

            var dir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            var baseName = Path.GetFileNameWithoutExtension(sourcePath);
            var candidate = Path.Combine(dir, $"{baseName}.trskl.json");
            if (File.Exists(candidate))
                return candidate;

            candidate = Path.Combine(dir, $"{baseName}.json");
            if (File.Exists(candidate))
                return candidate;

            return null;
        }

        #endregion

        #region Helpers

        private static Matrix4 CreateMatrixFromAxis(Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 axisW)
        {
            return new Matrix4(
                axisX.X, axisX.Y, axisX.Z, 0f,
                axisY.X, axisY.Y, axisY.Z, 0f,
                axisZ.X, axisZ.Y, axisZ.Z, 0f,
                axisW.X, axisW.Y, axisW.Z, 1f);
        }

        private sealed class JointInfoParseResult
        {
            public JointInfoParseResult(List<JointInfoJson> jointInfos, int[] nodeJointInfoIds, string[] nodeNames)
            {
                JointInfos = jointInfos;
                NodeJointInfoIds = nodeJointInfoIds;
                NodeNames = nodeNames;
            }

            public List<JointInfoJson> JointInfos { get; }
            public int[] NodeJointInfoIds { get; }
            public string[] NodeNames { get; }
        }

        private struct JointInfoJson
        {
            public bool SegmentScaleCompensate;
            public bool InfluenceSkinning;
            public bool HasInverseBind;
            public Matrix4 InverseBind;
        }

        #endregion
    }
}
