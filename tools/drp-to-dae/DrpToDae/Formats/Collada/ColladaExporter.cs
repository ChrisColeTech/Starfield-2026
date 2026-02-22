using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Xml;
using DrpToDae.Formats.Animation;
using NudModel = DrpToDae.Formats.NUD.NUD;
using NudMesh = DrpToDae.Formats.NUD.Mesh;
using NudPolygon = DrpToDae.Formats.NUD.Polygon;
using NudVertex = DrpToDae.Formats.NUD.Vertex;
using VbnSkeleton = DrpToDae.Formats.VBN.VBN;
using VbnBone = DrpToDae.Formats.VBN.Bone;

namespace DrpToDae.Formats.Collada
{
    public static class ColladaExporter
    {
        /// <summary>
        /// Export model without animation (split mode - model.dae)
        /// </summary>
        public static void Export(string outputPath, NudModel model, VbnSkeleton? skeleton = null)
        {
            var exporter = new ColladaExportContext(model, skeleton, null);
            exporter.Export(outputPath);
        }

        /// <summary>
        /// Export model with baked animation (baked mode - one DAE with model + animation)
        /// </summary>
        public static void ExportWithAnimation(string outputPath, NudModel model, VbnSkeleton? skeleton, AnimationData animation)
        {
            var exporter = new ColladaExportContext(model, skeleton, animation);
            exporter.Export(outputPath);
        }
    }

    internal class ColladaExportContext
    {
        private readonly NudModel _model;
        private readonly VbnSkeleton? _skeleton;
        private readonly AnimationData? _animation;

        private readonly List<ColladaGeometry> _geometries = new();
        private readonly List<ColladaController> _controllers = new();
        private readonly List<ColladaMaterial> _materials = new();
        private readonly List<ColladaEffect> _effects = new();
        private readonly List<ColladaImage> _images = new();
        private readonly List<ColladaNode> _nodes = new();
        private readonly HashSet<string> _seenImageIds = new();

        public ColladaExportContext(NudModel model, VbnSkeleton? skeleton, AnimationData? animation)
        {
            _model = model;
            _skeleton = skeleton;
            _animation = animation;
        }

        public void Export(string outputPath)
        {
            if (_skeleton != null && _skeleton.Bones.Count > 0)
            {
                ExportBoneNodes(_skeleton.Bones[0], null);
            }

            int meshIndex = 0;
            foreach (var mesh in _model.Meshes)
            {
                foreach (var polygon in mesh.Polygons)
                {
                    ExportPolygon(mesh, polygon, meshIndex);
                    meshIndex++;
                }
            }

            Write(outputPath);
        }

        private void ExportBoneNodes(VbnBone bone, ColladaNode? parent)
        {
            var node = new ColladaNode
            {
                Id = bone.Name + "_id",
                Name = bone.Name,
                NodeType = "JOINT",
                Position = bone.Pos,
                Scale = bone.Sca,
                Rotation = ExtractEulerAngles(bone.Rot),
                Transform = CalculateTransformMatrix(bone)
            };

            if (parent != null)
                parent.Children.Add(node);
            else
                _nodes.Add(node);

            foreach (var child in bone.Children)
            {
                ExportBoneNodes(child, node);
            }
        }

        private static Vector3 ExtractEulerAngles(Quaternion q)
        {
            float sinrCosp = 2 * (q.W * q.X + q.Y * q.Z);
            float cosrCosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            float roll = MathF.Atan2(sinrCosp, cosrCosp);

            float sinp = 2 * (q.W * q.Y - q.Z * q.X);
            float pitch;
            if (MathF.Abs(sinp) >= 1)
                pitch = MathF.CopySign(MathF.PI / 2, sinp);
            else
                pitch = MathF.Asin(sinp);

            float sinyCosp = 2 * (q.W * q.Z + q.X * q.Y);
            float cosyCosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            float yaw = MathF.Atan2(sinyCosp, cosyCosp);

            return new Vector3(roll, pitch, yaw);
        }

        private static Matrix4x4 CalculateTransformMatrix(VbnBone bone)
        {
            var scale = Matrix4x4.CreateScale(bone.Sca);
            var rotation = Matrix4x4.CreateFromQuaternion(bone.Rot);
            var translation = Matrix4x4.CreateTranslation(bone.Pos);
            return scale * rotation * translation;
        }

        private string GetRootBoneId()
        {
            if (_skeleton == null || _skeleton.Bones.Count == 0)
                return "";

            // Find first root bone (ParentIndex < 0)
            foreach (var bone in _skeleton.Bones)
            {
                if (bone.ParentIndex < 0)
                    return bone.Name + "_id";
            }

            // Fallback to first bone if no root found
            return _skeleton.Bones[0].Name + "_id";
        }

