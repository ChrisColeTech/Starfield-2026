# Starfield Map Editor

Tile-based map editor for Starfield. Built with React, TypeScript, and Vite.

## Quick Start

```bash
cd src/Starfield.MapEditor
npm install
npm run dev
```

Opens at `http://localhost:5173`.

## Features

- **Tile painting** — Left-click to paint, right-click to erase (reset to grass), drag to paint continuously
- **Building prefabs** — Pre-built structures (Pokecenter, Gym, Houses, etc.) placed with a single click, with rotation support
- **Two-panel layout** — Left sidebar for tile/building palette, right panel for live map inventory
- **Collapsible panels** — Both sidebar and properties panel collapse to icons
- **Import/Export** — Schema v2 JSON format with base + overlay tile layers
- **Dynamic tile registry** — Tile definitions loaded from JSON; swap registries via File > Load Registry

## Controls

| Action | Input |
|---|---|
| Paint tile | Left-click |
| Erase to grass | Right-click |
| Drag paint | Hold left-click and drag |
| Place building | Select building, then left-click |
| Rotate building | Rotate Left / Rotate Right buttons |

## Project Structure

```
src/
  components/
    canvas/
      MapGrid.tsx          — Interactive tile grid (CSS Grid rendering)
    layout/
      AppShell.tsx         — Root layout (MenuBar + Sidebar + Canvas + Properties)
      CanvasContainer.tsx  — Scrollable canvas wrapper
      MenuBar.tsx          — File/Edit/View dropdown menus
      PropertiesPanel.tsx  — Right panel: live tile inventory by category
      Sidebar.tsx          — Left panel: tile palette, buildings, map controls
  data/
    registries/
      default.json         — Default tile registry (51 tiles, 5 categories, 11 buildings)
  services/
    registryService.ts     — Registry loader, parser, validation, derived lookups
  store/
    editorStore.ts         — Zustand + immer store (map state, selection, IO, registry)
  types/
    editor.ts              — TypeScript types (EditorTileRegistry, EditorTileDefinition, etc.)
  App.tsx                  — App root
  main.tsx                 — Entry point
  index.css                — Global styles + Tailwind import
```

## Map JSON Format (Schema v2)

```json
{
  "schemaVersion": 2,
  "mapId": "pallet_town",
  "displayName": "Pallet Town",
  "tileSize": 32,
  "width": 25,
  "height": 18,
  "baseTiles": [[1, 1, ...], ...],
  "overlayTiles": [[null, 3, ...], ...],
  "registryId": "pokemon-green-default",
  "registryVersion": "1.0.0"
}
```

- `baseTiles` — 2D array `[y][x]` of tile IDs (required for every cell)
- `overlayTiles` — 2D array `[y][x]` of nullable tile IDs (null = no overlay)
- On import, overlay values are flattened onto base (overlay wins where non-null)

## Registry JSON Format

```json
{
  "id": "pokemon-green-default",
  "name": "Pokemon Green Default",
  "version": "1.0.0",
  "categories": [
    { "id": "terrain", "label": "Terrain", "showInPalette": true }
  ],
  "tiles": [
    { "id": 0, "name": "Water", "color": "#1a4a7a", "walkable": false, "category": "terrain" }
  ],
  "buildings": [
    { "id": "pokecenter", "name": "Pokecenter", "tiles": [[3,3,3,3],[3,4,4,3],[3,4,4,3],[6,4,4,6]] }
  ]
}
```

Building width/height are derived from the `tiles` matrix at runtime.

## Tech Stack

- React 19 + TypeScript 5.9
- Vite 7 + Tailwind CSS 4
- Zustand 5 + immer (state management)
- Lucide React (icons)
