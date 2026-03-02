# 38 - Tree Rendering, Camera Collision, Tile Registry Audit & Model Cache
## Lessons Learned Handoff

**Date:** 2026-03-02
**Scope:** Z-up model rotation, ground clipping fix, proactive camera obstacle avoidance, tile registry audit (150 tiles), TTL model cache, grass scatter rendering

---

## 1. What We Accomplished

### Z-Up Model Rotation Fix
- Some FBX models (exported from Blender) use Z-up instead of Y-up coordinate system, causing trees to appear as flat vertical rectangles
- Added detection in `DrawTileModel()`: if Z extent > Y extent * 1.5, the model is Z-up
- Apply `Matrix.CreateRotationX(-PI/2)` before scaling to convert Z-up to Y-up
- Ground offset uses `BoundsMin.Z` for Z-up models instead of `BoundsMin.Y`

### Ground Clipping Fix
- Trees were half-buried underground due to incorrect ground offset math
- Root cause: the code centered the model at origin (`-modelCenter`) then set `groundOffset = -BoundsMin.Y * scale`, but centering moved the bottom to `-center.Y * scale`, well below ground
- Fix: only center horizontally (X/Z), translate Y by `-BoundsMin.Y` to place model bottom at ground level
- For Z-up: center X/Y, translate Z by `-BoundsMin.Z` before rotation

### Proactive Camera Obstacle Avoidance
- Previous camera only checked height at its final position — clipped through trees between player and camera
- Added two-layer system:
  1. **Proactive scan**: Samples 12 points in a ring (radius 8 tiles) around the player. When trees detected nearby, smoothly increases camera distance and steepens pitch *before* hitting them. Rise time 0.4s, fall time 2.0s (stays elevated while in forest, drops slowly when leaving)
  2. **Raycast safety net**: 10 sample points from lookAt to camera position, finds highest obstacle, pushes camera 2 units above it as a hard floor
- Separate height samplers for player (terrain only) and camera (includes model collision heights via `GetCameraCollisionHeight`)

### Tile ID 6 (Tree01) Mapper Entry
- Tile ID 6 was used in all 5 anime world maps but had no entry in `AnimeForestTileMapper` — rendered as a plain green cube
- Mapped to `AnimeTree_05.fbx`
- Increased BaselineSize: AnimeTree tiles (45-48) from 2.5 to 5.0, Tree01 (6) to 6.0 for variety

### Grass Scatter Rendering
- Registered `TallGrass` (tile 57, Grass01.fbx, Encounter) and `ShortGrass` (tile 59, Grass.fbx, Decoration)
- Grass tiles render 6 instances per tile with deterministic pseudo-random X/Z offsets (spread 0.4) and Y rotation
- Uses a hash-based PRNG seeded by `(x * 7919 + y * 6271) ^ (i * 3571)` for consistent placement across frames
- `IsGrassTile()` checks ModelId for "Grass" or "Grass01" to trigger scatter path

### Tile Registry Audit & Expansion (150 tiles)
- **Removed 17 invalid tiles**: StylizedTree01-11 (IDs 19-20, 30-38) referenced `tree_a` through `tree_k` which don't exist; RockFree01-06 (IDs 39-44) referenced `rock1_LOD0` through `rock6_LOD0` which don't exist
- **Added 4 Stylized Rocks** (IDs 19-22): Rock_1 through Rock_4
- **Added 92 RPG Free models** (IDs 60-149): Nature (bushes, flowers, grass, hills, rocks, trees), Exterior (awnings, fences, sheds), Props (barrels, benches, crates, furniture, etc.), Structures (5 buildings)
- Every tile with a ModelId now maps to a verified `.fbx` file
- Organized by pack: anime_forest (0-10, 45-59), base_tiles (12-14), stylized_rocks (19-22), rpg_free (60-149)

### TTL Model Cache
- Previous `BuildForMap`/`BuildForMaps` called `ClearLoadedAssets()` on every map switch — disposed everything and reloaded from scratch, causing freezes
- New behavior: already-loaded models/textures persist across map switches
- If a model is already loaded from a previous map, `QueueModelPath` skips it
- TTL tracking: every `TryGetModel`/`TryGetTexture` call updates a last-used timestamp
- Assets not used by the current map AND older than 20 minutes are evicted
- Eviction runs on `BuildForMap` calls and every 60 seconds during `PumpQueuedLoads`
- File index (`*.png`, `*.fbx`, `*.dae` directory scan) is cached and only rebuilt if `mapsFolder` changes

---

## 2. What Work Remains

- **White grass models**: Grass/Grass01/GrassMesh FBX models render without textures (white blobs). The FBX materials reference textures that may not be resolving through the current `LoadFbxMaterialTexture` path, or the grass texture from the tile mapper isn't being applied to the model's material
- **Tile mapper coverage**: Only `AnimeForestTileMapper` exists — the 92 RPG Free models (IDs 60-149) and 4 Stylized Rock models (IDs 19-22) have no tile mapper entries, so their textures won't resolve unless embedded in the FBX
- **RPG Free texture format**: RPG Free pack uses `.tga` textures, not `.png`. The texture loader only handles PNG. Need TGA-to-PNG conversion or TGA loading support
- **Stylized Rocks texture format**: Same `.tga` issue as RPG Free
- **Map content**: Maps AF_B through AF_E still have minimal overlay content
- **Skybox/horizon**: Purple/yellow/blue grid still visible at map edges

