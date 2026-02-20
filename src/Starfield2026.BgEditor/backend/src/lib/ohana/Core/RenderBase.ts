/**
 * RenderBase -- all data structures for 3-D model rendering.
 * Ported from OhanaCli.Formats.RenderBase (C#, 2296 lines).
 *
 * C# Color (System.Drawing.Color) is represented as a plain number
 * (ARGB packed 32-bit unsigned int) throughout this port.
 *
 * C# Bitmap is replaced by a simple { width, height, data: Buffer } object
 * since we are server-side and have no System.Drawing.
 */

// ─── Color helper type ───────────────────────────────────────────────
/** ARGB packed 32-bit color (same layout as System.Drawing.Color). */
export type OColor = number;

// ─── Simple bitmap stand-in ──────────────────────────────────────────
export interface OBitmapData {
  width: number;
  height: number;
  data: Buffer;
}

// ──────────────────────────────────────────────────────────────────────
//  Vectors
// ──────────────────────────────────────────────────────────────────────

export class OVector2 {
  x: number;
  y: number;

  constructor(xOrVec?: number | OVector2, y?: number) {
    if (xOrVec instanceof OVector2) {
      this.x = xOrVec.x;
      this.y = xOrVec.y;
    } else {
      this.x = xOrVec ?? 0;
      this.y = y ?? 0;
    }
  }

  static eq(a: OVector2, b: OVector2): boolean {
    return a.x === b.x && a.y === b.y;
  }

  static neq(a: OVector2, b: OVector2): boolean {
    return !OVector2.eq(a, b);
  }

  toString(): string {
    return `X:${this.x}; Y:${this.y}`;
  }
}

export class OVector3 {
  x: number;
  y: number;
  z: number;

  constructor(xOrVec?: number | OVector3, y?: number, z?: number) {
    if (xOrVec instanceof OVector3) {
      this.x = xOrVec.x;
      this.y = xOrVec.y;
      this.z = xOrVec.z;
    } else {
      this.x = xOrVec ?? 0;
      this.y = y ?? 0;
      this.z = z ?? 0;
    }
  }

  static transform(input: OVector3, matrix: OMatrix): OVector3 {
    const output = new OVector3();
    output.x = input.x * matrix.M11 + input.y * matrix.M21 + input.z * matrix.M31 + matrix.M41;
    output.y = input.x * matrix.M12 + input.y * matrix.M22 + input.z * matrix.M32 + matrix.M42;
    output.z = input.x * matrix.M13 + input.y * matrix.M23 + input.z * matrix.M33 + matrix.M43;
    return output;
  }

  static mulScalar(a: OVector3, b: number): OVector3 {
    return new OVector3(a.x * b, a.y * b, a.z * b);
  }

  static mulVec(a: OVector3, b: OVector3): OVector3 {
    return new OVector3(a.x * b.x, a.y * b.y, a.z * b.z);
  }

  static divScalar(a: OVector3, b: number): OVector3 {
    return new OVector3(a.x / b, a.y / b, a.z / b);
  }

  static eq(a: OVector3, b: OVector3): boolean {
    return a.x === b.x && a.y === b.y && a.z === b.z;
  }

  static neq(a: OVector3, b: OVector3): boolean {
    return !OVector3.eq(a, b);
  }

  length(): number {
    return Math.sqrt(OVector3.dot(this, this));
  }

  normalize(): OVector3 {
    return OVector3.divScalar(this, this.length());
  }

  static dot(a: OVector3, b: OVector3): number {
    return a.x * b.x + a.y * b.y + a.z * b.z;
  }

  toString(): string {
    return `X:${this.x}; Y:${this.y}; Z:${this.z}`;
  }
}

export class OVector4 {
  x: number;
  y: number;
  z: number;
  w: number;

  constructor(
    xOrVecOrAxis?: number | OVector4 | OVector3,
    yOrAngle?: number,
    z?: number,
    w?: number,
  ) {
    if (xOrVecOrAxis instanceof OVector4) {
      this.x = xOrVecOrAxis.x;
      this.y = xOrVecOrAxis.y;
      this.z = xOrVecOrAxis.z;
      this.w = xOrVecOrAxis.w;
    } else if (xOrVecOrAxis instanceof OVector3) {
      // Quaternion from axis + angle
      const angle = yOrAngle ?? 0;
      this.x = Math.sin(angle * 0.5) * xOrVecOrAxis.x;
      this.y = Math.sin(angle * 0.5) * xOrVecOrAxis.y;
      this.z = Math.sin(angle * 0.5) * xOrVecOrAxis.z;
      this.w = Math.cos(angle * 0.5);
    } else {
      this.x = xOrVecOrAxis ?? 0;
      this.y = yOrAngle ?? 0;
      this.z = z ?? 0;
      this.w = w ?? 0;
    }
  }

  toEuler(): OVector3 {
    const output = new OVector3();
    output.z = Math.atan2(2 * (this.x * this.y + this.z * this.w), 1 - 2 * (this.y * this.y + this.z * this.z));
    output.y = -Math.asin(2 * (this.x * this.z - this.w * this.y));
    output.x = Math.atan2(2 * (this.x * this.w + this.y * this.z), -(1 - 2 * (this.z * this.z + this.w * this.w)));
    return output;
  }

  static mulScalar(a: OVector4, b: number): OVector4 {
    return new OVector4(a.x * b, a.y * b, a.z * b, a.w * b);
  }