        private void ExportPolygon(NudMesh mesh, NudPolygon polygon, int index)
        {
            string baseName = SanitizeName(mesh.Name);
            string geometryId = $"{baseName}_{index}";

            var geometry = new ColladaGeometry
            {
                Id = geometryId,
                Name = mesh.Name
            };

            var vertices = polygon.vertices;
            var triangles = polygon.GetTriangles();
            int vertexCount = vertices.Count;

            var posSource = CreatePositionSource(geometryId, vertices);
            var nrmSource = CreateNormalSource(geometryId, vertices);
            var uvSource = CreateUvSource(geometryId, vertices);
            var colorSource = CreateColorSource(geometryId, vertices);

            geometry.Mesh.Sources.Add(posSource);
            geometry.Mesh.Sources.Add(nrmSource);
            geometry.Mesh.Sources.Add(uvSource);
            geometry.Mesh.Sources.Add(colorSource);

            geometry.Mesh.Vertices.Id = geometryId + "_verts";
            geometry.Mesh.Vertices.Inputs.Add(new ColladaInput
            {
                Semantic = SemanticType.POSITION,
                Source = "#" + posSource.Id
            });

            string materialSymbol = $"Mat{index}";

            var colladaPoly = new ColladaPolygons
            {
                Count = triangles.Count / 3,
                Indices = triangles.ToArray(),
                MaterialSymbol = materialSymbol
            };

            int inputOffset = 0;

            colladaPoly.Inputs.Add(new ColladaInput
            {
                Semantic = SemanticType.VERTEX,
                Source = "#" + geometry.Mesh.Vertices.Id,
                Offset = inputOffset++
            });

            colladaPoly.Inputs.Add(new ColladaInput
            {
                Semantic = SemanticType.NORMAL,
                Source = "#" + nrmSource.Id,
                Offset = inputOffset++
            });

            colladaPoly.Inputs.Add(new ColladaInput
            {
                Semantic = SemanticType.TEXCOORD,
                Source = "#" + uvSource.Id,
                Offset = inputOffset++,
                Set = 0
            });

            colladaPoly.Inputs.Add(new ColladaInput
            {
                Semantic = SemanticType.COLOR,
                Source = "#" + colorSource.Id,
                Offset = inputOffset++,
                Set = 0
            });

            colladaPoly.RemappedIndices = RemapIndicesForMultipleInputs(triangles, inputOffset);

            geometry.Mesh.Polygons.Add(colladaPoly);
            _geometries.Add(geometry);

            string materialId = $"Material_{index}";
            string effectId = $"Effect_{index}";

            string textureName = GetTextureName(polygon);
            string imageId = $"Image_{textureName}";

            _materials.Add(new ColladaMaterial
            {
                Id = materialId,
                Name = $"Material_{index}",
                EffectUrl = "#" + effectId
            });

            if (_seenImageIds.Add(imageId))
            {
                _images.Add(new ColladaImage
                {
                    Id = imageId,
                    Name = textureName,
                    InitFrom = "textures/" + textureName + ".png"
                });
            }

            _effects.Add(new ColladaEffect
            {
                Id = effectId,
                Name = $"{baseName}-effect",
                SurfaceSid = $"{effectId}-surface",
                SamplerSid = $"{effectId}-sampler",
                ImageId = imageId
            });

            var sceneNode = new ColladaNode
            {
                Id = $"Node_{index}",
                Name = mesh.Name,
                NodeType = "NODE",
                Transform = Matrix4x4.Identity,
                InstanceType = _skeleton != null ? "instance_controller" : "instance_geometry",
                InstanceUrl = _skeleton != null ? $"#Controller_{index}" : $"#{geometryId}",
                MaterialSymbol = materialSymbol,
                MaterialTarget = $"#{materialId}",
                SkeletonRootId = _skeleton != null ? GetRootBoneId() : ""
            };

            _nodes.Add(sceneNode);

            if (_skeleton != null)
            {
                var controller = CreateController(geometryId, index, polygon);
                _controllers.Add(controller);
            }
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Mesh";

            var result = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(c);
                else
                    result.Append('_');
            }
            return result.ToString();
        }

        private static string GetTextureName(NudPolygon polygon)
        {
            if (polygon.materials.Count > 0 && polygon.materials[0].textures.Count > 0)
            {
                var tex = polygon.materials[0].textures[0];
                return $"Tex_0x{tex.hash:X8}";
            }
            return "DefaultTexture";
        }

        private ColladaSource CreatePositionSource(string baseId, List<NudVertex> vertices)
        {
            var data = new List<float>();
            foreach (var v in vertices)
            {
                data.Add(v.pos.X);
                data.Add(v.pos.Y);
                data.Add(v.pos.Z);
            }

            return new ColladaSource
            {
                Id = baseId + "_pos",
                Data = data.ToArray(),
                Stride = 3,
                AccessorParams = new List<string> { "X", "Y", "Z" }
            };
        }

        private ColladaSource CreateNormalSource(string baseId, List<NudVertex> vertices)
        {
            var data = new List<float>();
            foreach (var v in vertices)
            {
                data.Add(v.nrm.X);
                data.Add(v.nrm.Y);
                data.Add(v.nrm.Z);
            }

            return new ColladaSource
            {
                Id = baseId + "_nrm",
                Data = data.ToArray(),
                Stride = 3,
                AccessorParams = new List<string> { "X", "Y", "Z" }
            };
        }

        private ColladaSource CreateUvSource(string baseId, List<NudVertex> vertices)
        {
            var data = new List<float>();
            foreach (var v in vertices)
            {
                if (v.uv.Count > 0)
                {
                    data.Add(v.uv[0].X);
            data.Add(1.0f - v.uv[0].Y);
                }
                else
                {
                    data.Add(0);
                    data.Add(0);
                }
            }

            return new ColladaSource
            {
                Id = baseId + "_uv0",
                Data = data.ToArray(),
                Stride = 2,
                AccessorParams = new List<string> { "S", "T" }
            };
        }

        private ColladaSource CreateColorSource(string baseId, List<NudVertex> vertices)
        {
            var data = new List<float>();
            foreach (var v in vertices)
            {
                data.Add(v.color.X / 128f);
                data.Add(v.color.Y / 128f);
                data.Add(v.color.Z / 128f);
                data.Add(v.color.W / 128f);
            }

            return new ColladaSource
            {
                Id = baseId + "_clr",
                Data = data.ToArray(),
                Stride = 4,
                AccessorParams = new List<string> { "R", "G", "B", "A" }
            };
        }

