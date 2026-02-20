import type { EditorTileRegistry, EditorTileDefinition } from '../types/editor'
import type { MapEncounterData, SpeciesInfo } from '../types/encounters'

// --- C# class template (from MapClass.tpl) ---

const MAP_CLASS_TEMPLATE = `#nullable enable
{{USING_ENCOUNTERS}}{{TILE_LEGEND}}
namespace Starfield.Core.Maps;

public sealed class {{CLASS_NAME}} : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
{{BASE_TILE_DATA}}
    ];

    private static readonly int?[] OverlayTileData =
    [
{{OVERLAY_TILE_DATA}}
    ];

    private static readonly int[] WalkableTileIds =
    [
{{WALKABLE_TILE_IDS}}
    ];
{{ENCOUNTER_DATA}}
    public static {{CLASS_NAME}} Instance { get; } = new();

    private {{CLASS_NAME}}()
        : base("{{WORLD_ID}}", "{{MAP_ID}}", "{{DISPLAY_NAME}}", {{WIDTH}}, {{HEIGHT}}, {{TILE_SIZE}}, BaseTileData, OverlayTileData, WalkableTileIds{{EXTRA_CTOR_ARGS}})
    {
    }
}
`

// --- Helpers ---

const FALLBACK_BASE_TILE_ID = 1 // Grass — used only if no baseTile provided

export function toPascalCase(input: string): string {
  if (!input) return input
  let result = ''
  let capitalizeNext = true
  for (const c of input) {
    if (c === '_' || c === '-' || c === ' ') {
      capitalizeNext = true
    } else if (capitalizeNext) {
      result += c.toUpperCase()
      capitalizeNext = false
    } else {
      result += c
    }
  }
  return result
}

function escapeString(input: string): string {
  if (!input) return input
  return input
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\n/g, '\\n')
    .replace(/\r/g, '\\r')
    .replace(/\t/g, '\\t')
}

function isTerrainTile(tileId: number, tilesById: Map<number, EditorTileDefinition>): boolean {
  const tile = tilesById.get(tileId)
  if (!tile) return true
  return tile.category === 'terrain' || tile.category === 'structure'
}

// --- Array formatters (matching C# CodeGenerator exactly) ---

function formatBaseTiles(
  mapData: number[][],
  w: number,
  h: number,
  tilesById: Map<number, EditorTileDefinition>,
  baseTileId: number,
): string {
  const parts: string[] = []
  for (let y = 0; y < h; y++) {
    let row = '        '
    for (let x = 0; x < w; x++) {
      let tileId = mapData[y]?.[x] ?? 0
      if (!isTerrainTile(tileId, tilesById)) {
        tileId = baseTileId
      }
      row += String(tileId)
      if (x < w - 1 || y < h - 1) row += ', '
    }
    parts.push(row)
  }
  return parts.join('\n')
}

function formatOverlayTiles(
  mapData: number[][],
  w: number,
  h: number,
  tilesById: Map<number, EditorTileDefinition>,
): string {
  const parts: string[] = []
  for (let y = 0; y < h; y++) {
    let row = '        '
    for (let x = 0; x < w; x++) {
      const tileId = mapData[y]?.[x] ?? 0
      const hasOverlay = !isTerrainTile(tileId, tilesById)
      row += hasOverlay ? String(tileId).padStart(4) : 'null'
      if (x < w - 1 || y < h - 1) row += ', '
    }
    parts.push(row)
  }
  return parts.join('\n')
}

function formatWalkableIds(ids: number[]): string {
  if (ids.length === 0) return ''
  return '        ' + ids.join(', ')
}

// --- Tile legend ---

function categoryLabel(category: string): string {
  const map: Record<string, string> = {
    terrain: 'Terrain',
    decoration: 'Decoration',
    interactive: 'Interactive',
    entity: 'Entity',
    trainer: 'Trainer',
    encounter: 'Encounter',
    structure: 'Structure',
    item: 'Item',
    transition: 'Transition',
  }
  return map[category] ?? category.charAt(0).toUpperCase() + category.slice(1)
}

