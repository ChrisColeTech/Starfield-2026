export class TRBuffer {
  Bytes: Uint8Array = new Uint8Array(0);
}

export class TRMorphTarget {
  morphBuffers: TRBuffer[] = [];
}

export class TRModelBuffer {
  IndexBuffer: TRBuffer[] = [];
  VertexBuffer: TRBuffer[] = [];
  MorphTargets: TRMorphTarget[] = [];
}

export class TRMBF {
  Field_00: number = 0;
  TRMeshBuffers: TRModelBuffer[] = [];
}
