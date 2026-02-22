import { Vector2, Vector3, Vector4 } from './Math.js';
import { PathString } from '../utils/PathString.js';
import { TrinityArmature } from './TrinityArmature.js';
import { TrinityMaterial } from './TrinityMaterial.js';
import * as fs from 'fs';
import * as path from 'path';
import { FlatBufferConverter } from '../utils/index.js';
import { Material } from '../flatbuffers/Gfx2/Material.js';
import {
    TRMDL, TRMSH, TRMBF, TRMTR, TRBuffer, TRVertexDeclaration,
    TRVertexUsage, TRVertexFormat, TRIndexFormat,
    TRBoneWeight, TRTexture,
    TRFloatParameter, TRVec2fParameter, TRVec3fParameter, TRVec4fParameter,
    TRSKL, TRTransformNode, TRJointInfo, TRHelperBoneInfo, TRStringParameter,
    TRMaterial
} from '../flatbuffers/TR/Model/index.js';

enum BlendIndexRemapMode {
    None,
    BoneWeights,
    JointInfo,
    SkinningPalette,
    BoneMeta
}

interface BlendIndexStats {
    VertexCount: number;
    MaxIndex: number;
}

export interface ExportSubmesh {
    Name: string;
    MaterialName: string;
    Positions: Vector3[];
    Normals: Vector3[];
    UVs: Vector2[];
    Colors: Vector4[];
    Tangents: Vector4[];
    Binormals: Vector3[];
    BlendIndices: Vector4[];
    BlendWeights: Vector4[];
    Indices: number[];
    HasVertexColors: boolean;
    HasTangents: boolean;
    HasBinormals: boolean;
    HasSkinning: boolean;
}

export interface ExportData {
    Name: string;
    Submeshes: readonly ExportSubmesh[];
    Armature: TrinityArmature | null;
    Materials: readonly TrinityMaterial[];
}

/**
 * Headless Trinity model decoder. Parses TRMDL → mesh, skeleton, materials.
 * Ported from gftool Model.cs — all GL rendering code removed.
 */
export class TrinityModelDecoder {
    private _modelPath: PathString;
    private _baseSkeletonCategoryHint: string | null = null;

    public Name: string;

    private Positions: Vector3[][] = [];
    private Normals: Vector3[][] = [];
    private UVs: Vector2[][] = [];
    private Colors: Vector4[][] = [];
    private Tangents: Vector4[][] = [];
    private Binormals: Vector3[][] = [];
    private BlendIndicies: Vector4[][] = [];
    private BlendWeights: Vector4[][] = [];
    private BlendBoneWeights: (TRBoneWeight[] | null)[] = [];
    private BlendIndiciesOriginal: Vector4[][] = [];
    private BlendMeshNames: string[] = [];

    private Indices: number[][] = [];
    private HasVertexColors: boolean[] = [];
    private HasTangents: boolean[] = [];
    private HasBinormals: boolean[] = [];
    private HasSkinning: boolean[] = [];

    private _materials: TrinityMaterial[] | null = null;
    private MaterialNames: string[] = [];
    private SubmeshNames: string[] = [];

    private _armature: TrinityArmature | null = null;
    public get Armature(): TrinityArmature | null {
        return this._armature;
    }

    private _blendIndexStats: BlendIndexStats | null = null;

    constructor(modelFile: string, loadAllLods: boolean = false) {
        this.Name = this.GetFileNameWithoutExtension(modelFile);
        this._modelPath = new PathString(modelFile);

        const mdl = FlatBufferConverter.DeserializeFrom(modelFile, TRMDL);

        // Meshes
        if (loadAllLods) {
            for (const mesh of mdl.Meshes ?? []) {
                this.ParseMesh(this._modelPath.combine(mesh.PathName));
            }
        } else if (mdl.Meshes && mdl.Meshes.length > 0) {
            const mesh = mdl.Meshes[0]; // LOD0
            this.ParseMesh(this._modelPath.combine(mesh.PathName));
        }

        this._baseSkeletonCategoryHint = this.GuessBaseSkeletonCategoryFromMesh(
            mdl.Meshes != null && mdl.Meshes.length > 0 ? mdl.Meshes[0].PathName : null
        );

        // Materials
        for (const mat of mdl.Materials ?? []) {
            this.ParseMaterial(this._modelPath.combine(mat));
        }

        // Skeleton
        if (mdl.Skeleton) {
            this.ParseArmature(this._modelPath.combine(mdl.Skeleton.PathName));
        }
    }

    private GetFileNameWithoutExtension(filePath: string): string {
        const lastSlash = Math.max(filePath.lastIndexOf('/'), filePath.lastIndexOf('\\'));
        const fileName = lastSlash >= 0 ? filePath.substring(lastSlash + 1) : filePath;
        const lastDot = fileName.lastIndexOf('.');
        return lastDot >= 0 ? fileName.substring(0, lastDot) : fileName;
    }

    public CreateExportData(): ExportData {
        const subs: ExportSubmesh[] = [];
        const count = this.Positions.length;
        for (let i = 0; i < count; i++) {
            const submeshName = i < this.SubmeshNames.length ? this.SubmeshNames[i] : `Submesh ${i}`;
            const materialName = i < this.MaterialNames.length ? this.MaterialNames[i] : '';
            subs.push({
                Name: submeshName,
                MaterialName: materialName,
                Positions: this.Positions[i],
                Normals: i < this.Normals.length ? this.Normals[i] : [],
                UVs: i < this.UVs.length ? this.UVs[i] : [],
                Colors: i < this.Colors.length ? this.Colors[i] : [],
                Tangents: i < this.Tangents.length ? this.Tangents[i] : [],
                Binormals: i < this.Binormals.length ? this.Binormals[i] : [],
                BlendIndices: i < this.BlendIndicies.length ? this.BlendIndicies[i] : [],
                BlendWeights: i < this.BlendWeights.length ? this.BlendWeights[i] : [],
                Indices: i < this.Indices.length ? this.Indices[i] : [],
                HasVertexColors: i < this.HasVertexColors.length && this.HasVertexColors[i],
                HasTangents: i < this.HasTangents.length && this.HasTangents[i],
                HasBinormals: i < this.HasBinormals.length && this.HasBinormals[i],
                HasSkinning: i < this.HasSkinning.length && this.HasSkinning[i]
            });
        }

        return {
            Name: this.Name,
            Submeshes: subs,
            Armature: this._armature,
            Materials: this._materials ?? []
        };
    }

