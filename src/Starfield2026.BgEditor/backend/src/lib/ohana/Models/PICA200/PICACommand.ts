/**
 * PICA200 GPU command constants, enums, and types.
 * Ported from OhanaCli.Formats.Models.PICA200.PICACommand (C#).
 */

// Command register IDs
export const culling = 0x40;
export const polygonOffsetEnable = 0x4c;
export const polygonOffsetZScale = 0x4d;
export const polygonOffsetZBias = 0x4e;
export const texUnitsConfig = 0x80;
export const texUnit0BorderColor = 0x81;
export const texUnit0Size = 0x82;
export const texUnit0Param = 0x83;
export const texUnit0LevelOfDetail = 0x84;
export const texUnit0Address = 0x85;
export const texUnit0Type = 0x8e;
export const texUnit1BorderColor = 0x91;
export const texUnit1Size = 0x92;
export const texUnit1Param = 0x93;
export const texUnit1LevelOfDetail = 0x94;
export const texUnit1Address = 0x95;
export const texUnit1Type = 0x96;
export const texUnit2BorderColor = 0x99;
export const texUnit2Size = 0x9a;
export const texUnit2Param = 0x9b;
export const texUnit2LevelOfDetail = 0x9c;
export const texUnit2Address = 0x9d;
export const texUnit2Type = 0x9e;
export const tevStage0Source = 0xc0;
export const tevStage0Operand = 0xc1;
export const tevStage0Combine = 0xc2;
export const tevStage0Constant = 0xc3;
export const tevStage0Scale = 0xc4;
export const tevStage1Source = 0xc8;
export const tevStage1Operand = 0xc9;
export const tevStage1Combine = 0xca;
export const tevStage1Constant = 0xcb;
export const tevStage1Scale = 0xcc;
export const tevStage2Source = 0xd0;
export const tevStage2Operand = 0xd1;
export const tevStage2Combine = 0xd2;
export const tevStage2Constant = 0xd3;
export const tevStage2Scale = 0xd4;
export const tevStage3Source = 0xd8;
export const tevStage3Operand = 0xd9;
export const tevStage3Combine = 0xda;
export const tevStage3Constant = 0xdb;
export const tevStage3Scale = 0xdc;
export const fragmentBufferInput = 0xe0;
export const tevStage4Source = 0xf0;
export const tevStage4Operand = 0xf1;
export const tevStage4Combine = 0xf2;
export const tevStage4Constant = 0xf3;
export const tevStage4Scale = 0xf4;
export const tevStage5Source = 0xf8;
export const tevStage5Operand = 0xf9;
export const tevStage5Combine = 0xfa;
export const tevStage5Constant = 0xfb;
export const tevStage5Scale = 0xfc;
export const fragmentBufferColor = 0xfd;
export const colorOutputConfig = 0x100;
export const blendConfig = 0x101;
export const colorLogicOperationConfig = 0x102;
export const blendColor = 0x103;
export const alphaTestConfig = 0x104;
export const stencilTestConfig = 0x105;
export const stencilOperationConfig = 0x106;
export const depthTestConfig = 0x107;
export const cullModeConfig = 0x108;
export const frameBufferInvalidate = 0x110;
export const frameBufferFlush = 0x111;
export const colorBufferRead = 0x112;
export const colorBufferWrite = 0x113;
export const depthBufferRead = 0x114;
export const depthBufferWrite = 0x115;
export const depthTestConfig2 = 0x126;
export const fragmentShaderLookUpTableConfig = 0x1c5;
export const fragmentShaderLookUpTableData = 0x1c8;
export const lutSamplerAbsolute = 0x1d0;
export const lutSamplerInput = 0x1d1;
export const lutSamplerScale = 0x1d2;
export const vertexShaderAttributesBufferAddress = 0x200;
export const vertexShaderAttributesBufferFormatLow = 0x201;
export const vertexShaderAttributesBufferFormatHigh = 0x202;
export const vertexShaderAttributesBuffer0Address = 0x203;
export const vertexShaderAttributesBuffer0Permutation = 0x204;
export const vertexShaderAttributesBuffer0Stride = 0x205;
export const vertexShaderAttributesBuffer1Address = 0x206;
export const vertexShaderAttributesBuffer1Permutation = 0x207;
export const vertexShaderAttributesBuffer1Stride = 0x208;
export const vertexShaderAttributesBuffer2Address = 0x209;
export const vertexShaderAttributesBuffer2Permutation = 0x20a;
export const vertexShaderAttributesBuffer2Stride = 0x20b;
export const vertexShaderAttributesBuffer3Address = 0x20c;
export const vertexShaderAttributesBuffer3Permutation = 0x20d;
export const vertexShaderAttributesBuffer3Stride = 0x20e;
export const vertexShaderAttributesBuffer4Address = 0x20f;
export const vertexShaderAttributesBuffer4Permutation = 0x210;
export const vertexShaderAttributesBuffer4Stride = 0x211;
export const vertexShaderAttributesBuffer5Address = 0x212;
export const vertexShaderAttributesBuffer5Permutation = 0x213;
export const vertexShaderAttributesBuffer5Stride = 0x214;
export const vertexShaderAttributesBuffer6Address = 0x215;
export const vertexShaderAttributesBuffer6Permutation = 0x216;
export const vertexShaderAttributesBuffer6Stride = 0x217;
export const vertexShaderAttributesBuffer7Address = 0x218;
export const vertexShaderAttributesBuffer7Permutation = 0x219;
export const vertexShaderAttributesBuffer7Stride = 0x21a;
export const vertexShaderAttributesBuffer8Address = 0x21b;
export const vertexShaderAttributesBuffer8Permutation = 0x21c;
export const vertexShaderAttributesBuffer8Stride = 0x21d;
export const vertexShaderAttributesBuffer9Address = 0x21e;
export const vertexShaderAttributesBuffer9Permutation = 0x21f;
export const vertexShaderAttributesBuffer9Stride = 0x220;
export const vertexShaderAttributesBuffer10Address = 0x221;
export const vertexShaderAttributesBuffer10Permutation = 0x222;
export const vertexShaderAttributesBuffer10Stride = 0x223;
export const vertexShaderAttributesBuffer11Address = 0x224;
export const vertexShaderAttributesBuffer11Permutation = 0x225;
export const vertexShaderAttributesBuffer11Stride = 0x226;
export const indexBufferConfig = 0x227;
export const indexBufferTotalVertices = 0x228;
export const blockEnd = 0x23d;
export const vertexShaderTotalAttributes = 0x242;
export const vertexShaderBooleanUniforms = 0x2b0;
export const vertexShaderIntegerUniforms0 = 0x2b1;
export const vertexShaderIntegerUniforms1 = 0x2b2;
export const vertexShaderIntegerUniforms2 = 0x2b3;
export const vertexShaderIntegerUniforms3 = 0x2b4;
export const vertexShaderInputBufferConfig = 0x2b9;
export const vertexShaderEntryPoint = 0x2ba;
export const vertexShaderAttributesPermutationLow = 0x2bb;
export const vertexShaderAttributesPermutationHigh = 0x2bc;
export const vertexShaderOutmapMask = 0x2bd;
export const vertexShaderCodeTransferEnd = 0x2bf;
export const vertexShaderFloatUniformConfig = 0x2c0;
export const vertexShaderFloatUniformData = 0x2c1;

