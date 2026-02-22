using System;
using System.Collections.Generic;
using DrpToDae.IO;
using System.IO;

namespace DrpToDae.Formats.NUD
{
    public class NUD
    {
        public Endianness Endian { get; set; }
        public ushort version = 0x0200;

        public short boneIndexStart = 0;
        public short boneIndexEnd = 0;
        public bool hasBones = false;
        public float[] boundingSphere = new float[4];

        public List<Mesh> Meshes { get; set; } = new List<Mesh>();

        public NUD()
        {
        }

        public NUD(string filename) : this()
        {
            Read(filename);
        }

        private struct ObjectData
        {
            public float sortBias;
            public string name;
            public ushort boneflag;
            public short singlebind;
            public int polyCount;
            public int positionb;
        }

        public struct PolyData
        {
            public int polyStart;
            public int vertStart;
            public int verAddStart;
            public int vertCount;
            public int vertSize;
            public int UVSize;
            public ushort faceCount;
            public ushort polyFlag;
            public int texprop1;
            public int texprop2;
            public int texprop3;
            public int texprop4;
        }

        public void Read(string filename)
        {
            FileData fileData = new FileData(filename);
            fileData.endian = Endianness.Big;
            fileData.Seek(0);

            string magic = fileData.ReadString(0, 4);
            fileData.Seek(4);
            if (magic.Equals("NDP3"))
                Endian = Endianness.Big;
            else if (magic.Equals("NDWD"))
                Endian = Endianness.Little;

            fileData.endian = Endian;
            fileData.ReadUInt();

            fileData.endian = Endianness.Big;
            version = fileData.ReadUShort();
            fileData.endian = Endian;

            int polysets = fileData.ReadUShort();
            boneIndexStart = fileData.ReadShort();
            boneIndexEnd = fileData.ReadShort();

            int polyClumpStart = fileData.ReadInt() + 0x30;
            int polyClumpSize = fileData.ReadInt();
            int vertClumpStart = polyClumpStart + polyClumpSize;
            int vertClumpSize = fileData.ReadInt();
            int vertaddClumpStart = vertClumpStart + vertClumpSize;
            int vertaddClumpSize = fileData.ReadInt();
            int nameStart = vertaddClumpStart + vertaddClumpSize;
            boundingSphere[0] = fileData.ReadFloat();
            boundingSphere[1] = fileData.ReadFloat();
            boundingSphere[2] = fileData.ReadFloat();
            boundingSphere[3] = fileData.ReadFloat();

            ObjectData[] obj = new ObjectData[polysets];
            List<float[]> boundingSpheres = new List<float[]>();
            for (int i = 0; i < polysets; i++)
            {
                float[] boundingSphere = new float[4];
                boundingSphere[0] = fileData.ReadFloat();
                boundingSphere[1] = fileData.ReadFloat();
                boundingSphere[2] = fileData.ReadFloat();
                boundingSphere[3] = fileData.ReadFloat();
                boundingSpheres.Add(boundingSphere);

                fileData.ReadFloat();
                fileData.ReadFloat();
                fileData.ReadFloat();

                obj[i].sortBias = fileData.ReadFloat();

                int temp = fileData.Pos() + 4;
                fileData.Seek(nameStart + fileData.ReadInt());
                obj[i].name = (fileData.ReadString());
                fileData.Seek(temp);

                fileData.ReadUShort();
                obj[i].boneflag = fileData.ReadUShort();
                obj[i].singlebind = fileData.ReadShort();
                obj[i].polyCount = fileData.ReadUShort();
                obj[i].positionb = fileData.ReadInt();
            }

            int meshIndex = 0;
            foreach (var o in obj)
            {
                Mesh m = new Mesh();
                m.Name = o.name;
                Meshes.Add(m);
                m.boundingSphere = boundingSpheres[meshIndex++];
                m.sortBias = o.sortBias;
                m.boneflag = o.boneflag;
                m.singlebind = o.singlebind;

                for (int i = 0; i < o.polyCount; i++)
                {
                    PolyData polyData = new PolyData();

                    polyData.polyStart = fileData.ReadInt() + polyClumpStart;
                    polyData.vertStart = fileData.ReadInt() + vertClumpStart;
                    polyData.verAddStart = fileData.ReadInt() + vertaddClumpStart;
                    polyData.vertCount = fileData.ReadUShort();
                    polyData.vertSize = fileData.ReadByte();
                    polyData.UVSize = fileData.ReadByte();
                    polyData.texprop1 = fileData.ReadInt();
                    polyData.texprop2 = fileData.ReadInt();
                    polyData.texprop3 = fileData.ReadInt();
                    polyData.texprop4 = fileData.ReadInt();
                    polyData.faceCount = fileData.ReadUShort();
                    polyData.polyFlag = fileData.ReadUShort();
                    fileData.Skip(0xC);

                    int temp = fileData.Pos();

                    Polygon poly = ReadVertex(fileData, polyData, o);
                    m.Polygons.Add(poly);

                    poly.materials = ReadMaterials(fileData, polyData, nameStart);

                    fileData.Seek(temp);
                }
            }
        }

