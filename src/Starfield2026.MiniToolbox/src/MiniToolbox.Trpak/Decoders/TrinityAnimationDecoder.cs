using System;
using System.Collections.Generic;
using FlatSharp;
using OpenTK.Mathematics;
using MiniToolbox.Trpak.Flatbuffers.GF.Animation;
using MiniToolbox.Trpak.Flatbuffers.Utils;

namespace MiniToolbox.Trpak.Decoders
{
    /// <summary>
    /// Data-only animation decoder ported from gftool Animation.cs.
    /// Parses GF animation FlatBuffers and provides per-bone pose sampling.
    /// No GL rendering code — purely functional for DAE export.
    /// </summary>
    public class TrinityAnimationDecoder
    {
        public enum PlayType { Once, Looped }

        public string Name { get; }
        public PlayType LoopType { get; }
        public uint FrameCount { get; }
        public uint FrameRate { get; }
        public int TrackCount => _tracks.Count;
        public IReadOnlyList<string> TrackNames => _trackOrder;

        private readonly Dictionary<string, BoneTrack> _tracks = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _trackOrder = new();

        public TrinityAnimationDecoder(Flatbuffers.GF.Animation.Animation anim, string name)
        {
            Name = name;
            LoopType = anim.Info.DoesLoop != 0 ? PlayType.Looped : PlayType.Once;
            FrameCount = anim.Info.KeyFrames;
            FrameRate = anim.Info.FrameRate;

            if (anim.Skeleton?.Tracks != null)
            {
                foreach (var track in anim.Skeleton.Tracks)
                {
                    if (string.IsNullOrWhiteSpace(track.Name)) continue;

                    if (!_tracks.ContainsKey(track.Name))
                    {
                        _tracks[track.Name] = track;
                        _trackOrder.Add(track.Name);
                    }

                    var normalized = NormalizeBoneName(track.Name);
                    if (!string.IsNullOrWhiteSpace(normalized) && !_tracks.ContainsKey(normalized))
                    {
                        _tracks[normalized] = track;
                    }
                }
            }
        }

        /// <summary>
        /// Convert time in seconds to a frame index, respecting loop type and frame count.
        /// </summary>
        public float GetFrame(float timeSeconds)
        {
            float frameRate = FrameRate > 0 ? FrameRate : 30f;
            float frame = timeSeconds * frameRate;
            if (FrameCount > 0)
            {
                if (LoopType == PlayType.Looped) frame %= FrameCount;
                frame = Math.Clamp(frame, 0f, Math.Max(0f, FrameCount - 1));
            }
            return frame;
        }

        /// <summary>
        /// Sample the pose for a specific bone at a given frame.
        /// Returns false if no track exists for the bone.
        /// </summary>
        public bool TryGetPose(string boneName, float frame, out Vector3? scale, out Quaternion? rotation, out Vector3? translation)
        {
            scale = null;
            rotation = null;
            translation = null;

            if (!TryGetTrack(boneName, out var track)) return false;

            scale = SampleVector(track.Scale, frame);
            rotation = SampleRotation(track.Rotate, frame);
            translation = SampleVector(track.Translate, frame);
            return true;
        }

        /// <summary>
        /// Check if a track exists for the given bone name.
        /// </summary>
        public bool HasTrack(string boneName) => TryGetTrack(boneName, out _);

        #region Track Lookup

        private bool TryGetTrack(string boneName, out BoneTrack track)
        {
            if (string.IsNullOrWhiteSpace(boneName))
            {
                track = default!;
                return false;
            }

            if (_tracks.TryGetValue(boneName, out track!)) return true;

            var normalized = NormalizeBoneName(boneName);
            if (!string.IsNullOrWhiteSpace(normalized) && _tracks.TryGetValue(normalized, out track!)) return true;

            track = default!;
            return false;
        }

        private static string NormalizeBoneName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            name = name.Trim();

            int lastColon = name.LastIndexOf(':');
            if (lastColon >= 0 && lastColon < name.Length - 1) name = name[(lastColon + 1)..];

            int lastPipe = name.LastIndexOf('|');
            if (lastPipe >= 0 && lastPipe < name.Length - 1) name = name[(lastPipe + 1)..];

