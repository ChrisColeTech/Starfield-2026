# 24 - Map Viewer & Static Model Loading: Lessons Learned Handoff

**Date:** 2026-02-23
**Scope:** Map manifest generation, static (unskinned) DAE model loading, map viewer screen, dual-mode screen switching in 3DModelLoader
**Status:** Compiles clean (0 warnings, 0 errors). Map viewer screen renders grid + orbit camera. Static model rendering needs verification — geometry loads but some models may render invisible due to Collada material binding differences. Map asset curation still needed (many "map" assets are individual props, not full maps).

---

## 1. What We Accomplished

### Manifest Gap Fix
- Discovered 5 of 285 map model folders were missing `manifest.json` files
- Created `scripts/create-missing-map-manifests.py` — generates manifests matching the established format (version, format, modelFile, textures, clips, name, dir, modelFormat, assetsPath)
- All 285 map model folders now have proper manifests across `scarlet/maps/` and `sun-moon/maps/`
- Existing scripts (`fix-manifests.py`, `fix-field-manifests.py`) only patch existing manifests — they cannot create from scratch

### MapManifestScanner (`Animations/MapManifestScanner.cs`, 71 lines)
- Counterpart to `ManifestScanner` which explicitly **skips** Maps category (line 36-37 of original)
- Requires `modelFile` field instead of `clips` field (map models are static, no animations)
- Parses `Maps/<source>/maps/<model>` path structure into category + subfolder

### StaticModel Renderer (`Rendering/StaticModel.cs`, 259 lines)
- Loads Collada DAE files **without skeleton/skinning** — reuses `MeshLoader` and `TextureResolver`
- Builds `VertexPositionNormalTexture` buffers directly from VERTEX/NORMAL/TEXCOORD semantics
- Per-batch rendering: textured meshes use loaded textures, untextured meshes fall back to flat gray diffuse
- Computes bounding box (BoundsMin/Max), center, and radius for camera framing
- Diagnostic logging: geometry count, vertex count, bounds, texture hit/miss per mesh

### MapSelectOverlay (`Screens/MapSelectOverlay.cs`, 287 lines)
- Follows `CharacterSelectOverlay` pattern exactly: Category → Subfolder → Items hierarchy
- Key-repeat acceleration for smooth scrolling through 285+ entries
- Green accent theme (vs purple for characters) to visually distinguish modes
- Right panel shows selected map name, source, and group
- Uses `MapRecord` (parallel to `CharacterRecord` but not database-backed)

### MapViewerScreen (`Screens/MapViewerScreen.cs`, 179 lines)
- Orbit camera with pan (WASD relative to camera facing), rotate (Q/E/R/F), zoom (Z/X/scroll)
- Model offset: automatically lifts model so its bottom sits at Y=0 (above grid)
- Camera auto-frames model on load (2.5x radius distance)
- `CullNone` rasterizer state — Collada models have inconsistent triangle winding
- BasicEffect with default lighting, ambient boosted to 0.3

### Dual-Mode Screen Switching (`ModelLoaderGame.cs`)
- **Escape** toggles between Character mode (FreeRoamScreen) and Map mode (MapViewerScreen)
- **Tab** opens the appropriate select overlay depending on current mode
- Title bar shows mode indicator: `[Esc] Maps` or `[Esc] Characters`
- Map index and last-selected persisted via database settings

---

## 2. What Work Remains

### Critical (Must Complete)
1. **Curate actual map assets** — The `Maps/` folder contains ~285 entries but many are individual props (barrels, fences, plants, food items). True map/environment models need to be identified and possibly reorganized into subcategories like `terrain/`, `buildings/`, `battle-arenas/`, `props/`.
2. **Verify static model rendering** — User reported blue screen (just clear color visible). Root cause is likely one of the issues in section 3. Need to test with a known-good simple DAE file to confirm the rendering pipeline works end-to-end.
3. **Material binding for map DAEs** — The Collada material chain (symbol → material → effect → image) may differ between character models (which work) and map models (which may use a different binding structure). Need to inspect a map DAE's XML to verify the texture resolution path.

