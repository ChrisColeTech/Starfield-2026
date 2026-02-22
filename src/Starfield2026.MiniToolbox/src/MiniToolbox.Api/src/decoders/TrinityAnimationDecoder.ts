import { Vector3, MathQuaternion } from './Math.js';
import { Vector3f, PackedQuaternion } from '../flatbuffers/Common/Math.js';
import { Animation, BoneTrack, FixedVectorTrack, DynamicVectorTrack, Framed16VectorTrack, Framed8VectorTrack, FixedRotationTrack, DynamicRotationTrack, Framed16RotationTrack, Framed8RotationTrack } from '../flatbuffers/GF/Animation/index.js';

export enum PlayType {
    Once,
    Looped
}

/**
 * Data-only animation decoder ported from gftool Animation.cs.
 * Parses GF animation FlatBuffers and provides per-bone pose sampling.
 * No GL rendering code — purely functional for DAE export.
 */
export class TrinityAnimationDecoder {
    public Name: string;
    public LoopType: PlayType;
    public FrameCount: number;
    public FrameRate: number;

    private _tracks: Map<string, BoneTrack> = new Map();
    private _trackOrder: string[] = [];

    public get TrackCount(): number {
        return this._tracks.size;
    }

    public get TrackNames(): readonly string[] {
        return this._trackOrder;
    }

    constructor(anim: Animation, name: string) {
        this.Name = name;
        this.LoopType = anim.Info.DoesLoop !== 0 ? PlayType.Looped : PlayType.Once;
        this.FrameCount = anim.Info.KeyFrames;
        this.FrameRate = anim.Info.FrameRate;

        if (anim.Skeleton?.Tracks != null) {
            for (const track of anim.Skeleton.Tracks) {
                if (!track.Name || track.Name.trim().length === 0) {
                    continue;
                }

                const trackNameLower = track.Name.toLowerCase();
                if (!this._tracks.has(trackNameLower)) {
                    this._tracks.set(trackNameLower, track);
                    this._trackOrder.push(track.Name);
                }

                const normalized = TrinityAnimationDecoder.NormalizeBoneName(track.Name);
                if (normalized && normalized.length > 0) {
                    const normalizedLower = normalized.toLowerCase();
                    if (!this._tracks.has(normalizedLower)) {
                        this._tracks.set(normalizedLower, track);
                    }
                }
            }
        }
    }

    /**
     * Convert time in seconds to a frame index, respecting loop type and frame count.
     */
    public GetFrame(timeSeconds: number): number {
        const frameRate = this.FrameRate > 0 ? this.FrameRate : 30;
        let frame = timeSeconds * frameRate;
        if (this.FrameCount > 0) {
            if (this.LoopType === PlayType.Looped) {
                frame = frame % this.FrameCount;
            }
            frame = Math.max(0, Math.min(frame, Math.max(0, this.FrameCount - 1)));
        }
        return frame;
    }

    /**
     * Sample pose for a specific bone at a given frame.
     * Returns false if no track exists for bone.
     */
    public TryGetPose(boneName: string, frame: number): { scale: Vector3 | null; rotation: MathQuaternion | null; translation: Vector3 | null; success: boolean } {
        const track = this.TryGetTrack(boneName);
        if (!track) {
            return { scale: null, rotation: null, translation: null, success: false };
        }

        const scale = this.SampleVector(track.Scale, frame);
        const rotation = this.SampleRotation(track.Rotate, frame);
        const translation = this.SampleVector(track.Translate, frame);
        return { scale, rotation, translation, success: true };
    }

    /**
     * Check if a track exists for given bone name.
     */
    public HasTrack(boneName: string): boolean {
        return this.TryGetTrack(boneName) !== null;
    }

    private TryGetTrack(boneName: string): BoneTrack | null {
        if (!boneName || boneName.trim().length === 0) {
            return null;
        }

        const boneNameLower = boneName.toLowerCase();
        if (this._tracks.has(boneNameLower)) {
            return this._tracks.get(boneNameLower)!;
        }

        const normalized = TrinityAnimationDecoder.NormalizeBoneName(boneName);
        if (normalized && normalized.length > 0) {
            const normalizedLower = normalized.toLowerCase();
            if (this._tracks.has(normalizedLower)) {
                return this._tracks.get(normalizedLower)!;
            }
        }

        return null;
    }

