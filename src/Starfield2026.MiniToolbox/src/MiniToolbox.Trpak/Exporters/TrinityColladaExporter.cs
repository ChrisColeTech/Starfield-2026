using MiniToolbox.Core.Exporters;
using System.Globalization;
using System.Text;
using System.Xml;
using OpenTK.Mathematics;
using MiniToolbox.Trpak.Decoders;

namespace MiniToolbox.Trpak.Exporters
{
    /// <summary>
    /// Exports Trinity model data to COLLADA 1.4.1 DAE.
    /// Consumes <see cref="TrinityModelDecoder.ExportData"/>.
    /// </summary>
    public static class TrinityColladaExporter
    {
        /// <summary>
        /// Export model-only DAE (no animation).
        /// </summary>
        public static void Export(string outputPath, TrinityModelDecoder.ExportData data)
        {
            var ctx = new ExportContext(data, null);
            ctx.Export(outputPath);
        }

        /// <summary>
        /// Export model with baked animation.
        /// </summary>
        public static void ExportWithAnimation(string outputPath, TrinityModelDecoder.ExportData data, TrinityAnimationDecoder animation)
        {
            var ctx = new ExportContext(data, animation);
            ctx.Export(outputPath);
        }

        /// <summary>
        /// Export clip-only DAE (skeleton + animation, no geometry).
        /// </summary>
        public static void ExportClipOnly(string outputPath, TrinityArmature armature, TrinityAnimationDecoder animation, string name = "Clip")
        {
            var data = new TrinityModelDecoder.ExportData
            {
                Name = name,
                Armature = armature,
                Submeshes = new List<TrinityModelDecoder.ExportSubmesh>(),
                Materials = new List<TrinityMaterial>()
            };
            var ctx = new ExportContext(data, animation);
            ctx.Export(outputPath);
        }
    }

    internal class ExportContext
    {
        private readonly TrinityModelDecoder.ExportData _data;
        private readonly TrinityAnimationDecoder? _animation;

        private readonly List<ColladaGeometry> _geometries = new();
        private readonly List<ColladaController> _controllers = new();
        private readonly List<ColladaMaterial> _materials = new();
        private readonly List<ColladaEffect> _effects = new();
        private readonly List<ColladaImage> _images = new();
        private readonly List<ColladaNode> _sceneNodes = new();
        private readonly HashSet<string> _seenImageIds = new();
        private readonly HashSet<string> _seenMaterialIds = new();

        public ExportContext(TrinityModelDecoder.ExportData data, TrinityAnimationDecoder? animation)
        {
            _data = data;
            _animation = animation;
        }

        public void Export(string outputPath)
        {
            // Build armature joint hierarchy
            if (_data.Armature != null && _data.Armature.Bones.Count > 0)
                BuildBoneNodes();

            // Build geometry + controllers for each submesh
            for (int i = 0; i < _data.Submeshes.Count; i++)
                ExportSubmesh(_data.Submeshes[i], i);

            Write(outputPath);
        }

        #region Bone Hierarchy

        private void BuildBoneNodes()
        {
            var armature = _data.Armature!;
            var worldMatrices = armature.GetWorldMatrices();

            // Find root bones and build hierarchy
            for (int i = 0; i < armature.Bones.Count; i++)
            {
                var bone = armature.Bones[i];
                if (bone.ParentIndex < 0 || bone.ParentIndex >= armature.Bones.Count || bone.ParentIndex == i)
                {
                    var node = BuildBoneNode(i, armature);
                    _sceneNodes.Add(node);
                }
            }
        }

        private ColladaNode BuildBoneNode(int index, TrinityArmature armature)
        {
            var bone = armature.Bones[index];
            var localMatrix = bone.RestLocalMatrix;

            var node = new ColladaNode
            {
                Id = bone.Name + "_id",
                Name = bone.Name,
                NodeType = "JOINT",
                Transform = localMatrix
            };

            foreach (var child in bone.Children)
            {
                int childIndex = armature.Bones.IndexOf(child);
                if (childIndex >= 0)
                    node.Children.Add(BuildBoneNode(childIndex, armature));
            }

            return node;
        }