  static mulVec(a: OVector4, b: OVector4): OVector4 {
    return new OVector4(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
  }

  static eq(a: OVector4, b: OVector4): boolean {
    return a.x === b.x && a.y === b.y && a.z === b.z && a.w === b.w;
  }

  static neq(a: OVector4, b: OVector4): boolean {
    return !OVector4.eq(a, b);
  }

  toString(): string {
    return `X:${this.x}; Y:${this.y}; Z:${this.z}; W:${this.w}`;
  }
}

// ──────────────────────────────────────────────────────────────────────
//  Matrix 4x4
// ──────────────────────────────────────────────────────────────────────

export class OMatrix {
  private m: number[][];

  constructor() {
    this.m = [
      [1, 0, 0, 0],
      [0, 1, 0, 0],
      [0, 0, 1, 0],
      [0, 0, 0, 1],
    ];
  }

  // Row 1
  get M11(): number { return this.m[0][0]; } set M11(v: number) { this.m[0][0] = v; }
  get M12(): number { return this.m[0][1]; } set M12(v: number) { this.m[0][1] = v; }
  get M13(): number { return this.m[0][2]; } set M13(v: number) { this.m[0][2] = v; }
  get M14(): number { return this.m[0][3]; } set M14(v: number) { this.m[0][3] = v; }
  // Row 2
  get M21(): number { return this.m[1][0]; } set M21(v: number) { this.m[1][0] = v; }
  get M22(): number { return this.m[1][1]; } set M22(v: number) { this.m[1][1] = v; }
  get M23(): number { return this.m[1][2]; } set M23(v: number) { this.m[1][2] = v; }
  get M24(): number { return this.m[1][3]; } set M24(v: number) { this.m[1][3] = v; }
  // Row 3
  get M31(): number { return this.m[2][0]; } set M31(v: number) { this.m[2][0] = v; }
  get M32(): number { return this.m[2][1]; } set M32(v: number) { this.m[2][1] = v; }
  get M33(): number { return this.m[2][2]; } set M33(v: number) { this.m[2][2] = v; }
  get M34(): number { return this.m[2][3]; } set M34(v: number) { this.m[2][3] = v; }
  // Row 4
  get M41(): number { return this.m[3][0]; } set M41(v: number) { this.m[3][0] = v; }
  get M42(): number { return this.m[3][1]; } set M42(v: number) { this.m[3][1] = v; }
  get M43(): number { return this.m[3][2]; } set M43(v: number) { this.m[3][2] = v; }
  get M44(): number { return this.m[3][3]; } set M44(v: number) { this.m[3][3] = v; }

  /** Indexer: get(col, row) */
  get(col: number, row: number): number { return this.m[col][row]; }
  /** Indexer: set(col, row, value) */
  set(col: number, row: number, value: number): void { this.m[col][row] = value; }

  static mul(a: OMatrix, b: OMatrix): OMatrix {
    const c = new OMatrix();
    for (let i = 0; i < 4; i++) {
      for (let j = 0; j < 4; j++) {
        let sum = 0;
        for (let k = 0; k < 4; k++) {
          sum += a.get(i, k) * b.get(k, j);
        }
        c.set(i, j, sum);
      }
    }
    return c;
  }

  invert(): OMatrix {
    const op: number[][] = Array.from({ length: 4 }, () => new Array(8).fill(0));

    for (let n = 0; n < 4; n++) {
      for (let mm = 0; mm < 4; mm++) {
        op[mm][n] = this.m[mm][n];
      }
    }

    // Identity on right side
    for (let n = 0; n < 4; n++) {
      for (let mm = 0; mm < 4; mm++) {
        op[mm][n + 4] = n === mm ? 1 : 0;
      }
    }

    for (let k = 0; k < 4; k++) {
      if (op[k][k] === 0) {
        let row = 0;
        for (let n = k; n < 4; n++) {
          if (op[n][k] !== 0) { row = n; break; }
        }
        for (let mm = k; mm < 8; mm++) {
          const temp = op[k][mm];
          op[k][mm] = op[row][mm];
          op[row][mm] = temp;
        }
      }

      const element = op[k][k];
      for (let n = k; n < 8; n++) op[k][n] /= element;
      for (let n = 0; n < 4; n++) {
        if (n === k && n === 3) break;
        if (n === k && n < 3) continue;

        if (op[n][k] !== 0) {
          const multiplier = op[n][k] / op[k][k];
          for (let mm = k; mm < 8; mm++) op[n][mm] -= op[k][mm] * multiplier;
        }
      }
    }

    const output = new OMatrix();
    output.M11 = op[0][4]; output.M12 = op[0][5]; output.M13 = op[0][6]; output.M14 = op[0][7];
    output.M21 = op[1][4]; output.M22 = op[1][5]; output.M23 = op[1][6]; output.M24 = op[1][7];
    output.M31 = op[2][4]; output.M32 = op[2][5]; output.M33 = op[2][6]; output.M34 = op[2][7];
    output.M41 = op[3][4]; output.M42 = op[3][5]; output.M43 = op[3][6]; output.M44 = op[3][7];
    return output;
  }

  static scaleVec3(s: OVector3): OMatrix {
    const o = new OMatrix();
    o.M11 = s.x; o.M22 = s.y; o.M33 = s.z;
    return o;
  }

  static scaleVec2(s: OVector2): OMatrix {
    const o = new OMatrix();
    o.M11 = s.x; o.M22 = s.y;
    return o;
  }

  static scaleUniform(s: number): OMatrix {
    const o = new OMatrix();
    o.M11 = s; o.M22 = s; o.M33 = s;
    return o;
  }

  static rotateX(angle: number): OMatrix {
    const o = new OMatrix();
    o.M22 = Math.cos(angle);
    o.M32 = -Math.sin(angle);
    o.M23 = Math.sin(angle);
    o.M33 = Math.cos(angle);
    return o;
  }

  static rotateY(angle: number): OMatrix {
    const o = new OMatrix();
    o.M11 = Math.cos(angle);
    o.M31 = Math.sin(angle);
    o.M13 = -Math.sin(angle);
    o.M33 = Math.cos(angle);
    return o;
  }

