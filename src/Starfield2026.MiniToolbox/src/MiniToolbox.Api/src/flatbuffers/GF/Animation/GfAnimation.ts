import { Vector3f, PackedQuaternion, Transform } from '../../Common/Math.js';

export class FixedVectorTrack {
  Co: Vector3f = new Vector3f();
}

export class DynamicVectorTrack {
  Co: Vector3f[] = [];
}

export class Framed16VectorTrack {
  Frames: number[] = [];
  Co: Vector3f[] = [];
}

export class Framed8VectorTrack {
  Frames: number[] = [];
  Co: Vector3f[] = [];
}

export class FixedRotationTrack {
  Co: PackedQuaternion = new PackedQuaternion();
}

export class DynamicRotationTrack {
  Co: PackedQuaternion[] = [];
}

export class Framed16RotationTrack {
  Frames: number[] = [];
  Co: PackedQuaternion[] = [];
}

export class Framed8RotationTrack {
  Frames: number[] = [];
  Co: PackedQuaternion[] = [];
}

export type VectorTrackUnion = FixedVectorTrack | DynamicVectorTrack | Framed16VectorTrack | Framed8VectorTrack;
export type RotationTrackUnion = FixedRotationTrack | DynamicRotationTrack | Framed16RotationTrack | Framed8RotationTrack;

export class BoneTrack {
  Name: string = '';
  Scale: VectorTrackUnion | null = null;
  Rotate: RotationTrackUnion | null = null;
  Translate: VectorTrackUnion | null = null;
}

export class BoneInit {
  IsInit: number = 0;
  Transform: Transform = new Transform();
}

export class BoneAnimation {
  Tracks: BoneTrack[] = [];
  InitData: BoneInit | null = null;
}

export class Info {
  DoesLoop: number = 0;
  KeyFrames: number = 0;
  FrameRate: number = 0;
}

export class Animation {
  Info: Info = new Info();
  Skeleton: BoneAnimation = new BoneAnimation();
}
