/**
 * FlatBuffer binary deserializer with schema registry.
 * Mirrors C# FlatSharp [FlatBufferTable]/[FlatBufferStruct]/[FlatBufferItem(N)] attributes.
 * Uses the `flatbuffers` npm package's ByteBuffer for low-level binary access.
 */
import * as fs from 'fs';
import { ByteBuffer } from 'flatbuffers';

// ── Imports for schema registration ──
import {
    Vector2f, Vector3f, Vector4f, Vector2i, Sphere,
    TRBoundingBox, PackedQuaternion, RGBA, Transform,
} from '../Flatbuffers/Common/Math.js';
import {
    CustomFileDescriptor, FileDescriptor, FileInfo, PackInfo,
} from '../Flatbuffers/TR/ResourceDictionary/FileDescriptor.js';
import { FileSystem } from '../Flatbuffers/TR/ResourceDictionary/FileSystem.js';
import { PackedArchive, PackedFile } from '../Flatbuffers/TR/ResourceDictionary/PackedArchive.js';
import {
    TRMDL, ModelMesh, ModelSkeleton, ModelLOD, ModelLODEntry,
} from '../Flatbuffers/TR/Model/TRMDL.js';
import {
    TRMSH, TRMesh, TRMeshPart, TRVertexDeclaration, TRVertexElement, TRVertexElementSize, TRBoneWeight,
} from '../Flatbuffers/TR/Model/TRMSH.js';
import { TRMBF, TRModelBuffer, TRBuffer, TRMorphTarget } from '../Flatbuffers/TR/Model/TRMBF.js';
import {
    TRMTR, TRMaterial, TRTexture, TRSampler, TRMaterialShader,
    TRFloatParameter, TRVec2fParameter, TRVec3fParameter, TRVec4fParameter, TRStringParameter,
} from '../Flatbuffers/TR/Model/TRMTR.js';
import {
    TRSKL, TRTransformNode, TRJointInfo, TRHelperBoneInfo, Matrix4x3f, SRT,
} from '../Flatbuffers/TR/Model/TRSKL.js';

// ── Schema types ──

type ScalarType = 'int8' | 'uint8' | 'int16' | 'uint16' | 'int32' | 'uint32' | 'float32'
    | 'uint64' | 'bool';

interface StructSchema {
    kind: 'struct';
    size: number;
    fields: { name: string; type: ScalarType | { struct: Function }; size: number }[];
}

interface TableSchema {
    kind: 'table';
    ctor: Function;
    fields: TableFieldDef[];
}

interface TableFieldDef {
    name: string;
    index: number; // [FlatBufferItem(N)]
    type: FieldType;
}

type FieldType =
    | ScalarType
    | 'string'
    | 'bytes'
    | { table: Function }
    | { struct: Function }
    | { vectorOf: ScalarType | 'string' | { table: Function } | { struct: Function } }
    | { union: Function[] };     // FlatBufferUnion — index N = type, N+1 = value

type Schema = StructSchema | TableSchema;

// ── Schema registry ──
const schemaMap = new Map<Function, Schema>();

function registerStruct(ctor: Function, size: number, fields: StructSchema['fields']) {
    schemaMap.set(ctor, { kind: 'struct', size, fields });
}

function registerTable(ctor: Function, fields: TableFieldDef[]) {
    schemaMap.set(ctor, { kind: 'table', ctor, fields });
}

// ── Struct schemas (inline, fixed‑size, no vtable) ──