        public static List<Material> ReadMaterials(FileData d, PolyData p, int nameOffset)
        {
            int propoff = p.texprop1;
            List<Material> mats = new List<Material>();

            while (propoff != 0)
            {
                d.Seek(propoff);

                Material m = new Material();
                mats.Add(m);

                m.Flags = d.ReadUInt();
                d.Skip(4);
                m.SrcFactor = d.ReadUShort();
                ushort texCount = d.ReadUShort();
                m.DstFactor = d.ReadUShort();
                m.AlphaFunc = d.ReadUShort();
                m.RefAlpha = d.ReadUShort();
                m.CullMode = d.ReadUShort();
                d.Skip(4);
                m.Unk2 = d.ReadInt();
                m.ZBufferOffset = d.ReadInt();

                for (ushort i = 0; i < texCount; i++)
                {
                    MatTexture tex = new MatTexture();
                    tex.hash = d.ReadInt();
                    d.Skip(6);
                    tex.mapMode = d.ReadUShort();
                    tex.wrapModeS = d.ReadByte();
                    tex.wrapModeT = d.ReadByte();
                    tex.minFilter = d.ReadByte();
                    tex.magFilter = d.ReadByte();
                    tex.mipDetail = d.ReadByte();
                    tex.unknown = d.ReadByte();
                    d.Skip(4);
                    tex.unknown2 = d.ReadShort();
                    m.textures.Add(tex);
                }

                int matAttSize = d.ReadInt();
                if (matAttSize > 0)
                {
                    d.Seek(d.Pos() - 4);
                    do
                    {
                        int pos = d.Pos();

                        matAttSize = d.ReadInt();
                        int nameStart = d.ReadInt();
                        d.Skip(3);
                        byte valueCount = d.ReadByte();
                        d.Skip(4);

                        if (valueCount == 0)
                            goto Continue;

                        string name = d.ReadString(nameOffset + nameStart, -1);

                        float[] values = new float[4];
                        for (int i = 0; i < values.Length; i++)
                        {
                            if (i < valueCount)
                                values[i] = d.ReadFloat();
                            else
                                values[i] = 0;
                        }
                        m.UpdateProperty(name, values);

                    Continue:
                        if (matAttSize == 0)
                            d.Seek(pos + 0x10 + (valueCount * 4));
                        else
                            d.Seek(pos + matAttSize);
                    } while (matAttSize != 0);

                    if (propoff == p.texprop1)
                        propoff = p.texprop2;
                    else if (propoff == p.texprop2)
                        propoff = p.texprop3;
                    else if (propoff == p.texprop3)
                        propoff = p.texprop4;
                    else
                        propoff = 0;
                }
                else
                    propoff = 0;
            }

            return mats;
        }

