/**
 * Game Freak model parser (Pokemon Sun/Moon).
 * Ported from OhanaCli.Formats.Models.PocketMonsters.GfModel (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import { IOUtils } from '../../Core/IOUtils.js';
import {
  OVector2,
  OVector3,
  OVector4,
  OMatrix,
  OBone,
  OVertex,
  OMesh,
  OMaterial,
  OModel,
  OModelGroup,
  OTextureCoordinator,
  OTextureMapper,
} from '../../Core/RenderBase.js';
import * as PICACommand from '../PICA200/PICACommand.js';
import { PICACommandReader } from '../PICA200/PICACommandReader.js';
import { MeshUtils } from '../Mesh/MeshUtils.js';
import { GfTexture } from '../../Textures/PocketMonsters/GfTexture.js';

// Re-export the RenderBase types so downstream files that import from GfModel still work
export {
  OVector2,
  OVector3,
  OVector4,
  OMatrix,
  OBone,
  OVertex,
  OMesh,
  OMaterial,
  OModel,
  OModelGroup,
} from '../../Core/RenderBase.js';

// ── Factory helpers ──

function createVertex(): OVertex {
  const v = new OVertex();
  v.diffuseColor = 0xffffffff;
  return v;
}

function createMesh(): OMesh {
  return new OMesh();
}

function createMaterial(): OMaterial {
  const mat = new OMaterial();
  // Reset textureCoordinator to simpler defaults (scale=1)
  for (let i = 0; i < 3; i++) {
    mat.textureCoordinator[i].scaleU = 1;
    mat.textureCoordinator[i].scaleV = 1;
  }
  return mat;
}

function createModel(): OModel {
  const mdl = new OModel();
  mdl.name = 'model';
  mdl.minVector = new OVector3(Infinity, Infinity, Infinity);
  mdl.maxVector = new OVector3(-Infinity, -Infinity, -Infinity);
  return mdl;
}

export function createModelGroup(): OModelGroup {
  return new OModelGroup();
}

// ── Sub-mesh info ──

interface SubMeshInfo {
  cmdBuffers: PICACommandReader[];
  nodeLists: number[][];
  vtxLengths: number[];
  idxLengths: number[];
  names: string[];
  count: number;
}

// ── Main class ──

export let DiagnosticLogging = false;

function log(message: string): void {
  if (DiagnosticLogging) console.error(message);
}

export class GfModel {
  /** Loads a Pokemon Sun/Moon Model from a BinaryReader. */
  static load(data: BinaryReader): OModelGroup {
    const mdls = createModelGroup();
    const input = data;

    input.readUInt32();

    const sectionsCnt: number[] = new Array(5);
    for (let i = 0; i < 5; i++) {
      sectionsCnt[i] = input.readUInt32();
    }

    let baseAddr = input.position;

    const MODEL_SECT = 0;
    const TEXTURE_SECT = 1;

    for (let sect = 0; sect < 5; sect++) {
      const count = sectionsCnt[sect];

      for (let i = 0; i < count; i++) {
        input.seek(baseAddr + i * 4);
        input.seek(input.readUInt32());

        const nameStrLen = input.readByte();
        const name = IOUtils.readStringWithLength(input, nameStrLen);
        const descAddress = input.readUInt32();

        input.seek(descAddress);

        switch (sect) {
          case MODEL_SECT: {
            const mdl = GfModel.loadModel(data);
            mdl.name = name;
            mdls.model.push(mdl);
            break;
          }
          case TEXTURE_SECT: {
            const tex = GfTexture.load(data);
            if (tex) mdls.texture.push(tex);
            break;
          }
        }
      }

      baseAddr += count * 4;
    }

    return mdls;
  }

  /** Loads a single GfModel from the current reader position. */
  static loadModel(data: BinaryReader): OModel {
    const mdl = createModel();
    const input = data;

    const mdlStart = input.position;

    const preHeaderMagic = input.readUInt32();
    const preHeaderSectionsCount = input.readUInt32();
    input.readUInt32();
    input.readUInt32();

    log(`[GfModel] pre-header magic=0x${preHeaderMagic.toString(16).padStart(8, '0')} sections=${preHeaderSectionsCount}`);

    // gfmodel string (8 bytes)
    input.readUInt32();
    input.readUInt32();
    const mdlLength = input.readUInt32();
    input.readUInt32(); // -1

    log(`[GfModel] model length=0x${mdlLength.toString(16).padStart(8, '0')}`);

    const effectNames = getStrTable(input);
    const textureNames = getStrTable(input);
    const materialNames = getStrTable(input);
    const meshNames = getStrTable(input);

    input.seekRelative(0x20); // 2 float4

    mdl.transform.M11 = input.readFloat();
    mdl.transform.M12 = input.readFloat();
    mdl.transform.M13 = input.readFloat();
    mdl.transform.M14 = input.readFloat();
    mdl.transform.M21 = input.readFloat();
    mdl.transform.M22 = input.readFloat();
    mdl.transform.M23 = input.readFloat();
    mdl.transform.M24 = input.readFloat();
    mdl.transform.M31 = input.readFloat();
    mdl.transform.M32 = input.readFloat();
    mdl.transform.M33 = input.readFloat();
    mdl.transform.M34 = input.readFloat();
    mdl.transform.M41 = input.readFloat();
    mdl.transform.M42 = input.readFloat();
    mdl.transform.M43 = input.readFloat();
    mdl.transform.M44 = input.readFloat();

    const unkDataLen = input.readUInt32();
    const unkDataRelStart = input.readUInt32();
    input.readUInt32();
    input.readUInt32();

    input.seekRelative(unkDataRelStart + unkDataLen);

    const bonesCount = input.readUInt32();
    input.seekRelative(0xc);

    const boneNames: string[] = [];

    for (let b = 0; b < bonesCount; b++) {
      const boneName = IOUtils.readStringWithLength(input, input.readByte());
      const parentName = IOUtils.readStringWithLength(input, input.readByte());
      const _flags = input.readByte();

      const bone = new OBone();
      bone.name = boneName;
      bone.parentId = boneNames.indexOf(parentName);
      bone.scale = new OVector3(input.readFloat(), input.readFloat(), input.readFloat());
      bone.rotation = new OVector3(input.readFloat(), input.readFloat(), input.readFloat());
      bone.translation = new OVector3(input.readFloat(), input.readFloat(), input.readFloat());
      bone.absoluteScale = new OVector3(bone.scale.x, bone.scale.y, bone.scale.z);

      mdl.skeleton.push(bone);
      boneNames.push(boneName);
    }

    log(`[GfModel] bones=${mdl.skeleton.length} materials=${materialNames.length} meshes=${meshNames.length}`);

    // Materials
    const matMeshBinding: string[] = [];
    input.seek(mdlStart + mdlLength + 0x20);

    for (let m = 0; m < materialNames.length; m++) {
      const mat = createMaterial();
      mat.name = materialNames[m];

      // material magic (8 bytes)
      input.readUInt32();
      input.readUInt32();
      const matLength = input.readUInt32();
      input.readUInt32(); // -1

      const matStart = input.position;

      const unkNames: string[] = new Array(4);
      for (let n = 0; n < 4; n++) {
        input.readUInt32(); // maybeHash
        const nameLen = input.readByte();
        unkNames[n] = IOUtils.readStringWithLength(input, nameLen);
      }

      matMeshBinding.push(unkNames[0]);

      input.seekRelative(0xac);

      const textureCoordsStart = input.position;

      for (let unit = 0; unit < 3; unit++) {
        input.seek(textureCoordsStart + unit * 0x42);

        input.readUInt32(); // maybeHash
        const texName = IOUtils.readStringWithLength(input, input.readByte());

        if (texName === '') break;

        switch (unit) {
          case 0: mat.name0 = texName; break;
          case 1: mat.name1 = texName; break;
          case 2: mat.name2 = texName; break;
        }

        input.readUInt16(); // unitIdx

        mat.textureCoordinator[unit].scaleU = input.readFloat();
        mat.textureCoordinator[unit].scaleV = input.readFloat();
        mat.textureCoordinator[unit].rotate = input.readFloat();
        mat.textureCoordinator[unit].translateU = input.readFloat();
        mat.textureCoordinator[unit].translateV = input.readFloat();

        const texMapperU = input.readUInt32();
        const texMapperV = input.readUInt32();

        mat.textureMapper[unit].wrapU = texMapperU & 7;
        mat.textureMapper[unit].wrapV = texMapperV & 7;
      }

      mdl.material.push(mat);
      input.seek(matStart + matLength);
    }

    // Meshes
    for (let m = 0; m < meshNames.length; m++) {
      // mesh magic (8 bytes)
      input.readUInt32();
      input.readUInt32();
      const meshLength = input.readUInt32();
      input.readUInt32(); // -1

      const meshStart = input.position;

      input.seekRelative(0x80);

      const info = getSubMeshInfo(input);

      for (let sm = 0; sm < info.count; sm++) {
        const obj = createMesh();
        obj.isVisible = true;
        obj.name = info.names[sm];
        obj.materialId = matMeshBinding.indexOf(obj.name);

        const nodeList = info.nodeLists[sm];

        const vtxCmdReader = info.cmdBuffers[sm * 3 + 0];
        const idxCmdReader = info.cmdBuffers[sm * 3 + 2];

        const vshAttributesBufferStride = vtxCmdReader.getVSHAttributesBufferStride(0);
        const vshTotalAttributes = vtxCmdReader.getVSHTotalAttributes(0);
        const vshMainAttributesBufferPermutation = vtxCmdReader.getVSHAttributesBufferPermutation();
        const vshAttributesBufferPermutation = vtxCmdReader.getVSHAttributesBufferPermutationByIndex(0);
        const vshAttributesBufferFormat = vtxCmdReader.getVSHAttributesBufferFormat();

        for (let attribute = 0; attribute < vshTotalAttributes; attribute++) {
          switch (vshMainAttributesBufferPermutation[vshAttributesBufferPermutation[attribute]]) {
            case PICACommand.vshAttribute.normal: obj.hasNormal = true; break;
            case PICACommand.vshAttribute.tangent: obj.hasTangent = true; break;
            case PICACommand.vshAttribute.color: obj.hasColor = true; break;
            case PICACommand.vshAttribute.textureCoordinate0: obj.texUVCount = Math.max(obj.texUVCount, 1); break;
            case PICACommand.vshAttribute.textureCoordinate1: obj.texUVCount = Math.max(obj.texUVCount, 2); break;
            case PICACommand.vshAttribute.textureCoordinate2: obj.texUVCount = Math.max(obj.texUVCount, 3); break;
          }
        }

        const idxBufferFormat = idxCmdReader.getIndexBufferFormat();
        const idxBufferTotalVertices = idxCmdReader.getIndexBufferTotalVertices();

        obj.hasNode = true;
        obj.hasWeight = true;

        const vtxBufferStart = input.position;
        input.seekRelative(info.vtxLengths[sm]);
        const idxBufferStart = input.position;

        for (let faceIndex = 0; faceIndex < idxBufferTotalVertices; faceIndex++) {
          let index = 0;
          switch (idxBufferFormat) {
            case PICACommand.indexBufferFormat.unsignedShort: index = input.readUInt16(); break;
            case PICACommand.indexBufferFormat.unsignedByte: index = input.readByte(); break;
          }

          const dataPosition = input.position;
          const vertexOffset = vtxBufferStart + (index * vshAttributesBufferStride);
          input.seek(vertexOffset);

          const vertex = createVertex();
          vertex.diffuseColor = 0xffffffff;
          vertex.weight.push(1, 0, 0, 0);

          for (let attribute = 0; attribute < vshTotalAttributes; attribute++) {
            const att = vshMainAttributesBufferPermutation[vshAttributesBufferPermutation[attribute]];
            const format: PICACommand.attributeFormat = { ...vshAttributesBufferFormat[vshAttributesBufferPermutation[attribute]] };
            if (att === PICACommand.vshAttribute.boneWeight) format.type = PICACommand.attributeFormatType.unsignedByte;
            const vector = getVector(input, format);

            switch (att) {
              case PICACommand.vshAttribute.position:
                vertex.position = new OVector3(vector.x, vector.y, vector.z);
                break;
              case PICACommand.vshAttribute.normal:
                vertex.normal = new OVector3(vector.x, vector.y, vector.z);
                break;
              case PICACommand.vshAttribute.tangent:
                vertex.tangent = new OVector3(vector.x, vector.y, vector.z);
                break;
              case PICACommand.vshAttribute.color: {
                const r = MeshUtils.saturate(vector.x);
                const g = MeshUtils.saturate(vector.y);
                const b = MeshUtils.saturate(vector.z);
                const a = MeshUtils.saturate(vector.w);
                vertex.diffuseColor = (b | (g << 8) | (r << 16) | (a << 24)) >>> 0;
                break;
              }
              case PICACommand.vshAttribute.textureCoordinate0:
                vertex.texture0 = new OVector2(vector.x, vector.y);
                break;
              case PICACommand.vshAttribute.textureCoordinate1:
                vertex.texture1 = new OVector2(vector.x, vector.y);
                break;
              case PICACommand.vshAttribute.textureCoordinate2:
                vertex.texture2 = new OVector2(vector.x, vector.y);
                break;
              case PICACommand.vshAttribute.boneIndex:
                addNode(vertex.node, nodeList, vector.x);
                if (format.attributeLength > 0) addNode(vertex.node, nodeList, vector.y);
                if (format.attributeLength > 1) addNode(vertex.node, nodeList, vector.z);
                if (format.attributeLength > 2) addNode(vertex.node, nodeList, vector.w);
                break;
              case PICACommand.vshAttribute.boneWeight:
                vertex.weight[0] = vector.x / 255;
                if (format.attributeLength > 0) vertex.weight[1] = vector.y / 255;
                if (format.attributeLength > 1) vertex.weight[2] = vector.z / 255;
                if (format.attributeLength > 2) vertex.weight[3] = vector.w / 255;
                break;
            }
          }

          if (vertex.node.length === 0 && nodeList.length <= 4) {
            for (let n = 0; n < nodeList.length; n++) vertex.node.push(nodeList[n]);
            if (vertex.weight.length === 0) vertex.weight.push(1);
          }

          MeshUtils.calculateBounds(mdl, vertex);
          obj.vertices.push(vertex);

          input.seek(dataPosition);
        }

        input.seek(idxBufferStart + info.idxLengths[sm]);
        mdl.mesh.push(obj);
      }

      input.seek(meshStart + meshLength);
    }

    log(`[GfModel] parsed meshCount=${mdl.mesh.length}`);

    return mdl;
  }
}