        private static int[] RemapIndicesForMultipleInputs(List<int> triangleIndices, int inputCount)
        {
            var result = new List<int>();
            foreach (int idx in triangleIndices)
            {
                for (int i = 0; i < inputCount; i++)
                {
                    result.Add(idx);
                }
            }
            return result.ToArray();
        }

        private ColladaController CreateController(string geometryId, int index, NudPolygon polygon)
        {
            var controller = new ColladaController
            {
                Id = $"Controller_{index}",
                Skin = new ColladaSkin
                {
                    Source = "#" + geometryId,
                    BindShapeMatrix = Matrix4x4.Identity
                }
            };

            var jointsSource = CreateJointsSource(controller.Id);
            var transformSource = CreateTransformSource(controller.Id);
            var weightsSource = CreateWeightsSource(controller.Id, polygon.vertices);

            controller.Skin.Sources.Add(jointsSource);
            controller.Skin.Sources.Add(transformSource);
            controller.Skin.Sources.Add(weightsSource);

            controller.Skin.Joints.Inputs.Add(new ColladaInput
            {
                Semantic = SemanticType.JOINT,
                Source = "#" + jointsSource.Id
            });
            controller.Skin.Joints.Inputs.Add(new ColladaInput
            {
                Semantic = SemanticType.INV_BIND_MATRIX,
                Source = "#" + transformSource.Id
            });

            controller.Skin.VertexWeights.Count = polygon.vertices.Count;
            controller.Skin.VertexWeights.Inputs.Add(new ColladaInput
            {
                Semantic = SemanticType.JOINT,
                Source = "#" + jointsSource.Id,
                Offset = 0
            });
            controller.Skin.VertexWeights.Inputs.Add(new ColladaInput
            {
                Semantic = SemanticType.WEIGHT,
                Source = "#" + weightsSource.Id,
                Offset = 1
            });

            BuildVertexWeights(controller.Skin.VertexWeights, polygon.vertices, weightsSource);

            return controller;
        }

        private ColladaSource CreateJointsSource(string controllerId)
        {
            var names = new List<string>();
            if (_skeleton != null)
            {
                foreach (var bone in _skeleton.Bones)
                {
                    names.Add(bone.Name);
                }
            }
            else
            {
                names.Add("ROOT");
            }

            return new ColladaSource
            {
                Id = controllerId + "_joints",
                DataString = names.ToArray(),
                Stride = 1,
                AccessorParams = new List<string> { "JOINT" },
                IsNameArray = true
            };
        }

        private ColladaSource CreateTransformSource(string controllerId)
        {
            var data = new List<float>();
            if (_skeleton != null)
            {
                foreach (var bone in _skeleton.Bones)
                {
                    var inv = bone.InverseTransform;
                    data.Add(inv.M11); data.Add(inv.M21); data.Add(inv.M31); data.Add(inv.M41);
                    data.Add(inv.M12); data.Add(inv.M22); data.Add(inv.M32); data.Add(inv.M42);
                    data.Add(inv.M13); data.Add(inv.M23); data.Add(inv.M33); data.Add(inv.M43);
                    data.Add(inv.M14); data.Add(inv.M24); data.Add(inv.M34); data.Add(inv.M44);
                }
            }
            else
            {
                var identity = Matrix4x4.Identity;
                data.Add(identity.M11); data.Add(identity.M21); data.Add(identity.M31); data.Add(identity.M41);
                data.Add(identity.M12); data.Add(identity.M22); data.Add(identity.M32); data.Add(identity.M42);
                data.Add(identity.M13); data.Add(identity.M23); data.Add(identity.M33); data.Add(identity.M43);
                data.Add(identity.M14); data.Add(identity.M24); data.Add(identity.M34); data.Add(identity.M44);
            }

            return new ColladaSource
            {
                Id = controllerId + "_trans",
                Data = data.ToArray(),
                Stride = 16,
                AccessorParams = new List<string> { "TRANSFORM" }
            };
        }

        private ColladaSource CreateWeightsSource(string controllerId, List<NudVertex> vertices)
        {
            var uniqueWeights = new List<float> { 1.0f };
            var weightSet = new HashSet<float> { 1.0f };

            foreach (var v in vertices)
            {
                for (int i = 0; i < v.boneWeights.Count && i < 4; i++)
                {
                    float w = v.boneWeights[i];
                    if (w > 0 && !weightSet.Contains(w))
                    {
                        weightSet.Add(w);
                        uniqueWeights.Add(w);
                    }
                }
            }

            return new ColladaSource
            {
                Id = controllerId + "_weights",
                Data = uniqueWeights.ToArray(),
                Stride = 1,
                AccessorParams = new List<string> { "WEIGHT" }
            };
        }

