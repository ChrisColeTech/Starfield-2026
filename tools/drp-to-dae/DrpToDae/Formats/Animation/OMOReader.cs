using System;
using System.Numerics;
using DrpToDae.IO;

namespace DrpToDae.Formats.Animation
{
    public class OMOReader
    {
        private const float Epsilon = 1.0e-12f;
        private static readonly float Scale1 = 1 / (float)Math.Sqrt(2f);
        private static readonly float Scale2 = (Scale1 * 2) / 1048575f;

        public static AnimationData Read(string filePath)
        {
            return Read(new FileData(filePath));
        }

        public static AnimationData Read(FileData d)
        {
            d.endian = Endianness.Big;

            string magic = d.ReadString(0, 4);
            if (magic != "OMO ")
                throw new InvalidDataException($"Invalid OMO magic: {magic}");

            d.Seek(4);
            d.Skip(4); // version info (two shorts: 1, 3)
            d.Skip(4); // flags
            d.Skip(2); // padding

            int boneCount = d.ReadUShort();
            int frameCount = d.ReadUShort();
            int frameSize = d.ReadUShort();

            int nodeOffset = d.ReadInt();
            int interOffset = d.ReadInt();
            int keyOffset = d.ReadInt();

            if (boneCount > interOffset / 0x10)
                boneCount = interOffset / 0x10;

            var anim = new AnimationData("Anim") { FrameCount = frameCount };

            d.Seek(nodeOffset);
            var baseNodes = new OmoKeyNode[boneCount];
            var frameKeys = new int[boneCount];

            for (int i = 0; i < boneCount; i++)
            {
                int flags = d.ReadInt();
                d.Seek(d.Pos() - 4);

                flags = d.ReadByte();
                int tFlag = d.ReadByte();
                int rFlag = d.ReadByte();
                int sFlag = d.ReadByte();
                int hash = d.ReadInt();
                int off1 = d.ReadInt() + interOffset;
                frameKeys[i] = d.ReadInt();

                bool hasTrans = (flags & 0x01) == 0x01;
                bool hasScale = (flags & 0x04) == 0x04;
                bool hasRot = (flags & 0x02) == 0x02;

                var node = new OmoKeyNode { Hash = (uint)hash };
                baseNodes[i] = node;

                int temp = d.Pos();
                d.Seek(off1);

                if (hasTrans)
                {
                    node.HasTranslation = true;
                    if (tFlag == 0x8)
                    {
                        node.TranslationType = OmoKeyNode.InterpolationType.Interpolated;
                        node.Translation = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                        node.Translation2 = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                    }
                    else if (tFlag == 0x20)
                    {
                        node.TranslationType = OmoKeyNode.InterpolationType.Constant;
                        node.Translation = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                    }
                    else if (tFlag == 0x4)
                    {
                        node.TranslationType = OmoKeyNode.InterpolationType.Keyframe;
                    }
                }

                if (hasRot)
                {
                    node.HasRotation = true;
                    if ((rFlag & 0xF0) == 0xA0)
                    {
                        node.RotationType = OmoKeyNode.InterpolationType.Compressed;
                    }
                    else if ((rFlag & 0xF0) == 0x50)
                    {
                        node.RotationType = OmoKeyNode.InterpolationType.Interpolated;
                        node.RotationVec = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                        node.RotationVec2 = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                    }
                    else if ((rFlag & 0xF0) == 0x60)
                    {
                        node.RotationType = OmoKeyNode.InterpolationType.Keyframe;
                        node.RotationVec = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                        node.RotationExtra = d.ReadFloat() / 65535;
                    }
                    else if ((rFlag & 0xF0) == 0x70)
                    {
                        node.RotationType = OmoKeyNode.InterpolationType.Constant;
                        node.RotationVec = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                    }
                }

                if (hasScale)
                {
                    node.HasScale = true;
                    if ((sFlag & 0xF0) == 0x80)
                    {
                        node.ScaleType = OmoKeyNode.InterpolationType.Interpolated;
                        node.Scale = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                        node.Scale2 = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                    }
                    if ((rFlag & 0x0F) == 0x02 || (rFlag & 0x0F) == 0x03)
                    {
                        node.ScaleType = OmoKeyNode.InterpolationType.Constant;
                        node.Scale = new Vector3(d.ReadFloat(), d.ReadFloat(), d.ReadFloat());
                    }
                }

                d.Seek(temp);
            }

            d.Seek(keyOffset);

            for (int j = 0; j < boneCount; j++)
            {
                string boneName = $"Bone_{baseNodes[j].Hash:X}";
                int hash = (int)baseNodes[j].Hash;

                var keyNode = new KeyNode(boneName)
                {
                    Hash = hash,
                    BoneIndex = j
                };
                anim.Bones.Add(keyNode);

                for (int i = 0; i < frameCount; i++)
                {
                    d.Seek(keyOffset + frameSize * i + frameKeys[j]);

                    if (baseNodes[j].HasTranslation)
                    {
                        float x, y, z;
                        switch (baseNodes[j].TranslationType)
                        {
                            case OmoKeyNode.InterpolationType.Interpolated:
                                float i1 = d.ReadUShort() / 65535f;
                                float i2 = d.ReadUShort() / 65535f;
                                float i3 = d.ReadUShort() / 65535f;
                                x = baseNodes[j].Translation.X + baseNodes[j].Translation2.X * i1;
                                y = baseNodes[j].Translation.Y + baseNodes[j].Translation2.Y * i2;
                                z = baseNodes[j].Translation.Z + baseNodes[j].Translation2.Z * i3;
                                break;
                            case OmoKeyNode.InterpolationType.Constant:
                                x = baseNodes[j].Translation.X;
                                y = baseNodes[j].Translation.Y;
                                z = baseNodes[j].Translation.Z;
                                break;
                            default:
                                x = d.ReadFloat();
                                y = d.ReadFloat();
                                z = d.ReadFloat();
                                break;
                        }
                        keyNode.XPos.Keys.Add(new KeyFrame(x, i));
                        keyNode.YPos.Keys.Add(new KeyFrame(y, i));
                        keyNode.ZPos.Keys.Add(new KeyFrame(z, i));
                    }

                    if (baseNodes[j].HasRotation)
                    {
                        Quaternion r;
                        switch (baseNodes[j].RotationType)
                        {
                            case OmoKeyNode.InterpolationType.Compressed:
                                r = ReadCompressedRotation(d);
                                break;
                            case OmoKeyNode.InterpolationType.Interpolated:
                                float i1 = d.ReadUShort() / 65535f;
                                float i2 = d.ReadUShort() / 65535f;
                                float i3 = d.ReadUShort() / 65535f;
                                float x = baseNodes[j].RotationVec.X + baseNodes[j].RotationVec2.X * i1;
                                float y = baseNodes[j].RotationVec.Y + baseNodes[j].RotationVec2.Y * i2;
                                float z = baseNodes[j].RotationVec.Z + baseNodes[j].RotationVec2.Z * i3;
                                float w = (float)Math.Sqrt(Math.Abs(1 - (x * x + y * y + z * z)));
                                r = Quaternion.Normalize(new Quaternion(x, y, z, w));
                                break;
                            case OmoKeyNode.InterpolationType.Keyframe:
                                float scale = d.ReadUShort() * baseNodes[j].RotationExtra;
                                float kx = baseNodes[j].RotationVec.X;
                                float ky = baseNodes[j].RotationVec.Y;
                                float kz = baseNodes[j].RotationVec.Z + scale;
                                float kw = Rot6CalculateW(kx, ky, kz);
                                r = new Quaternion(kx, ky, kz, kw);
                                break;
                            default:
                                float cx = baseNodes[j].RotationVec.X;
                                float cy = baseNodes[j].RotationVec.Y;
                                float cz = baseNodes[j].RotationVec.Z;
                                float cw = (float)Math.Sqrt(Math.Abs(1 - (cx * cx + cy * cy + cz * cz)));
                                r = Quaternion.Normalize(new Quaternion(cx, cy, cz, cw));
                                break;
                        }
                        keyNode.RotationType = RotationType.Quaternion;
                        keyNode.XRot.Keys.Add(new KeyFrame(r.X, i));
                        keyNode.YRot.Keys.Add(new KeyFrame(r.Y, i));
                        keyNode.ZRot.Keys.Add(new KeyFrame(r.Z, i));
                        keyNode.WRot.Keys.Add(new KeyFrame(r.W, i));
                    }

                    if (baseNodes[j].HasScale)
                    {
                        float sx, sy, sz;
                        if (baseNodes[j].ScaleType == OmoKeyNode.InterpolationType.Interpolated)
                        {
                            float si1 = d.ReadUShort() / 65535f;
                            float si2 = d.ReadUShort() / 65535f;
                            float si3 = d.ReadUShort() / 65535f;
                            sx = baseNodes[j].Scale.X + baseNodes[j].Scale2.X * si1;
                            sy = baseNodes[j].Scale.Y + baseNodes[j].Scale2.Y * si2;
                            sz = baseNodes[j].Scale.Z + baseNodes[j].Scale2.Z * si3;
                        }
                        else
                        {
                            sx = baseNodes[j].Scale.X;
                            sy = baseNodes[j].Scale.Y;
                            sz = baseNodes[j].Scale.Z;
                        }
                        keyNode.XScale.Keys.Add(new KeyFrame(sx, i));
                        keyNode.YScale.Keys.Add(new KeyFrame(sy, i));
                        keyNode.ZScale.Keys.Add(new KeyFrame(sz, i));
                    }
                }
            }

            return anim;
        }

