# 34 - Map Editor & 3D Scene Integration — Lessons Learned Handoff

## 1) What We Accomplished

### Shader Experiment (Reverted)
- Built full custom HLSL shader pipeline (`PokemonEffect.fx`) for vertex color blending.
- Modified 7 files across the skinned model pipeline.
- Discovered vertex color alpha blending turns all baked Pokemon white (`alpha=1` everywhere).
- **Reverted entirely** back to `BasicEffect`. Shader files left in `Content/` for reference.

### Mountain Scene Fixes
- **Fixed white mountain**: `EnableDefaultLighting()` blows out light rock textures. Replaced with manual lighting (ambient 0.15, warm key 0.7, blue fill).
- **Fixed black return beam**: `VertexPositionColor` has no normals → `BasicEffect` lighting = black. Set `LightingEnabled = false` during beam draw.
- **Fixed character floating**: Replaced fake radial cone with `FbxModel.SampleHeight()` — barycentric ray-triangle intersection against actual FBX mesh geometry.
- **Fixed camera clipping**: Elevation-aware camera with asymmetric smoothing (rises 0.3s, falls 1.5s) plus terrain-surface push via `TerrainHeightSampler` callback.

### Model Management
- Rescaled Arceus baked `model.dae` (÷100 via `rescale_sunmoon.py`).

## 2) What Work Remains

### Map Editor ↔ 3D Scene Integration

**Pipeline (corrected)**:
```
C# TileRegistry.cs                      ← SOURCE OF TRUTH
  ↓
File > Load Registry (C#)                ← editor imports .cs from disk
  ↓
parseCSharpRegistry()                    ← regex parses C# into tile palette
  ↓
Editor palette populated                 ← user paints map
  ↓
File > Export Map (C#)                   ← generates .g.cs MapDefinition
  ↓
Copy to Core/Maps/Generated/ → compile  ← MapCatalog auto-registers
  ↓
MapScene3DScreen                         ← reads tiles, resolves ModelId → FbxModel
```

**Required changes**:
1. Add `ModelId` field to `TileDefinition.cs` — e.g. `ModelId: "Rock01"` maps to `Rock01.fbx`.
2. Add `ModelId` to relevant tiles in `TileRegistry.cs` (decoration, structure tiles).
3. Update editor's `parseCSharpRegistry()` regex to capture `ModelId: "..."`.
4. Update editor's `formatTileDefinition()` and `exportRegistryCSharp()` to round-trip `ModelId`.
5. Create `MapScene3DScreen.cs` — reads `MapDefinition`, resolves `ModelId` → `FbxModel`, places instances.
6. Wire into `ModelLoaderGame.cs` mode cycle.

### Screen Consolidation
- `TerritoriesScreen`, `MountainSceneScreen`, and `MapScene3DScreen` overlap. Deprecate earlier screens once map-driven placement works.

### Custom Shader (Deferred)
- Needs per-model vertex color analysis before selective application.

## 3) Optimizations — Prime Suspects

1. **FBX model instancing** — maps with 50+ trees draw the same mesh 50 times. Batch into instanced draw calls.
2. **Mesh raycasting spatial index** — `SampleHeight()` linearly scans ALL triangles. Build BVH or grid for O(1) lookups.
3. **Texture deduplication** — 30 Rock instances all load `Rock01_ALB.png` separately. Share via `Dictionary<string, Texture2D>` cache.
4. **Shared FbxModel cache** — `Dictionary<string, FbxModel>` keyed by FBX path. Load once, reuse for all instances.

## 4) Step-by-Step to Get App Fully Working

1. Build:
   ```bash
   dotnet build src/Starfield2026.3DModelLoader/Starfield2026.3DModelLoader.csproj
   ```
2. Confirm assets at `src/Starfield2026.Assets/Models/Maps/Mountain/Models/*.fbx`.
3. Run: `dotnet run --project src/Starfield2026.3DModelLoader`
4. Verify all modes (F1 to cycle): Character, Map, Mountain.
5. Mountain mode: rock texture visible (not white), character on mesh (not floating), camera above terrain on descent.
6. **Close app before rebuilding** — EXE lock will fail the build.

