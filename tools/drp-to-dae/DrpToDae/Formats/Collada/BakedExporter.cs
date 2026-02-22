using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Xml.Linq;
using DrpToDae.Formats.Animation;
using NudModel = DrpToDae.Formats.NUD.NUD;
using NudMesh = DrpToDae.Formats.NUD.Mesh;
using NudPolygon = DrpToDae.Formats.NUD.Polygon;
using NudVertex = DrpToDae.Formats.NUD.Vertex;
using VbnSkeleton = DrpToDae.Formats.VBN.VBN;
using VbnBone = DrpToDae.Formats.VBN.Bone;

namespace DrpToDae.Formats.Collada
{
    /// <summary>
    /// Clean baked COLLADA exporter built in phases:
    /// Phase 1: Model + Skeleton + Textures (no animation)
    /// Phase 2: Single-frame animation (frame 0 = bind pose)
    /// Phase 3: Full animation
    /// </summary>
    public static class BakedExporter
    {
        private static readonly XNamespace NS = "http://www.collada.org/2005/11/COLLADASchema";

        /// <summary>
        /// Phase 0: Export model only (no skeleton, no skinning) - like old static export
        /// </summary>
        public static void ExportPhase0(string outputPath, NudModel model)
        {
            var ctx = new ExportContext(model, null, null);
            ctx.Export(outputPath);
        }

        /// <summary>
        /// Phase 1: Export model with skeleton, no animation
        /// </summary>
        public static void ExportPhase1(string outputPath, NudModel model, VbnSkeleton? skeleton)
        {
            var ctx = new ExportContext(model, skeleton, null);
            ctx.Export(outputPath);
        }

        /// <summary>
        /// Phase 2: Export model with skeleton and single-frame (bind pose) animation
        /// </summary>
        public static void ExportPhase2(string outputPath, NudModel model, VbnSkeleton? skeleton)
        {
            // Create a fake 1-frame animation that matches bind pose
            AnimationData? bindPoseAnim = null;
            if (skeleton != null && skeleton.Bones.Count > 0)
            {
                bindPoseAnim = CreateBindPoseAnimation(skeleton);
            }
            var ctx = new ExportContext(model, skeleton, bindPoseAnim);
            ctx.Export(outputPath);
        }

        /// <summary>
        /// Phase 3: Export model with skeleton and full animation
        /// </summary>
        public static void ExportPhase3(string outputPath, NudModel model, VbnSkeleton? skeleton, AnimationData animation)
        {
            var ctx = new ExportContext(model, skeleton, animation);
            ctx.Export(outputPath);
        }

        /// <summary>
        /// Batch export with multiple matrix format variations for testing
        /// </summary>
        public static void ExportBatchVariations(string outputDir, NudModel model, VbnSkeleton? skeleton)
        {
            // Variation 0: Current format (row-major, translation at 3,7,11)
            var ctx0 = new ExportContext(model, skeleton, null) { MatrixVariant = 0 };
            ctx0.Export(Path.Combine(outputDir, "var0_rowmajor_t3711.dae"));

            // Variation 1: Column-major (translation at 12,13,14)
            var ctx1 = new ExportContext(model, skeleton, null) { MatrixVariant = 1 };
            ctx1.Export(Path.Combine(outputDir, "var1_colmajor_t121314.dae"));

            // Variation 2: Transposed rotation (swap rows/cols in rotation block)
            var ctx2 = new ExportContext(model, skeleton, null) { MatrixVariant = 2 };
            ctx2.Export(Path.Combine(outputDir, "var2_transposed_rot.dae"));

            // Variation 3: Use bone.Transform directly (world transform)
            var ctx3 = new ExportContext(model, skeleton, null) { MatrixVariant = 3 };
            ctx3.Export(Path.Combine(outputDir, "var3_world_transform.dae"));

            // Variation 4: Negate Y axis (Y-up correction)
            var ctx4 = new ExportContext(model, skeleton, null) { MatrixVariant = 4 };
            ctx4.Export(Path.Combine(outputDir, "var4_negate_y.dae"));

            // Variation 5: Match ColladaExporter FormatMatrix exactly
            var ctx5 = new ExportContext(model, skeleton, null) { MatrixVariant = 5 };
            ctx5.Export(Path.Combine(outputDir, "var5_formatmatrix_exact.dae"));

            // Variation 6: Identity bind shape matrix with adjusted IBMs
            var ctx6 = new ExportContext(model, skeleton, null) { MatrixVariant = 6 };
            ctx6.Export(Path.Combine(outputDir, "var6_identity_bsm.dae"));

            // Variation 7: Euler rotation order ZYX
            var ctx7 = new ExportContext(model, skeleton, null) { MatrixVariant = 7 };
            ctx7.Export(Path.Combine(outputDir, "var7_euler_zyx.dae"));
        }