// ── Private helpers ──

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

function getStrTable(input: BinaryReader): string[] {
  const count = input.readUInt32();
  const baseAddress = input.position;

  const output: string[] = new Array(count);

  for (let i = 0; i < count; i++) {
    input.seek(baseAddress + i * 0x44);
    input.readUInt32(); // maybeHash
    output[i] = IOUtils.readStringWithLength(input, 0x40);
  }

  input.seek(baseAddress + count * 0x44);
  return output;
}

function getSubMeshInfo(input: BinaryReader): SubMeshInfo {
  const output: SubMeshInfo = {
    cmdBuffers: [],
    nodeLists: [],
    vtxLengths: [],
    idxLengths: [],
    names: [],
    count: 0,
  };

  let currCmdIdx = 0;
  let totalCmds = 0;

  while ((currCmdIdx + 1 < totalCmds) || currCmdIdx === 0) {
    const cmdLength = input.readUInt32();
    currCmdIdx = input.readInt32();
    totalCmds = input.readInt32();
    input.readInt32();

    output.cmdBuffers.push(new PICACommandReader(input, cmdLength / 4));
  }

  output.count = Math.floor(totalCmds / 3);

  for (let i = 0; i < output.count; i++) {
    input.readUInt32(); // maybeHash
    const subMeshNameLen = input.readUInt32();
    const subMeshNameStart = input.position;
    const name = IOUtils.readStringWithLength(input, subMeshNameLen);

    input.seek(subMeshNameStart + subMeshNameLen);

    const nodeListStart = input.position;
    const nodeListLen = input.readByte();
    const nodeList: number[] = new Array(nodeListLen);
    for (let n = 0; n < nodeListLen; n++) nodeList[n] = input.readByte();

    input.seek(nodeListStart + 0x20);

    input.readUInt32(); // vtxCount
    input.readUInt32(); // idxCount
    output.vtxLengths.push(input.readUInt32());
    output.idxLengths.push(input.readUInt32());

    output.names.push(name);
    output.nodeLists.push(nodeList);
  }

  return output;
}

function addNode(target: number[], nodeList: number[], nodeVal: number): void {
  if (nodeVal !== 0xff) target.push(nodeList[nodeVal]);
}
