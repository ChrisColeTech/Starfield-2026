using System;
using System.Collections.Generic;
using System.Numerics;

namespace DrpToDae.Formats.VBN
{
    public class Bone
    {
        public string Name { get; set; } = "";
        public uint BoneType { get; set; }
        public uint BoneRotationType { get; set; }
        public uint BoneId { get; set; }
        public int ParentIndex { get; set; } = -1;

        public float[] Position { get; set; } = new float[3];
        public float[] Rotation { get; set; } = new float[3];
        public float[] Scale { get; set; } = new float[3] { 1, 1, 1 };

        public Vector3 Pos => new Vector3(Position[0], Position[1], Position[2]);
        public Vector3 Sca => new Vector3(Scale[0], Scale[1], Scale[2]);
        public Quaternion Rot => BoneRotationType == 1
            ? CreateQuaternion(Rotation[2], Rotation[1], Rotation[0], Rotation[3])
            : FromEulerAngles(Rotation[2], Rotation[1], Rotation[0]);

        public Matrix4x4 Transform { get; set; }
        public Matrix4x4 InverseTransform { get; set; }

        public VBN? ParentVbn { get; set; }
        public List<Bone> Children { get; set; } = new List<Bone>();

        public enum Type
        {
            Normal = 0,
            Follow = 1,
            Helper = 2,
            Swing = 3
        }

        public enum RotationType
        {
            Euler = 0,
            Quaternion = 1
        }

        private static Quaternion FromEulerAngles(float z, float y, float x)
        {
            var xRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, x);
            var yRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, y);
            var zRot = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z);
            var q = Quaternion.Normalize(zRot * yRot * xRot);
            if (q.W < 0) q = Quaternion.Negate(q);
            return q;
        }

        private static Quaternion CreateQuaternion(float z, float y, float x, float w)
        {
            var q = new Quaternion(x, y, z, w);
            if (q.W < 0) q = Quaternion.Negate(q);
            return Quaternion.Normalize(q);
        }
    }
}