    private static NormalizeBoneName(name: string): string {
        if (!name || name.trim().length === 0) {
            return '';
        }
        name = name.trim();

        const lastColon = name.lastIndexOf(':');
        if (lastColon >= 0 && lastColon < name.length - 1) {
            name = name.substring(lastColon + 1);
        }

        const lastPipe = name.lastIndexOf('|');
        if (lastPipe >= 0 && lastPipe < name.length - 1) {
            name = name.substring(lastPipe + 1);
        }

        const lastSlash = Math.max(name.lastIndexOf('/'), name.lastIndexOf('\\'));
        if (lastSlash >= 0 && lastSlash < name.length - 1) {
            name = name.substring(lastSlash + 1);
        }

        return name.trim();
    }

    private SampleVector(track: FixedVectorTrack | DynamicVectorTrack | Framed16VectorTrack | Framed8VectorTrack | null, frame: number): Vector3 | null {
        if (!track) return null;

        if (track instanceof FixedVectorTrack) {
            return new Vector3(track.Co.X, track.Co.Y, track.Co.Z);
        }

        if (track instanceof DynamicVectorTrack) {
            if (track.Co.length === 0) return null;
            const index = Math.max(0, Math.min(Math.floor(frame), track.Co.length - 1));
            const v = track.Co[index];
            return new Vector3(v.X, v.Y, v.Z);
        }

        if (track instanceof Framed16VectorTrack || track instanceof Framed8VectorTrack) {
            return this.SampleFramedVector(track.Frames, track.Co.map(v => new Vector3(v.X, v.Y, v.Z)), frame);
        }

        return null;
    }

    private SampleRotation(track: FixedRotationTrack | DynamicRotationTrack | Framed16RotationTrack | Framed8RotationTrack | null, frame: number): MathQuaternion | null {
        if (!track) return null;

        if (track instanceof FixedRotationTrack) {
            return TrinityAnimationDecoder.ToMathQuaternion(track.Co);
        }

        if (track instanceof DynamicRotationTrack) {
            if (track.Co.length === 0) return null;
            const index = Math.max(0, Math.min(Math.floor(frame), track.Co.length - 1));
            return TrinityAnimationDecoder.ToMathQuaternion(track.Co[index]);
        }

        if (track instanceof Framed16RotationTrack || track instanceof Framed8RotationTrack) {
            return this.SampleFramedRotation(track.Frames, track.Co.map(v => TrinityAnimationDecoder.ToMathQuaternion(v)), frame);
        }

        return null;
    }

    private SampleFramedVector(frames: number[], values: Vector3[], frame: number): Vector3 | null {
        if (!frames.length || !values.length) return null;

        const count = Math.min(frames.length, values.length);
        const keyFrame = frame;

        if (keyFrame <= frames[0]) return values[0];
        if (keyFrame >= frames[count - 1]) return values[count - 1];

        const useCatmull = count >= 4;
        for (let i = 0; i < count - 1; i++) {
            const k1 = frames[i];
            const k2 = frames[i + 1];
            if (keyFrame >= k1 && keyFrame <= k2) {
                const denom = k2 - k1;
                if (denom <= 0) return values[i + 1];

                const t = (keyFrame - k1) / denom;
                const v1 = values[i];
                const v2 = values[i + 1];

                if (!useCatmull) return Vector3.Lerp(v1, v2, t);

                const v0 = values[Math.max(i - 1, 0)];
                const v3 = values[Math.min(i + 2, count - 1)];

                return TrinityAnimationDecoder.CatmullRomNonUniform(
                    v0, v1, v2, v3,
                    frames[Math.max(i - 1, 0)], k1, k2,
                    frames[Math.min(i + 2, count - 1)], keyFrame
                );
            }
        }

        return values[count - 1];
    }