function buildTileLegend(
  mapData: number[][],
  w: number,
  h: number,
  tilesById: Map<number, EditorTileDefinition>,
  baseTileId: number,
): string {
  // Collect all unique tile IDs actually used in the map
  const usedIds = new Set<number>()

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const tileId = mapData[y]?.[x] ?? 0
      usedIds.add(tileId)
    }
  }

  // Non-terrain overlay tiles also cause the base tile to appear underneath
  let hasOverlay = false
  for (const id of usedIds) {
    if (!isTerrainTile(id, tilesById)) {
      hasOverlay = true
      break
    }
  }
  if (hasOverlay) {
    usedIds.add(baseTileId)
  }

  const sorted = [...usedIds].sort((a, b) => a - b)

  // Find max width of "ID = Name" portion for alignment
  const entries = sorted.map(id => {
    const tile = tilesById.get(id)
    const name = tile ? tile.name : 'Unknown'
    const cat = tile ? categoryLabel(tile.category) : 'Unknown'
    return { id, name, cat }
  })

  const maxIdWidth = Math.max(...entries.map(e => String(e.id).length))

  const lines: string[] = []
  lines.push('// Tile Legend:')
  for (const entry of entries) {
    const idStr = String(entry.id).padStart(maxIdWidth)
    lines.push(`//   ${idStr} = ${entry.name} (${entry.cat})`)
  }

  return '\n' + lines.join('\n')
}

// --- Encounter data formatter ---

function formatEncounterData(encounterData: MapEncounterData | undefined, species: SpeciesInfo[]): string {
  if (!encounterData || encounterData.encounterGroups.length === 0) return ''

  const speciesByName = new Map<string, SpeciesInfo>()
  for (const s of species) speciesByName.set(s.name.toLowerCase(), s)

  const lines: string[] = ['']
  lines.push(`    public const float ProgressMultiplier = ${encounterData.progressMultiplier.toFixed(1)}f;`)
  lines.push('')
  lines.push('    private static readonly EncounterTable[] EncounterGroups =')
  lines.push('    [')

  for (const group of encounterData.encounterGroups) {
    if (group.entries.length === 0) continue
    lines.push(`        new() { EncounterType = "${escapeString(group.encounterType)}", BaseEncounterRate = ${group.baseEncounterRate}, Entries =`)
    lines.push('        [')
    for (const entry of group.entries) {
      const sp = speciesByName.get(entry.speciesId.toLowerCase())
      const id = sp?.id ?? 0
      let args = `SpeciesId = ${id}, MinLevel = ${entry.minLevel}, MaxLevel = ${entry.maxLevel}, Weight = ${entry.weight}`
      if (entry.requiredBadges) args += `, RequiredBadges = ${entry.requiredBadges}`
      if (entry.requiredFlags && entry.requiredFlags.length > 0) {
        const flags = entry.requiredFlags.map(f => `"${escapeString(f)}"`).join(', ')
        args += `, RequiredFlags = new[] { ${flags} }`
      }
      lines.push(`            new() { ${args} }, // ${entry.speciesId}`)
    }
    lines.push('        ] },')
  }

  lines.push('    ];')
  return lines.join('\n')
}

// --- Main code generator ---