        private string GetRootBoneId()
        {
            if (_data.Armature == null || _data.Armature.Bones.Count == 0)
                return "";

            for (int i = 0; i < _data.Armature.Bones.Count; i++)
            {
                var bone = _data.Armature.Bones[i];
                if (bone.ParentIndex < 0 || bone.ParentIndex >= _data.Armature.Bones.Count || bone.ParentIndex == i)
                    return bone.Name + "_id";
            }

            return _data.Armature.Bones[0].Name + "_id";
        }

        #endregion

        #region Submesh Export

        private void ExportSubmesh(TrinityModelDecoder.ExportSubmesh sub, int index)
        {
            string baseName = SanitizeName(sub.Name);
            string geometryId = $"{baseName}_{index}";

            var geometry = new ColladaGeometry { Id = geometryId, Name = sub.Name };

            // Sources
            var posSource = CreateFloatSource(geometryId + "_pos", sub.Positions, 3, new[] { "X", "Y", "Z" }, (v, d) => { d.Add(v.X); d.Add(v.Y); d.Add(v.Z); });
            var nrmSource = CreateFloatSource(geometryId + "_nrm", sub.Normals, 3, new[] { "X", "Y", "Z" }, (v, d) => { d.Add(v.X); d.Add(v.Y); d.Add(v.Z); });
            var uvSource = CreateUvSource(geometryId + "_uv0", sub.UVs);
            var clrSource = CreateColorSource(geometryId + "_clr", sub.Colors);

            geometry.Mesh.Sources.Add(posSource);
            geometry.Mesh.Sources.Add(nrmSource);
            geometry.Mesh.Sources.Add(uvSource);
            geometry.Mesh.Sources.Add(clrSource);

            geometry.Mesh.Vertices.Id = geometryId + "_verts";
            geometry.Mesh.Vertices.Inputs.Add(new ColladaInput { Semantic = SemanticType.POSITION, Source = "#" + posSource.Id });

            // Material binding
            string materialSymbol = $"Mat{index}";
            string materialId = SanitizeName(sub.MaterialName);
            if (string.IsNullOrEmpty(materialId)) materialId = $"Material_{index}";

            // Triangles
            int triCount = (int)(sub.Indices.Length / 3);
            int inputCount = 4; // VERTEX, NORMAL, TEXCOORD, COLOR

            var poly = new ColladaPolygons
            {
                Count = triCount,
                MaterialSymbol = materialSymbol,
                RemappedIndices = RemapIndices(sub.Indices, inputCount)
            };

            int offset = 0;
            poly.Inputs.Add(new ColladaInput { Semantic = SemanticType.VERTEX, Source = "#" + geometry.Mesh.Vertices.Id, Offset = offset++ });
            poly.Inputs.Add(new ColladaInput { Semantic = SemanticType.NORMAL, Source = "#" + nrmSource.Id, Offset = offset++ });
            poly.Inputs.Add(new ColladaInput { Semantic = SemanticType.TEXCOORD, Source = "#" + uvSource.Id, Offset = offset++, Set = 0 });
            poly.Inputs.Add(new ColladaInput { Semantic = SemanticType.COLOR, Source = "#" + clrSource.Id, Offset = offset++, Set = 0 });

            geometry.Mesh.Polygons.Add(poly);
            _geometries.Add(geometry);

            // Material / Effect / Image
            if (_seenMaterialIds.Add(materialId))
            {
                string effectId = $"Effect_{materialId}";
                _materials.Add(new ColladaMaterial { Id = materialId, Name = sub.MaterialName, EffectUrl = "#" + effectId });

                // Find first texture from Trinity material
                string? texPath = FindTextureForMaterial(sub.MaterialName);
                string imageName = !string.IsNullOrEmpty(texPath) ? Path.GetFileNameWithoutExtension(texPath) : "DefaultTexture";
                string imageId = $"Image_{SanitizeName(imageName)}";

                if (_seenImageIds.Add(imageId))
                {
                    _images.Add(new ColladaImage
                    {
                        Id = imageId,
                        Name = imageName,
                        InitFrom = "textures/" + imageName + ".png"
                    });
                }

                _effects.Add(new ColladaEffect
                {
                    Id = effectId,
                    Name = $"{materialId}-effect",
                    SurfaceSid = $"{effectId}-surface",
                    SamplerSid = $"{effectId}-sampler",
                    ImageId = imageId
                });
            }

            // Scene node
            bool hasSkeleton = _data.Armature != null && _data.Armature.Bones.Count > 0 && sub.HasSkinning;
            var sceneNode = new ColladaNode
            {
                Id = $"Node_{index}",
                Name = sub.Name,
                NodeType = "NODE",
                Transform = Matrix4.Identity,
                InstanceType = hasSkeleton ? "instance_controller" : "instance_geometry",
                InstanceUrl = hasSkeleton ? $"#Controller_{index}" : $"#{geometryId}",
                MaterialSymbol = materialSymbol,
                MaterialTarget = $"#{materialId}",
                SkeletonRootId = hasSkeleton ? GetRootBoneId() : ""
            };
            _sceneNodes.Add(sceneNode);

            // Skinning controller
            if (hasSkeleton)
            {
                var controller = CreateController(geometryId, index, sub);
                _controllers.Add(controller);
            }
        }

