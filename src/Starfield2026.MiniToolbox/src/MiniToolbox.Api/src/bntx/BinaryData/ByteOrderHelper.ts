import { ByteOrder } from './ByteOrder.js';

/**
 * 1:1 port of ByteOrderHelper.cs
 * Detects the system's native byte order.
 */
export class ByteOrderHelper {
    private static _systemByteOrder: ByteOrder | null = null;

    static get systemByteOrder(): ByteOrder {
        if (this._systemByteOrder === null) {
            // Node.js is always little-endian on x86/x64/ARM
            const buf = new ArrayBuffer(2);
            new DataView(buf).setInt16(0, 256, true);
            this._systemByteOrder = new Int16Array(buf)[0] === 256
                ? ByteOrder.LittleEndian
                : ByteOrder.BigEndian;
        }
        return this._systemByteOrder;
    }
}