registerStruct(Vector2f, 8, [
    { name: 'X', type: 'float32', size: 4 },
    { name: 'Y', type: 'float32', size: 4 },
]);
registerStruct(Vector3f, 12, [
    { name: 'X', type: 'float32', size: 4 },
    { name: 'Y', type: 'float32', size: 4 },
    { name: 'Z', type: 'float32', size: 4 },
]);
registerStruct(Vector4f, 16, [
    { name: 'W', type: 'float32', size: 4 },
    { name: 'X', type: 'float32', size: 4 },
    { name: 'Y', type: 'float32', size: 4 },
    { name: 'Z', type: 'float32', size: 4 },
]);
registerStruct(Vector2i, 8, [
    { name: 'X', type: 'int32', size: 4 },
    { name: 'Y', type: 'int32', size: 4 },
]);
registerStruct(Sphere, 16, [
    { name: 'X', type: 'float32', size: 4 },
    { name: 'Y', type: 'float32', size: 4 },
    { name: 'Z', type: 'float32', size: 4 },
    { name: 'Radius', type: 'float32', size: 4 },
]);
registerStruct(PackedQuaternion, 6, [
    { name: 'X', type: 'uint16', size: 2 },
    { name: 'Y', type: 'uint16', size: 2 },
    { name: 'Z', type: 'uint16', size: 2 },
]);
registerStruct(RGBA, 16, [
    { name: 'R', type: 'float32', size: 4 },
    { name: 'G', type: 'float32', size: 4 },
    { name: 'B', type: 'float32', size: 4 },
    { name: 'A', type: 'float32', size: 4 },
]);
registerStruct(Transform, 40, [
    { name: 'Scale', type: { struct: Vector3f }, size: 12 },
    { name: 'Rotate', type: { struct: Vector4f }, size: 16 },
    { name: 'Translate', type: { struct: Vector3f }, size: 12 },
]);

// ── Table schemas (vtable‑based) ──

// ResourceDictionary
registerTable(FileInfo, [
    { name: 'PackIndex', index: 0, type: 'uint64' },
    { name: 'UnusedTable', index: 1, type: 'uint32' },
]);
registerTable(PackInfo, [
    { name: 'FileSize', index: 0, type: 'uint64' },
    { name: 'FileCount', index: 1, type: 'uint64' },
]);
registerTable(FileDescriptor, [
    { name: 'FileHashes', index: 0, type: { vectorOf: 'uint64' } },
    { name: 'PackNames', index: 1, type: { vectorOf: 'string' } },
    { name: 'FileInfo', index: 2, type: { vectorOf: { table: FileInfo } } },
    { name: 'PackInfo', index: 3, type: { vectorOf: { table: PackInfo } } },
]);
registerTable(CustomFileDescriptor, [
    { name: 'FileHashes', index: 0, type: { vectorOf: 'uint64' } },
    { name: 'PackNames', index: 1, type: { vectorOf: 'string' } },
    { name: 'FileInfo', index: 2, type: { vectorOf: { table: FileInfo } } },
    { name: 'PackInfo', index: 3, type: { vectorOf: { table: PackInfo } } },
    { name: 'UnusedHashes', index: 4, type: { vectorOf: 'uint64' } },
    { name: 'UnusedFileInfo', index: 5, type: { vectorOf: { table: FileInfo } } },
]);
registerTable(FileSystem, [
    { name: 'FileHashes', index: 0, type: { vectorOf: 'uint64' } },
    { name: 'FileOffsets', index: 1, type: { vectorOf: 'uint64' } },
]);
registerTable(PackedFile, [
    { name: 'Field_00', index: 0, type: 'uint32' },
    { name: 'EncryptionType', index: 1, type: 'int8' },
    { name: 'Level', index: 2, type: 'uint8' },
    { name: 'FileSize', index: 3, type: 'uint64' },
    { name: 'FileBuffer', index: 4, type: 'bytes' },
]);
registerTable(PackedArchive, [
    { name: 'FileHashes', index: 0, type: { vectorOf: 'uint64' } },
    { name: 'FileEntry', index: 1, type: { vectorOf: { table: PackedFile } } },
]);

