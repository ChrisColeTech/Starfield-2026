/**
 * BCH Importer made by gdkchan for Ohana3DS.
 * Please add credits if you use in your project.
 * It is about 92% complete, information here is not guaranteed to be accurate.
 *
 * BCH Version Chart
 * r38497 - Kirby Triple Deluxe (first game to use the format?) (bc 0x5)
 * r38xxx - Pokemon X/Y (bc 0x7)
 * r41xxx - Some Senran Kagura models (bc 0x20)
 * r42xxx - Pokemon OR/AS, SSB3DS, Zelda ALBW, Senran Kagura (bc 0x21)
 * r43xxx - Codename S.T.E.A.M. (lastest revision at date of writing) (bc 0x22/0x23)
 *
 * Ported from OhanaCli.Formats.Models.BCH (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import { IOUtils } from '../../Core/IOUtils.js';
import * as PICACommand from '../PICA200/PICACommand.js';
import { PICACommandReader } from '../PICA200/PICACommandReader.js';
import type { Color } from '../PICA200/PICACommandReader.js';
import { MeshUtils } from '../Mesh/MeshUtils.js';
import {
  OModelGroup,
  OModel,
  OMesh,
  OVertex,
  OVector2,
  OVector3,
  OVector4,
  OMatrix,
  OBone,
  OMaterial,
  OTextureCoordinator,
  OTextureMapper,
} from '../../Core/RenderBase.js';

// ── BCH internal header types ──

interface BchHeader {
  magic: string;
  backwardCompatibility: number;
  forwardCompatibility: number;
  version: number;
  mainHeaderOffset: number;
  stringTableOffset: number;
  gpuCommandsOffset: number;
  dataOffset: number;
  dataExtendedOffset: number;
  relocationTableOffset: number;
  mainHeaderLength: number;
  stringTableLength: number;
  gpuCommandsLength: number;
  dataLength: number;
  dataExtendedLength: number;
  relocationTableLength: number;
  uninitializedDataSectionLength: number;
  uninitializedDescriptionSectionLength: number;
  flags: number;
  addressCount: number;
}

interface BchContentHeader {
  modelsPointerTableOffset: number;
  modelsPointerTableEntries: number;
  modelsNameOffset: number;
  materialsPointerTableOffset: number;
  materialsPointerTableEntries: number;
  materialsNameOffset: number;
  shadersPointerTableOffset: number;
  shadersPointerTableEntries: number;
  shadersNameOffset: number;
  texturesPointerTableOffset: number;
  texturesPointerTableEntries: number;
  texturesNameOffset: number;
  materialsLUTPointerTableOffset: number;
  materialsLUTPointerTableEntries: number;
  materialsLUTNameOffset: number;
  lightsPointerTableOffset: number;
  lightsPointerTableEntries: number;
  lightsNameOffset: number;
  camerasPointerTableOffset: number;
  camerasPointerTableEntries: number;
  camerasNameOffset: number;
  fogsPointerTableOffset: number;
  fogsPointerTableEntries: number;
  fogsNameOffset: number;
  skeletalAnimationsPointerTableOffset: number;
  skeletalAnimationsPointerTableEntries: number;
  skeletalAnimationsNameOffset: number;
  materialAnimationsPointerTableOffset: number;
  materialAnimationsPointerTableEntries: number;
  materialAnimationsNameOffset: number;
  visibilityAnimationsPointerTableOffset: number;
  visibilityAnimationsPointerTableEntries: number;
  visibilityAnimationsNameOffset: number;
  lightAnimationsPointerTableOffset: number;
  lightAnimationsPointerTableEntries: number;
  lightAnimationsNameOffset: number;
  cameraAnimationsPointerTableOffset: number;
  cameraAnimationsPointerTableEntries: number;
  cameraAnimationsNameOffset: number;
  fogAnimationsPointerTableOffset: number;
  fogAnimationsPointerTableEntries: number;
  fogAnimationsNameOffset: number;
  scenePointerTableOffset: number;
  scenePointerTableEntries: number;
  sceneNameOffset: number;
}

interface BchObjectEntry {
  materialId: number;
  isSilhouette: boolean;
  nodeId: number;
  renderPriority: number;
  vshAttributesBufferCommandsOffset: number;
  vshAttributesBufferCommandsWordCount: number;
  facesHeaderOffset: number;
  facesHeaderEntries: number;
  vshExtraAttributesBufferCommandsOffset: number;
  vshExtraAttributesBufferCommandsWordCount: number;
  centerVector: OVector3;
  flagsOffset: number;
  boundingBoxOffset: number;
}

// ── Helper: writable buffer (replaces BinaryWriter) ──

/** Read a UInt32 at an offset without advancing position */
function peekU32(buf: Buffer, offset: number): number {
  return buf.readUInt32LE(offset);
}

