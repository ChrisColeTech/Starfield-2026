// ── Generator Types ──

export type GeneratorType =
  | 'grass'
  | 'grass-single'
  | 'flower'
  | 'tree-green'
  | 'tree-autumn'
  | 'bush';

export interface GeneratorConfig {
  type: GeneratorType;
  variant?: string;
  seed: number;
  frames: number;
}

export interface GeneratorInfo {
  type: GeneratorType;
  label: string;
  frames: number;
  variants?: { value: string; label: string }[];
  parameters?: ParameterDefinition[];
}

export interface ParameterDefinition {
  name: string;
  type: 'number' | 'color' | 'select';
  default: unknown;
  min?: number;
  max?: number;
  options?: { value: string; label: string }[];
}

// ── Sprite Types ──

export interface SpriteFrame {
  svg: string;
  width: number;
  height: number;
}

export interface GenerationContext {
  seed: number;
  frameIndex: number;
  totalFrames: number;
  variant?: string;
  parameters?: Record<string, unknown>;
}

// ── Service Interface ──

export interface GeneratorService {
  readonly type: GeneratorType;
  readonly label: string;
  readonly defaultFrames: number;
  readonly variants?: string[];
  readonly parameters?: ParameterDefinition[];

  generate(context: GenerationContext): SpriteFrame;
  generateAll(seed: number, frames?: number, variant?: string): SpriteFrame[];
}

// ── API Types ──

export interface GenerateRequest {
  type: GeneratorType;
  variant?: string;
  seed: number;
  frames?: number;
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

export interface GalleryResponse {
  items: GalleryItem[];
  total: number;
}

export interface SaveRequest {
  type: GeneratorType;
  variant?: string;
  seed: number;
  baseName?: string;
}

export interface SaveResponse {
  saved: string[];
  count: number;
}

export interface ConvertRequest {
  filename: string;
  format: 'png';
  scale?: number;
}

export interface ConvertResponse {
  outputFilename: string;
  downloadUrl: string;
}