// TRMDL
registerTable(ModelMesh, [{ name: 'PathName', index: 0, type: 'string' }]);
registerTable(ModelSkeleton, [{ name: 'PathName', index: 0, type: 'string' }]);
registerTable(ModelLODEntry, [{ name: 'Index', index: 0, type: 'int32' }]);
registerTable(ModelLOD, [
    { name: 'Entries', index: 0, type: { vectorOf: { table: ModelLODEntry } } },
    { name: 'Type', index: 1, type: 'string' },
]);
registerTable(TRMDL, [
    { name: 'Field_00', index: 0, type: 'int32' },
    { name: 'Meshes', index: 1, type: { vectorOf: { table: ModelMesh } } },
    { name: 'Skeleton', index: 2, type: { table: ModelSkeleton } },
    { name: 'Materials', index: 3, type: { vectorOf: 'string' } },
    { name: 'LODs', index: 4, type: { vectorOf: { table: ModelLOD } } },
    { name: 'Bounds', index: 5, type: { table: TRBoundingBox } },
    { name: 'Field_06', index: 6, type: { struct: Vector4f } },
]);
registerTable(TRBoundingBox, [
    { name: 'MinBound', index: 0, type: { struct: Vector3f } },
    { name: 'MaxBound', index: 1, type: { struct: Vector3f } },
]);

// TRMSH
registerTable(TRVertexElement, [
    { name: 'vertexElementSizeIndex', index: 0, type: 'int32' },
    { name: 'vertexUsage', index: 1, type: 'int32' },
    { name: 'vertexElementLayer', index: 2, type: 'int32' },
    { name: 'vertexFormat', index: 3, type: 'int32' },
    { name: 'vertexElementOffset', index: 4, type: 'int32' },
]);
registerTable(TRVertexElementSize, [
    { name: 'elementSize', index: 0, type: 'int32' },
]);
registerTable(TRVertexDeclaration, [
    { name: 'vertexElements', index: 0, type: { vectorOf: { table: TRVertexElement } } },
    { name: 'vertexElementSizes', index: 1, type: { vectorOf: { table: TRVertexElementSize } } },
]);
registerTable(TRBoneWeight, [
    { name: 'RigIndex', index: 0, type: 'int32' },
    { name: 'RigWeight', index: 1, type: 'float32' },
]);
registerTable(TRMeshPart, [
    { name: 'indexCount', index: 0, type: 'int32' },
    { name: 'indexOffset', index: 1, type: 'int32' },
    { name: 'Field_02', index: 2, type: 'int32' },
    { name: 'MaterialName', index: 3, type: 'string' },
    { name: 'vertexDeclarationIndex', index: 4, type: 'int32' },
]);
registerTable(TRMesh, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'boundingBox', index: 1, type: { table: TRBoundingBox } },
    { name: 'IndexType', index: 2, type: 'int32' },
    { name: 'vertexDeclaration', index: 3, type: { vectorOf: { table: TRVertexDeclaration } } },
    { name: 'meshParts', index: 4, type: { vectorOf: { table: TRMeshPart } } },
    { name: 'Field_05', index: 5, type: 'int32' },
    { name: 'Field_06', index: 6, type: 'int32' },
    { name: 'Field_07', index: 7, type: 'int32' },
    { name: 'Field_08', index: 8, type: 'int32' },
    { name: 'clipSphere', index: 9, type: { struct: Sphere } },
    { name: 'boneWeight', index: 10, type: { vectorOf: { table: TRBoneWeight } } },
    { name: 'Field_11', index: 11, type: 'string' },
    { name: 'Field_12', index: 12, type: 'string' },
]);
registerTable(TRMSH, [
    { name: 'Version', index: 0, type: 'int32' },
    { name: 'Meshes', index: 1, type: { vectorOf: { table: TRMesh } } },
    { name: 'bufferFilePath', index: 2, type: 'string' },
]);

// TRMBF
registerTable(TRBuffer, [
    { name: 'Bytes', index: 0, type: 'bytes' },
]);
registerTable(TRMorphTarget, [
    { name: 'morphBuffers', index: 0, type: { vectorOf: { table: TRBuffer } } },
]);
registerTable(TRModelBuffer, [
    { name: 'IndexBuffer', index: 0, type: { vectorOf: { table: TRBuffer } } },
    { name: 'VertexBuffer', index: 1, type: { vectorOf: { table: TRBuffer } } },
    { name: 'MorphTargets', index: 2, type: { vectorOf: { table: TRMorphTarget } } },
]);
registerTable(TRMBF, [
    { name: 'Field_00', index: 0, type: 'int32' },
    { name: 'TRMeshBuffers', index: 1, type: { vectorOf: { table: TRModelBuffer } } },
]);