export enum vshAttribute {
  position = 0,
  normal = 1,
  tangent = 2,
  color = 3,
  textureCoordinate0 = 4,
  textureCoordinate1 = 5,
  textureCoordinate2 = 6,
  boneIndex = 7,
  boneWeight = 8,
  userAttribute0 = 9,
  userAttribute1 = 0xa,
  userAttribute2 = 0xb,
  userAttribute3 = 0xc,
  userAttribute4 = 0xd,
  userAttribute5 = 0xe,
  userAttribute6 = 0xf,
  userAttribute7 = 0x10,
  userAttribute8 = 0x11,
  userAttribute9 = 0x12,
  userAttribute10 = 0x13,
  userAttribute11 = 0x14,
  interleave = 0x15,
  quantity = 0x16,
}

export enum attributeFormatType {
  signedByte = 0,
  unsignedByte = 1,
  signedShort = 2,
  single = 3,
}

export interface attributeFormat {
  type: attributeFormatType;
  attributeLength: number;
}

export enum indexBufferFormat {
  unsignedByte = 0,
  unsignedShort = 1,
}

export interface fragmentSamplerAbsolute {
  r: boolean;
  g: boolean;
  b: boolean;
  d0: boolean;
  d1: boolean;
  fresnel: boolean;
}

export interface fragmentSamplerInput {
  r: number;
  g: number;
  b: number;
  d0: number;
  d1: number;
  fresnel: number;
}

export interface fragmentSamplerScale {
  r: number;
  g: number;
  b: number;
  d0: number;
  d1: number;
  fresnel: number;
}

export function createFragmentSamplerAbsolute(): fragmentSamplerAbsolute {
  return { r: false, g: false, b: false, d0: false, d1: false, fresnel: false };
}

export function createFragmentSamplerInput(): fragmentSamplerInput {
  return { r: 0, g: 0, b: 0, d0: 0, d1: 0, fresnel: 0 };
}

export function createFragmentSamplerScale(): fragmentSamplerScale {
  return { r: 0, g: 0, b: 0, d0: 0, d1: 0, fresnel: 0 };
}