    private ParseMesh(file: string): void {
        const msh = FlatBufferConverter.DeserializeFrom(file, TRMSH);
        const bufferFile = this._modelPath.combine(msh.bufferFilePath ?? '');
        const buffersFb = FlatBufferConverter.DeserializeFrom(bufferFile, TRMBF);
        const buffers = buffersFb.TRMeshBuffers ?? [];
        const shapeCnt = msh.Meshes?.length ?? 0;
        for (let i = 0; i < shapeCnt; i++) {
            const meshShape = msh.Meshes![i];
            const vertBufs = buffers[i]?.VertexBuffer ?? [];
            const indexBuf = buffers[i]?.IndexBuffer?.[0];
            if (!indexBuf) continue;
            for (const part of meshShape.meshParts ?? []) {
                this.MaterialNames.push(part.MaterialName ?? '');
                this.SubmeshNames.push(`${meshShape.Name ?? ''}:${part.MaterialName ?? ''}`);
                let declIndex = part.vertexDeclarationIndex ?? 0;
                const decls = meshShape.vertexDeclaration;
                if (declIndex < 0 || declIndex >= (decls?.length ?? 0)) declIndex = 0;
                this.ParseMeshBuffer(decls![declIndex], vertBufs, indexBuf, meshShape.IndexType ?? TRIndexFormat.INT, Number(part.indexOffset ?? 0), Number(part.indexCount ?? 0), meshShape.boneWeight ?? null, meshShape.Name ?? '');
            }
        }
    }

    private ParseMeshBuffer(
        vertDesc: TRVertexDeclaration,
        vertexBuffers: TRBuffer[],
        indexBuf: TRBuffer,
        polyType: TRIndexFormat,
        start: number,
        count: number,
        boneWeights: TRBoneWeight[] | null,
        meshName: string
    ): void {
        if (!vertexBuffers.length) return;

        const posElement = vertDesc.vertexElements.find(e => e.vertexUsage === TRVertexUsage.POSITION);
        if (!posElement) return;

        const posBuffer = TrinityModelDecoder.GetVertexBuffer(vertexBuffers, posElement.vertexElementLayer);
        if (!posBuffer || !posBuffer.Bytes.length) return;

        const posStride = TrinityModelDecoder.GetStride(vertDesc, posElement.vertexElementSizeIndex);
        if (posStride <= 0) return;

        const vertexCount = posBuffer.Bytes.length / posStride;
        if (vertexCount <= 0) return;

        const pos: Vector3[] = new Array(vertexCount);
        const norm: Vector3[] = new Array(vertexCount);
        const uv: Vector2[] = new Array(vertexCount);
        const color: Vector4[] = new Array(vertexCount);
        const tangent: Vector4[] = new Array(vertexCount);
        const binormal: Vector3[] = new Array(vertexCount);
        const blendIndices: Vector4[] = new Array(vertexCount);
        const blendWeights: Vector4[] = new Array(vertexCount);
        let hasNormals = false;
        let hasUvs = false;
        let hasColors = false;
        let hasTangents = false;
        let hasBinormals = false;
        let hasBlendIndices = false;
        let hasBlendWeights = false;
        this._blendIndexStats = null;

        const indices: number[] = [];

        const blendIndexStreams: Vector4[][] = [];
        const blendWeightStreams: Vector4[][] = [];
        let blendIndexElementIndex = -1;
        let blendWeightElementIndex = -1;
        let texCoordElementIndex = -1;

        for (let i = 0; i < vertDesc.vertexElements.length; i++) {
            const att = vertDesc.vertexElements[i];
            const buffer = TrinityModelDecoder.GetVertexBuffer(vertexBuffers, att.vertexElementLayer);
            if (!buffer) continue;

            const stride = TrinityModelDecoder.GetStride(vertDesc, att.vertexElementSizeIndex);
            if (stride <= 0) continue;

            let blendIndexStreamIndex: number | null = null;
            let blendWeightStreamIndex: number | null = null;
            if (att.vertexUsage === TRVertexUsage.BLEND_INDEX) {
                blendIndexElementIndex++;
                this.EnsureBlendStream(blendIndexStreams, blendIndexElementIndex, vertexCount);
                blendIndexStreamIndex = blendIndexElementIndex;
            } else if (att.vertexUsage === TRVertexUsage.BLEND_WEIGHTS) {
                blendWeightElementIndex++;
                this.EnsureBlendStream(blendWeightStreams, blendWeightElementIndex, vertexCount);
                blendWeightStreamIndex = blendWeightElementIndex;
            } else if (att.vertexUsage === TRVertexUsage.TEX_COORD) {
                texCoordElementIndex++;
                if (texCoordElementIndex === 0) hasUvs = true;
            }

            for (let v = 0; v < vertexCount; v++) {
                const offset = v * stride + att.vertexElementOffset;
                if (!TrinityModelDecoder.HasBytes(buffer.Bytes, offset, att.vertexFormat)) continue;

                switch (att.vertexUsage) {
                    case TRVertexUsage.POSITION:
                        pos[v] = TrinityModelDecoder.ReadVector3(buffer.Bytes, offset, att.vertexFormat);
                        break;
                    case TRVertexUsage.NORMAL:
                        norm[v] = TrinityModelDecoder.ReadNormal(buffer.Bytes, offset, att.vertexFormat);
                        hasNormals = true;
                        break;
                    case TRVertexUsage.TEX_COORD:
                        uv[v] = TrinityModelDecoder.ReadVector2(buffer.Bytes, offset, att.vertexFormat);
                        break;
                    case TRVertexUsage.COLOR:
                        color[v] = TrinityModelDecoder.ReadColor(buffer.Bytes, offset, att.vertexFormat);
                        hasColors = true;
                        break;
                    case TRVertexUsage.TANGENT:
                        tangent[v] = TrinityModelDecoder.ReadTangent(buffer.Bytes, offset, att.vertexFormat);
                        hasTangents = true;
                        break;
                    case TRVertexUsage.BINORMAL:
                        binormal[v] = TrinityModelDecoder.ReadNormal(buffer.Bytes, offset, att.vertexFormat);
                        hasBinormals = true;
                        break;
                    case TRVertexUsage.BLEND_INDEX:
                        if (blendIndexStreamIndex !== null) {
                            blendIndexStreams[blendIndexStreamIndex][v] = TrinityModelDecoder.ReadBlendIndices(buffer.Bytes, offset, att.vertexFormat);
                        }
                        hasBlendIndices = true;
                        break;
                    case TRVertexUsage.BLEND_WEIGHTS:
                        if (blendWeightStreamIndex !== null) {
                            blendWeightStreams[blendWeightStreamIndex][v] = TrinityModelDecoder.ReadBlendWeights(buffer.Bytes, offset, att.vertexFormat);
                        }
                        hasBlendWeights = true;
                        break;
                }
            }
        }

        if (hasBlendIndices && blendIndexStreams.length > 0) {
            blendIndices.splice(0, blendIndices.length, ...blendIndexStreams[0]);
        }
        if (hasBlendWeights && blendWeightStreams.length > 0) {
            blendWeights.splice(0, blendWeights.length, ...blendWeightStreams[0]);
        }

        // Collapse multiple blend streams to top 4
        if ((blendIndexStreams.length > 1 || blendWeightStreams.length > 1) && hasBlendIndices && hasBlendWeights) {
            const streamCount = Math.min(blendIndexStreams.length, blendWeightStreams.length);
            if (streamCount > 1) {
                const [collapsedIndices, collapsedWeights] = this.CollapseBlendStreams(blendIndexStreams, blendWeightStreams, streamCount);
                blendIndices.splice(0, blendIndices.length, ...collapsedIndices);
                blendWeights.splice(0, blendWeights.length, ...collapsedWeights);
            }
        }

        if (hasBlendIndices) {
            let maxIndex = 0;
            for (let v = 0; v < vertexCount; v++) {
                const idx = blendIndices[v];
                maxIndex = Math.max(maxIndex, Math.round(Math.max(Math.max(idx.x, idx.y), Math.max(idx.z, idx.w))));
            }
            this._blendIndexStats = { VertexCount: vertexCount, MaxIndex: maxIndex };
        }

        this.Positions.push(pos);
        this.Normals.push(hasNormals ? norm : new Array(vertexCount).fill(Vector3.Zero));
        this.UVs.push(hasUvs ? uv : new Array(vertexCount).fill(Vector2.Zero));

        if (!hasColors) {
            for (let v = 0; v < color.length; v++) {
                color[v] = Vector4.One;
            }
        }
        this.Colors.push(color);
        this.HasVertexColors.push(hasColors);

        if (!hasTangents) {
            for (let v = 0; v < tangent.length; v++) {
                tangent[v] = new Vector4(1, 0, 0, 1);
            }
        }
        this.Tangents.push(tangent);
        this.HasTangents.push(hasTangents);

        if (!hasBinormals) {
            for (let v = 0; v < binormal.length; v++) {
                binormal[v] = Vector3.UnitY;
            }
        }
        this.Binormals.push(binormal);
        this.HasBinormals.push(hasBinormals);

        this.BlendIndicies.push(blendIndices);
        this.BlendIndiciesOriginal.push(blendIndices.slice());
        this.BlendWeights.push(blendWeights);
        this.BlendBoneWeights.push(boneWeights);
        this.BlendMeshNames.push(meshName);
        this.HasSkinning.push(hasBlendIndices && hasBlendWeights);

        // Parse index buffer
        const indexBytes = indexBuf.Bytes;
        const dv = new DataView(indexBytes.buffer, indexBytes.byteOffset);
        const indsize = 1 << Number(polyType);
        let currPos = start * indsize;
        while (currPos < (start + count) * indsize) {
            switch (polyType) {
                case TRIndexFormat.BYTE:
                    indices.push(dv.getUint8(currPos));
                    break;
                case TRIndexFormat.SHORT:
                    indices.push(dv.getUint16(currPos, true));
                    break;
                case TRIndexFormat.INT:
                    indices.push(dv.getUint32(currPos, true));
                    break;
            }
            currPos += indsize;
        }
        this.Indices.push(indices);
    }