    private SampleFramedRotation(frames: number[], values: MathQuaternion[], frame: number): MathQuaternion | null {
        if (!frames.length || !values.length) return null;

        const count = Math.min(frames.length, values.length);
        const keyFrame = frame;

        if (keyFrame <= frames[0]) return values[0];
        if (keyFrame >= frames[count - 1]) return values[count - 1];

        const useCatmull = count >= 4;
        for (let i = 0; i < count - 1; i++) {
            const k1 = frames[i];
            const k2 = frames[i + 1];
            if (keyFrame >= k1 && keyFrame <= k2) {
                const denom = k2 - k1;
                if (denom <= 0) return values[i + 1];

                const t = Math.max(0, Math.min(1, (keyFrame - k1) / denom));
                const q1 = values[i];
                const q2 = values[i + 1];

                if (!useCatmull) return MathQuaternion.Slerp(q1, q2, t);

                const q0 = values[Math.max(i - 1, 0)];
                const q3 = values[Math.min(i + 2, count - 1)];

                return TrinityAnimationDecoder.CatmullRomQuaternion(
                    q0, q1, q2, q3,
                    frames[Math.max(i - 1, 0)], k1, k2,
                    frames[Math.min(i + 2, count - 1)], keyFrame
                );
            }
        }

        return values[count - 1];
    }

    private static CatmullRomNonUniform(
        p0: Vector3, p1: Vector3, p2: Vector3, p3: Vector3,
        t0: number, t1: number, t2: number, t3: number, t: number
    ): Vector3 {
        if (t2 <= t1) return p2;

        const u = (t - t1) / (t2 - t1);
        let t10 = t1 - t0;
        let t21 = t2 - t1;
        let t32 = t3 - t2;

        if (t10 <= 0) t10 = t21;
        if (t32 <= 0) t32 = t21;

        const m1 = Vector3.Multiply(Vector3.Subtract(p2, p0), t21 / (t2 - t0));
        const m2 = Vector3.Multiply(Vector3.Subtract(p3, p1), t21 / (t3 - t1));

        const u2 = u * u;
        const u3 = u2 * u;

        const term1 = Vector3.Multiply(p1, 2 * u3 - 3 * u2 + 1);
        const term2 = Vector3.Multiply(p2, -2 * u3 + 3 * u2);
        const term3 = Vector3.Multiply(m1, u3 - 2 * u2 + u);
        const term4 = Vector3.Multiply(m2, u3 - u2);

        return Vector3.Add(Vector3.Add(Vector3.Add(term1, term2), term3), term4);
    }

    private static CatmullRomQuaternion(
        q0: MathQuaternion, q1: MathQuaternion, q2: MathQuaternion, q3: MathQuaternion,
        t0: number, t1: number, t2: number, t3: number, t: number
    ): MathQuaternion {
        if (t2 <= t1) return q2;

        const u = Math.max(0, Math.min(1, (t - t1) / (t2 - t1)));

        const qa = TrinityAnimationDecoder.EnsureSameHemisphere(q0, q1);
        const qb = q1;
        const qc = TrinityAnimationDecoder.EnsureSameHemisphere(q2, q1);
        const qd = TrinityAnimationDecoder.EnsureSameHemisphere(q3, q2);

        const dt10 = Math.max(0.000001, t1 - t0);
        const dt21 = Math.max(0.000001, t2 - t1);
        const dt32 = Math.max(0.000001, t3 - t2);

        const a1 = TrinityAnimationDecoder.ComputeSquadControl(qa, qb, qc, dt10, dt21);
        const a2 = TrinityAnimationDecoder.ComputeSquadControl(qb, qc, qd, dt21, dt32);

        return TrinityAnimationDecoder.Squad(qb, qc, a1, a2, u);
    }

    private static EnsureSameHemisphere(q: MathQuaternion, reference: MathQuaternion): MathQuaternion {
        const dot = MathQuaternion.Dot(q, reference);
        return dot < 0 ? new MathQuaternion(-q.x, -q.y, -q.z, -q.w) : q;
    }

    private static ComputeSquadControl(qPrev: MathQuaternion, q: MathQuaternion, qNext: MathQuaternion, dtPrev: number, dtNext: number): MathQuaternion {
        const inv = TrinityAnimationDecoder.ConjugateNormalized(q);
        const l1 = TrinityAnimationDecoder.Log(TrinityAnimationDecoder.EnsureSameHemisphere(MathQuaternion.Multiply(inv, qPrev), MathQuaternion.Identity));
        const l2 = TrinityAnimationDecoder.Log(TrinityAnimationDecoder.EnsureSameHemisphere(MathQuaternion.Multiply(inv, qNext), MathQuaternion.Identity));

        const wPrev = 1 / Math.max(0.000001, dtPrev);
        const wNext = 1 / Math.max(0.000001, dtNext);
        const wSum = wPrev + wNext;

        const wp = wSum <= 0 ? 0.5 : wPrev / wSum;
        const wn = wSum <= 0 ? 0.5 : wNext / wSum;

        const v = new MathQuaternion(
            (l1.x * wp + l2.x * wn) * -0.25,
            (l1.y * wp + l2.y * wn) * -0.25,
            (l1.z * wp + l2.z * wn) * -0.25,
            (l1.w * wp + l2.w * wn) * -0.25
        );

        const e = TrinityAnimationDecoder.Exp(v);
        const outQ = MathQuaternion.Multiply(q, e);
        const lenSq = outQ.LengthSquared;
        return lenSq > 0 ? outQ.Normalized() : outQ;
    }

