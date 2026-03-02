# 37 - Tile Mapper, FBX Loader & Anime World Screen
## Lessons Learned Handoff

**Date:** 2026-03-02
**Scope:** TileMapper wiring, AssimpNet FBX loader, alpha-tested foliage, base tile textures, multi-map anime world screen, map persistence

---

## 1. What We Accomplished

### Tile Mapper Wiring
- Connected `AnimeForestTileMapper` (previously dead code) to the rendering pipeline
- `TileModelCache.BuildForMap()` now consults the tile mapper to resolve model and texture paths for tiles with `ModelId`
- `MapRenderer.DrawTile()` renders 3D `StaticModel` instances for tiles that have loaded models, with automatic scale normalization via bounding box fitting
- Added model file indexing (`.dae` and `.fbx`) alongside existing texture indexing in the cache
- Path resolution includes `.fbx` -> `.dae` fallback for format flexibility

### FBX Loader (AssimpNet)
- Added `StaticModel.LoadFbx()` using AssimpNet (already a project dependency at v5.0.0-beta1)
- Uses `PostProcessSteps.Triangulate | GenerateNormals | FlipUVs` for clean mesh data
- Extracts diffuse textures from Assimp materials with multi-strategy path resolution (direct, filename, textures/ subfolder, parent textures/ folder)
- `TileModelCache.TryLoadOneModel()` routes by file extension: `.fbx` -> `LoadFbx()`, `.dae` -> `Load()`
- Resolved `PrimitiveType` ambiguity between `Assimp.PrimitiveType` and `Microsoft.Xna.Framework.Graphics.PrimitiveType`

### Alpha-Tested Foliage Rendering
- Fixed black squares on tree/bush foliage caused by forcing alpha to 255 on all texture pixels
- Added `StaticModel.DrawAlphaTested()` that uses `AlphaTestEffect` (ReferenceAlpha=128) for textured batches, discarding transparent pixels
- `MapRenderer.DrawTileModel()` uses the alpha-tested path for all tile models

### Base Tile Textures
- Updated `TileRegistry` to use new base tile textures: Grass (ID 1), Path (IDs 12-13), Indoor Floor (new ID 14)
- Texture paths point to `base_tiles/terrain/` subfolder, resolved automatically by recursive PNG indexing
- `AnimeForestTileMapper` updated to match the new grass texture path

### Multi-Map Anime World Screen
- Created `AnimeWorldScreen` that loads all maps with `WorldId == "anime_world"` and renders them as one connected world
- 5 maps in a cross pattern: AF_A (center), AF_B (north), AF_C (west), AF_D (south), AF_E (east)
- Each map is 20x20 tiles, offset by `worldX * width, worldY * height` in world coordinates
- `MapRenderer.DrawWithOffset()` renders tiles at world-offset positions while reading from local map coordinates
- `TileModelCache.BuildForMaps()` unions tile IDs from all maps, indexes assets once
- Cross-map terrain height sampling and collision via `FindMapAt()` -> local coordinate conversion
- Full mode cycle: FreeRoam -> Map -> AnimeModels -> AnimeWorld -> FreeRoam

### Map Persistence Bug Fix
- Fixed: switching maps with PgUp/PgDn never saved the map ID to the database
- `GameUpdateModule` now saves `last_map_id` / `last_anime_map_id` via `Database.SetSetting()` on map switch
- `GameRuntimeCoordinator` reads saved map IDs on startup and passes them as preferred IDs

### Generated Map Fixes
- Fixed duplicate `AnimeForest` class names in AF_A through AF_E -> renamed to `AnimeForestA`-`AnimeForestE` with unique map IDs (`af_a`-`af_e`)

---

## 2. What Work Remains

- **Map content**: AF_B through AF_E need unique overlay data (trees, bushes, paths) — currently they share the same sparse layout as AF_A or have minimal content
- **Map connections/warps**: The `MapConnection` and `WarpConnection` systems exist in `MapDefinition` but are not wired for the anime world maps — edge transitions between maps are handled purely by coordinate math
- **More base tile types**: Only 3 base tiles exist (Grass, Path, Indoor Floor) — additional terrain types like water, sand, dirt would expand map variety
- **AnimeForestTileMapper generalization**: The mapper is hardcoded for anime forest assets; other map themes need their own mappers or a generic mapper system
- **Character select in AnimeWorld**: Animation settings propagate correctly but the character select overlay needs testing in AnimeWorld mode
- **Skybox/horizon**: The purple/yellow grid visible at the map edges should be replaced with a proper skybox or fog

