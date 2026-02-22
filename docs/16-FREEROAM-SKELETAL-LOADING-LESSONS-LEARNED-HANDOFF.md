# 16 - FreeRoam & Skeletal Model Loading: Lessons Learned Handoff

**Date:** 2026-02-22
**Scope:** FreeRoam screen, minimap, skeletal character rendering on overworld, COLLADA multi-mesh loading
**Status:** FreeRoam screen fully working with placeholder cube; skeletal model loading renders but geometry is incorrect

---

## 1. What We Accomplished

### FreeRoam Screen (Complete)
- Full on-foot exploration screen with tank-style PlayerController controls
- 500x500 quadrant-colored grid (green NW, blue NE, gold SW, purple SE)
- Third-person chase camera with configurable pitch/distance/follow
- Walk/run/jump with ground detection
- Placeholder cube renders correctly with shadow, proper positioning, rotation

### Circular Rotating Minimap (Complete)
- Pixel-shader style minimap in bottom-left HUD corner
- Rotates with player yaw (screen-up = player forward direction)
- 4 quadrant colors with divider lines that rotate correctly
- North indicator arrow
- Works on both FreeRoam (4 colors) and Overworld (single cyan) screens
- Rotation math: screen-to-world transform `wx = -(lx*cos + ly*sin)`, `wy = lx*sin - ly*cos`