  static rotateZ(angle: number): OMatrix {
    const o = new OMatrix();
    o.M11 = Math.cos(angle);
    o.M21 = -Math.sin(angle);
    o.M12 = Math.sin(angle);
    o.M22 = Math.cos(angle);
    return o;
  }

  static translateVec3(position: OVector3): OMatrix {
    const o = new OMatrix();
    o.M41 = position.x; o.M42 = position.y; o.M43 = position.z;
    return o;
  }

  static translateVec2(position: OVector2): OMatrix {
    const o = new OMatrix();
    o.M31 = position.x; o.M32 = position.y;
    return o;
  }

  toString(): string {
    let s = '';
    for (let row = 0; row < 4; row++) {
      for (let col = 0; col < 4; col++) {
        s += `M${row + 1}${col + 1}: ${this.get(col, row)}  `;
      }
      s += '\n';
    }
    return s;
  }
}

// ──────────────────────────────────────────────────────────────────────
//  Vertex
// ──────────────────────────────────────────────────────────────────────

export class OVertex {
  position: OVector3 = new OVector3();
  normal: OVector3 = new OVector3();
  tangent: OVector3 = new OVector3();
  texture0: OVector2 = new OVector2();
  texture1: OVector2 = new OVector2();
  texture2: OVector2 = new OVector2();
  node: number[] = [];
  weight: number[] = [];
  diffuseColor: number = 0;

  constructor();
  constructor(vertex: OVertex);
  constructor(position: OVector3, normal: OVector3, texture0: OVector2, color: number);
  constructor(
    arg0?: OVertex | OVector3,
    arg1?: OVector3,
    arg2?: OVector2,
    arg3?: number,
  ) {
    if (arg0 instanceof OVertex) {
      const v = arg0;
      this.node = [...v.node];
      this.weight = [...v.weight];
      this.position = new OVector3(v.position);
      this.normal = new OVector3(v.normal);
      this.tangent = new OVector3(v.tangent);
      this.texture0 = new OVector2(v.texture0);
      this.texture1 = new OVector2(v.texture1);
      this.texture2 = new OVector2(v.texture2);
      this.diffuseColor = v.diffuseColor;
    } else if (arg0 instanceof OVector3 && arg1 instanceof OVector3 && arg2 instanceof OVector2) {
      this.position = new OVector3(arg0);
      this.normal = new OVector3(arg1);
      this.texture0 = new OVector2(arg2);
      this.diffuseColor = arg3 ?? 0;
    }
  }

