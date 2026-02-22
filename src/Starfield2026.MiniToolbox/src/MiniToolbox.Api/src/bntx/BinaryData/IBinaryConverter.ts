import type { BinaryDataReader } from './BinaryDataReader.js';
import type { BinaryDataWriter } from './BinaryDataWriter.js';
import type { BinaryMemberAttribute } from './BinaryMemberAttribute.js';

/**
 * 1:1 port of IBinaryConverter.cs
 * Interface for custom binary converters.
 */
export interface IBinaryConverter {
    read(reader: BinaryDataReader, instance: any, memberAttribute: BinaryMemberAttribute): any;
    write(writer: BinaryDataWriter, instance: any, memberAttribute: BinaryMemberAttribute, value: any): void;
}
