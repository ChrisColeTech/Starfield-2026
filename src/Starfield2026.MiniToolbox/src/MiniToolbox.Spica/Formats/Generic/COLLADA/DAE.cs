using MiniToolbox.Spica.Formats.Common;
using MiniToolbox.Spica.Formats.CtrH3D;
using MiniToolbox.Spica.Formats.CtrH3D.Animation;
using MiniToolbox.Spica.Formats.CtrH3D.Model;
using MiniToolbox.Spica.Formats.CtrH3D.Model.Material;
using MiniToolbox.Spica.Formats.CtrH3D.Model.Mesh;
using MiniToolbox.Spica.Formats.CtrH3D.Texture;
using MiniToolbox.Spica.Math3D;
using MiniToolbox.Spica.PICA.Commands;
using MiniToolbox.Spica.PICA.Converters;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Serialization;

namespace MiniToolbox.Spica.Formats.Generic.COLLADA
{
    [XmlRoot("COLLADA", Namespace = "http://www.collada.org/2005/11/COLLADASchema")]
    public class DAE
    {
        [XmlAttribute] public string version = "1.4.1";

        public DAEAsset asset = new DAEAsset();

        [XmlArrayItem("animation")]    public List<DAEAnimation>   library_animations;
        [XmlArrayItem("image")]        public List<DAEImage>       library_images;
        [XmlArrayItem("material")]     public List<DAEMaterial>    library_materials;
        [XmlArrayItem("effect")]       public List<DAEEffect>      library_effects;
        [XmlArrayItem("geometry")]     public List<DAEGeometry>    library_geometries;
        [XmlArrayItem("controller")]   public List<DAEController>  library_controllers;
        [XmlArrayItem("visual_scene")] public List<DAEVisualScene> library_visual_scenes;

        public DAEScene scene = new DAEScene();

        public DAE() { }

