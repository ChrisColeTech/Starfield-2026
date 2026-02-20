using OpenTK.Mathematics;
using MiniToolbox.Trpak.Flatbuffers.TR.Model;
using MiniToolbox.Trpak.Flatbuffers.Gfx2;
using MiniToolbox.Core.Utils;

namespace MiniToolbox.Trpak.Decoders
{
    /// <summary>
    /// Headless Trinity model decoder. Parses TRMDL → mesh, skeleton, materials.
    /// Ported from gftool Model.cs — all GL rendering code removed.
    /// </summary>
    public class TrinityModelDecoder
    {
        private enum BlendIndexRemapMode
        {
            None,
            BoneWeights,
            JointInfo,
            SkinningPalette,
            BoneMeta
        }

        #region Export Data Types

        public sealed class ExportSubmesh
        {
            public required string Name { get; init; }
            public required string MaterialName { get; init; }
            public required Vector3[] Positions { get; init; }
            public required Vector3[] Normals { get; init; }
            public required Vector2[] UVs { get; init; }
            public required Vector4[] Colors { get; init; }
            public required Vector4[] Tangents { get; init; }
            public required Vector3[] Binormals { get; init; }
            public required Vector4[] BlendIndices { get; init; }
            public required Vector4[] BlendWeights { get; init; }
            public required uint[] Indices { get; init; }
            public required bool HasVertexColors { get; init; }
            public required bool HasTangents { get; init; }
            public required bool HasBinormals { get; init; }
            public required bool HasSkinning { get; init; }
        }

        public sealed class ExportData
        {
            public required string Name { get; init; }
            public required IReadOnlyList<ExportSubmesh> Submeshes { get; init; }
            public required TrinityArmature? Armature { get; init; }
            public required IReadOnlyList<TrinityMaterial> Materials { get; init; }
        }

        #endregion

        #region Fields

        private PathString _modelPath;
        private string? _baseSkeletonCategoryHint;

        public string Name { get; private set; }

        private List<Vector3[]> Positions = new();
        private List<Vector3[]> Normals = new();
        private List<Vector2[]> UVs = new();
        private List<Vector4[]> Colors = new();
        private List<Vector4[]> Tangents = new();
        private List<Vector3[]> Binormals = new();
        private List<Vector4[]> BlendIndicies = new();
        private List<Vector4[]> BlendWeights = new();
        private List<TRBoneWeight[]?> BlendBoneWeights = new();
        private List<Vector4[]> BlendIndiciesOriginal = new();
        private List<string> BlendMeshNames = new();

        private List<uint[]> Indices = new();
        private List<bool> HasVertexColors = new();
        private List<bool> HasTangents = new();
        private List<bool> HasBinormals = new();
        private List<bool> HasSkinning = new();

        private TrinityMaterial[]? _materials;
        private List<string> MaterialNames = new();
        private List<string> SubmeshNames = new();

        private TrinityArmature? _armature;
        public TrinityArmature? Armature => _armature;

        private BlendIndexStats? _blendIndexStats;

        #endregion

        #region Constructor

        public TrinityModelDecoder(string modelFile, bool loadAllLods = false)
        {
            Name = Path.GetFileNameWithoutExtension(modelFile);
            _modelPath = new PathString(modelFile);

            var mdl = FlatBufferConverter.DeserializeFrom<TRMDL>(modelFile);

            // Meshes
            if (loadAllLods)
            {
                foreach (var mesh in mdl.Meshes)
                    ParseMesh(_modelPath.Combine(mesh.PathName));
            }
            else
            {
                var mesh = mdl.Meshes[0]; // LOD0
                ParseMesh(_modelPath.Combine(mesh.PathName));
            }

            _baseSkeletonCategoryHint = GuessBaseSkeletonCategoryFromMesh(
                mdl.Meshes != null && mdl.Meshes.Length > 0 ? mdl.Meshes[0].PathName : null);

            // Materials
            foreach (var mat in mdl.Materials)
                ParseMaterial(_modelPath.Combine(mat));

            // Skeleton
            if (mdl.Skeleton != null)
                ParseArmature(_modelPath.Combine(mdl.Skeleton.PathName));
        }

        #endregion

        #region Export

        public ExportData CreateExportData()
        {
            var subs = new List<ExportSubmesh>(Positions.Count);
            int count = Positions.Count;
            for (int i = 0; i < count; i++)
            {
                string submeshName = i < SubmeshNames.Count ? SubmeshNames[i] : $"Submesh {i}";
                string materialName = i < MaterialNames.Count ? MaterialNames[i] : string.Empty;
                subs.Add(new ExportSubmesh
                {
                    Name = submeshName,
                    MaterialName = materialName,
                    Positions = Positions[i],
                    Normals = i < Normals.Count ? Normals[i] : Array.Empty<Vector3>(),
                    UVs = i < UVs.Count ? UVs[i] : Array.Empty<Vector2>(),
                    Colors = i < Colors.Count ? Colors[i] : Array.Empty<Vector4>(),
                    Tangents = i < Tangents.Count ? Tangents[i] : Array.Empty<Vector4>(),
                    Binormals = i < Binormals.Count ? Binormals[i] : Array.Empty<Vector3>(),
                    BlendIndices = i < BlendIndicies.Count ? BlendIndicies[i] : Array.Empty<Vector4>(),
                    BlendWeights = i < BlendWeights.Count ? BlendWeights[i] : Array.Empty<Vector4>(),
                    Indices = i < Indices.Count ? Indices[i] : Array.Empty<uint>(),
                    HasVertexColors = i < HasVertexColors.Count && HasVertexColors[i],
                    HasTangents = i < HasTangents.Count && HasTangents[i],
                    HasBinormals = i < HasBinormals.Count && HasBinormals[i],
                    HasSkinning = i < HasSkinning.Count && HasSkinning[i]
                });
            }

            return new ExportData
            {
                Name = Name,
                Submeshes = subs,
                Armature = _armature,
                Materials = _materials ?? Array.Empty<TrinityMaterial>()
            };
        }

        #endregion

        #region Mesh Parsing