  equals(vertex: OVertex): boolean {
    return (
      OVector3.eq(this.position, vertex.position) &&
      OVector3.eq(this.normal, vertex.normal) &&
      OVector3.eq(this.tangent, vertex.tangent) &&
      OVector2.eq(this.texture0, vertex.texture0) &&
      OVector2.eq(this.texture1, vertex.texture1) &&
      OVector2.eq(this.texture2, vertex.texture2) &&
      this.node.length === vertex.node.length &&
      this.node.every((v, i) => v === vertex.node[i]) &&
      this.weight.length === vertex.weight.length &&
      this.weight.every((v, i) => v === vertex.weight[i]) &&
      this.diffuseColor === vertex.diffuseColor
    );
  }
}

// ──────────────────────────────────────────────────────────────────────
//  Oriented Bounding Box
// ──────────────────────────────────────────────────────────────────────

export class OOrientedBoundingBox {
  name: string = '';
  centerPosition: OVector3 = new OVector3();
  orientationMatrix: OMatrix = new OMatrix();
  size: OVector3 = new OVector3();
}

// ──────────────────────────────────────────────────────────────────────
//  Enums
// ──────────────────────────────────────────────────────────────────────

export enum OTranslucencyKind {
  opaque = 0,
  translucent = 1,
  subtractive = 2,
  additive = 3,
}

export enum OSkinningMode {
  none = 0,
  smoothSkinning = 1,
  rigidSkinning = 2,
}

export enum OBillboardMode {
  off = 0,
  world = 2,
  worldViewpoint = 3,
  screen = 4,
  screenViewpoint = 5,
  yAxial = 6,
  yAxialViewpoint = 7,
}

export enum OCullMode {
  never = 0,
  frontFace = 1,
  backFace = 2,
}

export enum OTextureMinFilter {
  nearestMipmapNearest = 1,
  nearestMipmapLinear = 2,
  linearMipmapNearest = 4,
  linearMipmapLinear = 5,
}

export enum OTextureMagFilter {
  nearest = 0,
  linear = 1,
}

export enum OTextureWrap {
  clampToEdge = 0,
  clampToBorder = 1,
  repeat = 2,
  mirroredRepeat = 3,
}

export enum OTextureProjection {
  uvMap = 0,
  cameraCubeMap = 1,
  cameraSphereMap = 2,
  projectionMap = 3,
  shadowMap = 4,
  shadowCubeMap = 5,
}

export enum OConstantColor {
  constant0 = 0,
  constant1 = 1,
  constant2 = 2,
  constant3 = 3,
  constant4 = 4,
  constant5 = 5,
  emission = 6,
  ambient = 7,
  diffuse = 8,
  specular0 = 9,
  specular1 = 0xa,
}

export enum OCombineOperator {
  replace = 0,
  modulate = 1,
  add = 2,
  addSigned = 3,
  interpolate = 4,
  subtract = 5,
  dot3Rgb = 6,
  dot3Rgba = 7,
  multiplyAdd = 8,
  addMultiply = 9,
}

export enum OCombineSource {
  primaryColor = 0,
  fragmentPrimaryColor = 1,
  fragmentSecondaryColor = 2,
  texture0 = 3,
  texture1 = 4,
  texture2 = 5,
  texture3 = 6,
  previousBuffer = 0xd,
  constant = 0xe,
  previous = 0xf,
}

export enum OCombineOperandRgb {
  color = 0,
  oneMinusColor = 1,
  alpha = 2,
  oneMinusAlpha = 3,
  red = 4,
  oneMinusRed = 5,
  green = 8,
  oneMinusGreen = 9,
  blue = 0xc,
  oneMinusBlue = 0xd,
}

export enum OCombineOperandAlpha {
  alpha = 0,
  oneMinusAlpha = 1,
  red = 2,
  oneMinusRed = 3,
  green = 4,
  oneMinusGreen = 5,
  blue = 6,
  oneMinusBlue = 7,
}

export enum OBumpTexture {
  texture0 = 0,
  texture1 = 1,
  texture2 = 2,
  texture3 = 3,
}

export enum OBumpMode {
  notUsed = 0,
  asBump = 1,
  asTangent = 2,
}

export enum OFresnelConfig {
  none = 0,
  primary = 1,
  secondary = 2,
  primarySecondary = 3,
}

export enum OFragmentSamplerInput {
  halfNormalCosine = 0,
  halfViewCosine = 1,
  viewNormalCosine = 2,
  normalLightCosine = 3,
  spotLightCosine = 4,
  phiCosine = 5,
}

export enum OFragmentSamplerScale {
  one = 0,
  two = 1,
  four = 2,
  eight = 3,
  quarter = 6,
  half = 7,
}

export enum OTestFunction {
  never = 0,
  always = 1,
  equal = 2,
  notEqual = 3,
  less = 4,
  lessOrEqual = 5,
  greater = 6,
  greaterOrEqual = 7,
}

export enum OBlendMode {
  logical = 0,
  notUsed = 2,
  blend = 3,
}

export enum OLogicalOperation {
  clear = 0,
  and = 1,
  andReverse = 2,
  copy = 3,
  set = 4,
  copyInverted = 5,
  noOperation = 6,
  invert = 7,
  notAnd = 8,
  or = 9,
  notOr = 0xa,
  exclusiveOr = 0xb,
  equiv = 0xc,
  andInverted = 0xd,
  orReverse = 0xe,
  orInverted = 0xf,
}

export enum OBlendFunction {
  zero = 0,
  one = 1,
  sourceColor = 2,
  oneMinusSourceColor = 3,
  destinationColor = 4,
  oneMinusDestinationColor = 5,
  sourceAlpha = 6,
  oneMinusSourceAlpha = 7,
  destinationAlpha = 8,
  oneMinusDestinationAlpha = 9,
  constantColor = 0xa,
  oneMinusConstantColor = 0xb,
  constantAlpha = 0xc,
  oneMinusConstantAlpha = 0xd,
  sourceAlphaSaturate = 0xe,
}

export enum OBlendEquation {
  add = 0,
  subtract = 1,
  reverseSubtract = 2,
  min = 3,
  max = 4,
}

export enum OStencilOp {
  keep = 0,
  zero = 1,
  replace = 2,
  increase = 3,
  decrease = 4,
  increaseWrap = 5,
  decreaseWrap = 6,
}

export enum OMetaDataValueType {
  integer = 0,
  single = 1,
  utf8String = 2,
  utf16String = 3,
}

export enum OModelCullingMode {
  dynamic = 0,
  always = 1,
  never = 2,
}

export enum OTextureFormat {
  rgba8 = 0,
  rgb8 = 1,
  rgba5551 = 2,
  rgb565 = 3,
  rgba4 = 4,
  la8 = 5,
  hilo8 = 6,
  l8 = 7,
  a8 = 8,
  la4 = 9,
  l4 = 0xa,
  a4 = 0xb,
  etc1 = 0xc,
  etc1a4 = 0xd,
  dontCare = 0xe,
}

export enum OLightType {
  directional = 0,
  point = 1,
  spot = 2,
}

export enum OLightUse {
  hemiSphere = 0,
  vertex = 1,
  fragment = 2,
  ambient = 3,
}

export enum OCameraView {
  aimTarget = 0,
  lookAtTarget = 1,
  rotate = 2,
}

export enum OCameraProjection {
  perspective = 0,
  orthogonal = 1,
}

export enum OFogUpdater {
  linear = 0,
  exponent = 1,
  exponentSquare = 2,
}

export enum ORepeatMethod {
  none = 0,
  repeat = 1,
  mirror = 2,
  relativeRepeat = 3,
}

export enum OInterpolationMode {
  step = 0,
  linear = 1,
  hermite = 2,
}

export enum OSegmentType {
  single = 0,
  vector2 = 2,
  vector3 = 3,
  transform = 4,
  rgbaColor = 5,
  integer = 6,
  transformQuaternion = 7,
  boolean = 8,
  transformMatrix = 9,
}

export enum OSegmentQuantization {
  hermite128 = 0,
  hermite64 = 1,
  hermite48 = 2,
  unifiedHermite96 = 3,
  unifiedHermite48 = 4,
  unifiedHermite32 = 5,
  stepLinear64 = 6,
  stepLinear32 = 7,
}

export enum OLoopMode {
  oneTime = 0,
  loop = 1,
}

export enum OMaterialAnimationType {
  constant0 = 1,
  constant1 = 2,
  constant2 = 3,
  constant3 = 4,
  constant4 = 5,
  constant5 = 6,
  emission = 7,
  ambient = 8,
  diffuse = 9,
  specular0 = 0xa,
  specular1 = 0xb,
  borderColorMapper0 = 0xc,
  textureMapper0 = 0xd,
  borderColorMapper1 = 0xe,
  textureMapper1 = 0xf,
  borderColorMapper2 = 0x10,
  textureMapper2 = 0x11,
  blendColor = 0x12,
  scaleCoordinator0 = 0x13,
  rotateCoordinator0 = 0x14,
  translateCoordinator0 = 0x15,
  scaleCoordinator1 = 0x16,
  rotateCoordinator1 = 0x17,
  translateCoordinator1 = 0x18,
  scaleCoordinator2 = 0x19,
  rotateCoordinator2 = 0x1a,
  translateCoordinator2 = 0x1b,
}

export enum OLightAnimationType {
  transform = 0x1c,
  ambient = 0x1d,
  diffuse = 0x1e,
  specular0 = 0x1f,
  specular1 = 0x20,
  direction = 0x21,
  distanceAttenuationStart = 0x22,
  distanceAttenuationEnd = 0x23,
  isLightEnabled = 0x24,
}

export enum OCameraAnimationType {
  transform = 5,
  vuTargetPosition = 6,
  vuTwist = 7,
  vuUpwardVector = 8,
  vuViewRotate = 9,
  puNear = 0xa,
  puFar = 0xb,
  puFovy = 0xc,
  puAspectRatio = 0xd,
  puHeight = 0xe,
}

// ──────────────────────────────────────────────────────────────────────
//  Struct-like data classes (C# structs -> TS classes with defaults)
// ──────────────────────────────────────────────────────────────────────

export class OMaterialColor {
  emission: OColor = 0;
  ambient: OColor = 0;
  diffuse: OColor = 0;
  specular0: OColor = 0;
  specular1: OColor = 0;
  constant0: OColor = 0;
  constant1: OColor = 0;
  constant2: OColor = 0;
  constant3: OColor = 0;
  constant4: OColor = 0;
  constant5: OColor = 0;
  colorScale: number = 0;
}

export class ORasterization {
  cullMode: OCullMode = OCullMode.never;
  isPolygonOffsetEnabled: boolean = false;
  polygonOffsetUnit: number = 0;
}

export class OTextureCoordinator {
  projection: OTextureProjection = OTextureProjection.uvMap;
  referenceCamera: number = 0;
  scaleU: number = 0;
  scaleV: number = 0;
  rotate: number = 0;
  translateU: number = 0;
  translateV: number = 0;
}

export class OTextureMapper {
  minFilter: OTextureMinFilter = OTextureMinFilter.nearestMipmapNearest;
  magFilter: OTextureMagFilter = OTextureMagFilter.nearest;
  wrapU: OTextureWrap = OTextureWrap.clampToEdge;
  wrapV: OTextureWrap = OTextureWrap.clampToEdge;
  minLOD: number = 0;
  LODBias: number = 0;
  borderColor: OColor = 0;
}

export class OFragmentBump {
  texture: OBumpTexture = OBumpTexture.texture0;
  mode: OBumpMode = OBumpMode.notUsed;
  isBumpRenormalize: boolean = false;
}

export class OFragmentSampler {
  isAbsolute: boolean = false;
  input: OFragmentSamplerInput = OFragmentSamplerInput.halfNormalCosine;
  scale: OFragmentSamplerScale = OFragmentSamplerScale.one;
  samplerName: string = '';
  tableName: string = '';
}

export class OFragmentLighting {
  fresnelConfig: OFresnelConfig = OFresnelConfig.none;
  isClampHighLight: boolean = false;
  isDistribution0Enabled: boolean = false;
  isDistribution1Enabled: boolean = false;
  isGeometryFactor0Enabled: boolean = false;
  isGeometryFactor1Enabled: boolean = false;
  isReflectionEnabled: boolean = false;