        #endregion

        #region Source Builders

        private static ColladaSource CreateFloatSource<T>(string id, T[] data, int stride, string[] paramNames, Action<T, List<float>> flatten)
        {
            var floats = new List<float>(data.Length * stride);
            foreach (var item in data) flatten(item, floats);
            return new ColladaSource
            {
                Id = id,
                Data = floats.ToArray(),
                Stride = stride,
                AccessorParams = paramNames.ToList()
            };
        }

        private static ColladaSource CreateUvSource(string id, Vector2[] uvs)
        {
            var data = new float[uvs.Length * 2];
            for (int i = 0; i < uvs.Length; i++)
            {
                data[i * 2] = uvs[i].X;
                data[i * 2 + 1] = uvs[i].Y;
            }
            return new ColladaSource { Id = id, Data = data, Stride = 2, AccessorParams = new List<string> { "S", "T" } };
        }

        private static ColladaSource CreateColorSource(string id, Vector4[] colors)
        {
            var data = new float[colors.Length * 4];
            for (int i = 0; i < colors.Length; i++)
            {
                data[i * 4] = colors[i].X;
                data[i * 4 + 1] = colors[i].Y;
                data[i * 4 + 2] = colors[i].Z;
                data[i * 4 + 3] = colors[i].W;
            }
            return new ColladaSource { Id = id, Data = data, Stride = 4, AccessorParams = new List<string> { "R", "G", "B", "A" } };
        }

        private static int[] RemapIndices(uint[] indices, int inputCount)
        {
            var result = new int[indices.Length * inputCount];
            for (int i = 0; i < indices.Length; i++)
            {
                int idx = (int)indices[i];
                for (int j = 0; j < inputCount; j++)
                    result[i * inputCount + j] = idx;
            }
            return result;
        }

        #endregion

        #region Skinning Controller

