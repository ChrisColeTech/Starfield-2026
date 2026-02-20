import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'
import type { EditorTileRegistry, EditorTileDefinition } from '../types/editor'
import type { MapEncounterData, EncounterEntry, SpeciesInfo } from '../types/encounters'
import { defaultMapEncounterData, defaultEncounterGroup, defaultEncounterEntry } from '../types/encounters'
import {
  loadDefaultRegistry,
  buildTilesById,
  buildingWidth,
  buildingHeight,
  fallbackTileId,
  parseCSharpMap,
  UNKNOWN_TILE,
} from '../services/registryService'
import { generateMapClass, exportRegistryCSharp } from '../services/codeGenService'

function createGrid(width: number, height: number, fillTile = 1): number[][] {
  return Array.from({ length: height }, () => Array(width).fill(fillTile))
}

function rotateMatrix(tiles: (number | null)[][], width: number, height: number) {
  const rotated: (number | null)[][] = []
  for (let y = 0; y < width; y++) {
    rotated[y] = []
    for (let x = 0; x < height; x++) {
      rotated[y][x] = tiles[height - 1 - x][y]
    }
  }
  return { tiles: rotated, width: height, height: width }
}

function detectBaseTile(mapData: number[][]): number {
  const counts = new Map<number, number>()
  for (const row of mapData) {
    for (const id of row) {
      counts.set(id, (counts.get(id) ?? 0) + 1)
    }
  }
  let maxId = mapData[0]?.[0] ?? 1
  let maxCount = 0
  for (const [id, count] of counts) {
    if (count > maxCount) {
      maxCount = count
      maxId = id
    }
  }
  return maxId
}

// --- Persistence ---

const STORAGE_KEY = 'mapeditor'

interface PersistedState {
  registry: EditorTileRegistry
  mapData: number[][]
  mapWidth: number
  mapHeight: number
  cellSize: number
  mapName: string
  worldId: string
  worldX: number
  worldY: number
  selectedTile: number
  baseTile: number
  encounterData?: MapEncounterData
}

function loadPersisted(): PersistedState | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) return JSON.parse(raw) as PersistedState
  } catch { /* ignore */ }
  return null
}

function savePersisted(state: PersistedState) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
  } catch { /* ignore */ }
}

const saved = loadPersisted()
const defaultReg = loadDefaultRegistry()
const initRegistry = saved?.registry ?? defaultReg
const initTilesById = buildTilesById(initRegistry)
const initBaseTile = saved?.mapData ? detectBaseTile(saved.mapData) : fallbackTileId(initRegistry)

interface EditorState {
  // Registry
  registry: EditorTileRegistry
  tilesById: Map<number, EditorTileDefinition>

  // Map
  mapData: number[][]
  mapWidth: number
  mapHeight: number
  cellSize: number
  mapName: string
  worldId: string
  worldX: number
  worldY: number

  // Encounters
  encounterData: MapEncounterData
  species: SpeciesInfo[]
  speciesLoaded: boolean

  // Selection
  selectedTile: number
  selectedBuilding: number | null
  buildingRotation: number
  baseTile: number

  // Actions — registry
  setRegistry: (registry: EditorTileRegistry) => void
  getTile: (id: number) => EditorTileDefinition

  // Actions — map
  paint: (x: number, y: number, tileId?: number) => void
  resize: (width: number, height: number) => void
  clear: () => void
  rotateMap: (direction: 1 | -1) => void
  setWorldId: (id: string) => void
  setWorldX: (x: number) => void
  setWorldY: (y: number) => void
  setMapData: (data: number[][], width: number, height: number) => void
  setCellSize: (size: number) => void
  setMapName: (name: string) => void

  // Actions — encounters
  loadSpecies: () => Promise<void>
  setProgressMultiplier: (value: number) => void
  addEncounterGroup: (encounterType?: string) => void
  removeEncounterGroup: (index: number) => void
  updateEncounterGroupType: (index: number, encounterType: string) => void
  updateEncounterGroupRate: (index: number, rate: number) => void
  addEncounterEntry: (groupIndex: number) => void
  removeEncounterEntry: (groupIndex: number, entryIndex: number) => void
  updateEncounterEntry: (groupIndex: number, entryIndex: number, partial: Partial<EncounterEntry>) => void