  reflectanceRSampler: OFragmentSampler = new OFragmentSampler();
  reflectanceGSampler: OFragmentSampler = new OFragmentSampler();
  reflectanceBSampler: OFragmentSampler = new OFragmentSampler();
  distribution0Sampler: OFragmentSampler = new OFragmentSampler();
  distribution1Sampler: OFragmentSampler = new OFragmentSampler();
  fresnelSampler: OFragmentSampler = new OFragmentSampler();
}

export class OTextureCombiner {
  rgbScale: number = 0;
  alphaScale: number = 0;
  constantColor: OConstantColor = OConstantColor.constant0;
  combineRgb: OCombineOperator = OCombineOperator.replace;
  combineAlpha: OCombineOperator = OCombineOperator.replace;
  rgbSource: OCombineSource[] = new Array<OCombineSource>(3).fill(OCombineSource.primaryColor);
  rgbOperand: OCombineOperandRgb[] = new Array<OCombineOperandRgb>(3).fill(OCombineOperandRgb.color);
  alphaSource: OCombineSource[] = new Array<OCombineSource>(3).fill(OCombineSource.primaryColor);
  alphaOperand: OCombineOperandAlpha[] = new Array<OCombineOperandAlpha>(3).fill(OCombineOperandAlpha.alpha);
}

export class OAlphaTest {
  isTestEnabled: boolean = false;
  testFunction: OTestFunction = OTestFunction.never;
  testReference: number = 0;
}

export class OFragmentShader {
  layerConfig: number = 0;
  bufferColor: OColor = 0;
  bump: OFragmentBump = new OFragmentBump();
  lighting: OFragmentLighting = new OFragmentLighting();
  textureCombiner: OTextureCombiner[];
  alphaTest: OAlphaTest = new OAlphaTest();

