export type GeneratorType =
  | 'grass'
  | 'grass-single'
  | 'flower'
  | 'tree-green'
  | 'tree-autumn'
  | 'bush';

export interface GeneratorInfo {
  type: GeneratorType;
  label: string;
  defaultFrames: number;
  variants?: string[];
}

export interface GenerateResponse {
  frames: string[];
  metadata: {
    type: GeneratorType;
    variant?: string;
    seed: number;
    frameCount: number;
  };
}

export interface GalleryItem {
  filename: string;
  type: string;
  variant?: string;
  frameIndex?: number;
  thumbnail?: string;
  createdAt: string;
}