/** Write a UInt32 at an offset */
function writeU32(buf: Buffer, offset: number, value: number): void {
  buf.writeUInt32LE(value >>> 0, offset);
}

// ── Helper factories ──

function createMatrix(): OMatrix {
  return new OMatrix();
}

function createVertex(): OVertex {
  const v = new OVertex();
  v.diffuseColor = 0xffffffff;
  return v;
}

function createMesh(): OMesh {
  return new OMesh();
}

function createModel(): OModel {
  const mdl = new OModel();
  mdl.minVector = new OVector3(Infinity, Infinity, Infinity);
  mdl.maxVector = new OVector3(-Infinity, -Infinity, -Infinity);
  return mdl;
}

function createModelGroup(): OModelGroup {
  return new OModelGroup();
}

// ── Main BCH class ──

export class BCH {
  /**
   * Loads a BCH file from a Buffer.
   * The buffer is mutated in-place during relocation table processing (like the C# version).
   */
  static load(input: BinaryReader): OModelGroup {
    // We need a mutable copy of the buffer for relocation patching
    const rawBuf = input.slice(0, input.length);
    const buf = Buffer.from(rawBuf); // mutable copy
    const data = BinaryReader.fromBuffer(buf);

    const models = createModelGroup();

    // Primary header
    const header: BchHeader = {
      magic: '', backwardCompatibility: 0, forwardCompatibility: 0, version: 0,
      mainHeaderOffset: 0, stringTableOffset: 0, gpuCommandsOffset: 0,
      dataOffset: 0, dataExtendedOffset: 0, relocationTableOffset: 0,
      mainHeaderLength: 0, stringTableLength: 0, gpuCommandsLength: 0,
      dataLength: 0, dataExtendedLength: 0, relocationTableLength: 0,
      uninitializedDataSectionLength: 0, uninitializedDescriptionSectionLength: 0,
      flags: 0, addressCount: 0,
    };

    header.magic = IOUtils.readString(data, 0);
    data.seekRelative(4);
    header.backwardCompatibility = data.readByte();
    header.forwardCompatibility = data.readByte();
    header.version = data.readUInt16();

    header.mainHeaderOffset = data.readUInt32();
    header.stringTableOffset = data.readUInt32();
    header.gpuCommandsOffset = data.readUInt32();
    header.dataOffset = data.readUInt32();
    if (header.backwardCompatibility > 0x20) header.dataExtendedOffset = data.readUInt32();
    header.relocationTableOffset = data.readUInt32();

    header.mainHeaderLength = data.readUInt32();
    header.stringTableLength = data.readUInt32();
    header.gpuCommandsLength = data.readUInt32();
    header.dataLength = data.readUInt32();
    if (header.backwardCompatibility > 0x20) header.dataExtendedLength = data.readUInt32();
    header.relocationTableLength = data.readUInt32();

    header.uninitializedDataSectionLength = data.readUInt32();
    header.uninitializedDescriptionSectionLength = data.readUInt32();

    if (header.backwardCompatibility > 7) {
      header.flags = data.readUInt16();
      header.addressCount = data.readUInt16();
    }

    // Relocation: transform relative offsets to absolute offsets
    for (let o = header.relocationTableOffset; o < header.relocationTableOffset + header.relocationTableLength; o += 4) {
      data.seek(o);
      const value = data.readUInt32();
      const offset = value & 0x1ffffff;
      const flags = (value >>> 25) & 0x7f;

      switch (flags) {
        case 0: {
          const addr = (offset * 4) + header.mainHeaderOffset;
          const peeked = peekU32(buf, addr);
          writeU32(buf, addr, (peeked + header.mainHeaderOffset) >>> 0);
          break;
        }
        case 1: {
          const addr = offset + header.mainHeaderOffset;
          const peeked = peekU32(buf, addr);
          writeU32(buf, addr, (peeked + header.stringTableOffset) >>> 0);
          break;
        }
        case 2: {
          const addr = (offset * 4) + header.mainHeaderOffset;
          const peeked = peekU32(buf, addr);
          writeU32(buf, addr, (peeked + header.gpuCommandsOffset) >>> 0);
          break;
        }
        case 7:
        case 0xc: {
          const addr = (offset * 4) + header.mainHeaderOffset;
          const peeked = peekU32(buf, addr);
          writeU32(buf, addr, (peeked + header.dataOffset) >>> 0);
          break;
        }
      }

      // GPU commands relocation
      const gpuAddr = (offset * 4) + header.gpuCommandsOffset;
      if (gpuAddr + 4 <= buf.length) {
        const peeked = peekU32(buf, gpuAddr);
        if (header.backwardCompatibility < 6) {
          switch (flags) {
            case 0x23: writeU32(buf, gpuAddr, (peeked + header.dataOffset) >>> 0); break;
            case 0x25: writeU32(buf, gpuAddr, (peeked + header.dataOffset) >>> 0); break;
            case 0x26: writeU32(buf, gpuAddr, (((peeked + header.dataOffset) & 0x7fffffff) | 0x80000000) >>> 0); break;
            case 0x27: writeU32(buf, gpuAddr, ((peeked + header.dataOffset) & 0x7fffffff) >>> 0); break;
          }
        } else if (header.backwardCompatibility < 8) {
          switch (flags) {
            case 0x24: writeU32(buf, gpuAddr, (peeked + header.dataOffset) >>> 0); break;
            case 0x26: writeU32(buf, gpuAddr, (peeked + header.dataOffset) >>> 0); break;
            case 0x27: writeU32(buf, gpuAddr, (((peeked + header.dataOffset) & 0x7fffffff) | 0x80000000) >>> 0); break;
            case 0x28: writeU32(buf, gpuAddr, ((peeked + header.dataOffset) & 0x7fffffff) >>> 0); break;
          }
        } else if (header.backwardCompatibility < 0x21) {
          switch (flags) {
            case 0x25: writeU32(buf, gpuAddr, (peeked + header.dataOffset) >>> 0); break;
            case 0x27: writeU32(buf, gpuAddr, (peeked + header.dataOffset) >>> 0); break;
            case 0x28: writeU32(buf, gpuAddr, (((peeked + header.dataOffset) & 0x7fffffff) | 0x80000000) >>> 0); break;
            case 0x29: writeU32(buf, gpuAddr, ((peeked + header.dataOffset) & 0x7fffffff) >>> 0); break;
          }
        } else {
          switch (flags) {
            case 0x25: writeU32(buf, gpuAddr, (peeked + header.dataOffset) >>> 0); break;
            case 0x26: writeU32(buf, gpuAddr, (peeked + header.dataOffset) >>> 0); break;
            case 0x27: writeU32(buf, gpuAddr, (((peeked + header.dataOffset) & 0x7fffffff) | 0x80000000) >>> 0); break;
            case 0x28: writeU32(buf, gpuAddr, ((peeked + header.dataOffset) & 0x7fffffff) >>> 0); break;
            case 0x2b: writeU32(buf, gpuAddr, (peeked + header.dataExtendedOffset) >>> 0); break;
            case 0x2c: writeU32(buf, gpuAddr, (((peeked + header.dataExtendedOffset) & 0x7fffffff) | 0x80000000) >>> 0); break;
            case 0x2d: writeU32(buf, gpuAddr, ((peeked + header.dataExtendedOffset) & 0x7fffffff) >>> 0); break;
          }
        }
      }
    }

    // Re-create reader from patched buffer
    const rd = BinaryReader.fromBuffer(buf);

    // Content header
    rd.seek(header.mainHeaderOffset);
    const ch: BchContentHeader = {
      modelsPointerTableOffset: rd.readUInt32(),
      modelsPointerTableEntries: rd.readUInt32(),
      modelsNameOffset: rd.readUInt32(),
      materialsPointerTableOffset: rd.readUInt32(),
      materialsPointerTableEntries: rd.readUInt32(),
      materialsNameOffset: rd.readUInt32(),
      shadersPointerTableOffset: rd.readUInt32(),
      shadersPointerTableEntries: rd.readUInt32(),
      shadersNameOffset: rd.readUInt32(),
      texturesPointerTableOffset: rd.readUInt32(),
      texturesPointerTableEntries: rd.readUInt32(),
      texturesNameOffset: rd.readUInt32(),
      materialsLUTPointerTableOffset: rd.readUInt32(),
      materialsLUTPointerTableEntries: rd.readUInt32(),
      materialsLUTNameOffset: rd.readUInt32(),
      lightsPointerTableOffset: rd.readUInt32(),
      lightsPointerTableEntries: rd.readUInt32(),
      lightsNameOffset: rd.readUInt32(),
      camerasPointerTableOffset: rd.readUInt32(),
      camerasPointerTableEntries: rd.readUInt32(),
      camerasNameOffset: rd.readUInt32(),
      fogsPointerTableOffset: rd.readUInt32(),
      fogsPointerTableEntries: rd.readUInt32(),
      fogsNameOffset: rd.readUInt32(),
      skeletalAnimationsPointerTableOffset: rd.readUInt32(),
      skeletalAnimationsPointerTableEntries: rd.readUInt32(),
      skeletalAnimationsNameOffset: rd.readUInt32(),
      materialAnimationsPointerTableOffset: rd.readUInt32(),
      materialAnimationsPointerTableEntries: rd.readUInt32(),
      materialAnimationsNameOffset: rd.readUInt32(),
      visibilityAnimationsPointerTableOffset: rd.readUInt32(),
      visibilityAnimationsPointerTableEntries: rd.readUInt32(),
      visibilityAnimationsNameOffset: rd.readUInt32(),
      lightAnimationsPointerTableOffset: rd.readUInt32(),
      lightAnimationsPointerTableEntries: rd.readUInt32(),
      lightAnimationsNameOffset: rd.readUInt32(),
      cameraAnimationsPointerTableOffset: rd.readUInt32(),
      cameraAnimationsPointerTableEntries: rd.readUInt32(),
      cameraAnimationsNameOffset: rd.readUInt32(),
      fogAnimationsPointerTableOffset: rd.readUInt32(),
      fogAnimationsPointerTableEntries: rd.readUInt32(),
      fogAnimationsNameOffset: rd.readUInt32(),
      scenePointerTableOffset: rd.readUInt32(),
      scenePointerTableEntries: rd.readUInt32(),
      sceneNameOffset: rd.readUInt32(),
    };

    // Textures (decode step skipped - requires TextureCodec which is out of scope)
    // We still iterate to read the texture metadata for the OModelGroup
    for (let index = 0; index < ch.texturesPointerTableEntries; index++) {
      rd.seek(ch.texturesPointerTableOffset + (index * 4));
      const dataOff = rd.readUInt32();
      rd.seek(dataOff);

      const texUnit0CommandsOffset = rd.readUInt32();
      const texUnit0CommandsWordCount = rd.readUInt32();
      rd.readUInt32(); rd.readUInt32(); // texUnit1
      rd.readUInt32(); rd.readUInt32(); // texUnit2
      rd.readUInt32();
      const textureName = readString(rd);

      rd.seek(texUnit0CommandsOffset);
      const textureCommands = new PICACommandReader(rd, texUnit0CommandsWordCount);
      const textureSize = textureCommands.getTexUnit0Size();

      // We store a placeholder texture entry
      models.texture.push({
        name: textureName ?? '',
        width: textureSize.width,
        height: textureSize.height,
        format: textureCommands.getTexUnit0Format(),
        address: textureCommands.getTexUnit0Address(),
      } as any);
    }

    // Models
    for (let modelIndex = 0; modelIndex < ch.modelsPointerTableEntries; modelIndex++) {
      const model = createModel();

      rd.seek(ch.modelsPointerTableOffset + (modelIndex * 4));
      const objectsHeaderOffset = rd.readUInt32();

      rd.seek(objectsHeaderOffset);

      const modelFlags = rd.readByte();
      const skeletonScalingType = rd.readByte();
      const silhouetteMaterialEntries = rd.readUInt16();

      const wt = createMatrix();
      wt.M11 = rd.readFloat(); wt.M21 = rd.readFloat(); wt.M31 = rd.readFloat(); wt.M41 = rd.readFloat();
      wt.M12 = rd.readFloat(); wt.M22 = rd.readFloat(); wt.M32 = rd.readFloat(); wt.M42 = rd.readFloat();
      wt.M13 = rd.readFloat(); wt.M23 = rd.readFloat(); wt.M33 = rd.readFloat(); wt.M43 = rd.readFloat();

      const materialsTableOffset = rd.readUInt32();
      const materialsTableEntries = rd.readUInt32();
      const materialsNameOffset = rd.readUInt32();
      const verticesTableOffset = rd.readUInt32();
      const verticesTableEntries = rd.readUInt32();
      rd.seekRelative(header.backwardCompatibility > 6 ? 0x28 : 0x20);
      const skeletonOffset = rd.readUInt32();
      const skeletonEntries = rd.readUInt32();
      const skeletonNameOffset = rd.readUInt32();
      const objectsNodeVisibilityOffset = rd.readUInt32();
      const objectsNodeCount = rd.readUInt32();
      const modelName = readString(rd);
      const objectsNodeNameEntries = rd.readUInt32();
      const objectsNodeNameOffset = rd.readUInt32();
      rd.readUInt32(); // 0x0
      const metaDataPointerOffset = rd.readUInt32();

      model.transform = wt;
      model.name = modelName ?? '';

      // Object names (Patricia tree)
      const objectName: string[] = new Array(objectsNodeNameEntries);
      rd.seek(objectsNodeNameOffset);
      rd.readInt32(); // rootReferenceBit
      rd.readUInt16(); // rootLeftNode
      rd.readUInt16(); // rootRightNode
      rd.readUInt32(); // rootNameOffset
      for (let i = 0; i < objectsNodeNameEntries; i++) {
        rd.readInt32(); // referenceBit
        rd.readUInt16(); // leftNode
        rd.readUInt16(); // rightNode
        objectName[i] = readString(rd) ?? '';
      }

      // Materials (simplified - we store names and basic info)
      for (let index = 0; index < materialsTableEntries; index++) {
        if (header.backwardCompatibility < 0x21)
          rd.seek(materialsTableOffset + (index * 0x58));
        else
          rd.seek(materialsTableOffset + (index * 0x2c));

        const materialParametersOffset = rd.readUInt32();
        rd.readUInt32(); rd.readUInt32(); rd.readUInt32();
        const textureCommandsOffset = rd.readUInt32();
        const textureCommandsWordCount = rd.readUInt32();

        let materialMapperOffset = 0;
        if (header.backwardCompatibility < 0x21) {
          materialMapperOffset = rd.position;
          rd.seekRelative(0x30);
        } else {
          materialMapperOffset = rd.readUInt32();
        }

        const name0 = readString(rd) ?? '';
        const name1 = readString(rd) ?? '';
        const name2 = readString(rd) ?? '';
        const matName = readString(rd) ?? '';

        const material = new OMaterial();
        material.name = matName;
        material.name0 = name0;
        material.name1 = name1;
        material.name2 = name2;
        // Reset textureCoordinator scale to 1 (OMaterial constructor sets different defaults)
        for (let i = 0; i < 3; i++) {
          material.textureCoordinator[i].scaleU = 1;
          material.textureCoordinator[i].scaleV = 1;
        }

        // Read texture coordinators from parameters section
        if (materialParametersOffset !== 0) {
          rd.seek(materialParametersOffset);
          rd.readUInt32(); // hash
          rd.readUInt16(); // materialFlags
          rd.readUInt16(); // fragmentFlags
          rd.readUInt32();

          for (let i = 0; i < 3; i++) {
            const projectionAndCamera = rd.readUInt32();
            material.textureCoordinator[i].scaleU = rd.readFloat();
            material.textureCoordinator[i].scaleV = rd.readFloat();
            material.textureCoordinator[i].rotate = rd.readFloat();
            material.textureCoordinator[i].translateU = rd.readFloat();
            material.textureCoordinator[i].translateV = rd.readFloat();
          }
        }

        // Read mapper
        rd.seek(materialMapperOffset);
        for (let i = 0; i < 3; i++) {
          const wrapAndMagFilter = rd.readUInt32();
          rd.readUInt32(); // levelOfDetailAndMinFilter
          rd.readFloat(); // LODBias
          rd.readUInt32(); // borderColor (4 bytes)
          material.textureMapper[i].wrapU = (wrapAndMagFilter >>> 8) & 0xff;
          material.textureMapper[i].wrapV = (wrapAndMagFilter >>> 16) & 0xff;
        }

        model.material.push(material);
      }

      // Skeleton
      rd.seek(skeletonOffset);
      for (let index = 0; index < skeletonEntries; index++) {
        const boneFlags = rd.readUInt32();
        const parentId = rd.readInt16();
        rd.readUInt16(); // spacer
        const scale = new OVector3(rd.readFloat(), rd.readFloat(), rd.readFloat());
        const rotation = new OVector3(rd.readFloat(), rd.readFloat(), rd.readFloat());
        const translation = new OVector3(rd.readFloat(), rd.readFloat(), rd.readFloat());
        const absoluteScale = new OVector3(scale.x, scale.y, scale.z);

        // Inverse transform matrix (12 floats, 3x4)
        for (let k = 0; k < 12; k++) rd.readFloat();

        const boneName = readString(rd) ?? '';
        rd.readUInt32(); // metaDataPointerOffset

        const bone = new OBone();
        bone.name = boneName;
        bone.parentId = parentId;
        bone.translation = translation;
        bone.rotation = rotation;
        bone.scale = scale;
        bone.absoluteScale = absoluteScale;
        model.skeleton.push(bone);
      }

      // Skeleton transform computation
      const skeletonTransform: OMatrix[] = [];
      for (let index = 0; index < skeletonEntries; index++) {
        const transform = transformSkeleton(model.skeleton, index, createMatrix());
        skeletonTransform.push(transform);
      }

      rd.seek(objectsNodeVisibilityOffset);
      const nodeVisibility = rd.readUInt32();

      // Vertices header
      rd.seek(verticesTableOffset);
      const objects: BchObjectEntry[] = [];

      for (let index = 0; index < verticesTableEntries; index++) {
        const obj: BchObjectEntry = {
          materialId: rd.readUInt16(),
          isSilhouette: false,
          nodeId: 0, renderPriority: 0,
          vshAttributesBufferCommandsOffset: 0, vshAttributesBufferCommandsWordCount: 0,
          facesHeaderOffset: 0, facesHeaderEntries: 0,
          vshExtraAttributesBufferCommandsOffset: 0, vshExtraAttributesBufferCommandsWordCount: 0,
          centerVector: new OVector3(),
          flagsOffset: 0, boundingBoxOffset: 0,
        };
        const fl = rd.readUInt16();
        if (header.backwardCompatibility !== 8) obj.isSilhouette = (fl & 1) > 0;
        obj.nodeId = rd.readUInt16();
        obj.renderPriority = rd.readUInt16();
        obj.vshAttributesBufferCommandsOffset = rd.readUInt32();
        obj.vshAttributesBufferCommandsWordCount = rd.readUInt32();
        obj.facesHeaderOffset = rd.readUInt32();
        obj.facesHeaderEntries = rd.readUInt32();
        obj.vshExtraAttributesBufferCommandsOffset = rd.readUInt32();
        obj.vshExtraAttributesBufferCommandsWordCount = rd.readUInt32();
        obj.centerVector = new OVector3(rd.readFloat(), rd.readFloat(), rd.readFloat());
        obj.flagsOffset = rd.readUInt32();
        rd.readUInt32();
        obj.boundingBoxOffset = rd.readUInt32();
        objects.push(obj);
      }

      for (let objIndex = 0; objIndex < objects.length; objIndex++) {
        if (objects[objIndex].isSilhouette) continue;

        const obj = createMesh();
        obj.materialId = objects[objIndex].materialId;
        if (objects[objIndex].nodeId < objectName.length) {
          obj.name = objectName[objects[objIndex].nodeId];
        } else {
          obj.name = 'mesh' + objIndex;
        }
        obj.isVisible = (nodeVisibility & (1 << objects[objIndex].nodeId)) > 0;

        // Vertices
        rd.seek(objects[objIndex].vshAttributesBufferCommandsOffset);
        const vshCommands = new PICACommandReader(rd, objects[objIndex].vshAttributesBufferCommandsWordCount);

        const vshAttributesUniformReg6 = vshCommands.getVSHFloatUniformData(6);
        const vshAttributesUniformReg7 = vshCommands.getVSHFloatUniformData(7);
        const positionOffset = new OVector4(
          vshAttributesUniformReg6.pop() ?? 0,
          vshAttributesUniformReg6.pop() ?? 0,
          vshAttributesUniformReg6.pop() ?? 0,
          vshAttributesUniformReg6.pop() ?? 0,
        );
        const texture0Scale = vshAttributesUniformReg7.pop() ?? 1;
        const texture1Scale = vshAttributesUniformReg7.pop() ?? 1;
        const texture2Scale = vshAttributesUniformReg7.pop() ?? 1;
        const boneWeightScale = vshAttributesUniformReg7.pop() ?? 1;
        const positionScale = vshAttributesUniformReg7.pop() ?? 1;
        const normalScale = vshAttributesUniformReg7.pop() ?? 1;
        const tangentScale = vshAttributesUniformReg7.pop() ?? 1;
        const colorScale = vshAttributesUniformReg7.pop() ?? 1;

        // Faces
        let facesCount = objects[objIndex].facesHeaderEntries;
        let hasFaces = facesCount > 0;
        let facesTableOffset = 0;
        if (!hasFaces) {
          rd.seek(verticesTableOffset + verticesTableEntries * 0x38);
          rd.seekRelative(objIndex * 0x1c + 0x10);
          facesTableOffset = rd.readUInt32();
          facesCount = rd.readUInt32();
        }

        for (let f = 0; f < facesCount; f++) {
          let skinningMode = 0; // none
          const nodeList: number[] = [];
          let idxBufferOffset: number;
          let idxBufferFormat: PICACommand.indexBufferFormat;
          let idxBufferTotalVertices: number;

          if (hasFaces) {
            const baseOff = objects[objIndex].facesHeaderOffset + f * 0x34;
            rd.seek(baseOff);
            skinningMode = rd.readUInt16();
            const nodeIdEntries = rd.readUInt16();
            for (let n = 0; n < nodeIdEntries; n++) nodeList.push(rd.readUInt16());

            rd.seek(baseOff + 0x2c);
            const faceHeaderOffset = rd.readUInt32();
            const faceHeaderWordCount = rd.readUInt32();
            rd.seek(faceHeaderOffset);
            const idxCommands = new PICACommandReader(rd, faceHeaderWordCount);
            idxBufferOffset = idxCommands.getIndexBufferAddress();
            idxBufferFormat = idxCommands.getIndexBufferFormat();
            idxBufferTotalVertices = idxCommands.getIndexBufferTotalVertices();
          } else {
            rd.seek(facesTableOffset + f * 8);
            idxBufferOffset = rd.readUInt32();
            idxBufferFormat = PICACommand.indexBufferFormat.unsignedShort;
            idxBufferTotalVertices = rd.readUInt32();
          }

          const vshAttributesBufferOffset = vshCommands.getVSHAttributesBufferAddress(0);
          const vshAttributesBufferStride = vshCommands.getVSHAttributesBufferStride(0);
          const vshTotalAttributes = vshCommands.getVSHTotalAttributes(0);
          const vshMainPerm = vshCommands.getVSHAttributesBufferPermutation();
          const vshPerm = vshCommands.getVSHAttributesBufferPermutationByIndex(0);
          const vshFmt = vshCommands.getVSHAttributesBufferFormat();

          for (let attribute = 0; attribute < vshTotalAttributes; attribute++) {
            switch (vshMainPerm[vshPerm[attribute]]) {
              case PICACommand.vshAttribute.normal: obj.hasNormal = true; break;
              case PICACommand.vshAttribute.tangent: obj.hasTangent = true; break;
              case PICACommand.vshAttribute.color: obj.hasColor = true; break;
              case PICACommand.vshAttribute.textureCoordinate0: obj.texUVCount = Math.max(obj.texUVCount, 1); break;
              case PICACommand.vshAttribute.textureCoordinate1: obj.texUVCount = Math.max(obj.texUVCount, 2); break;
              case PICACommand.vshAttribute.textureCoordinate2: obj.texUVCount = Math.max(obj.texUVCount, 3); break;
            }
          }

          if (nodeList.length > 0) {
            obj.hasNode = true;
            obj.hasWeight = true;
          }

          rd.seek(idxBufferOffset);
          for (let faceIndex = 0; faceIndex < idxBufferTotalVertices; faceIndex++) {
            let index = 0;
            switch (idxBufferFormat) {
              case PICACommand.indexBufferFormat.unsignedShort: index = rd.readUInt16(); break;
              case PICACommand.indexBufferFormat.unsignedByte: index = rd.readByte(); break;
            }

            const dataPosition = rd.position;
            const vertexOff = vshAttributesBufferOffset + (index * vshAttributesBufferStride);
            rd.seek(vertexOff);

            const vertex = createVertex();
            for (let attribute = 0; attribute < vshTotalAttributes; attribute++) {
              const att = vshMainPerm[vshPerm[attribute]];
              const format: PICACommand.attributeFormat = { ...vshFmt[vshPerm[attribute]] };
              if (att === PICACommand.vshAttribute.boneWeight) format.type = PICACommand.attributeFormatType.unsignedByte;
              const vector = getVector(rd, format);

              switch (att) {
                case PICACommand.vshAttribute.position: {
                  const x = (vector.x * positionScale) + positionOffset.x;
                  const y = (vector.y * positionScale) + positionOffset.y;
                  const z = (vector.z * positionScale) + positionOffset.z;
                  vertex.position = new OVector3(x, y, z);
                  break;
                }
                case PICACommand.vshAttribute.normal:
                  vertex.normal = new OVector3(vector.x * normalScale, vector.y * normalScale, vector.z * normalScale);
                  break;
                case PICACommand.vshAttribute.tangent:
                  vertex.tangent = new OVector3(vector.x * tangentScale, vector.y * tangentScale, vector.z * tangentScale);
                  break;
                case PICACommand.vshAttribute.color: {
                  const r = MeshUtils.saturate((vector.x * colorScale) * 0xff);
                  const g = MeshUtils.saturate((vector.y * colorScale) * 0xff);
                  const b = MeshUtils.saturate((vector.z * colorScale) * 0xff);
                  const a = MeshUtils.saturate((vector.w * colorScale) * 0xff);
                  vertex.diffuseColor = (b | (g << 8) | (r << 16) | (a << 24)) >>> 0;
                  break;
                }
                case PICACommand.vshAttribute.textureCoordinate0:
                  vertex.texture0 = new OVector2(vector.x * texture0Scale, vector.y * texture0Scale);
                  break;
                case PICACommand.vshAttribute.textureCoordinate1:
                  vertex.texture1 = new OVector2(vector.x * texture1Scale, vector.y * texture1Scale);
                  break;
                case PICACommand.vshAttribute.textureCoordinate2:
                  vertex.texture2 = new OVector2(vector.x * texture2Scale, vector.y * texture2Scale);
                  break;
                case PICACommand.vshAttribute.boneIndex:
                  if (nodeList.length > 0) vertex.node.push(nodeList[Math.floor(vector.x)]);
                  if (skinningMode === 1) { // smoothSkinning
                    if (format.attributeLength > 0 && nodeList.length > 0) vertex.node.push(nodeList[Math.floor(vector.y)]);
                    if (format.attributeLength > 1 && nodeList.length > 0) vertex.node.push(nodeList[Math.floor(vector.z)]);
                    if (format.attributeLength > 2 && nodeList.length > 0) vertex.node.push(nodeList[Math.floor(vector.w)]);
                  }
                  break;
                case PICACommand.vshAttribute.boneWeight:
                  vertex.weight.push(vector.x * boneWeightScale);
                  if (skinningMode === 1) {
                    if (format.attributeLength > 0) vertex.weight.push(vector.y * boneWeightScale);
                    if (format.attributeLength > 1) vertex.weight.push(vector.z * boneWeightScale);
                    if (format.attributeLength > 2) vertex.weight.push(vector.w * boneWeightScale);
                  }
                  break;
              }
            }

            if (vertex.node.length === 0 && nodeList.length <= 4) {
              for (let n = 0; n < nodeList.length; n++) vertex.node.push(nodeList[n]);
              if (vertex.weight.length === 0) vertex.weight.push(1);
            }

            if (skinningMode !== 1 && vertex.node.length > 0) {
              if (vertex.weight.length === 0) vertex.weight.push(1);
              if (vertex.node[0] < skeletonTransform.length) {
                vertex.position = transformVector3(vertex.position, skeletonTransform[vertex.node[0]]);
              }
            }

            MeshUtils.calculateBounds(model, vertex);
            obj.vertices.push(vertex);
            rd.seek(dataPosition);
          }
        }

        model.mesh.push(obj);
      }

      // Scale skeleton
      for (let index = 0; index < skeletonEntries; index++) {
        scaleSkeleton(model.skeleton, index, index);
      }

      models.model.push(model);
    }

    return models;
  }
}