    private static GetVertexBuffer(buffers: TRBuffer[], index: number): TRBuffer | null {
        if (!buffers || index < 0 || index >= buffers.length) return null;
        return buffers[index] ?? null;
    }

    private static GetStride(vertDesc: TRVertexDeclaration, sizeIndex: number): number {
        if (!vertDesc.vertexElementSizes || sizeIndex < 0 || sizeIndex >= vertDesc.vertexElementSizes.length) return 0;
        return vertDesc.vertexElementSizes[sizeIndex]?.elementSize ?? 0;
    }

    private static HasBytes(bytes: Uint8Array, offset: number, format: TRVertexFormat): boolean {
        let size = 0;
        switch (format) {
            case TRVertexFormat.X32_Y32_Z32_FLOAT: size = 12; break;
            case TRVertexFormat.X32_Y32_FLOAT: size = 8; break;
            case TRVertexFormat.W32_X32_Y32_Z32_FLOAT: size = 16; break;
            case TRVertexFormat.W32_X32_Y32_Z32_UNSIGNED: size = 16; break;
            case TRVertexFormat.W16_X16_Y16_Z16_FLOAT: size = 8; break;
            case TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED: size = 8; break;
            case TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED: size = 4; break;
            case TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED: size = 4; break;
            default: size = 0;
        }
        return size > 0 && offset >= 0 && offset + size <= bytes.length;
    }

    private static halfToFloat(h: number): number {
        const e = (h >> 10) & 0x1F;
        const m = h & 0x3FF;
        let f: number;
        if (e === 0) {
            f = m * Math.pow(2, -14);
        } else if (e === 0x1F) {
            f = m === 0 ? Infinity : NaN;
        } else {
            f = (1 + m * Math.pow(2, -10)) * Math.pow(2, e - 15);
        }
        return f * ((h >> 15) === 1 ? -1 : 1);
    }

    private static readUnorm16(bytes: Uint8Array, offset: number): number {
        return new DataView(bytes.buffer, bytes.byteOffset + offset, 2).getUint16(0, true) / 65535;
    }

    private static readSnorm16(bytes: Uint8Array, offset: number): number {
        return (new DataView(bytes.buffer, bytes.byteOffset + offset, 2).getUint16(0, true) / 65535 * 2 - 1);
    }

    private static readUnorm8(bytes: Uint8Array, offset: number): number {
        return bytes[offset] / 255;
    }

    private static readSnorm8(bytes: Uint8Array, offset: number): number {
        return (bytes[offset] / 255 * 2 - 1);
    }