// TRMTR
registerTable(TRFloatParameter, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'Value', index: 1, type: 'float32' },
]);
registerTable(TRVec2fParameter, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'Value', index: 1, type: { struct: Vector2f } },
]);
registerTable(TRVec3fParameter, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'Value', index: 1, type: { struct: Vector3f } },
]);
registerTable(TRVec4fParameter, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'Value', index: 1, type: { struct: Vector4f } },
]);
registerTable(TRStringParameter, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'Value', index: 1, type: 'string' },
]);
registerTable(TRTexture, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'File', index: 1, type: 'string' },
    { name: 'Slot', index: 2, type: 'uint32' },
]);
registerTable(TRSampler, [
    { name: 'State0', index: 0, type: 'uint32' },
    { name: 'State1', index: 1, type: 'uint32' },
    { name: 'State2', index: 2, type: 'uint32' },
    { name: 'State3', index: 3, type: 'uint32' },
    { name: 'State4', index: 4, type: 'uint32' },
    { name: 'State5', index: 5, type: 'uint32' },
    { name: 'State6', index: 6, type: 'uint32' },
    { name: 'State7', index: 7, type: 'uint32' },
    { name: 'State8', index: 8, type: 'uint32' },
    { name: 'RepeatU', index: 9, type: 'uint32' },
    { name: 'RepeatV', index: 10, type: 'uint32' },
    { name: 'RepeatW', index: 11, type: 'uint32' },
    { name: 'BorderColor', index: 12, type: { struct: RGBA } },
]);
registerTable(TRMaterialShader, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'Values', index: 1, type: { vectorOf: { table: TRStringParameter } } },
]);
registerTable(TRMaterial, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'Shader', index: 1, type: { vectorOf: { table: TRMaterialShader } } },
    { name: 'Textures', index: 2, type: { vectorOf: { table: TRTexture } } },
    { name: 'Samplers', index: 3, type: { vectorOf: { table: TRSampler } } },
    { name: 'FloatParams', index: 4, type: { vectorOf: { table: TRFloatParameter } } },
    { name: 'Vec2fParams', index: 5, type: { vectorOf: { table: TRVec2fParameter } } },
    { name: 'Vec3fParams', index: 6, type: { vectorOf: { table: TRVec3fParameter } } },
    { name: 'Vec4fParams', index: 7, type: { vectorOf: { table: TRVec4fParameter } } },
]);
registerTable(TRMTR, [
    { name: 'Field_00', index: 0, type: 'int32' },
    { name: 'Materials', index: 1, type: { vectorOf: { table: TRMaterial } } },
]);

