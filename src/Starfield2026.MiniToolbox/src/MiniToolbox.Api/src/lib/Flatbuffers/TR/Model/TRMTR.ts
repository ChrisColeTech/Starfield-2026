import { Vector2f, Vector3f, Vector4f, RGBA } from '../../Common/Math.js';

export enum UVWrapMode {
  WRAP = 0,
  CLAMP = 1,
  MIRROR = 6,
  MIRROR_ONCE = 7
}

export class TRFloatParameter {
  Name: string = '';
  Value: number = 0;
}

export class TRVec2fParameter {
  Name: string = '';
  Value: Vector2f = new Vector2f();
}

export class TRVec3fParameter {
  Name: string = '';
  Value: Vector3f = new Vector3f();
}

export class TRVec4fParameter {
  Name: string = '';
  Value: Vector4f = new Vector4f();
}

export class TRStringParameter {
  Name: string = '';
  Value: string = '';
}

export class TRSampler {
  State0: number = 0;
  State1: number = 0;
  State2: number = 0;
  State3: number = 0;
  State4: number = 0;
  State5: number = 0;
  State6: number = 0;
  State7: number = 0;
  State8: number = 0;
  RepeatU: UVWrapMode = UVWrapMode.WRAP;
  RepeatV: UVWrapMode = UVWrapMode.WRAP;
  RepeatW: UVWrapMode = UVWrapMode.WRAP;
  BorderColor: RGBA = new RGBA();
}

export class TRTexture {
  Name: string = '';
  File: string = '';
  Slot: number = 0;
}

export class TRMaterialShader {
  Name: string = '';
  Values: TRStringParameter[] = [];
}

export class TRMaterial {
  Name: string = '';
  Shader: TRMaterialShader[] = [];
  Textures: TRTexture[] = [];
  Samplers: TRSampler[] = [];
  FloatParams: TRFloatParameter[] = [];
  Vec2fParams: TRVec2fParameter[] = [];
  Vec3fParams: TRVec3fParameter[] = [];
  Vec4fParams: TRVec4fParameter[] = [];
}

export class TRMTR {
  Field_00: number = 0;
  Materials: TRMaterial[] = [];
}
