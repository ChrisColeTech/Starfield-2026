# 17 - 3DModelLoader & MiniToolbox: Lessons Learned Handoff

**Date:** 2026-02-22
**Scope:** Standalone 3DModelLoader POC (rendering quality, DPI, resize), MiniToolbox GARC extraction pipeline, trainer model asset generation
**Status:** 3DModelLoader rendering quality fixes applied (untested at runtime); MiniToolbox GARC extractor complete with 309 trainer models exported

---

## 1. What We Accomplished

### MiniToolbox GARC Command (Complete)
- Wired `MiniToolbox.Garc` library into the CLI as the `garc` command with `--info`, `--list`, `--extract` modes
- Changed `GARC` class from `internal` to `public` so the App project can reference it
- Changed App target framework from `net8.0` to `net8.0-windows` to match GARC's dependency on `System.Drawing.Common`
- Built `GarcCommand.cs` with lazy-loading materialization (`loadFromDisk` + LZSS decompression), name-based filtering (`--filter tr`), and automatic CM/PC format detection via `FileIO` magic bytes
- Fixed CM.cs animation loading: `GfMotion.load()` was commented out, causing all clip folders to be empty. Uncommented with bounds checking and error handling
- Added `manifest.json` generation matching TRPAK format: textures as plain strings, clips with index/name/file/frameCount/fps/boneCount

### Trainer Model Extraction (Complete)
- **Battle trainers** (GARC `a/1/7/4`): 126 extracted, 190 skipped (non-trainer), 0 failed
- **Field trainers** (GARC `a/2/0/0`): 183 extracted, 3 failed (file lock from duplicate IDs: tr0077_00, tr0092_00, tr0000_00)
- Each export folder contains: `model.dae`, `clips/*.dae`, `textures/*.png`, `manifest.json`
- Output at `Starfield2026.Tests/trainer-exports/battle/` and `trainer-exports/field/`

### MiniToolbox README (Complete)
- Full documentation covering all three commands (garc, trpak, gdb1) with usage examples, options tables, output structures

### 3DModelLoader Rendering Quality Fixes (Applied, Untested)
- **Back buffer resize handler**: `Window.ClientSizeChanged` now calls `ApplyChanges()` so resizing/maximizing renders at correct resolution instead of stretching the initial buffer size
- **DPI-aware app manifest**: Per-monitor DPI awareness so Windows renders at native pixel density
- **Texture filtering**: Changed `SamplerState.PointClamp` to `SamplerState.AnisotropicClamp` for 3D scene rendering

### 3D Build Output Cleanup
- Excluded `Models/` from content copy in both `Starfield2026.3D.csproj` and `Starfield2026.3DModelLoader.csproj` — models are loaded from the Assets root at runtime, not from bin/

---

## 2. What Work Remains

### 3DModelLoader — Rendering
- **Runtime verification** of all three rendering fixes (resize, DPI, texture filtering) — changes compile but haven't been tested visually
- **Grid line rendering quality** may still be thin/aliased at certain zoom levels — grid uses `PrimitiveType.LineList` which doesn't benefit from MSAA on all drivers
- **MSAA sample count** is never explicitly set (`PreferMultiSampling = true` but no sample count) — MonoGame DesktopGL may default to 0 or 4 depending on GPU

### 3DModelLoader — Skeletal Rendering (Carried from Doc 16)
- Model geometry rendering still has issues from the merged-buffer approach (see Doc 16 Suspects 1-4)
- Per-mesh texturing via COLLADA material chain is implemented in `SkinnedDaeModel` but depends on correct geometry/skin pairing
- Diagnostic logging still present in multiple files

### MiniToolbox — GARC
- Duplicate trainer IDs across GARC entries cause file lock collisions (3 failures in field extraction)
- No unified single-command extraction for both battle + field GARCs — currently requires two separate invocations
- `--skip` and `--limit` are useful but `--filter` is the primary workflow; could add `--filter-regex` for complex patterns

### Asset Pipeline
- Extracted DAE models need validation against the 3DModelLoader — do they load, animate, and texture correctly?
- No automated pipeline from GARC extraction to Assets/Models/ folder structure

---

## 3. Optimizations — Prime Suspects

