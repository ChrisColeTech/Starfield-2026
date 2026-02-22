import { Vector4f, TRBoundingBox } from '../../Common/Math.js';

export class ModelMesh {
  PathName: string = '';
}

export class ModelSkeleton {
  PathName: string = '';
}

export class ModelLODEntry {
  Index: number = 0;
}

export class ModelLOD {
  Entries: ModelLODEntry[] = [];
  Type: string = '';
}

export class TRMDL {
  Field_00: number = 0;
  Meshes: ModelMesh[] = [];
  Skeleton: ModelSkeleton = new ModelSkeleton();
  Materials: string[] = [];
  LODs: ModelLOD[] = [];
  Bounds: TRBoundingBox = new TRBoundingBox();
  Field_06: Vector4f = new Vector4f();
}
