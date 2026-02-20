/**
 * Game Freak animation loader (Pokemon Sun/Moon).
 * Ported from OhanaCli.Formats.Models.PocketMonsters.GfMotion (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import { IOUtils } from '../../Core/IOUtils.js';
import {
  OAnimationKeyFrame,
  OAnimationKeyFrameGroup,
  OSkeletalAnimationBone,
  OSkeletalAnimation,
  OInterpolationMode,
} from '../../Core/RenderBase.js';

// ── Factory helpers ──

function createKeyFrameGroup(): OAnimationKeyFrameGroup {
  return new OAnimationKeyFrameGroup();
}

function createAnimBone(): OSkeletalAnimationBone {
  return new OSkeletalAnimationBone();
}

// ── Main class ──

export class GfMotion {
  /** Loads skeletal animations from a BinaryReader. */
  static load(data: BinaryReader): OSkeletalAnimation[] {
    const output: OSkeletalAnimation[] = [];
    const input = data;

    const animCount = input.readUInt32();

    for (let anm = 0; anm < animCount; anm++) {
      input.seek(4 + anm * 4);

      const animAddr = input.readUInt32();
      if (animAddr === 0) continue;

      input.seek(animAddr + 4);

      output.push(GfMotion.loadAnim(input, anm));
    }

    return output;
  }

  /** Loads a single skeletal animation. */
  static loadAnim(input: BinaryReader, anm: number = 0): OSkeletalAnimation {
    const anim = new OSkeletalAnimation();
    anim.name = 'anim_' + anm;
    anim.frameSize = 1;

    const unkFlags = input.readUInt32();
    const unkCount = input.readUInt32();
    input.seekRelative(0x24 + unkCount * 0xc);

    const boneNamesCount = input.readUInt32();
    const boneNamesLength = input.readUInt32();

    const boneNamesStart = input.position;

    const boneNames: string[] = new Array(boneNamesCount);
    for (let b = 0; b < boneNamesCount; b++) {
      boneNames[b] = IOUtils.readStringWithLength(input, input.readByte());
    }

    input.seek(boneNamesStart + boneNamesLength);

    let bbone = 0;

    for (let b = 0; b < boneNames.length; b++) {
      const flags = input.readUInt32();
      const frameLength = input.readUInt32();
      const frameStart = input.position;

      const bone = createAnimBone();
      bone.name = boneNames[b];
      bone.isAxisAngle = (flags >>> 31) === 0;

      for (let axis = 0; axis < 9; axis++) {
        const axisExists = ((flags >>> (2 + axis * 3)) & 1) !== 0;
        const axisConst = ((flags >>> (axis * 3)) & 3) === 3;

        const mul2 = axis > 2 && axis < 6 && (flags >>> 31) === 0;

        if (axisConst) addFrame(bone, mul2, axis, input.readFloat());
        if (!axisExists) continue;

        const keyFramesCount = input.readUInt32();

        const keyFrames: number[] = new Array(keyFramesCount);
        for (let n = 0; n < keyFramesCount; n++) {
          keyFrames[n] = input.readByte();
          if (keyFrames[n] > bbone) bbone = keyFrames[n];
        }
        while ((input.position & 3) !== 0) input.readByte();

        const valueScale = input.readFloat();
        const valueOffset = input.readFloat();
        const slopeScale = input.readFloat();
        const slopeOffset = input.readFloat();

        for (let i = 0; i < keyFramesCount; i++) {
          const qvalue = input.readUInt16();
          const qslope = input.readUInt16();

          const value = valueOffset + (qvalue / 0xffff) * valueScale;
          const slope = slopeOffset + (qslope / 0xffff) * slopeScale;

          addFrame(bone, mul2, axis, value, keyFrames[i], slope);
        }
      }

      anim.bone.push(bone);
    }

    for (const b of anim.bone) {
      b.scaleX.interpolation = OInterpolationMode.hermite;
      b.scaleY.interpolation = OInterpolationMode.hermite;
      b.scaleZ.interpolation = OInterpolationMode.hermite;
      b.rotationX.interpolation = OInterpolationMode.hermite;
      b.rotationY.interpolation = OInterpolationMode.hermite;
      b.rotationZ.interpolation = OInterpolationMode.hermite;
      b.translationX.interpolation = OInterpolationMode.hermite;
      b.translationY.interpolation = OInterpolationMode.hermite;
      b.translationZ.interpolation = OInterpolationMode.hermite;

      b.scaleX.exists = b.scaleX.keyFrames.length > 0;
      b.scaleY.exists = b.scaleY.keyFrames.length > 0;
      b.scaleZ.exists = b.scaleZ.keyFrames.length > 0;
      b.rotationX.exists = b.rotationX.keyFrames.length > 0;
      b.rotationY.exists = b.rotationY.keyFrames.length > 0;
      b.rotationZ.exists = b.rotationZ.keyFrames.length > 0;
      b.translationX.exists = b.translationX.keyFrames.length > 0;
      b.translationY.exists = b.translationY.keyFrames.length > 0;
      b.translationZ.exists = b.translationZ.keyFrames.length > 0;
    }

    if (bbone > 0) anim.frameSize = bbone;

    return anim;
  }
}

// ── Private helpers ──

function addFrame(
  bone: OSkeletalAnimationBone,
  mul2: boolean,
  axis: number,
  val: number,
  frame: number = 0,
  slope: number = 0,
): void {
  if (mul2) val *= 2;

  const frm = new OAnimationKeyFrame(val, slope, slope, frame);

  switch (axis) {
    case 0: bone.scaleX.keyFrames.push(frm); break;
    case 1: bone.scaleY.keyFrames.push(frm); break;
    case 2: bone.scaleZ.keyFrames.push(frm); break;
    case 3: bone.rotationX.keyFrames.push(frm); break;
    case 4: bone.rotationY.keyFrames.push(frm); break;
    case 5: bone.rotationZ.keyFrames.push(frm); break;
    case 6: bone.translationX.keyFrames.push(frm); break;
    case 7: bone.translationY.keyFrames.push(frm); break;
    case 8: bone.translationZ.keyFrames.push(frm); break;
  }
}