            int lastSlash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            if (lastSlash >= 0 && lastSlash < name.Length - 1) name = name[(lastSlash + 1)..];

            return name.Trim();
        }

        #endregion

        #region Vector Sampling

        private static Vector3? SampleVector(FlatBufferUnion<FixedVectorTrack, DynamicVectorTrack, Framed16VectorTrack, Framed8VectorTrack> channel, float frame)
        {
            Vector3? result = null;
            channel.Switch(
                defaultCase: () => { },
                case1: v => result = v.Co != null ? ToVector3(v.Co) : null,
                case2: v => result = SampleDynamicVector(v.Co, frame),
                case3: v => result = SampleFramedVector(v.Frames, v.Co, frame),
                case4: v => result = SampleFramedVector(v.Frames, v.Co, frame)
            );
            return result;
        }

        private static Vector3? SampleDynamicVector(IList<Vector3f> values, float frame)
        {
            if (values == null || values.Count == 0) return null;
            int index = Math.Clamp((int)MathF.Floor(frame), 0, values.Count - 1);
            return ToVector3(values[index]);
        }

        private static Vector3? SampleFramedVector<T>(IList<T> frames, IList<Vector3f> values, float frame) where T : struct
        {
            if (frames == null || values == null || frames.Count == 0 || values.Count == 0) return null;

            int count = Math.Min(frames.Count, values.Count);
            float keyFrame = frame;

            if (keyFrame <= GetFrameValue(frames[0])) return ToVector3(values[0]);
            if (keyFrame >= GetFrameValue(frames[count - 1])) return ToVector3(values[count - 1]);

            bool useCatmull = count >= 4;
            for (int i = 0; i < count - 1; i++)
            {
                float k1 = GetFrameValue(frames[i]);
                float k2 = GetFrameValue(frames[i + 1]);
                if (keyFrame >= k1 && keyFrame <= k2)
                {
                    float denom = k2 - k1;
                    if (denom <= 0f) return ToVector3(values[i + 1]);

                    float t = (keyFrame - k1) / denom;
                    var v1 = ToVector3(values[i]);
                    var v2 = ToVector3(values[i + 1]);

                    if (!useCatmull) return Vector3.Lerp(v1, v2, t);

                    return CatmullRomNonUniform(
                        ToVector3(values[Math.Max(i - 1, 0)]), v1, v2,
                        ToVector3(values[Math.Min(i + 2, count - 1)]),
                        GetFrameValue(frames[Math.Max(i - 1, 0)]), k1, k2,
                        GetFrameValue(frames[Math.Min(i + 2, count - 1)]), keyFrame);
                }
            }

            return ToVector3(values[count - 1]);
        }

        #endregion

        #region Rotation Sampling

        private static Quaternion? SampleRotation(FlatBufferUnion<FixedRotationTrack, DynamicRotationTrack, Framed16RotationTrack, Framed8RotationTrack> channel, float frame)
        {
            Quaternion? result = null;
            channel.Switch(
                defaultCase: () => { },
                case1: v => result = v.Co != null ? ToQuaternion(v.Co) : null,
                case2: v => result = SampleDynamicRotation(v.Co, frame),
                case3: v => result = SampleFramedRotation(v.Frames, v.Co, frame),
                case4: v => result = SampleFramedRotation(v.Frames, v.Co, frame)
            );
            return result;
        }

        private static Quaternion? SampleDynamicRotation(IList<PackedQuaternion> values, float frame)
        {
            if (values == null || values.Count == 0) return null;
            int index = Math.Clamp((int)MathF.Floor(frame), 0, values.Count - 1);
            return ToQuaternion(values[index]);
        }