        private ColladaController CreateController(string geometryId, int index, TrinityModelDecoder.ExportSubmesh sub)
        {
            var armature = _data.Armature!;
            var controller = new ColladaController
            {
                Id = $"Controller_{index}",
                Skin = new ColladaSkin { Source = "#" + geometryId, BindShapeMatrix = Matrix4.Identity }
            };

            // Joint names
            var boneNames = armature.Bones.Select(b => b.Name).ToArray();
            var jointsSource = new ColladaSource
            {
                Id = $"Controller_{index}_joints",
                DataString = boneNames,
                Stride = 1,
                AccessorParams = new List<string> { "JOINT" },
                IsNameArray = true
            };

            // Inverse bind matrices
            var invBindData = new float[armature.Bones.Count * 16];
            for (int i = 0; i < armature.Bones.Count; i++)
            {
                var m = armature.Bones[i].InverseBindWorld;
                int b = i * 16;
                invBindData[b + 0] = m.M11; invBindData[b + 1] = m.M21; invBindData[b + 2] = m.M31; invBindData[b + 3] = m.M41;
                invBindData[b + 4] = m.M12; invBindData[b + 5] = m.M22; invBindData[b + 6] = m.M32; invBindData[b + 7] = m.M42;
                invBindData[b + 8] = m.M13; invBindData[b + 9] = m.M23; invBindData[b + 10] = m.M33; invBindData[b + 11] = m.M43;
                invBindData[b + 12] = m.M14; invBindData[b + 13] = m.M24; invBindData[b + 14] = m.M34; invBindData[b + 15] = m.M44;
            }

            var transformSource = new ColladaSource
            {
                Id = $"Controller_{index}_trans",
                Data = invBindData,
                Stride = 16,
                AccessorParams = new List<string> { "TRANSFORM" }
            };

            // Weights — collect unique weights
            var uniqueWeights = new List<float> { 1.0f };
            var weightSet = new HashSet<float> { 1.0f };
            foreach (var w in sub.BlendWeights)
            {
                AddUniqueWeight(w.X, uniqueWeights, weightSet);
                AddUniqueWeight(w.Y, uniqueWeights, weightSet);
                AddUniqueWeight(w.Z, uniqueWeights, weightSet);
                AddUniqueWeight(w.W, uniqueWeights, weightSet);
            }

            var weightsSource = new ColladaSource
            {
                Id = $"Controller_{index}_weights",
                Data = uniqueWeights.ToArray(),
                Stride = 1,
                AccessorParams = new List<string> { "WEIGHT" }
            };

            controller.Skin.Sources.Add(jointsSource);
            controller.Skin.Sources.Add(transformSource);
            controller.Skin.Sources.Add(weightsSource);

            // Joints element
            controller.Skin.Joints.Inputs.Add(new ColladaInput { Semantic = SemanticType.JOINT, Source = "#" + jointsSource.Id });
            controller.Skin.Joints.Inputs.Add(new ColladaInput { Semantic = SemanticType.INV_BIND_MATRIX, Source = "#" + transformSource.Id });

            // Vertex weights
            controller.Skin.VertexWeights.Count = sub.BlendIndices.Length;
            controller.Skin.VertexWeights.Inputs.Add(new ColladaInput { Semantic = SemanticType.JOINT, Source = "#" + jointsSource.Id, Offset = 0 });
            controller.Skin.VertexWeights.Inputs.Add(new ColladaInput { Semantic = SemanticType.WEIGHT, Source = "#" + weightsSource.Id, Offset = 1 });

            BuildVertexWeights(controller.Skin.VertexWeights, sub, uniqueWeights);

            return controller;
        }

        private static void AddUniqueWeight(float weight, List<float> list, HashSet<float> set)
        {
            if (weight > 0 && set.Add(weight))
                list.Add(weight);
        }

        private static void BuildVertexWeights(ColladaVertexWeights vws, TrinityModelDecoder.ExportSubmesh sub, List<float> uniqueWeights)
        {
            var weightIndexMap = new Dictionary<float, int>();
            for (int i = 0; i < uniqueWeights.Count; i++)
                weightIndexMap[uniqueWeights[i]] = i;

            var vcount = new List<int>();
            var v = new List<int>();

            for (int vi = 0; vi < sub.BlendIndices.Length; vi++)
            {
                var idx = sub.BlendIndices[vi];
                var w = vi < sub.BlendWeights.Length ? sub.BlendWeights[vi] : Vector4.Zero;

                int count = 0;
                TryAddWeight(v, ref count, (int)MathF.Round(idx.X), w.X, weightIndexMap);
                TryAddWeight(v, ref count, (int)MathF.Round(idx.Y), w.Y, weightIndexMap);
                TryAddWeight(v, ref count, (int)MathF.Round(idx.Z), w.Z, weightIndexMap);
                TryAddWeight(v, ref count, (int)MathF.Round(idx.W), w.W, weightIndexMap);

                if (count == 0)
                {
                    v.Add(0);
                    v.Add(0);
                    count = 1;
                }

                vcount.Add(count);
            }

            vws.VCount = vcount.ToArray();
            vws.V = v.ToArray();
        }

