/**
 * PICA200 Command Buffer reader.
 * Ported from OhanaCli.Formats.Models.PICA200.PICACommandReader (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import * as PICACommand from './PICACommand.js';

/** ARGB color as { r, g, b, a } with each channel 0-255 */
export interface Color {
  r: number;
  g: number;
  b: number;
  a: number;
}

/** Simple size record replacing System.Drawing.Size */
export interface Size {
  width: number;
  height: number;
}

export class PICACommandReader {
  private floatUniform: (number[] | null)[] = new Array(96).fill(null);
  private uniform: number[] = [];
  private lookUpTable: number[] = new Array(256).fill(0);
  private lutIndex: number = 0;
  private commands: Uint32Array = new Uint32Array(0x10000);
  private currentUniform: number = 0;

  /**
   * Creates a new PICA200 Command Buffer reader and reads the content.
   * @param input BinaryReader positioned at the buffer start
   * @param wordCount Total number of 32-bit words in the buffer
   * @param ignoreAlign Set to true to skip 0x0 padding alignment
   */
  constructor(input: BinaryReader, wordCount: number, ignoreAlign: boolean = false) {
    let readedWords = 0;
    while (readedWords < wordCount) {
      const parameter = input.readUInt32();
      const header = input.readUInt32();
      readedWords += 2;

      let id = header & 0xffff;
      const mask = (header >>> 16) & 0xf;
      const extraParameters = (header >>> 20) & 0x7ff;
      const consecutiveWriting = (header & 0x80000000) !== 0;

      this.commands[id] = ((this.getParameter(id) & (~mask & 0xf)) | (parameter & (0xfffffff0 | mask))) >>> 0;
      if (id === PICACommand.blockEnd) break;
      else if (id === PICACommand.vertexShaderFloatUniformConfig) this.currentUniform = parameter & 0x7fffffff;
      else if (id === PICACommand.vertexShaderFloatUniformData) this.uniform.push(this.toFloat(this.commands[id]));
      else if (id === PICACommand.fragmentShaderLookUpTableData) this.lookUpTable[this.lutIndex++] = this.commands[id];

      for (let i = 0; i < extraParameters; i++) {
        if (consecutiveWriting) id++;
        this.commands[id] = ((this.getParameter(id) & (~mask & 0xf)) | (input.readUInt32() & (0xfffffff0 | mask))) >>> 0;
        readedWords++;

        if (id > PICACommand.vertexShaderFloatUniformConfig && id < PICACommand.vertexShaderFloatUniformData + 8) {
          this.uniform.push(this.toFloat(this.commands[id]));
        } else if (id === PICACommand.fragmentShaderLookUpTableData) {
          this.lookUpTable[this.lutIndex++] = this.commands[id];
        }
      }

      if (this.uniform.length > 0) {
        if (this.floatUniform[this.currentUniform] === null) this.floatUniform[this.currentUniform] = [];
        this.floatUniform[this.currentUniform]!.push(...this.uniform);
        this.uniform.length = 0;
      }
      this.lutIndex = 0;

      if (!ignoreAlign) {
        while ((input.position & 7) !== 0) input.readUInt32();
      }
    }
  }

  /** Gets the latest written parameter of a given command ID. */
  getParameter(commandId: number): number {
    return this.commands[commandId];
  }

  /** Converts a uint (IEEE 754 encoded) to a JS float. */
  private toFloat(value: number): number {
    const buf = Buffer.alloc(4);
    buf.writeUInt32LE(value >>> 0, 0);
    return buf.readFloatLE(0);
  }

  /** Gets the total attributes minus 1 (no buffer index). */
  getVSHTotalAttributesGlobal(): number {
    return this.getParameter(PICACommand.vertexShaderTotalAttributes);
  }