        private static Quaternion ReadCompressedRotation(FileData d)
        {
            int b1 = d.ReadByte();
            int b2 = d.ReadByte();
            int b3 = d.ReadByte();
            int b4 = d.ReadByte();
            int b5 = d.ReadByte();
            int b6 = d.ReadByte();
            int b7 = d.ReadByte();
            int b8 = d.ReadByte();

            int f1 = (b1 << 12) | (b2 << 4) | ((b3 & 0xF0) >> 4);
            int f2 = ((b3 & 0xF) << 16) | (b4 << 8) | b5;
            int f3 = (b6 << 12) | (b7 << 4) | ((b8 & 0xF0) >> 4);
            int flags = b8 & 0xF;

            float c1 = EncodedRotToQuaternionComponent(f1);
            float c2 = EncodedRotToQuaternionComponent(f2);
            float c3 = EncodedRotToQuaternionComponent(f3);
            float missing = (float)Math.Sqrt(Math.Abs(1 - (c1 * c1 + c2 * c2 + c3 * c3)));

            return flags switch
            {
                0 => new Quaternion(missing, c1, c2, c3),
                1 => new Quaternion(c1, missing, c2, c3),
                2 => new Quaternion(c1, c2, missing, c3),
                3 => new Quaternion(c1, c2, c3, missing),
                _ => Quaternion.Identity
            };
        }