    private static ReadVector3(bytes: Uint8Array, offset: number, format: TRVertexFormat): Vector3 {
        const dv = new DataView(bytes.buffer, bytes.byteOffset + offset);
        switch (format) {
            case TRVertexFormat.X32_Y32_Z32_FLOAT:
                return new Vector3(dv.getFloat32(0, true), dv.getFloat32(4, true), dv.getFloat32(8, true));
            case TRVertexFormat.W32_X32_Y32_Z32_FLOAT:
                return new Vector3(dv.getFloat32(4, true), dv.getFloat32(8, true), dv.getFloat32(12, true));
            case TRVertexFormat.W16_X16_Y16_Z16_FLOAT:
                return new Vector3(
                    this.halfToFloat(dv.getUint16(0, true)),
                    this.halfToFloat(dv.getUint16(2, true)),
                    this.halfToFloat(dv.getUint16(4, true))
                );
            case TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED:
                return new Vector3(
                    this.readUnorm16(bytes, offset),
                    this.readUnorm16(bytes, offset + 2),
                    this.readUnorm16(bytes, offset + 4)
                );
            case TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED:
            case TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED:
                return new Vector3(
                    this.readUnorm8(bytes, offset),
                    this.readUnorm8(bytes, offset + 1),
                    this.readUnorm8(bytes, offset + 2)
                );
            default:
                return Vector3.Zero;
        }
    }

    private static ReadNormal(bytes: Uint8Array, offset: number, format: TRVertexFormat): Vector3 {
        const dv = new DataView(bytes.buffer, bytes.byteOffset + offset);
        switch (format) {
            case TRVertexFormat.W16_X16_Y16_Z16_FLOAT:
                return new Vector3(
                    this.halfToFloat(dv.getUint16(0, true)),
                    this.halfToFloat(dv.getUint16(2, true)),
                    this.halfToFloat(dv.getUint16(4, true))
                );
            case TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED:
                return new Vector3(
                    this.readSnorm16(bytes, offset),
                    this.readSnorm16(bytes, offset + 2),
                    this.readSnorm16(bytes, offset + 4)
                );
            case TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED:
            case TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED:
                return new Vector3(
                    this.readSnorm8(bytes, offset),
                    this.readSnorm8(bytes, offset + 1),
                    this.readSnorm8(bytes, offset + 2)
                );
            case TRVertexFormat.X32_Y32_Z32_FLOAT:
                return new Vector3(dv.getFloat32(0, true), dv.getFloat32(4, true), dv.getFloat32(8, true));
            default:
                return Vector3.UnitZ;
        }
    }

    private static ReadVector2(bytes: Uint8Array, offset: number, format: TRVertexFormat): Vector2 {
        const dv = new DataView(bytes.buffer, bytes.byteOffset + offset);
        switch (format) {
            case TRVertexFormat.X32_Y32_FLOAT:
                return new Vector2(dv.getFloat32(0, true), dv.getFloat32(4, true));
            case TRVertexFormat.W16_X16_Y16_Z16_FLOAT:
                return new Vector2(this.halfToFloat(dv.getUint16(0, true)), this.halfToFloat(dv.getUint16(2, true)));
            case TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED:
                return new Vector2(this.readUnorm16(bytes, offset), this.readUnorm16(bytes, offset + 2));
            case TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED:
            case TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED:
                return new Vector2(this.readUnorm8(bytes, offset), this.readUnorm8(bytes, offset + 1));
            default:
                return Vector2.Zero;
        }
    }

    private static ReadColor(bytes: Uint8Array, offset: number, format: TRVertexFormat): Vector4 {
        const dv = new DataView(bytes.buffer, bytes.byteOffset + offset);
        switch (format) {
            case TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED:
            case TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED:
                return new Vector4(
                    this.readUnorm8(bytes, offset),
                    this.readUnorm8(bytes, offset + 1),
                    this.readUnorm8(bytes, offset + 2),
                    this.readUnorm8(bytes, offset + 3)
                );
            case TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED:
                return new Vector4(
                    this.readUnorm16(bytes, offset),
                    this.readUnorm16(bytes, offset + 2),
                    this.readUnorm16(bytes, offset + 4),
                    this.readUnorm16(bytes, offset + 6)
                );
            case TRVertexFormat.W16_X16_Y16_Z16_FLOAT:
                return new Vector4(
                    this.halfToFloat(dv.getUint16(0, true)),
                    this.halfToFloat(dv.getUint16(2, true)),
                    this.halfToFloat(dv.getUint16(4, true)),
                    this.halfToFloat(dv.getUint16(6, true))
                );
            default:
                return Vector4.One;
        }
    }

    private static ReadTangent(bytes: Uint8Array, offset: number, format: TRVertexFormat): Vector4 {
        const dv = new DataView(bytes.buffer, bytes.byteOffset + offset);
        switch (format) {
            case TRVertexFormat.W32_X32_Y32_Z32_FLOAT:
                return new Vector4(
                    dv.getFloat32(4, true),
                    dv.getFloat32(8, true),
                    dv.getFloat32(12, true),
                    dv.getFloat32(0, true)
                );
            case TRVertexFormat.X32_Y32_Z32_FLOAT:
                return new Vector4(
                    dv.getFloat32(0, true),
                    dv.getFloat32(4, true),
                    dv.getFloat32(8, true),
                    1
                );
            case TRVertexFormat.W16_X16_Y16_Z16_FLOAT:
                return new Vector4(
                    this.halfToFloat(dv.getUint16(0, true)),
                    this.halfToFloat(dv.getUint16(2, true)),
                    this.halfToFloat(dv.getUint16(4, true)),
                    this.halfToFloat(dv.getUint16(6, true))
                );
            case TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED:
                return new Vector4(
                    this.readSnorm16(bytes, offset),
                    this.readSnorm16(bytes, offset + 2),
                    this.readSnorm16(bytes, offset + 4),
                    this.readSnorm16(bytes, offset + 6)
                );
            case TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED:
            case TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED:
                return new Vector4(
                    this.readSnorm8(bytes, offset),
                    this.readSnorm8(bytes, offset + 1),
                    this.readSnorm8(bytes, offset + 2),
                    this.readSnorm8(bytes, offset + 3)
                );
            default:
                return new Vector4(1, 0, 0, 1);
        }
    }