        private static AnimationData CreateBindPoseAnimation(VbnSkeleton skeleton)
        {
            var anim = new AnimationData("BindPose") { FrameCount = 1 };
            foreach (var bone in skeleton.Bones)
            {
                var keyNode = new KeyNode(bone.Name)
                {
                    Hash = (int)bone.BoneId,
                    BoneIndex = skeleton.Bones.IndexOf(bone),
                    RotationType = RotationType.Quaternion
                };

                // Add single keyframe at frame 0 with bind pose values
                keyNode.XPos.Keys.Add(new KeyFrame(bone.Pos.X, 0));
                keyNode.YPos.Keys.Add(new KeyFrame(bone.Pos.Y, 0));
                keyNode.ZPos.Keys.Add(new KeyFrame(bone.Pos.Z, 0));

                keyNode.XRot.Keys.Add(new KeyFrame(bone.Rot.X, 0));
                keyNode.YRot.Keys.Add(new KeyFrame(bone.Rot.Y, 0));
                keyNode.ZRot.Keys.Add(new KeyFrame(bone.Rot.Z, 0));
                keyNode.WRot.Keys.Add(new KeyFrame(bone.Rot.W, 0));

                keyNode.XScale.Keys.Add(new KeyFrame(bone.Sca.X, 0));
                keyNode.YScale.Keys.Add(new KeyFrame(bone.Sca.Y, 0));
                keyNode.ZScale.Keys.Add(new KeyFrame(bone.Sca.Z, 0));

                anim.Bones.Add(keyNode);
            }
            return anim;
        }

        private class ExportContext
        {
            private readonly NudModel _model;
            private readonly VbnSkeleton? _skeleton;
            private readonly AnimationData? _animation;
            private readonly HashSet<string> _usedImageIds = new();
            private int _meshIndex = 0;

            /// <summary>
            /// Matrix format variant for testing:
            /// 0 = Row-major, translation at 3,7,11 (current)
            /// 1 = Column-major, translation at 12,13,14
            /// 2 = Transposed rotation block
            /// 3 = Use bone.Transform directly (world transform)
            /// 4 = Negate Y axis
            /// 5 = FormatMatrix exact (M11 M21 M31 M41...)
            /// 6 = Identity bind shape
            /// 7 = Different Euler order
            /// </summary>
            public int MatrixVariant { get; set; } = 0;

            public ExportContext(NudModel model, VbnSkeleton? skeleton, AnimationData? animation)
            {
                _model = model;
                _skeleton = skeleton;
                _animation = animation;
            }

            public void Export(string outputPath)
            {
                var root = new XElement(NS + "COLLADA",
                    new XAttribute("version", "1.4.1"),
                    CreateAsset(),
                    CreateLibraryImages(),
                    CreateLibraryMaterials(),
                    CreateLibraryEffects(),
                    CreateLibraryGeometries(),
                    CreateLibraryControllers(),
                    _animation != null ? CreateLibraryAnimations() : null,
                    CreateLibraryVisualScenes(),
                    CreateScene()
                );

                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    root
                );
                doc.Save(outputPath);
            }