        private void ParseMesh(string file)
        {
            var msh = FlatBufferConverter.DeserializeFrom<TRMSH>(file);
            var buffers = FlatBufferConverter.DeserializeFrom<TRMBF>(_modelPath.Combine(msh.bufferFilePath)).TRMeshBuffers;
            var shapeCnt = msh.Meshes.Count();
            for (int i = 0; i < shapeCnt; i++)
            {
                var meshShape = msh.Meshes[i];
                var vertBufs = buffers[i].VertexBuffer;
                var indexBuf = buffers[i].IndexBuffer[0]; // LOD0

                foreach (var part in meshShape.meshParts)
                {
                    MaterialNames.Add(part.MaterialName);
                    SubmeshNames.Add($"{meshShape.Name}:{part.MaterialName}");
                    int declIndex = part.vertexDeclarationIndex;
                    if (declIndex < 0 || declIndex >= meshShape.vertexDeclaration.Length)
                        declIndex = 0;
                    ParseMeshBuffer(meshShape.vertexDeclaration[declIndex], vertBufs, indexBuf,
                        meshShape.IndexType, part.indexOffset, part.indexCount,
                        meshShape.boneWeight, meshShape.Name);
                }
            }
        }

        private void ParseMeshBuffer(TRVertexDeclaration vertDesc, TRBuffer[] vertexBuffers, TRBuffer indexBuf,
            TRIndexFormat polyType, long start, long count, TRBoneWeight[]? boneWeights, string meshName)
        {
            if (vertexBuffers == null || vertexBuffers.Length == 0)
                return;

            var posElement = vertDesc.vertexElements.FirstOrDefault(e => e.vertexUsage == TRVertexUsage.POSITION);
            if (posElement == null)
                return;

            var posBuffer = GetVertexBuffer(vertexBuffers, posElement.vertexElementLayer);
            if (posBuffer == null)
                return;

            var posStride = GetStride(vertDesc, posElement.vertexElementSizeIndex);
            if (posStride <= 0)
                return;

            int vertexCount = posBuffer.Bytes.Length / posStride;
            if (vertexCount <= 0)
                return;

            Vector3[] pos = new Vector3[vertexCount];
            Vector3[] norm = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            Vector4[] color = new Vector4[vertexCount];
            Vector4[] tangent = new Vector4[vertexCount];
            Vector3[] binormal = new Vector3[vertexCount];
            Vector4[] blendIndices = new Vector4[vertexCount];
            Vector4[] blendWeights = new Vector4[vertexCount];
            bool hasNormals = false;
            bool hasUvs = false;
            bool hasColors = false;
            bool hasTangents = false;
            bool hasBinormals = false;
            bool hasBlendIndices = false;
            bool hasBlendWeights = false;
            _blendIndexStats = null;

            List<uint> indices = new();

            var blendIndexStreams = new List<Vector4[]>();
            var blendWeightStreams = new List<Vector4[]>();
            int blendIndexElementIndex = -1;
            int blendWeightElementIndex = -1;
            int texCoordElementIndex = -1;

            for (int i = 0; i < vertDesc.vertexElements.Length; i++)
            {
                var att = vertDesc.vertexElements[i];
                var buffer = GetVertexBuffer(vertexBuffers, att.vertexElementLayer);
                if (buffer == null)
                    continue;

                var stride = GetStride(vertDesc, att.vertexElementSizeIndex);
                if (stride <= 0)
                    continue;

                int? blendIndexStreamIndex = null;
                int? blendWeightStreamIndex = null;
                if (att.vertexUsage == TRVertexUsage.BLEND_INDEX)
                {
                    blendIndexElementIndex++;
                    EnsureBlendStream(blendIndexStreams, blendIndexElementIndex, vertexCount);
                    blendIndexStreamIndex = blendIndexElementIndex;
                }
                else if (att.vertexUsage == TRVertexUsage.BLEND_WEIGHTS)
                {
                    blendWeightElementIndex++;
                    EnsureBlendStream(blendWeightStreams, blendWeightElementIndex, vertexCount);
                    blendWeightStreamIndex = blendWeightElementIndex;
                }
                else if (att.vertexUsage == TRVertexUsage.TEX_COORD)
                {
                    texCoordElementIndex++;
                    if (texCoordElementIndex == 0) hasUvs = true;
                }

                for (int v = 0; v < vertexCount; v++)
                {
                    int offset = (v * stride) + att.vertexElementOffset;
                    if (!HasBytes(buffer.Bytes, offset, att.vertexFormat))
                        continue;

                    switch (att.vertexUsage)
                    {
                        case TRVertexUsage.POSITION:
                            pos[v] = ReadVector3(buffer.Bytes, offset, att.vertexFormat);
                            break;
                        case TRVertexUsage.NORMAL:
                            norm[v] = ReadNormal(buffer.Bytes, offset, att.vertexFormat);
                            hasNormals = true;
                            break;
                        case TRVertexUsage.TEX_COORD:
                            uv[v] = ReadVector2(buffer.Bytes, offset, att.vertexFormat);
                            hasUvs = true;
                            break;
                        case TRVertexUsage.COLOR:
                            color[v] = ReadColor(buffer.Bytes, offset, att.vertexFormat);
                            hasColors = true;
                            break;
                        case TRVertexUsage.TANGENT:
                            tangent[v] = ReadTangent(buffer.Bytes, offset, att.vertexFormat);
                            hasTangents = true;
                            break;
                        case TRVertexUsage.BINORMAL:
                            binormal[v] = ReadNormal(buffer.Bytes, offset, att.vertexFormat);
                            hasBinormals = true;
                            break;
                        case TRVertexUsage.BLEND_INDEX:
                            if (blendIndexStreamIndex.HasValue)
                                blendIndexStreams[blendIndexStreamIndex.Value][v] = ReadBlendIndices(buffer.Bytes, offset, att.vertexFormat);
                            hasBlendIndices = true;
                            break;
                        case TRVertexUsage.BLEND_WEIGHTS:
                            if (blendWeightStreamIndex.HasValue)
                                blendWeightStreams[blendWeightStreamIndex.Value][v] = ReadBlendWeights(buffer.Bytes, offset, att.vertexFormat);
                            hasBlendWeights = true;
                            break;
                    }
                }
            }

            if (hasBlendIndices && blendIndexStreams.Count > 0)
                blendIndices = blendIndexStreams[0];
            if (hasBlendWeights && blendWeightStreams.Count > 0)
                blendWeights = blendWeightStreams[0];

            // Collapse multiple blend streams to top 4
            if ((blendIndexStreams.Count > 1 || blendWeightStreams.Count > 1) && hasBlendIndices && hasBlendWeights)
            {
                int streamCount = Math.Min(blendIndexStreams.Count, blendWeightStreams.Count);
                if (streamCount > 1)
                    CollapseBlendStreams(blendIndexStreams, blendWeightStreams, streamCount, out blendIndices, out blendWeights);
            }

            if (hasBlendIndices)
            {
                int maxIndex = 0;
                for (int v = 0; v < vertexCount; v++)
                {
                    var idx = blendIndices[v];
                    maxIndex = Math.Max(maxIndex, (int)MathF.Max(MathF.Max(idx.X, idx.Y), MathF.Max(idx.Z, idx.W)));
                }
                _blendIndexStats = new BlendIndexStats { VertexCount = vertexCount, MaxIndex = maxIndex };
            }

            Positions.Add(pos);
            Normals.Add(hasNormals ? norm : new Vector3[vertexCount]);
            UVs.Add(hasUvs ? uv : new Vector2[vertexCount]);

            if (!hasColors)
                for (int v = 0; v < color.Length; v++)
                    color[v] = Vector4.One;
            Colors.Add(color);
            HasVertexColors.Add(hasColors);

            if (!hasTangents)
                for (int v = 0; v < tangent.Length; v++)
                    tangent[v] = new Vector4(1f, 0f, 0f, 1f);
            Tangents.Add(tangent);
            HasTangents.Add(hasTangents);

            if (!hasBinormals)
                for (int v = 0; v < binormal.Length; v++)
                    binormal[v] = Vector3.UnitY;
            Binormals.Add(binormal);
            HasBinormals.Add(hasBinormals);

            BlendIndicies.Add(blendIndices);
            BlendIndiciesOriginal.Add(blendIndices.ToArray());
            this.BlendWeights.Add(blendWeights);
            BlendBoneWeights.Add(boneWeights);
            BlendMeshNames.Add(meshName);
            HasSkinning.Add(hasBlendIndices && hasBlendWeights);

            // Parse index buffer
            using (var indBuf = new BinaryReader(new MemoryStream(indexBuf.Bytes)))
            {
                int indsize = (1 << (int)polyType);
                long currPos = start * indsize;
                indBuf.BaseStream.Position = currPos;
                while (currPos < (start + count) * indsize)
                {
                    switch (polyType)
                    {
                        case TRIndexFormat.BYTE: indices.Add(indBuf.ReadByte()); break;
                        case TRIndexFormat.SHORT: indices.Add(indBuf.ReadUInt16()); break;
                        case TRIndexFormat.INT: indices.Add(indBuf.ReadUInt32()); break;
                    }
                    currPos += indsize;
                }
                Indices.Add(indices.ToArray());
            }
        }