        private static Quaternion? SampleFramedRotation<T>(IList<T> frames, IList<PackedQuaternion> values, float frame) where T : struct
        {
            if (frames == null || values == null || frames.Count == 0 || values.Count == 0) return null;

            int count = Math.Min(frames.Count, values.Count);
            float keyFrame = frame;

            if (keyFrame <= GetFrameValue(frames[0])) return ToQuaternion(values[0]);
            if (keyFrame >= GetFrameValue(frames[count - 1])) return ToQuaternion(values[count - 1]);

            bool useCatmull = count >= 4;
            for (int i = 0; i < count - 1; i++)
            {
                float k1 = GetFrameValue(frames[i]);
                float k2 = GetFrameValue(frames[i + 1]);
                if (keyFrame >= k1 && keyFrame <= k2)
                {
                    float denom = k2 - k1;
                    if (denom <= 0f) return ToQuaternion(values[i + 1]);

                    float t = (keyFrame - k1) / denom;
                    var q1 = ToQuaternion(values[i]);
                    var q2 = ToQuaternion(values[i + 1]);

                    if (!useCatmull) return Quaternion.Slerp(q1, q2, t);

                    return CatmullRomNonUniform(
                        ToQuaternion(values[Math.Max(i - 1, 0)]), q1, q2,
                        ToQuaternion(values[Math.Min(i + 2, count - 1)]),
                        GetFrameValue(frames[Math.Max(i - 1, 0)]), k1, k2,
                        GetFrameValue(frames[Math.Min(i + 2, count - 1)]), keyFrame);
                }
            }

            return ToQuaternion(values[count - 1]);
        }

        #endregion

        #region Interpolation Math

        private static float GetFrameValue<T>(T value) where T : struct
        {
            return value switch
            {
                byte b => b,
                ushort s => s,
                _ => 0f
            };
        }

        private static Vector3 ToVector3(Vector3f v) => new(v.X, v.Y, v.Z);

        private static Vector3 CatmullRomNonUniform(
            in Vector3 p0, in Vector3 p1, in Vector3 p2, in Vector3 p3,
            float t0, float t1, float t2, float t3, float t)
        {
            if (t2 <= t1) return p2;
            float u = (t - t1) / (t2 - t1);
            float t10 = t1 - t0; float t21 = t2 - t1; float t32 = t3 - t2;
            if (t10 <= 0f) t10 = t21;
            if (t32 <= 0f) t32 = t21;

            Vector3 m1 = (p2 - p0) / (t2 - t0) * t21;
            Vector3 m2 = (p3 - p1) / (t3 - t1) * t21;

            float u2 = u * u; float u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * p1 +
                   (u3 - 2f * u2 + u) * m1 +
                   (-2f * u3 + 3f * u2) * p2 +
                   (u3 - u2) * m2;
        }

        private static Quaternion CatmullRomNonUniform(
            in Quaternion q0, in Quaternion q1, in Quaternion q2, in Quaternion q3,
            float t0, float t1, float t2, float t3, float t)
        {
            if (t2 <= t1) return q2;

            float u = Math.Clamp((t - t1) / (t2 - t1), 0f, 1f);

            Quaternion qa = EnsureSameHemisphere(q0, q1);
            Quaternion qb = q1;
            Quaternion qc = EnsureSameHemisphere(q2, q1);
            Quaternion qd = EnsureSameHemisphere(q3, q2);

            float dt10 = MathF.Max(0.000001f, t1 - t0);
            float dt21 = MathF.Max(0.000001f, t2 - t1);
            float dt32 = MathF.Max(0.000001f, t3 - t2);

            Quaternion a1 = ComputeSquadControl(qa, qb, qc, dt10, dt21);
            Quaternion a2 = ComputeSquadControl(qb, qc, qd, dt21, dt32);

            return Squad(qb, qc, a1, a2, u);
        }

        private static Quaternion ComputeSquadControl(in Quaternion qPrev, in Quaternion q, in Quaternion qNext, float dtPrev, float dtNext)
        {
            Quaternion inv = ConjugateNormalized(q);
            Quaternion l1 = Log(EnsureSameHemisphere(Mul(inv, qPrev), Quaternion.Identity));
            Quaternion l2 = Log(EnsureSameHemisphere(Mul(inv, qNext), Quaternion.Identity));

            float wPrev = 1f / MathF.Max(0.000001f, dtPrev);
            float wNext = 1f / MathF.Max(0.000001f, dtNext);
            float wSum = wPrev + wNext;
            if (wSum <= 0f) { wPrev = wNext = 0.5f; }
            else { wPrev /= wSum; wNext /= wSum; }

            Quaternion v = (l1 * wPrev + l2 * wNext) * (-0.25f);
            Quaternion e = Exp(v);
            Quaternion outQ = Mul(q, e);
            if (outQ.LengthSquared > 0f) outQ = outQ.Normalized();
            return outQ;
        }