        /// <summary>
        /// Clip-only export: skeleton hierarchy + animation channels, no mesh/geometry/textures.
        /// Use for separate animation clip files that pair with a model exported via DAE(Scene, MdlIndex, -1).
        /// </summary>
        public DAE(H3D Scene, int MdlIndex, int AnimIndex, bool clipOnly)
        {
            if (!clipOnly) { /* fall through to normal constructor logic via the other overload */ return; }

            H3DModel Mdl = Scene.Models[MdlIndex];

            // Visual scene with skeleton only (no mesh nodes)
            library_visual_scenes = new List<DAEVisualScene>();

            DAEVisualScene VN = new DAEVisualScene();
            VN.name = $"{Mdl.Name}_{MdlIndex:D2}_clip";
            VN.id   = $"{VN.name}_id";

            // Build skeleton hierarchy — same bone IDs as model export
            if ((Mdl.Skeleton?.Count ?? 0) > 0)
            {
                Queue<Tuple<H3DBone, DAENode>> ChildBones = new Queue<Tuple<H3DBone, DAENode>>();

                DAENode RootNode = new DAENode();
                ChildBones.Enqueue(Tuple.Create(Mdl.Skeleton[0], RootNode));

                while (ChildBones.Count > 0)
                {
                    Tuple<H3DBone, DAENode> Bone_Node = ChildBones.Dequeue();
                    H3DBone Bone = Bone_Node.Item1;

                    if (string.IsNullOrEmpty(Bone.Name))
                    {
                        foreach (H3DBone B in Mdl.Skeleton)
                        {
                            if (B.ParentIndex == -1) continue;
                            if (Mdl.Skeleton[B.ParentIndex] == Bone)
                                ChildBones.Enqueue(Tuple.Create(B, Bone_Node.Item2));
                        }
                        continue;
                    }

                    Bone_Node.Item2.id   = $"{Bone.Name}_bone_id";
                    Bone_Node.Item2.name = Bone.Name;
                    Bone_Node.Item2.sid  = Bone.Name;
                    Bone_Node.Item2.type = DAENodeType.JOINT;

                    // Always use matrix format — matches animation channel targets
                    Bone_Node.Item2.SetBoneMatrix(Bone.Transform);

                    foreach (H3DBone B in Mdl.Skeleton)
                    {
                        if (B.ParentIndex == -1) continue;
                        if (Mdl.Skeleton[B.ParentIndex] == Bone)
                        {
                            DAENode Node = new DAENode();
                            ChildBones.Enqueue(Tuple.Create(B, Node));
                            if (Bone_Node.Item2.Nodes == null) Bone_Node.Item2.Nodes = new List<DAENode>();
                            Bone_Node.Item2.Nodes.Add(Node);
                        }
                    }
                }

                VN.node.Add(RootNode);
            }

            library_visual_scenes.Add(VN);
            scene.instance_visual_scene.url = $"#{VN.id}";

            // Animation channels (reuses the same code path as baked export)
            if (AnimIndex >= 0 && AnimIndex < Scene.SkeletalAnimations.Count)
            {
                library_animations = new List<DAEAnimation>();

                H3DAnimation SklAnim = Scene.SkeletalAnimations[AnimIndex];
                H3DDict<H3DBone> Skeleton = Mdl.Skeleton;
                int FramesCount = (int)SklAnim.FramesCount + 1;

                Dictionary<string, int> BoneNameToIdx = new Dictionary<string, int>();
                for (int i = 0; i < Skeleton.Count; i++)
                {
                    if (!string.IsNullOrEmpty(Skeleton[i].Name))
                        BoneNameToIdx[Skeleton[i].Name] = i;
                }

                foreach (H3DAnimationElement Elem in SklAnim.Elements)
                {
                    if (string.IsNullOrEmpty(Elem.Name)) continue;
                    if (Elem.PrimitiveType != H3DPrimitiveType.Transform &&
                        Elem.PrimitiveType != H3DPrimitiveType.QuatTransform) continue;
                    if (!BoneNameToIdx.ContainsKey(Elem.Name)) continue;

                    int BoneIdx = BoneNameToIdx[Elem.Name];
                    H3DBone SklBone = Skeleton[BoneIdx];

                    H3DBone Parent = null;
                    H3DAnimationElement PElem = null;

                    if (SklBone.ParentIndex != -1)
                    {
                        Parent = Skeleton[SklBone.ParentIndex];
                        PElem = SklAnim.Elements.FirstOrDefault(x => x.Name == Parent.Name);
                    }

                    string[] AnimTimes = new string[FramesCount];
                    string[] AnimPoses = new string[FramesCount];
                    string[] AnimLerps = new string[FramesCount];

                    for (int Frame = 0; Frame < FramesCount; Frame++)
                    {
                        Vector3 T = SklBone.Translation;
                        Vector3 S = SklBone.Scale;
                        Matrix4x4 R;

                        if (Elem.Content is H3DAnimTransform Transform)
                        {
                            if (Transform.TranslationX.Exists) T.X = Transform.TranslationX.GetFrameValue(Frame);
                            if (Transform.TranslationY.Exists) T.Y = Transform.TranslationY.GetFrameValue(Frame);
                            if (Transform.TranslationZ.Exists) T.Z = Transform.TranslationZ.GetFrameValue(Frame);

                            float Rx = Transform.RotationX.Exists ? Transform.RotationX.GetFrameValue(Frame) : SklBone.Rotation.X;
                            float Ry = Transform.RotationY.Exists ? Transform.RotationY.GetFrameValue(Frame) : SklBone.Rotation.Y;
                            float Rz = Transform.RotationZ.Exists ? Transform.RotationZ.GetFrameValue(Frame) : SklBone.Rotation.Z;

                            R = Matrix4x4.CreateRotationX(Rx) *
                                Matrix4x4.CreateRotationY(Ry) *
                                Matrix4x4.CreateRotationZ(Rz);

                            if (Transform.ScaleX.Exists) S.X = Transform.ScaleX.GetFrameValue(Frame);
                            if (Transform.ScaleY.Exists) S.Y = Transform.ScaleY.GetFrameValue(Frame);
                            if (Transform.ScaleZ.Exists) S.Z = Transform.ScaleZ.GetFrameValue(Frame);
                        }
                        else if (Elem.Content is H3DAnimQuatTransform QT)
                        {
                            if (QT.HasTranslation) T = QT.GetTranslationValue(Frame);
                            if (QT.HasRotation)    R = Matrix4x4.CreateFromQuaternion(QT.GetRotationValue(Frame));
                            else                   R = Matrix4x4.CreateRotationX(SklBone.Rotation.X) *
                                                       Matrix4x4.CreateRotationY(SklBone.Rotation.Y) *
                                                       Matrix4x4.CreateRotationZ(SklBone.Rotation.Z);
                            if (QT.HasScale)       S = QT.GetScaleValue(Frame);
                        }
                        else
                        {
                            R = Matrix4x4.CreateRotationX(SklBone.Rotation.X) *
                                Matrix4x4.CreateRotationY(SklBone.Rotation.Y) *
                                Matrix4x4.CreateRotationZ(SklBone.Rotation.Z);
                        }

                        if (Parent != null && (SklBone.Flags & H3DBoneFlags.IsSegmentScaleCompensate) != 0)
                        {
                            Vector3 PS = Parent.Scale;
                            if (PElem != null)
                            {
                                if (PElem.Content is H3DAnimTransform PT)
                                {
                                    if (PT.ScaleX.Exists) PS.X = PT.ScaleX.GetFrameValue(Frame);
                                    if (PT.ScaleY.Exists) PS.Y = PT.ScaleY.GetFrameValue(Frame);
                                    if (PT.ScaleZ.Exists) PS.Z = PT.ScaleZ.GetFrameValue(Frame);
                                }
                                else if (PElem.Content is H3DAnimQuatTransform PQT && PQT.HasScale)
                                {
                                    PS = PQT.GetScaleValue(Frame);
                                }
                            }
                            S /= PS;
                        }

                        Matrix4x4 LocalMtx = Matrix4x4.CreateScale(S) * R * Matrix4x4.CreateTranslation(T);

                        AnimTimes[Frame] = (Frame / 30f).ToString(CultureInfo.InvariantCulture);
                        AnimPoses[Frame] = DAEUtils.MatrixStr(new Matrix3x4(LocalMtx));
                        AnimLerps[Frame] = "LINEAR";
                    }

                    DAEAnimation Anim = new DAEAnimation();
                    Anim.name = $"{SklAnim.Name}_{SklBone.Name}_transform";
                    Anim.id   = $"{Anim.name}_id";

                    Anim.src.Add(new DAESource($"{Anim.name}_frame",  1, AnimTimes, "TIME",          "float"));
                    Anim.src.Add(new DAESource($"{Anim.name}_interp", 1, AnimLerps, "INTERPOLATION", "Name"));
                    Anim.src.Add(new DAESource($"{Anim.name}_pose",  16, AnimPoses, "TRANSFORM",     "float4x4"));

                    Anim.sampler.AddInput("INPUT",         $"#{Anim.src[0].id}");
                    Anim.sampler.AddInput("INTERPOLATION", $"#{Anim.src[1].id}");
                    Anim.sampler.AddInput("OUTPUT",        $"#{Anim.src[2].id}");

                    Anim.sampler.id     = $"{Anim.name}_samp_id";
                    Anim.channel.source = $"#{Anim.sampler.id}";
                    Anim.channel.target = $"{SklBone.Name}_bone_id/transform";

                    library_animations.Add(Anim);
                }
            }
        }