    private static ReadBlendIndices(bytes: Uint8Array, offset: number, format: TRVertexFormat): Vector4 {
        const dv = new DataView(bytes.buffer, bytes.byteOffset + offset);
        switch (format) {
            case TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED:
                return new Vector4(
                    bytes[offset + 1],
                    bytes[offset + 2],
                    bytes[offset + 3],
                    bytes[offset]
                );
            case TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED:
                return new Vector4(
                    dv.getUint16(2, true),
                    dv.getUint16(4, true),
                    dv.getUint16(6, true),
                    dv.getUint16(0, true)
                );
            case TRVertexFormat.W32_X32_Y32_Z32_UNSIGNED:
                return new Vector4(
                    dv.getUint32(4, true),
                    dv.getUint32(8, true),
                    dv.getUint32(12, true),
                    dv.getUint32(0, true)
                );
            case TRVertexFormat.W32_X32_Y32_Z32_FLOAT:
                return new Vector4(
                    dv.getFloat32(4, true),
                    dv.getFloat32(8, true),
                    dv.getFloat32(12, true),
                    dv.getFloat32(0, true)
                );
            default:
                return Vector4.Zero;
        }
    }

    private static ReadBlendWeights(bytes: Uint8Array, offset: number, format: TRVertexFormat): Vector4 {
        const dv = new DataView(bytes.buffer, bytes.byteOffset + offset);
        switch (format) {
            case TRVertexFormat.W16_X16_Y16_Z16_UNSIGNED_NORMALIZED:
                return new Vector4(
                    this.readUnorm16(bytes, offset + 2),
                    this.readUnorm16(bytes, offset + 4),
                    this.readUnorm16(bytes, offset + 6),
                    this.readUnorm16(bytes, offset)
                );
            case TRVertexFormat.R8_G8_B8_A8_UNSIGNED_NORMALIZED:
            case TRVertexFormat.W8_X8_Y8_Z8_UNSIGNED:
                return new Vector4(
                    this.readUnorm8(bytes, offset + 1),
                    this.readUnorm8(bytes, offset + 2),
                    this.readUnorm8(bytes, offset + 3),
                    this.readUnorm8(bytes, offset)
                );
            case TRVertexFormat.W32_X32_Y32_Z32_FLOAT:
                return new Vector4(
                    dv.getFloat32(4, true),
                    dv.getFloat32(8, true),
                    dv.getFloat32(12, true),
                    dv.getFloat32(0, true)
                );
            default:
                return Vector4.Zero;
        }
    }

    private EnsureBlendStream(streams: Vector4[][], index: number, vertexCount: number): void {
        while (streams.length <= index) {
            streams.push(new Array(vertexCount).fill(Vector4.Zero));
        }
    }

    private CollapseBlendStreams(
        indexStreams: Vector4[][],
        weightStreams: Vector4[][],
        streamCount: number
    ): [Vector4[], Vector4[]] {
        const vertexCount = indexStreams[0].length;
        const collapsedIndices: Vector4[] = new Array(vertexCount).fill(Vector4.Zero);
        const collapsedWeights: Vector4[] = new Array(vertexCount).fill(Vector4.Zero);

        for (let v = 0; v < vertexCount; v++) {
            const totals: Record<number, number> = {};
            for (let s = 0; s < streamCount; s++) {
                const idx = indexStreams[s][v];
                const w = weightStreams[s][v];
                this.AccumulateInfluence(totals, Math.round(idx.x), w.x);
                this.AccumulateInfluence(totals, Math.round(idx.y), w.y);
                this.AccumulateInfluence(totals, Math.round(idx.z), w.z);
                this.AccumulateInfluence(totals, Math.round(idx.w), w.w);
            }

            if (Object.keys(totals).length === 0) {
                collapsedIndices[v] = Vector4.Zero;
                collapsedWeights[v] = Vector4.Zero;
                continue;
            }

            const entries = Object.entries(totals)
                .map(([k, v]) => ({ k: Number(k), v: Number(v) }))
                .sort((a, b) => b.v - a.v)
                .slice(0, 4);
            collapsedIndices[v] = new Vector4(
                entries[0]?.k ?? 0,
                entries[1]?.k ?? 0,
                entries[2]?.k ?? 0,
                entries[3]?.k ?? 0
            );
            collapsedWeights[v] = new Vector4(
                entries[0]?.v ?? 0,
                entries[1]?.v ?? 0,
                entries[2]?.v ?? 0,
                entries[3]?.v ?? 0
            );
        }
        return [collapsedIndices, collapsedWeights];
    }

    private AccumulateInfluence(totals: Record<number, number>, index: number, weight: number): void {
        if (weight <= 0) return;
        totals[index] = (totals[index] ?? 0) + weight;
    }

    private ParseMaterial(file: string): void {
        const matlist: TrinityMaterial[] = [];
        const materialPath = new PathString(file);

        let trmtrFallback: TRMTR | null = null;
        try {
            trmtrFallback = FlatBufferConverter.DeserializeFrom(file, TRMTR);
        } catch (e) {
            // ignore
        }

        const trmtrByName = new Map<string, TRMaterial>();
        if (trmtrFallback?.Materials) {
            for (const mat of trmtrFallback.Materials) {
                if (mat?.Name) {
                    trmtrByName.set(mat.Name, mat);
                }
            }
        }

        let gfxMaterials: Material | null = null;
        try {
            gfxMaterials = FlatBufferConverter.DeserializeFrom(file, Material);
        } catch (e) {
            // ignore
        }

        if (gfxMaterials?.ItemList && gfxMaterials.ItemList.length > 0) {
            for (const item of gfxMaterials.ItemList) {
                const shaderName = item?.TechniqueList?.[0]?.Name ?? 'Standard';
                const shaderParams: TRStringParameter[] = [];

                if (item.TechniqueList) {
                    for (const technique of item.TechniqueList ?? []) {
                        if (!technique?.ShaderOptions) continue;
                        for (const opt of technique.ShaderOptions ?? []) {
                            if (opt) {
                                shaderParams.push({ Name: opt.Name ?? '', Value: opt.Choice ?? '' });
                            }
                        }
                    }
                }

                if (item.IntParamList) {
                    for (const p of item.IntParamList ?? []) {
                        if (p) {
                            shaderParams.push({ Name: p.Name ?? '', Value: p.Value.toString() });
                        }
                    }
                }

                const textures = item.TextureParamList?.map(t => ({
                    Name: t.Name ?? '',
                    File: t.FilePath ?? '',
                    Slot: Math.max(0, t.SamplerId ?? 0)
                })) ?? [];

                const trmat: any = {
                    Name: item.Name ?? 'Material',
                    Shader: [{
                        Name: shaderName,
                        Values: shaderParams
                    }],
                    Textures: textures,
                    FloatParams: item.FloatParamList?.map(p => ({ Name: p.Name ?? '', Value: p.Value })) ?? [],
                    Vec2fParams: item.Vector2fParamList?.map(p => ({ Name: p.Name ?? '', Value: p.Value })) ?? [],
                    Vec3fParams: item.Vector3fParamList?.map(p => ({ Name: p.Name ?? '', Value: p.Value })) ?? [],
                    Vec4fParams: item.Vector4fParamList?.map(p => ({ Name: p.Name ?? '', Value: p.Value })) ?? []
                };

                const fallbackMat = trmtrByName.get(trmat.Name);
                if (fallbackMat) {
                    (trmat as any).Samplers = (fallbackMat as any).Samplers;
                }

                matlist.push(new TrinityMaterial(materialPath, trmat));
            }
            this._materials = matlist;
            return;
        }

        // Fallback to TRMTR
        const mats = trmtrFallback ?? FlatBufferConverter.DeserializeFrom(file, TRMTR);
        for (const mat of mats.Materials ?? []) {
            matlist.push(new TrinityMaterial(materialPath, mat));
        }
        this._materials = matlist;
    }