---

## 3. Optimization Prime Suspects

### 3.1 Per-Frame Model Rendering (High Impact)
Every tile with a 3D model calls `DrawAlphaTested()` individually, which sets vertex buffers and draws per-batch. For 5 maps with many tree overlays, this means hundreds of draw calls per frame.
**Fix:** Instance rendering — group tiles by ModelId, build instance buffers, draw all trees of the same type in one call.

### 3.2 Frustum Culling Granularity (Medium Impact)
`IsTileVisible()` creates a `BoundingSphere` per tile with a generous 5-unit radius. For a 100x60 world (5 maps), most tiles are off-screen but still tested.
**Fix:** Hierarchical culling — test map-level bounding boxes first (`20x20` regions), skip entire maps that are off-screen, then cull tiles within visible maps.

### 3.3 Texture/Model Duplication in Cache (Medium Impact)
`LoadFbxMaterialTexture()` loads textures per-mesh, so the same tree texture may be loaded multiple times across different `StaticModel` instances. Each model also stores its own vertex/index buffers.
**Fix:** Shared texture cache at the `TileModelCache` level. When a model loads, check if the texture path is already cached and reuse the `Texture2D` instance.

### 3.4 BuildForMaps File Enumeration (Low Impact, Startup)
`BuildForMaps` calls `Directory.EnumerateFiles` with `SearchOption.AllDirectories` for `.png`, `.dae`, and `.fbx` separately. On large asset folders this scans the tree 3 times.
**Fix:** Single `Directory.EnumerateFiles("*.*")` pass with extension filtering, or cache the file index across rebuilds if the maps folder hasn't changed.

---

## 4. Step-by-Step: Getting the App Fully Working

1. **Prerequisites**: .NET 9 SDK, MonoGame 3.8 (pulled via NuGet)
2. **Restore packages**: `dotnet restore src/Starfield2026.3DModelLoader`
3. **Build**: `dotnet build src/Starfield2026.3DModelLoader`
4. **Assets**: Ensure `src/Starfield2026.Assets/Models/Maps/` contains:
   - `base_tiles/terrain/Grass.png`, `Path.png`, `Indoor_Floor.png`
   - `anime_forest/models/AnimeTree_01.fbx` through `AnimeTree_08.fbx`
   - `anime_forest/models/AnimeBush_01.fbx` through `AnimeBush_04.fbx`
   - `anime_forest/textures/` (textures referenced by the FBX materials)
5. **Run**: `dotnet run --project src/Starfield2026.3DModelLoader`
6. **Navigate modes**: Press F1 to cycle through FreeRoam -> Map -> AnimeModels -> AnimeWorld
7. **Switch maps**: PgUp/PgDn in Map and AnimeModels modes
8. **Select character**: Tab to open character select overlay
9. **Verify models**: Check `modelloader.log` in the bin output directory for `[StaticModel] FBX loaded:` lines confirming mesh/texture counts

---

## 5. How to Start/Test the App

```bash
# Build and run
cd D:\Projects\Starfield-2026
dotnet run --project src/Starfield2026.3DModelLoader

# Build only (check for compile errors)
dotnet build src/Starfield2026.3DModelLoader

# Check the runtime log after launch
cat src/Starfield2026.3DModelLoader/bin/Debug/net9.0/modelloader.log
```

**Key log lines to look for:**
- `[StaticModel] FBX meshes: N, materials: M` — Assimp parsed the FBX
- `[StaticModel] FBX mesh 'name': X verts, Y tris, tex=True` — mesh loaded with texture
- `[StaticModel] FBX loaded: X total verts, N batches` — model ready to render

**MiniToolbox MCP** (for screenshots during development):
- The `mcp__minitoolbox__screenshot` tool captures the active window
- Alt+Tab to the game window before capturing

---

## 6. Known Issues & Strategies

### Issue 1: FBX Models May Use Z-Up Coordinate System
Some FBX exporters (Blender default) use Z-up instead of Y-up, causing models to appear rotated 90 degrees.
**Strategy:** Add a rotation correction in `DrawTileModel()` — detect Z-up from model bounds (if Z extents >> Y extents) and apply `Matrix.CreateRotationX(-MathF.PI / 2)` before scaling.