    private static ConjugateNormalized(q: MathQuaternion): MathQuaternion {
        const lenSq = q.LengthSquared;
        const n = lenSq > 0 ? new MathQuaternion(q.x / Math.sqrt(lenSq), q.y / Math.sqrt(lenSq), q.z / Math.sqrt(lenSq), q.w / Math.sqrt(lenSq)) : q;
        return new MathQuaternion(-n.x, -n.y, -n.z, n.w);
    }

    private static Log(q: MathQuaternion): MathQuaternion {
        const lenSq = q.LengthSquared;
        const n = lenSq > 0 ? new MathQuaternion(q.x / Math.sqrt(lenSq), q.y / Math.sqrt(lenSq), q.z / Math.sqrt(lenSq), q.w / Math.sqrt(lenSq)) : q;
        const w = Math.max(-1, Math.min(1, n.w));
        const angle = Math.acos(w);
        const sin = Math.sin(angle);

        if (Math.abs(sin) < 1e-6) {
            return new MathQuaternion(0, 0, 0, 0);
        }

        const scale = angle / sin;
        return new MathQuaternion(n.x * scale, n.y * scale, n.z * scale, 0);
    }

    private static Exp(v: MathQuaternion): MathQuaternion {
        const angle = Math.sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        const sin = Math.sin(angle);
        const cos = Math.cos(angle);

        if (angle < 1e-6) {
            return new MathQuaternion(v.x, v.y, v.z, 1).Normalized();
        }

        const scale = sin / angle;
        return new MathQuaternion(v.x * scale, v.y * scale, v.z * scale, cos).Normalized();
    }

    private static Squad(q1: MathQuaternion, q2: MathQuaternion, a1: MathQuaternion, a2: MathQuaternion, t: number): MathQuaternion {
        const slerp12 = MathQuaternion.Slerp(q1, q2, t);
        const slerpA = MathQuaternion.Slerp(a1, a2, t);
        const h = 2 * t * (1 - t);
        return MathQuaternion.Slerp(slerp12, slerpA, h);
    }

    private static ToVector3(v: Vector3f): Vector3 {
        return new Vector3(v.X, v.Y, v.Z);
    }

    private static ToMathQuaternion(packed: PackedQuaternion): MathQuaternion {
        const PI_ADDEND = Math.PI / 4;
        const PI_HALF = Math.PI / 2;
        const SCALE = 0x7FFF;

        const pack = (packed.Z * 0x100000000) | (packed.Y << 16) | packed.X;
        const q1 = ((pack >> 3) & 0x7FFF) * (PI_HALF / SCALE) - PI_ADDEND;
        const q2 = ((pack >> 18) & 0x7FFF) * (PI_HALF / SCALE) - PI_ADDEND;
        const q3 = ((pack >> 33) & 0x7FFF) * (PI_HALF / SCALE) - PI_ADDEND;

        const sum = q1 * q1 + q2 * q2 + q3 * q3;
        const maxComponent = Math.max(0, Math.sqrt(Math.max(0, 1 - sum)));

        const missingComponent = (pack & 0x3);
        const isNegative = (pack & 0x4) !== 0;

        let x: number, y: number, z: number, w: number;

        switch (missingComponent) {
            case 0:
                x = maxComponent; y = q1; z = q2; w = q3;
                break;
            case 1:
                x = q1; y = maxComponent; z = q2; w = q3;
                break;
            case 2:
                x = q1; y = q2; z = maxComponent; w = q3;
                break;
            default:
                x = q1; y = q2; z = q3; w = maxComponent;
                break;
        }

        if (isNegative) {
            x = -x; y = -y; z = -z; w = -w;
        }

        const result = new MathQuaternion(x, y, z, w);
        return result.LengthSquared > 0 ? result.Normalized() : result;
    }
}