### Important (Should Complete)
4. **Database integration for maps** — Currently maps use a simple in-memory `List<MapRecord>` with auto-incremented IDs. Should mirror `CharacterDatabase` with a `maps` table for persistence and faster lookups.
5. **Map preview/thumbnail system** — With 285+ entries, browsing by name alone is slow. A thumbnail cache (render each model once at low res, save as PNG) would greatly improve the selection experience.
6. **Hot-reload on file changes** — Currently maps are scanned once at startup. A FileSystemWatcher on the Maps folder would allow adding new models without restarting.

### Nice to Have
7. **Multi-model scene composition** — Load multiple static models into one scene (terrain + buildings + props) to compose full environments.
8. **Wireframe toggle** — Add a key (e.g., `G`) to toggle wireframe rendering for debugging geometry issues.
9. **Model info overlay** — Display vertex count, triangle count, bounds, texture count on-screen for the loaded model.

---

## 3. Optimizations — Prime Suspects

### 3.1 Collada Material Chain Mismatch
**Symptom:** Model loads with 0 textured batches, renders as flat gray or invisible.
**Cause:** Map DAE files may use `<instance_geometry>` instead of `<instance_controller>`, which means `<bind_material>` lives in a different XML path than what `TextureResolver.ParseBindMaterialMap` searches.
**Fix:** Audit `ParseBindMaterialMap` to also search under `<instance_geometry>` nodes, not just `<instance_controller>`.

### 3.2 Geometry Filtering in MeshLoader
**Symptom:** `MeshLoader.Load()` returns geometries but `BuildVertices` returns null for all of them.
**Cause:** Some DAE files use `<polylist>` with `<vcount>` (variable polygon sizes) instead of `<triangles>`. `MeshLoader` handles both but polygon triangulation may produce unexpected stride/index layouts.
**Fix:** Log the stride and input count per mesh. If stride is unexpected (e.g., 0 or >4), the mesh format needs special handling.

### 3.3 Coordinate System / Scale
**Symptom:** Model loads and renders but is microscopic or enormous, appearing as empty scene.
**Cause:** Collada files may use different up-axis (`Y_UP` vs `Z_UP`) or unit scales. Map models from different sources may use meters vs centimeters.
**Fix:** Read `<asset><up_axis>` and `<asset><unit>` from the DAE header. Apply a correction transform in `StaticModel.Load()` or `MapViewerScreen` world matrix. Log the bounds to verify scale.

### 3.4 Vertex Buffer Batching
**Symptom:** Loading large map models is slow (multiple seconds).
**Cause:** Each mesh gets its own `BuildVertices` call creating fresh arrays, then everything is merged into a single VB/IB. For models with 50+ sub-meshes this creates GC pressure.
**Fix:** Pre-allocate based on total index count from all meshes. Use `Span<T>` or pooled arrays for intermediate vertex data.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
- .NET 9 SDK installed
- MonoGame 3.8+ (pulled via NuGet on build)
- Map model assets present at `src/Starfield2026.Assets/Models/Maps/`

### Build & Run
```bash
cd D:/Projects/Starfield-2026/src/Starfield2026.3DModelLoader
dotnet build
dotnet run
```

### Test Character Mode (Known Working)
1. App starts in Character mode with the last-selected character loaded
2. **WASD** to move, **Shift** to run, **Space** to jump
3. **Q/E** to orbit camera, **R/F** to pitch, **Z/X** to zoom
4. **Tab** to open character select overlay → pick a character → **Enter** to load

### Test Map Mode (New)
1. **Escape** to switch from Character mode to Map mode
2. Grid and dark background should appear (no model loaded yet)
3. **Tab** to open map select overlay
4. Browse categories (scarlet, sun-moon) → pick a map → **Enter** to load
5. **WASD** to pan, **Q/E** to orbit, **R/F** to pitch, **Z/X** or scroll to zoom
6. **Escape** to switch back to Character mode

