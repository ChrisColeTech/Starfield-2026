// --- Dynamic Registry Types (from doc 02) ---

export type RegistryId = string

export interface EditorTileDefinition {
  id: number
  name: string
  color: string
  walkable: boolean
  category: string
  encounter?: string
  direction?: 'up' | 'down' | 'left' | 'right'
  isOverlay?: boolean
  // Extended props (Pokemon-specific)
  requires?: string
  villain?: boolean
  minion?: boolean
  rival?: boolean
  elite?: boolean
  champion?: boolean
  final?: boolean
  hidden?: boolean
  alt?: boolean
}

export interface EditorCategoryDefinition {
  id: string
  label: string
  showInPalette: boolean
}

export interface EditorBuildingDefinition {
  id: string
  name: string
  tiles: (number | null)[][]
}

export interface EditorTileRegistry {
  id: RegistryId
  name: string
  version: string
  categories: EditorCategoryDefinition[]
  tiles: EditorTileDefinition[]
  buildings: EditorBuildingDefinition[]
}

// --- Legacy aliases (kept for minimal churn during migration) ---

export type TileCategory = string
export type TileDefinition = EditorTileDefinition
export type BuildingDefinition = EditorBuildingDefinition & { width: number; height: number }