        private void BuildVertexWeights(ColladaVertexWeights vws, List<NudVertex> vertices, ColladaSource weightsSource)
        {
            var vcount = new List<int>();
            var v = new List<int>();
            var weightIndexMap = new Dictionary<float, int>();
            for (int i = 0; i < weightsSource.Data!.Length; i++)
            {
                weightIndexMap[weightsSource.Data[i]] = i;
            }

            foreach (var vertex in vertices)
            {
                int count = 0;
                for (int i = 0; i < vertex.boneIds.Count && i < 4; i++)
                {
                    if (i < vertex.boneWeights.Count)
                    {
                        float weight = vertex.boneWeights[i];
                        if (weight > 0)
                        {
                            v.Add(vertex.boneIds[i]);
                            int weightIdx = weightIndexMap.ContainsKey(weight) ? weightIndexMap[weight] : 0;
                            v.Add(weightIdx);
                            count++;
                        }
                    }
                }

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

        private void Write(string filename)
        {
            var doc = new XmlDocument();
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));

            var colladaNode = doc.CreateElement("COLLADA");
            colladaNode.Attributes.Append(CreateAttribute(doc, "xmlns", "http://www.collada.org/2005/11/COLLADASchema"));
            colladaNode.Attributes.Append(CreateAttribute(doc, "version", "1.4.1"));
            doc.AppendChild(colladaNode);

            WriteAsset(doc, colladaNode);
            WriteLibraryImages(doc, colladaNode);
            WriteLibraryMaterials(doc, colladaNode);
            WriteLibraryEffects(doc, colladaNode);
            WriteLibraryGeometries(doc, colladaNode);
            WriteLibraryControllers(doc, colladaNode);
            if (_animation != null)
                WriteLibraryAnimations(doc, colladaNode);
            WriteLibraryVisualScenes(doc, colladaNode);
            WriteScene(doc, colladaNode);

            doc.Save(filename);
        }

        private static XmlAttribute CreateAttribute(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }

        private void WriteAsset(XmlDocument doc, XmlNode parent)
        {
            var asset = doc.CreateElement("asset");
            parent.AppendChild(asset);

            var created = doc.CreateElement("created");
            created.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            asset.AppendChild(created);

            var modified = doc.CreateElement("modified");
            modified.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            asset.AppendChild(modified);
        }

        private void WriteLibraryImages(XmlDocument doc, XmlNode parent)
        {
            var library = doc.CreateElement("library_images");
            parent.AppendChild(library);

            foreach (var image in _images)
            {
                image.Write(doc, library);
            }
        }

        private void WriteLibraryMaterials(XmlDocument doc, XmlNode parent)
        {
            var library = doc.CreateElement("library_materials");
            parent.AppendChild(library);

            foreach (var material in _materials)
            {
                material.Write(doc, library);
            }
        }

        private void WriteLibraryEffects(XmlDocument doc, XmlNode parent)
        {
            var library = doc.CreateElement("library_effects");
            parent.AppendChild(library);

            foreach (var effect in _effects)
            {
                effect.Write(doc, library);
            }
        }

        private void WriteLibraryGeometries(XmlDocument doc, XmlNode parent)
        {
            var library = doc.CreateElement("library_geometries");
            parent.AppendChild(library);

            foreach (var geometry in _geometries)
            {
                geometry.Write(doc, library);
            }
        }

        private void WriteLibraryControllers(XmlDocument doc, XmlNode parent)
        {
            if (_controllers.Count == 0) return;

            var library = doc.CreateElement("library_controllers");
            parent.AppendChild(library);

            foreach (var controller in _controllers)
            {
                controller.Write(doc, library);
            }
        }

        private void WriteLibraryAnimations(XmlDocument doc, XmlNode parent)
        {
            if (_animation == null || _skeleton == null || _skeleton.Bones.Count == 0) return;

            var library = doc.CreateElement("library_animations");
            parent.AppendChild(library);

            // Iterate over ALL skeleton bones, not just animated ones
            // This ensures every bone has animation data (either from animation or bind pose)
            foreach (var skelBone in _skeleton.Bones)
            {
                // Find matching animation bone by name
                KeyNode? animBone = null;
                foreach (var ab in _animation.Bones)
                {
                    if (ab.Name == skelBone.Name)
                    {
                        animBone = ab;
                        break;
                    }
                }

                string boneId = $"{skelBone.Name}_id";
                string animId = $"{boneId}_transform";

                var animElement = doc.CreateElement("animation");
                animElement.Attributes.Append(CreateAttribute(doc, "id", animId));
                animElement.Attributes.Append(CreateAttribute(doc, "name", $"{skelBone.Name}_transform"));
                library.AppendChild(animElement);

                // Time input source
                string inputId = $"{animId}_input";
                var timeValues = new StringBuilder();
                for (int i = 0; i < _animation.FrameCount; i++)
                    timeValues.Append(FormatFloat(i / 30f)).Append(' ');

                WriteAnimationSource(doc, animElement, inputId, _animation.FrameCount, 1,
                    timeValues.ToString().Trim(), "TIME", "float");

                // Matrix output source
                string outputId = $"{animId}_output";
                var matrixValues = new StringBuilder();

                for (int i = 0; i < _animation.FrameCount; i++)
                {
                    float[] mat = ComputeAnimationMatrix(animBone, i, skelBone);
                    for (int m = 0; m < 16; m++)
                        matrixValues.Append(FormatFloat(mat[m])).Append(' ');
                }

                WriteAnimationSource(doc, animElement, outputId, _animation.FrameCount, 16,
                    matrixValues.ToString().Trim(), "TRANSFORM", "float4x4");

                // Interpolation source
                string interpId = $"{animId}_interp";
                var interpValues = new StringBuilder();
                for (int i = 0; i < _animation.FrameCount; i++)
                    interpValues.Append("LINEAR ");

                WriteAnimationSourceName(doc, animElement, interpId, _animation.FrameCount,
                    interpValues.ToString().Trim(), "INTERPOLATION");

                // Sampler
                var sampler = doc.CreateElement("sampler");
                sampler.Attributes.Append(CreateAttribute(doc, "id", $"{animId}_sampler"));
                animElement.AppendChild(sampler);

                var inputSemantic = doc.CreateElement("input");
                inputSemantic.Attributes.Append(CreateAttribute(doc, "semantic", "INPUT"));
                inputSemantic.Attributes.Append(CreateAttribute(doc, "source", $"#{inputId}"));
                sampler.AppendChild(inputSemantic);

                var outputSemantic = doc.CreateElement("input");
                outputSemantic.Attributes.Append(CreateAttribute(doc, "semantic", "OUTPUT"));
                outputSemantic.Attributes.Append(CreateAttribute(doc, "source", $"#{outputId}"));
                sampler.AppendChild(outputSemantic);

                var interpSemantic = doc.CreateElement("input");
                interpSemantic.Attributes.Append(CreateAttribute(doc, "semantic", "INTERPOLATION"));
                interpSemantic.Attributes.Append(CreateAttribute(doc, "source", $"#{interpId}"));
                sampler.AppendChild(interpSemantic);

                // Channel - targets bone by node id (must match the node's id attribute)
                var channel = doc.CreateElement("channel");
                channel.Attributes.Append(CreateAttribute(doc, "source", $"#{animId}_sampler"));
                channel.Attributes.Append(CreateAttribute(doc, "target", $"{boneId}/transform"));
                animElement.AppendChild(channel);
            }
        }