### Verify Rendering
1. After loading a map, check `modelloader.log` in the bin/Debug/net9.0 output directory
2. Look for `[StaticModel]` log lines:
   - `Geometries found: N` — should be > 0
   - `Geometry 'xxx': N verts, N tris, tex=True/False` — per-mesh breakdown
   - `Loaded: N total verts, N batches, bounds ...` — final summary
3. If `Geometries found: 0` — the DAE has no `<geometry>` elements (bad file)
4. If geometries found but 0 verts — `BuildVertices` is failing (check VERTEX semantic)
5. If verts loaded but nothing visible — camera/scale issue (check bounds vs camera distance)

---

## 5. How to Start/Test

### Quick Start
```bash
# Build
cd D:/Projects/Starfield-2026/src/Starfield2026.3DModelLoader
dotnet build

# Run
dotnet run

# Or run the built exe directly:
bin/Debug/net9.0/Starfield2026.3DModelLoader.exe
```

### Log File Location
```
D:/Projects/Starfield-2026/src/Starfield2026.3DModelLoader/bin/Debug/net9.0/modelloader.log
```
This file is recreated each launch. All `ModelLoaderLog.Info()` calls write here. Check after loading a map to diagnose rendering issues.

### Database Location
```
D:/Projects/Starfield-2026/src/Starfield2026.3DModelLoader/bin/Debug/net9.0/modelloader.db
```
SQLite database storing character records and settings. Delete to force full rescan.

### Manifest Script
```bash
# Dry run — see what would be created
python scripts/create-missing-map-manifests.py --dry-run D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models/Maps

# Apply
python scripts/create-missing-map-manifests.py D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models/Maps
```
Note: Must use venv python (`D:/Projects/py-to-cpp/.venv/Scripts/python.exe`) if hook blocks bare `python`.

---

## 6. Known Issues & Strategies

### Issue 1: Blue Screen — Model Not Rendering
**Symptom:** Map viewer shows only the clear color (dark blue), no geometry visible.
**Strategy A — Inspect the DAE XML:** Open the model.dae in a text editor. Check if it has `<geometry>` elements with `<triangles>` or `<polylist>`. Some DAEs may only have `<lines>` or empty geometry.
**Strategy B — Test with a known-good file:** Create a minimal test DAE (a single textured cube) and load it. If it renders, the issue is model-specific. If not, the rendering pipeline has a bug.
**Strategy C — Add a fallback cube:** If `StaticModel.IsLoaded` is true but nothing renders, draw a CubeRenderer at the model's center as a debug indicator. This confirms the camera is pointed at the right location.
**Strategy D — Dump vertex data:** Log the first 10 vertices (position values) to verify they're not all zeros or NaN. If positions are valid but nothing shows, the issue is projection/culling.

### Issue 2: Map Assets Are Mostly Props, Not Maps
**Symptom:** Browsing the map select shows items like `pi_cucumber01`, `objects_un_barrel02`, `plants_un_shrub02_pink`.
**Strategy A — Prefix-based filtering:** True environment models in scarlet tend to use prefixes like `a_` (areas), `sub_area_`, `ground_`, `clod_terrain_`, `clod_town_`. Props use `objects_un_`, `pi_`, `plants_`. Filter or tag by prefix.
**Strategy B — File size heuristic:** Full map/terrain models are typically 1-10MB+ while small props are <500KB. Sort by `model.dae` file size to surface real maps first.
**Strategy C — Subcategory reorganization:** Move manifest entries into subcategories (terrain, buildings, battle-arenas, props, plants, items) either by renaming folders or adding a `category` field to the manifest.
**Strategy D — Size-based auto-categorization:** In `MapManifestScanner`, read the DAE file size or vertex count and assign a category tag (small/medium/large). Show large models first in the overlay.

### Issue 3: Inverted Camera/Controls
**Symptom:** W moves camera backward, or pitch is reversed.
**Root cause:** `MoveZ` is +1 for W in `InputManager`, but the orbit camera's world-space Z axis points away from the camera at yaw=0. Fixed by negating `input.MoveZ` in the pan calculation. If other axes feel wrong, same approach — negate the relevant input axis.

