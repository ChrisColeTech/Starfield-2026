export interface EncounterEntry {
  speciesId: string
  minLevel: number
  maxLevel: number
  weight: number
  formId?: string
  requiredBadges?: number
  requiredFlags?: string[]
}

export interface EncounterGroup {
  encounterType: string
  baseEncounterRate: number
  entries: EncounterEntry[]
}

export interface MapEncounterData {
  progressMultiplier: number
  encounterGroups: EncounterGroup[]
}

export interface SpeciesInfo {
  id: number
  name: string
  type1: string
  type2: string | null
}

export const ENCOUNTER_TYPES = [
  { id: 'tall_grass', label: 'Tall Grass' },
  { id: 'rare_grass', label: 'Rare Grass' },
  { id: 'dark_grass', label: 'Dark Grass' },
  { id: 'cave_floor', label: 'Cave Floor' },
  { id: 'water_surface', label: 'Water Surface' },
  { id: 'surf', label: 'Surf' },
  { id: 'fishing', label: 'Fishing' },
  { id: 'headbutt', label: 'Headbutt' },
  { id: 'fire_terrain', label: 'Fire Terrain' },
] as const

export const PROGRESS_MULTIPLIERS = [
  { value: 0.0, label: '0.0 — No scaling (early routes)' },
  { value: 0.3, label: '0.3 — Mild (mid-game)' },
  { value: 0.5, label: '0.5 — Moderate (late-game)' },
  { value: 0.8, label: '0.8 — Strong (post-game)' },
  { value: 1.0, label: '1.0 — Full scaling' },
] as const

export function defaultEncounterEntry(): EncounterEntry {
  return { speciesId: '', minLevel: 2, maxLevel: 5, weight: 10 }
}

export function defaultEncounterGroup(encounterType: string = 'tall_grass'): EncounterGroup {
  return { encounterType, baseEncounterRate: 26, entries: [] }
}

export function defaultMapEncounterData(): MapEncounterData {
  return { progressMultiplier: 0, encounterGroups: [] }
}
