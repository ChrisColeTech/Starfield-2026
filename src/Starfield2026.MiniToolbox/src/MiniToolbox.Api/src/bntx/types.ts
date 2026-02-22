import { BinaryDataReader } from './BinaryDataReader.js';

/**
 * Interface for resources that can be loaded from a BNTX file.
 */
export interface IResData {
    load(reader: BinaryDataReader): void;
}

/**
 * Common data types and interfaces
 */
export interface BntxHeader {
    magic: string; // 'BNTX'
    version: number;
    byteOrder: number;
    alignment: number;
    targetAddressSize: number;
    fileNameOffset: number;
    relocationTableOffset: number;
    dataBlockOffset: number;
}
