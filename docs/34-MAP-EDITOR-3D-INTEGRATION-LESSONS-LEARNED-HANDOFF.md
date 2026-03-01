# 34 - Map Editor & 3D Scene Integration — Lessons Learned Handoff

## 1) What We Accomplished

### Shader Experiment (Reverted)
- Implemented full custom HLSL shader pipeline (`PokemonEffect.fx`) for vertex color blending on Pokemon models.
- Modified 7 files: `SkinnedVertex`, `MeshData`, `MeshLoader`, `MeshBuilder`, `CpuSkinner`, `SkinnedModel`, `PokemonSlot`.
- Discovered vertex color alpha blending applied universally turns all Pokemon white (baked exports have `alpha=1` everywhere).
- **Reverted entirely** back to `BasicEffect` — all custom shader code removed, pipeline restored to original state.
- Shader `.fx`/`.mgfx` files left in `Content/` for future reference.

### Mountain Scene Fixes
- **Fixed white mountain**: Replaced `EnableDefaultLighting()` (blows out light rock textures) with manual lighting — ambient 0.15, warm key 0.7, subtle blue fill.
- **Fixed black return beam**: `VertexPositionColor` has no normals, so `BasicEffect` lighting produced black. Added `LightingEnabled = false` during beam draw.
- **Fixed character floating**: Replaced fake radial cone height approximation with **actual mesh raycasting** (`FbxModel.SampleHeight()`). Uses barycentric ray-triangle intersection against FBX geometry.
- **Fixed camera clipping**: Added elevation-aware camera with asymmetric smoothing (rises in 0.3s, falls over 1.5s) plus terrain-surface push to prevent camera from going through the mountain.

### Model Management
- Rescaled Arceus baked `model.dae` (÷100 via `rescale_sunmoon.py`) to match non-baked scale.

## 2) What Work Remains

### Map Editor ↔ 3D Scene Integration (Primary)
The map editor and 3D scene are currently disconnected. The integration plan:

1. **Create `mountain.json` editor registry** — new tile palette mapping tile IDs to FBX model names (Rock=Rock01.fbx, Tree=Tree01.fbx, etc.).
2. **Add `ModelId` field to `TileDefinition`** — connects tile IDs to FBX stems.
3. **Update code gen** — `codeGenService.ts` must include `ModelId` in exported `TileRegistry.cs`.
4. **Create `MapScene3DScreen`** — reads compiled `MapDefinition` grid, places FBX instances at cells, handles collision via mesh raycasting.
5. **Wire into `ModelLoaderGame`** — add mode cycling for the new screen.

### Pipeline Flow (Corrected)
```
Editor JSON registry (mountain.json)
  → Paint map in editor grid
  → Export C# → .g.cs MapDefinition class
  → Export Registry C# → TileRegistry.cs with ModelId fields
  → Compile into game
  → MapCatalog.LoadAllMaps() auto-registers map
  → MapScene3DScreen reads tile grid → resolves FBX models → places instances
```

### Custom Shader (Deferred)
- Vertex color blending for type-specific Pokemon coloring (Arceus Ice form, etc.) requires per-model analysis — which models have meaningful vertex colors vs baked white.
- Custom shader infrastructure is understood but needs selective application, not universal.

### Screen Consolidation
- `TerritoriesScreen`, `MountainSceneScreen`, and future `MapScene3DScreen` overlap. Territories should eventually be deprecated once map-driven 3D placement works.

## 3) Optimizations — Prime Suspects

1. **FBX model instancing**: `FbxModel` stores one copy of vertex/index data. For maps with 50+ trees, we draw the same mesh 50 times with different world matrices. Batch these into instanced draw calls.

2. **Mesh raycasting performance**: `SampleHeight()` iterates ALL triangles linearly for every position query every frame. Build a spatial index (BVH or grid) for O(1) lookups. Current mountain has ~5K triangles — fine now, but won't scale to full scenes.

3. **Texture deduplication**: `FbxModel` loads textures per-model. When a map has 30 Rock instances, they all reference the same `Rock01_ALB.png` — should share one `Texture2D` via a cache.

4. **Camera terrain query caching**: `TerrainHeightSampler` is called for the camera position every frame, doing full mesh raycasting. Cache the last result and only recompute when camera moves >0.1 units.

## 4) Step-by-Step to Get App Fully Working

1. **Build and verify**:
   ```bash
   dotnet restore
   dotnet build src/Starfield2026.3DModelLoader/Starfield2026.3DModelLoader.csproj
   ```

2. **Confirm asset paths exist**:
   - `src/Starfield2026.Assets/Models/Maps/Mountain/Models/*.fbx`
   - `src/Starfield2026.Assets/Models/Maps/Mountain/Textures/*.png`
   - `src/Starfield2026.Assets/Models/Characters/` (trainer models)
   - `src/Starfield2026.Assets/Models/Pokemon/` (pokemon models)

3. **Run app**:
   ```bash
   dotnet run --project src/Starfield2026.3DModelLoader
   ```

4. **Verify all three modes** (F1 to cycle):
   - **Character mode**: Trainer renders with animations, beam works (red, not black).
   - **Map mode**: 2D tile grid with terrain textures.
   - **Mountain mode**: FBX mountain renders with rock texture (not white), character walks on mesh surface (not floating), camera stays above terrain on descent.

5. **Map editor** (separate process):
   ```bash
   cd src/Starfield2026.MapEditor/frontend
   npm run dev
   ```