  /** Gets the total attributes for a specific buffer. */
  getVSHTotalAttributes(bufferIndex: number): number {
    const value = this.getParameter(PICACommand.vertexShaderAttributesBuffer0Stride + (bufferIndex * 3));
    return value >>> 28;
  }

  /**
   * Gets an array containing the vertex shader attribute permutation order.
   * Uses BigInt internally for 64-bit permutation field.
   */
  getVSHAttributesBufferPermutation(): PICACommand.vshAttribute[] {
    const lo = BigInt(this.getParameter(PICACommand.vertexShaderAttributesPermutationLow) >>> 0);
    const hi = BigInt(this.getParameter(PICACommand.vertexShaderAttributesPermutationHigh) >>> 0);
    const permutation = lo | (hi << 32n);

    const attributes: PICACommand.vshAttribute[] = new Array(23);
    for (let attribute = 0; attribute < 23; attribute++) {
      attributes[attribute] = Number((permutation >> BigInt(attribute * 4)) & 0xfn) as PICACommand.vshAttribute;
    }
    return attributes;
  }

  /** Gets the main attributes buffer address. */
  getVSHAttributesBufferAddress(): number;
  /** Gets the address of a specific attributes buffer (0-11). */
  getVSHAttributesBufferAddress(bufferIndex: number): number;
  getVSHAttributesBufferAddress(bufferIndex?: number): number {
    if (bufferIndex === undefined) {
      return this.getParameter(PICACommand.vertexShaderAttributesBufferAddress);
    }
    return this.getParameter(PICACommand.vertexShaderAttributesBuffer0Address + (bufferIndex * 3));
  }

  /** Gets all attribute format entries from the main buffer. Uses BigInt for 64-bit field. */
  getVSHAttributesBufferFormat(): PICACommand.attributeFormat[] {
    const lo = BigInt(this.getParameter(PICACommand.vertexShaderAttributesBufferFormatLow) >>> 0);
    const hi = BigInt(this.getParameter(PICACommand.vertexShaderAttributesBufferFormatHigh) >>> 0);
    const format = lo | (hi << 32n);

    const formats: PICACommand.attributeFormat[] = new Array(23);
    for (let attribute = 0; attribute < 23; attribute++) {
      const value = Number((format >> BigInt(attribute * 4)) & 0xfn);
      formats[attribute] = {
        type: (value & 3) as PICACommand.attributeFormatType,
        attributeLength: value >>> 2,
      };
    }
    return formats;
  }

  /** Gets boolean uniforms used by the vertex shader. */
  getVSHBooleanUniforms(): boolean[] {
    const output: boolean[] = new Array(16);
    const value = this.getParameter(PICACommand.vertexShaderBooleanUniforms);
    for (let i = 0; i < 16; i++) output[i] = (value & (1 << i)) > 0;
    return output;
  }

  /** Gets permutation of a specific attributes buffer (0-11). Uses BigInt for 48-bit field. */
  getVSHAttributesBufferPermutationByIndex(bufferIndex: number): number[] {
    const lo = BigInt(this.getParameter(PICACommand.vertexShaderAttributesBuffer0Permutation + (bufferIndex * 3)) >>> 0);
    const hi = BigInt(this.getParameter(PICACommand.vertexShaderAttributesBuffer0Stride + (bufferIndex * 3)) & 0xffff);
    const permutation = lo | (hi << 32n);

    const attributes: number[] = new Array(23);
    for (let attribute = 0; attribute < 23; attribute++) {
      attributes[attribute] = Number((permutation >> BigInt(attribute * 4)) & 0xfn);
    }
    return attributes;
  }

  /** Gets stride of a specific attributes buffer (0-11). */
  getVSHAttributesBufferStride(bufferIndex: number): number {
    const value = this.getParameter(PICACommand.vertexShaderAttributesBuffer0Stride + (bufferIndex * 3));
    return (value >>> 16) & 0xff;
  }