---

## 3. Optimization Prime Suspects

### 3.1 Grass Scatter Draw Calls (High Impact)
Each grass tile calls `DrawAlphaTested()` 6 times (scatter), each setting vertex buffers and doing a draw call. A 20x20 map with 50 grass tiles = 300 draw calls just for grass.
**Fix:** Instance rendering — build one instance buffer for all grass of the same ModelId across the map, draw all in one call. Or batch the scatter transforms into a single draw.

### 3.2 Per-Frame Obstacle Probe (Medium Impact)
The proactive camera scan samples 36 height points (12 angles x 3 radii) every frame. Each `TerrainHeightSampler` call does `FindMapAt()` which iterates all maps.
**Fix:** Cache the obstacle scan result and only recalculate when the player moves to a new tile coordinate. Use spatial hashing for `FindMapAt()` instead of linear search.

### 3.3 File Index on First Load (Low Impact, Startup)
`EnsureFileIndex` scans the entire Maps directory tree for `.png`, `.dae`, `.fbx` on first load. For large asset folders this is slow.
**Fix:** Cache the file index to a JSON manifest. Only rebuild if the Maps folder modification time changed. Or do the scan on a background thread.

### 3.4 Duplicate Texture Instances (Medium Impact)
`LoadFbxMaterialTexture` in `StaticModel` loads textures per-mesh per-model. The same tree bark texture may be loaded 8 times across 8 AnimeTree models. Each `Texture2D` consumes GPU memory.
**Fix:** Pass a shared texture dictionary into `LoadFbx()` keyed by absolute file path. Before loading, check if the texture is already in the dictionary.

---

## 4. Step-by-Step: Getting the App Fully Working

1. **Prerequisites**: .NET 9 SDK, MonoGame 3.8 (pulled via NuGet)
2. **Restore packages**: `dotnet restore src/Starfield2026.3DModelLoader`
3. **Build**: `dotnet build src/Starfield2026.3DModelLoader`
4. **Assets**: Ensure `src/Starfield2026.Assets/Models/Maps/` contains:
   - `base_tiles/terrain/Grass.png`, `Path.png`, `Indoor_Floor.png`
   - `anime_forest/models/` — 24 FBX files (AnimeTree_01-08, AnimeBush_01-04, Tree01, Bush01, Flower01, Flowers01, Bridge01, Mountain01, Rock01, Rock02, Pebbles01, Grass.fbx, Grass01.fbx, GrassMesh.fbx)
   - `anime_forest/textures/` — textures referenced by FBX materials
   - `stylized_rocks/models/Rock 1-4/Rock_1-4.fbx`
   - `rpg_free/Models/` — Exterior, Nature, Props, Structures subdirectories
5. **Convert TGA textures**: RPG Free and Stylized Rocks use `.tga` — convert to `.png` using the tools in `D:\Projects\Starfield-2026\tools\`
6. **Run**: `dotnet run --project src/Starfield2026.3DModelLoader`
7. **Navigate modes**: F1 cycles FreeRoam -> Map -> AnimeModels -> AnimeWorld
8. **Switch maps**: PgUp/PgDn in Map and AnimeModels modes
9. **Select character**: Tab to open character select overlay

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
- `[StaticModel] FBX mesh 'name': X verts, Y tris, tex=True/False` — mesh loaded (tex=False means missing texture)
- `[StaticModel] FBX loaded: X total verts, N batches` — model ready to render

**MiniToolbox MCP** (for screenshots during development):
- The `mcp__minitoolbox__screenshot` tool captures the active window
- Alt+Tab to the game window before capturing

---

## 6. Known Issues & Strategies

### Issue 1: Grass Models Render as White Blobs
Grass tiles (57, 58, 59) render 6 scatter instances each but appear solid white — no texture applied.
**Strategy A:** The `AnimeForestTileMapper` maps grass tiles to a texture path (`anime_forest/textures/Grass.png`), but `DrawAlphaTested()` uses the texture embedded in the FBX via `LoadFbxMaterialTexture()`. If the FBX doesn't embed the texture or the path doesn't resolve, the mesh gets no texture. Fix: in `TryLoadOneModel`, after loading the FBX, apply the mapper's texture to any untextured batches.
**Strategy B:** The grass FBX material may reference a `.tga` file that doesn't exist as `.png`. Check what texture path Assimp reports for the grass materials and ensure the file exists at that path.
**Strategy C:** Add a fallback in `DrawAlphaTested()` — if a batch has no texture but the tile's mapper provides one, use the mapper texture from the `TileModelCache`.

### Issue 2: RPG Free & Stylized Rocks Use TGA Textures
The rpg_free and stylized_rocks packs include `.tga` texture files. The `LoadFbxMaterialTexture` method and `TileModelCache` only handle `.png`.
**Strategy:** Run the `tools/tga_to_png.py` script to batch-convert all `.tga` files to `.png` in-place. Then the existing FBX material texture resolver will find them. Alternative: add TGA loading support via `Pfim` or `ImageSharp` NuGet packages.

### Issue 3: No Tile Mapper for RPG Free / Stylized Rocks
Tiles 19-22 and 60-149 have no `AnimeForestTileMapper` entries. The cache's `QueueAssetsForTiles` only queues models when `AnimeForestTileMapper.TryGetAsset()` returns true.
**Strategy:** Make the tile mapper lookup optional — if no mapper entry exists but the tile has a `ModelId`, try to resolve the model file by matching `ModelId + ".fbx"` against the file index directly. This makes the mapper an override, not a requirement.

### Issue 4: Camera Occasionally Snaps When Leaving Dense Forest
The proactive obstacle boost uses `SmoothDamp` with a 2-second fall time, but if the player teleports or moves quickly from dense forest to open area, the camera height change can feel abrupt.
**Strategy:** Increase `ObstacleBoostFallSmoothTime` to 3-4 seconds. Or add hysteresis: only start lowering the camera after the player has been in open space for 1+ seconds.

---

## 7. Architecture & New Features

### Updated Architecture

```
TileRegistry (150 tiles)
  - anime_forest: 0-10, 45-59
  - base_tiles: 12-14
  - stylized_rocks: 19-22
  - rpg_free: 60-149

