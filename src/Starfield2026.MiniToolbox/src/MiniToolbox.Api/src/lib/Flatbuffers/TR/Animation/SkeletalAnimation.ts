import { Vector3f, PackedQuaternion, Transform } from '../../Common/Math.js';

export enum PlayType {
  Once = 0,
  Looped = 1
}

export class FixedVectorTrack {
  Value: Vector3f = new Vector3f();
}

export class FramedVectorTrack {
  Values: Vector3f[] = [];
}

export class Keyed16VectorTrack {
  Keys: number[] = [];
  Values: Vector3f[] = [];
}

export class Keyed8VectorTrack {
  Keys: number[] = [];
  Values: Vector3f[] = [];
}

export class FixedRotationTrack {
  Value: PackedQuaternion = new PackedQuaternion();
}

export class FramedRotationTrack {
  Values: PackedQuaternion[] = [];
}

export class Keyed16RotationTrack {
  Keys: number[] = [];
  Values: PackedQuaternion[] = [];
}

export class Keyed8RotationTrack {
  Keys: number[] = [];
  Values: PackedQuaternion[] = [];
}

export type VectorTrackUnion = FixedVectorTrack | FramedVectorTrack | Keyed16VectorTrack | Keyed8VectorTrack;
export type RotationTrackUnion = FixedRotationTrack | FramedRotationTrack | Keyed16RotationTrack | Keyed8RotationTrack;

export class SkeletalTrack {
  BoneName: string = '';
  ScaleChannel: VectorTrackUnion | null = null;
  RotationChannel: RotationTrackUnion | null = null;
  TranslateChannel: VectorTrackUnion | null = null;
}

export class PlaybackInfo {
  PlayType: PlayType = PlayType.Once;
  FrameCount: number = 0;
  FrameRate: number = 0;
}

export class BoneInit {
  IsInit: number = 0;
  BoneTransform: Transform = new Transform();
}

export class SkeletalAnimation {
  Tracks: SkeletalTrack[] = [];
  Init: BoneInit = new BoneInit();
}

export class TRANM {
  Info: PlaybackInfo = new PlaybackInfo();
  SkeletalAnimation: SkeletalAnimation = new SkeletalAnimation();
}
