import type { EditorTileRegistry, EditorTileDefinition, EditorCategoryDefinition, EditorBuildingDefinition } from '../types/editor'
import defaultRegistry from '../data/registries/default.json'

// --- Loader ---

export function parseRegistryJson(json: string): EditorTileRegistry {
  const data = JSON.parse(json)

  if (!data.id || !data.name || !data.version) {
    throw new Error('Registry must have id, name, and version fields')
  }
  if (!Array.isArray(data.categories) || data.categories.length === 0) {
    throw new Error('Registry must have at least one category')
  }
  if (!Array.isArray(data.tiles) || data.tiles.length === 0) {
    throw new Error('Registry must have at least one tile')
  }

  // Validate no duplicate tile IDs
  const tileIds = new Set<number>()
  for (const tile of data.tiles) {
    if (tileIds.has(tile.id)) {
      throw new Error(`Duplicate tile id: ${tile.id}`)
    }
    tileIds.add(tile.id)
  }

  // Validate tile category references
  const catIds = new Set(data.categories.map((c: EditorCategoryDefinition) => c.id))
  for (const tile of data.tiles) {
    if (!catIds.has(tile.category)) {
      throw new Error(`Tile "${tile.name}" references unknown category "${tile.category}"`)
    }
  }

  // Validate building tile references
  if (data.buildings) {
    for (const building of data.buildings) {
      for (const row of building.tiles) {
        for (const tileId of row) {
          if (tileId !== null && !tileIds.has(tileId)) {
            throw new Error(`Building "${building.name}" references unknown tile id ${tileId}`)
          }
        }
      }
    }
  }

  return {
    id: data.id,
    name: data.name,
    version: data.version,
    categories: data.categories,
    tiles: data.tiles,
    buildings: data.buildings || [],
  }
}

export function loadDefaultRegistry(): EditorTileRegistry {
  return defaultRegistry as EditorTileRegistry
}

// --- C# TileRegistry.cs parser ---

export function parseCSharpRegistry(source: string): EditorTileRegistry {
  // Match lines like: [0] = new TileDefinition(0, "Water", false, "#3890f8", TileCategory.Terrain),
  // or with optional overlay: [8] = new TileDefinition(8, "Ice", true, "#b0e0f8", TileCategory.Terrain, "slippery"),
  // or with optional EntityId: [48] = new TileDefinition(48, "Villager", false, "#ffd700", TileCategory.Entity, "npc", EntityId: 506),
  const tilePattern = /\[(\d+)\]\s*=\s*new\s+TileDefinition\(\s*(\d+)\s*,\s*"([^"]+)"\s*,\s*(true|false)\s*,\s*"([^"]+)"\s*,\s*TileCategory\.(\w+)(?:\s*,\s*"([^"]+)")?(?:\s*,\s*EntityId:\s*(\d+))?[^)]*\)/g

  const tiles: EditorTileDefinition[] = []
  const categorySet = new Set<string>()
  let match: RegExpExecArray | null

  while ((match = tilePattern.exec(source)) !== null) {
    const [, , idStr, name, walkableStr, color, category, overlay] = match
    const categoryId = category.toLowerCase()
    categorySet.add(categoryId)

    const tile: EditorTileDefinition = {
      id: parseInt(idStr),
      name,
      walkable: walkableStr === 'true',
      color,
      category: categoryId,
    }

    if (overlay) {
      tile.encounter = overlay
    }

    tiles.push(tile)
  }

  if (tiles.length === 0) {
    throw new Error('No TileDefinition entries found in C# source')
  }

  // Derive categories from what we found, preserving enum order
  const categoryOrder = ['terrain', 'decoration', 'interactive', 'entity', 'trainer', 'encounter', 'structure', 'item', 'transition']
  const categories: EditorCategoryDefinition[] = categoryOrder
    .filter(c => categorySet.has(c))
    .map(c => ({
      id: c,
      label: c.charAt(0).toUpperCase() + c.slice(1),
      showInPalette: true,
    }))

  // Add any categories not in the predefined order
  for (const c of categorySet) {
    if (!categoryOrder.includes(c)) {
      categories.push({ id: c, label: c.charAt(0).toUpperCase() + c.slice(1), showInPalette: true })
    }
  }

  return {
    id: 'csharp-tile-registry',
    name: 'C# TileRegistry',
    version: '1.0.0',
    categories,
    tiles,
    buildings: [],
  }
}