        private void WriteAnimationSource(XmlDocument doc, XmlNode parent, string id, int count, int stride,
            string data, string paramName, string paramType)
        {
            var source = doc.CreateElement("source");
            source.Attributes.Append(CreateAttribute(doc, "id", id));
            parent.AppendChild(source);

            var array = doc.CreateElement("float_array");
            array.Attributes.Append(CreateAttribute(doc, "id", $"{id}-array"));
            array.Attributes.Append(CreateAttribute(doc, "count", (count * stride).ToString()));
            array.InnerText = data;
            source.AppendChild(array);

            var technique = doc.CreateElement("technique_common");
            source.AppendChild(technique);

            var accessor = doc.CreateElement("accessor");
            accessor.Attributes.Append(CreateAttribute(doc, "source", $"#{id}-array"));
            accessor.Attributes.Append(CreateAttribute(doc, "count", count.ToString()));
            accessor.Attributes.Append(CreateAttribute(doc, "stride", stride.ToString()));
            technique.AppendChild(accessor);

            var param = doc.CreateElement("param");
            param.Attributes.Append(CreateAttribute(doc, "name", paramName));
            param.Attributes.Append(CreateAttribute(doc, "type", paramType));
            accessor.AppendChild(param);
        }

        private void WriteAnimationSourceName(XmlDocument doc, XmlNode parent, string id, int count,
            string data, string paramName)
        {
            var source = doc.CreateElement("source");
            source.Attributes.Append(CreateAttribute(doc, "id", id));
            parent.AppendChild(source);

            var array = doc.CreateElement("Name_array");
            array.Attributes.Append(CreateAttribute(doc, "id", $"{id}-array"));
            array.Attributes.Append(CreateAttribute(doc, "count", count.ToString()));
            array.InnerText = data;
            source.AppendChild(array);

            var technique = doc.CreateElement("technique_common");
            source.AppendChild(technique);

            var accessor = doc.CreateElement("accessor");
            accessor.Attributes.Append(CreateAttribute(doc, "source", $"#{id}-array"));
            accessor.Attributes.Append(CreateAttribute(doc, "count", count.ToString()));
            accessor.Attributes.Append(CreateAttribute(doc, "stride", "1"));
            technique.AppendChild(accessor);

            var param = doc.CreateElement("param");
            param.Attributes.Append(CreateAttribute(doc, "name", paramName));
            param.Attributes.Append(CreateAttribute(doc, "type", "Name"));
            accessor.AppendChild(param);
        }