### Suspect 1: CPU Skinning Every Frame
`SkinnedDaeModel.RebuildBuffers()` is called every frame from `OverworldCharacter.Draw()` via `UpdatePose()`. This rebuilds the entire VertexBuffer and IndexBuffer from scratch — allocating new buffers, iterating every vertex, computing skin transforms, and uploading to GPU. For a 2000-vertex model with 8 clips this dominates frame time.

**Fix:** Use `SetData()` on existing buffers instead of disposing/recreating. Better: move skinning to a vertex shader (GPU skinning) by passing bone matrices as shader constants. MonoGame's `BasicEffect` doesn't support this, so a custom `Effect` with a bone palette would be needed. As an intermediate step, only rebuild when the pose actually changes (dirty flag on `SkeletalAnimator`).

### Suspect 2: VertexBuffer/IndexBuffer Allocation Per Frame
`RebuildBuffers()` calls `VertexBuffer?.Dispose()` then `new VertexBuffer(...)` every frame. GPU buffer allocation is expensive. The vertex count doesn't change between frames — only positions and normals change.

**Fix:** Allocate `VertexBuffer` once with `BufferUsage.None` (not `WriteOnly`) and use `SetData()` to update in place. Pre-allocate the `VertexPositionNormalTexture[]` array as a class field instead of `new List<>()` each frame.

### Suspect 3: Minimap Per-Pixel Rendering
`MinimapHUD.DrawMinimap()` draws each pixel of the minimap as an individual `SpriteBatch.Draw()` call. For a 120px diameter circle, that's ~11,000+ draw calls per frame for the minimap alone. The span-batching optimization helps but still produces hundreds of draw calls.

**Fix:** Render the minimap to a `RenderTarget2D` and only update it when the player moves more than a threshold distance or rotates more than a threshold angle. Draw the cached texture as a single sprite each frame.

### Suspect 4: ColladaSkeletalLoader XML Parsing
`XDocument.Load()` and LINQ-to-XML are used for all COLLADA parsing. This is fine for load-time but the DOM stays in memory. For large models with many animations, the parsed `XDocument` objects and intermediate string arrays (`ParseFloats`, `ParseInts`) generate significant GC pressure.

**Fix:** For hot-path loading (character switching), consider caching the parsed skeleton and clip data in a binary format. The `manifest.json` already has frame counts and durations — extend it with pre-baked bone indices and keyframe arrays to skip XML parsing entirely after first load.

---

## 4. Step-by-Step Approach to Get App Fully Working

### Phase 1: Verify Rendering Fixes
1. Build and run the 3DModelLoader: `dotnet run --project Starfield2026.3DModelLoader`
2. Resize the window — grid lines, cube, and UI should stay crisp (not blurry)
3. Maximize the window — same check, back buffer should match new size
4. Check `modelloader.log` for any errors during startup

### Phase 2: Verify Model Loading Pipeline
1. Ensure `Starfield2026.Assets/Models/` contains at least one character folder with `manifest.json`, `model.dae`, `textures/`, and `clips/`
2. App auto-loads first character on startup — check if it renders (even incorrectly) or falls back to blue cube
3. Press Tab to open character select overlay, verify categories and character list populate
4. Select a different character, verify it loads without crash

### Phase 3: Fix Skeletal Geometry (See Doc 16, Strategy 1)
1. The current `SkinnedDaeModel` already has the PokemonGreen-style per-mesh architecture (explicit geometry/skin pairing, per-mesh textures, face mesh depth handling)
2. Verify that `ParseControllers()` matches each skin to its geometry via `<skin source="#geometry_id">`
3. Test with a simple single-mesh model first, then multi-mesh
4. If geometry looks correct in bind pose but wrong when animated, the issue is in `SkeletalAnimator.SkinPose` computation

### Phase 4: Validate Extracted Trainer Models
1. Copy one trainer folder (e.g., `trainer-exports/battle/tr0001_00/`) into `Starfield2026.Assets/Models/Characters/`
2. Rebuild and launch — the trainer should appear in character select
3. Check: does the model render? Are textures applied? Do animations play?
4. If the DAE from MiniToolbox doesn't load, compare its structure against a known-working DAE