        private static void TryAddWeight(List<int> v, ref int count, int boneIndex, float weight, Dictionary<float, int> weightMap)
        {
            if (weight <= 0) return;
            v.Add(boneIndex);
            v.Add(weightMap.TryGetValue(weight, out int wi) ? wi : 0);
            count++;
        }

        #endregion

        #region Material Lookup

        private string? FindTextureForMaterial(string materialName)
        {
            if (_data.Materials == null) return null;
            foreach (var mat in _data.Materials)
            {
                if (string.Equals(mat.Name, materialName, StringComparison.OrdinalIgnoreCase) && mat.Textures.Count > 0)
                    return mat.Textures[0].FilePath;
            }
            return null;
        }

        #endregion

        #region XML Write

        private void Write(string filename)
        {
            var doc = new XmlDocument();
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));

            var collada = doc.CreateElement("COLLADA");
            collada.Attributes.Append(Attr(doc, "xmlns", "http://www.collada.org/2005/11/COLLADASchema"));
            collada.Attributes.Append(Attr(doc, "version", "1.4.1"));
            doc.AppendChild(collada);

            WriteAsset(doc, collada);
            WriteLibrary(doc, collada, "library_images", _images);
            WriteLibrary(doc, collada, "library_materials", _materials);
            WriteLibrary(doc, collada, "library_effects", _effects);
            if (_geometries.Count > 0) WriteLibrary(doc, collada, "library_geometries", _geometries);
            if (_controllers.Count > 0) WriteLibrary(doc, collada, "library_controllers", _controllers);
            if (_animation != null) WriteLibraryAnimations(doc, collada);
            WriteVisualScenes(doc, collada);
            WriteScene(doc, collada);