        private static string FormatFloat(float f)
        {
            return f.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static float[] ComputeAnimationMatrix(KeyNode? animBone, int frame, VbnBone skelBone)
        {
            // Use animation data if available, otherwise fall back to skeleton bind pose
            float tx, ty, tz, sx, sy, sz, qx, qy, qz, qw;

            if (animBone != null)
            {
                tx = animBone.XPos.HasAnimation ? animBone.XPos.GetValue(frame) : skelBone.Pos.X;
                ty = animBone.YPos.HasAnimation ? animBone.YPos.GetValue(frame) : skelBone.Pos.Y;
                tz = animBone.ZPos.HasAnimation ? animBone.ZPos.GetValue(frame) : skelBone.Pos.Z;

                sx = animBone.XScale.HasAnimation ? animBone.XScale.GetValue(frame) : skelBone.Sca.X;
                sy = animBone.YScale.HasAnimation ? animBone.YScale.GetValue(frame) : skelBone.Sca.Y;
                sz = animBone.ZScale.HasAnimation ? animBone.ZScale.GetValue(frame) : skelBone.Sca.Z;

                qx = animBone.XRot.HasAnimation ? animBone.XRot.GetValue(frame) : skelBone.Rot.X;
                qy = animBone.YRot.HasAnimation ? animBone.YRot.GetValue(frame) : skelBone.Rot.Y;
                qz = animBone.ZRot.HasAnimation ? animBone.ZRot.GetValue(frame) : skelBone.Rot.Z;
                qw = animBone.WRot.HasAnimation ? animBone.WRot.GetValue(frame) : skelBone.Rot.W;
            }
            else
            {
                // No animation for this bone - use bind pose
                tx = skelBone.Pos.X; ty = skelBone.Pos.Y; tz = skelBone.Pos.Z;
                sx = skelBone.Sca.X; sy = skelBone.Sca.Y; sz = skelBone.Sca.Z;
                qx = skelBone.Rot.X; qy = skelBone.Rot.Y; qz = skelBone.Rot.Z; qw = skelBone.Rot.W;
            }

            // Normalize quaternion to ensure valid rotation
            float len = (float)Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
            if (len > 0.0001f) { qx /= len; qy /= len; qz /= len; qw /= len; }

            // Build rotation matrix from quaternion (same as AnimationExporter)
            float x2 = qx + qx, y2 = qy + qy, z2 = qz + qz;
            float xx = qx * x2, xy = qx * y2, xz = qx * z2;
            float yy = qy * y2, yz = qy * z2, zz = qz * z2;
            float wx = qw * x2, wy = qw * y2, wz = qw * z2;

            // Build rotation matrix with scale applied: Matrix = T * R * S
            float r00 = (1 - yy - zz) * sx;
            float r01 = (xy - wz) * sy;
            float r02 = (xz + wy) * sz;
            float r10 = (xy + wz) * sx;
            float r11 = (1 - xx - zz) * sy;
            float r12 = (yz - wx) * sz;
            float r20 = (xz - wy) * sx;
            float r21 = (yz + wx) * sy;
            float r22 = (1 - xx - yy) * sz;

            // COLLADA column-major: col0, col1, col2, col3
            // Translation goes in column 3 (positions 12-14)
            return new float[]
            {
                r00, r10, r20, 0,   // column 0
                r01, r11, r21, 0,   // column 1
                r02, r12, r22, 0,   // column 2
                tx,  ty,  tz,  1    // column 3 (translation)
            };
        }

        private void WriteLibraryVisualScenes(XmlDocument doc, XmlNode parent)
        {
            var library = doc.CreateElement("library_visual_scenes");
            parent.AppendChild(library);

            var scene = doc.CreateElement("visual_scene");
            scene.Attributes.Append(CreateAttribute(doc, "id", "VisualSceneNode"));
            scene.Attributes.Append(CreateAttribute(doc, "name", "rdmscene"));
            library.AppendChild(scene);

            foreach (var node in _nodes)
            {
                node.Write(doc, scene);
            }
        }

        private void WriteScene(XmlDocument doc, XmlNode parent)
        {
            var scene = doc.CreateElement("scene");
            parent.AppendChild(scene);

            var instanceScene = doc.CreateElement("instance_visual_scene");
            instanceScene.Attributes.Append(CreateAttribute(doc, "url", "#VisualSceneNode"));
            scene.AppendChild(instanceScene);
        }
    }

    internal enum SemanticType
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

    internal class ColladaGeometry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public ColladaMesh Mesh { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("geometry");
            node.Attributes.Append(CreateAttr(doc, "id", Id));
            node.Attributes.Append(CreateAttr(doc, "name", Name));
            parent.AppendChild(node);

            Mesh.Write(doc, node);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaMesh
    {
        public List<ColladaSource> Sources { get; set; } = new();
        public ColladaVertices Vertices { get; set; } = new();
        public List<ColladaPolygons> Polygons { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("mesh");
            parent.AppendChild(node);

            foreach (var source in Sources)
                source.Write(doc, node);

            Vertices.Write(doc, node);

            foreach (var poly in Polygons)
                poly.Write(doc, node);
        }
    }

    internal class ColladaSource
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
            node.Attributes.Append(CreateAttr(doc, "id", Id));
            parent.AppendChild(node);

            if (IsNameArray)
            {
                var arr = doc.CreateElement("Name_array");
                arr.Attributes.Append(CreateAttr(doc, "id", Id + "-array"));
                arr.Attributes.Append(CreateAttr(doc, "count", (DataString?.Length ?? 0).ToString()));
                arr.InnerText = DataString != null ? string.Join(" ", DataString) : "";
                node.AppendChild(arr);
            }
            else
            {
                var arr = doc.CreateElement("float_array");
                arr.Attributes.Append(CreateAttr(doc, "id", Id + "-array"));
                arr.Attributes.Append(CreateAttr(doc, "count", (Data?.Length ?? 0).ToString()));
                arr.InnerText = Data != null ? string.Join(" ", Array.ConvertAll(Data, f => FormatFloat(f))) : "";
                node.AppendChild(arr);
            }

            var tc = doc.CreateElement("technique_common");
            node.AppendChild(tc);

            var accessor = doc.CreateElement("accessor");
            accessor.Attributes.Append(CreateAttr(doc, "source", "#" + Id + "-array"));
            accessor.Attributes.Append(CreateAttr(doc, "count", ((Data?.Length ?? DataString?.Length ?? 0) / Math.Max(1, Stride)).ToString()));
            accessor.Attributes.Append(CreateAttr(doc, "stride", Stride.ToString()));
            tc.AppendChild(accessor);

            foreach (var param in AccessorParams)
            {
                var pa = doc.CreateElement("param");
                pa.Attributes.Append(CreateAttr(doc, "name", param));
                if (param == "TRANSFORM")
                    pa.Attributes.Append(CreateAttr(doc, "type", "float4x4"));
                else if (IsNameArray)
                    pa.Attributes.Append(CreateAttr(doc, "type", "Name"));
                else
                    pa.Attributes.Append(CreateAttr(doc, "type", "float"));
                accessor.AppendChild(pa);
            }
        }

