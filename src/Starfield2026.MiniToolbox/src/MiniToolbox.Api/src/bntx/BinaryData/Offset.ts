import type { BinaryDataWriter } from './BinaryDataWriter.js';

/**
 * 1:1 port of Offset.cs
 * Reserves space for an offset value to be filled in later (write-only).
 */
export class Offset {
    readonly writer: BinaryDataWriter;
    readonly position: number;

    constructor(writer: BinaryDataWriter) {
        this.writer = writer;
        this.position = writer.position;
        writer.position += 4; // Reserve 4 bytes
    }

    satisfy(value?: number): void {
        const current = this.writer.position;
        this.writer.position = this.position;
        this.writer.writeInt32(value ?? this.writer.position);
        this.writer.position = current;
    }
}