// --- C# generated map (.g.cs) parser ---

import type { MapEncounterData, EncounterGroup, EncounterEntry } from '../types/encounters'
import { defaultMapEncounterData } from '../types/encounters'

export interface ParsedCSharpMap {
  worldId: string
  mapId: string
  displayName: string
  width: number
  height: number
  tileSize: number
  worldX: number
  worldY: number
  mapData: number[][]
  encounterData: MapEncounterData
}

export function parseCSharpMap(source: string): ParsedCSharpMap {
  // Try new format first: base("worldId", "map_id", "Display Name", width, height, tileSize, ...)
  const newCtorPattern = /base\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/
  const newCtorMatch = newCtorPattern.exec(source)

  let worldId: string
  let mapId: string
  let displayName: string
  let widthStr: string
  let heightStr: string
  let tileSizeStr: string

  if (newCtorMatch) {
    [, worldId, mapId, displayName, widthStr, heightStr, tileSizeStr] = newCtorMatch
  } else {
    // Legacy format: base("map_id", "Display Name", width, height, tileSize, ...)
    const legacyPattern = /base\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/
    const legacyMatch = legacyPattern.exec(source)
    if (!legacyMatch) {
      throw new Error('Could not find MapDefinition constructor call')
    }
    worldId = 'default';
    [, mapId, displayName, widthStr, heightStr, tileSizeStr] = legacyMatch
  }
  const width = parseInt(widthStr)
  const height = parseInt(heightStr)
  const tileSize = parseInt(tileSizeStr)

  // Extract BaseTileData = [ ... ];
  const basePattern = /BaseTileData\s*=\s*\[([\s\S]*?)\];/
  const baseMatch = basePattern.exec(source)
  if (!baseMatch) {
    throw new Error('Could not find BaseTileData array')
  }
  const baseValues = baseMatch[1].match(/-?\d+/g)?.map(Number)
  if (!baseValues || baseValues.length !== width * height) {
    throw new Error(`BaseTileData has ${baseValues?.length ?? 0} values, expected ${width * height}`)
  }

  // Extract OverlayTileData = [ ... ]; (optional)
  const overlayPattern = /OverlayTileData\s*=\s*\[([\s\S]*?)\];/
  const overlayMatch = overlayPattern.exec(source)
  let overlayValues: (number | null)[] | null = null
  if (overlayMatch) {
    overlayValues = overlayMatch[1]
      .split(',')
      .map(s => s.trim())
      .filter(s => s.length > 0)
      .map(s => s === 'null' ? null : parseInt(s))
  }

  // Build 2D map, merging overlay over base
  const mapData: number[][] = []
  for (let y = 0; y < height; y++) {
    mapData[y] = []
    for (let x = 0; x < width; x++) {
      const idx = y * width + x
      const ov = overlayValues?.[idx]
      mapData[y][x] = ov != null ? ov : baseValues[idx]
    }
  }

  // Extract worldX/worldY from constructor: ..., null, null, worldX, worldY)
  let worldX = 0
  let worldY = 0
  const worldPosPattern = /WalkableTileIds\s*,\s*null\s*,\s*null\s*,\s*(-?\d+)\s*,\s*(-?\d+)/
  const worldPosMatch = worldPosPattern.exec(source)
  if (worldPosMatch) {
    worldX = parseInt(worldPosMatch[1])
    worldY = parseInt(worldPosMatch[2])
  }

  // Parse encounter data
  const encounterData = parseEncounterData(source)

  return { worldId, mapId, displayName, width, height, tileSize, worldX, worldY, mapData, encounterData }
}