        private static string FormatFloat(float f)
        {
            return f.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaVertices
    {
        public string Id { get; set; } = "";
        public List<ColladaInput> Inputs { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("vertices");
            node.Attributes.Append(CreateAttr(doc, "id", Id));
            parent.AppendChild(node);

            foreach (var input in Inputs)
                input.Write(doc, node);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaPolygons
    {
        public int Count { get; set; }
        public int[]? Indices { get; set; }
        public int[]? RemappedIndices { get; set; }
        public string MaterialSymbol { get; set; } = "";
        public List<ColladaInput> Inputs { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("triangles");
            node.Attributes.Append(CreateAttr(doc, "count", Count.ToString()));
            if (!string.IsNullOrEmpty(MaterialSymbol))
                node.Attributes.Append(CreateAttr(doc, "material", MaterialSymbol));
            parent.AppendChild(node);

            foreach (var input in Inputs)
                input.Write(doc, node);

            var p = doc.CreateElement("p");
            p.InnerText = RemappedIndices != null ? string.Join(" ", RemappedIndices) : "";
            node.AppendChild(p);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaInput
    {
        public SemanticType Semantic { get; set; }
        public string Source { get; set; } = "";
        public int Offset { get; set; }
        public int Set { get; set; } = -1;

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("input");
            node.Attributes.Append(CreateAttr(doc, "semantic", Semantic.ToString()));
            node.Attributes.Append(CreateAttr(doc, "source", Source));
            node.Attributes.Append(CreateAttr(doc, "offset", Offset.ToString()));
            if (Set >= 0)
                node.Attributes.Append(CreateAttr(doc, "set", Set.ToString()));
            parent.AppendChild(node);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaMaterial
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string EffectUrl { get; set; } = "";

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("material");
            node.Attributes.Append(CreateAttr(doc, "id", Id));
            if (!string.IsNullOrEmpty(Name))
                node.Attributes.Append(CreateAttr(doc, "name", Name));
            parent.AppendChild(node);

            var instance = doc.CreateElement("instance_effect");
            instance.Attributes.Append(CreateAttr(doc, "url", EffectUrl));
            node.AppendChild(instance);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaImage
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string InitFrom { get; set; } = "";

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("image");
            node.Attributes.Append(CreateAttr(doc, "id", Id));
            node.Attributes.Append(CreateAttr(doc, "name", Name));
            parent.AppendChild(node);

            var init = doc.CreateElement("init_from");
            init.InnerText = InitFrom;
            node.AppendChild(init);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaEffect
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string SurfaceSid { get; set; } = "";
        public string SamplerSid { get; set; } = "";
        public string ImageId { get; set; } = "";

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("effect");
            node.Attributes.Append(CreateAttr(doc, "id", Id));
            node.Attributes.Append(CreateAttr(doc, "name", Name));
            parent.AppendChild(node);

            var profile = doc.CreateElement("profile_COMMON");
            node.AppendChild(profile);

            var np1 = doc.CreateElement("newparam");
            np1.Attributes.Append(CreateAttr(doc, "sid", SurfaceSid));
            profile.AppendChild(np1);

            var surface = doc.CreateElement("surface");
            surface.Attributes.Append(CreateAttr(doc, "type", "2D"));
            np1.AppendChild(surface);

            var initFrom = doc.CreateElement("init_from");
            initFrom.InnerText = ImageId;
            surface.AppendChild(initFrom);

            var np2 = doc.CreateElement("newparam");
            np2.Attributes.Append(CreateAttr(doc, "sid", SamplerSid));
            profile.AppendChild(np2);

            var sampler = doc.CreateElement("sampler2D");
            np2.AppendChild(sampler);

            var src = doc.CreateElement("source");
            src.InnerText = SurfaceSid;
            sampler.AppendChild(src);

            var technique = doc.CreateElement("technique");
            technique.Attributes.Append(CreateAttr(doc, "sid", "COMMON"));
            profile.AppendChild(technique);

            var phong = doc.CreateElement("phong");
            technique.AppendChild(phong);

            var diffuse = doc.CreateElement("diffuse");
            phong.AppendChild(diffuse);

            var texture = doc.CreateElement("texture");
            texture.Attributes.Append(CreateAttr(doc, "texture", SamplerSid));
            texture.Attributes.Append(CreateAttr(doc, "texcoord", "CHANNEL0"));
            diffuse.AppendChild(texture);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaController
    {
        public string Id { get; set; } = "";
        public ColladaSkin Skin { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("controller");
            node.Attributes.Append(CreateAttr(doc, "id", Id));
            parent.AppendChild(node);

            Skin.Write(doc, node);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaSkin
    {
        public string Source { get; set; } = "";
        public Matrix4x4 BindShapeMatrix { get; set; } = Matrix4x4.Identity;
        public List<ColladaSource> Sources { get; set; } = new();
        public ColladaJoints Joints { get; set; } = new();
        public ColladaVertexWeights VertexWeights { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("skin");
            node.Attributes.Append(CreateAttr(doc, "source", Source));
            parent.AppendChild(node);

            var matrix = doc.CreateElement("bind_shape_matrix");
            matrix.InnerText = FormatMatrix(BindShapeMatrix);
            node.AppendChild(matrix);

            foreach (var source in Sources)
                source.Write(doc, node);

            Joints.Write(doc, node);
            VertexWeights.Write(doc, node);
        }

        private static string FormatMatrix(Matrix4x4 m)
        {
            return $"{FormatFloat(m.M11)} {FormatFloat(m.M21)} {FormatFloat(m.M31)} {FormatFloat(m.M41)} " +
                   $"{FormatFloat(m.M12)} {FormatFloat(m.M22)} {FormatFloat(m.M32)} {FormatFloat(m.M42)} " +
                   $"{FormatFloat(m.M13)} {FormatFloat(m.M23)} {FormatFloat(m.M33)} {FormatFloat(m.M43)} " +
                   $"{FormatFloat(m.M14)} {FormatFloat(m.M24)} {FormatFloat(m.M34)} {FormatFloat(m.M44)}";
        }

        private static string FormatFloat(float f)
        {
            return f.ToString("G", CultureInfo.InvariantCulture);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaJoints
    {
        public List<ColladaInput> Inputs { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("joints");
            parent.AppendChild(node);

            foreach (var input in Inputs)
                input.Write(doc, node);
        }
    }

    internal class ColladaVertexWeights
    {
        public int Count { get; set; }
        public List<ColladaInput> Inputs { get; set; } = new();
        public int[]? VCount { get; set; }
        public int[]? V { get; set; }

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("vertex_weights");
            node.Attributes.Append(CreateAttr(doc, "count", Count.ToString()));
            parent.AppendChild(node);

            foreach (var input in Inputs)
                input.Write(doc, node);

            var vcount = doc.CreateElement("vcount");
            vcount.InnerText = VCount != null ? string.Join(" ", VCount) : "";
            node.AppendChild(vcount);

            var v = doc.CreateElement("v");
            v.InnerText = V != null ? string.Join(" ", V) : "";
            node.AppendChild(v);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }

    internal class ColladaNode
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string NodeType { get; set; } = "NODE";
        public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;
        public Vector3 Position { get; set; }
        public Vector3 Scale { get; set; } = Vector3.One;
        public Vector3 Rotation { get; set; }

        public string InstanceType { get; set; } = "";
        public string InstanceUrl { get; set; } = "";
        public string MaterialSymbol { get; set; } = "";
        public string MaterialTarget { get; set; } = "";
        public string SkeletonRootId { get; set; } = "";

        public List<ColladaNode> Children { get; set; } = new();

        public void Write(XmlDocument doc, XmlNode parent)
        {
            var node = doc.CreateElement("node");
            node.Attributes.Append(CreateAttr(doc, "id", Id));
            node.Attributes.Append(CreateAttr(doc, "name", Name));
            node.Attributes.Append(CreateAttr(doc, "type", NodeType));

            if (NodeType == "JOINT")
                node.Attributes.Append(CreateAttr(doc, "sid", Name));

            parent.AppendChild(node);

            var matrix = doc.CreateElement("matrix");
            matrix.Attributes.Append(CreateAttr(doc, "sid", "transform"));
            matrix.InnerText = FormatMatrix(Transform);
            node.AppendChild(matrix);

            if (!string.IsNullOrEmpty(InstanceType))
            {
                var instance = doc.CreateElement(InstanceType);
                instance.Attributes.Append(CreateAttr(doc, "url", InstanceUrl));
                node.AppendChild(instance);

                if (InstanceType == "instance_controller" && !string.IsNullOrEmpty(SkeletonRootId))
                {
                    var skel = doc.CreateElement("skeleton");
                    skel.InnerText = "#" + SkeletonRootId;
                    instance.AppendChild(skel);
                }

                if (!string.IsNullOrEmpty(MaterialSymbol) && !string.IsNullOrEmpty(MaterialTarget))
                {
                    var bindMaterial = doc.CreateElement("bind_material");
                    instance.AppendChild(bindMaterial);

                    var technique = doc.CreateElement("technique_common");
                    bindMaterial.AppendChild(technique);

                    var instanceMaterial = doc.CreateElement("instance_material");
                    instanceMaterial.Attributes.Append(CreateAttr(doc, "symbol", MaterialSymbol));
                    instanceMaterial.Attributes.Append(CreateAttr(doc, "target", MaterialTarget));
                    technique.AppendChild(instanceMaterial);

                    var bindVertexInput = doc.CreateElement("bind_vertex_input");
                    bindVertexInput.Attributes.Append(CreateAttr(doc, "semantic", "CHANNEL0"));
                    bindVertexInput.Attributes.Append(CreateAttr(doc, "input_semantic", "TEXCOORD"));
                    bindVertexInput.Attributes.Append(CreateAttr(doc, "input_set", "0"));
                    instanceMaterial.AppendChild(bindVertexInput);
                }
            }

            foreach (var child in Children)
                child.Write(doc, node);
        }

        private static string FormatMatrix(Matrix4x4 m)
        {
            return $"{FormatFloat(m.M11)} {FormatFloat(m.M21)} {FormatFloat(m.M31)} {FormatFloat(m.M41)} " +
                   $"{FormatFloat(m.M12)} {FormatFloat(m.M22)} {FormatFloat(m.M32)} {FormatFloat(m.M42)} " +
                   $"{FormatFloat(m.M13)} {FormatFloat(m.M23)} {FormatFloat(m.M33)} {FormatFloat(m.M43)} " +
                   $"{FormatFloat(m.M14)} {FormatFloat(m.M24)} {FormatFloat(m.M34)} {FormatFloat(m.M44)}";
        }

        private static string FormatFloat(float f)
        {
            return f.ToString("G", CultureInfo.InvariantCulture);
        }

        private static XmlAttribute CreateAttr(XmlDocument doc, string name, string value)
        {
            var attr = doc.CreateAttribute(name);
            attr.Value = value;
            return attr;
        }
    }
}
