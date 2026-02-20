export interface SplitManifest {
  version: number
  mode: string
  textures: string[]
  models: SplitManifestModel[]
}

export interface SplitManifestModel {
  name: string
  modelFile: string
  clips: SplitManifestClip[]
}

export interface SplitManifestClip {
  index: number
  id: string
  name: string
  sourceName: string
  semanticName: string | null
  semanticSource: string | null
  file: string
  frameCount: number
  fps: number
}

/** Known semantic animation tags for tagging UI */
export const SEMANTIC_TAGS = [
  // Universal
  'Idle',
  'Walk',
  'Run',
  'Jump',
  'Land',
  // Overworld character slots
  'ShortAction1',
  'LongAction1',
  'ShortAction2',
  'MediumAction',
  'Action',
  'Action2',
  'ShortAction3',
  'ShortAction4',
  'IdleVariant',
  'ShortAction5',
  'LongAction2',
  'ShortAction6',
  'Action3',
  'Action4',
  'Action5',
  'LongAction3',
  'Action6',
  'Action7',
  'Action8',
  'Action9',
  // Battle
  'Attack',
  'Hurt',
  'Faint',
  'Special',
  'Entrance',
  'Victory',
] as const

export type SemanticTag = (typeof SEMANTIC_TAGS)[number]