### Phase 5: Polish
1. Remove diagnostic `ModelLoaderLog.Info()` calls from `SkinnedDaeModel.Load()` (the ~30 lines of per-vertex/per-joint logging)
2. Add error handling for missing/corrupt manifest.json during ManifestScanner.Scan
3. Clean up `window.json` and `modelloader.db` lifecycle (currently persist in bin/)

---

## 5. How to Start/Test the App

### Prerequisites
- .NET 9.0 SDK
- MonoGame 3.8.x DesktopGL (restored via NuGet automatically)
- `Starfield2026.Assets/Models/` must contain at least one character folder with `manifest.json`

### Build & Run (3DModelLoader)
```bash
cd D:/Projects/Starfield-2026/src/Starfield2026.3DModelLoader
dotnet build
dotnet run
```

### Build & Run (MiniToolbox)
```bash
cd D:/Projects/Starfield-2026/src/Starfield2026.MiniToolbox
dotnet build src/MiniToolbox.App

# Extract battle trainers
dotnet run --project src/MiniToolbox.App -- garc --extract \
  --input "D:/Projects/Starfield-2026/src/Starfield2026.Tests/sun-moon-dump/RomFS/a/1/7/4" \
  --output "D:/Projects/Starfield-2026/src/Starfield2026.Tests/trainer-exports/battle" \
  --format dae --filter tr

# Extract field trainers
dotnet run --project src/MiniToolbox.App -- garc --extract \
  --input "D:/Projects/Starfield-2026/src/Starfield2026.Tests/sun-moon-dump/RomFS/a/2/0/0" \
  --output "D:/Projects/Starfield-2026/src/Starfield2026.Tests/trainer-exports/field" \
  --format dae --filter tr
```

### Controls (3DModelLoader)
| Key | Action |
|-----|--------|
| W/S | Move forward/backward |
| A/D | Turn left/right |
| Shift | Toggle run |
| Space | Jump |
| Q/E | Rotate camera yaw |
| R/F | Tilt camera pitch |
| Z/X or scroll | Zoom in/out |
| Tab | Character select overlay |
| Esc | Quit |

### Debug Output
- `bin/Debug/net9.0/modelloader.log` — full startup and character loading trace
- `bin/Debug/net9.0/modelloader.db` — SQLite character registry (auto-rebuilt on scan mismatch)
- `bin/Debug/net9.0/window.json` — persisted window position, size, maximized state

---

## 6. Issues & Strategies

### Issue 1: Back Buffer Stretch on Resize
**Symptom:** All graphics (models, grid lines, UI text) become blurry when window is resized or maximized.
**Root cause:** `AllowUserResizing = true` but no `ClientSizeChanged` handler — back buffer stays at initial size and gets stretched.
**Fix applied:** Added `OnClientSizeChanged` handler that updates `PreferredBackBufferWidth/Height` and calls `ApplyChanges()`.

### Issue 2: DPI Scaling Blur
**Symptom:** Everything looks blurry on displays with >100% scaling, even at initial window size.
**Root cause:** No app manifest declaring DPI awareness — Windows treats the app as DPI-unaware and upscales it with bilinear filtering.
**Fix applied:** Added `app.manifest` with `permonitorv2` DPI awareness and referenced it in the .csproj.

### Issue 3: Blocky/Pixelated 3D Textures
**Symptom:** Model textures look blocky and grid lines have stair-step aliasing.
**Root cause:** `SamplerState.PointClamp` was being set for the 3D rendering pass — this uses nearest-neighbor filtering which is appropriate for pixel art but wrong for 3D models.
**Fix applied:** Changed to `SamplerState.AnisotropicClamp` for smooth filtering with proper mip-mapping at oblique angles.

### Strategy 1: Render Target for Resolution Independence
If resize handling alone isn't sufficient (e.g., DPI fractional scaling on mixed-DPI setups), render the 3D scene to a `RenderTarget2D` at a fixed internal resolution, then blit to screen. This guarantees pixel-perfect rendering regardless of window size. Set the render target to the native back buffer size and only recreate on resize.

