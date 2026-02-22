import { Vector3f, Vector4f } from '../../Common/Math.js';

export class Matrix4x3f {
  AxisX: Vector3f = new Vector3f();
  AxisY: Vector3f = new Vector3f();
  AxisZ: Vector3f = new Vector3f();
  AxisW: Vector3f = new Vector3f();
}

export class SRT {
  Scale: Vector3f = new Vector3f();
  Rotate: Vector3f = new Vector3f();
  Translate: Vector3f = new Vector3f();
}

export class TRTransformNode {
  Name: string = '';
  Transform: SRT = new SRT();
  ScalePivot: Vector3f = new Vector3f();
  RotatePivot: Vector3f = new Vector3f();
  ParentNodeIndex: number = -1;
  JointInfoIndex: number = -1;
  ParentNodeName: string = '';
  Priority: number = 0;
  PriorityPass: boolean = false;
  IgnoreParentRotation: boolean = false;
}

export class TRJointInfo {
  SegmentScaleCompensate: boolean = false;
  InfluenceSkinning: boolean = true;
  InverseBindPoseMatrix: Matrix4x3f = new Matrix4x3f();
}

export class TRHelperBoneInfo {
  Output: string = '';
  Target: string = '';
  Reference: string = '';
  Type: string = '';
  UpType: string = '';
  Weight: Vector3f = new Vector3f();
  Adjust: Vector4f = new Vector4f();
}

export class TRSKL {
  Version: number = 2;
  TransformNodes: TRTransformNode[] = [];
  JointInfos: TRJointInfo[] = [];
  HelperBones: TRHelperBoneInfo[] = [];
  SkinningPaletteOffset: number = -1;
  IsInteriorMap: boolean = false;
}