  constructor() {
    this.textureCombiner = [];
    for (let i = 0; i < 6; i++) {
      this.textureCombiner.push(new OTextureCombiner());
    }
  }
}

export class ODepthOperation {
  isTestEnabled: boolean = false;
  testFunction: OTestFunction = OTestFunction.never;
  isMaskEnabled: boolean = false;
}

export class OBlendOperation {
  mode: OBlendMode = OBlendMode.logical;
  logicalOperation: OLogicalOperation = OLogicalOperation.clear;
  rgbFunctionSource: OBlendFunction = OBlendFunction.zero;
  rgbFunctionDestination: OBlendFunction = OBlendFunction.zero;
  rgbBlendEquation: OBlendEquation = OBlendEquation.add;
  alphaFunctionSource: OBlendFunction = OBlendFunction.zero;
  alphaFunctionDestination: OBlendFunction = OBlendFunction.zero;
  alphaBlendEquation: OBlendEquation = OBlendEquation.add;
  blendColor: OColor = 0;
}

export class OStencilOperation {
  isTestEnabled: boolean = false;
  testFunction: OTestFunction = OTestFunction.never;
  testReference: number = 0;
  testMask: number = 0;
  failOperation: OStencilOp = OStencilOp.keep;
  zFailOperation: OStencilOp = OStencilOp.keep;
  passOperation: OStencilOp = OStencilOp.keep;
}

export class OFragmentOperation {
  depth: ODepthOperation = new ODepthOperation();
  blend: OBlendOperation = new OBlendOperation();
  stencil: OStencilOperation = new OStencilOperation();
}

// ──────────────────────────────────────────────────────────────────────
//  References
// ──────────────────────────────────────────────────────────────────────

export class OReference {
  id: string = '';
  name: string = '';

  constructor(idOrFullName?: string, name?: string) {
    if (idOrFullName === undefined) return;
    if (name !== undefined) {
      // Two-arg form: id, name
      this.id = idOrFullName;
      this.name = name;
    } else {
      // Single-arg form: "id@name" or just "name"
      if (idOrFullName == null) return;
      if (idOrFullName.includes('@')) {
        const parts = idOrFullName.split('@');
        this.id = parts[0];
        this.name = parts[1];
      } else {
        this.name = idOrFullName;
      }
    }
  }

  toString(): string {
    return `${this.id}@${this.name}`;
  }
}

// ──────────────────────────────────────────────────────────────────────
//  Mesh
// ──────────────────────────────────────────────────────────────────────

export class OMesh {
  vertices: OVertex[] = [];
  materialId: number = 0;
  renderPriority: number = 0;
  name: string = '';
  isVisible: boolean = true;
  boundingBox: OOrientedBoundingBox = new OOrientedBoundingBox();

  hasNormal: boolean = false;
  hasTangent: boolean = false;
  hasColor: boolean = false;
  hasNode: boolean = false;
  hasWeight: boolean = false;
  texUVCount: number = 0;
}

// ──────────────────────────────────────────────────────────────────────
//  Bone
// ──────────────────────────────────────────────────────────────────────

export class OBone {
  translation: OVector3 = new OVector3();
  rotation: OVector3 = new OVector3();
  scale: OVector3 = new OVector3();
  absoluteScale: OVector3 = new OVector3();
  invTransform: OMatrix = new OMatrix();
  parentId: number = 0;
  name: string | null = null;

  billboardMode: OBillboardMode = OBillboardMode.off;
  isSegmentScaleCompensate: boolean = false;