        private static Polygon ReadVertex(FileData d, PolyData p, ObjectData o)
        {
            Polygon m = new Polygon();
            m.vertSize = p.vertSize;
            m.UVSize = p.UVSize;
            m.strip = (byte)(p.polyFlag >> 8);
            m.polflag = (byte)(p.polyFlag & 0xFF);

            Vertex[] vertices = new Vertex[p.vertCount];
            for (int x = 0; x < p.vertCount; x++)
                vertices[x] = new Vertex();

            d.Seek(p.vertStart);
            if (m.boneType > 0)
            {
                foreach (Vertex v in vertices)
                    ReadUV(d, m, v);
                d.Seek(p.verAddStart);
                foreach (Vertex v in vertices)
                    ReadVertexData(d, m, v);
            }
            else
            {
                foreach (Vertex v in vertices)
                {
                    ReadVertexData(d, m, v);
                    ReadUV(d, m, v);

                    v.boneIds.Add(o.singlebind);
                    v.boneWeights.Add(1);
                }
            }

            foreach (Vertex v in vertices)
                m.vertices.Add(v);

            d.Seek(p.polyStart);
            for (int x = 0; x < p.faceCount; x++)
            {
                m.vertexIndices.Add(d.ReadUShort());
            }

            return m;
        }

        private static void ReadUV(FileData d, Polygon poly, Vertex v)
        {
            int uvCount = poly.uvCount;
            int colorType = poly.colorType;
            int uvType = poly.uvType;

            if (colorType == (int)Polygon.VertexColorTypes.None)
            { }
            else if (colorType == (int)Polygon.VertexColorTypes.Byte)
                v.color = new System.Numerics.Vector4(d.ReadByte(), d.ReadByte(), d.ReadByte(), d.ReadByte());
            else if (colorType == (int)Polygon.VertexColorTypes.HalfFloat)
                v.color = new System.Numerics.Vector4(d.ReadHalfFloat() * 0xFF, d.ReadHalfFloat() * 0xFF, d.ReadHalfFloat() * 0xFF, d.ReadHalfFloat() * 0xFF);
            else
                throw new NotImplementedException($"Unsupported vertex color type: {colorType}");

            for (int i = 0; i < uvCount; i++)
            {
                if (uvType == (int)Polygon.UVTypes.HalfFloat)
                    v.uv.Add(new System.Numerics.Vector2(d.ReadHalfFloat(), d.ReadHalfFloat()));
                else if (uvType == (int)Polygon.UVTypes.Float)
                    v.uv.Add(new System.Numerics.Vector2(d.ReadFloat(), d.ReadFloat()));
                else
                    throw new NotImplementedException($"Unsupported UV type: {uvType}");
            }
        }