  /**
   * Gets the float uniform data array from the given register.
   * Returns values as an array in LIFO order (like a stack -- pop from end).
   */
  getVSHFloatUniformData(register: number): number[] {
    const data: number[] = [];
    const arr = this.floatUniform[register];
    if (arr) {
      for (const value of arr) data.push(value);
    }
    return data;
  }

  /** Gets the index buffer address. */
  getIndexBufferAddress(): number {
    return this.getParameter(PICACommand.indexBufferConfig) & 0x7fffffff;
  }

  /** Gets the index buffer format (byte or short). */
  getIndexBufferFormat(): PICACommand.indexBufferFormat {
    return this.getParameter(PICACommand.indexBufferConfig) >>> 31;
  }

  /** Gets the total number of indexed vertices. */
  getIndexBufferTotalVertices(): number {
    return this.getParameter(PICACommand.indexBufferTotalVertices);
  }

  /** Gets TEV Stage parameters for stages 0-5. */
  getTevStage(stage: number): {
    rgbSource: number[];
    alphaSource: number[];
    rgbOperand: number[];
    alphaOperand: number[];
    combineRgb: number;
    combineAlpha: number;
    rgbScale: number;
    alphaScale: number;
  } {
    let baseCommand = 0;
    switch (stage) {
      case 0: baseCommand = PICACommand.tevStage0Source; break;
      case 1: baseCommand = PICACommand.tevStage1Source; break;
      case 2: baseCommand = PICACommand.tevStage2Source; break;
      case 3: baseCommand = PICACommand.tevStage3Source; break;
      case 4: baseCommand = PICACommand.tevStage4Source; break;
      case 5: baseCommand = PICACommand.tevStage5Source; break;
      default: throw new Error('PICACommandReader: Invalid TevStage number!');
    }

    const source = this.getParameter(baseCommand);
    const operand = this.getParameter(baseCommand + 1);
    const combine = this.getParameter(baseCommand + 2);
    const scale = this.getParameter(baseCommand + 4);

    return {
      rgbSource: [source & 0xf, (source >>> 4) & 0xf, (source >>> 8) & 0xf],
      alphaSource: [(source >>> 16) & 0xf, (source >>> 20) & 0xf, (source >>> 24) & 0xf],
      rgbOperand: [operand & 0xf, (operand >>> 4) & 0xf, (operand >>> 8) & 0xf],
      alphaOperand: [(operand >>> 12) & 0xf, (operand >>> 16) & 0xf, (operand >>> 20) & 0xf],
      combineRgb: combine & 0xffff,
      combineAlpha: combine >>> 16,
      rgbScale: (scale & 0xffff) + 1,
      alphaScale: (scale >>> 16) + 1,
    };
  }

  /** Gets the fragment buffer color as ARGB { r, g, b, a }. */
  getFragmentBufferColor(): Color {
    const rgba = this.getParameter(PICACommand.fragmentBufferColor);
    return {
      r: rgba & 0xff,
      g: (rgba >>> 8) & 0xff,
      b: (rgba >>> 16) & 0xff,
      a: (rgba >>> 24) & 0xff,
    };
  }

  /** Gets blending operation parameters. */
  getBlendOperation(): {
    rgbFunctionSource: number;
    rgbFunctionDestination: number;
    alphaFunctionSource: number;
    alphaFunctionDestination: number;
    rgbBlendEquation: number;
    alphaBlendEquation: number;
  } {
    const value = this.getParameter(PICACommand.blendConfig);
    return {
      rgbFunctionSource: (value >>> 16) & 0xf,
      rgbFunctionDestination: (value >>> 20) & 0xf,
      alphaFunctionSource: (value >>> 24) & 0xf,
      alphaFunctionDestination: (value >>> 28) & 0xf,
      rgbBlendEquation: value & 0xff,
      alphaBlendEquation: (value >>> 8) & 0xff,
    };
  }