export function generateMapClass(
  mapData: number[][],
  mapWidth: number,
  mapHeight: number,
  mapName: string,
  cellSize: number,
  tilesById: Map<number, EditorTileDefinition>,
  worldId: string = 'default',
  worldX: number = 0,
  worldY: number = 0,
  baseTile: number = FALLBACK_BASE_TILE_ID,
  encounterData?: MapEncounterData,
  species: SpeciesInfo[] = [],
): string {
  const mapId = mapName.toLowerCase().replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '')
  const className = toPascalCase(mapId) || 'UntitledMap'

  // Collect all unique tile IDs and determine which are walkable
  const usedIds = new Set<number>()
  for (let y = 0; y < mapHeight; y++) {
    for (let x = 0; x < mapWidth; x++) {
      usedIds.add(mapData[y]?.[x] ?? 0)
    }
  }

  const walkableIds = [...usedIds]
    .filter(id => tilesById.get(id)?.walkable === true)
    .sort((a, b) => a - b)

  const tileLegend = buildTileLegend(mapData, mapWidth, mapHeight, tilesById, baseTile)
  const hasEncounters = encounterData && encounterData.encounterGroups.some(g => g.entries.length > 0)

  // Extra constructor args: warps, connections, worldX, worldY, encounterGroups, progressMultiplier
  const hasWorldPos = worldX !== 0 || worldY !== 0

  let extraCtorArgs = ''
  if (hasWorldPos || hasEncounters) {
    extraCtorArgs += `, null, null, ${worldX}, ${worldY}`
  }
  if (hasEncounters) {
    extraCtorArgs += `, EncounterGroups, ProgressMultiplier`
  }

  return MAP_CLASS_TEMPLATE
    .replace('{{USING_ENCOUNTERS}}', hasEncounters ? 'using Starfield.Core.Encounters;\n' : '')
    .replace('{{TILE_LEGEND}}', tileLegend)
    .replace(/\{\{CLASS_NAME\}\}/g, className)
    .replace('{{WORLD_ID}}', escapeString(worldId))
    .replace('{{MAP_ID}}', escapeString(mapId))
    .replace('{{DISPLAY_NAME}}', escapeString(mapName))
    .replace('{{WIDTH}}', String(mapWidth))
    .replace('{{HEIGHT}}', String(mapHeight))
    .replace('{{TILE_SIZE}}', String(cellSize))
    .replace('{{BASE_TILE_DATA}}', formatBaseTiles(mapData, mapWidth, mapHeight, tilesById, baseTile))
    .replace('{{OVERLAY_TILE_DATA}}', formatOverlayTiles(mapData, mapWidth, mapHeight, tilesById))
    .replace('{{WALKABLE_TILE_IDS}}', formatWalkableIds(walkableIds))
    .replace('{{ENCOUNTER_DATA}}', formatEncounterData(encounterData, species))
    .replace('{{EXTRA_CTOR_ARGS}}', extraCtorArgs)
}

// --- Registry C# export ---

function toCSharpCategory(category: string): string {
  // Editor uses lowercase category strings; C# uses PascalCase enum
  const map: Record<string, string> = {
    terrain: 'Terrain',
    decoration: 'Decoration',
    interactive: 'Interactive',
    entity: 'Entity',
    trainer: 'Trainer',
    encounter: 'Encounter',
    structure: 'Structure',
    item: 'Item',
    transition: 'Transition',
  }
  return map[category] ?? 'Terrain'
}

function formatTileDefinition(tile: EditorTileDefinition): string {
  const cat = toCSharpCategory(tile.category)
  let args = `${tile.id}, "${escapeString(tile.name)}", ${tile.walkable.toString()}, "${escapeString(tile.color)}", TileCategory.${cat}`
  if (tile.encounter) {
    args += `, "${escapeString(tile.encounter)}"`
  }
  return `        [${tile.id}] = new TileDefinition(${args}),`
}

export function exportRegistryCSharp(registry: EditorTileRegistry): string {
  const sorted = registry.tiles.slice().sort((a, b) => a.id - b.id)

  // Group tiles by category for comments
  const groups: { label: string; tiles: EditorTileDefinition[] }[] = []
  let currentCat = ''
  for (const tile of sorted) {
    if (tile.category !== currentCat) {
      currentCat = tile.category
      const catDef = registry.categories.find(c => c.id === currentCat)
      groups.push({ label: catDef?.label ?? currentCat, tiles: [] })
    }
    groups[groups.length - 1].tiles.push(tile)
  }

  const tileLines: string[] = []
  for (const group of groups) {
    const first = group.tiles[0].id
    const last = group.tiles[group.tiles.length - 1].id
    tileLines.push(``)
    tileLines.push(`        // ${group.label} (${first}-${last})`)
    for (const tile of group.tiles) {
      tileLines.push(formatTileDefinition(tile))
    }
  }

  return `namespace Starfield.Core.Maps;

public static class TileRegistry
{
    private static readonly Dictionary<int, TileDefinition> _tiles = new()
    {${tileLines.join('\n')}
    };

    public static TileDefinition? GetTile(int id) =>
        _tiles.TryGetValue(id, out var tile) ? tile : null;

    public static IEnumerable<TileDefinition> GetTilesByCategory(TileCategory category) =>
        _tiles.Values.Where(t => t.Category == category);

    public static IEnumerable<TileDefinition> AllTiles => _tiles.Values;

    public static int Count => _tiles.Count;
}
`
}