// TRSKL
registerTable(SRT, [
    { name: 'Scale', index: 0, type: { struct: Vector3f } },
    { name: 'Rotate', index: 1, type: { struct: Vector3f } },
    { name: 'Translate', index: 2, type: { struct: Vector3f } },
]);
registerTable(Matrix4x3f, [
    { name: 'AxisX', index: 0, type: { struct: Vector3f } },
    { name: 'AxisY', index: 1, type: { struct: Vector3f } },
    { name: 'AxisZ', index: 2, type: { struct: Vector3f } },
    { name: 'AxisW', index: 3, type: { struct: Vector3f } },
]);
registerTable(TRTransformNode, [
    { name: 'Name', index: 0, type: 'string' },
    { name: 'Transform', index: 1, type: { table: SRT } },
    { name: 'ScalePivot', index: 2, type: { struct: Vector3f } },
    { name: 'RotatePivot', index: 3, type: { struct: Vector3f } },
    { name: 'ParentNodeIndex', index: 4, type: 'int32' },
    { name: 'JointInfoIndex', index: 5, type: 'int32' },
    { name: 'ParentNodeName', index: 6, type: 'string' },
    { name: 'Priority', index: 7, type: 'uint32' },
    { name: 'PriorityPass', index: 8, type: 'bool' },
    { name: 'IgnoreParentRotation', index: 9, type: 'bool' },
]);
registerTable(TRJointInfo, [
    { name: 'SegmentScaleCompensate', index: 0, type: 'bool' },
    { name: 'InfluenceSkinning', index: 1, type: 'bool' },
    { name: 'InverseBindPoseMatrix', index: 2, type: { table: Matrix4x3f } },
]);
registerTable(TRHelperBoneInfo, [
    { name: 'Output', index: 0, type: 'string' },
    { name: 'Target', index: 1, type: 'string' },
    { name: 'Reference', index: 2, type: 'string' },
    { name: 'Type', index: 3, type: 'string' },
    { name: 'UpType', index: 4, type: 'string' },
    { name: 'Weight', index: 5, type: { struct: Vector3f } },
    { name: 'Adjust', index: 6, type: { struct: Vector4f } },
]);
registerTable(TRSKL, [
    { name: 'Version', index: 0, type: 'uint32' },
    { name: 'TransformNodes', index: 1, type: { vectorOf: { table: TRTransformNode } } },
    { name: 'JointInfos', index: 2, type: { vectorOf: { table: TRJointInfo } } },
    { name: 'HelperBones', index: 3, type: { vectorOf: { table: TRHelperBoneInfo } } },
    { name: 'SkinningPaletteOffset', index: 4, type: 'int32' },
    { name: 'IsInteriorMap', index: 5, type: 'bool' },
]);


// ── FlatBuffer binary reader ──

function getStructSize(ctor: Function): number {
    const s = schemaMap.get(ctor);
    if (!s || s.kind !== 'struct') throw new Error(`No struct schema for ${(ctor as any).name}`);
    return s.size;
}

function scalarSize(t: ScalarType): number {
    switch (t) {
        case 'int8': case 'uint8': case 'bool': return 1;
        case 'int16': case 'uint16': return 2;
        case 'int32': case 'uint32': case 'float32': return 4;
        case 'uint64': return 8;
    }
}

function readScalar(bb: ByteBuffer, pos: number, t: ScalarType): number | bigint | boolean {
    switch (t) {
        case 'int8': return bb.readInt8(pos);
        case 'uint8': return bb.readUint8(pos);
        case 'int16': return bb.readInt16(pos);
        case 'uint16': return bb.readUint16(pos);
        case 'int32': return bb.readInt32(pos);
        case 'uint32': return bb.readUint32(pos);
        case 'float32': return bb.readFloat32(pos);
        case 'uint64': return bb.readUint64(pos);
        case 'bool': return bb.readInt8(pos) !== 0;
    }
}

function readStruct(bb: ByteBuffer, pos: number, ctor: Function): any {
    const schema = schemaMap.get(ctor) as StructSchema;
    if (!schema) throw new Error(`No struct schema for ${(ctor as any).name}`);
    const obj = new (ctor as any)();
    let offset = pos;
    for (const field of schema.fields) {
        if (typeof field.type === 'object' && 'struct' in field.type) {
            (obj as any)[field.name] = readStruct(bb, offset, field.type.struct);
        } else {
            (obj as any)[field.name] = readScalar(bb, offset, field.type as ScalarType);
        }
        offset += field.size;
    }
    return obj;
}

function readString(bb: ByteBuffer, pos: number): string {
    // pos points to the offset to the string
    const strOffset = pos + bb.readInt32(pos);
    const len = bb.readInt32(strOffset);
    const bytes = bb.bytes();
    let result = '';
    for (let i = 0; i < len; i++) {
        result += String.fromCharCode(bytes[strOffset + 4 + i]);
    }
    return result;
}

