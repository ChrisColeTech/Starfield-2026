import { Vector2f, Vector3f, Vector4f } from '../Common/Math.js';

export class IntParam {
  Name: string = '';
  Value: number = 0;
}

export class FloatParam {
  Name: string = '';
  Value: number = 0.0;
}

export class Vector2fParam {
  Name: string = '';
  Value: Vector2f = new Vector2f();
}

export class Vector3fParam {
  Name: string = '';
  Value: Vector3f = new Vector3f();
}

export class Vector4fParam {
  Name: string = '';
  Value: Vector4f = new Vector4f();
}

export class TextureParam {
  Name: string = '';
  FilePath: string = '';
  SamplerId: number = 0;
}

export class ShaderOption {
  Name: string = '';
  Choice: string = '';
}

export class Technique {
  Name: string = '';
  ShaderOptions: ShaderOption[] = [];
}

export class MaterialItem {
  Name: string = '';
  TechniqueList: Technique[] = [];
  TextureParamList: TextureParam[] = [];
  FloatParamList: FloatParam[] = [];
  Vector2fParamList: Vector2fParam[] = [];
  Vector3fParamList: Vector3fParam[] = [];
  Vector4fParamList: Vector4fParam[] = [];
  IntParamList: IntParam[] = [];
}

export class Material {
  Version: number = 0;
  ItemList: MaterialItem[] = [];
}
