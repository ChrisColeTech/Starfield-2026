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
  // Character SourceName-inferred tags
  'WalkUp',
  'WalkDown',
  'BattleIdle',
  'BallThrow',
  'Speak',
  'TurnRight',
  'TurnLeft',
  'Action1',
  'Sit',
  'Wave',
  // Overworld character index-based slots
  'IdleVariant',
  'ShortAction1',
  'ShortAction2',
  'ShortAction3',
  'ShortAction4',
  'ShortAction5',
  'ShortAction6',
  'LongAction1',
  'LongAction2',
  'LongAction3',
  'MediumAction',
  'Action',
  'Action2',
  'Action3',
  'Action4',
  'Action5',
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