function parseEncounterData(source: string): MapEncounterData {
  const data = defaultMapEncounterData()

  // Parse ProgressMultiplier
  const progMatch = /ProgressMultiplier\s*=\s*([\d.]+)f?/.exec(source)
  if (progMatch) {
    data.progressMultiplier = parseFloat(progMatch[1])
  }

  // Parse EncounterGroups array (object initializer format)
  const groupsMatch = /EncounterGroups\s*=\s*\[([\s\S]*?)\];\s*$/m.exec(source)
  if (!groupsMatch) return data

  // Match each new() { EncounterType = "...", BaseEncounterRate = N, Entries = [...] }
  const groupPattern = /EncounterType\s*=\s*"([^"]+)"\s*,\s*BaseEncounterRate\s*=\s*(\d+)\s*,\s*Entries\s*=\s*\[([\s\S]*?)\]\s*\}/g
  let groupMatch: RegExpExecArray | null
  while ((groupMatch = groupPattern.exec(groupsMatch[1])) !== null) {
    const group: EncounterGroup = {
      encounterType: groupMatch[1],
      baseEncounterRate: parseInt(groupMatch[2]),
      entries: [],
    }

    // Match entries: new() { SpeciesId = N, MinLevel = N, ... }, // SpeciesName
    const entryPattern = /new\(\)\s*\{([^}]+)\}[^,]*?(?:,\s*\/\/\s*(\S+))?/g
    let entryMatch: RegExpExecArray | null
    while ((entryMatch = entryPattern.exec(groupMatch[3])) !== null) {
      const props = entryMatch[1]
      const commentName = entryMatch[2] || ''

      const speciesIdMatch = /SpeciesId\s*=\s*(\d+)/.exec(props)
      const minMatch = /MinLevel\s*=\s*(\d+)/.exec(props)
      const maxMatch = /MaxLevel\s*=\s*(\d+)/.exec(props)
      const weightMatch = /Weight\s*=\s*(\d+)/.exec(props)

      if (!speciesIdMatch || !minMatch || !maxMatch || !weightMatch) continue

      const entry: EncounterEntry = {
        speciesId: commentName || `#${speciesIdMatch[1]}`,
        minLevel: parseInt(minMatch[1]),
        maxLevel: parseInt(maxMatch[1]),
        weight: parseInt(weightMatch[1]),
      }

      const badgeMatch = /RequiredBadges\s*=\s*(\d+)/.exec(props)
      if (badgeMatch && parseInt(badgeMatch[1]) > 0) entry.requiredBadges = parseInt(badgeMatch[1])

      const flagsMatch = /RequiredFlags\s*=\s*new\[\]\s*\{([^}]*)\}/.exec(props)
      if (flagsMatch) {
        entry.requiredFlags = flagsMatch[1].match(/"([^"]+)"/g)?.map(s => s.slice(1, -1)) ?? []
      }

      group.entries.push(entry)
    }

    data.encounterGroups.push(group)
  }

  return data
}

// --- Derived lookups ---

export function buildTilesById(registry: EditorTileRegistry): Map<number, EditorTileDefinition> {
  const map = new Map<number, EditorTileDefinition>()
  for (const tile of registry.tiles) {
    map.set(tile.id, tile)
  }
  return map
}

export function tilesForCategory(registry: EditorTileRegistry, categoryId: string): EditorTileDefinition[] {
  return registry.tiles.filter(t => t.category === categoryId)
}

export function paletteCategories(registry: EditorTileRegistry): EditorCategoryDefinition[] {
  return registry.categories.filter(c => c.showInPalette)
}

export function buildingWidth(building: EditorBuildingDefinition): number {
  return building.tiles[0]?.length ?? 0
}

export function buildingHeight(building: EditorBuildingDefinition): number {
  return building.tiles.length
}

// Fallback tile for when the selected tile is missing after a registry switch
export function fallbackTileId(registry: EditorTileRegistry): number {
  // Prefer first walkable terrain tile
  const grass = registry.tiles.find(t => t.category === 'terrain' && t.walkable)
  if (grass) return grass.id
  return registry.tiles[0]?.id ?? 0
}

// Fallback category when selected category is missing
export function fallbackCategoryId(registry: EditorTileRegistry): string {
  const visible = paletteCategories(registry)
  return visible[0]?.id ?? registry.categories[0]?.id ?? ''
}

// Unknown tile placeholder (for IDs not in registry)
export const UNKNOWN_TILE: EditorTileDefinition = {
  id: -1,
  name: 'Unknown',
  color: '#ff00ff',
  walkable: false,
  category: 'terrain',
}