  /** Gets the logical operation applied to fragment colors. */
  getColorLogicOperation(): number {
    return this.getParameter(PICACommand.colorLogicOperationConfig) & 0xf;
  }

  /** Gets alpha test parameters. */
  getAlphaTest(): { isTestEnabled: boolean; testFunction: number; testReference: number } {
    const value = this.getParameter(PICACommand.alphaTestConfig);
    return {
      isTestEnabled: (value & 1) > 0,
      testFunction: (value >>> 4) & 0xf,
      testReference: (value >>> 8) & 0xff,
    };
  }

  /** Gets stencil test parameters. */
  getStencilTest(): {
    isTestEnabled: boolean;
    testFunction: number;
    testReference: number;
    testMask: number;
    failOperation: number;
    zFailOperation: number;
    passOperation: number;
  } {
    const test = this.getParameter(PICACommand.stencilTestConfig);
    const operation = this.getParameter(PICACommand.stencilOperationConfig);
    return {
      isTestEnabled: (test & 1) > 0,
      testFunction: (test >>> 4) & 0xf,
      testReference: (test >>> 16) & 0xff,
      testMask: test >>> 24,
      failOperation: operation & 0xf,
      zFailOperation: (operation >>> 4) & 0xf,
      passOperation: (operation >>> 8) & 0xf,
    };
  }

  /** Gets depth test parameters. */
  getDepthTest(): { isTestEnabled: boolean; testFunction: number; isMaskEnabled: boolean } {
    const value = this.getParameter(PICACommand.depthTestConfig);
    return {
      isTestEnabled: (value & 1) > 0,
      testFunction: (value >>> 4) & 0xf,
      isMaskEnabled: (value & 0x1000) > 0,
    };
  }

  /** Gets the culling mode. */
  getCullMode(): number {
    return this.getParameter(PICACommand.cullModeConfig) & 0xf;
  }

  /** Gets the 1D look-up table sampler for fragment shader lighting. */
  getFSHLookUpTable(): number[] {
    return this.lookUpTable.slice();
  }

  /** Gets whether absolute value should be used before LUT for each input. */
  getReflectanceSamplerAbsolute(): PICACommand.fragmentSamplerAbsolute {
    const value = this.getParameter(PICACommand.lutSamplerAbsolute);
    return {
      r: (value & 0x2000000) === 0,
      g: (value & 0x200000) === 0,
      b: (value & 0x20000) === 0,
      d0: (value & 2) === 0,
      d1: (value & 0x20) === 0,
      fresnel: (value & 0x2000) === 0,
    };
  }

  /** Gets the input used to pick a value from the LUT on fragment shader. */
  getReflectanceSamplerInput(): PICACommand.fragmentSamplerInput {
    const value = this.getParameter(PICACommand.lutSamplerInput);
    return {
      r: (value >>> 24) & 0xf,
      g: (value >>> 20) & 0xf,
      b: (value >>> 16) & 0xf,
      d0: value & 0xf,
      d1: (value >>> 4) & 0xf,
      fresnel: (value >>> 12) & 0xf,
    };
  }

  /** Gets the scale used on the value on fragment shader. */
  getReflectanceSamplerScale(): PICACommand.fragmentSamplerScale {
    const value = this.getParameter(PICACommand.lutSamplerScale);
    return {
      r: (value >>> 24) & 0xf,
      g: (value >>> 20) & 0xf,
      b: (value >>> 16) & 0xf,
      d0: value & 0xf,
      d1: (value >>> 4) & 0xf,
      fresnel: (value >>> 12) & 0xf,
    };
  }

  /** Gets address of texture at Texture Unit 0. */
  getTexUnit0Address(): number { return this.getParameter(PICACommand.texUnit0Address); }