    private ParseArmature(file: string): void {
        const skel = FlatBufferConverter.DeserializeFrom(file, TRSKL);
        const merged = TrinityModelDecoder.TryLoadAndMergeBaseSkeleton(skel, file, this._baseSkeletonCategoryHint);
        this._armature = new TrinityArmature(merged ?? skel, file);
        this.ApplyBlendIndexMapping();
    }

    private GuessBaseSkeletonCategoryFromMesh(meshPathName: string | null): string | null {
        if (!meshPathName || meshPathName.trim().length === 0) {
            return null;
        }

        const fn = this.GetFileNameWithoutExtension(meshPathName).toLowerCase();
        if (fn.startsWith('p0') || fn.startsWith('p1') || fn.startsWith('p2')) {
            return 'Protag';
        }

        if (fn.startsWith('bu_')) return 'CommonNPCbu';
        if (fn.startsWith('dm_')) return 'CommonNPCdm';
        if (fn.startsWith('df_')) return 'CommonNPCdf';
        if (fn.startsWith('em_')) return 'CommonNPCem';
        if (fn.startsWith('fm_')) return 'CommonNPCfm';
        if (fn.startsWith('ff_')) return 'CommonNPCff';
        if (fn.startsWith('gm_')) return 'CommonNPCgm';
        if (fn.startsWith('gf_')) return 'CommonNPCgf';
        if (fn.startsWith('rv_')) return 'CommonNPCrv';

        return null;
    }

    private static TryLoadAndMergeBaseSkeleton(localSkel: TRSKL, localSkelPath: string, category: string | null): TRSKL | null {
        if (!localSkel || !category || !localSkelPath) return null;

        const localDir = path.dirname(localSkelPath);
        const basePath = TrinityModelDecoder.ResolveBaseTrsklPath(localDir, category);
        if (!basePath || !fs.existsSync(basePath)) return null;

        try {
            const baseSkel = FlatBufferConverter.DeserializeFrom(basePath, TRSKL);
            return TrinityModelDecoder.MergeBaseAndLocalSkeletons(baseSkel, localSkel);
        } catch {
            return null;
        }
    }

    private static ResolveBaseTrsklPath(modelDir: string, category: string): string | null {
        let rels: string[];
        switch (category) {
            case 'Protag':
                rels = [
                    '../../model_pc_base/model/p0_base.trskl',
                    '../../../../p2/model/base/p2_base0001_00_default/p2_base0001_00_default.trskl',
                    '../../p2/p2_base0001_00_default/p2_base0001_00_default.trskl'
                ];
                break;
            case 'CommonNPCbu':
                rels = ['../../../model_cc_base/bu/bu_base.trskl', '../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl'];
                break;
            case 'CommonNPCdm':
            case 'CommonNPCdf':
                rels = ['../../../model_cc_base/dm/dm_base.trskl', '../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl'];
                break;
            case 'CommonNPCem':
                rels = ['../../../model_cc_base/em/em_base.trskl', '../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl'];
                break;
            case 'CommonNPCfm':
            case 'CommonNPCff':
                rels = ['../../../model_cc_base/fm/fm_base.trskl', '../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl'];
                break;
            case 'CommonNPCgm':
            case 'CommonNPCgf':
                rels = ['../../../model_cc_base/gm/gm_base.trskl', '../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl'];
                break;
            case 'CommonNPCrv':
                rels = ['../../../model_cc_base/rv/rv_base.trskl', '../../base/cc_base0001_00_young_m/cc_base0001_00_young_m.trskl'];
                break;
            default:
                return null;
        }

        for (const rel of rels) {
            const full = path.resolve(path.join(modelDir, rel));
            if (fs.existsSync(full)) return full;
        }
        return null;
    }

    private static MergeBaseAndLocalSkeletons(baseSkel: TRSKL, localSkel: TRSKL): TRSKL {
        const baseNodeCount = baseSkel.TransformNodes?.length ?? 0;
        const baseJointCount = baseSkel.JointInfos?.length ?? 0;

        const mergedNodes: TRTransformNode[] = [];
        const baseIndexByName = new Map<string, number>();

        baseSkel.TransformNodes?.forEach((n, i) => {
            mergedNodes.push(n);
            if (n.Name) baseIndexByName.set(n.Name, i);
        });

        const mergedJoints: TRJointInfo[] = [...(baseSkel.JointInfos ?? []), ...(localSkel.JointInfos ?? [])];

        localSkel.TransformNodes?.forEach((node) => {
            let parentIndex = node.ParentNodeIndex ?? -1;
            if (node.ParentNodeName && baseIndexByName.has(node.ParentNodeName)) {
                parentIndex = baseIndexByName.get(node.ParentNodeName)!;
            } else if (parentIndex >= 0) {
                parentIndex += baseNodeCount;
            }

            let jointIndex = node.JointInfoIndex ?? -1;
            if (jointIndex >= 0) {
                jointIndex += baseJointCount;
            }

            mergedNodes.push({
                Name: node.Name ?? '',
                Transform: node.Transform,
                ScalePivot: node.ScalePivot,
                RotatePivot: node.RotatePivot,
                ParentNodeIndex: parentIndex,
                JointInfoIndex: jointIndex,
                ParentNodeName: node.ParentNodeName ?? '',
                Priority: node.Priority,
                PriorityPass: node.PriorityPass,
                IgnoreParentRotation: node.IgnoreParentRotation
            } as TRTransformNode);
        });

        return {
            Version: baseSkel.Version || localSkel.Version,
            TransformNodes: mergedNodes,
            JointInfos: mergedJoints,
            HelperBones: baseSkel.HelperBones?.length > 0 ? baseSkel.HelperBones : (localSkel.HelperBones ?? []),
            SkinningPaletteOffset: baseJointCount,
            IsInteriorMap: baseSkel.IsInteriorMap || localSkel.IsInteriorMap
        } as TRSKL;
    }