## 5) How to Start/Test

### 3D Model Loader
```bash
dotnet run --project src/Starfield2026.3DModelLoader
```
F1 = cycle modes, WASD = move, Shift = run, Alt = deploy Pokemon.

### Map Editor
```bash
cd src/Starfield2026.MapEditor/frontend && npm run dev
```
- File > Load Registry (C#) → select `TileRegistry.cs` → palette populates
- Paint map → File > Export Map (C#) → save `.g.cs`
- File > Export Registry (C#) → regenerate `TileRegistry.cs` from editor state

## 6) Issues + Strategies

### Issues
- Two registries existed (editor's `default.json` vs C# `TileRegistry.cs`) with different ID schemes — now understood: C# is source of truth, editor imports it.
- Vertex colors cause universal whitening on baked Pokemon.
- Camera clips on steep descent slopes.
- FBX model scale inconsistency across asset packs.

### Strategies

1. **C# TileRegistry.cs as single source of truth** — editor imports it, never maintain tiles separately.
2. **Per-model vertex color analysis** — Python script scanning `.dae` files for non-trivial alpha. Only apply shader to those models.
3. **Asset profile metadata** — per-pack JSON (`unitScale`, `upAxis`, `textureRoot`) normalized at load time.
4. **Ray-march camera** — march from lookAt to camera position, pull camera forward at first terrain hit. Handles steep faces and overhangs.

## 7) Architecture

### Map System (`Starfield2026.Core.Maps`)
```
TileDefinition (record)        TileCategory (enum)
  Id, Name, Color, Walkable      Terrain, Decoration, Interactive,
  Category, Height               Entity, Trainer, Encounter,
  OverlayBehavior, EntityId      Structure, Item, Transition, Spawn
  SpriteName, AnimationFrames
  ModelId (NEW) ←── "Rock01", "Tree01", etc.

TileRegistry (static)          MapDefinition (abstract)
  Dictionary<int, Tile>          base tile grid int[]
  ↓                              overlay tile grid int?[]
  SOURCE OF TRUTH                warps, connections, encounters

MapCatalog (static)            WorldDefinition → WorldRegistry
  LoadAllMaps() via reflection
```

### Map Editor (`Starfield2026.MapEditor`)
```
MenuBar.tsx
  File > Load Registry (C#)    → parseCSharpRegistry() → setRegistry()
  File > Export Map (C#)        → generateMapClass() → .g.cs
  File > Export Registry (C#)   → exportRegistryCSharp() → TileRegistry.cs

registryService.ts
  parseCSharpRegistry()         ← regex parses C# source code
  parseCSharpMap()              ← regex parses .g.cs MapDefinition

codeGenService.ts
  generateMapClass()            ← template-based C# code gen
  exportRegistryCSharp()        ← generates full TileRegistry.cs
  formatTileDefinition()        ← single tile → C# constructor call
```

### 3D Scene (`Starfield2026.3DModelLoader`)
```
Loaders/FbxLoader.cs          ← FbxModel: Assimp load, SampleHeight()
Rendering/FollowCamera.cs     ← elevation-aware, TerrainHeightSampler
Screens/MountainSceneScreen.cs ← current: hardcoded placement
Screens/MapScene3DScreen.cs    ← planned: map-driven via ModelId
```

## 8) Quick Wins

1. **Add `ModelId` to `TileDefinition`** — one field, unlocks entire 3D pipeline.
2. **Shared `FbxModel` cache** — `Dictionary<string, FbxModel>`, prevents loading same FBX 30 times.
3. **`parseCSharpRegistry()` ModelId support** — one regex update, editor immediately shows model tiles.
4. **Registry round-trip test** — Load `TileRegistry.cs` in editor → Export Registry (C#) → diff. Validates lossless parsing.