### Camera System (Complete)
- Smooth follow with exponential lerp
- Only follows yaw when player has forward speed (turning in place doesn't drag camera)
- Pitch: -0.15 rad, Distance: 12 units, LookAt height: 1.5 units
- Yaw offset support for manual camera rotation

### Character Select & Manifest System (Complete)
- ManifestScanner scans `Models/` root (both Characters/ and Pokemon/)
- Category inferred from first path segment under models root (`parts[0]`)
- CharacterSelectScreen with folder/name/category lists
- Selected character persists via GameState

### Skeletal Model Loading Pipeline (Partially Working)
- Multi-mesh COLLADA loading (all `<geometry>` + `<skin>` elements merged)
- CPU skinning with bone weight blending (4 bones per vertex)
- Vertex deduplication with `skinWeightIndices` mapping back to original positions
- Joint name matching between skin controllers and skeleton rig (verified 108/108 match)
- COLLADA `INV_BIND_MATRIX` loading and application (verified values match computed)
- Backface culling disabled (`CullNone`) for COLLADA models
- Auto-fit scaling to `TargetHeight = 2.0` game units

---

## 2. What Work Remains

### Critical: Skeletal Model Geometry Rendering Incorrect
The model loads all meshes, all bone weights map correctly, inverse bind matrices are verified correct, but the rendered character still looks wrong. Head meshes appear mispositioned. Switching to a more complex character (108 bones vs 34) makes it worse ("complete mismatch").

### Secondary
- Animation playback: models T-pose (idle animation may not be applying transforms correctly)
- Per-material texturing: current code applies single texture to entire model; old code supports per-mesh textures
- Face mesh depth handling: old code uses `DepthBufferFunction.LessEqual` for Eye/Mouth meshes to render on top of body
- Diagnostic logging still present in `SkinnedDaeModel.Load()` and `FreeRoamScreen.LoadCharacter()` — remove after fix

---

## 3. Prime Suspects (Where to Begin)

### Suspect 1: Geometry-to-Skin Pairing Is Not Explicit
**Current approach:** `LoadGeometry()` iterates `<mesh>` elements in document order and accumulates `skinVertexOffset`. `LoadSkinWeights()` iterates `<skin>` elements in document order and concatenates weights. The implicit assumption is that mesh order = skin order.

**Old approach (PokemonGreen):** Explicitly pairs each geometry with its skin controller via the `<skin source="#geometry_id">` attribute. Each geometry/skin pair is processed independently with its own dedup context and bone weight mapping.

**Why this matters:** If any DAE file has geometries and controllers in different document order, every vertex maps to the wrong bone weights. Even if current files match, the approach is fragile.

**Fix:** Match skins to geometries by ID, not by iteration order. Process each pair independently like the old code.

### Suspect 2: Vertex Weight Mapping Through skinWeightIndices
**Current approach:** Deduped vertices get `skinWeightIndices[i] = skinVertexOffset + posIdx`. Then in `SkinnedDaeModel.Load()`, each vertex looks up `weights[skinWeightIndices[i]]`.

**Problem:** The `skinVertexOffset` is the cumulative POSITION count per mesh. But the skin weight count per skin (from `vcount.Length`) may differ from the position count if the COLLADA file has control points vs vertices distinction.

**Old approach:** Direct lookup: `skin.InfluencesByControlPoint[posIndex]` — the influence list is indexed by the COLLADA control point (position) index, and each geometry/skin pair has its own influence list. No cross-mesh offset needed.

**Fix:** Process each geometry/skin pair independently, building per-mesh vertex+weight data, then merge into final buffers.

### Suspect 3: Single vs Per-Mesh Rendering
**Current approach:** All meshes merged into one VertexBuffer/IndexBuffer, drawn with a single `DrawIndexedPrimitives` call and single texture.

**Old approach:** Each mesh is a separate `SkinnedMesh` with its own texture. Drawn as separate batches via `MeshDrawBatch` with `BaseVertex`/`StartIndex`/`PrimitiveCount`. Face meshes drawn last with `LessEqual` depth.

**Why this matters:** Different meshes may need different textures (body vs face vs eyes). Face meshes need special depth handling to render on top of body geometry at the same depth.

### Suspect 4: Animation Not Applying (T-Pose)
Models are T-posing which could mean: (a) idle animation first frame IS the T-pose (bind pose), or (b) animation transforms aren't being applied. The old code's `ParseMatrix` uses explicit `Matrix.Transpose()` while the new code uses index swizzling — both should produce the same result mathematically, but this hasn't been verified against actual animation data.

---

## 4. Step-by-Step Approach to Get Fully Working

### Phase 1: Verify Geometry in Isolation
1. Add a debug mode to `OverworldCharacter` that shows bind pose only (SkinPose = Identity for all bones, skip animation)
2. If bind pose looks correct, the issue is in skinning/animation
3. If bind pose looks wrong, the issue is in geometry loading

### Phase 2: Port the Old Architecture
The PokemonGreen code is proven working. Rather than debugging the new code's complex merged approach, port the old architecture:

1. **Replace `LoadGeometry` + `LoadSkinWeights`** with the old `ParseGeometries` + `ParseControllers` approach:
   - `ParseGeometries()` returns `Dictionary<geometryId, GeometryData>` — each geometry parsed independently
   - `ParseControllers()` returns `Dictionary<geometryId, ControllerSkinData>` — each skin matched to its geometry by ID
   - `BuildMesh()` builds per-mesh skinned vertices with direct `posIndex → influence` lookup

2. **Replace `SkinnedDaeModel`** with the old mesh-list approach:
   - Store `List<SkinnedMesh>` instead of merged arrays
   - `RebuildBuffers()` combines meshes at render time, computing skin transforms per-vertex
   - Each mesh has its own texture and face flag
   - Draw body meshes first, face meshes last with `LessEqual` depth

3. **Keep the existing skeleton/animation/controller infrastructure** — that's working correctly

### Phase 3: Verify Animation
1. Load a character and check if idle animation produces visible movement
2. Walk/run animations should trigger correctly (tag-based switching already works)
3. If animations don't apply, check that clip bone names match rig bone names

### Phase 4: Polish
1. Per-material texturing (resolve texture per mesh from COLLADA material chain)
2. Remove diagnostic logging
3. Clean up unused inverse bind matrix override code (computed values are already correct)

---

## 5. How to Start/Test

### Build & Run
```bash
cd D:/Projects/Starfield-2026/src
dotnet build
cd Starfield2026.3D
dotnet run
```

### Testing Character Loading
1. Game starts on Overworld screen
2. Press `Cancel` key to cycle screens: Overworld → Driving → Space → FreeRoam → Overworld
3. On FreeRoam, press `Tab` to open pause menu
4. Select "Character Select" from pause menu
5. Choose a character from the list (Categories: Characters, Pokemon)
6. Character loads on FreeRoam map (or falls back to blue cube on failure)

### Debug Logs
- `bin/Debug/net9.0/character_load.log` — mesh stats (vertex/index counts, joint names, weight mapping)
- `bin/Debug/net9.0/Models/Characters/.../skinning_debug.log` — per-joint inverse bind matrix comparison, bone mapping, vertex weight samples

### Quick Validation
- Placeholder cube should render correctly (cyan, proper shadow, follows player movement)
- Minimap should rotate with player, show 4 quadrant colors, north indicator
- Camera should follow behind player, smooth yaw tracking

---

## 6. Issues & New Strategies

### Strategy 1: Port PokemonGreen's SkinnedDaeModel Directly
**Fastest path to working.** The old `SkinnedDaeModel` is self-contained (~705 lines). It handles geometry parsing, skin controller parsing, mesh building, per-mesh texturing, and CPU skinning all in one file. Port it directly, adapting only the constructor/Load signature to match the new project's `SkeletonRig` and `SkeletalAnimationClip` types (which are structurally identical).

**Risk:** Low. The old code is battle-tested.
**Effort:** ~1 hour.

### Strategy 2: A/B Test with Single-Mesh Models First
Load a character that has only 1 mesh (if any exist) or a Pokemon model. If single-mesh models render correctly, the issue is specifically in multi-mesh merging. This narrows the debugging surface.

### Strategy 3: Render Each Mesh Independently in Different Colors
Modify the draw code to render each mesh in a distinct solid color (no texture, no skinning). This reveals whether all mesh parts are present and positioned correctly in bind pose, isolating geometry issues from skinning issues.

### Strategy 4: Binary Comparison of Vertex Data
Export the vertex positions from both the old (PokemonGreen) and new (Starfield) loaders for the same DAE file. Compare the arrays element-by-element. The first divergence reveals exactly where the loading logic differs.

---

## 7. Architecture & Quick Wins

### Current Architecture
```
FreeRoamScreen
  └─ OverworldCharacter
       ├─ AnimationController (tag-based: Idle/Walk/Run)
       │    └─ SkeletalAnimator (skin pose computation)
       │         └─ SkeletonRig (bone hierarchy, inverse bind)
       └─ SkinnedDaeModel (geometry + CPU skinning)
            └─ ColladaSkeletalLoader (DAE parsing)
```

### Quick Win 1: Remove Diagnostic Logging
`SkinnedDaeModel.Load()` has ~30 lines of diagnostic logging that write to `skinning_debug.log`. `FreeRoamScreen.LoadCharacter()` has ~15 lines that re-parse the DAE for stats. Remove both after the rendering issue is fixed.

### Quick Win 2: Face Mesh Depth Handling
Once per-mesh rendering works, add the `LessEqual` depth stencil state for Eye/Mouth meshes (3 lines of code, already proven in PokemonGreen).

### Quick Win 3: Alpha Test for Textures
PokemonGreen forces all texture pixels to `Alpha = 255` to avoid transparency issues with cutout-style textures. Add the same 4-line pixel fixup after `Texture2D.FromStream()`.

### Quick Win 4: Per-Mesh Texture Resolution
The old code resolves textures per-mesh via the COLLADA material chain: `<triangles material="...">` → `<instance_material symbol="..." target="...">` → `<material>` → `<effect>` → `<image>` → file path. This is already implemented in PokemonGreen's `ParseMaterialImageMap()` and `ParseBindMaterialMap()`.

---

## 8. Key Files Reference

| File | Purpose |
|------|---------|
| `Core/Screens/FreeRoamScreen.cs` | FreeRoam screen, camera, character loading |
| `Core/Rendering/Skeletal/OverworldCharacter.cs` | Character lifecycle, animation selection, draw |
| `Core/Rendering/Skeletal/SkinnedDaeModel.cs` | Geometry + skinning (NEEDS REWORK) |
| `Core/Rendering/Skeletal/ColladaSkeletalLoader.cs` | DAE parsing for skeleton, clips, geometry, weights |
| `Core/Rendering/Skeletal/SkeletonRig.cs` | Bone hierarchy, bind/inverse transforms |
| `Core/Rendering/Skeletal/SkeletalAnimator.cs` | Animation playback, skin pose computation |
| `Core/Rendering/Skeletal/AnimationController.cs` | Tag-based animation switching |
| `Core/Rendering/Skeletal/ManifestScanner.cs` | Scans Models/ for manifest.json files |
| `Core/UI/HUDRenderer.cs` | Minimap rendering (bottom-left) |
| `3D/Starfield2026Game.cs` | Game loop, screen management, HUD wiring |
| **PokemonGreen reference:** | |
| `PokemonGreen.Core/Rendering/Skeletal/SkinnedDaeModel.cs` | Working implementation (705 lines) |
| `PokemonGreen.Core/Rendering/Skeletal/ColladaSkeletalLoader.cs` | Working DAE parser (255 lines) |

---

## 9. Coordinate System & Math Reference

- **World:** Y-up, +X = east, +Z = south, -Z = north
- **Player forward at yaw=0:** `(Sin(0), 0, Cos(0))` = `(0, 0, 1)` = south
- **COLLADA models:** Y-up, centimeters (vertex Y range ~0-130 for characters)
- **Fit scale:** `TargetHeight / modelHeight` ≈ `2.0 / 130` ≈ `0.0154`
- **COLLADA matrices:** Row-major, column-vector convention. Transpose to XNA row-vector.
- **Skin pose:** `SkinPose[i] = InverseBindTransforms[i] * WorldPose[i]`
- **Vertex skinning:** `pos = Transform(bindPos, weightedSum(SkinPose[bone] * weight))`
- **Minimap rotation:** Screen→World: `wx = -(lx*cos + ly*sin)`, `wy = lx*sin - ly*cos`

---

## 10. Next Steps (Immediate)

### Step 1: Extract Model Loading to New Project
Create `Starfield2026.3DModelLoader` — a self-contained class library that owns ALL model loading, parsing, manifest scanning, and character database logic. Move the entire `Rendering/Skeletal/` directory from Core. This isolates the broken code, makes it testable independently, and prevents Core from accumulating rendering concerns.

### Step 2: Port PokemonGreen's SkinnedDaeModel
Inside the new `3DModelLoader` project, replace the current broken `SkinnedDaeModel` with a port of PokemonGreen's working implementation. Key differences to adopt:
- Per-geometry/skin explicit pairing via `<skin source="#geometry_id">`
- Per-mesh `SkinnedMesh` objects with independent dedup and bone weight lookup
- Per-mesh texture resolution via COLLADA material chain
- Face mesh depth handling (`LessEqual`)
- `MeshDrawBatch` rendering with `BaseVertex`/`StartIndex`/`PrimitiveCount`

### Step 3: Verify End-to-End
Load a character on FreeRoam, confirm geometry renders correctly in T-pose, then verify idle/walk/run animations play.

### Step 4: Clean Up
Remove diagnostic logging, remove unused inverse bind matrix override code, remove stale log file generation.

---

## 11. Quick Wins After Extraction

| Win | Effort | Impact |
|-----|--------|--------|
| Port `SkinnedDaeModel` from PokemonGreen | 1-2 hrs | Fixes all geometry rendering |
| Force texture alpha to 255 (cutout fix) | 5 min | Eliminates transparency artifacts |
| Face mesh `LessEqual` depth | 5 min | Eyes/mouth render correctly on top of body |
| Per-mesh texturing via material chain | 30 min | Each body part gets correct texture |
| Remove diagnostic logging | 10 min | Cleaner code, no stale log files |
| A/B test single-mesh vs multi-mesh models | 15 min | Narrows root cause if port doesn't work |
