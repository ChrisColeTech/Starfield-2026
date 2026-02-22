import { Vector3f, TRBoundingBox, Sphere } from '../../Common/Math.js';

export enum TRIndexFormat {
  BYTE = 0,
  SHORT = 1,
  INT = 2,
  Count = 3
}

export enum TRVertexUsage {
  NONE = 0,
  POSITION = 1,
  NORMAL = 2,
  TANGENT = 3,
  BINORMAL = 4,
  COLOR = 5,
  TEX_COORD = 6,
  BLEND_INDEX = 7,
  BLEND_WEIGHTS = 8
}

export enum TRVertexFormat {
  NONE = 0,
  R8_G8_B8_A8_UNSIGNED_NORMALIZED = 20,
  W8_X8_Y8_Z8_UNSIGNED = 22,
  W32_X32_Y32_Z32_UNSIGNED = 52,
  W16_X16_Y16_Z16_UNSIGNED_NORMALIZED = 39,
  W16_X16_Y16_Z16_FLOAT = 43,
  X32_Y32_FLOAT = 48,
  X32_Y32_Z32_FLOAT = 51,
  W32_X32_Y32_Z32_FLOAT = 54
}

export class TRVertexElement {
  vertexElementSizeIndex: number = 0;
  vertexUsage: TRVertexUsage = TRVertexUsage.NONE;
  vertexElementLayer: number = 0;
  vertexFormat: TRVertexFormat = TRVertexFormat.NONE;
  vertexElementOffset: number = 0;
}

export class TRVertexElementSize {
  elementSize: number = 0;
}

export class TRVertexDeclaration {
  vertexElements: TRVertexElement[] = [];
  vertexElementSizes: TRVertexElementSize[] = [];
}

export class TRMeshPart {
  indexCount: number = 0;
  indexOffset: number = 0;
  Field_02: number = 0;
  MaterialName: string = '';
  vertexDeclarationIndex: number = 0;
}

export class TRBoneWeight {
  RigIndex: number = 0;
  RigWeight: number = 0;
}

export class TRMesh {
  Name: string = '';
  boundingBox: TRBoundingBox = new TRBoundingBox();
  IndexType: TRIndexFormat = TRIndexFormat.BYTE;
  vertexDeclaration: TRVertexDeclaration[] = [];
  meshParts: TRMeshPart[] = [];
  Field_05: number = 0;
  Field_06: number = 0;
  Field_07: number = 0;
  Field_08: number = 0;
  clipSphere: Sphere = new Sphere();
  boneWeight: TRBoneWeight[] = [];
  Field_11: string = '';
  Field_12: string = '';
}

export class TRMSH {
  Version: number = 0;
  Meshes: TRMesh[] = [];
  bufferFilePath: string = '';
}