        public DAE(H3D Scene, int MdlIndex, int AnimIndex = -1)
        {
            if (MdlIndex != -1)
            {
                library_visual_scenes = new List<DAEVisualScene>();

                H3DModel Mdl = Scene.Models[MdlIndex];

                DAEVisualScene VN = new DAEVisualScene();

                VN.name = $"{Mdl.Name}_{MdlIndex.ToString("D2")}";
                VN.id   = $"{VN.name}_id";

                //Materials
                if (Mdl.Materials.Count > 0)
                {
                    library_materials = new List<DAEMaterial>();
                    library_effects   = new List<DAEEffect>();
                }

                foreach (H3DMaterial Mtl in Mdl.Materials)
                {
                    string MtlName = $"{MdlIndex.ToString("D2")}_{Mtl.Name}";

                    DAEEffect Effect = new DAEEffect();

                    Effect.name = $"{Mtl.Name}_eff";
                    Effect.id = $"{Effect.name}_id";

                    DAEEffectParam ImgSurface = new DAEEffectParam();
                    DAEEffectParam ImgSampler = new DAEEffectParam();

                    ImgSurface.surface   = new DAEEffectParamSurfaceElement();
                    ImgSampler.sampler2D = new DAEEffectParamSampler2DElement();

                    ImgSurface.sid = $"{Mtl.Name}_surf";
                    ImgSurface.surface.type = "2D";
                    ImgSurface.surface.init_from = Mtl.Texture0Name;
                    ImgSurface.surface.format = "PNG";

                    ImgSampler.sid = $"{Mtl.Name}_samp";
                    ImgSampler.sampler2D.source = ImgSurface.sid;
                    ImgSampler.sampler2D.wrap_s = Mtl.TextureMappers[0].WrapU.ToDAEWrap();
                    ImgSampler.sampler2D.wrap_t = Mtl.TextureMappers[0].WrapV.ToDAEWrap();
                    ImgSampler.sampler2D.minfilter = Mtl.TextureMappers[0].MinFilter.ToDAEFilter();
                    ImgSampler.sampler2D.magfilter = Mtl.TextureMappers[0].MagFilter.ToDAEFilter();
                    ImgSampler.sampler2D.mipfilter = DAEFilter.LINEAR;

                    Effect.profile_COMMON.newparam.Add(ImgSurface);
                    Effect.profile_COMMON.newparam.Add(ImgSampler);

                    Effect.profile_COMMON.technique.sid = $"{Mtl.Name}_tech";
                    Effect.profile_COMMON.technique.phong.diffuse.texture.texture = ImgSampler.sid;

                    library_effects.Add(Effect);

                    DAEMaterial Material = new DAEMaterial();

                    Material.name = $"{Mtl.Name}_mat";
                    Material.id = $"{Material.name}_id";

                    Material.instance_effect.url = $"#{Effect.id}";

                    library_materials.Add(Material);
                }

                //Skeleton nodes
                string RootBoneId = string.Empty;

                if ((Mdl.Skeleton?.Count ?? 0) > 0)
                {
                    Queue<Tuple<H3DBone, DAENode>> ChildBones = new Queue<Tuple<H3DBone, DAENode>>();

                    DAENode RootNode = new DAENode();

                    ChildBones.Enqueue(Tuple.Create(Mdl.Skeleton[0], RootNode));

                    RootBoneId = $"#{Mdl.Skeleton[0].Name}_bone_id";

                    while (ChildBones.Count > 0)
                    {
                        Tuple<H3DBone, DAENode> Bone_Node = ChildBones.Dequeue();

                        H3DBone Bone = Bone_Node.Item1;

                        // Skip bones with empty names — they produce invalid IDs like "_bone_id"
                        if (string.IsNullOrEmpty(Bone.Name))
                        {
                            // Still enqueue children so they aren't lost from the hierarchy
                            foreach (H3DBone B in Mdl.Skeleton)
                            {
                                if (B.ParentIndex == -1) continue;
                                if (Mdl.Skeleton[B.ParentIndex] == Bone)
                                {
                                    ChildBones.Enqueue(Tuple.Create(B, Bone_Node.Item2));
                                }
                            }
                            continue;
                        }

                        Bone_Node.Item2.id   = $"{Bone.Name}_bone_id";
                        Bone_Node.Item2.name = Bone.Name;
                        Bone_Node.Item2.sid  = Bone.Name;
                        Bone_Node.Item2.type = DAENodeType.JOINT;

                        // Always use matrix format for consistent bone SIDs across model + clip files
                        Bone_Node.Item2.SetBoneMatrix(Bone.Transform);

                        foreach (H3DBone B in Mdl.Skeleton)
                        {
                            if (B.ParentIndex == -1) continue;

                            H3DBone ParentBone = Mdl.Skeleton[B.ParentIndex];

                            if (ParentBone == Bone)
                            {
                                DAENode Node = new DAENode();

                                ChildBones.Enqueue(Tuple.Create(B, Node));

                                if (Bone_Node.Item2.Nodes == null) Bone_Node.Item2.Nodes = new List<DAENode>();

                                Bone_Node.Item2.Nodes.Add(Node);
                            }
                        }
                    }

                    VN.node.Add(RootNode);
                }

                //Mesh
                if (Mdl.Meshes.Count > 0)
                {
                    library_geometries = new List<DAEGeometry>();
                }

                for (int MeshIndex = 0; MeshIndex < Mdl.Meshes.Count; MeshIndex++)
                {
                    if (Mdl.Meshes[MeshIndex].Type == H3DMeshType.Silhouette) continue;

                    H3DMesh Mesh = Mdl.Meshes[MeshIndex];

                    PICAVertex[] Vertices = MeshTransform.GetWorldSpaceVertices(Mdl.Skeleton, Mesh);

                    // Apply texture coordinate transforms from material
                    H3DMaterial MtlTex = Mdl.Materials[Mesh.MaterialIndex];
                    var TexCoords = MtlTex.MaterialParams.TextureCoords;
                    Matrix3x4[] TexMtx = new Matrix3x4[TexCoords.Length];
                    for (int tc = 0; tc < TexCoords.Length; tc++)
                        TexMtx[tc] = TexCoords[tc].GetTransform();

                    System.Diagnostics.Debug.WriteLine($"  Mesh {MeshIndex} mtl={MtlTex.Name} tex0={MtlTex.Texture0Name} " +
                        $"Scale=({TexCoords[0].Scale.X},{TexCoords[0].Scale.Y}) " +
                        $"Rot={TexCoords[0].Rotation} Trans=({TexCoords[0].Translation.X},{TexCoords[0].Translation.Y}) " +
                        $"Type={TexCoords[0].TransformType} Map={TexCoords[0].MappingType} " +
                        $"Mtx=[{TexMtx[0].M11:F4} {TexMtx[0].M12:F4} | {TexMtx[0].M21:F4} {TexMtx[0].M22:F4} | {TexMtx[0].M41:F4} {TexMtx[0].M42:F4}]");
                    Console.Error.WriteLine($"  Mesh {MeshIndex} mtl={MtlTex.Name} tex0={MtlTex.Texture0Name} " +
                        $"Scale=({TexCoords[0].Scale.X},{TexCoords[0].Scale.Y}) " +
                        $"Rot={TexCoords[0].Rotation} Trans=({TexCoords[0].Translation.X},{TexCoords[0].Translation.Y}) " +
                        $"Type={TexCoords[0].TransformType} Map={TexCoords[0].MappingType} " +
                        $"Mtx=[{TexMtx[0].M11:F4} {TexMtx[0].M12:F4} | {TexMtx[0].M21:F4} {TexMtx[0].M22:F4} | {TexMtx[0].M41:F4} {TexMtx[0].M42:F4}]");

                    PICATextureWrap WrapU = MtlTex.TextureMappers[0].WrapU;
                    PICATextureWrap WrapV = MtlTex.TextureMappers[0].WrapV;

                    for (int vi = 0; vi < Vertices.Length; vi++)
                    {
                        Vertices[vi].TexCoord0 = TransformUV(Vertices[vi].TexCoord0, TexMtx[0], WrapU, WrapV);
                        if (TexCoords.Length > 1)
                            Vertices[vi].TexCoord1 = TransformUV(Vertices[vi].TexCoord1, TexMtx[1], WrapU, WrapV);
                        if (TexCoords.Length > 2)
                            Vertices[vi].TexCoord2 = TransformUV(Vertices[vi].TexCoord2, TexMtx[2], WrapU, WrapV);
                    }

                    string MtlName = $"Mdl_{MdlIndex}_Mtl_{Mdl.Materials[Mesh.MaterialIndex].Name}";
                    string MtlTgt = library_materials[Mesh.MaterialIndex].id;

                    for (int SMIndex = 0; SMIndex < Mesh.SubMeshes.Count; SMIndex++)
                    {
                        H3DSubMesh SM = Mesh.SubMeshes[SMIndex];

                        string ShortName = string.Empty;

                        if (Mdl.MeshNodesTree != null && Mesh.NodeIndex < Mdl.MeshNodesTree.Count)
                        {
                            ShortName = Mdl.MeshNodesTree.Find(Mesh.NodeIndex);
                        }

                        string MeshName = $"{ShortName}_{MeshIndex}_{SMIndex}";

                        DAEGeometry Geometry = new DAEGeometry();

                        Geometry.name = MeshName;
                        Geometry.id = $"{Geometry.name}_geo_id";

                        //Geometry
                        string VertsId = $"{MeshName}_vtx_id";

                        Geometry.mesh.vertices.id = VertsId;
                        Geometry.mesh.triangles.material = MtlName;
                        Geometry.mesh.triangles.AddInput("VERTEX", $"#{VertsId}");
                        Geometry.mesh.triangles.Set_p(SM.Indices);

                        foreach (PICAAttribute Attr in Mesh.Attributes)
                        {
                            if (Attr.Name >= PICAAttributeName.BoneIndex) continue;

                            string[] Values = new string[Vertices.Length];

                            for (int Index = 0; Index < Vertices.Length; Index++)
                            {
                                PICAVertex v = Vertices[Index];

                                switch (Attr.Name)
                                {
                                    case PICAAttributeName.Position:  Values[Index] = DAEUtils.Vector3Str(v.Position);  break;
                                    case PICAAttributeName.Normal:    Values[Index] = DAEUtils.Vector3Str(v.Normal);    break;
                                    case PICAAttributeName.Tangent:   Values[Index] = DAEUtils.Vector3Str(v.Tangent);   break;
                                    case PICAAttributeName.Color:     Values[Index] = DAEUtils.Vector4Str(v.Color);     break;
                                    case PICAAttributeName.TexCoord0: Values[Index] = DAEUtils.Vector2Str(v.TexCoord0); break;
                                    case PICAAttributeName.TexCoord1: Values[Index] = DAEUtils.Vector2Str(v.TexCoord1); break;
                                    case PICAAttributeName.TexCoord2: Values[Index] = DAEUtils.Vector2Str(v.TexCoord2); break;
                                }
                            }

                            int Elements = 0;

                            switch (Attr.Name)
                            {
                                case PICAAttributeName.Position:  Elements = 3; break;
                                case PICAAttributeName.Normal:    Elements = 3; break;
                                case PICAAttributeName.Tangent:   Elements = 3; break;
                                case PICAAttributeName.Color:     Elements = 4; break;
                                case PICAAttributeName.TexCoord0: Elements = 2; break;
                                case PICAAttributeName.TexCoord1: Elements = 2; break;
                                case PICAAttributeName.TexCoord2: Elements = 2; break;
                            }

                            DAESource Source = new DAESource();

                            Source.name = $"{MeshName}_{Attr.Name}";
                            Source.id   = $"{Source.name}_id";

                            Source.float_array = new DAEArray()
                            {
                                id    = $"{Source.name}_array_id",
                                count = (uint)(Vertices.Length * Elements),
                                data  = string.Join(" ", Values)
                            };

                            DAEAccessor Accessor = new DAEAccessor()
                            {
                                source = $"#{Source.float_array.id}",
                                count  = (uint)Vertices.Length,
                                stride = (uint)Elements
                            };

                            switch (Elements)
                            {
                                case 2: Accessor.AddParams("float", "S", "T");           break;
                                case 3: Accessor.AddParams("float", "X", "Y", "Z");      break;
                                case 4: Accessor.AddParams("float", "R", "G", "B", "A"); break;
                            }

                            Source.technique_common.accessor = Accessor;

                            Geometry.mesh.source.Add(Source);

                            if (Attr.Name < PICAAttributeName.Color)
                            {
                                string Semantic = string.Empty;

                                switch (Attr.Name)
                                {
                                    case PICAAttributeName.Position: Semantic = "POSITION"; break;
                                    case PICAAttributeName.Normal:   Semantic = "NORMAL";   break;
                                    case PICAAttributeName.Tangent:  Semantic = "TANGENT";  break;
                                }

                                Geometry.mesh.vertices.AddInput(Semantic, $"#{Source.id}");
                            }
                            else if (Attr.Name == PICAAttributeName.Color)
                            {
                                Geometry.mesh.triangles.AddInput("COLOR", $"#{Source.id}", 0);
                            }
                            else
                            {
                                Geometry.mesh.triangles.AddInput("TEXCOORD", $"#{Source.id}", 0, (uint)Attr.Name - 4);
                            }
                        } //Attributes Loop

                        library_geometries.Add(Geometry);

                        //Controller
                        bool HasController = SM.BoneIndicesCount > 0 && (Mdl.Skeleton?.Count ?? 0) > 0;

                        DAEController Controller = new DAEController();

                        if (HasController)
                        {
                            if (library_controllers == null)
                            {
                                library_controllers = new List<DAEController>();
                            }

                            Controller.name = $"{MeshName}_ctrl";
                            Controller.id = $"{Controller.name}_id";

                            Controller.skin.source = $"#{Geometry.id}";
                            Controller.skin.vertex_weights.count = (uint)Vertices.Length;

                            string[] BoneNames = new string[Mdl.Skeleton.Count];
                            string[] BindPoses = new string[Mdl.Skeleton.Count];

                            for (int Index = 0; Index < Mdl.Skeleton.Count; Index++)
                            {
                                BoneNames[Index] = Mdl.Skeleton[Index].Name;
                                BindPoses[Index] = DAEUtils.MatrixStr(Mdl.Skeleton[Index].InverseTransform);
                            }

                            //4 is the max number of bones per vertex
                            int[] v      = new int[Vertices.Length * 4 * 2];
                            int[] vcount = new int[Vertices.Length];

                            Dictionary<string, int> Weights = new Dictionary<string, int>();

                            int vi = 0, vci = 0;

                            if (SM.Skinning == H3DSubMeshSkinning.Smooth)
                            {
                                foreach (PICAVertex Vertex in Vertices)
                                {
                                    int Count = 0;

                                    for (int Index = 0; Index < 4; Index++)
                                    {
                                        int   BIndex = Vertex.Indices[Index];
                                        float Weight = Vertex.Weights[Index];

                                        if (Weight == 0) break;

                                        if (BIndex < SM.BoneIndices.Length && BIndex > -1)
                                            BIndex = SM.BoneIndices[BIndex];
                                        else
                                            BIndex = 0;

                                        string WStr = Weight.ToString(CultureInfo.InvariantCulture);

                                        v[vi++] = BIndex;

                                        if (Weights.ContainsKey(WStr))
                                        {
                                            v[vi++] = Weights[WStr];
                                        }
                                        else
                                        {
                                            v[vi++] = Weights.Count;

                                            Weights.Add(WStr, Weights.Count);
                                        }

                                        Count++;
                                    }

                                    vcount[vci++] = Count;
                                }
                            }
                            else
                            {
                                foreach (PICAVertex Vertex in Vertices)
                                {
                                    int BIndex = Vertex.Indices[0];

                                    if (BIndex < SM.BoneIndices.Length && BIndex > -1)
                                        BIndex = SM.BoneIndices[BIndex];
                                    else
                                        BIndex = 0;

                                    v[vi++] = BIndex;
                                    v[vi++] = 0;

                                    vcount[vci++] = 1;
                                }

                                Weights.Add("1", 0);
                            }

                            Array.Resize(ref v, vi);

                            Controller.skin.src.Add(new DAESource($"{Controller.name}_names", 1, BoneNames, "JOINT", "Name"));
                            Controller.skin.src.Add(new DAESource($"{Controller.name}_poses", 16, BindPoses, "TRANSFORM", "float4x4"));
                            Controller.skin.src.Add(new DAESource($"{Controller.name}_weights", 1, Weights.Keys.ToArray(), "WEIGHT", "float"));

                            Controller.skin.joints.AddInput("JOINT", $"#{Controller.skin.src[0].id}");
                            Controller.skin.joints.AddInput("INV_BIND_MATRIX", $"#{Controller.skin.src[1].id}");

                            Controller.skin.vertex_weights.AddInput("JOINT", $"#{Controller.skin.src[0].id}", 0);
                            Controller.skin.vertex_weights.AddInput("WEIGHT", $"#{Controller.skin.src[2].id}", 1);

                            Controller.skin.vertex_weights.vcount = string.Join(" ", vcount);
                            Controller.skin.vertex_weights.v      = string.Join(" ", v);

                            library_controllers.Add(Controller);
                        }

                        //Mesh node
                        DAENode Node = new DAENode();

                        Node.name   = $"{MeshName}_node";
                        Node.id     = $"{Node.name}_id";
                        Node.matrix = DAEMatrix.Identity;

                        DAENodeInstance NodeInstance = new DAENodeInstance();

                        NodeInstance.url = $"#{(HasController ? Controller.id : Geometry.id)}";
                        NodeInstance.bind_material.technique_common.instance_material.symbol = MtlName;
                        NodeInstance.bind_material.technique_common.instance_material.target = $"#{MtlTgt}";
                        NodeInstance.bind_material.technique_common.instance_material.bind_vertex_input = new List<DAEBindVertexInput>
                        {
                            new DAEBindVertexInput { semantic = "uv", input_semantic = "TEXCOORD", input_set = 0 }
                        };

                        if (HasController)
                        {
                            NodeInstance.skeleton = $"#{VN.node[0].id}";
                            Node.instance_controller = NodeInstance;
                        }
                        else
                        {
                            Node.instance_geometry = NodeInstance;
                        }

                        VN.node.Add(Node);
                    } //SubMesh Loop
                } //Mesh Loop

                library_visual_scenes.Add(VN);

                if (library_visual_scenes.Count > 0)
                {
                    scene.instance_visual_scene.url = $"#{library_visual_scenes[0].id}";
                }

                if (Scene.Textures.Count > 0)
                {
                    library_images = new List<DAEImage>();
                }

                foreach (H3DTexture Tex in Scene.Textures)
                {
                    library_images.Add(new DAEImage()
                    {
                        id        = Tex.Name,
                        init_from = $"./{Tex.Name}.png"
                    });
                }
            } //MdlIndex != -1

            if (AnimIndex != -1)
            {
                library_animations = new List<DAEAnimation>();

                H3DAnimation SklAnim = Scene.SkeletalAnimations[AnimIndex];

                H3DDict<H3DBone> Skeleton = Scene.Models[0].Skeleton;

                int FramesCount = (int)SklAnim.FramesCount + 1;

                // Build bone name → index map
                Dictionary<string, int> BoneNameToIdx = new Dictionary<string, int>();
                for (int i = 0; i < Skeleton.Count; i++)
                {
                    if (!string.IsNullOrEmpty(Skeleton[i].Name))
                        BoneNameToIdx[Skeleton[i].Name] = i;
                }

                // Only export channels for bones that have actual animation data
                foreach (H3DAnimationElement Elem in SklAnim.Elements)
                {
                    if (string.IsNullOrEmpty(Elem.Name)) continue;

                    if (Elem.PrimitiveType != H3DPrimitiveType.Transform &&
                        Elem.PrimitiveType != H3DPrimitiveType.QuatTransform)
                        continue;

                    if (!BoneNameToIdx.ContainsKey(Elem.Name)) continue;

                    int BoneIdx = BoneNameToIdx[Elem.Name];
                    H3DBone SklBone = Skeleton[BoneIdx];

                    H3DBone Parent = null;
                    H3DAnimationElement PElem = null;

                    if (SklBone.ParentIndex != -1)
                    {
                        Parent = Skeleton[SklBone.ParentIndex];
                        PElem = SklAnim.Elements.FirstOrDefault(x => x.Name == Parent.Name);
                    }

                    string[] AnimTimes = new string[FramesCount];
                    string[] AnimPoses = new string[FramesCount];
                    string[] AnimLerps = new string[FramesCount];

                    for (int Frame = 0; Frame < FramesCount; Frame++)
                    {
                        Vector3 T = SklBone.Translation;
                        Vector3 S = SklBone.Scale;
                        Matrix4x4 R;

                        if (Elem.Content is H3DAnimTransform Transform)
                        {
                            if (Transform.TranslationX.Exists) T.X = Transform.TranslationX.GetFrameValue(Frame);
                            if (Transform.TranslationY.Exists) T.Y = Transform.TranslationY.GetFrameValue(Frame);
                            if (Transform.TranslationZ.Exists) T.Z = Transform.TranslationZ.GetFrameValue(Frame);

                            float Rx = Transform.RotationX.Exists ? Transform.RotationX.GetFrameValue(Frame) : SklBone.Rotation.X;
                            float Ry = Transform.RotationY.Exists ? Transform.RotationY.GetFrameValue(Frame) : SklBone.Rotation.Y;
                            float Rz = Transform.RotationZ.Exists ? Transform.RotationZ.GetFrameValue(Frame) : SklBone.Rotation.Z;

                            R = Matrix4x4.CreateRotationX(Rx) *
                                Matrix4x4.CreateRotationY(Ry) *
                                Matrix4x4.CreateRotationZ(Rz);

                            if (Transform.ScaleX.Exists) S.X = Transform.ScaleX.GetFrameValue(Frame);
                            if (Transform.ScaleY.Exists) S.Y = Transform.ScaleY.GetFrameValue(Frame);
                            if (Transform.ScaleZ.Exists) S.Z = Transform.ScaleZ.GetFrameValue(Frame);
                        }
                        else if (Elem.Content is H3DAnimQuatTransform QT)
                        {
                            if (QT.HasTranslation) T = QT.GetTranslationValue(Frame);
                            if (QT.HasRotation)    R = Matrix4x4.CreateFromQuaternion(QT.GetRotationValue(Frame));
                            else                   R = Matrix4x4.CreateRotationX(SklBone.Rotation.X) *
                                                       Matrix4x4.CreateRotationY(SklBone.Rotation.Y) *
                                                       Matrix4x4.CreateRotationZ(SklBone.Rotation.Z);
                            if (QT.HasScale)       S = QT.GetScaleValue(Frame);
                        }
                        else
                        {
                            R = Matrix4x4.CreateRotationX(SklBone.Rotation.X) *
                                Matrix4x4.CreateRotationY(SklBone.Rotation.Y) *
                                Matrix4x4.CreateRotationZ(SklBone.Rotation.Z);
                        }

                        // Scale compensation — remove parent scale inheritance
                        if (Parent != null && (SklBone.Flags & H3DBoneFlags.IsSegmentScaleCompensate) != 0)
                        {
                            Vector3 PS = Parent.Scale;

                            if (PElem != null)
                            {
                                if (PElem.Content is H3DAnimTransform PT)
                                {
                                    if (PT.ScaleX.Exists) PS.X = PT.ScaleX.GetFrameValue(Frame);
                                    if (PT.ScaleY.Exists) PS.Y = PT.ScaleY.GetFrameValue(Frame);
                                    if (PT.ScaleZ.Exists) PS.Z = PT.ScaleZ.GetFrameValue(Frame);
                                }
                                else if (PElem.Content is H3DAnimQuatTransform PQT && PQT.HasScale)
                                {
                                    PS = PQT.GetScaleValue(Frame);
                                }
                            }

                            S /= PS;
                        }

                        // Build local transform: S * R * T (same order as H3DBone.Transform)
                        Matrix4x4 LocalMtx = Matrix4x4.CreateScale(S) * R * Matrix4x4.CreateTranslation(T);

                        AnimTimes[Frame] = (Frame / 30f).ToString(CultureInfo.InvariantCulture);
                        AnimPoses[Frame] = DAEUtils.MatrixStr(new Matrix3x4(LocalMtx));
                        AnimLerps[Frame] = "LINEAR";
                    }

                    DAEAnimation Anim = new DAEAnimation();

                    Anim.name = $"{SklAnim.Name}_{SklBone.Name}_transform";
                    Anim.id   = $"{Anim.name}_id";

                    Anim.src.Add(new DAESource($"{Anim.name}_frame",  1, AnimTimes, "TIME",          "float"));
                    Anim.src.Add(new DAESource($"{Anim.name}_interp", 1, AnimLerps, "INTERPOLATION", "Name"));
                    Anim.src.Add(new DAESource($"{Anim.name}_pose",  16, AnimPoses, "TRANSFORM",     "float4x4"));

                    Anim.sampler.AddInput("INPUT",         $"#{Anim.src[0].id}");
                    Anim.sampler.AddInput("INTERPOLATION", $"#{Anim.src[1].id}");
                    Anim.sampler.AddInput("OUTPUT",        $"#{Anim.src[2].id}");

                    Anim.sampler.id     = $"{Anim.name}_samp_id";
                    Anim.channel.source = $"#{Anim.sampler.id}";
                    Anim.channel.target = $"{SklBone.Name}_bone_id/transform";

                    library_animations.Add(Anim);
                } //Animation elements
            } //AnimIndex != -1
        }