            var dir = Path.GetDirectoryName(filename);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            doc.Save(filename);
        }

        private static void WriteAsset(XmlDocument doc, XmlNode parent)
        {
            var asset = doc.CreateElement("asset");
            parent.AppendChild(asset);
            var created = doc.CreateElement("created"); created.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"); asset.AppendChild(created);
            var modified = doc.CreateElement("modified"); modified.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"); asset.AppendChild(modified);
            var upAxis = doc.CreateElement("up_axis"); upAxis.InnerText = "Y_UP"; asset.AppendChild(upAxis);
        }

        private static void WriteLibrary<T>(XmlDocument doc, XmlNode parent, string elementName, List<T> items) where T : class
        {
            var lib = doc.CreateElement(elementName);
            parent.AppendChild(lib);
            foreach (var item in items)
            {
                var method = item.GetType().GetMethod("Write");
                method?.Invoke(item, new object[] { doc, lib });
            }
        }

        private void WriteVisualScenes(XmlDocument doc, XmlNode parent)
        {
            var lib = doc.CreateElement("library_visual_scenes");
            parent.AppendChild(lib);

            var scene = doc.CreateElement("visual_scene");
            scene.Attributes.Append(Attr(doc, "id", "Scene"));
            scene.Attributes.Append(Attr(doc, "name", _data.Name));
            lib.AppendChild(scene);

            foreach (var node in _sceneNodes)
                node.Write(doc, scene);
        }

        private static void WriteScene(XmlDocument doc, XmlNode parent)
        {
            var scene = doc.CreateElement("scene");
            parent.AppendChild(scene);
            var inst = doc.CreateElement("instance_visual_scene");
            inst.Attributes.Append(Attr(doc, "url", "#Scene"));
            scene.AppendChild(inst);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Mesh";
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name) sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            return sb.ToString();
        }

        private static XmlAttribute Attr(XmlDocument doc, string n, string v)
        {
            var a = doc.CreateAttribute(n); a.Value = v; return a;
        }

        #endregion

        #region Animation Export

        private void WriteLibraryAnimations(XmlDocument doc, XmlNode parent)
        {
            if (_animation == null || _data.Armature == null || _data.Armature.Bones.Count == 0) return;

            var lib = doc.CreateElement("library_animations");
            parent.AppendChild(lib);

            var armature = _data.Armature;
            uint frameCount = _animation.FrameCount;
            if (frameCount == 0) return;

            float frameRate = _animation.FrameRate > 0 ? _animation.FrameRate : 30f;

            foreach (var bone in armature.Bones)
            {
                string boneId = $"{bone.Name}_id";
                string animId = $"{boneId}_transform";

                var animElement = doc.CreateElement("animation");
                animElement.Attributes.Append(Attr(doc, "id", animId));
                animElement.Attributes.Append(Attr(doc, "name", $"{bone.Name}_transform"));
                lib.AppendChild(animElement);

                // Time input source
                string inputId = $"{animId}_input";
                var timeValues = new StringBuilder();
                for (uint i = 0; i < frameCount; i++)
                    timeValues.Append(FmtFloat(i / frameRate)).Append(' ');

                WriteAnimSource(doc, animElement, inputId, (int)frameCount, 1, timeValues.ToString().TrimEnd(), "TIME", "float");

                // Matrix output source — bake SRT at each frame
                string outputId = $"{animId}_output";
                var matrixValues = new StringBuilder();
                for (uint i = 0; i < frameCount; i++)
                {
                    var mat = ComputeBoneMatrix(bone, i);
                    AppendMatrix(matrixValues, mat);
                }

                WriteAnimSource(doc, animElement, outputId, (int)frameCount, 16, matrixValues.ToString().TrimEnd(), "TRANSFORM", "float4x4");

                // Interpolation source
                string interpId = $"{animId}_interp";
                var interpValues = new StringBuilder();
                for (uint i = 0; i < frameCount; i++) interpValues.Append("LINEAR ");

                WriteAnimSourceName(doc, animElement, interpId, (int)frameCount, interpValues.ToString().TrimEnd(), "INTERPOLATION");

                // Sampler
                var sampler = doc.CreateElement("sampler");
                sampler.Attributes.Append(Attr(doc, "id", $"{animId}_sampler"));
                animElement.AppendChild(sampler);
                AppendSamplerInput(doc, sampler, "INPUT", inputId);
                AppendSamplerInput(doc, sampler, "OUTPUT", outputId);
                AppendSamplerInput(doc, sampler, "INTERPOLATION", interpId);

                // Channel targets bone node
                var channel = doc.CreateElement("channel");
                channel.Attributes.Append(Attr(doc, "source", $"#{animId}_sampler"));
                channel.Attributes.Append(Attr(doc, "target", $"{boneId}/transform"));
                animElement.AppendChild(channel);
            }
        }

        private Matrix4 ComputeBoneMatrix(TrinityArmature.Bone bone, float frame)
        {
            // Start with rest pose
            var sca = bone.RestScale;
            var rot = bone.RestRotation;
            var pos = bone.RestPosition;

            // Override with animation data if available
            if (_animation != null && _animation.TryGetPose(bone.Name, frame, out var animScale, out var animRot, out var animTrans))
            {
                if (animScale.HasValue) sca = animScale.Value;
                if (animRot.HasValue) rot = animRot.Value;
                if (animTrans.HasValue) pos = animTrans.Value;
            }

            // Build TRS matrix (Scale * Rotation * Translation)
            var matS = Matrix4.CreateScale(sca);
            var matR = Matrix4.CreateFromQuaternion(rot);
            var matT = Matrix4.CreateTranslation(pos);
            return matS * matR * matT;
        }

        private static void AppendMatrix(StringBuilder sb, Matrix4 m)
        {
            // Column-major for COLLADA
            sb.Append(FmtFloat(m.M11)).Append(' ').Append(FmtFloat(m.M21)).Append(' ').Append(FmtFloat(m.M31)).Append(' ').Append(FmtFloat(m.M41)).Append(' ');
            sb.Append(FmtFloat(m.M12)).Append(' ').Append(FmtFloat(m.M22)).Append(' ').Append(FmtFloat(m.M32)).Append(' ').Append(FmtFloat(m.M42)).Append(' ');
            sb.Append(FmtFloat(m.M13)).Append(' ').Append(FmtFloat(m.M23)).Append(' ').Append(FmtFloat(m.M33)).Append(' ').Append(FmtFloat(m.M43)).Append(' ');
            sb.Append(FmtFloat(m.M14)).Append(' ').Append(FmtFloat(m.M24)).Append(' ').Append(FmtFloat(m.M34)).Append(' ').Append(FmtFloat(m.M44)).Append(' ');
        }

        private static string FmtFloat(float f) => f.ToString("0.######", CultureInfo.InvariantCulture);

        private static void WriteAnimSource(XmlDocument doc, XmlNode parent, string id, int count, int stride, string data, string paramName, string paramType)
        {
            var source = doc.CreateElement("source");
            source.Attributes.Append(Attr(doc, "id", id));
            parent.AppendChild(source);

            var arr = doc.CreateElement("float_array");
            arr.Attributes.Append(Attr(doc, "id", $"{id}-array"));
            arr.Attributes.Append(Attr(doc, "count", (count * stride).ToString()));
            arr.InnerText = data;
            source.AppendChild(arr);

            var tc = doc.CreateElement("technique_common"); source.AppendChild(tc);
            var accessor = doc.CreateElement("accessor");
            accessor.Attributes.Append(Attr(doc, "source", $"#{id}-array"));
            accessor.Attributes.Append(Attr(doc, "count", count.ToString()));
            accessor.Attributes.Append(Attr(doc, "stride", stride.ToString()));
            tc.AppendChild(accessor);

            var param = doc.CreateElement("param");
            param.Attributes.Append(Attr(doc, "name", paramName));
            param.Attributes.Append(Attr(doc, "type", paramType));
            accessor.AppendChild(param);
        }

        private static void WriteAnimSourceName(XmlDocument doc, XmlNode parent, string id, int count, string data, string paramName)
        {
            var source = doc.CreateElement("source");
            source.Attributes.Append(Attr(doc, "id", id));
            parent.AppendChild(source);

            var arr = doc.CreateElement("Name_array");
            arr.Attributes.Append(Attr(doc, "id", $"{id}-array"));
            arr.Attributes.Append(Attr(doc, "count", count.ToString()));
            arr.InnerText = data;
            source.AppendChild(arr);

            var tc = doc.CreateElement("technique_common"); source.AppendChild(tc);
            var accessor = doc.CreateElement("accessor");
            accessor.Attributes.Append(Attr(doc, "source", $"#{id}-array"));
            accessor.Attributes.Append(Attr(doc, "count", count.ToString()));
            accessor.Attributes.Append(Attr(doc, "stride", "1"));
            tc.AppendChild(accessor);

            var param = doc.CreateElement("param");
            param.Attributes.Append(Attr(doc, "name", paramName));
            param.Attributes.Append(Attr(doc, "type", "Name"));
            accessor.AppendChild(param);
        }

        private static void AppendSamplerInput(XmlDocument doc, XmlNode sampler, string semantic, string sourceId)
        {
            var input = doc.CreateElement("input");
            input.Attributes.Append(Attr(doc, "semantic", semantic));
            input.Attributes.Append(Attr(doc, "source", $"#{sourceId}"));
            sampler.AppendChild(input);
        }

        #endregion
    }
}
