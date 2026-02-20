using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MiniToolbox.Garc.Models.GenericFormats
{
    public class DAE
    {
        private const float AnimationFramesPerSecond = 30f;

        public static bool DiagnosticLogging { get; set; }

        [XmlRootAttribute("COLLADA", Namespace = "http://www.collada.org/2005/11/COLLADASchema")]
        public class COLLADA
        {
            [XmlAttribute]
            public string version = "1.4.1";

            public daeAsset asset = new daeAsset();

            [XmlArrayItem("image")]
            public List<daeImage> library_images = new List<daeImage>();

            [XmlArrayItem("material")]
            public List<daeMaterial> library_materials = new List<daeMaterial>();

            [XmlArrayItem("effect")]
            public List<daeEffect> library_effects = new List<daeEffect>();

            [XmlArrayItem("geometry")]
            public List<daeGeometry> library_geometries = new List<daeGeometry>();

            [XmlArrayItem("controller")]
            public List<daeController> library_controllers;

            [XmlArrayItem("visual_scene")]
            public List<daeVisualScene> library_visual_scenes = new List<daeVisualScene>();

            [XmlArrayItem("animation")]
            public List<daeAnimation> library_animations = new List<daeAnimation>();

            [XmlArrayItem("instance_visual_scene")]
            public List<daeInstaceVisualScene> scene = new List<daeInstaceVisualScene>();
        }

        public class daeAsset
        {
            public string created;
            public string modified;
            public string up_axis;
        }

        public class daeImage
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            public string init_from;
        }

        public class daeInstanceEffect
        {
            [XmlAttribute]
            public string url;
        }

        public class daeMaterial
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            public daeInstanceEffect instance_effect = new daeInstanceEffect();
        }

        public class daeParamSurfaceElement
        {
            [XmlAttribute]
            public string type;

            public string init_from;
            public string format;
        }

        public class daeParamSampler2DElement
        {
            public string source;
            public string wrap_s;
            public string wrap_t;
            public string minfilter;
            public string magfilter;
            public string mipfilter;
        }

        public class daeParam
        {
            [XmlAttribute]
            public string sid;

            [XmlElement(IsNullable = false)]
            public daeParamSurfaceElement surface;

            [XmlElement(IsNullable = false)]
            public daeParamSampler2DElement sampler2D;
        }

        public class daePhongDiffuseTexture
        {
            [XmlAttribute]
            public string texture;

            [XmlAttribute]
            public string texcoord = "uv";
        }

        public class daePhongDiffuse
        {
            public daePhongDiffuseTexture texture = new daePhongDiffuseTexture();
        }

        public class daeColor
        {
            public string color;

            public void set(Color col)
            {
                color = string.Format(
                    "{0} {1} {2} {3}",
                    getString(col.R / 255f),
                    getString(col.G / 255f),
                    getString(col.B / 255f),
                    getString(col.A / 255f));
            }

            private string getString(float value)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public class daePhong
        {
            public daeColor emission = new daeColor();
            public daeColor ambient = new daeColor();
            public daePhongDiffuse diffuse = new daePhongDiffuse();
            public daeColor specular = new daeColor();
        }

        public class daeTechnique
        {
            [XmlAttribute]
            public string sid;

            public daePhong phong = new daePhong();
        }

        public class daeProfile
        {
            [XmlAttribute]
            public string sid;

            [XmlElement("newparam")]
            public List<daeParam> newparam = new List<daeParam>();
            public daeTechnique technique = new daeTechnique();
        }

        public class daeEffect
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            public daeProfile profile_COMMON = new daeProfile();
        }

        public class daeFloatArray
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public uint count;

            [XmlText]
            public string data;

            public void set(List<float> content)
            {
                StringBuilder strArray = new StringBuilder();
                for (int i = 0; i < content.Count; i++)
                {
                    if (i < content.Count - 1)
                        strArray.Append(content[i].ToString(CultureInfo.InvariantCulture) + " ");
                    else
                        strArray.Append(content[i].ToString(CultureInfo.InvariantCulture));
                }
                count = (uint)content.Count;
                data = strArray.ToString();
            }

            public List<float> get()
            {
                List<float> output = new List<float>();
                string[] values = data.Split(Convert.ToChar(" "));
                for (int i = 0; i < values.Length; i++) output.Add(float.Parse(values[i]));
                return output;
            }
        }

        public class daeNameArray
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public uint count;

            [XmlText]
            public string data;

            public void set(List<string> content)
            {
                StringBuilder strArray = new StringBuilder();
                for (int i = 0; i < content.Count; i++)
                {
                    if (i < content.Count - 1)
                        strArray.Append(content[i] + " ");
                    else
                        strArray.Append(content[i]);
                }
                count = (uint)content.Count;
                data = strArray.ToString();
            }

            public List<string> get()
            {
                List<string> output = new List<string>();
                string[] values = data.Split(Convert.ToChar(" "));
                output.AddRange(values);
                return output;
            }
        }

        public class daeAccessorParam
        {
            [XmlAttribute]
            public string name;

            [XmlAttribute]
            public string type;
        }

        public class daeAccessor
        {
            [XmlAttribute]
            public string source;

            [XmlAttribute]
            public uint count;

            [XmlAttribute]
            public uint stride;

            [XmlElement("param")]
            public List<daeAccessorParam> param = new List<daeAccessorParam>();

            public void addParam(string name, string type)
            {
                daeAccessorParam prm = new daeAccessorParam();

                prm.name = name;
                prm.type = type;

                param.Add(prm);
            }
        }

        public class daeMeshTechnique
        {
            public daeAccessor accessor = new daeAccessor();
        }

        public class daeSource
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            [XmlElement(IsNullable = false)]
            public daeNameArray Name_array;

            [XmlElement(IsNullable = false)]
            public daeFloatArray float_array;

            public daeMeshTechnique technique_common = new daeMeshTechnique();
        }

        public class daeInput
        {
            [XmlAttribute]
            public string semantic;

            [XmlAttribute]
            public string source;
        }

        public class daeInputTable
        {
            [XmlAttribute]
            public string id;

            [XmlElement("input")]
            public List<daeInput> input = new List<daeInput>();

            public void addInput(string semantic, string source)
            {
                daeInput i = new daeInput();

                i.semantic = semantic;
                i.source = source;

                input.Add(i);
            }
        }

        public class daeInputOffset
        {
            [XmlAttribute]
            public string semantic;

            [XmlAttribute]
            public string source;

            [XmlAttribute]
            public uint offset;

            [XmlAttribute]
            public uint set;

            public bool ShouldSerializeset() { return semantic == "TEXCOORD"; }
        }

        public class daeTriangles
        {
            [XmlAttribute]
            public string material;

            [XmlAttribute]
            public uint count;

            [XmlElement("input")]
            public List<daeInputOffset> input = new List<daeInputOffset>();

            public string p;

            public void addInput(string semantic, string source, uint offset = 0, uint set = 0)
            {
                daeInputOffset i = new daeInputOffset();

                i.semantic = semantic;
                i.source = source;
                i.offset = offset;
                i.set = set;

                input.Add(i);
            }

            public void set(List<uint> indices)
            {
                StringBuilder strArray = new StringBuilder();
                for (int i = 0; i < indices.Count; i++)
                {
                    if (i < indices.Count - 1)
                        strArray.Append(indices[i].ToString(CultureInfo.InvariantCulture) + " ");
                    else
                        strArray.Append(indices[i].ToString(CultureInfo.InvariantCulture));
                }
                count = (uint)(indices.Count / 3);
                p = strArray.ToString();
            }

            public List<uint> get()
            {
                List<uint> output = new List<uint>();
                string[] values = p.Split(Convert.ToChar(" "));
                for (int i = 0; i < values.Length; i++) output.Add(uint.Parse(values[i]));
                return output;
            }
        }

        public class daeMesh
        {
            [XmlElement("source")]
            public List<daeSource> source = new List<daeSource>();

            public daeInputTable vertices = new daeInputTable();
            public daeTriangles triangles = new daeTriangles();
        }

        public class daeGeometry
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            public daeMesh mesh = new daeMesh();
        }

        public class daeVertexWeights
        {
            [XmlAttribute]
            public uint count;

            [XmlElement("input")]
            public List<daeInputOffset> input = new List<daeInputOffset>();

            public string vcount;
            public string v;

            public void addInput(string semantic, string source, uint offset = 0)
            {
                daeInputOffset i = new daeInputOffset();

                i.semantic = semantic;
                i.source = source;
                i.offset = offset;

                input.Add(i);
            }
        }

        public class daeSkin
        {
            [XmlAttribute]
            public string source;

            public daeMatrix bind_shape_matrix = new daeMatrix();

            [XmlElement("source")]
            public List<daeSource> src = new List<daeSource>();

            public daeInputTable joints = new daeInputTable();
            public daeVertexWeights vertex_weights = new daeVertexWeights();
        }

        public class daeController
        {
            [XmlAttribute]
            public string id;

            public daeSkin skin = new daeSkin();
        }

        public class daeMatrix
        {
            [XmlAttribute]
            public string sid;

            [XmlText]
            public string data;

            public void set(RenderBase.OMatrix mtx)
            {
                StringBuilder strArray = new StringBuilder();
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (i == 3 && j == 3)
                            strArray.Append(mtx[j, i].ToString(CultureInfo.InvariantCulture));
                        else
                            strArray.Append(mtx[j, i].ToString(CultureInfo.InvariantCulture) + " ");
                    }

                }
                data = strArray.ToString();
            }

            public RenderBase.OMatrix get()
            {
                RenderBase.OMatrix output = new RenderBase.OMatrix();
                string[] values = data.Split(Convert.ToChar(" "));
                int k = 0;
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        output[j, i] = float.Parse(values[k++]);
                    }

                }
                return output;
            }
        }

        public class daeBindMaterialInstace
        {
            [XmlAttribute]
            public string symbol;

            [XmlAttribute]
            public string target;

            [XmlElement("bind_vertex_input")]
            public List<daeBindVertexInput> bind_vertex_input = new List<daeBindVertexInput>();
        }

        public class daeBindVertexInput
        {
            [XmlAttribute]
            public string semantic;

            [XmlAttribute]
            public string input_semantic;

            [XmlAttribute]
            public uint input_set;
        }

        public class daeBindMaterial
        {
            public daeBindMaterialInstace instance_material = new daeBindMaterialInstace();
        }

        public class daeBindMaterialTechnique
        {
            public daeBindMaterial technique_common = new daeBindMaterial();
        }

        public class daeInstanceGeometry
        {
            [XmlAttribute]
            public string url;

            public daeBindMaterialTechnique bind_material = new daeBindMaterialTechnique();
        }

        public class daeInstanceController
        {
            [XmlAttribute]
            public string url;

            public string skeleton;
            public daeBindMaterialTechnique bind_material = new daeBindMaterialTechnique();
        }

        public class daeNode
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            [XmlAttribute]
            public string sid;

            [XmlAttribute]
            public string type = "NODE";

            public daeMatrix matrix = new daeMatrix();

            [XmlElement("node", IsNullable = false)]
            public List<daeNode> childs;

            [XmlElement(IsNullable = false)]
            public daeInstanceGeometry instance_geometry;

            [XmlElement(IsNullable = false)]
            public daeInstanceController instance_controller;
        }

        public class daeVisualScene
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            [XmlElement("node")]
            public List<daeNode> node = new List<daeNode>();
        }

        public class daeInstaceVisualScene
        {
            [XmlAttribute]
            public string url;
        }

        public class daeAnimationSampler
        {
            [XmlAttribute]
            public string id;

            [XmlElement("input")]
            public List<daeInput> input = new List<daeInput>();

            public void addInput(string semantic, string source)
            {
                daeInput i = new daeInput();
                i.semantic = semantic;
                i.source = source;
                input.Add(i);
            }
        }

        public class daeChannel
        {
            [XmlAttribute]
            public string source;

            [XmlAttribute]
            public string target;
        }

        public class daeAnimation
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            [XmlElement("source")]
            public List<daeSource> source = new List<daeSource>();

            public daeAnimationSampler sampler = new daeAnimationSampler();
            public daeChannel channel = new daeChannel();
        }

        /// <summary>
        ///     Exports a Model to the Collada format.
        ///     See: https://www.khronos.org/files/collada_spec_1_4.pdf for more information.
        /// </summary>
        /// <param name="model">The Model that will be exported</param>
        /// <param name="fileName">The output File Name</param>
        /// <param name="modelIndex">Index of the model to be exported</param>
        /// <param name="skeletalAnimationIndex">(Optional) Index of the skeletal animation</param>
        public static void export(
            RenderBase.OModelGroup model,
            string fileName,
            int modelIndex,
            int skeletalAnimationIndex = -1)
        {
            RenderBase.OModel mdl = model.model[modelIndex];
            COLLADA dae = new COLLADA();

            dae.asset.created = DateTime.Now.ToString("yyyy-MM-ddThh:mm:ssZ");
            dae.asset.modified = dae.asset.created;
            dae.asset.up_axis = "Y_UP";

            foreach (RenderBase.OTexture tex in model.texture)
            {
                daeImage img = new daeImage();
                img.id = tex.name + "_id";
                img.name = tex.name;
                img.init_from = "./" + tex.name + ".png";

                dae.library_images.Add(img);
            }

            foreach (RenderBase.OMaterial mat in mdl.material)
            {
                daeMaterial mtl = new daeMaterial();
                mtl.name = mat.name + "_mat";
                mtl.id = mtl.name + "_id";
                mtl.instance_effect.url = "#eff_" + mat.name + "_id";

                dae.library_materials.Add(mtl);

                daeEffect eff = new daeEffect();
                eff.id = "eff_" + mat.name + "_id";
                eff.name = "eff_" + mat.name;

                string surfaceSid = "img_surface_" + mat.name;
                string samplerSid = "img_sampler_" + mat.name;

                daeParam surface = new daeParam();
                surface.surface = new daeParamSurfaceElement();
                surface.sid = surfaceSid;
                surface.surface.type = "2D";
                surface.surface.init_from = mat.name0 + "_id";
                surface.surface.format = "PNG";
                eff.profile_COMMON.newparam.Add(surface);

                daeParam sampler = new daeParam();
                sampler.sampler2D = new daeParamSampler2DElement();
                sampler.sid = samplerSid;
                sampler.sampler2D.source = surfaceSid;

                switch (mat.textureMapper[0].wrapU)
                {
                    case RenderBase.OTextureWrap.repeat: sampler.sampler2D.wrap_s = "WRAP"; break;
                    case RenderBase.OTextureWrap.mirroredRepeat: sampler.sampler2D.wrap_s = "MIRROR"; break;
                    case RenderBase.OTextureWrap.clampToEdge: sampler.sampler2D.wrap_s = "CLAMP"; break;
                    case RenderBase.OTextureWrap.clampToBorder: sampler.sampler2D.wrap_s = "BORDER"; break;
                    default: sampler.sampler2D.wrap_s = "NONE"; break;
                }

                switch (mat.textureMapper[0].wrapV)
                {
                    case RenderBase.OTextureWrap.repeat: sampler.sampler2D.wrap_t = "WRAP"; break;
                    case RenderBase.OTextureWrap.mirroredRepeat: sampler.sampler2D.wrap_t = "MIRROR"; break;
                    case RenderBase.OTextureWrap.clampToEdge: sampler.sampler2D.wrap_t = "CLAMP"; break;
                    case RenderBase.OTextureWrap.clampToBorder: sampler.sampler2D.wrap_t = "BORDER"; break;
                    default: sampler.sampler2D.wrap_t = "NONE"; break;
                }

                switch (mat.textureMapper[0].minFilter)
                {
                    case RenderBase.OTextureMinFilter.linearMipmapLinear: sampler.sampler2D.minfilter = "LINEAR_MIPMAP_LINEAR"; break;
                    case RenderBase.OTextureMinFilter.linearMipmapNearest: sampler.sampler2D.minfilter = "LINEAR_MIPMAP_NEAREST"; break;
                    case RenderBase.OTextureMinFilter.nearestMipmapLinear: sampler.sampler2D.minfilter = "NEAREST_MIPMAP_LINEAR"; break;
                    case RenderBase.OTextureMinFilter.nearestMipmapNearest: sampler.sampler2D.minfilter = "NEAREST_MIPMAP_NEAREST"; break;
                    default: sampler.sampler2D.minfilter = "NONE"; break;
                }

                switch (mat.textureMapper[0].magFilter)
                {
                    case RenderBase.OTextureMagFilter.linear: sampler.sampler2D.magfilter = "LINEAR"; break;
                    case RenderBase.OTextureMagFilter.nearest: sampler.sampler2D.magfilter = "NEAREST"; break;
                    default: sampler.sampler2D.magfilter = "NONE"; break;
                }

                sampler.sampler2D.mipfilter = sampler.sampler2D.magfilter;

                eff.profile_COMMON.newparam.Add(sampler);

                eff.profile_COMMON.technique.sid = "img_technique";
                eff.profile_COMMON.technique.phong.emission.set(Color.Black);
                eff.profile_COMMON.technique.phong.ambient.set(Color.Black);
                eff.profile_COMMON.technique.phong.specular.set(Color.White);
                eff.profile_COMMON.technique.phong.diffuse.texture.texture = samplerSid;

                dae.library_effects.Add(eff);
            }

            string jointNames = null;
            string invBindPoses = null;
            for (int index = 0; index < mdl.skeleton.Count; index++)
            {
                RenderBase.OMatrix transform = new RenderBase.OMatrix();
                transformSkeleton(mdl.skeleton, index, ref transform);

                jointNames += mdl.skeleton[index].name;
                daeMatrix mtx = new daeMatrix();
                mtx.set(transform.invert());
                invBindPoses += mtx.data;
                if (index < mdl.skeleton.Count - 1)
                {
                    jointNames += " ";
                    invBindPoses += " ";
                }
            }

            int meshIndex = 0;
            daeVisualScene vs = new daeVisualScene();
            vs.name = "vs_" + mdl.name;
            vs.id = vs.name + "_id";
            if (mdl.skeleton.Count > 0) writeSkeleton(mdl.skeleton, 0, ref vs.node);
            foreach (RenderBase.OMesh obj in mdl.mesh)
            {
                //Geometry
                daeGeometry geometry = new daeGeometry();
                RenderBase.OMaterial meshMaterial = getMeshMaterial(mdl, obj);

                string meshName = "mesh_" + meshIndex++ + "_" + obj.name;
                geometry.id = meshName + "_id";
                geometry.name = meshName;

                MeshUtils.optimizedMesh mesh = MeshUtils.optimizeMesh(obj);
                List<float> positions = new List<float>();
                List<float> normals = new List<float>();
                List<float> uv0 = new List<float>();
                List<float> uv1 = new List<float>();
                List<float> uv2 = new List<float>();
                List<float> colors = new List<float>();
                foreach (RenderBase.OVertex vtx in mesh.vertices)
                {
                    positions.Add(vtx.position.x);
                    positions.Add(vtx.position.y);
                    positions.Add(vtx.position.z);

                    if (mesh.hasNormal)
                    {
                        normals.Add(vtx.normal.x);
                        normals.Add(vtx.normal.y);
                        normals.Add(vtx.normal.z);
                    }

                    if (mesh.texUVCount > 0)
                    {
                        RenderBase.OVector2 transformedUv0 = applyTextureCoordinator(vtx.texture0, meshMaterial.textureCoordinator[0]);
                        uv0.Add(transformedUv0.x);
                        uv0.Add(transformedUv0.y);
                    }

                    if (mesh.texUVCount > 1)
                    {
                        RenderBase.OVector2 transformedUv1 = applyTextureCoordinator(vtx.texture1, meshMaterial.textureCoordinator[1]);
                        uv1.Add(transformedUv1.x);
                        uv1.Add(transformedUv1.y);
                    }

                    if (mesh.texUVCount > 2)
                    {
                        RenderBase.OVector2 transformedUv2 = applyTextureCoordinator(vtx.texture2, meshMaterial.textureCoordinator[2]);
                        uv2.Add(transformedUv2.x);
                        uv2.Add(transformedUv2.y);
                    }

                    if (mesh.hasColor)
                    {
                        colors.Add(((vtx.diffuseColor >> 16) & 0xff) / 255f);
                        colors.Add(((vtx.diffuseColor >> 8) & 0xff) / 255f);
                        colors.Add((vtx.diffuseColor & 0xff) / 255f);
                        colors.Add(((vtx.diffuseColor >> 24) & 0xff) / 255f);
                    }
                }

                daeSource position = new daeSource();
                position.name = meshName + "_position";
                position.id = position.name + "_id";
                position.float_array = new daeFloatArray();
                position.float_array.id = position.name + "_array_id";
                position.float_array.set(positions);
                position.technique_common.accessor.source = "#" + position.float_array.id;
                position.technique_common.accessor.count = (uint)mesh.vertices.Count;
                position.technique_common.accessor.stride = 3;
                position.technique_common.accessor.addParam("X", "float");
                position.technique_common.accessor.addParam("Y", "float");
                position.technique_common.accessor.addParam("Z", "float");

                geometry.mesh.source.Add(position);

                daeSource normal = new daeSource();
                if (mesh.hasNormal)
                {
                    normal.name = meshName + "_normal";
                    normal.id = normal.name + "_id";
                    normal.float_array = new daeFloatArray();
                    normal.float_array.id = normal.name + "_array_id";
                    normal.float_array.set(normals);
                    normal.technique_common.accessor.source = "#" + normal.float_array.id;
                    normal.technique_common.accessor.count = (uint)mesh.vertices.Count;
                    normal.technique_common.accessor.stride = 3;
                    normal.technique_common.accessor.addParam("X", "float");
                    normal.technique_common.accessor.addParam("Y", "float");
                    normal.technique_common.accessor.addParam("Z", "float");

                    geometry.mesh.source.Add(normal);
                }

                daeSource[] texUV = new daeSource[3];
                for (int i = 0; i < mesh.texUVCount; i++)
                {
                    texUV[i] = new daeSource();

                    texUV[i].name = meshName + "_uv" + i;
                    texUV[i].id = texUV[i].name + "_id";
                    texUV[i].float_array = new daeFloatArray();
                    texUV[i].float_array.id = texUV[i].name + "_array_id";
                    texUV[i].technique_common.accessor.source = "#" + texUV[i].float_array.id;
                    texUV[i].technique_common.accessor.count = (uint)mesh.vertices.Count;
                    texUV[i].technique_common.accessor.stride = 2;
                    texUV[i].technique_common.accessor.addParam("S", "float");
                    texUV[i].technique_common.accessor.addParam("T", "float");

                    geometry.mesh.source.Add(texUV[i]);
                }

                daeSource color = new daeSource();
                if (mesh.hasColor)
                {
                    color.name = meshName + "_color";
                    color.id = color.name + "_id";
                    color.float_array = new daeFloatArray();
                    color.float_array.id = color.name + "_array_id";
                    color.float_array.set(colors);
                    color.technique_common.accessor.source = "#" + color.float_array.id;
                    color.technique_common.accessor.count = (uint)mesh.vertices.Count;
                    color.technique_common.accessor.stride = 4;
                    color.technique_common.accessor.addParam("R", "float");
                    color.technique_common.accessor.addParam("G", "float");
                    color.technique_common.accessor.addParam("B", "float");
                    color.technique_common.accessor.addParam("A", "float");

                    geometry.mesh.source.Add(color);
                }

                geometry.mesh.vertices.id = meshName + "_vertices_id";
                geometry.mesh.vertices.addInput("POSITION", "#" + position.id);


                geometry.mesh.triangles.material = meshMaterial.name;
                geometry.mesh.triangles.addInput("VERTEX", "#" + geometry.mesh.vertices.id);
                if (mesh.hasNormal) geometry.mesh.triangles.addInput("NORMAL", "#" + normal.id);
                if (mesh.hasColor) geometry.mesh.triangles.addInput("COLOR", "#" + color.id);
                if (mesh.texUVCount > 0)
                {
                    texUV[0].float_array.set(uv0);
                    geometry.mesh.triangles.addInput("TEXCOORD", "#" + texUV[0].id);
                }
                if (mesh.texUVCount > 1)
                {
                    texUV[1].float_array.set(uv1);
                    geometry.mesh.triangles.addInput("TEXCOORD", "#" + texUV[1].id, 0, 1);
                }
                if (mesh.texUVCount > 2)
                {
                    texUV[2].float_array.set(uv2);
                    geometry.mesh.triangles.addInput("TEXCOORD", "#" + texUV[2].id, 0, 2);
                }
                geometry.mesh.triangles.set(mesh.indices);

                dae.library_geometries.Add(geometry);

                bool hasNode = obj.vertices[0].node.Count > 0;
                bool hasWeight = obj.vertices[0].weight.Count > 0;
                bool hasController = hasNode && hasWeight && mdl.skeleton.Count > 0;

                //Controller
                daeController controller = new daeController();
                if (hasController)
                {
                    controller.id = meshName + "_ctrl_id";

                    controller.skin.source = "#" + geometry.id;
                    controller.skin.bind_shape_matrix.set(new RenderBase.OMatrix());

                    daeSource joints = new daeSource();
                    joints.id = meshName + "_ctrl_joint_names_id";
                    joints.Name_array = new daeNameArray();
                    joints.Name_array.id = meshName + "_ctrl_joint_names_array_id";
                    joints.Name_array.count = (uint)mdl.skeleton.Count;
                    joints.Name_array.data = jointNames;
                    joints.technique_common.accessor.source = "#" + joints.Name_array.id;
                    joints.technique_common.accessor.count = joints.Name_array.count;
                    joints.technique_common.accessor.stride = 1;
                    joints.technique_common.accessor.addParam("JOINT", "Name");

                    controller.skin.src.Add(joints);

                    daeSource bindPoses = new daeSource();
                    bindPoses.id = meshName + "_ctrl_inv_bind_poses_id";
                    bindPoses.float_array = new daeFloatArray();
                    bindPoses.float_array.id = meshName + "_ctrl_inv_bind_poses_array_id";
                    bindPoses.float_array.count = (uint)(mdl.skeleton.Count * 16);
                    bindPoses.float_array.data = invBindPoses;
                    bindPoses.technique_common.accessor.source = "#" + bindPoses.float_array.id;
                    bindPoses.technique_common.accessor.count = (uint)mdl.skeleton.Count;
                    bindPoses.technique_common.accessor.stride = 16;
                    bindPoses.technique_common.accessor.addParam("TRANSFORM", "float4x4");

                    controller.skin.src.Add(bindPoses);

                    daeSource weights = new daeSource();
                    weights.id = meshName + "_ctrl_weights_id";
                    weights.float_array = new daeFloatArray();
                    weights.float_array.id = meshName + "_ctrl_weights_array_id";
                    weights.technique_common.accessor.source = "#" + weights.float_array.id;
                    weights.technique_common.accessor.stride = 1;
                    weights.technique_common.accessor.addParam("WEIGHT", "float");

                    StringBuilder w = new StringBuilder();
                    StringBuilder vcount = new StringBuilder();
                    StringBuilder v = new StringBuilder();

                    float[] wLookBack = new float[32];
                    uint wLookBackIndex = 0;
                    int buffLen = 0;

                    int wIndex = 0;
                    int wCount = 0;
                    foreach (RenderBase.OVertex vtx in mesh.vertices)
                    {
                        int count = Math.Min(vtx.node.Count, vtx.weight.Count);

                        vcount.Append(count + " ");
                        for (int n = 0; n < count; n++)
                        {
                            v.Append(vtx.node[n] + " ");
                            bool found = false;
                            uint bPos = (wLookBackIndex - 1) & 0x1f;
                            for (int i = 0; i < buffLen; i++)
                            {
                                if (wLookBack[bPos] == vtx.weight[n])
                                {
                                    v.Append(wIndex - (i + 1) + " ");
                                    found = true;
                                    break;
                                }
                                bPos = (bPos - 1) & 0x1f;
                            }

                            if (!found)
                            {
                                v.Append(wIndex++ + " ");
                                w.Append(vtx.weight[n].ToString(CultureInfo.InvariantCulture) + " ");
                                wCount++;

                                wLookBack[wLookBackIndex] = vtx.weight[n];
                                wLookBackIndex = (wLookBackIndex + 1) & 0x1f;
                                if (buffLen < wLookBack.Length) buffLen++;
                            }
                        }
                    }

                    weights.float_array.data = w.ToString().TrimEnd();
                    weights.float_array.count = (uint)wCount;
                    weights.technique_common.accessor.count = (uint)wCount;

                    controller.skin.src.Add(weights);
                    controller.skin.vertex_weights.vcount = vcount.ToString().TrimEnd();
                    controller.skin.vertex_weights.v = v.ToString().TrimEnd();
                    controller.skin.vertex_weights.count = (uint)mesh.vertices.Count;
                    controller.skin.joints.addInput("JOINT", "#" + joints.id);
                    controller.skin.joints.addInput("INV_BIND_MATRIX", "#" + bindPoses.id);

                    controller.skin.vertex_weights.addInput("JOINT", "#" + joints.id);
                    controller.skin.vertex_weights.addInput("WEIGHT", "#" + weights.id, 1);

                    if (dae.library_controllers == null) dae.library_controllers = new List<daeController>();
                    dae.library_controllers.Add(controller);
                }

                //Visual scene node
                daeNode node = new daeNode();
                node.name = "vsn_" + meshName;
                node.id = node.name + "_id";
                node.matrix.set(new RenderBase.OMatrix());
                if (hasController)
                {
                    node.instance_controller = new daeInstanceController();
                    node.instance_controller.url = "#" + controller.id;
                    node.instance_controller.skeleton = "#" + mdl.skeleton[0].name + "_bone_id";
                    node.instance_controller.bind_material.technique_common.instance_material.symbol = meshMaterial.name;
                    node.instance_controller.bind_material.technique_common.instance_material.target = "#" + meshMaterial.name + "_mat_id";
                    addTexcoordBinding(node.instance_controller.bind_material.technique_common.instance_material, mesh.texUVCount);
                }
                else
                {
                    node.instance_geometry = new daeInstanceGeometry();
                    node.instance_geometry.url = "#" + geometry.id;
                    node.instance_geometry.bind_material.technique_common.instance_material.symbol = meshMaterial.name;
                    node.instance_geometry.bind_material.technique_common.instance_material.target = "#" + meshMaterial.name + "_mat_id";
                    addTexcoordBinding(node.instance_geometry.bind_material.technique_common.instance_material, mesh.texUVCount);
                }

                vs.node.Add(node);
            }

            if (skeletalAnimationIndex >= 0)
            {
                exportAnimation(dae, model, mdl, skeletalAnimationIndex);
            }

            dae.library_visual_scenes.Add(vs);

            daeInstaceVisualScene scene = new daeInstaceVisualScene();
            scene.url = "#" + vs.id;
            dae.scene.Add(scene);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "\t"
            };

            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.collada.org/2005/11/COLLADASchema");
            XmlSerializer serializer = new XmlSerializer(typeof(COLLADA));
            using (XmlWriter output = XmlWriter.Create(new FileStream(fileName, FileMode.Create), settings))
            {
                serializer.Serialize(output, dae, ns);
            }
        }

        public static void exportSkeletalClip(
            RenderBase.OModelGroup model,
            string fileName,
            int modelIndex,
            int skeletalAnimationIndex)
        {
            RenderBase.OModel mdl = model.model[modelIndex];
            COLLADA dae = new COLLADA();

            dae.asset.created = DateTime.Now.ToString("yyyy-MM-ddThh:mm:ssZ");
            dae.asset.modified = dae.asset.created;
            dae.asset.up_axis = "Y_UP";

            daeVisualScene vs = new daeVisualScene();
            vs.name = "vs_" + mdl.name;
            vs.id = vs.name + "_id";
            if (mdl.skeleton.Count > 0) writeSkeleton(mdl.skeleton, 0, ref vs.node);

            exportAnimation(dae, model, mdl, skeletalAnimationIndex);

            dae.library_visual_scenes.Add(vs);

            daeInstaceVisualScene scene = new daeInstaceVisualScene();
            scene.url = "#" + vs.id;
            dae.scene.Add(scene);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "\t"
            };

            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.collada.org/2005/11/COLLADASchema");
            XmlSerializer serializer = new XmlSerializer(typeof(COLLADA));
            using (XmlWriter output = XmlWriter.Create(new FileStream(fileName, FileMode.Create), settings))
            {
                serializer.Serialize(output, dae, ns);
            }
        }

        public static void exportAnimation(
            COLLADA dae,
            RenderBase.OModelGroup model,
            RenderBase.OModel mdl,
            int animIndex)
        {
            if (model.skeletalAnimation == null) return;
            if (animIndex < 0 || animIndex >= model.skeletalAnimation.list.Count) return;

            RenderBase.OSkeletalAnimation anim = model.skeletalAnimation.list[animIndex] as RenderBase.OSkeletalAnimation;
            if (anim == null) return;

            Dictionary<string, RenderBase.OBone> skeletonByName = new Dictionary<string, RenderBase.OBone>();
            foreach (RenderBase.OBone skeletonBone in mdl.skeleton)
            {
                if (!skeletonByName.ContainsKey(skeletonBone.name)) skeletonByName.Add(skeletonBone.name, skeletonBone);
            }

            for (int boneIndex = 0; boneIndex < anim.bone.Count; boneIndex++)
            {
                RenderBase.OSkeletalAnimationBone animBone = anim.bone[boneIndex];

                if (string.IsNullOrWhiteSpace(animBone.name) || !skeletonByName.ContainsKey(animBone.name))
                {
                    logDiagnostic("Skipping animation bone '" + (animBone.name ?? "") + "' at index " + boneIndex + " (not in skeleton).");
                    continue;
                }

                string segmentKind = getSegmentKind(animBone);

                logDiagnostic(
                    "Bone '" + animBone.name
                    + "' segment=" + segmentKind
                    + " axisAngle=" + animBone.isAxisAngle
                    + " keyframes={"
                    + getKeyframeSummary(animBone)
                    + "}");

                if (!hasAnyAnimationData(animBone))
                {
                    logDiagnostic("Skipping bone '" + animBone.name + "' (no animation data, segment=" + segmentKind + ").");
                    continue;
                }

                List<float> sampleFrames = collectSampleFrames(anim, animBone);

                if (sampleFrames.Count == 0)
                {
                    logDiagnostic("Skipping bone '" + animBone.name + "' (no sample frames, segment=" + segmentKind + ").");
                    continue;
                }

                RenderBase.OBone skeletonBone = null;
                if (skeletonByName.ContainsKey(animBone.name)) skeletonBone = skeletonByName[animBone.name];

                float defaultRotationX = skeletonBone != null ? skeletonBone.rotation.x : 0f;
                float defaultRotationY = skeletonBone != null ? skeletonBone.rotation.y : 0f;
                float defaultRotationZ = skeletonBone != null ? skeletonBone.rotation.z : 0f;
                float defaultTranslationX = skeletonBone != null ? skeletonBone.translation.x : 0f;
                float defaultTranslationY = skeletonBone != null ? skeletonBone.translation.y : 0f;
                float defaultTranslationZ = skeletonBone != null ? skeletonBone.translation.z : 0f;

                string baseName = "anim_" + animBone.name + "_transform";

                List<float> outputMatrices = new List<float>(sampleFrames.Count * 16);
                for (int sampleIndex = 0; sampleIndex < sampleFrames.Count; sampleIndex++)
                {
                    float frame = sampleFrames[sampleIndex];
                    RenderBase.OMatrix localTransform;

                    if (animBone.isFullBakedFormat && animBone.transform.Count > 0)
                    {
                        int idx = ((int)frame) % animBone.transform.Count;
                        if (idx < 0) idx += animBone.transform.Count;
                        localTransform = animBone.transform[idx];
                    }
                    else if (animBone.isFrameFormat)
                    {
                        RenderBase.OVector4 scale = sampleFrameVector(
                            animBone.scale,
                            frame,
                            new RenderBase.OVector4(1f, 1f, 1f, 0f));

                        RenderBase.OVector4 translation = sampleFrameVector(
                            animBone.translation,
                            frame,
                            new RenderBase.OVector4(defaultTranslationX, defaultTranslationY, defaultTranslationZ, 0f));

                        if (animBone.rotationQuaternion.exists)
                        {
                            RenderBase.OVector4 rotationQuaternion = sampleFrameVector(
                                animBone.rotationQuaternion,
                                frame,
                                new RenderBase.OVector4(0f, 0f, 0f, 1f));

                            localTransform = buildLocalMatrixFromQuaternion(
                                scale.x,
                                scale.y,
                                scale.z,
                                rotationQuaternion.x,
                                rotationQuaternion.y,
                                rotationQuaternion.z,
                                rotationQuaternion.w,
                                translation.x,
                                translation.y,
                                translation.z);
                        }
                        else
                        {
                            localTransform = buildLocalMatrix(
                                scale.x,
                                scale.y,
                                scale.z,
                                defaultRotationX,
                                defaultRotationY,
                                defaultRotationZ,
                                translation.x,
                                translation.y,
                                translation.z,
                                false);
                        }
                    }
                    else
                    {
                        float scaleX = sampleKeyframes(animBone.scaleX, frame, 1f);
                        float scaleY = sampleKeyframes(animBone.scaleY, frame, 1f);
                        float scaleZ = sampleKeyframes(animBone.scaleZ, frame, 1f);

                        float rotationX = sampleKeyframes(animBone.rotationX, frame, defaultRotationX);
                        float rotationY = sampleKeyframes(animBone.rotationY, frame, defaultRotationY);
                        float rotationZ = sampleKeyframes(animBone.rotationZ, frame, defaultRotationZ);

                        float translationX = sampleKeyframes(animBone.translationX, frame, defaultTranslationX);
                        float translationY = sampleKeyframes(animBone.translationY, frame, defaultTranslationY);
                        float translationZ = sampleKeyframes(animBone.translationZ, frame, defaultTranslationZ);

                        localTransform = buildLocalMatrix(
                            scaleX,
                            scaleY,
                            scaleZ,
                            rotationX,
                            rotationY,
                            rotationZ,
                            translationX,
                            translationY,
                            translationZ,
                            animBone.isAxisAngle);
                    }

                    appendMatrix(outputMatrices, localTransform);
                }

                List<float> sampleTimes = new List<float>(sampleFrames.Count);
                for (int i = 0; i < sampleFrames.Count; i++)
                {
                    sampleTimes.Add(frameToSeconds(sampleFrames[i]));
                }

                addTransformAnimation(dae, baseName, animBone.name, sampleTimes, outputMatrices);
                logDiagnostic("Exported matrix animation for bone '" + animBone.name + "' with " + sampleFrames.Count + " samples.");
            }
        }

        private static void addTransformAnimation(
            COLLADA dae,
            string baseName,
            string boneName,
            List<float> sampleTimes,
            List<float> outputMatrices)
        {
            daeSource input = new daeSource();
            input.id = baseName + "_input";
            input.float_array = new daeFloatArray();
            input.float_array.id = input.id + "_array";
            input.float_array.set(sampleTimes);
            input.technique_common.accessor.source = "#" + input.float_array.id;
            input.technique_common.accessor.count = (uint)sampleTimes.Count;
            input.technique_common.accessor.stride = 1;
            input.technique_common.accessor.addParam("TIME", "float");

            daeSource output = new daeSource();
            output.id = baseName + "_output";
            output.float_array = new daeFloatArray();
            output.float_array.id = output.id + "_array";
            output.float_array.set(outputMatrices);
            output.technique_common.accessor.source = "#" + output.float_array.id;
            output.technique_common.accessor.count = (uint)sampleTimes.Count;
            output.technique_common.accessor.stride = 16;
            output.technique_common.accessor.addParam("TRANSFORM", "float4x4");

            daeSource interpolation = new daeSource();
            interpolation.id = baseName + "_interpolation";
            interpolation.Name_array = new daeNameArray();
            interpolation.Name_array.id = interpolation.id + "_array";
            List<string> interpolationData = new List<string>();
            for (int i = 0; i < sampleTimes.Count; i++) interpolationData.Add("LINEAR");
            interpolation.Name_array.set(interpolationData);
            interpolation.technique_common.accessor.source = "#" + interpolation.Name_array.id;
            interpolation.technique_common.accessor.count = (uint)sampleTimes.Count;
            interpolation.technique_common.accessor.stride = 1;
            interpolation.technique_common.accessor.addParam("INTERPOLATION", "Name");

            daeAnimation animation = new daeAnimation();
            animation.id = baseName;
            animation.name = baseName;
            animation.source.Add(input);
            animation.source.Add(output);
            animation.source.Add(interpolation);
            animation.sampler.id = baseName + "_sampler";
            animation.sampler.addInput("INPUT", "#" + input.id);
            animation.sampler.addInput("OUTPUT", "#" + output.id);
            animation.sampler.addInput("INTERPOLATION", "#" + interpolation.id);
            animation.channel.source = "#" + animation.sampler.id;
            animation.channel.target = boneName + "_bone_id/transform";

            dae.library_animations.Add(animation);
        }

        private static string getSegmentKind(RenderBase.OSkeletalAnimationBone bone)
        {
            if (bone.isFullBakedFormat) return "transformMatrix";
            if (bone.isFrameFormat)
            {
                return bone.rotationQuaternion.exists ? "transformQuaternion" : "transformFrame";
            }

            return bone.isAxisAngle ? "transformAxisAngle" : "transformEuler";
        }

        private static string getKeyframeSummary(RenderBase.OSkeletalAnimationBone bone)
        {
            return "rx=" + getKeyCount(bone.rotationX)
                + ",ry=" + getKeyCount(bone.rotationY)
                + ",rz=" + getKeyCount(bone.rotationZ)
                + ",tx=" + getKeyCount(bone.translationX)
                + ",ty=" + getKeyCount(bone.translationY)
                + ",tz=" + getKeyCount(bone.translationZ)
                + ",sx=" + getKeyCount(bone.scaleX)
                + ",sy=" + getKeyCount(bone.scaleY)
                + ",sz=" + getKeyCount(bone.scaleZ)
                + ",quat=" + getFrameVectorCount(bone.rotationQuaternion)
                + ",frameT=" + getFrameVectorCount(bone.translation)
                + ",frameS=" + getFrameVectorCount(bone.scale)
                + ",matrix=" + bone.transform.Count;
        }

        private static int getKeyCount(RenderBase.OAnimationKeyFrameGroup group)
        {
            return group.exists ? group.keyFrames.Count : 0;
        }

        private static int getFrameVectorCount(RenderBase.OAnimationFrame frame)
        {
            return frame.exists ? frame.vector.Count : 0;
        }

        private static bool hasAnyAnimationData(RenderBase.OSkeletalAnimationBone bone)
        {
            return bone.isFullBakedFormat
                || bone.isFrameFormat
                || hasKeyFrames(bone.scaleX)
                || hasKeyFrames(bone.scaleY)
                || hasKeyFrames(bone.scaleZ)
                || hasKeyFrames(bone.rotationX)
                || hasKeyFrames(bone.rotationY)
                || hasKeyFrames(bone.rotationZ)
                || hasKeyFrames(bone.translationX)
                || hasKeyFrames(bone.translationY)
                || hasKeyFrames(bone.translationZ);
        }

        private static bool hasKeyFrames(RenderBase.OAnimationKeyFrameGroup group)
        {
            return group.exists && group.keyFrames.Count > 0;
        }

        private static List<float> collectSampleFrames(RenderBase.OSkeletalAnimation animation, RenderBase.OSkeletalAnimationBone bone)
        {
            List<float> frames = new List<float>();

            addGroupFrames(frames, bone.scaleX);
            addGroupFrames(frames, bone.scaleY);
            addGroupFrames(frames, bone.scaleZ);
            addGroupFrames(frames, bone.rotationX);
            addGroupFrames(frames, bone.rotationY);
            addGroupFrames(frames, bone.rotationZ);
            addGroupFrames(frames, bone.translationX);
            addGroupFrames(frames, bone.translationY);
            addGroupFrames(frames, bone.translationZ);

            if (bone.isFrameFormat)
            {
                addFrameRange(frames, bone.scale, animation.frameSize);
                addFrameRange(frames, bone.rotationQuaternion, animation.frameSize);
                addFrameRange(frames, bone.translation, animation.frameSize);
            }

            if (bone.isFullBakedFormat && bone.transform.Count > 0)
            {
                for (int i = 0; i < bone.transform.Count; i++) frames.Add(i);
            }

            if (frames.Count == 0) return frames;

            frames.Sort();
            List<float> uniqueFrames = new List<float>();
            float last = frames[0];
            uniqueFrames.Add(last);

            for (int i = 1; i < frames.Count; i++)
            {
                if (Math.Abs(frames[i] - last) < 0.0001f) continue;
                uniqueFrames.Add(frames[i]);
                last = frames[i];
            }

            if (bone.isFullBakedFormat)
            {
                return uniqueFrames;
            }

            int denseStart = Math.Max(0, (int)Math.Floor(uniqueFrames[0]));
            int denseEnd = Math.Max(
                denseStart,
                (int)Math.Ceiling(Math.Max(animation.frameSize, uniqueFrames[uniqueFrames.Count - 1])));

            List<float> denseFrames = new List<float>(Math.Max(uniqueFrames.Count, denseEnd - denseStart + 1));
            for (int frame = denseStart; frame <= denseEnd; frame++)
            {
                denseFrames.Add(frame);
            }

            for (int i = 0; i < uniqueFrames.Count; i++)
            {
                float keyFrame = uniqueFrames[i];
                bool alreadyExists = false;
                for (int j = 0; j < denseFrames.Count; j++)
                {
                    if (Math.Abs(denseFrames[j] - keyFrame) < 0.0001f)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                {
                    denseFrames.Add(keyFrame);
                }
            }

            denseFrames.Sort();
            return denseFrames;
        }

        private static float frameToSeconds(float frame)
        {
            return frame / AnimationFramesPerSecond;
        }

        private static void addGroupFrames(List<float> frames, RenderBase.OAnimationKeyFrameGroup group)
        {
            if (!group.exists || group.keyFrames.Count == 0) return;
            for (int i = 0; i < group.keyFrames.Count; i++) frames.Add(group.keyFrames[i].frame);
        }

        private static void addFrameRange(List<float> frames, RenderBase.OAnimationFrame frame, float animationFrameSize)
        {
            if (!frame.exists || frame.vector.Count == 0) return;

            if (frame.vector.Count == 1)
            {
                frames.Add(frame.startFrame);
                return;
            }

            float start = frame.startFrame;
            float end = frame.endFrame;
            if (end <= start) end = Math.Max(start + frame.vector.Count - 1, animationFrameSize);

            float step = (end - start) / (frame.vector.Count - 1);
            for (int i = 0; i < frame.vector.Count; i++)
            {
                frames.Add(start + (step * i));
            }
        }

        private static float sampleKeyframes(RenderBase.OAnimationKeyFrameGroup group, float frame, float defaultValue)
        {
            if (!group.exists || group.keyFrames.Count == 0) return defaultValue;
            if (group.keyFrames.Count == 1) return group.keyFrames[0].value;

            RenderBase.OAnimationKeyFrame first = group.keyFrames[0];
            RenderBase.OAnimationKeyFrame last = group.keyFrames[group.keyFrames.Count - 1];

            if (frame <= first.frame) return first.value;
            if (frame >= last.frame) return last.value;

            for (int i = 0; i < group.keyFrames.Count - 1; i++)
            {
                RenderBase.OAnimationKeyFrame left = group.keyFrames[i];
                RenderBase.OAnimationKeyFrame right = group.keyFrames[i + 1];
                if (frame > right.frame) continue;

                float delta = right.frame - left.frame;
                if (Math.Abs(delta) < 0.0001f) return right.value;

                float mu = (frame - left.frame) / delta;
                return left.value + ((right.value - left.value) * mu);
            }

            return last.value;
        }

        private static RenderBase.OVector4 sampleFrameVector(RenderBase.OAnimationFrame frameData, float frame, RenderBase.OVector4 defaultValue)
        {
            if (!frameData.exists || frameData.vector.Count == 0) return defaultValue;
            if (frameData.vector.Count == 1) return frameData.vector[0];

            float mappedFrame = frame;
            if (frameData.endFrame > frameData.startFrame)
            {
                float normalized = (frame - frameData.startFrame) / (frameData.endFrame - frameData.startFrame);
                mappedFrame = normalized * (frameData.vector.Count - 1);
            }

            mappedFrame = Math.Clamp(mappedFrame, 0f, frameData.vector.Count - 1);
            int left = (int)Math.Floor(mappedFrame);
            int right = Math.Min(left + 1, frameData.vector.Count - 1);
            float mu = mappedFrame - left;

            RenderBase.OVector4 leftValue = frameData.vector[left];
            RenderBase.OVector4 rightValue = frameData.vector[right];

            return new RenderBase.OVector4(
                leftValue.x + ((rightValue.x - leftValue.x) * mu),
                leftValue.y + ((rightValue.y - leftValue.y) * mu),
                leftValue.z + ((rightValue.z - leftValue.z) * mu),
                leftValue.w + ((rightValue.w - leftValue.w) * mu));
        }

        private static RenderBase.OMatrix buildLocalMatrix(
            float sx,
            float sy,
            float sz,
            float rx,
            float ry,
            float rz,
            float tx,
            float ty,
            float tz,
            bool isAxisAngle)
        {
            RenderBase.OMatrix output = new RenderBase.OMatrix();
            output *= RenderBase.OMatrix.scale(new RenderBase.OVector3(sx, sy, sz));

            if (isAxisAngle)
            {
                output *= buildAxisAngleMatrix(rx, ry, rz);
            }
            else
            {
                output *= RenderBase.OMatrix.rotateZ(rz);
                output *= RenderBase.OMatrix.rotateY(ry);
                output *= RenderBase.OMatrix.rotateX(rx);
            }

            output *= RenderBase.OMatrix.translate(new RenderBase.OVector3(tx, ty, tz));
            return output;
        }

        private static RenderBase.OMatrix buildLocalMatrixFromQuaternion(
            float sx,
            float sy,
            float sz,
            float qx,
            float qy,
            float qz,
            float qw,
            float tx,
            float ty,
            float tz)
        {
            RenderBase.OMatrix output = new RenderBase.OMatrix();
            output *= RenderBase.OMatrix.scale(new RenderBase.OVector3(sx, sy, sz));
            output *= buildQuaternionMatrix(qx, qy, qz, qw);
            output *= RenderBase.OMatrix.translate(new RenderBase.OVector3(tx, ty, tz));
            return output;
        }

        private static RenderBase.OMatrix buildAxisAngleMatrix(float x, float y, float z)
        {
            float angle = (float)Math.Sqrt((x * x) + (y * y) + (z * z));
            if (angle <= 0.000001f) return new RenderBase.OMatrix();

            System.Numerics.Vector3 axis = new System.Numerics.Vector3(x / angle, y / angle, z / angle);
            System.Numerics.Quaternion q = System.Numerics.Quaternion.CreateFromAxisAngle(axis, angle);
            return buildQuaternionMatrix(q.X, q.Y, q.Z, q.W);
        }

        private static RenderBase.OMatrix buildQuaternionMatrix(float x, float y, float z, float w)
        {
            System.Numerics.Quaternion q = new System.Numerics.Quaternion(x, y, z, w);
            if (q.LengthSquared() <= 0.0000001f) return new RenderBase.OMatrix();

            q = System.Numerics.Quaternion.Normalize(q);
            System.Numerics.Matrix4x4 matrix = System.Numerics.Matrix4x4.CreateFromQuaternion(q);
            return toOMatrix(matrix);
        }

        private static RenderBase.OMatrix toOMatrix(System.Numerics.Matrix4x4 matrix)
        {
            RenderBase.OMatrix output = new RenderBase.OMatrix();

            output.M11 = matrix.M11;
            output.M12 = matrix.M12;
            output.M13 = matrix.M13;
            output.M14 = matrix.M14;

            output.M21 = matrix.M21;
            output.M22 = matrix.M22;
            output.M23 = matrix.M23;
            output.M24 = matrix.M24;

            output.M31 = matrix.M31;
            output.M32 = matrix.M32;
            output.M33 = matrix.M33;
            output.M34 = matrix.M34;

            output.M41 = matrix.M41;
            output.M42 = matrix.M42;
            output.M43 = matrix.M43;
            output.M44 = matrix.M44;

            return output;
        }

        private static void appendMatrix(List<float> destination, RenderBase.OMatrix matrix)
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    destination.Add(matrix[j, i]);
                }
            }
        }

        private static RenderBase.OVector2 applyTextureCoordinator(RenderBase.OVector2 uv, RenderBase.OTextureCoordinator coordinator)
        {
            float scaleU = coordinator.scaleU;
            float scaleV = coordinator.scaleV;
            if (scaleU == 0f && scaleV == 0f)
            {
                scaleU = 1f;
                scaleV = 1f;
            }

            float centeredU = uv.x - 0.5f;
            float centeredV = uv.y - 0.5f;

            float cos = (float)Math.Cos(coordinator.rotate);
            float sin = (float)Math.Sin(coordinator.rotate);

            float rotatedU = (centeredU * cos) - (centeredV * sin);
            float rotatedV = (centeredU * sin) + (centeredV * cos);

            float transformedU = scaleU * (rotatedU + 0.5f - coordinator.translateU);
            float transformedV = scaleV * (rotatedV + 0.5f - coordinator.translateV);

            return new RenderBase.OVector2(transformedU, transformedV);
        }

        private static RenderBase.OMaterial getMeshMaterial(RenderBase.OModel model, RenderBase.OMesh mesh)
        {
            if (model.material.Count == 0) return new RenderBase.OMaterial();

            int materialIndex = mesh.materialId;
            if (materialIndex < 0 || materialIndex >= model.material.Count)
            {
                return model.material[0];
            }

            return model.material[materialIndex];
        }

        private static void addTexcoordBinding(daeBindMaterialInstace instanceMaterial, int texUvCount)
        {
            if (texUvCount <= 0)
            {
                return;
            }

            daeBindVertexInput binding = new daeBindVertexInput();
            binding.semantic = "uv";
            binding.input_semantic = "TEXCOORD";
            binding.input_set = 0;
            instanceMaterial.bind_vertex_input.Add(binding);
        }

        private static void logDiagnostic(string message)
        {
            if (!DiagnosticLogging) return;
            Console.Error.WriteLine("[DAE] " + message);
        }

        /// <summary>
        ///     Transforms a Skeleton from relative to absolute positions.
        /// </summary>
        /// <param name="skeleton">The skeleton</param>
        /// <param name="index">Index of the bone to convert</param>
        /// <param name="target">Target matrix to save bone transformation</param>
        private static void transformSkeleton(List<RenderBase.OBone> skeleton, int index, ref RenderBase.OMatrix target)
        {
            target *= RenderBase.OMatrix.rotateX(skeleton[index].rotation.x);
            target *= RenderBase.OMatrix.rotateY(skeleton[index].rotation.y);
            target *= RenderBase.OMatrix.rotateZ(skeleton[index].rotation.z);
            target *= RenderBase.OMatrix.translate(skeleton[index].translation);
            if (skeleton[index].parentId > -1) transformSkeleton(skeleton, skeleton[index].parentId, ref target);
        }

        /// <summary>
        ///     Writes the skeleton hierarchy to the DAE.
        /// </summary>
        /// <param name="skeleton">The skeleton</param>
        /// <param name="index">Index of the current bone (root bone when it's not a recursive call)</param>
        /// <param name="nodes">List with the DAE nodes</param>
        private static void writeSkeleton(List<RenderBase.OBone> skeleton, int index, ref List<daeNode> nodes)
        {
            daeNode node = new daeNode();
            node.name = skeleton[index].name;
            node.id = node.name + "_bone_id";
            node.sid = node.name;
            node.type = "JOINT";
            node.matrix.sid = "transform";

            RenderBase.OMatrix transform = new RenderBase.OMatrix();
            transform *= RenderBase.OMatrix.rotateX(skeleton[index].rotation.x);
            transform *= RenderBase.OMatrix.rotateY(skeleton[index].rotation.y);
            transform *= RenderBase.OMatrix.rotateZ(skeleton[index].rotation.z);
            transform *= RenderBase.OMatrix.translate(skeleton[index].translation);

            node.matrix.set(transform);

            for (int i = 0; i < skeleton.Count; i++)
            {
                if (skeleton[i].parentId == index)
                {
                    if (node.childs == null) node.childs = new List<daeNode>();
                    writeSkeleton(skeleton, i, ref node.childs);
                }
            }

            nodes.Add(node);
        }
    }
}