// ── Private helper functions ──

function readString(input: BinaryReader): string | null {
  const offset = input.readUInt32();
  if (offset !== 0) return IOUtils.readString(input, offset);
  return null;
}

function getVector(input: BinaryReader, format: PICACommand.attributeFormat): OVector4 {
  const output = new OVector4();

  switch (format.type) {
    case PICACommand.attributeFormatType.signedByte:
      output.x = input.readSByte();
      if (format.attributeLength > 0) output.y = input.readSByte();
      if (format.attributeLength > 1) output.z = input.readSByte();
      if (format.attributeLength > 2) output.w = input.readSByte();
      break;
    case PICACommand.attributeFormatType.unsignedByte:
      output.x = input.readByte();
      if (format.attributeLength > 0) output.y = input.readByte();
      if (format.attributeLength > 1) output.z = input.readByte();
      if (format.attributeLength > 2) output.w = input.readByte();
      break;
    case PICACommand.attributeFormatType.signedShort:
      output.x = input.readInt16();
      if (format.attributeLength > 0) output.y = input.readInt16();
      if (format.attributeLength > 1) output.z = input.readInt16();
      if (format.attributeLength > 2) output.w = input.readInt16();
      break;
    case PICACommand.attributeFormatType.single:
      output.x = input.readFloat();
      if (format.attributeLength > 0) output.y = input.readFloat();
      if (format.attributeLength > 1) output.z = input.readFloat();
      if (format.attributeLength > 2) output.w = input.readFloat();
      break;
  }

  return output;
}