        private static Quaternion Squad(in Quaternion q1, in Quaternion q2, in Quaternion a1, in Quaternion a2, float t)
        {
            var slerp12 = Quaternion.Slerp(q1, q2, t);
            var slerpA = Quaternion.Slerp(a1, a2, t);
            float h = 2f * t * (1f - t);
            return Quaternion.Slerp(slerp12, slerpA, h);
        }

        private static Quaternion ConjugateNormalized(in Quaternion q)
        {
            Quaternion n = q;
            if (n.LengthSquared > 0f) n = n.Normalized();
            return new Quaternion(-n.X, -n.Y, -n.Z, n.W);
        }

        private static Quaternion Mul(in Quaternion a, in Quaternion b)
        {
            return new Quaternion(
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
                a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
        }

        private static Quaternion Log(in Quaternion q)
        {
            Quaternion n = q;
            if (n.LengthSquared > 0f) n = n.Normalized();
            float w = Math.Clamp(n.W, -1f, 1f);
            float angle = MathF.Acos(w);
            float sin = MathF.Sin(angle);
            if (MathF.Abs(sin) < 1e-6f) return new Quaternion(0f, 0f, 0f, 0f);
            float scale = angle / sin;
            return new Quaternion(n.X * scale, n.Y * scale, n.Z * scale, 0f);
        }

        private static Quaternion Exp(in Quaternion q)
        {
            float angle = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z);
            float sin = MathF.Sin(angle);
            float cos = MathF.Cos(angle);
            if (angle < 1e-6f) return new Quaternion(q.X, q.Y, q.Z, 1f).Normalized();
            float scale = sin / angle;
            return new Quaternion(q.X * scale, q.Y * scale, q.Z * scale, cos);
        }

        private static Quaternion EnsureSameHemisphere(in Quaternion q, in Quaternion reference)
        {
            return Dot(q, reference) < 0f ? new Quaternion(-q.X, -q.Y, -q.Z, -q.W) : q;
        }

        private static float Dot(in Quaternion a, in Quaternion b)
            => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

        private static Quaternion ToQuaternion(PackedQuaternion packed)
        {
            // Inline unpacking from QuaternionExtensions.Unpack()
            const float PiAddend = (float)(Math.PI / 4.0);
            const float PiHalf = (float)(Math.PI / 2.0);
            const float Scale = 0x7FFF;

            ulong pack = ((ulong)packed.Z << 32) | ((ulong)packed.Y << 16) | packed.X;
            float q1 = (float)(((pack >> 3) & 0x7FFF) * (PiHalf / Scale) - PiAddend);
            float q2 = (float)(((pack >> 18) & 0x7FFF) * (PiHalf / Scale) - PiAddend);
            float q3 = (float)(((pack >> 33) & 0x7FFF) * (PiHalf / Scale) - PiAddend);

            float sum = q1 * q1 + q2 * q2 + q3 * q3;
            float maxComponent = MathF.Max(0f, 1f - sum);
            maxComponent = MathF.Sqrt(maxComponent);

            int missingComponent = (int)(pack & 0x3);
            bool isNegative = (pack & 0x4) != 0;

            // Insert the missing (largest) component at its index
            float x, y, z, w;
            switch (missingComponent)
            {
                case 0: x = maxComponent; y = q1; z = q2; w = q3; break;
                case 1: x = q1; y = maxComponent; z = q2; w = q3; break;
                case 2: x = q1; y = q2; z = maxComponent; w = q3; break;
                default: x = q1; y = q2; z = q3; w = maxComponent; break;
            }

            if (isNegative) { x = -x; y = -y; z = -z; w = -w; }

            var result = new Quaternion(x, y, z, w);
            if (result.LengthSquared > 0f) result = result.Normalized();
            return result;
        }

        #endregion
    }
}