            #region Asset
            private XElement CreateAsset()
            {
                return new XElement(NS + "asset",
                    new XElement(NS + "created", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                    new XElement(NS + "modified", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                    new XElement(NS + "up_axis", "Y_UP")
                );
            }
            #endregion

            #region Images
            private XElement CreateLibraryImages()
            {
                var images = new List<XElement>();
                foreach (var mesh in _model.Meshes)
                {
                    foreach (var poly in mesh.Polygons)
                    {
                        foreach (var mat in poly.materials)
                        {
                            foreach (var tex in mat.textures)
                            {
                                string imageId = $"Tex_0x{tex.hash:X8}";
                                if (_usedImageIds.Add(imageId))
                                {
                                    images.Add(new XElement(NS + "image",
                                        new XAttribute("id", imageId),
                                        new XAttribute("name", imageId),
                                        new XElement(NS + "init_from", $"textures/{imageId}.png")
                                    ));
                                }
                            }
                        }
                    }
                }
                return new XElement(NS + "library_images", images);
            }
            #endregion

            #region Effects
            private XElement CreateLibraryEffects()
            {
                var effects = new List<XElement>();
                var seenEffects = new HashSet<string>();

                foreach (var mesh in _model.Meshes)
                {
                    foreach (var poly in mesh.Polygons)
                    {
                        string effectId = $"effect_{_model.Meshes.IndexOf(mesh)}_{mesh.Polygons.IndexOf(poly)}";
                        if (!seenEffects.Add(effectId)) continue;

                        string? diffuseImageId = null;
                        if (poly.materials.Count > 0 && poly.materials[0].textures.Count > 0)
                        {
                            diffuseImageId = $"Tex_0x{poly.materials[0].textures[0].hash:X8}";
                        }

                        effects.Add(CreateEffect(effectId, diffuseImageId));
                    }
                }
                return new XElement(NS + "library_effects", effects);
            }

            private XElement CreateEffect(string effectId, string? diffuseImageId)
            {
                XElement diffuseElement;
                if (diffuseImageId != null)
                {
                    diffuseElement = new XElement(NS + "texture",
                        new XAttribute("texture", $"{diffuseImageId}-sampler"),
                        new XAttribute("texcoord", "CHANNEL0")
                    );
                }
                else
                {
                    diffuseElement = new XElement(NS + "color", "0.8 0.8 0.8 1.0");
                }

                return new XElement(NS + "effect",
                    new XAttribute("id", effectId),
                    new XElement(NS + "profile_COMMON",
                        diffuseImageId != null ? new XElement(NS + "newparam",
                            new XAttribute("sid", $"{diffuseImageId}-surface"),
                            new XElement(NS + "surface",
                                new XAttribute("type", "2D"),
                                new XElement(NS + "init_from", diffuseImageId)
                            )
                        ) : null,
                        diffuseImageId != null ? new XElement(NS + "newparam",
                            new XAttribute("sid", $"{diffuseImageId}-sampler"),
                            new XElement(NS + "sampler2D",
                                new XElement(NS + "source", $"{diffuseImageId}-surface")
                            )
                        ) : null,
                        new XElement(NS + "technique",
                            new XAttribute("sid", "COMMON"),
                            new XElement(NS + "phong",
                                new XElement(NS + "diffuse", diffuseElement),
                                new XElement(NS + "specular",
                                    new XElement(NS + "color", "0.2 0.2 0.2 1.0")
                                ),
                                new XElement(NS + "shininess",
                                    new XElement(NS + "float", "20")
                                )
                            )
                        )
                    )
                );
            }
            #endregion

            #region Materials
            private XElement CreateLibraryMaterials()
            {
                var materials = new List<XElement>();
                var seenMaterials = new HashSet<string>();

                foreach (var mesh in _model.Meshes)
                {
                    foreach (var poly in mesh.Polygons)
                    {
                        string matId = $"material_{_model.Meshes.IndexOf(mesh)}_{mesh.Polygons.IndexOf(poly)}";
                        string effectId = $"effect_{_model.Meshes.IndexOf(mesh)}_{mesh.Polygons.IndexOf(poly)}";
                        if (!seenMaterials.Add(matId)) continue;

                        materials.Add(new XElement(NS + "material",
                            new XAttribute("id", matId),
                            new XAttribute("name", matId),
                            new XElement(NS + "instance_effect",
                                new XAttribute("url", $"#{effectId}")
                            )
                        ));
                    }
                }
                return new XElement(NS + "library_materials", materials);
            }
            #endregion

            #region Geometries
            private XElement CreateLibraryGeometries()
            {
                var geometries = new List<XElement>();
                _meshIndex = 0;

                foreach (var mesh in _model.Meshes)
                {
                    foreach (var poly in mesh.Polygons)
                    {
                        geometries.Add(CreateGeometry(mesh, poly, _meshIndex));
                        _meshIndex++;
                    }
                }
                return new XElement(NS + "library_geometries", geometries);
            }

            private XElement CreateGeometry(NudMesh mesh, NudPolygon poly, int index)
            {
                string geoId = $"geometry_{index}";
                var vertices = poly.vertices;
                var triangles = poly.GetTriangles();

                // Position source
                var posData = new StringBuilder();
                foreach (var v in vertices)
                    posData.Append($"{F(v.pos.X)} {F(v.pos.Y)} {F(v.pos.Z)} ");

                // Normal source
                var normData = new StringBuilder();
                foreach (var v in vertices)
                    normData.Append($"{F(v.nrm.X)} {F(v.nrm.Y)} {F(v.nrm.Z)} ");

                // UV source
                var uvData = new StringBuilder();
                foreach (var v in vertices)
                {
                    float u = v.uv.Count > 0 ? v.uv[0].X : 0;
                    float vCoord = v.uv.Count > 0 ? (1.0f - v.uv[0].Y) : 0;  // Flip V for COLLADA
                    uvData.Append($"{F(u)} {F(vCoord)} ");
                }

                // Triangle indices (each index repeated 3 times for VERTEX/NORMAL/TEXCOORD)
                var indexData = new StringBuilder();
                foreach (var idx in triangles)
                {
                    indexData.Append($"{idx} {idx} {idx} ");
                }

                return new XElement(NS + "geometry",
                    new XAttribute("id", geoId),
                    new XAttribute("name", $"mesh_{index}"),
                    new XElement(NS + "mesh",
                        // Positions
                        new XElement(NS + "source",
                            new XAttribute("id", $"{geoId}-positions"),
                            new XElement(NS + "float_array",
                                new XAttribute("id", $"{geoId}-positions-array"),
                                new XAttribute("count", vertices.Count * 3),
                                posData.ToString().Trim()
                            ),
                            new XElement(NS + "technique_common",
                                new XElement(NS + "accessor",
                                    new XAttribute("source", $"#{geoId}-positions-array"),
                                    new XAttribute("count", vertices.Count),
                                    new XAttribute("stride", "3"),
                                    new XElement(NS + "param", new XAttribute("name", "X"), new XAttribute("type", "float")),
                                    new XElement(NS + "param", new XAttribute("name", "Y"), new XAttribute("type", "float")),
                                    new XElement(NS + "param", new XAttribute("name", "Z"), new XAttribute("type", "float"))
                                )
                            )
                        ),
                        // Normals
                        new XElement(NS + "source",
                            new XAttribute("id", $"{geoId}-normals"),
                            new XElement(NS + "float_array",
                                new XAttribute("id", $"{geoId}-normals-array"),
                                new XAttribute("count", vertices.Count * 3),
                                normData.ToString().Trim()
                            ),
                            new XElement(NS + "technique_common",
                                new XElement(NS + "accessor",
                                    new XAttribute("source", $"#{geoId}-normals-array"),
                                    new XAttribute("count", vertices.Count),
                                    new XAttribute("stride", "3"),
                                    new XElement(NS + "param", new XAttribute("name", "X"), new XAttribute("type", "float")),
                                    new XElement(NS + "param", new XAttribute("name", "Y"), new XAttribute("type", "float")),
                                    new XElement(NS + "param", new XAttribute("name", "Z"), new XAttribute("type", "float"))
                                )
                            )
                        ),
                        // UVs
                        new XElement(NS + "source",
                            new XAttribute("id", $"{geoId}-texcoords"),
                            new XElement(NS + "float_array",
                                new XAttribute("id", $"{geoId}-texcoords-array"),
                                new XAttribute("count", vertices.Count * 2),
                                uvData.ToString().Trim()
                            ),
                            new XElement(NS + "technique_common",
                                new XElement(NS + "accessor",
                                    new XAttribute("source", $"#{geoId}-texcoords-array"),
                                    new XAttribute("count", vertices.Count),
                                    new XAttribute("stride", "2"),
                                    new XElement(NS + "param", new XAttribute("name", "S"), new XAttribute("type", "float")),
                                    new XElement(NS + "param", new XAttribute("name", "T"), new XAttribute("type", "float"))
                                )
                            )
                        ),
                        // Vertices
                        new XElement(NS + "vertices",
                            new XAttribute("id", $"{geoId}-vertices"),
                            new XElement(NS + "input",
                                new XAttribute("semantic", "POSITION"),
                                new XAttribute("source", $"#{geoId}-positions")
                            )
                        ),
                        // Triangles
                        new XElement(NS + "triangles",
                            new XAttribute("count", triangles.Count / 3),
                            new XAttribute("material", $"material_{_model.Meshes.IndexOf(mesh)}_{mesh.Polygons.IndexOf(poly)}"),
                            new XElement(NS + "input",
                                new XAttribute("semantic", "VERTEX"),
                                new XAttribute("source", $"#{geoId}-vertices"),
                                new XAttribute("offset", "0")
                            ),
                            new XElement(NS + "input",
                                new XAttribute("semantic", "NORMAL"),
                                new XAttribute("source", $"#{geoId}-normals"),
                                new XAttribute("offset", "1")
                            ),
                            new XElement(NS + "input",
                                new XAttribute("semantic", "TEXCOORD"),
                                new XAttribute("source", $"#{geoId}-texcoords"),
                                new XAttribute("offset", "2"),
                                new XAttribute("set", "0")
                            ),
                            new XElement(NS + "p", indexData.ToString().Trim())
                        )
                    )
                );
            }
            #endregion

            #region Controllers (Skinning)
            private XElement CreateLibraryControllers()
            {
                if (_skeleton == null || _skeleton.Bones.Count == 0)
                    return new XElement(NS + "library_controllers");

                var controllers = new List<XElement>();
                int meshIndex = 0;

                foreach (var mesh in _model.Meshes)
                {
                    foreach (var poly in mesh.Polygons)
                    {
                        controllers.Add(CreateSkinController(mesh, poly, meshIndex));
                        meshIndex++;
                    }
                }
                return new XElement(NS + "library_controllers", controllers);
            }

            private XElement CreateSkinController(NudMesh mesh, NudPolygon poly, int meshIndex)
            {
                string ctrlId = $"controller_{meshIndex}";
                string geoId = $"geometry_{meshIndex}";
                var vertices = poly.vertices;

                // Joint names
                var jointNames = new StringBuilder();
                foreach (var bone in _skeleton!.Bones)
                    jointNames.Append($"{bone.Name} ");

                // Inverse bind matrices (one 4x4 matrix per bone)
                var ibmData = new StringBuilder();
                foreach (var bone in _skeleton.Bones)
                {
                    var ibm = bone.InverseTransform;
                    // Column-major output
                    ibmData.Append($"{F(ibm.M11)} {F(ibm.M21)} {F(ibm.M31)} {F(ibm.M41)} ");
                    ibmData.Append($"{F(ibm.M12)} {F(ibm.M22)} {F(ibm.M32)} {F(ibm.M42)} ");
                    ibmData.Append($"{F(ibm.M13)} {F(ibm.M23)} {F(ibm.M33)} {F(ibm.M43)} ");
                    ibmData.Append($"{F(ibm.M14)} {F(ibm.M24)} {F(ibm.M34)} {F(ibm.M44)} ");
                }

                // Weights and vertex-joint assignments
                // Use dictionary to track unique weights (like ColladaExporter)
                // Pre-initialize with weight 1.0 at index 0 (matching ColladaExporter)
                var uniqueWeights = new Dictionary<string, int> { { "1", 0 } };
                var vcount = new List<int>();
                var v = new List<int>();

                foreach (var vtx in vertices)
                {
                    int count = 0;
                    for (int i = 0; i < vtx.boneIds.Count && i < 4; i++)
                    {
                        float weight = i < vtx.boneWeights.Count ? vtx.boneWeights[i] : 0;
                        if (weight > 0.0001f)
                        {
                            int boneIdx = vtx.boneIds[i];
                            if (boneIdx >= 0 && boneIdx < _skeleton.Bones.Count)
                            {
                                string weightStr = F(weight);
                                if (!uniqueWeights.ContainsKey(weightStr))
                                {
                                    uniqueWeights[weightStr] = uniqueWeights.Count;
                                }
                                v.Add(boneIdx);
                                v.Add(uniqueWeights[weightStr]);
                                count++;
                            }
                        }
                    }
                    if (count == 0)
                    {
                        // Fallback: bind to root bone with weight 1
                        string oneStr = F(1.0f);
                        if (!uniqueWeights.ContainsKey(oneStr))
                        {
                            uniqueWeights[oneStr] = uniqueWeights.Count;
                        }
                        v.Add(0);
                        v.Add(uniqueWeights[oneStr]);
                        count = 1;
                    }
                    vcount.Add(count);
                }

                var weightsData = string.Join(" ", uniqueWeights.Keys);

                return new XElement(NS + "controller",
                    new XAttribute("id", ctrlId),
                    new XElement(NS + "skin",
                        new XAttribute("source", $"#{geoId}"),
                        new XElement(NS + "bind_shape_matrix", "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1"),
                        // Joint names source
                        new XElement(NS + "source",
                            new XAttribute("id", $"{ctrlId}-joints"),
                            new XElement(NS + "Name_array",
                                new XAttribute("id", $"{ctrlId}-joints-array"),
                                new XAttribute("count", _skeleton.Bones.Count),
                                jointNames.ToString().Trim()
                            ),
                            new XElement(NS + "technique_common",
                                new XElement(NS + "accessor",
                                    new XAttribute("source", $"#{ctrlId}-joints-array"),
                                    new XAttribute("count", _skeleton.Bones.Count),
                                    new XAttribute("stride", "1"),
                                    new XElement(NS + "param",
                                        new XAttribute("name", "JOINT"),
                                        new XAttribute("type", "Name")
                                    )
                                )
                            )
                        ),
                        // Inverse bind matrices source
                        new XElement(NS + "source",
                            new XAttribute("id", $"{ctrlId}-bind-poses"),
                            new XElement(NS + "float_array",
                                new XAttribute("id", $"{ctrlId}-bind-poses-array"),
                                new XAttribute("count", _skeleton.Bones.Count * 16),
                                ibmData.ToString().Trim()
                            ),
                            new XElement(NS + "technique_common",
                                new XElement(NS + "accessor",
                                    new XAttribute("source", $"#{ctrlId}-bind-poses-array"),
                                    new XAttribute("count", _skeleton.Bones.Count),
                                    new XAttribute("stride", "16"),
                                    new XElement(NS + "param",
                                        new XAttribute("name", "TRANSFORM"),
                                        new XAttribute("type", "float4x4")
                                    )
                                )
                            )
                        ),
                        // Weights source
                        new XElement(NS + "source",
                            new XAttribute("id", $"{ctrlId}-weights"),
                            new XElement(NS + "float_array",
                                new XAttribute("id", $"{ctrlId}-weights-array"),
                                new XAttribute("count", uniqueWeights.Count),
                                weightsData
                            ),
                            new XElement(NS + "technique_common",
                                new XElement(NS + "accessor",
                                    new XAttribute("source", $"#{ctrlId}-weights-array"),
                                    new XAttribute("count", uniqueWeights.Count),
                                    new XAttribute("stride", "1"),
                                    new XElement(NS + "param",
                                        new XAttribute("name", "WEIGHT"),
                                        new XAttribute("type", "float")
                                    )
                                )
                            )
                        ),
                        // Joints element
                        new XElement(NS + "joints",
                            new XElement(NS + "input",
                                new XAttribute("semantic", "JOINT"),
                                new XAttribute("source", $"#{ctrlId}-joints")
                            ),
                            new XElement(NS + "input",
                                new XAttribute("semantic", "INV_BIND_MATRIX"),
                                new XAttribute("source", $"#{ctrlId}-bind-poses")
                            )
                        ),
                        // Vertex weights
                        new XElement(NS + "vertex_weights",
                            new XAttribute("count", vertices.Count),
                            new XElement(NS + "input",
                                new XAttribute("semantic", "JOINT"),
                                new XAttribute("source", $"#{ctrlId}-joints"),
                                new XAttribute("offset", "0")
                            ),
                            new XElement(NS + "input",
                                new XAttribute("semantic", "WEIGHT"),
                                new XAttribute("source", $"#{ctrlId}-weights"),
                                new XAttribute("offset", "1")
                            ),
                            new XElement(NS + "vcount", string.Join(" ", vcount)),
                            new XElement(NS + "v", string.Join(" ", v))
                        )
                    )
                );
            }
            #endregion

            #region Animations
            private XElement? CreateLibraryAnimations()
            {
                if (_animation == null || _skeleton == null || _skeleton.Bones.Count == 0)
                    return null;

                var animations = new List<XElement>();

                // For each skeleton bone, create animation targeting it
                foreach (var skelBone in _skeleton.Bones)
                {
                    // Find matching animation bone
                    KeyNode? animBone = _animation.Bones.FirstOrDefault(b => b.Name == skelBone.Name);

                    string boneId = $"{skelBone.Name}_id";
                    string animId = $"{boneId}_anim";

                    animations.Add(CreateBoneAnimation(skelBone, animBone, boneId, animId));
                }

                return new XElement(NS + "library_animations", animations);
            }

            private XElement CreateBoneAnimation(VbnBone skelBone, KeyNode? animBone, string boneId, string animId)
            {
                int frameCount = _animation!.FrameCount;
                if (frameCount < 1) frameCount = 1;

                // Time values (30 fps)
                var timeData = new StringBuilder();
                for (int i = 0; i < frameCount; i++)
                    timeData.Append($"{F(i / 30f)} ");

                // Matrix values for each frame
                var matrixData = new StringBuilder();
                for (int frame = 0; frame < frameCount; frame++)
                {
                    float[] mat = ComputeLocalMatrix(skelBone, animBone, frame);
                    foreach (var m in mat)
                        matrixData.Append($"{F(m)} ");
                }

                // Interpolation values
                var interpData = new StringBuilder();
                for (int i = 0; i < frameCount; i++)
                    interpData.Append("LINEAR ");

                return new XElement(NS + "animation",
                    new XAttribute("id", animId),
                    // Time input
                    new XElement(NS + "source",
                        new XAttribute("id", $"{animId}-input"),
                        new XElement(NS + "float_array",
                            new XAttribute("id", $"{animId}-input-array"),
                            new XAttribute("count", frameCount),
                            timeData.ToString().Trim()
                        ),
                        new XElement(NS + "technique_common",
                            new XElement(NS + "accessor",
                                new XAttribute("source", $"#{animId}-input-array"),
                                new XAttribute("count", frameCount),
                                new XAttribute("stride", "1"),
                                new XElement(NS + "param",
                                    new XAttribute("name", "TIME"),
                                    new XAttribute("type", "float")
                                )
                            )
                        )
                    ),
                    // Matrix output
                    new XElement(NS + "source",
                        new XAttribute("id", $"{animId}-output"),
                        new XElement(NS + "float_array",
                            new XAttribute("id", $"{animId}-output-array"),
                            new XAttribute("count", frameCount * 16),
                            matrixData.ToString().Trim()
                        ),
                        new XElement(NS + "technique_common",
                            new XElement(NS + "accessor",
                                new XAttribute("source", $"#{animId}-output-array"),
                                new XAttribute("count", frameCount),
                                new XAttribute("stride", "16"),
                                new XElement(NS + "param",
                                    new XAttribute("name", "TRANSFORM"),
                                    new XAttribute("type", "float4x4")
                                )
                            )
                        )
                    ),
                    // Interpolation
                    new XElement(NS + "source",
                        new XAttribute("id", $"{animId}-interp"),
                        new XElement(NS + "Name_array",
                            new XAttribute("id", $"{animId}-interp-array"),
                            new XAttribute("count", frameCount),
                            interpData.ToString().Trim()
                        ),
                        new XElement(NS + "technique_common",
                            new XElement(NS + "accessor",
                                new XAttribute("source", $"#{animId}-interp-array"),
                                new XAttribute("count", frameCount),
                                new XAttribute("stride", "1"),
                                new XElement(NS + "param",
                                    new XAttribute("name", "INTERPOLATION"),
                                    new XAttribute("type", "Name")
                                )
                            )
                        )
                    ),
                    // Sampler
                    new XElement(NS + "sampler",
                        new XAttribute("id", $"{animId}-sampler"),
                        new XElement(NS + "input",
                            new XAttribute("semantic", "INPUT"),
                            new XAttribute("source", $"#{animId}-input")
                        ),
                        new XElement(NS + "input",
                            new XAttribute("semantic", "OUTPUT"),
                            new XAttribute("source", $"#{animId}-output")
                        ),
                        new XElement(NS + "input",
                            new XAttribute("semantic", "INTERPOLATION"),
                            new XAttribute("source", $"#{animId}-interp")
                        )
                    ),
                    // Channel - targets node by ID
                    new XElement(NS + "channel",
                        new XAttribute("source", $"#{animId}-sampler"),
                        new XAttribute("target", $"{boneId}/transform")
                    )
                );
            }

            /// <summary>
            /// Compute local transform matrix for a bone at a given frame.
            /// Uses animation data if available, otherwise falls back to bind pose.
            /// Output is column-major 16 floats for COLLADA animation.
            /// </summary>
            private float[] ComputeLocalMatrix(VbnBone skelBone, KeyNode? animBone, int frame)
            {
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
                    tx = skelBone.Pos.X; ty = skelBone.Pos.Y; tz = skelBone.Pos.Z;
                    sx = skelBone.Sca.X; sy = skelBone.Sca.Y; sz = skelBone.Sca.Z;
                    qx = skelBone.Rot.X; qy = skelBone.Rot.Y; qz = skelBone.Rot.Z; qw = skelBone.Rot.W;
                }

                // Normalize quaternion
                float len = (float)Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
                if (len > 0.0001f) { qx /= len; qy /= len; qz /= len; qw /= len; }

                // Build rotation matrix from quaternion
                float x2 = qx + qx, y2 = qy + qy, z2 = qz + qz;
                float xx = qx * x2, xy = qx * y2, xz = qx * z2;
                float yy = qy * y2, yz = qy * z2, zz = qz * z2;
                float wx = qw * x2, wy = qw * y2, wz = qw * z2;

                // Rotation * Scale (3x3 block)
                float r00 = (1 - yy - zz) * sx;
                float r01 = (xy - wz) * sy;
                float r02 = (xz + wy) * sz;
                float r10 = (xy + wz) * sx;
                float r11 = (1 - xx - zz) * sy;
                float r12 = (yz - wx) * sz;
                float r20 = (xz - wy) * sx;
                float r21 = (yz + wx) * sy;
                float r22 = (1 - xx - yy) * sz;

                // Output matches COLLADA/Spica format: translation at positions 3, 7, 11
                // This is the standard format for row-major 4x4 with translation in last column
                return new float[]
                {
                    r00, r01, r02, tx,   // row 0
                    r10, r11, r12, ty,   // row 1
                    r20, r21, r22, tz,   // row 2
                    0,   0,   0,   1     // row 3
                };
            }
            #endregion

            #region Visual Scenes
            private XElement CreateLibraryVisualScenes()
            {
                var sceneNodes = new List<XElement>();

                // Build skeleton hierarchy
                if (_skeleton != null && _skeleton.Bones.Count > 0)
                {
                    // Find root bones
                    foreach (var bone in _skeleton.Bones)
                    {
                        if (bone.ParentIndex < 0 || bone.ParentIndex >= _skeleton.Bones.Count)
                        {
                            sceneNodes.Add(CreateBoneNode(bone));
                        }
                    }
                }

                // Create mesh instances
                int meshIndex = 0;
                foreach (var mesh in _model.Meshes)
                {
                    foreach (var poly in mesh.Polygons)
                    {
                        sceneNodes.Add(CreateMeshInstance(mesh, poly, meshIndex));
                        meshIndex++;
                    }
                }

                return new XElement(NS + "library_visual_scenes",
                    new XElement(NS + "visual_scene",
                        new XAttribute("id", "Scene"),
                        new XAttribute("name", "Scene"),
                        sceneNodes
                    )
                );
            }

            private XElement CreateBoneNode(VbnBone bone)
            {
                string boneId = $"{bone.Name}_id";

                // Compute local transform matrix
                float[] mat = ComputeBindPoseMatrix(bone);
                string matrixStr = string.Join(" ", mat.Select(m => F(m)));

                var children = new List<XElement>();
                foreach (var child in bone.Children)
                {
                    children.Add(CreateBoneNode(child));
                }

                return new XElement(NS + "node",
                    new XAttribute("id", boneId),
                    new XAttribute("name", bone.Name),
                    new XAttribute("sid", bone.Name),
                    new XAttribute("type", "JOINT"),
                    new XElement(NS + "matrix",
                        new XAttribute("sid", "transform"),
                        matrixStr
                    ),
                    children
                );
            }

            private float[] ComputeBindPoseMatrix(VbnBone bone)
            {
                // Variant 3: Use bone.Transform directly (world transform from VBN)
                if (MatrixVariant == 3)
                {
                    var m = bone.Transform;
                    // Compute local by removing parent contribution
                    if (bone.ParentIndex >= 0 && bone.ParentIndex < _skeleton!.Bones.Count)
                    {
                        var parent = _skeleton.Bones[bone.ParentIndex];
                        if (Matrix4x4.Invert(parent.Transform, out var parentInv))
                        {
                            m = bone.Transform * parentInv;
                        }
                    }
                    // Output as FormatMatrix does (columns as rows)
                    return new float[]
                    {
                        m.M11, m.M21, m.M31, m.M41,
                        m.M12, m.M22, m.M32, m.M42,
                        m.M13, m.M23, m.M33, m.M43,
                        m.M14, m.M24, m.M34, m.M44
                    };
                }

                // Variant 5: Use Matrix4x4 operations and FormatMatrix exactly like ColladaExporter
                if (MatrixVariant == 5)
                {
                    var scale = Matrix4x4.CreateScale(bone.Sca);
                    var rotation = Matrix4x4.CreateFromQuaternion(bone.Rot);
                    var translation = Matrix4x4.CreateTranslation(bone.Pos);
                    var m = scale * rotation * translation;
                    // FormatMatrix outputs columns as rows
                    return new float[]
                    {
                        m.M11, m.M21, m.M31, m.M41,
                        m.M12, m.M22, m.M32, m.M42,
                        m.M13, m.M23, m.M33, m.M43,
                        m.M14, m.M24, m.M34, m.M44
                    };
                }

                float tx = bone.Pos.X, ty = bone.Pos.Y, tz = bone.Pos.Z;
                float sx = bone.Sca.X, sy = bone.Sca.Y, sz = bone.Sca.Z;
                float qx = bone.Rot.X, qy = bone.Rot.Y, qz = bone.Rot.Z, qw = bone.Rot.W;

                // Variant 4: Negate Y axis
                if (MatrixVariant == 4)
                {
                    ty = -ty;
                }

                // Normalize quaternion
                float len = (float)Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
                if (len > 0.0001f) { qx /= len; qy /= len; qz /= len; qw /= len; }

                // Build rotation matrix from quaternion
                float x2 = qx + qx, y2 = qy + qy, z2 = qz + qz;
                float xx = qx * x2, xy = qx * y2, xz = qx * z2;
                float yy = qy * y2, yz = qy * z2, zz = qz * z2;
                float wx = qw * x2, wy = qw * y2, wz = qw * z2;

                float r00 = (1 - yy - zz) * sx;
                float r01 = (xy - wz) * sy;
                float r02 = (xz + wy) * sz;
                float r10 = (xy + wz) * sx;
                float r11 = (1 - xx - zz) * sy;
                float r12 = (yz - wx) * sz;
                float r20 = (xz - wy) * sx;
                float r21 = (yz + wx) * sy;
                float r22 = (1 - xx - yy) * sz;

                // Variant 0 (default): Row-major, translation at 3,7,11
                if (MatrixVariant == 0 || MatrixVariant == 4 || MatrixVariant == 6 || MatrixVariant == 7)
                {
                    return new float[]
                    {
                        r00, r01, r02, tx,   // row 0
                        r10, r11, r12, ty,   // row 1
                        r20, r21, r22, tz,   // row 2
                        0,   0,   0,   1     // row 3
                    };
                }

                // Variant 1: Column-major, translation at 12,13,14
                if (MatrixVariant == 1)
                {
                    return new float[]
                    {
                        r00, r10, r20, 0,    // column 0
                        r01, r11, r21, 0,    // column 1
                        r02, r12, r22, 0,    // column 2
                        tx,  ty,  tz,  1     // column 3 (translation)
                    };
                }

                // Variant 2: Transposed rotation (rows become columns)
                if (MatrixVariant == 2)
                {
                    return new float[]
                    {
                        r00, r10, r20, tx,   // transposed row 0
                        r01, r11, r21, ty,   // transposed row 1
                        r02, r12, r22, tz,   // transposed row 2
                        0,   0,   0,   1     // row 3
                    };
                }

                // Default fallback
                return new float[]
                {
                    r00, r01, r02, tx,
                    r10, r11, r12, ty,
                    r20, r21, r22, tz,
                    0,   0,   0,   1
                };
            }

            private XElement CreateMeshInstance(NudMesh mesh, NudPolygon poly, int meshIndex)
            {
                string ctrlId = $"controller_{meshIndex}";
                string matId = $"material_{_model.Meshes.IndexOf(mesh)}_{mesh.Polygons.IndexOf(poly)}";
                string rootBoneId = _skeleton != null && _skeleton.Bones.Count > 0
                    ? $"{_skeleton.Bones[0].Name}_id"
                    : "";

                // Identity matrix for mesh node transform
                string identityMatrix = "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1";

                if (_skeleton != null && _skeleton.Bones.Count > 0)
                {
                    // Skinned mesh - use instance_controller
                    return new XElement(NS + "node",
                        new XAttribute("id", $"mesh_{meshIndex}_node"),
                        new XAttribute("name", $"mesh_{meshIndex}"),
                        new XAttribute("type", "NODE"),
                        new XElement(NS + "matrix",
                            new XAttribute("sid", "transform"),
                            identityMatrix
                        ),
                        new XElement(NS + "instance_controller",
                            new XAttribute("url", $"#{ctrlId}"),
                            new XElement(NS + "skeleton", $"#{rootBoneId}"),
                            new XElement(NS + "bind_material",
                                new XElement(NS + "technique_common",
                                    new XElement(NS + "instance_material",
                                        new XAttribute("symbol", matId),
                                        new XAttribute("target", $"#{matId}"),
                                        new XElement(NS + "bind_vertex_input",
                                            new XAttribute("semantic", "TEXCOORD0"),
                                            new XAttribute("input_semantic", "TEXCOORD"),
                                            new XAttribute("input_set", "0")
                                        )
                                    )
                                )
                            )
                        )
                    );
                }
                else
                {
                    // Static mesh - use instance_geometry
                    return new XElement(NS + "node",
                        new XAttribute("id", $"mesh_{meshIndex}_node"),
                        new XAttribute("name", $"mesh_{meshIndex}"),
                        new XAttribute("type", "NODE"),
                        new XElement(NS + "matrix",
                            new XAttribute("sid", "transform"),
                            identityMatrix
                        ),
                        new XElement(NS + "instance_geometry",
                            new XAttribute("url", $"#geometry_{meshIndex}"),
                            new XElement(NS + "bind_material",
                                new XElement(NS + "technique_common",
                                    new XElement(NS + "instance_material",
                                        new XAttribute("symbol", matId),
                                        new XAttribute("target", $"#{matId}"),
                                        new XElement(NS + "bind_vertex_input",
                                            new XAttribute("semantic", "TEXCOORD0"),
                                            new XAttribute("input_semantic", "TEXCOORD"),
                                            new XAttribute("input_set", "0")
                                        )
                                    )
                                )
                            )
                        )
                    );
                }
            }
            #endregion

            #region Scene
            private XElement CreateScene()
            {
                return new XElement(NS + "scene",
                    new XElement(NS + "instance_visual_scene",
                        new XAttribute("url", "#Scene")
                    )
                );
            }
            #endregion

            #region Helpers
            private static string F(float value)
            {
                return value.ToString("0.######", CultureInfo.InvariantCulture);
            }
            #endregion
        }
    }
}