6. **Close app before rebuilding** — `dotnet build` will fail with EXE lock if the app is running.

## 5) How to Start/Test

### 3D Model Loader (main game)
```bash
dotnet run --project src/Starfield2026.3DModelLoader
```
- F1 = cycle modes (Character → Map → Mountain)
- Tab = cycle characters
- WASD = move, Shift = run
- Alt = deploy/recall Pokemon

### Map Editor
```bash
cd src/Starfield2026.MapEditor/frontend
npm run dev
```
- Opens at `http://localhost:5173`
- Paint tiles on grid, export as C# `.g.cs`
- Can also export `TileRegistry.cs` from registry

### MiniToolbox MCP Server
- See `src/Starfield2026.MiniToolboxMCP/README.md`
- Provides screenshot tool, GARC/TRPAK extraction, model listing

## 6) Issues + Strategies

### Known Issues
- **Two disconnected registries**: Map editor uses `default.json` (IDs 0-72), C# `TileRegistry.cs` uses IDs 0-119. Different ID schemes for same concepts.
- **Vertex colors cause whitening**: Custom shader applied vertex color alpha blending universally — baked exports have `alpha=1` everywhere, turning all textures white.
- **Camera clipping on descent**: Asymmetric smoothing helps but isn't perfect on steep slopes.
- **FBX model scale inconsistency**: Different asset packs use different unit scales.

### Strategies

1. **Editor as single source of truth for registries**
   - Stop maintaining `TileRegistry.cs` by hand. Use the editor's `exportRegistryCSharp()` to generate it from the JSON registry. One registry, one export.

2. **Per-model vertex color analysis script**
   - Write a Python script that scans all `.dae` files, reports which have non-trivial vertex colors (alpha != 0). Only apply the custom shader to those specific models.

3. **Asset profile metadata**
   - Create a JSON profile per asset pack (`Mountain.profile.json`) with `unitScale`, `upAxis`, `textureRoot`. `FbxModel.Load()` reads the profile to normalize scale automatically. No more manual guessing.

4. **Terrain-aware camera with ray march**
   - Instead of just pushing camera Y above terrain, ray-march from lookAt point to camera position. If any point along the ray hits terrain, pull camera forward to the clear position. This handles steep faces and overhangs.

## 7) Architecture Overview

### Map System (`Starfield2026.Core.Maps`)

```
TileDefinition          TileCategory (enum)
  ├ Id, Name, Color       Terrain, Decoration, Interactive,
  ├ Walkable, Height      Entity, Trainer, Encounter,
  ├ Category              Structure, Item, Transition, Spawn
  ├ OverlayBehavior
  ├ EntityId, SpriteName
  └ ModelId (NEW)       

TileRegistry (static)   MapDefinition (abstract)
  └ Dictionary<int, Tile>   ├ base tile grid (int[])
                            ├ overlay tile grid (int?[])
MapCatalog (static)         ├ walkable set
  └ Dictionary<id, Map>     ├ warps, connections
  └ LoadAllMaps()           └ encounter tables
    (reflection)
                          WorldDefinition → WorldRegistry
```

### Map Editor (`Starfield2026.MapEditor`)

```
frontend/src/
  data/registries/default.json   ← tile palette (JSON)
  types/editor.ts                ← EditorTileDefinition, EditorTileRegistry
  store/editorStore.ts           ← zustand: paint, resize, import/export
  services/
    codeGenService.ts            ← generateMapClass() → .g.cs
                                   exportRegistryCSharp() → TileRegistry.cs
    registryService.ts           ← loadDefaultRegistry(), parseCSharpMap()
```

### 3D Scene (`Starfield2026.3DModelLoader`)

```
Screens/
  MountainSceneScreen.cs   ← current: hardcoded FBX placement
  MapScene3DScreen.cs      ← planned: map-driven FBX placement

Loaders/
  FbxLoader.cs    ← FbxModel: Assimp load, SampleHeight() raycasting

Rendering/
  FollowCamera.cs ← elevation-aware, terrain-push, asymmetric smoothing
```

## 8) New Features & Quick Wins

### New Features Implemented This Session
- `FbxModel.SampleHeight()` — vertical ray-triangle intersection against FBX mesh geometry.
- Elevation-aware `FollowCamera` — asymmetric rise/fall smoothing + terrain surface push via `TerrainHeightSampler` callback.
- Beam lighting fix — `LightingEnabled = false` for `VertexPositionColor` geometry.
- Manual scene lighting — replaced `EnableDefaultLighting()` with tuned 2-light setup.

### Quick Wins (High Impact, Low Effort)
1. **Add `ModelId` to `TileDefinition`** — one field addition, unlocks the entire 3D map pipeline.
2. **Create `mountain.json`** — copy `default.json`, change tiles to match available FBX assets. ~30 minutes.
3. **Registry dropdown in editor** — let users switch registries. The store's `setRegistry()` already supports this.
4. **Shared `FbxModel` cache** — `Dictionary<string, FbxModel>` keyed by FBX path. Load once, reuse for all instances. Prevents loading Rock01.fbx 30 times.

### Architecture Decisions
- The editor's C# code gen is the bridge between the JSON tile palette and the compiled game. No JSON parsing at runtime needed.
- `MapDefinition` base/overlay grid pattern works for both 2D and 3D — base tiles define ground, overlays define objects on top.
- Mesh raycasting is the correct approach for terrain collision (vs heightmaps) since our terrain IS meshes (FBX models), not generated heightmap data.