### Issue 4: No Textures on Models
**Symptom:** Models render as flat gray shapes (untextured fallback).
**Root cause:** The Collada `<bind_material>` → `<instance_material>` chain may use different symbol names than expected. `TextureResolver.ParseBindMaterialMap` searches `<instance_material>` globally — but map DAEs may nest these under `<instance_geometry>` with different attribute patterns.
**Strategy:** Log `symbolToMaterial` and `materialToImage` dictionaries in `StaticModel.Load()`, compare against the actual `mesh.MaterialSymbol` values to find the disconnect.

---

## 7. New Architecture & Features

### Architecture: Dual-Mode Screen System
The 3DModelLoader now has a mode toggle pattern:
```
ModelLoaderGame
├── Character Mode (Escape toggles)
│   ├── FreeRoamScreen (3rd-person free camera)
│   ├── CharacterSelectOverlay (Tab)
│   └── SkinnedModel + AnimationSetLoader
└── Map Mode (Escape toggles)
    ├── MapViewerScreen (orbit camera)
    ├── MapSelectOverlay (Tab)
    └── StaticModel (no skeleton)
```
This pattern is extensible — adding a third mode (e.g., Battle Arena Viewer) would follow the same structure: new screen, new overlay, same toggle mechanism.

### New Components
| Component | File | Lines | Purpose |
|-----------|------|-------|---------|
| MapManifestScanner | Animations/ | 71 | Scans Maps/ for manifest.json |
| StaticModel | Rendering/ | 259 | Unskinned DAE loader + renderer |
| MapSelectOverlay | Screens/ | 287 | Hierarchical map browser UI |
| MapViewerScreen | Screens/ | 179 | Orbit camera + model display |
| MapRecord | Screens/ | 1 | Data record for map entries |
| create-missing-map-manifests.py | scripts/ | 140 | Manifest generator for gaps |

### Quick Wins
1. **Wireframe debug toggle** — Add `if (input.IsKeyJustPressed(Keys.G))` to toggle between `CullNone` and a wireframe `RasterizerState`. ~5 lines of code, huge debugging value.
2. **Bounds box visualization** — Draw the model's AABB as wireframe lines using `CubeRenderer` at bounds center/extents. Confirms geometry is loaded even when invisible. ~10 lines.
3. **File size display in overlay** — Show the `model.dae` file size next to each entry in `MapSelectOverlay`. Helps identify real maps vs tiny props without loading them. ~15 lines.
4. **Auto-load last map** — Mirror the character system's `last_character_id` pattern. Save `last_map_id` on selection, restore on startup. Already half-wired (SetSetting call exists). ~10 lines.
5. **Category prefix filter** — Add a toggle key (e.g., `1/2/3`) in `MapSelectOverlay` to filter by prefix group (terrain/buildings/props). Instant improvement for 285-entry list.

---

## 8. File Inventory

### New Files Created
```
src/Starfield2026.3DModelLoader/
├── Animations/MapManifestScanner.cs          (71 lines)
├── Rendering/StaticModel.cs                  (259 lines)
├── Screens/MapSelectOverlay.cs               (287 lines)
└── Screens/MapViewerScreen.cs                (179 lines)

scripts/
└── create-missing-map-manifests.py           (140 lines)
```

### Modified Files
```
src/Starfield2026.3DModelLoader/
└── ModelLoaderGame.cs                        (+76 lines, dual-mode wiring)
```

### Generated Manifests
```
src/Starfield2026.Assets/Models/Maps/scarlet/maps/
├── a_t06_g10_battle/manifest.json            (NEW)
├── objects_un_a_w23cliff01_01_l2/manifest.json (NEW)
├── objects_un_a_w23observatory01_gate01/manifest.json (NEW)
├── objects_un_base_flag01_fighting/manifest.json (NEW)
└── objects_un_base_house03_fighting/manifest.json (NEW)
```
