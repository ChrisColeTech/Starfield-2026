import { BinaryReader } from './BinaryReader.js';

/**
 * I/O utility helpers ported from OhanaCli.Formats.IOUtils (C#).
 */
export class IOUtils {

  /**
   * Read a null-terminated ASCII string from a given address.
   * Does NOT advance the reader position unless `advancePosition` is true.
   */
  static readString(input: BinaryReader, address: number, advancePosition: boolean = false): string {
    return input.readStringAt(address, advancePosition);
  }

  /**
   * Read an ASCII string of a fixed byte length from a given address.
   * Stops early on null terminator. Advances position to address + count.
   */
  static readStringAt(input: BinaryReader, address: number, count: number): string {
    input.seek(address);
    const bytes: number[] = [];
    for (let i = 0; i < count; i++) {
      const b = input.readByte();
      if (b === 0) break;
      bytes.push(b);
    }
    // Always advance to address + count (matching C# BinaryReader.ReadBytes behavior)
    input.seek(address + count);
    return Buffer.from(bytes).toString('ascii');
  }

  /**
   * Read an ASCII string of a fixed byte length from the current position.
   * Stops early on null terminator. Advances position by `count`.
   */
  static readStringWithLength(input: BinaryReader, count: number): string {
    return IOUtils.readStringAt(input, input.position, count);
  }

  /**
   * Sign-extend a value from `bits` width to a full 32-bit signed integer.
   * Works for both unsigned and signed input values.
   */
  static signExtend(value: number, bits: number): number {
    const sign = (value & (1 << (bits - 1))) > 0;
    if (sign) value -= (1 << bits);
    return value;
  }

  /**
   * Swap endianness of a 32-bit unsigned value (little <-> big).
   */
  static endianSwap(value: number): number {
    return (
      (value >>> 24) |
      ((value >>> 8) & 0xff00) |
      ((value & 0xff00) << 8) |
      ((value & 0xff) << 24)
    ) >>> 0;
  }
}