  /** Gets mapping parameters of Texture Unit 0. */
  getTexUnit0Mapper(): { magFilter: number; minFilter: number; wrapU: number; wrapV: number } {
    const value = this.getParameter(PICACommand.texUnit0Param);
    return {
      magFilter: (value >>> 1) & 1,
      minFilter: ((value >>> 2) & 1) | ((value >>> 23) & 2),
      wrapU: (value >>> 12) & 0xf,
      wrapV: (value >>> 8) & 0xf,
    };
  }

  /** Gets the border color of Texture Unit 0. */
  getTexUnit0BorderColor(): Color {
    const rgba = this.getParameter(PICACommand.texUnit0BorderColor);
    return { r: rgba & 0xff, g: (rgba >>> 8) & 0xff, b: (rgba >>> 16) & 0xff, a: (rgba >>> 24) & 0xff };
  }

  /** Gets the resolution of texture at Texture Unit 0. */
  getTexUnit0Size(): Size {
    const value = this.getParameter(PICACommand.texUnit0Size);
    return { width: value >>> 16, height: value & 0xffff };
  }

  /** Gets the encoded format of texture at Texture Unit 0. */
  getTexUnit0Format(): number { return this.getParameter(PICACommand.texUnit0Type); }

  /** Gets address of texture at Texture Unit 1. */
  getTexUnit1Address(): number { return this.getParameter(PICACommand.texUnit1Address); }

  /** Gets mapping parameters of Texture Unit 1. */
  getTexUnit1Mapper(): { magFilter: number; minFilter: number; wrapU: number; wrapV: number } {
    const value = this.getParameter(PICACommand.texUnit1Param);
    return {
      magFilter: (value >>> 1) & 1,
      minFilter: ((value >>> 2) & 1) | ((value >>> 23) & 2),
      wrapU: (value >>> 12) & 0xf,
      wrapV: (value >>> 8) & 0xf,
    };
  }

  /** Gets the border color of Texture Unit 1. */
  getTexUnit1BorderColor(): Color {
    const rgba = this.getParameter(PICACommand.texUnit1BorderColor);
    return { r: rgba & 0xff, g: (rgba >>> 8) & 0xff, b: (rgba >>> 16) & 0xff, a: (rgba >>> 24) & 0xff };
  }

  /** Gets the resolution of texture at Texture Unit 1. */
  getTexUnit1Size(): Size {
    const value = this.getParameter(PICACommand.texUnit1Size);
    return { width: value >>> 16, height: value & 0xffff };
  }

  /** Gets the encoded format of texture at Texture Unit 1. */
  getTexUnit1Format(): number { return this.getParameter(PICACommand.texUnit1Type); }

  /** Gets address of texture at Texture Unit 2. */
  getTexUnit2Address(): number { return this.getParameter(PICACommand.texUnit2Address); }

  /** Gets mapping parameters of Texture Unit 2. */
  getTexUnit2Mapper(): { magFilter: number; minFilter: number; wrapU: number; wrapV: number } {
    const value = this.getParameter(PICACommand.texUnit2Param);
    return {
      magFilter: (value >>> 1) & 1,
      minFilter: ((value >>> 2) & 1) | ((value >>> 23) & 2),
      wrapU: (value >>> 12) & 0xf,
      wrapV: (value >>> 8) & 0xf,
    };
  }

  /** Gets the border color of Texture Unit 2. */
  getTexUnit2BorderColor(): Color {
    const rgba = this.getParameter(PICACommand.texUnit2BorderColor);
    return { r: rgba & 0xff, g: (rgba >>> 8) & 0xff, b: (rgba >>> 16) & 0xff, a: (rgba >>> 24) & 0xff };
  }

  /** Gets the resolution of texture at Texture Unit 2. */
  getTexUnit2Size(): Size {
    const value = this.getParameter(PICACommand.texUnit2Size);
    return { width: value >>> 16, height: value & 0xffff };
  }

  /** Gets the encoded format of texture at Texture Unit 2. */
  getTexUnit2Format(): number { return this.getParameter(PICACommand.texUnit2Type); }
}