  userData: OMetaData[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Meta Data
// ──────────────────────────────────────────────────────────────────────

export class OMetaData {
  name: string = '';
  type: OMetaDataValueType = OMetaDataValueType.integer;
  values: (number | string)[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Material
// ──────────────────────────────────────────────────────────────────────

export class OMaterial {
  name: string = 'material';
  name0: string = '';
  name1: string = '';
  name2: string = '';
  shaderReference: OReference = new OReference();
  modelReference: OReference = new OReference();
  userData: OMetaData[] = [];

  materialColor: OMaterialColor = new OMaterialColor();
  rasterization: ORasterization = new ORasterization();
  textureCoordinator: OTextureCoordinator[];
  textureMapper: OTextureMapper[];
  fragmentShader: OFragmentShader;
  fragmentOperation: OFragmentOperation;

  lightSetIndex: number = 0;
  fogIndex: number = 0;
  isFragmentLightEnabled: boolean = false;
  isVertexLightEnabled: boolean = false;
  isHemiSphereLightEnabled: boolean = false;
  isHemiSphereOcclusionEnabled: boolean = false;
  isFogEnabled: boolean = false;

  constructor() {
    this.textureCoordinator = [new OTextureCoordinator(), new OTextureCoordinator(), new OTextureCoordinator()];
    this.textureMapper = [new OTextureMapper(), new OTextureMapper(), new OTextureMapper()];
    this.fragmentShader = new OFragmentShader();
    this.fragmentOperation = new OFragmentOperation();

    this.fragmentShader.alphaTest.isTestEnabled = true;
    this.fragmentShader.alphaTest.testFunction = OTestFunction.greater;

    this.textureMapper[0].wrapU = OTextureWrap.repeat;
    this.textureMapper[0].wrapV = OTextureWrap.repeat;
    this.textureMapper[0].minFilter = OTextureMinFilter.linearMipmapLinear;
    this.textureMapper[0].magFilter = OTextureMagFilter.linear;

    for (let i = 0; i < 6; i++) {
      this.fragmentShader.textureCombiner[i].rgbSource[0] = OCombineSource.texture0;
      this.fragmentShader.textureCombiner[i].rgbSource[1] = OCombineSource.primaryColor;
      this.fragmentShader.textureCombiner[i].combineRgb = OCombineOperator.modulate;
      this.fragmentShader.textureCombiner[i].alphaSource[0] = OCombineSource.texture0;
      this.fragmentShader.textureCombiner[i].rgbScale = 1;
      this.fragmentShader.textureCombiner[i].alphaScale = 1;
    }

    this.fragmentOperation.depth.isTestEnabled = true;
    this.fragmentOperation.depth.testFunction = OTestFunction.lessOrEqual;
    this.fragmentOperation.depth.isMaskEnabled = true;
  }
}

// ──────────────────────────────────────────────────────────────────────
//  Model
// ──────────────────────────────────────────────────────────────────────

export class OModel {
  name: string = '';
  layerId: number = 0;
  mesh: OMesh[] = [];
  skeleton: OBone[] = [];
  material: OMaterial[] = [];
  userData: OMetaData[] = [];
  transform: OMatrix = new OMatrix();
  minVector: OVector3 = new OVector3();
  maxVector: OVector3 = new OVector3();

  get verticesCount(): number {
    let count = 0;
    for (const m of this.mesh) count += m.vertices.length;
    return count;
  }
}

// ──────────────────────────────────────────────────────────────────────
//  Texture
// ──────────────────────────────────────────────────────────────────────

export class OTexture {
  texture: OBitmapData;
  name: string;

  constructor(tex: OBitmapData, name: string) {
    this.texture = tex;
    this.name = name;
  }
}

// ──────────────────────────────────────────────────────────────────────
//  LookUp Table
// ──────────────────────────────────────────────────────────────────────

export class OLookUpTableSampler {
  name: string = '';
  table: number[] = new Array<number>(256).fill(0);
}

export class OLookUpTable {
  name: string = '';
  sampler: OLookUpTableSampler[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Light
// ──────────────────────────────────────────────────────────────────────

export class OLight {
  name: string = '';

  transformScale: OVector3 = new OVector3();
  transformRotate: OVector3 = new OVector3();
  transformTranslate: OVector3 = new OVector3();

  ambient: OColor = 0;
  diffuse: OColor = 0;
  specular0: OColor = 0;
  specular1: OColor = 0;
  direction: OVector3 = new OVector3();

  attenuationStart: number = 0;
  attenuationEnd: number = 0;

  isLightEnabled: boolean = false;
  isTwoSideDiffuse: boolean = false;
  isDistanceAttenuationEnabled: boolean = false;
  lightType: OLightType = OLightType.directional;
  lightUse: OLightUse = OLightUse.hemiSphere;

  // Vertex
  distanceAttenuationConstant: number = 0;
  distanceAttenuationLinear: number = 0;
  distanceAttenuationQuadratic: number = 0;
  spotExponent: number = 0;
  spotCutoffAngle: number = 0;

  // HemiSphere
  groundColor: OColor = 0;
  skyColor: OColor = 0;
  lerpFactor: number = 0;

  angleSampler: OFragmentSampler = new OFragmentSampler();
  distanceSampler: OFragmentSampler = new OFragmentSampler();
}

// ──────────────────────────────────────────────────────────────────────
//  Camera
// ──────────────────────────────────────────────────────────────────────

export class OCamera {
  name: string = '';

  transformScale: OVector3 = new OVector3();
  transformRotate: OVector3 = new OVector3();
  transformTranslate: OVector3 = new OVector3();
  target: OVector3 = new OVector3();
  rotation: OVector3 = new OVector3();
  upVector: OVector3 = new OVector3();
  twist: number = 0;
  view: OCameraView = OCameraView.aimTarget;
  projection: OCameraProjection = OCameraProjection.perspective;
  zNear: number = 0;
  zFar: number = 0;
  fieldOfViewY: number = 0;
  height: number = 0;
  aspectRatio: number = 0;

  isInheritingTargetRotate: boolean = false;
  isInheritingTargetTranslate: boolean = false;
  isInheritingUpRotate: boolean = false;
}

// ──────────────────────────────────────────────────────────────────────
//  Fog
// ──────────────────────────────────────────────────────────────────────

export class OFog {
  name: string = '';

  transformScale: OVector3 = new OVector3();
  transformRotate: OVector3 = new OVector3();
  transformTranslate: OVector3 = new OVector3();

  fogColor: OColor = 0;

  fogUpdater: OFogUpdater = OFogUpdater.linear;
  minFogDepth: number = 0;
  maxFogDepth: number = 0;
  fogDensity: number = 0;

  isZFlip: boolean = false;
  isAttenuateDistance: boolean = false;
}

// ──────────────────────────────────────────────────────────────────────
//  Animation
// ──────────────────────────────────────────────────────────────────────

export class OAnimationKeyFrame {
  frame: number = 0;
  value: number = 0;
  inSlope: number = 0;
  outSlope: number = 0;
  bValue: boolean = false;

  constructor();
  constructor(value: number, frame: number);
  constructor(value: number, inSlope: number, outSlope: number, frame: number);
  constructor(
    valueOrBool?: number | boolean,
    inSlopeOrFrame?: number,
    outSlope?: number,
    frame?: number,
  ) {
    if (valueOrBool === undefined) return;
    if (typeof valueOrBool === 'boolean') {
      this.bValue = valueOrBool;
      this.frame = inSlopeOrFrame ?? 0;
    } else if (outSlope !== undefined && frame !== undefined) {
      this.value = valueOrBool;
      this.inSlope = inSlopeOrFrame ?? 0;
      this.outSlope = outSlope;
      this.frame = frame;
    } else {
      this.value = valueOrBool;
      this.frame = inSlopeOrFrame ?? 0;
    }
  }

  toString(): string {
    return `Frame:${this.frame}; Value (float):${this.value}; Value (boolean):${this.bValue}; InSlope:${this.inSlope}; OutSlope:${this.outSlope}`;
  }
}

/** Create an OAnimationKeyFrame from a boolean value (since TS cannot overload constructors on type alone). */
export function createBoolKeyFrame(bValue: boolean, frame: number): OAnimationKeyFrame {
  const kf = new OAnimationKeyFrame();
  kf.bValue = bValue;
  kf.frame = frame;
  return kf;
}

export class OAnimationKeyFrameGroup {
  keyFrames: OAnimationKeyFrame[] = [];
  interpolation: OInterpolationMode = OInterpolationMode.step;
  startFrame: number = 0;
  endFrame: number = 0;
  exists: boolean = false;
  defaultValue: boolean = false;

  preRepeat: ORepeatMethod = ORepeatMethod.none;
  postRepeat: ORepeatMethod = ORepeatMethod.none;
}

export class OAnimationFrame {
  vector: OVector4[] = [];
  startFrame: number = 0;
  endFrame: number = 0;
  exists: boolean = false;

  preRepeat: ORepeatMethod = ORepeatMethod.none;
  postRepeat: ORepeatMethod = ORepeatMethod.none;
}

export class OAnimationBase {
  name: string = '';
  frameSize: number = 0;
  loopMode: OLoopMode = OLoopMode.oneTime;
}

export class OAnimationListBase {
  list: OAnimationBase[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Skeletal Animation
// ──────────────────────────────────────────────────────────────────────

export class OSkeletalAnimationBone {
  name: string = '';

  scaleX: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();
  scaleY: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();
  scaleZ: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();

  rotationX: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();
  rotationY: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();
  rotationZ: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();

  translationX: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();
  translationY: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();
  translationZ: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();
  isAxisAngle: boolean = false;

  rotationQuaternion: OAnimationFrame = new OAnimationFrame();
  translation: OAnimationFrame = new OAnimationFrame();
  scale: OAnimationFrame = new OAnimationFrame();
  isFrameFormat: boolean = false;

  transform: OMatrix[] = [];
  isFullBakedFormat: boolean = false;
}

export class OSkeletalAnimation extends OAnimationBase {
  bone: OSkeletalAnimationBone[] = [];
  userData: OMetaData[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Material Animation
// ──────────────────────────────────────────────────────────────────────

export class OMaterialAnimationData {
  name: string = '';
  type: OMaterialAnimationType = OMaterialAnimationType.constant0;
  frameList: OAnimationKeyFrameGroup[] = [];
}

export class OMaterialAnimation extends OAnimationBase {
  data: OMaterialAnimationData[] = [];
  textureName: string[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Visibility Animation
// ──────────────────────────────────────────────────────────────────────

export class OVisibilityAnimationData {
  name: string = '';
  visibilityList: OAnimationKeyFrameGroup = new OAnimationKeyFrameGroup();
}

export class OVisibilityAnimation extends OAnimationBase {
  data: OVisibilityAnimationData[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Light Animation
// ──────────────────────────────────────────────────────────────────────

export class OLightAnimationData {
  name: string = '';
  type: OLightAnimationType = OLightAnimationType.transform;
  frameList: OAnimationKeyFrameGroup[] = [];
}

export class OLightAnimation extends OAnimationBase {
  lightType: OLightType = OLightType.directional;
  lightUse: OLightUse = OLightUse.hemiSphere;
  data: OLightAnimationData[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Camera Animation
// ──────────────────────────────────────────────────────────────────────

export class OCameraAnimationData {
  name: string = '';
  type: OCameraAnimationType = OCameraAnimationType.transform;
  frameList: OAnimationKeyFrameGroup[] = [];
}

export class OCameraAnimation extends OAnimationBase {
  viewMode: OCameraView = OCameraView.aimTarget;
  projectionMode: OCameraProjection = OCameraProjection.perspective;
  data: OCameraAnimationData[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Fog Animation
// ──────────────────────────────────────────────────────────────────────

export class OFogAnimationData {
  name: string = '';
  colorList: OAnimationKeyFrameGroup[] = [];
}

export class OFogAnimation extends OAnimationBase {
  data: OFogAnimationData[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Scene
// ──────────────────────────────────────────────────────────────────────

export class OSceneReference {
  slotIndex: number = 0;
  name: string = '';
}

export class OScene {
  name: string = '';
  cameras: OSceneReference[] = [];
  lights: OSceneReference[] = [];
  fogs: OSceneReference[] = [];
}

// ──────────────────────────────────────────────────────────────────────
//  Model Group (top-level container)
// ──────────────────────────────────────────────────────────────────────

export class OModelGroup {
  model: OModel[] = [];
  texture: OTexture[] = [];
  lookUpTable: OLookUpTable[] = [];
  light: OLight[] = [];
  camera: OCamera[] = [];
  fog: OFog[] = [];
  skeletalAnimation: OAnimationListBase = new OAnimationListBase();
  materialAnimation: OAnimationListBase = new OAnimationListBase();
  visibilityAnimation: OAnimationListBase = new OAnimationListBase();
  lightAnimation: OAnimationListBase = new OAnimationListBase();
  cameraAnimation: OAnimationListBase = new OAnimationListBase();
  fogAnimation: OAnimationListBase = new OAnimationListBase();
  scene: OScene[] = [];

  merge(data: OModelGroup): void {
    this.model.push(...data.model);
    this.texture.push(...data.texture);
    this.lookUpTable.push(...data.lookUpTable);
    this.light.push(...data.light);
    this.camera.push(...data.camera);
    this.fog.push(...data.fog);
    this.skeletalAnimation.list.push(...data.skeletalAnimation.list);
    this.materialAnimation.list.push(...data.materialAnimation.list);
    this.visibilityAnimation.list.push(...data.visibilityAnimation.list);
    this.lightAnimation.list.push(...data.lightAnimation.list);
    this.cameraAnimation.list.push(...data.cameraAnimation.list);
    this.fogAnimation.list.push(...data.fogAnimation.list);
    this.scene.push(...data.scene);
  }
}