### Strategy 2: Line Rendering with Geometry Instead of LineList
`PrimitiveType.LineList` produces 1px lines that don't scale with resolution and don't benefit from MSAA on some OpenGL drivers. Replace the grid's `LineList` with thin quads (two triangles per line, e.g., 0.02 units wide). This makes lines resolution-independent, properly anti-aliased, and consistently visible at any zoom level.

### Strategy 3: Separate SamplerState per Render Pass
The 3D model pass needs `AnisotropicClamp` for smooth textures, but the 2D UI pass (PixelFont, minimap, character select) needs `PointClamp` for crisp pixel-perfect rendering. Currently the 3D pass sets `SamplerStates[0]` globally. The HUD's `SpriteBatch.Begin()` should explicitly set `SamplerState.PointClamp` to avoid inheriting the 3D pass's state:
```csharp
_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
```

### Strategy 4: Validate MSAA Is Actually Active
MonoGame DesktopGL's `PreferMultiSampling = true` requests MSAA but doesn't guarantee it. After `Initialize()`, check `GraphicsDevice.PresentationParameters.MultiSampleCount` — if it's 0 or 1, MSAA was rejected by the driver. In that case, implement FXAA as a post-process shader, or render to a 2x-resolution `RenderTarget2D` and downsample (supersampling).

---

## 7. Architecture & Quick Wins

### Current Architecture

```
Starfield2026.3DModelLoader (standalone MonoGame app)
├── ModelLoaderGame              Game loop, window, content, HUD wiring
├── Screens/
│   ├── FreeRoamScreen           3D scene: grid, camera, character rendering
│   └── CharacterSelectOverlay   Tab-menu character picker (category → item)
├── Skeletal/
│   ├── ColladaSkeletalLoader    DAE parsing: skeleton, clips, geometry, skin
│   ├── SkinnedDaeModel          Multi-mesh COLLADA model with CPU skinning
│   ├── SkeletonRig              Bone hierarchy, bind/inverse transforms
│   ├── SkeletalAnimator         Playback: local→world→skin pose pipeline
│   ├── AnimationController      Tag-based clip switching (Idle/Walk/Run)
│   ├── SkeletalAnimationClip    Keyframe tracks with decompose/slerp interpolation
│   ├── SplitModelAnimationSet   Manifest-driven model+clips loader
│   ├── ManifestScanner          Scans Models/ tree for manifest.json
│   └── OverworldCharacter       Character lifecycle: load, animate, draw
├── Controllers/
│   └── PlayerController         Tank-style WASD movement, jump, ground detection
├── Rendering/
│   ├── QuadrantGridRenderer     Color-coded infinite grid (NW/NE/SW/SE)
│   ├── CubeRenderer             Placeholder cube + shadow
│   └── PixelFont                5x7 bitmap font from 1px texture
├── Input/
│   ├── InputManager             Keyboard + mouse state polling
│   └── InputSnapshot            Immutable frame input state
├── Save/
│   └── CharacterDatabase        SQLite character registry
└── UI/
    ├── MinimapHUD               Rotating minimap + status bar
    └── WindowStateHelper        Window position/size persistence (Win32 interop)

MiniToolbox (CLI asset extraction tool)
├── MiniToolbox.App              CLI entry: trpak, gdb1, garc commands
│   └── Commands/GarcCommand     GARC extraction with filter, lazy-load, manifest gen
├── MiniToolbox.Garc             3DS GARC/CM/PC/GfModel/GfMotion/DAE pipeline
├── MiniToolbox.Trpak            Switch TRPAK extraction
└── MiniToolbox.Gdb1             GDB1 (Galar Dex) database extraction
```

### New Feature: GARC Command
The `garc` command bridges the gap between raw 3DS ROM dumps and the 3DModelLoader's expected asset structure. It handles GARC container unpacking, LZSS decompression, CM/PC format detection, GfModel→DAE conversion, GfMotion→clip DAE conversion, texture extraction, and manifest generation — all in a single pipeline.

### Quick Win 1: SpriteBatch SamplerState for UI (5 min)
The HUD and character select overlay inherit whatever `SamplerStates[0]` was set by the 3D pass. Add explicit `SamplerState.PointClamp` to every `SpriteBatch.Begin()` call in `MinimapHUD.Draw()` and `ModelLoaderGame.Draw()` for the overlay. This keeps UI text and minimap pixel-crisp while 3D models use anisotropic filtering.