AnimeForestTileMapper
  (tile ID -> model/tex path override)
         \
          v
      TileModelCache (with TTL)
        - EnsureFileIndex (cached, one-time scan)
        - CollectTileIds from map(s)
        - QueueAssetsForTiles (skip already-loaded)
        - TTL eviction (20 min, checked every 60s)
        - PumpQueuedLoads (1 model/frame, 4ms budget)
                    |
                    v
      MapRenderer.DrawTileModel
        - Z-up detection + rotation
        - Ground alignment (no centering on Y/Z axis)
        - Grass scatter (6 instances, hash-based offsets)
        - DrawAlphaTested (foliage transparency)

FollowCamera
  - Proactive obstacle scan (12 points x 3 radii around player)
  - Smoothed boost (rise 0.4s, fall 2.0s)
  - Raycast safety net (10 samples, lookAt -> camera)
  - Separate TerrainHeightSampler for player vs camera
```

### Quick Wins

1. **Generic tile mapper fallback**: If no mapper entry exists for a tile, auto-resolve `ModelId + ".fbx"` from the file index. Eliminates need for mapper entries for RPG Free/Stylized Rocks — just having the file in the Maps folder is enough.

2. **TGA texture conversion script**: Run `tools/tga_to_png.py` across all asset folders to convert `.tga` to `.png`. One-time batch operation that unlocks RPG Free and Stylized Rocks textures.

3. **Grass texture override**: In `DrawTileModel`, if the model has untextured batches and the tile mapper provides a texture path, load and apply that texture as a fallback. Fixes white grass immediately.

4. **Fog to hide horizon**: Add `FogEnabled = true`, `FogStart = 30f`, `FogEnd = 60f`, `FogColor = Color.DarkSlateBlue` to `_modelEffect` and `_alphaTestEffect`. Five lines of BasicEffect config that eliminates the grid horizon.

5. **Map edge boundaries**: In `AnimeWorldScreen.IsPassable()`, return `false` when `FindMapAt()` returns null. Prevents player from walking into the void.

---

## File Reference

| File | Role |
|------|------|
| `Maps/TileDefinition.cs` | Tile data record (Id, ModelId, TexturePath, BaselineSize, Scale, Height) |
| `Maps/TileRegistry.cs` | Central registry of 150 tile definitions across 4 asset packs |
| `Maps/TileMappers/AnimeForestTileMapper.cs` | Tile ID -> FBX model + texture path mapping for anime forest |
| `Maps/MapDefinition.cs` | Abstract base for maps; added `GetCameraCollisionHeight()` |
| `Rendering/StaticModel.cs` | DAE + FBX model loader (Load, LoadFbx, Draw, DrawAlphaTested) |
| `Rendering/TileModelCache.cs` | TTL-based async texture + model cache (BuildForMap, EnsureFileIndex, EvictExpiredAssets) |
| `Rendering/MapRenderer.cs` | Tile rendering (DrawTileModel, BuildModelWorldMatrix, DrawScatteredModel, Z-up detection) |
| `Rendering/FollowCamera.cs` | Camera with proactive obstacle avoidance + raycast safety net |
| `Runtime/NavigationRuntime.cs` | Terrain config with separate camera height sampler |
| `Screens/AnimeWorldScreen.cs` | Multi-map world with SampleCameraHeight for camera collision |