function readTable(bb: ByteBuffer, tablePos: number, ctor: Function): any {
    const schema = schemaMap.get(ctor) as TableSchema;
    if (!schema) throw new Error(`No table schema for ${(ctor as any).name}`);

    const obj = new (ctor as any)();

    // Read vtable offset (signed, relative to tablePos)
    const vtableOffset = tablePos - bb.readInt32(tablePos);
    const vtableSize = bb.readInt16(vtableOffset);

    for (const field of schema.fields) {
        const vtableSlot = 4 + field.index * 2;
        if (vtableSlot >= vtableSize) continue; // field not in vtable

        const fieldOff = bb.readInt16(vtableOffset + vtableSlot);
        if (fieldOff === 0) continue; // field not present

        const fieldPos = tablePos + fieldOff;
        (obj as any)[field.name] = readFieldValue(bb, fieldPos, field.type);
    }

    return obj;
}

function readFieldValue(bb: ByteBuffer, fieldPos: number, type: FieldType): any {
    if (typeof type === 'string') {
        // Scalar or string or bytes or bool
        if (type === 'string') {
            return readString(bb, fieldPos);
        } else if (type === 'bytes') {
            // Bytes is a vector of uint8
            const vecOffset = fieldPos + bb.readInt32(fieldPos);
            const len = bb.readInt32(vecOffset);
            const start = vecOffset + 4;
            return new Uint8Array(bb.bytes().buffer, bb.bytes().byteOffset + start, len);
        } else {
            return readScalar(bb, fieldPos, type);
        }
    }

    if ('table' in type) {
        const tableOffset = fieldPos + bb.readInt32(fieldPos);
        return readTable(bb, tableOffset, type.table);
    }

    if ('struct' in type) {
        return readStruct(bb, fieldPos, type.struct);
    }

    if ('vectorOf' in type) {
        return readVector(bb, fieldPos, type.vectorOf);
    }

    throw new Error(`Unknown field type: ${JSON.stringify(type)}`);
}

function readVector(bb: ByteBuffer, fieldPos: number, elementType: ScalarType | 'string' | { table: Function } | { struct: Function }): any[] {
    const vecOffset = fieldPos + bb.readInt32(fieldPos);
    const len = bb.readInt32(vecOffset);
    const dataStart = vecOffset + 4;
    const result: any[] = [];

    if (typeof elementType === 'string') {
        if (elementType === 'string') {
            for (let i = 0; i < len; i++) {
                const elemPos = dataStart + i * 4;
                result.push(readString(bb, elemPos));
            }
        } else {
            // Scalar vector
            const sz = scalarSize(elementType);
            for (let i = 0; i < len; i++) {
                result.push(readScalar(bb, dataStart + i * sz, elementType));
            }
        }
    } else if ('table' in elementType) {
        for (let i = 0; i < len; i++) {
            const elemPos = dataStart + i * 4;
            const tableOffset = elemPos + bb.readInt32(elemPos);
            result.push(readTable(bb, tableOffset, elementType.table));
        }
    } else if ('struct' in elementType) {
        const structSize = getStructSize(elementType.struct);
        for (let i = 0; i < len; i++) {
            result.push(readStruct(bb, dataStart + i * structSize, elementType.struct));
        }
    }

    return result;
}


// ── Public API ──

export class FlatBufferConverter {
    /**
     * Deserialize a FlatBuffer binary from a file path or Buffer.
     * Pass the root table class as the second argument.
     * Matches C#: FlatBufferConverter.DeserializeFrom<T>(data)
     */
    static DeserializeFrom<T>(data: string | Buffer | Uint8Array, RootType: Function): T {
        let bytes: Uint8Array;
        if (typeof data === 'string') {
            const buf = fs.readFileSync(data);
            bytes = new Uint8Array(buf.buffer, buf.byteOffset, buf.byteLength);
        } else if (Buffer.isBuffer(data)) {
            bytes = new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
        } else {
            bytes = data;
        }

        const bb = new ByteBuffer(bytes);
        // Root table offset is at byte 0
        const rootOffset = bb.readInt32(0);
        return readTable(bb, rootOffset, RootType) as T;
    }
}