function scaleSkeleton(skeleton: OBone[], index: number, parentIndex: number): void {
  if (index !== parentIndex) {
    skeleton[parentIndex].absoluteScale.x *= skeleton[index].scale.x;
    skeleton[parentIndex].absoluteScale.y *= skeleton[index].scale.y;
    skeleton[parentIndex].absoluteScale.z *= skeleton[index].scale.z;

    skeleton[parentIndex].translation.x *= skeleton[index].scale.x;
    skeleton[parentIndex].translation.y *= skeleton[index].scale.y;
    skeleton[parentIndex].translation.z *= skeleton[index].scale.z;
  }

  if (skeleton[index].parentId > -1) scaleSkeleton(skeleton, skeleton[index].parentId, parentIndex);
}

function transformSkeleton(skeleton: OBone[], index: number, target: OMatrix): OMatrix {
  const bone = skeleton[index];

  let result = mulMatrix(target, matScale(bone.scale));
  result = mulMatrix(result, matRotateX(bone.rotation.x));
  result = mulMatrix(result, matRotateY(bone.rotation.y));
  result = mulMatrix(result, matRotateZ(bone.rotation.z));
  result = mulMatrix(result, matTranslate(bone.translation));

  if (bone.parentId > -1) result = transformSkeleton(skeleton, bone.parentId, result);
  return result;
}

/** Transform a vector3 by a 4x4 matrix */
function transformVector3(v: OVector3, m: OMatrix): OVector3 {
  return new OVector3(
    v.x * m.M11 + v.y * m.M21 + v.z * m.M31 + m.M41,
    v.x * m.M12 + v.y * m.M22 + v.z * m.M32 + m.M42,
    v.x * m.M13 + v.y * m.M23 + v.z * m.M33 + m.M43,
  );
}

// ── Simple 4x4 matrix helpers ──

function matScale(v: OVector3): OMatrix {
  return OMatrix.scaleVec3(v);
}

function matTranslate(v: OVector3): OMatrix {
  return OMatrix.translateVec3(v);
}

function matRotateX(angle: number): OMatrix {
  return OMatrix.rotateX(angle);
}

function matRotateY(angle: number): OMatrix {
  return OMatrix.rotateY(angle);
}

function matRotateZ(angle: number): OMatrix {
  return OMatrix.rotateZ(angle);
}

/** Multiply two matrices, returning the result. */
function mulMatrix(a: OMatrix, b: OMatrix): OMatrix {
  return OMatrix.mul(a, b);
}