        #endregion

        #region Vertex Readers

        private static Vector3 ReadVector3(byte[] buffer, int offset, TRVertexFormat format)
        {
            return format switch
            {
                TRVertexFormat.X32_Y32_Z32_FLOAT => new Vector3(BitConverter.ToSingle(buffer, offset), BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8)),
                TRVertexFormat.W32_X32_Y32_Z32_FLOAT => new Vector3(BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8), BitConverter.ToSingle(buffer, offset + 12)),
                TRVertexFormat.W16_X16_Y16_Z16_FLOAT => new Vector3(ReadHalf(buffer, offset), ReadHalf(buffer, offset + 2), ReadHalf(buffer, offset + 4)),
                TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED => new Vector3(ReadUnorm16(buffer, offset), ReadUnorm16(buffer, offset + 2), ReadUnorm16(buffer, offset + 4)),
                TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED or TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED => new Vector3(ReadUnorm8(buffer, offset), ReadUnorm8(buffer, offset + 1), ReadUnorm8(buffer, offset + 2)),
                _ => Vector3.Zero,
            };
        }

        private static Vector3 ReadNormal(byte[] buffer, int offset, TRVertexFormat format)
        {
            return format switch
            {
                TRVertexFormat.W16_X16_Y16_Z16_FLOAT => new Vector3(ReadHalf(buffer, offset), ReadHalf(buffer, offset + 2), ReadHalf(buffer, offset + 4)),
                TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED => new Vector3(ReadSnorm16(buffer, offset), ReadSnorm16(buffer, offset + 2), ReadSnorm16(buffer, offset + 4)),
                TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED or TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED => new Vector3(ReadSnorm8(buffer, offset), ReadSnorm8(buffer, offset + 1), ReadSnorm8(buffer, offset + 2)),
                TRVertexFormat.X32_Y32_Z32_FLOAT => new Vector3(BitConverter.ToSingle(buffer, offset), BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8)),
                _ => Vector3.UnitZ,
            };
        }

        private static Vector2 ReadVector2(byte[] buffer, int offset, TRVertexFormat format)
        {
            return format switch
            {
                TRVertexFormat.X32_Y32_FLOAT => new Vector2(BitConverter.ToSingle(buffer, offset), BitConverter.ToSingle(buffer, offset + 4)),
                TRVertexFormat.W16_X16_Y16_Z16_FLOAT => new Vector2(ReadHalf(buffer, offset), ReadHalf(buffer, offset + 2)),
                TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED => new Vector2(ReadUnorm16(buffer, offset), ReadUnorm16(buffer, offset + 2)),
                TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED or TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED => new Vector2(ReadUnorm8(buffer, offset), ReadUnorm8(buffer, offset + 1)),
                _ => Vector2.Zero,
            };
        }

        private static Vector4 ReadColor(byte[] buffer, int offset, TRVertexFormat format)
        {
            return format switch
            {
                TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED or TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED => new Vector4(ReadUnorm8(buffer, offset), ReadUnorm8(buffer, offset + 1), ReadUnorm8(buffer, offset + 2), ReadUnorm8(buffer, offset + 3)),
                TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED => new Vector4(ReadUnorm16(buffer, offset), ReadUnorm16(buffer, offset + 2), ReadUnorm16(buffer, offset + 4), ReadUnorm16(buffer, offset + 6)),
                TRVertexFormat.W16_X16_Y16_Z16_FLOAT => new Vector4(ReadHalf(buffer, offset), ReadHalf(buffer, offset + 2), ReadHalf(buffer, offset + 4), ReadHalf(buffer, offset + 6)),
                _ => Vector4.One,
            };
        }

        private static Vector4 ReadTangent(byte[] buffer, int offset, TRVertexFormat format)
        {
            return format switch
            {
                TRVertexFormat.W32_X32_Y32_Z32_FLOAT => new Vector4(BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8), BitConverter.ToSingle(buffer, offset + 12), BitConverter.ToSingle(buffer, offset)),
                TRVertexFormat.X32_Y32_Z32_FLOAT => new Vector4(BitConverter.ToSingle(buffer, offset), BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8), 1f),
                TRVertexFormat.W16_X16_Y16_Z16_FLOAT => new Vector4(ReadHalf(buffer, offset), ReadHalf(buffer, offset + 2), ReadHalf(buffer, offset + 4), ReadHalf(buffer, offset + 6)),
                TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED => new Vector4(ReadSnorm16(buffer, offset), ReadSnorm16(buffer, offset + 2), ReadSnorm16(buffer, offset + 4), ReadSnorm16(buffer, offset + 6)),
                TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED or TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED => new Vector4(ReadSnorm8(buffer, offset), ReadSnorm8(buffer, offset + 1), ReadSnorm8(buffer, offset + 2), ReadSnorm8(buffer, offset + 3)),
                _ => new Vector4(1f, 0f, 0f, 1f),
            };
        }

        private static Vector4 ReadBlendIndices(byte[] buffer, int offset, TRVertexFormat format)
        {
            return format switch
            {
                TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED => new Vector4(buffer[offset + 1], buffer[offset + 2], buffer[offset + 3], buffer[offset]),
                TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED => new Vector4(BitConverter.ToUInt16(buffer, offset + 2), BitConverter.ToUInt16(buffer, offset + 4), BitConverter.ToUInt16(buffer, offset + 6), BitConverter.ToUInt16(buffer, offset)),
                TRVertexFormat.W32_X32_Y32_Z32_UNSIGNED => new Vector4(BitConverter.ToUInt32(buffer, offset + 4), BitConverter.ToUInt32(buffer, offset + 8), BitConverter.ToUInt32(buffer, offset + 12), BitConverter.ToUInt32(buffer, offset)),
                TRVertexFormat.W32_X32_Y32_Z32_FLOAT => new Vector4(BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8), BitConverter.ToSingle(buffer, offset + 12), BitConverter.ToSingle(buffer, offset)),
                _ => Vector4.Zero,
            };
        }

        private static Vector4 ReadBlendWeights(byte[] buffer, int offset, TRVertexFormat format)
        {
            return format switch
            {
                TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED => new Vector4(ReadUnorm16(buffer, offset + 2), ReadUnorm16(buffer, offset + 4), ReadUnorm16(buffer, offset + 6), ReadUnorm16(buffer, offset)),
                TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED or TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED => new Vector4(ReadUnorm8(buffer, offset + 1), ReadUnorm8(buffer, offset + 2), ReadUnorm8(buffer, offset + 3), ReadUnorm8(buffer, offset)),
                TRVertexFormat.W32_X32_Y32_Z32_FLOAT => new Vector4(BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8), BitConverter.ToSingle(buffer, offset + 12), BitConverter.ToSingle(buffer, offset)),
                _ => Vector4.Zero,
            };
        }

        private static float ReadHalf(byte[] buffer, int offset)
        {
            ushort raw = BitConverter.ToUInt16(buffer, offset);
            return (float)BitConverter.UInt16BitsToHalf(raw);
        }

        private static float ReadUnorm16(byte[] buffer, int offset)
            => BitConverter.ToUInt16(buffer, offset) / 65535f;

        private static float ReadSnorm16(byte[] buffer, int offset)
            => (BitConverter.ToUInt16(buffer, offset) / 65535f) * 2f - 1f;

        private static float ReadUnorm8(byte[] buffer, int offset)
            => buffer[offset] / 255f;

        private static float ReadSnorm8(byte[] buffer, int offset)
            => (buffer[offset] / 255f) * 2f - 1f;

        #endregion

        #region Buffer Helpers

        private static TRBuffer? GetVertexBuffer(TRBuffer[] buffers, int index)
        {
            if (buffers == null || index < 0 || index >= buffers.Length)
                return null;
            return buffers[index];
        }

        private static int GetStride(TRVertexDeclaration vertDesc, int sizeIndex)
        {
            if (vertDesc.vertexElementSizes == null || sizeIndex < 0 || sizeIndex >= vertDesc.vertexElementSizes.Length)
                return 0;
            return vertDesc.vertexElementSizes[sizeIndex].elementSize;
        }

        private static bool HasBytes(byte[] buffer, int offset, TRVertexFormat format)
        {
            int size = format switch
            {
                TRVertexFormat.X32_Y32_Z32_FLOAT => 12,
                TRVertexFormat.X32_Y32_FLOAT => 8,
                TRVertexFormat.W32_X32_Y32_Z32_FLOAT => 16,
                TRVertexFormat.W32_X32_Y32_Z32_UNSIGNED => 16,
                TRVertexFormat.W16_X16_Y16_Z16_FLOAT => 8,
                TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED => 8,
                TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED => 4,
                TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED => 4,
                _ => 0
            };
            return size > 0 && offset >= 0 && offset + size <= buffer.Length;
        }

        private static void EnsureBlendStream(List<Vector4[]> streams, int index, int vertexCount)
        {
            while (streams.Count <= index)
                streams.Add(new Vector4[vertexCount]);
        }

        #endregion

        #region Blend Stream Collapse

        private static void CollapseBlendStreams(
            List<Vector4[]> indexStreams, List<Vector4[]> weightStreams, int streamCount,
            out Vector4[] collapsedIndices, out Vector4[] collapsedWeights)
        {
            int vertexCount = indexStreams[0].Length;
            collapsedIndices = new Vector4[vertexCount];
            collapsedWeights = new Vector4[vertexCount];

            for (int v = 0; v < vertexCount; v++)
            {
                var totals = new Dictionary<int, float>();
                for (int s = 0; s < streamCount; s++)
                {
                    var idx = indexStreams[s][v];
                    var w = weightStreams[s][v];
                    AccumulateInfluence(totals, (int)MathF.Round(idx.X), w.X);
                    AccumulateInfluence(totals, (int)MathF.Round(idx.Y), w.Y);
                    AccumulateInfluence(totals, (int)MathF.Round(idx.Z), w.Z);
                    AccumulateInfluence(totals, (int)MathF.Round(idx.W), w.W);
                }

                if (totals.Count == 0)
                {
                    collapsedIndices[v] = Vector4.Zero;
                    collapsedWeights[v] = Vector4.Zero;
                    continue;
                }

                var top = totals.OrderByDescending(kv => kv.Value).Take(4).ToArray();
                collapsedIndices[v] = new Vector4(
                    top.Length > 0 ? top[0].Key : 0, top.Length > 1 ? top[1].Key : 0,
                    top.Length > 2 ? top[2].Key : 0, top.Length > 3 ? top[3].Key : 0);
                collapsedWeights[v] = new Vector4(
                    top.Length > 0 ? top[0].Value : 0f, top.Length > 1 ? top[1].Value : 0f,
                    top.Length > 2 ? top[2].Value : 0f, top.Length > 3 ? top[3].Value : 0f);
            }
        }

        private static void AccumulateInfluence(Dictionary<int, float> totals, int index, float weight)
        {
            if (weight <= 0f) return;
            if (totals.TryGetValue(index, out var current))
                totals[index] = current + weight;
            else
                totals[index] = weight;
        }

        #endregion

        #region Material Parsing

        private void ParseMaterial(string file)
        {
            List<TrinityMaterial> matlist = new();
            var materialPath = new PathString(file);

            TRMTR? trmtrFallback = null;
            try { trmtrFallback = FlatBufferConverter.DeserializeFrom<TRMTR>(file); }
            catch { trmtrFallback = null; }

            Dictionary<string, TRMaterial> trmtrByName = new(StringComparer.OrdinalIgnoreCase);
            if (trmtrFallback?.Materials != null)
            {
                foreach (var mat in trmtrFallback.Materials)
                {
                    if (!string.IsNullOrEmpty(mat?.Name))
                        trmtrByName[mat.Name] = mat;
                }
            }

            var gfxMaterials = FlatBufferConverter.DeserializeFrom<Flatbuffers.Gfx2.Material>(file);
            if (gfxMaterials?.ItemList != null && gfxMaterials.ItemList.Length > 0)
            {
                foreach (var item in gfxMaterials.ItemList)
                {
                    var shaderName = item?.TechniqueList?.FirstOrDefault()?.Name ?? "Standard";
                    var shaderParams = new List<TRStringParameter>();

                    if (item?.TechniqueList != null)
                    {
                        foreach (var technique in item.TechniqueList)
                        {
                            if (technique?.ShaderOptions == null) continue;
                            foreach (var opt in technique.ShaderOptions)
                            {
                                if (opt == null) continue;
                                shaderParams.Add(new TRStringParameter { Name = opt.Name, Value = opt.Choice });
                            }
                        }
                    }

                    if (item?.IntParamList != null)
                    {
                        foreach (var p in item.IntParamList)
                        {
                            if (p == null) continue;
                            shaderParams.Add(new TRStringParameter { Name = p.Name, Value = p.Value.ToString() });
                        }
                    }

                    var textures = item?.TextureParamList?
                        .Select(t => new TRTexture
                        {
                            Name = t.Name,
                            File = t.FilePath,
                            Slot = (uint)Math.Max(0, t.SamplerId)
                        })
                        .ToArray() ?? Array.Empty<TRTexture>();

                    var trmat = new TRMaterial
                    {
                        Name = item?.Name ?? "Material",
                        Shader = new[] { new TRMaterialShader { Name = shaderName, Values = shaderParams.ToArray() } },
                        Textures = textures,
                        FloatParams = item?.FloatParamList?
                            .Select(p => new TRFloatParameter { Name = p.Name, Value = p.Value })
                            .ToArray(),
                        Vec2fParams = item?.Vector2fParamList?
                            .Select(p => new TRVec2fParameter { Name = p.Name, Value = p.Value })
                            .ToArray(),
                        Vec3fParams = item?.Vector3fParamList?
                            .Select(p => new TRVec3fParameter { Name = p.Name, Value = p.Value })
                            .ToArray(),
                        Vec4fParams = item?.Vector4fParamList?
                            .Select(p => new TRVec4fParameter { Name = p.Name, Value = p.Value })
                            .ToArray(),
                    };

                    if (trmtrByName.TryGetValue(trmat.Name, out var fallbackMat))
                        trmat.Samplers = fallbackMat.Samplers;

                    matlist.Add(new TrinityMaterial(materialPath, trmat));
                }

                _materials = matlist.ToArray();
                return;
            }

            var mats = FlatBufferConverter.DeserializeFrom<TRMTR>(file);
            foreach (var mat in mats.Materials)
                matlist.Add(new TrinityMaterial(materialPath, mat));
            _materials = matlist.ToArray();
        }

        #endregion

        #region Armature Parsing

        private void ParseArmature(string file)
        {
            var skel = FlatBufferConverter.DeserializeFrom<TRSKL>(file);
            var merged = TryLoadAndMergeBaseSkeleton(skel, file, _baseSkeletonCategoryHint);
            _armature = new TrinityArmature(merged ?? skel, file);
            ApplyBlendIndexMapping();
        }

        private static string? GuessBaseSkeletonCategoryFromMesh(string? meshPathName)
        {
            if (string.IsNullOrWhiteSpace(meshPathName))
                return null;

            string fn = Path.GetFileName(meshPathName);
            if (fn.StartsWith("p0", StringComparison.OrdinalIgnoreCase) ||
                fn.StartsWith("p1", StringComparison.OrdinalIgnoreCase) ||
                fn.StartsWith("p2", StringComparison.OrdinalIgnoreCase))
                return "Protag";

            if (fn.StartsWith("bu_", StringComparison.OrdinalIgnoreCase)) return "CommonNPCbu";
            if (fn.StartsWith("dm_", StringComparison.OrdinalIgnoreCase)) return "CommonNPCdm";
            if (fn.StartsWith("df_", StringComparison.OrdinalIgnoreCase)) return "CommonNPCdf";
            if (fn.StartsWith("em_", StringComparison.OrdinalIgnoreCase)) return "CommonNPCem";
            if (fn.StartsWith("fm_", StringComparison.OrdinalIgnoreCase)) return "CommonNPCfm";
            if (fn.StartsWith("ff_", StringComparison.OrdinalIgnoreCase)) return "CommonNPCff";
            if (fn.StartsWith("gm_", StringComparison.OrdinalIgnoreCase)) return "CommonNPCgm";
            if (fn.StartsWith("gf_", StringComparison.OrdinalIgnoreCase)) return "CommonNPCgf";
            if (fn.StartsWith("rv_", StringComparison.OrdinalIgnoreCase)) return "CommonNPCrv";

            return null;
        }

        private static TRSKL? TryLoadAndMergeBaseSkeleton(TRSKL localSkel, string localSkelPath, string? category)
        {
            if (localSkel == null || string.IsNullOrWhiteSpace(localSkelPath) || string.IsNullOrWhiteSpace(category))
                return null;

            var localDir = Path.GetDirectoryName(localSkelPath);
            if (string.IsNullOrWhiteSpace(localDir))
                return null;

            var basePath = ResolveBaseTrsklPath(localDir, category);
            if (string.IsNullOrWhiteSpace(basePath) || !File.Exists(basePath))
                return null;

            try
            {
                var baseSkel = FlatBufferConverter.DeserializeFrom<TRSKL>(basePath);
                return MergeBaseAndLocalSkeletons(baseSkel, localSkel);
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolveBaseTrsklPath(string modelDir, string category)
        {
            string[] rels = category switch
            {
                "Protag" => new[]
                {
                    "../../model_pc_base/model/p0_base.trskl",
                    "../../../../p2/model/base/p2_base0001_00_default/p2_base0001_00_default.trskl",
                    "../../p2/p2_base0001_00_default/p2_base0001_00_default.trskl"
                },
                "CommonNPCbu" => new[] { "../../../model_cc_base/bu/bu_base.trskl", "../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl" },
                "CommonNPCdm" or "CommonNPCdf" => new[] { "../../../model_cc_base/dm/dm_base.trskl", "../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl" },
                "CommonNPCem" => new[] { "../../../model_cc_base/em/em_base.trskl", "../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl" },
                "CommonNPCfm" or "CommonNPCff" => new[] { "../../../model_cc_base/fm/fm_base.trskl", "../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl" },
                "CommonNPCgm" or "CommonNPCgf" => new[] { "../../../model_cc_base/gm/gm_base.trskl", "../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl" },
                "CommonNPCrv" => new[] { "../../../model_cc_base/rv/rv_base.trskl", "../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl" },
                _ => Array.Empty<string>()
            };

            foreach (var rel in rels)
            {
                var full = Path.GetFullPath(Path.Combine(modelDir, rel));
                if (File.Exists(full))
                    return full;
            }
            return null;
        }

        private static TRSKL MergeBaseAndLocalSkeletons(TRSKL baseSkel, TRSKL localSkel)
        {
            int baseNodeCount = baseSkel.TransformNodes?.Length ?? 0;
            int baseJointCount = baseSkel.JointInfos?.Length ?? 0;

            var mergedNodes = new List<TRTransformNode>(baseNodeCount + (localSkel.TransformNodes?.Length ?? 0));
            var mergedJoints = new List<TRJointInfo>(baseJointCount + (localSkel.JointInfos?.Length ?? 0));

            var baseIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (baseSkel.TransformNodes != null)
            {
                for (int i = 0; i < baseSkel.TransformNodes.Length; i++)
                {
                    var n = baseSkel.TransformNodes[i];
                    mergedNodes.Add(n);
                    if (!string.IsNullOrWhiteSpace(n?.Name))
                        baseIndexByName[n.Name] = i;
                }
            }

            if (baseSkel.JointInfos != null)
                mergedJoints.AddRange(baseSkel.JointInfos);
            if (localSkel.JointInfos != null)
                mergedJoints.AddRange(localSkel.JointInfos);

            if (localSkel.TransformNodes != null)
            {
                for (int i = 0; i < localSkel.TransformNodes.Length; i++)
                {
                    var node = localSkel.TransformNodes[i];
                    if (node == null) continue;

                    int parentIndex = node.ParentNodeIndex;
                    string parentName = node.ParentNodeName ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(parentName) && baseIndexByName.TryGetValue(parentName, out int baseParent))
                        parentIndex = baseParent;
                    else if (parentIndex >= 0)
                        parentIndex = parentIndex + baseNodeCount;

                    int jointIndex = node.JointInfoIndex;
                    if (jointIndex >= 0)
                        jointIndex = jointIndex + baseJointCount;

                    mergedNodes.Add(new TRTransformNode
                    {
                        Name = node.Name,
                        Transform = node.Transform,
                        ScalePivot = node.ScalePivot,
                        RotatePivot = node.RotatePivot,
                        ParentNodeIndex = parentIndex,
                        JointInfoIndex = jointIndex,
                        ParentNodeName = node.ParentNodeName,
                        Priority = node.Priority,
                        PriorityPass = node.PriorityPass,
                        IgnoreParentRotation = node.IgnoreParentRotation
                    });
                }
            }

            return new TRSKL
            {
                Version = baseSkel.Version != 0 ? baseSkel.Version : localSkel.Version,
                TransformNodes = mergedNodes.ToArray(),
                JointInfos = mergedJoints.ToArray(),
                HelperBones = baseSkel.HelperBones?.Length > 0 ? baseSkel.HelperBones : (localSkel.HelperBones ?? Array.Empty<TRHelperBoneInfo>()),
                SkinningPaletteOffset = baseJointCount,
                IsInteriorMap = baseSkel.IsInteriorMap || localSkel.IsInteriorMap
            };
        }

        #endregion

        #region Blend Index Mapping

        private void ApplyBlendIndexMapping()
        {
            if (_armature == null)
                return;

            var skinPalette = _armature.BuildSkinningPalette();

            for (int i = 0; i < BlendIndiciesOriginal.Count; i++)
            {
                var source = BlendIndiciesOriginal[i];
                var boneWeights = i < BlendBoneWeights.Count ? BlendBoneWeights[i] : null;
                var mapped = new Vector4[source.Length];
                int maxIndexBefore = GetMaxIndex(source);

                bool canRemapViaBoneWeights = false;
                if (boneWeights != null && boneWeights.Length > 0 && maxIndexBefore < boneWeights.Length)
                {
                    int outOfRangeWeights = 0;
                    int sampleCount = Math.Min(source.Length, 512);
                    for (int v = 0; v < sampleCount; v++)
                    {
                        var idx = source[v];
                        CountOutOfRange(boneWeights, (int)MathF.Round(idx.X), ref outOfRangeWeights);
                        CountOutOfRange(boneWeights, (int)MathF.Round(idx.Y), ref outOfRangeWeights);
                        CountOutOfRange(boneWeights, (int)MathF.Round(idx.Z), ref outOfRangeWeights);
                        CountOutOfRange(boneWeights, (int)MathF.Round(idx.W), ref outOfRangeWeights);
                    }
                    canRemapViaBoneWeights = outOfRangeWeights == 0;
                }

                var mode = SelectBlendIndexRemapMode(i, canRemapViaBoneWeights, boneWeights, maxIndexBefore, skinPalette);

                for (int v = 0; v < source.Length; v++)
                {
                    var idx = source[v];
                    if (mode == BlendIndexRemapMode.BoneWeights && boneWeights != null)
                    {
                        idx = new Vector4(
                            MapBlendIndex(idx.X, boneWeights),
                            MapBlendIndex(idx.Y, boneWeights),
                            MapBlendIndex(idx.Z, boneWeights),
                            MapBlendIndex(idx.W, boneWeights));
                    }

                    if (mode == BlendIndexRemapMode.JointInfo)
                    {
                        mapped[v] = new Vector4(
                            (int)MathF.Round(idx.X) >= 0 && (int)MathF.Round(idx.X) < _armature.JointInfoCount ? _armature.MapJointInfoIndex((int)MathF.Round(idx.X)) : idx.X,
                            (int)MathF.Round(idx.Y) >= 0 && (int)MathF.Round(idx.Y) < _armature.JointInfoCount ? _armature.MapJointInfoIndex((int)MathF.Round(idx.Y)) : idx.Y,
                            (int)MathF.Round(idx.Z) >= 0 && (int)MathF.Round(idx.Z) < _armature.JointInfoCount ? _armature.MapJointInfoIndex((int)MathF.Round(idx.Z)) : idx.Z,
                            (int)MathF.Round(idx.W) >= 0 && (int)MathF.Round(idx.W) < _armature.JointInfoCount ? _armature.MapJointInfoIndex((int)MathF.Round(idx.W)) : idx.W);
                    }
                    else if (mode == BlendIndexRemapMode.SkinningPalette)
                    {
                        int ix = (int)MathF.Round(idx.X), iy = (int)MathF.Round(idx.Y), iz = (int)MathF.Round(idx.Z), iw = (int)MathF.Round(idx.W);
                        mapped[v] = new Vector4(
                            ix >= 0 && ix < skinPalette.Length ? skinPalette[ix] : idx.X,
                            iy >= 0 && iy < skinPalette.Length ? skinPalette[iy] : idx.Y,
                            iz >= 0 && iz < skinPalette.Length ? skinPalette[iz] : idx.Z,
                            iw >= 0 && iw < skinPalette.Length ? skinPalette[iw] : idx.W);
                    }
                    else if (mode == BlendIndexRemapMode.BoneMeta)
                    {
                        mapped[v] = new Vector4(
                            (int)MathF.Round(idx.X) >= 0 && (int)MathF.Round(idx.X) < _armature.BoneMetaCount ? _armature.MapBoneMetaIndex((int)MathF.Round(idx.X)) : idx.X,
                            (int)MathF.Round(idx.Y) >= 0 && (int)MathF.Round(idx.Y) < _armature.BoneMetaCount ? _armature.MapBoneMetaIndex((int)MathF.Round(idx.Y)) : idx.Y,
                            (int)MathF.Round(idx.Z) >= 0 && (int)MathF.Round(idx.Z) < _armature.BoneMetaCount ? _armature.MapBoneMetaIndex((int)MathF.Round(idx.Z)) : idx.Z,
                            (int)MathF.Round(idx.W) >= 0 && (int)MathF.Round(idx.W) < _armature.BoneMetaCount ? _armature.MapBoneMetaIndex((int)MathF.Round(idx.W)) : idx.W);
                    }
                    else
                    {
                        mapped[v] = idx;
                    }
                }

                BlendIndicies[i] = mapped;
            }
        }

        private BlendIndexRemapMode SelectBlendIndexRemapMode(
            int submeshIndex, bool canRemapViaBoneWeights, TRBoneWeight[]? boneWeights,
            int maxIndexBefore, int[] skinPalette)
        {
            if (_armature == null)
                return BlendIndexRemapMode.None;

            if (canRemapViaBoneWeights && boneWeights != null)
                return BlendIndexRemapMode.BoneWeights;

            bool canMapJointInfo = _armature.JointInfoCount > 0;
            bool canMapSkinPalette = skinPalette.Length > 0;

            // Auto mode — try joint info first for dual-skeleton models
            if (_armature.JointInfoCount > 0 &&
                maxIndexBefore >= 0 &&
                maxIndexBefore < _armature.JointInfoCount &&
                (_armature.Bones.Count - _armature.JointInfoCount) >= 16)
            {
                bool mappingIsIdentity = true;
                int sampleMax = Math.Min(maxIndexBefore, Math.Min(_armature.JointInfoCount - 1, 64));
                for (int i = 0; i <= sampleMax; i++)
                {
                    if (_armature.MapJointInfoIndex(i) != i)
                    {
                        mappingIsIdentity = false;
                        break;
                    }
                }
                if (!mappingIsIdentity)
                    return BlendIndexRemapMode.JointInfo;
            }

            // Score-based auto selection
            var source = BlendIndiciesOriginal[submeshIndex];
            var weights = submeshIndex < this.BlendWeights.Count ? this.BlendWeights[submeshIndex] : null;

            (int outOfRange, int nonInfluencer) bestScore = ScoreBlendIndexMapping(source, weights, BlendIndexRemapMode.None, boneWeights, skinPalette);
            BlendIndexRemapMode bestMode = BlendIndexRemapMode.None;

            void consider(BlendIndexRemapMode candidate)
            {
                var score = ScoreBlendIndexMapping(source, weights, candidate, boneWeights, skinPalette);
                if (score.outOfRange < bestScore.outOfRange ||
                    (score.outOfRange == bestScore.outOfRange && score.nonInfluencer < bestScore.nonInfluencer))
                {
                    bestScore = score;
                    bestMode = candidate;
                }
            }

            if (_armature.JointInfoCount > 0) consider(BlendIndexRemapMode.JointInfo);
            if (skinPalette.Length > 0) consider(BlendIndexRemapMode.SkinningPalette);
            if (_armature.BoneMetaCount > 0) consider(BlendIndexRemapMode.BoneMeta);

            // Tie breaker
            if (bestMode == BlendIndexRemapMode.None)
            {
                var jointScore = _armature.JointInfoCount > 0 ? ScoreBlendIndexMapping(source, weights, BlendIndexRemapMode.JointInfo, boneWeights, skinPalette) : (int.MaxValue, int.MaxValue);
                if (jointScore == bestScore)
                    bestMode = BlendIndexRemapMode.JointInfo;
                else if (skinPalette.Length > 0)
                {
                    var palScore = ScoreBlendIndexMapping(source, weights, BlendIndexRemapMode.SkinningPalette, boneWeights, skinPalette);
                    if (palScore == bestScore)
                        bestMode = BlendIndexRemapMode.SkinningPalette;
                }
            }

            return bestMode;
        }

        private (int outOfRange, int nonInfluencer) ScoreBlendIndexMapping(
            Vector4[] indices, Vector4[]? weights, BlendIndexRemapMode mode,
            TRBoneWeight[]? boneWeights, int[] skinPalette)
        {
            if (_armature == null || indices == null || indices.Length == 0)
                return (0, 0);

            int outOfRange = 0;
            int nonInfluencer = 0;
            int sampleCount = Math.Min(indices.Length, 2048);

            for (int v = 0; v < sampleCount; v++)
            {
                var idx = indices[v];
                var w = weights != null && v < weights.Length ? weights[v] : Vector4.One;
                ScoreComponent(idx.X, w.X);
                ScoreComponent(idx.Y, w.Y);
                ScoreComponent(idx.Z, w.Z);
                ScoreComponent(idx.W, w.W);
            }

            return (outOfRange, nonInfluencer);

            void ScoreComponent(float value, float weight)
            {
                if (weight <= 0.0001f) return;
                int mapped = MapBlendIndexComponent(value, mode, boneWeights, skinPalette);
                if (mapped < 0 || mapped >= _armature!.Bones.Count)
                {
                    outOfRange++;
                    return;
                }
                if (!_armature.Bones[mapped].Skinning)
                    nonInfluencer++;
            }
        }

        private int MapBlendIndexComponent(float value, BlendIndexRemapMode mode, TRBoneWeight[]? boneWeights, int[] skinPalette)
        {
            if (_armature == null) return 0;
            int index = (int)MathF.Round(value);
            if (index < 0) return index;

            return mode switch
            {
                BlendIndexRemapMode.BoneWeights when boneWeights != null && index < boneWeights.Length => boneWeights[index].RigIndex,
                BlendIndexRemapMode.JointInfo when index < _armature.JointInfoCount => _armature.MapJointInfoIndex(index),
                BlendIndexRemapMode.SkinningPalette when skinPalette != null && index < skinPalette.Length => skinPalette[index],
                BlendIndexRemapMode.BoneMeta when index < _armature.BoneMetaCount => _armature.MapBoneMetaIndex(index),
                _ => index,
            };
        }

        private static float MapBlendIndex(float value, TRBoneWeight[] boneWeights)
        {
            int index = (int)MathF.Round(value);
            if (index >= 0 && index < boneWeights.Length)
            {
                int rigIndex = boneWeights[index].RigIndex;
                return rigIndex >= 0 ? rigIndex : value;
            }
            return value;
        }

        private static int GetMaxIndex(Vector4[] indices)
        {
            int max = 0;
            foreach (var idx in indices)
                max = Math.Max(max, (int)MathF.Max(MathF.Max(idx.X, idx.Y), MathF.Max(idx.Z, idx.W)));
            return max;
        }

        private static void CountOutOfRange(TRBoneWeight[] boneWeights, int index, ref int counter)
        {
            if (index < 0 || index >= boneWeights.Length)
                counter++;
        }

        #endregion

        #region Internal Types

        private class BlendIndexStats
        {
            public int VertexCount;
            public int MaxIndex;
        }

        #endregion
    }
}