    private ApplyBlendIndexMapping(): void {
        if (this._armature == null) {
            return;
        }

        const skinPalette = this._armature.BuildSkinningPalette();

        for (let i = 0; i < this.BlendIndiciesOriginal.length; i++) {
            const source = this.BlendIndiciesOriginal[i];
            const boneWeights = i < this.BlendBoneWeights.length ? this.BlendBoneWeights[i] : null;
            const mapped: Vector4[] = new Array(source.length).fill(Vector4.Zero);
            const maxIndexBefore = TrinityModelDecoder.GetMaxIndex(source);

            let canRemapViaBoneWeights = false;
            if (boneWeights && boneWeights.length > 0 && maxIndexBefore < boneWeights.length) {
                let outOfRangeWeights = 0;
                const sampleCount = Math.min(source.length, 512);
                for (let v = 0; v < sampleCount; v++) {
                    const idx = source[v];
                    outOfRangeWeights = TrinityModelDecoder.CountOutOfRange(boneWeights, Math.round(idx.x), outOfRangeWeights);
                    outOfRangeWeights = TrinityModelDecoder.CountOutOfRange(boneWeights, Math.round(idx.y), outOfRangeWeights);
                    outOfRangeWeights = TrinityModelDecoder.CountOutOfRange(boneWeights, Math.round(idx.z), outOfRangeWeights);
                    outOfRangeWeights = TrinityModelDecoder.CountOutOfRange(boneWeights, Math.round(idx.w), outOfRangeWeights);
                }
                canRemapViaBoneWeights = outOfRangeWeights === 0;
            }

            const mode = this.SelectBlendIndexRemapMode(i, canRemapViaBoneWeights, boneWeights, maxIndexBefore, skinPalette);

            for (let v = 0; v < source.length; v++) {
                const idx = source[v];
                if (mode === BlendIndexRemapMode.BoneWeights && boneWeights) {
                    mapped[v] = new Vector4(
                        TrinityModelDecoder.MapBlendIndex(idx.x, boneWeights),
                        TrinityModelDecoder.MapBlendIndex(idx.y, boneWeights),
                        TrinityModelDecoder.MapBlendIndex(idx.z, boneWeights),
                        TrinityModelDecoder.MapBlendIndex(idx.w, boneWeights)
                    );
                } else if (mode === BlendIndexRemapMode.JointInfo) {
                    mapped[v] = new Vector4(
                        Math.round(idx.x) >= 0 && Math.round(idx.x) < this._armature.JointInfoCount ? this._armature.MapJointInfoIndex(Math.round(idx.x)) : idx.x,
                        Math.round(idx.y) >= 0 && Math.round(idx.y) < this._armature.JointInfoCount ? this._armature.MapJointInfoIndex(Math.round(idx.y)) : idx.y,
                        Math.round(idx.z) >= 0 && Math.round(idx.z) < this._armature.JointInfoCount ? this._armature.MapJointInfoIndex(Math.round(idx.z)) : idx.z,
                        Math.round(idx.w) >= 0 && Math.round(idx.w) < this._armature.JointInfoCount ? this._armature.MapJointInfoIndex(Math.round(idx.w)) : idx.w
                    );
                } else if (mode === BlendIndexRemapMode.SkinningPalette) {
                    const ix = Math.round(idx.x);
                    const iy = Math.round(idx.y);
                    const iz = Math.round(idx.z);
                    const iw = Math.round(idx.w);
                    mapped[v] = new Vector4(
                        ix >= 0 && ix < skinPalette.length ? skinPalette[ix] : idx.x,
                        iy >= 0 && iy < skinPalette.length ? skinPalette[iy] : idx.y,
                        iz >= 0 && iz < skinPalette.length ? skinPalette[iz] : idx.z,
                        iw >= 0 && iw < skinPalette.length ? skinPalette[iw] : idx.w
                    );
                } else if (mode === BlendIndexRemapMode.BoneMeta) {
                    mapped[v] = new Vector4(
                        Math.round(idx.x) >= 0 && Math.round(idx.x) < this._armature.BoneMetaCount ? this._armature.MapBoneMetaIndex(Math.round(idx.x)) : idx.x,
                        Math.round(idx.y) >= 0 && Math.round(idx.y) < this._armature.BoneMetaCount ? this._armature.MapBoneMetaIndex(Math.round(idx.y)) : idx.y,
                        Math.round(idx.z) >= 0 && Math.round(idx.z) < this._armature.BoneMetaCount ? this._armature.MapBoneMetaIndex(Math.round(idx.z)) : idx.z,
                        Math.round(idx.w) >= 0 && Math.round(idx.w) < this._armature.BoneMetaCount ? this._armature.MapBoneMetaIndex(Math.round(idx.w)) : idx.w
                    );
                } else {
                    mapped[v] = idx;
                }
            }

            this.BlendIndicies[i] = mapped;
        }
    }

