using System;
using System.Collections.Generic;
using System.Numerics;
using DrpToDae.IO;

namespace DrpToDae.Formats.VBN
{
    public class VBN
    {
        public string FilePath { get; set; } = "";
        public short Unk1 { get; set; }
        public short Unk2 { get; set; }
        public uint TotalBoneCount { get; set; }
        public uint[] BoneCountPerType { get; set; } = new uint[4];
        public List<Bone> Bones { get; set; } = new List<Bone>();
        public Endianness Endian { get; set; }

        public VBN() { }

        public VBN(string filename)
        {
            FilePath = filename;
            Read(filename);
        }

        public void Read(string filename)
        {
            var file = new FileData(filename);
            file.endian = Endianness.Little;
            Endian = Endianness.Little;

            string magic = file.ReadString(0, 4);
            if (magic == "VBN ")
            {
                file.endian = Endianness.Big;
                Endian = Endianness.Big;
            }

            file.Seek(4);
            Unk1 = file.ReadShort();
            Unk2 = file.ReadShort();
            TotalBoneCount = (uint)file.ReadInt();
            BoneCountPerType[0] = (uint)file.ReadInt();
            BoneCountPerType[1] = (uint)file.ReadInt();
            BoneCountPerType[2] = (uint)file.ReadInt();
            BoneCountPerType[3] = (uint)file.ReadInt();

            int[] parentIndices = new int[TotalBoneCount];
            for (int i = 0; i < TotalBoneCount; i++)
            {
                var bone = new Bone
                {
                    ParentVbn = this,
                    Name = file.ReadString(file.Pos(), -1)
                };
                file.Skip(64);
                bone.BoneType = (uint)file.ReadInt();
                parentIndices[i] = file.ReadInt();
                bone.BoneId = (uint)file.ReadInt();
                Bones.Add(bone);
            }

            for (int i = 0; i < Bones.Count; i++)
            {
                Bones[i].Position[0] = file.ReadFloat();
                Bones[i].Position[1] = file.ReadFloat();
                Bones[i].Position[2] = file.ReadFloat();
                Bones[i].Rotation[0] = file.ReadFloat();
                Bones[i].Rotation[1] = file.ReadFloat();
                Bones[i].Rotation[2] = file.ReadFloat();
                Bones[i].Scale[0] = file.ReadFloat();
                Bones[i].Scale[1] = file.ReadFloat();
                Bones[i].Scale[2] = file.ReadFloat();
                Bones[i].ParentIndex = parentIndices[i];
            }

            UpdateTransforms();
            BuildHierarchy();
        }

        private void UpdateTransforms()
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                var bone = Bones[i];
                var scale = Matrix4x4.CreateScale(bone.Sca);
                var rotation = Matrix4x4.CreateFromQuaternion(bone.Rot);
                var translation = Matrix4x4.CreateTranslation(bone.Pos);
                bone.Transform = scale * rotation * translation;

                if (bone.ParentIndex >= 0 && bone.ParentIndex < Bones.Count)
                {
                    bone.Transform = bone.Transform * Bones[bone.ParentIndex].Transform;
                }

                if (Matrix4x4.Invert(bone.Transform, out var inverse))
                {
                    bone.InverseTransform = inverse;
                }
                else
                {
                    bone.InverseTransform = Matrix4x4.Identity;
                }
            }
        }

        private void BuildHierarchy()
        {
            foreach (var bone in Bones)
            {
                if (bone.ParentIndex >= 0 && bone.ParentIndex < Bones.Count)
                {
                    Bones[bone.ParentIndex].Children.Add(bone);
                }
            }
        }

        public Bone? GetBone(string name)
        {
            foreach (var bone in Bones)
                if (bone.Name == name)
                    return bone;
            return null;
        }

        public Bone? GetBone(uint hash)
        {
            foreach (var bone in Bones)
                if (bone.BoneId == hash)
                    return bone;
            return null;
        }

        public int GetBoneIndex(string name)
        {
            for (int i = 0; i < Bones.Count; i++)
                if (Bones[i].Name == name)
                    return i;
            return -1;
        }

        public List<Bone> GetBoneTreeOrder()
        {
            var result = new List<Bone>();
            if (Bones.Count == 0) return result;

            var queue = new Queue<Bone>();
            foreach (var bone in Bones)
            {
                if (bone.ParentIndex < 0)
                    queue.Enqueue(bone);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);
                foreach (var child in current.Children)
                    queue.Enqueue(child);
            }

            return result;
        }

        public Matrix4x4[] GetShaderMatrices()
        {
            var matrices = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                matrices[i] = Bones[i].InverseTransform * Bones[i].Transform;
            }
            return matrices;
        }
    }
}
