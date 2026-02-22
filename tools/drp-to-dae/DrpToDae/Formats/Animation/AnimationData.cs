using System;
using System.Collections.Generic;
using System.Numerics;

namespace DrpToDae.Formats.Animation
{
    public enum InterpolationType
    {
        Linear = 0,
        Constant,
        Hermite,
        Step
    }

    public enum RotationType
    {
        Euler = 0,
        Quaternion
    }

    public class KeyFrame
    {
        public float Value { get; set; }
        public float Frame { get; set; }
        public float In { get; set; } = 0;
        public float Out { get; set; } = -1;
        public InterpolationType InterpolationType { get; set; } = InterpolationType.Linear;

        public KeyFrame() { }

        public KeyFrame(float value, float frame)
        {
            Value = value;
            Frame = frame;
        }

        public override string ToString() => $"Frame {Frame}: {Value}";
    }

    public class KeyGroup
    {
        public string Name { get; set; } = "";
        public List<KeyFrame> Keys { get; set; } = new();

        public bool HasAnimation => Keys.Count > 0;

        public float FrameCount
        {
            get
            {
                float fc = 0;
                foreach (var k in Keys)
                    if (k.Frame > fc) fc = k.Frame;
                return fc;
            }
        }

        public KeyFrame GetKeyFrame(float frame)
        {
            KeyFrame? key = null;
            int i;
            for (i = 0; i < Keys.Count; i++)
            {
                if (Keys[i].Frame == frame)
                {
                    key = Keys[i];
                    break;
                }
                if (Keys[i].Frame > frame)
                    break;
            }

            if (key == null)
            {
                key = new KeyFrame { Frame = frame };
                Keys.Insert(i, key);
            }

            return key;
        }

        public float GetValue(float frame)
        {
            if (Keys.Count == 0) return 0;
            
            KeyFrame k1 = Keys[0], k2 = Keys[0];
            foreach (var k in Keys)
            {
                if (k.Frame <= frame)
                    k1 = k;
                else
                {
                    k2 = k;
                    break;
                }
            }

            if (k1.InterpolationType == InterpolationType.Constant || 
                k1.InterpolationType == InterpolationType.Step)
                return k1.Value;

            if (k1.InterpolationType == InterpolationType.Linear)
                return Lerp(k1.Value, k2.Value, k1.Frame, k2.Frame, frame);

            return k1.Value;
        }

        public KeyFrame[] GetFrame(float frame)
        {
            if (Keys.Count == 0) return Array.Empty<KeyFrame>();
            
            KeyFrame k1 = Keys[0], k2 = Keys[0];
            foreach (var k in Keys)
            {
                if (k.Frame <= frame)
                    k1 = k;
                else
                {
                    k2 = k;
                    break;
                }
            }

            return new[] { k1, k2 };
        }

        private static float Lerp(float av, float bv, float v0, float v1, float t)
        {
            if (v0 == v1) return av;
            if (t == v0) return av;
            if (t == v1) return bv;

            float mu = (t - v0) / (v1 - v0);
            return (av * (1 - mu)) + (bv * mu);
        }
    }

    public class KeyNode
    {
        public string Name { get; set; } = "";
        public int Hash { get; set; } = -1;
        public int BoneIndex { get; set; } = -1;

        public KeyGroup XPos { get; set; } = new() { Name = "XPOS" };
        public KeyGroup YPos { get; set; } = new() { Name = "YPOS" };
        public KeyGroup ZPos { get; set; } = new() { Name = "ZPOS" };

        public RotationType RotationType { get; set; } = RotationType.Quaternion;
        public KeyGroup XRot { get; set; } = new() { Name = "XROT" };
        public KeyGroup YRot { get; set; } = new() { Name = "YROT" };
        public KeyGroup ZRot { get; set; } = new() { Name = "ZROT" };
        public KeyGroup WRot { get; set; } = new() { Name = "WROT" };

        public KeyGroup XScale { get; set; } = new() { Name = "XSCA" };
        public KeyGroup YScale { get; set; } = new() { Name = "YSCA" };
        public KeyGroup ZScale { get; set; } = new() { Name = "ZSCA" };

        public KeyNode(string name)
        {
            Name = name;
        }

        public bool HasPositionAnimation => XPos.HasAnimation || YPos.HasAnimation || ZPos.HasAnimation;
        public bool HasRotationAnimation => XRot.HasAnimation || YRot.HasAnimation || ZRot.HasAnimation;
        public bool HasScaleAnimation => XScale.HasAnimation || YScale.HasAnimation || ZScale.HasAnimation;

        public Vector3 GetPosition(float frame) => new(
            XPos.HasAnimation ? XPos.GetValue(frame) : 0,
            YPos.HasAnimation ? YPos.GetValue(frame) : 0,
            ZPos.HasAnimation ? ZPos.GetValue(frame) : 0
        );

        public Quaternion GetRotation(float frame)
        {
            if (!XRot.HasAnimation) return Quaternion.Identity;

            var x = XRot.GetFrame(frame);
            var y = YRot.GetFrame(frame);
            var z = ZRot.GetFrame(frame);
            var w = WRot.GetFrame(frame);

            if (x.Length == 0) return Quaternion.Identity;

            Quaternion q1 = new(x[0].Value, y[0].Value, z[0].Value, w[0].Value);
            Quaternion q2 = new(x[1].Value, y[1].Value, z[1].Value, w[1].Value);

            if (Math.Abs(x[0].Frame - frame) < 0.001f)
                return q1;
            if (Math.Abs(x[1].Frame - frame) < 0.001f)
                return q2;

            float t = (frame - x[0].Frame) / (x[1].Frame - x[0].Frame);
            return Quaternion.Slerp(q1, q2, t);
        }

        public Vector3 GetScale(float frame) => new(
            XScale.HasAnimation ? XScale.GetValue(frame) : 1,
            YScale.HasAnimation ? YScale.GetValue(frame) : 1,
            ZScale.HasAnimation ? ZScale.GetValue(frame) : 1
        );
    }

    public class AnimationData
    {
        public string Name { get; set; } = "";
        public int FrameCount { get; set; }
        public List<KeyNode> Bones { get; set; } = new();

        public AnimationData(string name)
        {
            Name = name;
        }

        public bool HasBone(string name)
        {
            foreach (var bone in Bones)
                if (bone.Name == name)
                    return true;
            return false;
        }

        public KeyNode? GetBone(string name)
        {
            foreach (var bone in Bones)
                if (bone.Name == name)
                    return bone;
            return null;
        }

        public KeyNode? GetBoneByHash(int hash)
        {
            foreach (var bone in Bones)
                if (bone.Hash == hash)
                    return bone;
            return null;
        }

        public KeyNode? GetBoneByIndex(int index)
        {
            foreach (var bone in Bones)
                if (bone.BoneIndex == index)
                    return bone;
            return null;
        }
    }
}