### Quick Win 2: Dirty-Flag Pose Updates (15 min)
`OverworldCharacter.Draw()` calls `_model.UpdatePose()` every frame even when the animation hasn't advanced (e.g., paused, same frame). Add a `PoseVersion` counter to `SkeletalAnimator` that increments on `Update()`, and only call `RebuildBuffers()` when the version changes.

### Quick Win 3: Pre-Allocate Vertex Arrays (10 min)
`RebuildBuffers()` creates `new List<VertexPositionNormalTexture>()` and `new List<int>()` each frame. Pre-allocate these as class-level arrays sized to the mesh vertex/index count (known at load time). Eliminates GC pressure from the hot path.

### Quick Win 4: Unified GARC Extraction (30 min)
Add `--input` support for multiple paths or a config file that specifies both `a/1/7/4:battle` and `a/2/0/0:field` mappings. Single invocation extracts all trainer models with appropriate subfolder routing.

### Quick Win 5: Duplicate ID Conflict Resolution (15 min)
When multiple GARC entries produce the same trainer ID (e.g., tr0077_00 appears in entries 49, 81, 97, 252, 253), append the GARC entry index as a suffix: `tr0077_00_049/`, `tr0077_00_081/`. This eliminates the 3 file-lock failures in field extraction.

---

## 8. Key Files Reference

| File | Purpose | Lines |
|------|---------|-------|
| `3DModelLoader/ModelLoaderGame.cs` | Game loop, window resize handler, DPI manifest | 232 |
| `3DModelLoader/Screens/FreeRoamScreen.cs` | 3D scene, camera, sampler state | 173 |
| `3DModelLoader/Skeletal/SkinnedDaeModel.cs` | Multi-mesh COLLADA + CPU skinning | 808 |
| `3DModelLoader/Skeletal/ColladaSkeletalLoader.cs` | DAE parser (skeleton, clips, geometry, skin) | 494 |
| `3DModelLoader/Skeletal/OverworldCharacter.cs` | Character lifecycle + BasicEffect setup | 111 |
| `3DModelLoader/Skeletal/SkeletalAnimator.cs` | Animation playback + skin pose | 96 |
| `3DModelLoader/Skeletal/SplitModelAnimationSet.cs` | Manifest-driven model+clips loader | 134 |
| `3DModelLoader/UI/MinimapHUD.cs` | Minimap + status bar (per-pixel rendering) | 203 |
| `3DModelLoader/Rendering/QuadrantGridRenderer.cs` | Quadrant-colored grid (LineList) | 121 |
| `3DModelLoader/Rendering/PixelFont.cs` | 5x7 bitmap font renderer | 198 |
| `3DModelLoader/app.manifest` | DPI awareness declaration (NEW) | 9 |
| `MiniToolbox.App/Commands/GarcCommand.cs` | GARC extraction command (NEW) | ~350 |
| `MiniToolbox.Garc/Containers/GARC.cs` | GARC container parser (visibility fixed) | — |
| `MiniToolbox.Garc/Models/PocketMonsters/CM.cs` | CM loader (animation fix) | — |
| `MiniToolbox/README.md` | Full CLI documentation (NEW) | — |

---

## 9. Coordinate System & Rendering Reference

- **World:** Y-up, +X east, +Z south. Player forward at yaw=0 = `(0, 0, 1)` = south
- **COLLADA models:** Y-up, centimeters. Vertex Y range ~0-130 for characters
- **Fit scale:** `TargetHeight / modelHeight` = `2.0 / ~130` = `~0.0154`
- **Camera:** FOV 45deg, near 0.1, far 500, pitch -0.15 rad, distance 12 units, look-at height 1.5
- **Grid:** 2-unit spacing, 250 half-size = 500x500 world units, quadrant colors at world origin
- **Skin pose:** `SkinPose[i] = InverseBindTransforms[i] * WorldPose[i]`
- **COLLADA matrices:** Row-major, column-vector. Transposed to XNA row-vector convention via index swizzling in `ReadMatrixFromFloats()`
- **Keyframe interpolation:** Decompose → Slerp(rotation) + Lerp(translation, scale) → Recompose
