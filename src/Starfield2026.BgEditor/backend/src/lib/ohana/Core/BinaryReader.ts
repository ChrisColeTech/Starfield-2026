import * as fs from 'fs';

/**
 * Binary reader that wraps a Node.js Buffer and provides sequential
 * binary reading, matching the C# BinaryReader + Stream API surface
 * used throughout the Ohana codebase.
 *
 * All multi-byte reads are little-endian unless noted otherwise.
 */
export class BinaryReader {
  private buf: Buffer;
  private pos: number;

  constructor(buf: Buffer) {
    this.buf = buf;
    this.pos = 0;
  }

  /** Create a BinaryReader by reading an entire file into memory. */
  static fromFile(filePath: string): BinaryReader {
    const buf = fs.readFileSync(filePath);
    return new BinaryReader(buf);
  }

  /** Create a BinaryReader from an existing Buffer. */
  static fromBuffer(buf: Buffer): BinaryReader {
    return new BinaryReader(buf);
  }

  // --------------- position / length ---------------

  /** Current read position (absolute byte offset). */
  get position(): number {
    return this.pos;
  }

  /** Total length of the underlying buffer. */
  get length(): number {
    return this.buf.length;
  }

  // --------------- seeking ---------------

  /** Seek to an absolute byte offset. */
  seek(offset: number): void {
    this.pos = offset;
  }

  /** Seek relative to the current position (positive = forward). */
  seekRelative(offset: number): void {
    this.pos += offset;
  }

  // --------------- primitive reads ---------------

  /** Read an unsigned byte (0..255) and advance by 1. */
  readByte(): number {
    const v = this.buf.readUInt8(this.pos);
    this.pos += 1;
    return v;
  }

  /** Read a signed byte (-128..127) and advance by 1. */
  readSByte(): number {
    const v = this.buf.readInt8(this.pos);
    this.pos += 1;
    return v;
  }

  /** Read an unsigned 16-bit integer (little-endian) and advance by 2. */
  readUInt16(): number {
    const v = this.buf.readUInt16LE(this.pos);
    this.pos += 2;
    return v;
  }

  /** Read a signed 16-bit integer (little-endian) and advance by 2. */
  readInt16(): number {
    const v = this.buf.readInt16LE(this.pos);
    this.pos += 2;
    return v;
  }

  /** Read an unsigned 32-bit integer (little-endian) and advance by 4. */
  readUInt32(): number {
    const v = this.buf.readUInt32LE(this.pos);
    this.pos += 4;
    return v;
  }

  /** Read a signed 32-bit integer (little-endian) and advance by 4. */
  readInt32(): number {
    const v = this.buf.readInt32LE(this.pos);
    this.pos += 4;
    return v;
  }

  /** Read a 32-bit IEEE 754 float (little-endian) and advance by 4. */
  readFloat(): number {
    const v = this.buf.readFloatLE(this.pos);
    this.pos += 4;
    return v;
  }

  /** Read a single byte and return true if non-zero. */
  readBoolean(): boolean {
    return this.readByte() !== 0;
  }

  /** Read `count` bytes into a new Buffer and advance the position. */
  readBytes(count: number): Buffer {
    const slice = Buffer.from(this.buf.subarray(this.pos, this.pos + count));
    this.pos += count;
    return slice;
  }

  // --------------- string reads ---------------

  /**
   * Read a null-terminated ASCII string starting at an absolute address.
   * By default the reader position is restored after reading.
   * If `advancePosition` is true, the position is left after the null terminator.
   */
  readStringAt(address: number, advancePosition: boolean = false): string {
    const originalPos = this.pos;
    this.pos = address;
    const bytes: number[] = [];
    for (;;) {
      const b = this.readByte();
      if (b === 0) break;
      bytes.push(b);
    }
    if (!advancePosition) {
      this.pos = originalPos;
    }
    return Buffer.from(bytes).toString('ascii');
  }

  /**
   * Read an ASCII string of fixed byte length from the current position.
   * Stops early if a null terminator (0x00) is encountered.
   * Always advances the position by `count` bytes.
   */
  readStringWithLength(count: number): string {
    const bytes: number[] = [];
    const end = this.pos + count;
    for (let i = 0; i < count; i++) {
      const b = this.buf.readUInt8(this.pos + i);
      if (b === 0) break;
      bytes.push(b);
    }
    this.pos = end;
    return Buffer.from(bytes).toString('ascii');
  }

  /**
   * Read `length` bytes from the current position and return them as an
   * ASCII string (useful for reading magic / signature bytes).
   */
  readMagic(length: number): string {
    const s = this.buf.toString('ascii', this.pos, this.pos + length);
    this.pos += length;
    return s;
  }

  // --------------- slicing ---------------

  /**
   * Return a sub-view of the underlying buffer without copying.
   * Useful for handing off to sub-parsers.
   */
  slice(offset: number, length: number): Buffer {
    return this.buf.subarray(offset, offset + length);
  }
}