        private static float EncodedRotToQuaternionComponent(float toConvert)
        {
            return (toConvert * Scale2) - Scale1;
        }

        private static float Rot6CalculateW(float x, float y, float z)
        {
            float cumulative = 1 - (x * x + y * y + z * z);
            float f12 = (float)(1 / Math.Sqrt(cumulative));
            float sqrtCumulative = (cumulative - Epsilon) < 0 ? 0f : f12;

            float f7 = (0.5f * cumulative) * sqrtCumulative;
            float f8 = 1.5f - (f7 * sqrtCumulative);
            float f0 = f8 * sqrtCumulative;
            float f9 = (0.5f * cumulative) * f0;
            float f10 = 1.5f - (f9 * f0);
            f0 = f0 * f10;
            float f11 = (0.5f * cumulative) * f0;
            float f13 = 1.5f - (f11 * f0);
            f0 = f0 * f13;
            f7 = cumulative * f0;

            return f7;
        }

        private class OmoKeyNode
        {
            public enum InterpolationType
            {
                None,
                Interpolated,
                Constant,
                Keyframe,
                Compressed
            }

            public uint Hash;
            public bool HasTranslation;
            public InterpolationType TranslationType;
            public Vector3 Translation;
            public Vector3 Translation2;

            public bool HasRotation;
            public InterpolationType RotationType;
            public Vector3 RotationVec;
            public Vector3 RotationVec2;
            public float RotationExtra;

            public bool HasScale;
            public InterpolationType ScaleType;
            public Vector3 Scale;
            public Vector3 Scale2;
        }
    }

    public class InvalidDataException : Exception
    {
        public InvalidDataException(string message) : base(message) { }
    }
}
