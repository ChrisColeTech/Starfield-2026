using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniToolbox.Garc.Models.GenericFormats
{
    public class OBJ
    {
        /// <summary>
        ///     Exports a model to the Wavefront OBJ format.
        /// </summary>
        /// <param name="model">The model to be exported</param>
        /// <param name="fileName">The output file name</param>
        /// <param name="modelIndex">The index of the model that should be exported</param>
        public static void export(RenderBase.OModelGroup model, string fileName, int modelIndex)
        {
            StringBuilder output = new StringBuilder();

            RenderBase.OModel mdl = model.model[modelIndex];

            int faceIndexBase = 1;
            for (int objIndex = 0; objIndex < mdl.mesh.Count; objIndex++)
            {
                output.AppendLine("g " + mdl.mesh[objIndex].name);
                output.AppendLine(null);

                RenderBase.OMaterial meshMaterial = getMeshMaterial(mdl, mdl.mesh[objIndex]);

                output.AppendLine("usemtl " + meshMaterial.name0 + ".png");
                output.AppendLine(null);

                MeshUtils.optimizedMesh obj = MeshUtils.optimizeMesh(mdl.mesh[objIndex]);
                foreach (RenderBase.OVertex vertex in obj.vertices)
                {
                    RenderBase.OVector2 transformedUv0 = applyTextureCoordinator(vertex.texture0, meshMaterial.textureCoordinator[0]);

                    output.AppendLine("v " + getString(vertex.position.x) + " " + getString(vertex.position.y) + " " + getString(vertex.position.z));
                    output.AppendLine("vn " + getString(vertex.normal.x) + " " + getString(vertex.normal.y) + " " + getString(vertex.normal.z));
                    output.AppendLine("vt " + getString(transformedUv0.x) + " " + getString(transformedUv0.y));
                }
                output.AppendLine(null);

                for (int i = 0; i < obj.indices.Count; i += 3)
                {
                    output.AppendLine(
                        string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}",
                        faceIndexBase + obj.indices[i],
                        faceIndexBase + obj.indices[i + 1],
                        faceIndexBase + obj.indices[i + 2]));
                }
                faceIndexBase += obj.vertices.Count;
                output.AppendLine(null);
            }

            File.WriteAllText(fileName, output.ToString());
        }

        /// <summary>
        ///     Transforms a Float into a String that will always have "." into decimal places,
        ///     even if the region uses ",".
        /// </summary>
        /// <param name="value">The Float value</param>
        /// <returns></returns>
        private static string getString(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
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

        /// <summary>
        ///     Imports a Wavefront OBJ model from file.
        /// </summary>
        /// <param name="fileName">The complete file name</param>
        /// <returns></returns>
        public static RenderBase.OModelGroup import(string fileName)
        {
            RenderBase.OModelGroup output = new RenderBase.OModelGroup();
            RenderBase.OModel model = new RenderBase.OModel();

            string obj = File.ReadAllText(fileName);

            List<RenderBase.OVector3> vertices = new List<RenderBase.OVector3>();
            List<RenderBase.OVector3> normals = new List<RenderBase.OVector3>();
            List<RenderBase.OVector2> uvs = new List<RenderBase.OVector2>();
            List<RenderBase.OVertex> currVertices = new List<RenderBase.OVertex>();

            string name = string.Empty, oldName;
            string[] lines = obj.Split((char)0xa);
            foreach (string l in lines)
            {
                string line = l.Trim();
                string[] lineParams = Regex.Split(line, "\\s+");

                switch (lineParams[0])
                {
                    case "v":
                    case "vn":
                        RenderBase.OVector3 pvec = new RenderBase.OVector3();
                        pvec.x = float.Parse(lineParams[1], CultureInfo.InvariantCulture);
                        pvec.y = float.Parse(lineParams[2], CultureInfo.InvariantCulture);
                        pvec.z = float.Parse(lineParams[3], CultureInfo.InvariantCulture);
                        if (lineParams[0] == "v") vertices.Add(pvec); else normals.Add(pvec);
                        break;
                    case "vt":
                        RenderBase.OVector2 tvec = new RenderBase.OVector2();
                        tvec.x = float.Parse(lineParams[1], CultureInfo.InvariantCulture);
                        tvec.y = float.Parse(lineParams[2], CultureInfo.InvariantCulture);
                        uvs.Add(tvec);
                        break;
                    case "f":
                        string[][] vtx = new string[lineParams.Length - 1][];
                        for (int i = 0; i < lineParams.Length - 1; i++)
                        {
                            vtx[i] = lineParams[i + 1].Split('/');
                        }

                        for (int i = 0; i < lineParams.Length - 1; i++)
                        {
                            RenderBase.OVertex vertex = new RenderBase.OVertex();

                            vertex.position = vertices[int.Parse(vtx[i][0]) - 1];
                            if (vtx[i].Length > 1 && vtx[i][1] != string.Empty) vertex.texture0 = uvs[int.Parse(vtx[i][1]) - 1];
                            if (vtx[i].Length > 2) vertex.normal = normals[int.Parse(vtx[i][2]) - 1];
                            vertex.diffuseColor = 0xffffffff;

                            if (i > 2)
                            {
                                currVertices.Add(currVertices[currVertices.Count - 3]);
                                currVertices.Add(currVertices[currVertices.Count - 2]);
                                currVertices.Add(vertex);
                            }
                            else
                                currVertices.Add(vertex);
                        }
                        break;
                    case "g":
                        oldName = name;
                        if (lineParams.Length > 1)
                            name = lineParams[1];
                        else
                            name = "mesh";

                        if (currVertices.Count > 0)
                        {
                            RenderBase.OMesh mesh = new RenderBase.OMesh();
                            mesh.vertices = currVertices;
                            mesh.name = oldName;
                            mesh.hasNormal = true;
                            mesh.texUVCount = 1;
                            model.mesh.Add(mesh);

                            currVertices = new List<RenderBase.OVertex>();
                        }
                        break;
                }
            }

            if (currVertices.Count > 0)
            {
                RenderBase.OMesh mesh = new RenderBase.OMesh();
                mesh.vertices = currVertices;
                mesh.name = name;
                mesh.hasNormal = true;
                mesh.texUVCount = 1;
                model.mesh.Add(mesh);
            }

            model.material.Add(new RenderBase.OMaterial());
            output.model.Add(model);
            return output;
        }
    }
}
