export class Vector2f {
  X: number;
  Y: number;

  constructor(X: number = 0.0, Y: number = 0.0) {
    this.X = X;
    this.Y = Y;
  }
}

export class Vector3f {
  X: number;
  Y: number;
  Z: number;

  constructor(X: number = 0.0, Y: number = 0.0, Z: number = 0.0) {
    this.X = X;
    this.Y = Y;
    this.Z = Z;
  }
}

export class Vector4f {
  W: number;
  X: number;
  Y: number;
  Z: number;

  constructor(W: number = 0.0, X: number = 0.0, Y: number = 0.0, Z: number = 0.0) {
    this.W = W;
    this.X = X;
    this.Y = Y;
    this.Z = Z;
  }
}

export class Vector2i {
  X: number;
  Y: number;

  constructor(X: number = 0, Y: number = 0) {
    this.X = X;
    this.Y = Y;
  }
}

export class Sphere {
  X: number;
  Y: number;
  Z: number;
  Radius: number;

  constructor(X: number = 0.0, Y: number = 0.0, Z: number = 0.0, Radius: number = 0.0) {
    this.X = X;
    this.Y = Y;
    this.Z = Z;
    this.Radius = Radius;
  }
}

export class TRBoundingBox {
  MinBound: Vector3f;
  MaxBound: Vector3f;

  constructor(MinBound: Vector3f = new Vector3f(), MaxBound: Vector3f = new Vector3f()) {
    this.MinBound = MinBound;
    this.MaxBound = MaxBound;
  }
}

export class PackedQuaternion {
  X: number;
  Y: number;
  Z: number;

  constructor(X: number = 0, Y: number = 0, Z: number = 0) {
    this.X = X;
    this.Y = Y;
    this.Z = Z;
  }
}

export class RGBA {
  R: number;
  G: number;
  B: number;
  A: number;

  constructor(R: number = 0.0, G: number = 0.0, B: number = 0.0, A: number = 0.0) {
    this.R = R;
    this.G = G;
    this.B = B;
    this.A = A;
  }
}

export class Transform {
  Scale: Vector3f;
  Rotate: Vector4f;
  Translate: Vector3f;

  constructor(
    Scale: Vector3f = new Vector3f(),
    Rotate: Vector4f = new Vector4f(),
    Translate: Vector3f = new Vector3f()
  ) {
    this.Scale = Scale;
    this.Rotate = Rotate;
    this.Translate = Translate;
  }
}
