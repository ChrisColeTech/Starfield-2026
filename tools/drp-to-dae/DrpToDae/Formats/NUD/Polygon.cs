using System;
using System.Collections.Generic;
using System.Numerics;

namespace DrpToDae.Formats.NUD
{
    public class Polygon
    {
        public enum BoneTypes
        {
            NoBones = 0x00,
            Float = 0x10,
            HalfFloat = 0x20,
            Byte = 0x40
        }

        public enum VertexTypes
        {
            NoNormals = 0x0,
            NormalsFloat = 0x1,
            NormalsTanBiTanFloat = 0x3,
            NormalsHalfFloat = 0x6,
            NormalsTanBiTanHalfFloat = 0x7
        }

        public enum UVTypes
        {
            HalfFloat = 0,
            Float = 1
        }

        public enum VertexColorTypes
        {
            None = 0,
            Byte = 2,
            HalfFloat = 4
        }

        public enum PrimitiveTypes
        {
            TriangleStrip = 0x0,
            Triangles = 0x40
        }

        public List<Vertex> vertices = new List<Vertex>();
        public List<int> vertexIndices = new List<int>();
        public int displayFaceSize = 0;

        public List<Material> materials = new List<Material>();

        public int boneType = (int)BoneTypes.Byte;
        public int normalType = (int)VertexTypes.NormalsHalfFloat;
        public int vertSize
        {
            get { return (boneType & 0xF0) | (normalType & 0xF); }
            set { boneType = value & 0xF0; normalType = value & 0xF; }
        }

        public int uvCount = 1;
        public int colorType = (int)VertexColorTypes.Byte;
        public int uvType = (int)UVTypes.HalfFloat;
        public int UVSize
        {
            get { return (uvCount << 4) | (colorType & 0xE) | uvType; }
            set { uvCount = value >> 4; colorType = value & 0xE; uvType = value & 1; }
        }

        public int strip = (int)PrimitiveTypes.Triangles;
        public int polflag
        {
            get { return boneType > 0 ? 4 : 0; }
            set
            {
                if (value == 0 && boneType == 0) { }
                else if (value == 4 && boneType != 0) { }
                else throw new NotImplementedException("Poly flag not supported " + value);
            }
        }

        public bool IsTransparent => (materials.Count > 0 && materials[0].SrcFactor > 0) || (materials.Count > 0 && materials[0].DstFactor > 0);

        public Polygon()
        {
        }

        public void AddVertex(Vertex v)
        {
            vertices.Add(v);
        }

        public void AddDefaultMaterial()
        {
            Material mat = Material.GetDefault();
            materials.Add(mat);
            mat.textures.Add(new MatTexture(0x10000000));
            mat.textures.Add(MatTexture.GetDefault());
        }

        public List<int> GetTriangles()
        {
            if (strip == (int)PrimitiveTypes.Triangles)
            {
                return vertexIndices;
            }
            else if (strip == (int)PrimitiveTypes.TriangleStrip)
            {
                List<int> vertexIndices = new List<int>();

                int startDirection = 1;
                int p = 0;
                int f1 = this.vertexIndices[p++];
                int f2 = this.vertexIndices[p++];
                int faceDirection = startDirection;
                int f3;
                do
                {
                    f3 = this.vertexIndices[p++];
                    if (f3 == 0xFFFF)
                    {
                        f1 = this.vertexIndices[p++];
                        f2 = this.vertexIndices[p++];
                        faceDirection = startDirection;
                    }
                    else
                    {
                        faceDirection *= -1;
                        if ((f1 != f2) && (f2 != f3) && (f3 != f1))
                        {
                            if (faceDirection > 0)
                            {
                                vertexIndices.Add(f3);
                                vertexIndices.Add(f2);
                                vertexIndices.Add(f1);
                            }
                            else
                            {
                                vertexIndices.Add(f2);
                                vertexIndices.Add(f3);
                                vertexIndices.Add(f1);
                            }
                        }
                        f1 = f2;
                        f2 = f3;
                    }
                } while (p < this.vertexIndices.Count);

                return vertexIndices;
            }
            else
            {
                throw new NotImplementedException("Face type not supported: " + strip);
            }
        }

        public List<int> GetTriangleStrip()
        {
            if (strip == (int)PrimitiveTypes.TriangleStrip)
            {
                return vertexIndices;
            }
            else if (strip == (int)PrimitiveTypes.Triangles)
            {
                List<int> vertexIndices = new List<int>();

                for (int p = 0; p < this.vertexIndices.Count; p += 3)
                {
                    if (p > 0)
                        vertexIndices.Add(0xFFFF);
                    vertexIndices.Add(this.vertexIndices[p]);
                    vertexIndices.Add(this.vertexIndices[p + 1]);
                    vertexIndices.Add(this.vertexIndices[p + 2]);
                }
                return vertexIndices;
            }
            else
            {
                throw new NotImplementedException("Face type not supported: " + strip);
            }
        }
    }
}