        private static void ReadVertexData(FileData d, Polygon poly, Vertex v)
        {
            int boneType = poly.boneType;
            int vertexType = poly.normalType;

            v.pos.X = d.ReadFloat();
            v.pos.Y = d.ReadFloat();
            v.pos.Z = d.ReadFloat();

            if (vertexType == (int)Polygon.VertexTypes.NoNormals)
            {
                d.ReadFloat();
            }
            else if (vertexType == (int)Polygon.VertexTypes.NormalsFloat)
            {
                d.ReadFloat();
                v.nrm.X = d.ReadFloat();
                v.nrm.Y = d.ReadFloat();
                v.nrm.Z = d.ReadFloat();
                d.ReadFloat();
            }
            else if (vertexType == 2)
            {
                d.ReadFloat();
                v.nrm.X = d.ReadFloat();
                v.nrm.Y = d.ReadFloat();
                v.nrm.Z = d.ReadFloat();
                d.ReadFloat();
                v.bitan.X = d.ReadFloat();
                v.bitan.Y = d.ReadFloat();
                v.bitan.Z = d.ReadFloat();
                v.bitan.W = d.ReadFloat();
                v.tan.X = d.ReadFloat();
                v.tan.Y = d.ReadFloat();
                v.tan.Z = d.ReadFloat();
                v.tan.W = d.ReadFloat();
            }
            else if (vertexType == (int)Polygon.VertexTypes.NormalsTanBiTanFloat)
            {
                d.ReadFloat();
                v.nrm.X = d.ReadFloat();
                v.nrm.Y = d.ReadFloat();
                v.nrm.Z = d.ReadFloat();
                d.ReadFloat();
                v.bitan.X = d.ReadFloat();
                v.bitan.Y = d.ReadFloat();
                v.bitan.Z = d.ReadFloat();
                v.bitan.W = d.ReadFloat();
                v.tan.X = d.ReadFloat();
                v.tan.Y = d.ReadFloat();
                v.tan.Z = d.ReadFloat();
                v.tan.W = d.ReadFloat();
            }
            else if (vertexType == (int)Polygon.VertexTypes.NormalsHalfFloat)
            {
                v.nrm.X = d.ReadHalfFloat();
                v.nrm.Y = d.ReadHalfFloat();
                v.nrm.Z = d.ReadHalfFloat();
                d.ReadHalfFloat();
            }
            else if (vertexType == (int)Polygon.VertexTypes.NormalsTanBiTanHalfFloat)
            {
                v.nrm.X = d.ReadHalfFloat();
                v.nrm.Y = d.ReadHalfFloat();
                v.nrm.Z = d.ReadHalfFloat();
                d.ReadHalfFloat();
                v.bitan.X = d.ReadHalfFloat();
                v.bitan.Y = d.ReadHalfFloat();
                v.bitan.Z = d.ReadHalfFloat();
                v.bitan.W = d.ReadHalfFloat();
                v.tan.X = d.ReadHalfFloat();
                v.tan.Y = d.ReadHalfFloat();
                v.tan.Z = d.ReadHalfFloat();
                v.tan.W = d.ReadHalfFloat();
            }
            else
            {
                throw new Exception($"Unsupported vertex type: {vertexType}");
            }

            if (boneType == (int)Polygon.BoneTypes.NoBones)
            {
            }
            else if (boneType == (int)Polygon.BoneTypes.Float)
            {
                v.boneIds.Add(d.ReadInt());
                v.boneIds.Add(d.ReadInt());
                v.boneIds.Add(d.ReadInt());
                v.boneIds.Add(d.ReadInt());
                v.boneWeights.Add(d.ReadFloat());
                v.boneWeights.Add(d.ReadFloat());
                v.boneWeights.Add(d.ReadFloat());
                v.boneWeights.Add(d.ReadFloat());
            }
            else if (boneType == (int)Polygon.BoneTypes.HalfFloat)
            {
                v.boneIds.Add(d.ReadUShort());
                v.boneIds.Add(d.ReadUShort());
                v.boneIds.Add(d.ReadUShort());
                v.boneIds.Add(d.ReadUShort());
                v.boneWeights.Add(d.ReadHalfFloat());
                v.boneWeights.Add(d.ReadHalfFloat());
                v.boneWeights.Add(d.ReadHalfFloat());
                v.boneWeights.Add(d.ReadHalfFloat());
            }
            else if (boneType == (int)Polygon.BoneTypes.Byte)
            {
                v.boneIds.Add(d.ReadByte());
                v.boneIds.Add(d.ReadByte());
                v.boneIds.Add(d.ReadByte());
                v.boneIds.Add(d.ReadByte());
                v.boneWeights.Add((float)d.ReadByte() / 255);
                v.boneWeights.Add((float)d.ReadByte() / 255);
                v.boneWeights.Add((float)d.ReadByte() / 255);
                v.boneWeights.Add((float)d.ReadByte() / 255);
            }
            else
            {
                throw new Exception($"Unsupported bone type: {boneType}");
            }
        }
    }
}
