import { PackedQuaternion } from '../flatbuffers/Common/Math.js';

export interface Quaternion {
    X: number;
    Y: number;
    Z: number;
    W: number;
}

export class QuaternionExtensions {
    static readonly PI_DIVISOR: number = Math.PI / 65536.0;
    static readonly PI_ADDEND: number = Math.PI / 4.0;
    private static readonly PI_HALF: number = Math.PI / 2.0;
    private static readonly SCALE: number = 0x7FFF;

    private static ExpandFloat(i: number): number {
        return i * (QuaternionExtensions.PI_HALF / QuaternionExtensions.SCALE) - QuaternionExtensions.PI_ADDEND;
    }

    private static QuantizeFloat(f: number): number {
        const result = (f + QuaternionExtensions.PI_ADDEND) / QuaternionExtensions.PI_DIVISOR;
        return Math.floor(result) & 0x7FFF;
    }

    static Unpack(pq: PackedQuaternion): Quaternion {
        const pack = (BigInt(pq.Z) << BigInt(32)) | (BigInt(pq.Y) << BigInt(16)) | BigInt(pq.X);
        const packNum = Number(pack);

        const q1 = QuaternionExtensions.ExpandFloat((packNum >> 3) & 0x7FFF);
        const q2 = QuaternionExtensions.ExpandFloat((packNum >> 18) & 0x7FFF);
        const q3 = QuaternionExtensions.ExpandFloat((packNum >> 33) & 0x7FFF);

        let sum = q1 * q1 + q2 * q2 + q3 * q3;
        let maxComponent = 1.0 - sum;
        if (maxComponent < 0.0) {
            maxComponent = 0.0;
        }
        maxComponent = Math.sqrt(maxComponent);

        const missingComponent = packNum & 0x3;
        const isNegative = (packNum & 0x4) !== 0;

        const values: number[] = [q1, q2, q3];
        values.splice(missingComponent, 0, maxComponent);

        let w = values[3];
        let x = values[0];
        let y = values[1];
        let z = values[2];

        if (isNegative) {
            x = -x;
            y = -y;
            z = -z;
            w = -w;
        }

        return { X: x, Y: y, Z: z, W: w };
    }

    static Pack(q: Quaternion): PackedQuaternion {
        q = QuaternionExtensions.Normalize(q);

        let qList: number[] = [q.W, q.X, q.Y, q.Z];
        let maxVal = Math.max(...qList);
        let minVal = Math.min(...qList);
        let isNegative = 0;
        if (Math.abs(minVal) > maxVal) {
            maxVal = minVal;
            isNegative = 1;
        }

        let maxIndex = qList.indexOf(maxVal);
        if (isNegative === 1) {
            for (let i = 0; i < qList.length; i++) {
                qList[i] = -qList[i];
            }
        }

        let tx: number;
        let ty: number;
        let tz: number;

        switch (maxIndex) {
            case 0:
                tx = QuaternionExtensions.QuantizeFloat(qList[1]);
                ty = QuaternionExtensions.QuantizeFloat(qList[2]);
                tz = QuaternionExtensions.QuantizeFloat(qList[3]);
                break;
            case 1:
                tx = QuaternionExtensions.QuantizeFloat(qList[2]);
                ty = QuaternionExtensions.QuantizeFloat(qList[3]);
                tz = QuaternionExtensions.QuantizeFloat(qList[0]);
                break;
            case 2:
                tx = QuaternionExtensions.QuantizeFloat(qList[1]);
                ty = QuaternionExtensions.QuantizeFloat(qList[3]);
                tz = QuaternionExtensions.QuantizeFloat(qList[0]);
                break;
            default:
                tx = QuaternionExtensions.QuantizeFloat(qList[1]);
                ty = QuaternionExtensions.QuantizeFloat(qList[2]);
                tz = QuaternionExtensions.QuantizeFloat(qList[0]);
                break;
        }

        let pack = (BigInt(tz) << BigInt(30)) | (BigInt(ty) << BigInt(15)) | BigInt(tx);
        pack = (pack << BigInt(3)) | (BigInt(isNegative << 2) | BigInt(maxIndex));

        let x = Number(pack & BigInt(0xFFFF));
        let y = Number((pack >> BigInt(16)) & BigInt(0xFFFF));
        let z = Number((pack >> BigInt(32)) & BigInt(0xFFFF));

        if (maxIndex === 0) {
            x = Math.min(65535, x + 3);
        } else if (x > 0) {
            x -= 1;
        }

        return new PackedQuaternion(x, y, z);
    }

    private static Normalize(q: Quaternion): Quaternion {
        const magnitude = Math.sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
        if (magnitude === 0) {
            return { X: 0, Y: 0, Z: 0, W: 1 };
        }
        return {
            X: q.X / magnitude,
            Y: q.Y / magnitude,
            Z: q.Z / magnitude,
            W: q.W / magnitude
        };
    }

    static ToDictionary(quaternion: Quaternion): Record<string, number> {
        return {
            "W": quaternion.W,
            "X": quaternion.X,
            "Y": quaternion.Y,
            "Z": quaternion.Z
        };
    }
}