        private static float ApplyWrap(float coord, PICATextureWrap wrap)
        {
            switch (wrap)
            {
                case PICATextureWrap.Mirror:
                    // Fold into 0-2 range, then mirror the 1-2 portion
                    coord = Math.Abs(coord);
                    int period = (int)Math.Floor(coord);
                    float frac = coord - period;
                    return (period % 2 == 0) ? frac : 1f - frac;

                case PICATextureWrap.Repeat:
                    coord = coord % 1f;
                    if (coord < 0) coord += 1f;
                    return coord;

                case PICATextureWrap.ClampToEdge:
                    return Math.Clamp(coord, 0f, 1f);

                case PICATextureWrap.ClampToBorder:
                    return Math.Clamp(coord, 0f, 1f);

                default:
                    return coord;
            }
        }

        private static Vector4 TransformUV(Vector4 uv, Matrix3x4 m, PICATextureWrap wrapU, PICATextureWrap wrapV)
        {
            float u = uv.X;
            float v = uv.Y;
            float tu = m.M11 * u + m.M21 * v + m.M41;
            float tv = m.M12 * u + m.M22 * v + m.M42;
            return new Vector4(
                ApplyWrap(tu, wrapU),
                ApplyWrap(tv, wrapV),
                uv.Z,
                uv.W);
        }

        public void Save(string FileName)
        {
            using (FileStream FS = new FileStream(FileName, FileMode.Create))
            {
                XmlSerializer Serializer = new XmlSerializer(typeof(DAE));

                Serializer.Serialize(FS, this);
            }
        }
    }
}
