export class Vector2 {
    constructor(public x: number, public y: number) {}
    static get Zero(): Vector2 { return new Vector2(0, 0); }
}

export class Vector3 {
    constructor(public x: number, public y: number, public z: number) {}
    static get Zero(): Vector3 { return new Vector3(0, 0, 0); }
    static get UnitX(): Vector3 { return new Vector3(1, 0, 0); }
    static get UnitY(): Vector3 { return new Vector3(0, 1, 0); }
    static get UnitZ(): Vector3 { return new Vector3(0, 0, 1); }

    lengthSquared(): number { return this.x * this.x + this.y * this.y + this.z * this.z; }

    length(): number { return Math.sqrt(this.lengthSquared()); }

    normalize(): Vector3 {
      const len = this.length();
      return len !== 0 ? new Vector3(this.x / len, this.y / len, this.z / len) : Vector3.Zero;
    }

    static Lerp(a: Vector3, b: Vector3, t: number): Vector3 {
        return new Vector3(
            a.x + (b.x - a.x) * t,
            a.y + (b.y - a.y) * t,
            a.z + (b.z - a.z) * t
        );
    }

    static Add(a: Vector3, b: Vector3): Vector3 {
        return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    static Subtract(a: Vector3, b: Vector3): Vector3 {
        return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    static Multiply(v: Vector3, s: number | Vector3): Vector3 {
        if (typeof s === "number") {
            return new Vector3(v.x * s, v.y * s, v.z * s);
        }
        return new Vector3(v.x * s.x, v.y * s.y, v.z * s.z);
    }

    static Dot(a: Vector3, b: Vector3): number {
        return a.x * b.x + a.y * b.y + a.z * b.z;
    }

    static Cross(a: Vector3, b: Vector3): Vector3 {
        return new Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x
        );
    }

    static Clamp(value: Vector3, min: Vector3, max: Vector3): Vector3 {
        return new Vector3(
            Math.max(min.x, Math.min(value.x, max.x)),
            Math.max(min.y, Math.min(value.y, max.y)),
            Math.max(min.z, Math.min(value.z, max.z))
        );
    }

export class Vector4 {
    constructor(public x: number, public y: number, public z: number, public w: number) {}
    static get Zero(): Vector4 { return new Vector4(0, 0, 0, 0); }
    static get One(): Vector4 { return new Vector4(1, 1, 1, 1); }
}

export class MathQuaternion {
    constructor(public x: number, public y: number, public z: number, public w: number) {}
    static get Identity(): MathQuaternion { return new MathQuaternion(0, 0, 0, 1); }

    static FromAxisAngle(axis: Vector3, angle: number): MathQuaternion {
        const halfAngle = angle / 2;
        const sin = Math.sin(halfAngle);
        return new MathQuaternion(
            axis.x * sin,
            axis.y * sin,
            axis.z * sin,
            Math.cos(halfAngle)
        );
    }

    static FromEulerXYZ(euler: Vector3): MathQuaternion {
        const cx = Math.cos(euler.x * 0.5);
        const sx = Math.sin(euler.x * 0.5);
        const cy = Math.cos(euler.y * 0.5);
        const sy = Math.sin(euler.y * 0.5);
        const cz = Math.cos(euler.z * 0.5);
        const sz = Math.sin(euler.z * 0.5);
        return new MathQuaternion(
            sx * cy * cz + cx * sy * sz,
            cx * sy * cz - sx * cy * sz,
            cx * cy * sz + sx * sy * cz,
            cx * cy * cz - sx * sy * sz
        );
    }

    static Dot(a: MathQuaternion, b: MathQuaternion): number {
        return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
    }

    static Multiply(a: MathQuaternion, b: MathQuaternion): MathQuaternion {
        const ax = a.x, ay = a.y, az = a.z, aw = a.w;
        const bx = b.x, by = b.y, bz = b.z, bw = b.w;
        return new MathQuaternion(
            aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz
        );
    }

    static Slerp(a: MathQuaternion, b: MathQuaternion, t: number): MathQuaternion {
        let cos = MathQuaternion.Dot(a, b);
        let flag = false;
        if (cos < 0.0) {
            cos = -cos;
            flag = true;
        }
        if (cos > 0.9995) {
            const inv = 1.0 - t;
            const tx = inv * a.x + t * b.x;
            const ty = inv * a.y + t * b.y;
            const tz = inv * a.z + t * b.z;
            const tw = inv * a.w + t * b.w;
            return new MathQuaternion(tx, ty, tz, tw).Normalized();
        }
        const angle = Math.acos(cos);
        const sin = Math.sin(angle);
        const t0 = Math.sin((1.0 - t) * angle) / sin;
        const t1 = Math.sin(t * angle) / sin;
        const tx = a.x * t0 + (flag ? -b.x : b.x) * t1;
        const ty = a.y * t0 + (flag ? -b.y : b.y) * t1;
        const tz = a.z * t0 + (flag ? -b.z : b.z) * t1;
        const tw = a.w * t0 + (flag ? -b.w : b.w) * t1;
        return new MathQuaternion(tx, ty, tz, tw).Normalized();
    }

    get LengthSquared(): number {
        return this.x * this.x + this.y * this.y + this.z * this.z + this.w * this.w;
    }

    Normalized(): MathQuaternion {
        const len = Math.sqrt(this.LengthSquared);
        if (len === 0) return MathQuaternion.Identity;
        return new MathQuaternion(this.x / len, this.y / len, this.z / len, this.w / len);
    }
}

export class Matrix4 {
    constructor(public m: Float32Array) {}

    static get Identity(): Matrix4 {
        const id = new Float32Array([
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        ]);
        return new Matrix4(id);
    }

    static CreateScale(scale: Vector3): Matrix4 {
        const m = new Float32Array(16);
        m[0] = scale.x;
        m[5] = scale.y;
        m[10] = scale.z;
        m[15] = 1;
        return new Matrix4(m);
    }

    static CreateTranslation(translation: Vector3): Matrix4 {
        const m = new Float32Array(16);
        m[0] = m[5] = m[10] = m[15] = 1;
        m[12] = translation.x;
        m[13] = translation.y;
        m[14] = translation.z;
        return new Matrix4(m);
    }

    static CreateFromQuaternion(q: MathQuaternion): Matrix4 {
        const x = q.x, y = q.y, z = q.z, w = q.w;
        const x2 = x + x, y2 = y + y, z2 = z + z;
        const xx = x * x2, xy = x * y2, xz = x * z2;
        const yy = y * y2, yz = y * z2, zz = z * z2;
        const wx = w * x2, wy = w * y2, wz = w * z2;
        const m = new Float32Array(16);
        m[0] = 1 - (yy + zz); m[4] = xy - wz; m[8] = xz + wy;
        m[1] = xy + wz; m[5] = 1 - (xx + zz); m[9] = yz - wx;
        m[2] = xz - wy; m[6] = yz + wx; m[10] = 1 - (xx + yy);
        m[15] = 1;
        return new Matrix4(m);
    }

    static Multiply(a: Matrix4, b: Matrix4): Matrix4 {
        const am = a.m, bm = b.m;
        const dm = new Float32Array(16);
        for (let row = 0; row < 4; ++row) {
            for (let col = 0; col < 4; ++col) {
                dm[row * 4 + col] = 0;
                for (let i = 0; i < 4; ++i) {
                    dm[row * 4 + col] += am[row * 4 + i] * bm[i * 4 + col];
                }
            }
        }
        return new Matrix4(dm);
    }

    static Invert(matrix: Matrix4): Matrix4 {
        const m = matrix.m;
        const r00 = m[0], r01 = m[4], r02 = m[8], r03 = m[12];
        const r10 = m[1], r11 = m[5], r12 = m[9], r13 = m[13];
        const r20 = m[2], r21 = m[6], r22 = m[10], r23 = m[14];
        const det = r00 * (r11 * r22 - r12 * r21) +
                    r01 * (r12 * r20 - r10 * r22) +
                    r02 * (r10 * r21 - r11 * r20);
        if (Math.abs(det) < 1e-10) {
            return Matrix4.Identity; // or throw
        }
        const idet = 1 / det;
        const dm = new Float32Array(16);
        dm[0] = (r11 * r22 - r12 * r21) * idet;
        dm[4] = (r02 * r21 - r01 * r22) * idet;
        dm[8] = (r01 * r12 - r02 * r11) * idet;
        dm[1] = (r12 * r20 - r10 * r22) * idet;
        dm[5] = (r00 * r22 - r02 * r20) * idet;
        dm[9] = (r02 * r10 - r00 * r12) * idet;
        dm[2] = (r10 * r21 - r11 * r20) * idet;
        dm[6] = (r01 * r20 - r00 * r21) * idet;
        dm[10] = (r00 * r11 - r01 * r10) * idet;
        dm[12] = - (dm[0] * r03 + dm[4] * r13 + dm[8] * r23);
        dm[13] = - (dm[1] * r03 + dm[5] * r13 + dm[9] * r23);
        dm[14] = - (dm[2] * r03 + dm[6] * r13 + dm[10] * r23);
        dm[3] = dm[7] = dm[11] = 0;
        dm[15] = 1;
        return new Matrix4(dm);
    }

    static Transpose(m: Matrix4): Matrix4 {
        const src = m.m;
        const dm = new Float32Array(16);
        dm[0] = src[0]; dm[1] = src[4]; dm[2] = src[8]; dm[3] = src[12];
        dm[4] = src[1]; dm[5] = src[5]; dm[6] = src[9]; dm[7] = src[13];
        dm[8] = src[2]; dm[9] = src[6]; dm[10] = src[10]; dm[11] = src[14];
        dm[12] = src[3]; dm[13] = src[7]; dm[14] = src[11]; dm[15] = src[15];
        return new Matrix4(dm);
    }

    column(index: number): Vector4 {
        const i = index * 4;
        return new Vector4(this.m[i], this.m[i + 1], this.m[i + 2], this.m[i + 3]);
    }

    row(index: number): Vector4 {
        return new Vector4(this.m[index], this.m[index + 4], this.m[index + 8], this.m[index + 12]);
    }

    toString(): string {
        return Array.from(this.m.slice(0, 16), (v) => v.toFixed(6)).join(" ");
    }
}