### Issue 2: Model Scale Mismatch Across Packs
Different asset packs use wildly different unit scales (centimeters vs meters vs arbitrary). The `BaselineSize` normalization handles this, but some models may still appear too large or small.
**Strategy:** Add a per-tile `ModelScale` override in `TileDefinition` or in the tile mapper, allowing fine-grained scale tuning per asset. Log the raw model bounds during loading so scale issues can be diagnosed from the log.

### Issue 3: Texture Filename Collisions
Multiple asset packs may have files named `Grass.png` or `Diffuse.png`. The `ConcurrentDictionary.TryAdd` keeps the first file found, which may not be the correct one.
**Strategy:** Prefer relative-path keys over filename-only keys in the texture/model file index. When queueing textures from the tile mapper, always use the full relative path (e.g., `anime_forest/textures/Grass.png`) rather than just the filename.

### Issue 4: Memory Pressure from Large FBX Models
The anime tree models have up to 257K vertices each. With 14 unique models loaded, vertex buffer memory can be significant.
**Strategy:** LOD system — load lower-poly versions for distant tiles. Or implement view-distance culling that skips model rendering beyond a configurable distance and falls back to colored cubes or billboards.

---

## 7. Architecture & New Features

### Architecture Introduced

```
AnimeForestTileMapper          TileRegistry
  (tile ID -> model/tex path)    (tile ID -> TileDefinition)
         \                         /
          v                       v
      TileModelCache.BuildForMap(s)
        - indexes .png/.dae/.fbx files
        - unions tile IDs from map(s)
        - queues textures + models
        - async loading via PumpQueuedLoads
                    |
                    v
      MapRenderer.DrawTile / DrawWithOffset
        - TryGetModel -> DrawAlphaTested (3D)
        - TryGetTexture -> QuadRenderer (flat)
        - fallback -> CubeRenderer (colored)
```

**Multi-map world rendering:**
```
AnimeWorldScreen
  - holds List<MapDefinition> (all anime_world maps)
  - Draw: for each map, DrawWithOffset(offsetX = worldX*20, offsetZ = worldY*20)
  - SampleHeight/IsPassable: FindMapAt(worldPos) -> local coords -> query map
```

### Quick Wins

1. **Fog/distance fade**: Add distance-based fog to `_modelEffect` and `_alphaTestEffect` to hide the horizon grid — 5 lines of BasicEffect configuration (`FogEnabled`, `FogStart`, `FogEnd`, `FogColor`)

2. **Tile scatter**: The `ScatterCount`/`ScatterSpread` constants already exist in `MapRenderer` (unused). Wire them up to place multiple small props (grass, flowers) per tile with hash-based random offsets for organic-looking ground cover.

3. **Map edge boundaries**: Add invisible collision walls at the outer edges of the anime world to prevent the player from walking into the void. Check in `AnimeWorldScreen.IsPassable()` if `FindMapAt()` returns null -> return false.

4. **Billboard rendering for distant trees**: For trees beyond ~30 tiles from the camera, render a textured quad facing the camera instead of the full 3D model. Huge performance win with minimal visual impact at distance.

---

## File Reference

| File | Role |
|------|------|
| `Maps/TileDefinition.cs` | Tile data record (Id, ModelId, TexturePath, BaselineSize, Scale, Height) |
| `Maps/TileRegistry.cs` | Central registry of 60 tile definitions |
| `Maps/TileMappers/AnimeForestTileMapper.cs` | Tile ID -> FBX model + texture path mapping |
| `Maps/MapDefinition.cs` | Abstract base for maps (base tiles, overlays, warps, WorldX/Y) |
| `Maps/MapCatalog.cs` | Global map registry (auto-discovers via reflection) |
| `Rendering/StaticModel.cs` | DAE + FBX model loader (Load, LoadFbx, Draw, DrawAlphaTested) |
| `Rendering/TileModelCache.cs` | Async texture + model cache (BuildForMap, BuildForMaps, PumpQueuedLoads) |
| `Rendering/MapRenderer.cs` | Tile rendering (Draw, DrawWithOffset, DrawTileModel) |
| `Screens/AnimeWorldScreen.cs` | Multi-map connected world screen |
| `Screens/AnimeModelsScreen.cs` | Single anime forest map screen |
| `Runtime/GameRuntimeCoordinator.cs` | Initialization, cache rebuilding, mode persistence |
| `Runtime/GameUpdateModule.cs` | Mode switching, input handling, map persistence |
| `Runtime/GameDrawModule.cs` | Screen drawing dispatch |