    private SelectBlendIndexRemapMode(
        submeshIndex: number,
        canRemapViaBoneWeights: boolean,
        boneWeights: TRBoneWeight[] | null,
        maxIndexBefore: number,
        skinPalette: number[]
    ): BlendIndexRemapMode {
        if (this._armature == null) {
            return BlendIndexRemapMode.None;
        }

        if (canRemapViaBoneWeights && boneWeights) {
            return BlendIndexRemapMode.BoneWeights;
        }

        const canMapJointInfo = this._armature.JointInfoCount > 0;
        const canMapSkinPalette = skinPalette.length > 0;

        if (this._armature.JointInfoCount > 0 &&
            maxIndexBefore >= 0 &&
            maxIndexBefore < this._armature.JointInfoCount &&
            (this._armature.Bones.length - this._armature.JointInfoCount) >= 16) {
            let mappingIsIdentity = true;
            const sampleMax = Math.min(maxIndexBefore, Math.min(this._armature.JointInfoCount - 1, 64));
            for (let i = 0; i <= sampleMax; i++) {
                if (this._armature.MapJointInfoIndex(i) !== i) {
                    mappingIsIdentity = false;
                    break;
                }
            }
            if (!mappingIsIdentity) {
                return BlendIndexRemapMode.JointInfo;
            }
        }

        const source = this.BlendIndiciesOriginal[submeshIndex];
        const weights = submeshIndex < this.BlendWeights.length ? this.BlendWeights[submeshIndex] : null;

        let bestScore = this.ScoreBlendIndexMapping(source, weights, BlendIndexRemapMode.None, boneWeights, skinPalette);
        let bestMode = BlendIndexRemapMode.None;

        const consider = (candidate: BlendIndexRemapMode) => {
            const score = this.ScoreBlendIndexMapping(source, weights, candidate, boneWeights, skinPalette);
            if (score.outOfRange < bestScore.outOfRange ||
                (score.outOfRange === bestScore.outOfRange && score.nonInfluencer < bestScore.nonInfluencer)) {
                bestScore = score;
                bestMode = candidate;
            }
        };

        if (this._armature.JointInfoCount > 0) consider(BlendIndexRemapMode.JointInfo);
        if (skinPalette.length > 0) consider(BlendIndexRemapMode.SkinningPalette);
        if (this._armature.BoneMetaCount > 0) consider(BlendIndexRemapMode.BoneMeta);

        if (bestMode === BlendIndexRemapMode.None) {
            const jointScore = this._armature.JointInfoCount > 0
                ? this.ScoreBlendIndexMapping(source, weights, BlendIndexRemapMode.JointInfo, boneWeights, skinPalette)
                : { outOfRange: Number.MAX_VALUE, nonInfluencer: Number.MAX_VALUE };
            if (jointScore.outOfRange === bestScore.outOfRange && jointScore.nonInfluencer === bestScore.nonInfluencer) {
                bestMode = BlendIndexRemapMode.JointInfo;
            } else if (skinPalette.length > 0) {
                const palScore = this.ScoreBlendIndexMapping(source, weights, BlendIndexRemapMode.SkinningPalette, boneWeights, skinPalette);
                if (palScore.outOfRange === bestScore.outOfRange && palScore.nonInfluencer === bestScore.nonInfluencer) {
                    bestMode = BlendIndexRemapMode.SkinningPalette;
                }
            }
        }

        return bestMode;
    }

    private ScoreBlendIndexMapping(
        indices: Vector4[],
        weights: Vector4[] | null,
        mode: BlendIndexRemapMode,
        boneWeights: TRBoneWeight[] | null,
        skinPalette: number[]
    ): { outOfRange: number; nonInfluencer: number } {
        if (!this._armature || !indices || indices.length === 0) {
            return { outOfRange: 0, nonInfluencer: 0 };
        }

        let outOfRange = 0;
        let nonInfluencer = 0;
        const sampleCount = Math.min(indices.length, 2048);

        for (let v = 0; v < sampleCount; v++) {
            const idx = indices[v];
            const w = weights && v < weights.length ? weights[v] : Vector4.One;
            const result = this.ScoreComponent(idx.x, w.x, mode, boneWeights, skinPalette);
            outOfRange += result.outOfRange;
            nonInfluencer += result.nonInfluencer;
            const result2 = this.ScoreComponent(idx.y, w.y, mode, boneWeights, skinPalette);
            outOfRange += result2.outOfRange;
            nonInfluencer += result2.nonInfluencer;
            const result3 = this.ScoreComponent(idx.z, w.z, mode, boneWeights, skinPalette);
            outOfRange += result3.outOfRange;
            nonInfluencer += result3.nonInfluencer;
            const result4 = this.ScoreComponent(idx.w, w.w, mode, boneWeights, skinPalette);
            outOfRange += result4.outOfRange;
            nonInfluencer += result4.nonInfluencer;
        }

        return { outOfRange, nonInfluencer };
    }

    private ScoreComponent(
        value: number,
        weight: number,
        mode: BlendIndexRemapMode,
        boneWeights: TRBoneWeight[] | null,
        skinPalette: number[]
    ): { outOfRange: number; nonInfluencer: number } {
        if (weight <= 0.0001) return { outOfRange: 0, nonInfluencer: 0 };
        const mapped = this.MapBlendIndexComponent(value, mode, boneWeights, skinPalette);
        if (mapped < 0 || mapped >= this._armature!.Bones.length) {
            return { outOfRange: 1, nonInfluencer: 0 };
        }
        if (!this._armature!.Bones[mapped].Skinning) {
            return { outOfRange: 0, nonInfluencer: 1 };
        }
        return { outOfRange: 0, nonInfluencer: 0 };
    }

    private MapBlendIndexComponent(value: number, mode: BlendIndexRemapMode, boneWeights: TRBoneWeight[] | null, skinPalette: number[]): number {
        if (!this._armature) return 0;
        const index = Math.round(value);
        if (index < 0) return index;

        if (mode === BlendIndexRemapMode.BoneWeights && boneWeights && index < boneWeights.length) {
            return boneWeights[index].RigIndex;
        }
        if (mode === BlendIndexRemapMode.JointInfo && index < this._armature.JointInfoCount) {
            return this._armature.MapJointInfoIndex(index);
        }
        if (mode === BlendIndexRemapMode.SkinningPalette && index < skinPalette.length) {
            return skinPalette[index];
        }
        if (mode === BlendIndexRemapMode.BoneMeta && index < this._armature.BoneMetaCount) {
            return this._armature.MapBoneMetaIndex(index);
        }
        return index;
    }

    private static GetMaxIndex(indices: Vector4[]): number {
        let max = 0;
        for (const idx of indices) {
            max = Math.max(max, Math.round(Math.max(Math.max(idx.x, idx.y), Math.max(idx.z, idx.w))));
        }
        return max;
    }

    private static CountOutOfRange(boneWeights: TRBoneWeight[], index: number, counter: number): number {
        if (index < 0 || index >= boneWeights.length) {
            return counter + 1;
        }
        return counter;
    }

    private static MapBlendIndex(value: number, boneWeights: TRBoneWeight[]): number {
        const index = Math.round(value);
        if (index >= 0 && index < boneWeights.length) {
            const rigIndex = boneWeights[index].RigIndex;
            return rigIndex >= 0 ? rigIndex : value;
        }
        return value;
    }
}