  // Actions — IO
  importJson: (json: string) => void
  importCSharpMap: (source: string) => void
  exportCSharp: () => string
  exportRegistryCSharp: () => string

  // Actions — selection
  selectTile: (id: number) => void
  selectBuilding: (idx: number) => void
  rotateBuilding: (direction: 1 | -1) => void
  setBaseTile: (id: number) => void

  // Actions — building placement
  placeBuilding: (x: number, y: number) => void
  getRotatedBuilding: () => { tiles: (number | null)[][]; width: number; height: number } | null
}

export const useEditorStore = create<EditorState>()(
  immer((set, get) => ({
    registry: initRegistry,
    tilesById: initTilesById,

    mapData: saved?.mapData ?? createGrid(25, 18, initBaseTile),
    mapWidth: saved?.mapWidth ?? 25,
    mapHeight: saved?.mapHeight ?? 18,
    cellSize: saved?.cellSize ?? 24,
    mapName: saved?.mapName ?? 'Untitled Map',
    worldId: saved?.worldId ?? 'default',
    worldX: saved?.worldX ?? 0,
    worldY: saved?.worldY ?? 0,

    encounterData: saved?.encounterData ?? defaultMapEncounterData(),
    species: [],
    speciesLoaded: false,

    selectedTile: saved?.selectedTile ?? fallbackTileId(initRegistry),
    selectedBuilding: null,
    buildingRotation: 0,
    baseTile: initBaseTile,

    setRegistry: (registry) => {
      set(state => {
        state.registry = registry as EditorTileRegistry
        state.tilesById = buildTilesById(registry) as Map<number, EditorTileDefinition>

        if (!registry.tiles.some(t => t.id === state.selectedTile)) {
          state.selectedTile = fallbackTileId(registry)
        }

        if (!registry.tiles.some(t => t.id === state.baseTile)) {
          state.baseTile = fallbackTileId(registry)
        }

        if (state.selectedBuilding !== null && state.selectedBuilding >= registry.buildings.length) {
          state.selectedBuilding = null
          state.buildingRotation = 0
        }
      })
    },

    getTile: (id) => {
      return get().tilesById.get(id) ?? UNKNOWN_TILE
    },

    paint: (x, y, tileId) => set(state => {
      const id = tileId ?? state.selectedTile
      if (y < 0 || y >= state.mapHeight || x < 0 || x >= state.mapWidth) return
      if (state.mapData[y][x] === id && tileId === undefined) {
        state.mapData[y][x] = state.baseTile
      } else {
        state.mapData[y][x] = id
      }
    }),

    resize: (width, height) => set(state => {
      const newMap: number[][] = []
      for (let y = 0; y < height; y++) {
        newMap[y] = []
        for (let x = 0; x < width; x++) {
          newMap[y][x] = state.mapData[y]?.[x] ?? state.baseTile
        }
      }
      state.mapData = newMap
      state.mapWidth = width
      state.mapHeight = height
    }),

    clear: () => set(state => {
      state.mapData = createGrid(25, 18, state.baseTile)
      state.mapWidth = 25
      state.mapHeight = 18
      state.cellSize = 24
      state.mapName = 'Untitled Map'
      state.worldId = 'default'
      state.worldX = 0
      state.worldY = 0
      state.selectedTile = fallbackTileId(state.registry as EditorTileRegistry)
      state.selectedBuilding = null
      state.buildingRotation = 0
      state.encounterData = defaultMapEncounterData() as MapEncounterData
    }),

    rotateMap: (direction) => set(state => {
      const oldW = state.mapWidth
      const oldH = state.mapHeight
      const newW = oldH
      const newH = oldW
      const newMap: number[][] = []

      for (let y = 0; y < newH; y++) {
        newMap[y] = []
        for (let x = 0; x < newW; x++) {
          if (direction === 1) {
            newMap[y][x] = state.mapData[oldH - 1 - x][y]
          } else {
            newMap[y][x] = state.mapData[x][oldW - 1 - y]
          }
        }
      }

      state.mapData = newMap
      state.mapWidth = newW
      state.mapHeight = newH
    }),

    setMapData: (data, width, height) => set(state => {
      state.mapData = data
      state.mapWidth = width
      state.mapHeight = height
    }),

    setCellSize: (size) => set(state => {
      state.cellSize = size
    }),

    setMapName: (name) => set(state => {
      state.mapName = name
    }),

    setWorldId: (id) => set(state => {
      state.worldId = id
    }),

    setWorldX: (x) => set(state => {
      state.worldX = x
    }),

    setWorldY: (y) => set(state => {
      state.worldY = y
    }),

    loadSpecies: async () => {
      if (get().speciesLoaded) return
      try {
        const result = await window.electronAPI.openFile([{ name: 'JSON Files', extensions: ['json'] }])
        if (!result) return
        const data = JSON.parse(result.content) as Array<{ id: number; name: string; type1: string; type2?: string | null }>
        const species: SpeciesInfo[] = data.map(s => ({
          id: s.id,
          name: s.name,
          type1: s.type1,
          type2: s.type2 ?? null,
        }))
        set(state => {
          state.species = species as SpeciesInfo[]
          state.speciesLoaded = true
        })
      } catch {
        alert('Failed to load species data')
      }
    },

    setProgressMultiplier: (value) => set(state => {
      state.encounterData.progressMultiplier = value
    }),

    addEncounterGroup: (encounterType) => set(state => {
      state.encounterData.encounterGroups.push(defaultEncounterGroup(encounterType) as ReturnType<typeof defaultEncounterGroup>)
    }),

    removeEncounterGroup: (index) => set(state => {
      state.encounterData.encounterGroups.splice(index, 1)
    }),

    updateEncounterGroupType: (index, encounterType) => set(state => {
      state.encounterData.encounterGroups[index].encounterType = encounterType
    }),

    updateEncounterGroupRate: (index, rate) => set(state => {
      state.encounterData.encounterGroups[index].baseEncounterRate = Math.max(0, Math.min(255, rate))
    }),

    addEncounterEntry: (groupIndex) => set(state => {
      state.encounterData.encounterGroups[groupIndex].entries.push(defaultEncounterEntry() as ReturnType<typeof defaultEncounterEntry>)
    }),

    removeEncounterEntry: (groupIndex, entryIndex) => set(state => {
      state.encounterData.encounterGroups[groupIndex].entries.splice(entryIndex, 1)
    }),

    updateEncounterEntry: (groupIndex, entryIndex, partial) => set(state => {
      const entry = state.encounterData.encounterGroups[groupIndex].entries[entryIndex]
      Object.assign(entry, partial)
    }),

    importJson: (json) => {
      try {
        const data = JSON.parse(json)

        // Schema v2: baseTiles + overlayTiles
        if (data.baseTiles) {
          const width = data.width
          const height = data.height
          const base: number[][] = data.baseTiles
          const overlay: (number | null)[][] | undefined = data.overlayTiles

          const merged: number[][] = []
          for (let y = 0; y < height; y++) {
            merged[y] = []
            for (let x = 0; x < width; x++) {
              const ov = overlay?.[y]?.[x]
              merged[y][x] = ov != null ? ov : (base[y]?.[x] ?? 1)
            }
          }

          set(state => {
            state.mapData = merged
            state.mapWidth = width
            state.mapHeight = height
            state.mapName = data.displayName || data.mapId || 'Imported Map'
            if (data.tileSize) state.cellSize = data.tileSize
            if (data.worldId) state.worldId = data.worldId
            state.worldX = data.worldX ?? 0
            state.worldY = data.worldY ?? 0
            state.baseTile = detectBaseTile(merged)
          })
          return
        }

        // Legacy format: tiles (flat 2D array)
        if (data.tiles) {
          set(state => {
            state.mapData = data.tiles
            state.mapWidth = data.width
            state.mapHeight = data.height
            state.mapName = data.name || 'Imported Map'
            state.baseTile = detectBaseTile(data.tiles)
          })
          return
        }
      } catch {
        alert('Invalid JSON file')
      }
    },

    importCSharpMap: (source) => {
      try {
        const parsed = parseCSharpMap(source)
        set(state => {
          state.mapData = parsed.mapData
          state.mapWidth = parsed.width
          state.mapHeight = parsed.height
          state.mapName = parsed.displayName
          state.cellSize = parsed.tileSize
          state.worldId = parsed.worldId
          state.worldX = parsed.worldX
          state.worldY = parsed.worldY
          state.baseTile = detectBaseTile(parsed.mapData)
          state.encounterData = parsed.encounterData as MapEncounterData
        })
      } catch (err) {
        alert(`Invalid C# map: ${err instanceof Error ? err.message : 'Unknown error'}`)
      }
    },

    exportCSharp: () => {
      const { mapData, mapWidth, mapHeight, cellSize, mapName, worldId, worldX, worldY, tilesById, baseTile, encounterData, species } = get()
      return generateMapClass(mapData, mapWidth, mapHeight, mapName, cellSize, tilesById as Map<number, EditorTileDefinition>, worldId, worldX, worldY, baseTile, encounterData, species)
    },

    exportRegistryCSharp: () => {
      const { registry } = get()
      return exportRegistryCSharp(registry as EditorTileRegistry)
    },

    setBaseTile: (id) => set(state => {
      const oldBase = state.baseTile
      state.baseTile = id
      // Replace all old base tiles on the map with the new one
      for (let y = 0; y < state.mapHeight; y++) {
        for (let x = 0; x < state.mapWidth; x++) {
          if (state.mapData[y][x] === oldBase) {
            state.mapData[y][x] = id
          }
        }
      }
    }),

    selectTile: (id) => set(state => {
      state.selectedTile = id
      state.selectedBuilding = null
      state.buildingRotation = 0
    }),

    selectBuilding: (idx) => set(state => {
      if (state.selectedBuilding === idx) {
        state.selectedBuilding = null
        state.buildingRotation = 0
      } else {
        state.selectedBuilding = idx
        state.buildingRotation = 0
      }
    }),

    rotateBuilding: (direction) => set(state => {
      if (state.selectedBuilding === null) return
      state.buildingRotation = (state.buildingRotation + direction + 4) % 4
    }),

    placeBuilding: (x, y) => {
      const state = get()
      if (state.selectedBuilding === null) return
      const rotated = state.getRotatedBuilding()
      if (!rotated) return

      set(draft => {
        for (let by = 0; by < rotated.height; by++) {
          for (let bx = 0; bx < rotated.width; bx++) {
            const tx = x + bx
            const ty = y + by
            const tileId = rotated.tiles[by][bx]
            if (ty < draft.mapHeight && tx < draft.mapWidth && tileId !== null) {
              draft.mapData[ty][tx] = tileId
            }
          }
        }
      })
    },

    getRotatedBuilding: () => {
      const { selectedBuilding, buildingRotation, registry } = get()
      if (selectedBuilding === null) return null
      const building = registry.buildings[selectedBuilding]
      if (!building) return null

      let tiles = building.tiles.map(row => [...row])
      let w = buildingWidth(building)
      let h = buildingHeight(building)

      for (let r = 0; r < buildingRotation; r++) {
        const result = rotateMatrix(tiles, w, h)
        tiles = result.tiles
        w = result.width
        h = result.height
      }

      return { tiles, width: w, height: h }
    },
  }))
)

// Auto-save to localStorage on every state change
useEditorStore.subscribe((state) => {
  savePersisted({
    registry: state.registry,
    mapData: state.mapData,
    mapWidth: state.mapWidth,
    mapHeight: state.mapHeight,
    cellSize: state.cellSize,
    mapName: state.mapName,
    worldId: state.worldId,
    worldX: state.worldX,
    worldY: state.worldY,
    selectedTile: state.selectedTile,
    baseTile: state.baseTile,
    encounterData: state.encounterData,
  })
})
