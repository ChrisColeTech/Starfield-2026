# 20 - PICA Texture Baker & SpicaCli Pipeline: Lessons Learned Handoff

**Date:** 2026-02-22
**Scope:** SpicaCli GARC-to-DAE export pipeline, PICA200 texture combiner baker, COLLADA DAE texture reference fixes, bulk export of all Sun/Moon 3D assets
**Status:** Bulk export complete (1,848 model groups across 8 categories, 19GB output); texture baking and DAE references verified in Blender; not yet committed

---

## 1. What We Accomplished

### SpicaCli — Full 3DS Model Export Pipeline (Complete)
- Built a CLI tool (`SpicaCli/Program.cs`) that reads GARC containers from Pokemon Sun/Moon ROM dumps, identifies 3DS model formats (BCH, CGFX, GF packages), and exports to COLLADA DAE + PNG textures + animation clips
- Two export modes: flat (one group per entry) and `--split-model-anims` (groups consecutive GARC entries by Pokemon ID — model + textures + animations merged into one group)
- Each export group produces: `model.dae`, `model_lowpoly.dae` (if present), `textures/*.png`, `clips/clip_NNN.dae`, `manifest.json`
- Semantic animation name resolution via slot maps: overworld slots (Idle/Walk/Run/Jump/etc.) and Pokemon slots, resolved from source clip names or index positions

### PICA200 Texture Combiner Baker (Complete, Verified)
- Built `SpicaCli/Formats/PicaTextureBaker.cs` — a software evaluator of the 3DS PICA200 GPU's 6-stage texture combiner pipeline
- Solves the core problem: Pokemon Sun/Moon materials blend up to 3 textures at runtime using GPU combiners (e.g., `Body1 * Body2 = final color`). Standard exporters only emit a single diffuse texture reference, so multi-texture materials look wrong in Blender.
- **Full combiner stage evaluation**: Resolves all 3 source inputs per stage (Texture0/1/2, Previous, PreviousBuffer, Constant, PrimaryColor), applies operands (Color, OneMinusColor, Alpha, Red, etc.), applies combiner modes (Replace, Modulate, Add, Interpolate, Subtract, DotProduct3, MultAdd, AddMult), applies scale (1x/2x/4x), updates buffer registers
- **Vertex color rasterization**: Materials referencing `PrimaryColor` (fire effects, glow effects) get vertex colors rasterized into UV space via barycentric interpolation of mesh triangles. This produces correct per-pixel vertex color gradients (e.g., Charizard's fire goes from pink → yellow → orange)
- **Constant color support**: Each stage can reference one of 6 constant colors via `GetConstantIndex()`. Used for tinting effects (e.g., fire core uses constant color 129,129,0 → olive/yellow)
- Overwrites original Texture0 file on disk (not separate `_baked.png` files) so DAE references stay consistent
- `NeedsBaking()` detection: only bakes materials where a non-pass-through stage references Texture1 or Texture2

### COLLADA DAE Texture Reference Chain Fix (Complete)
- **Root cause**: SPICA's DAE exporter writes two kinds of `<init_from>` elements: (1) in `<library_images>` — actual file paths (`./name.tga.png`), and (2) in `<effect>/<surface>` — image ID references (`name.tga`). Our `PatchDaeTexturePaths` regex was converting BOTH to file paths, breaking the COLLADA reference chain.
- **Fix**: Only replace `<init_from>./` with `<init_from>./textures/` — this targets library_images (which start with `./`) and leaves surface image ID references untouched.
- **COLLADA spec**: `<library_images>` → defines images with `id` + file path. `<surface><init_from>` → references image **ID**, NOT a file path. `<sampler2D><source>` → references surface SID. `<texture>` → references sampler SID.

### Bulk Export Results (Complete)
All Sun/Moon 3D model GARCs exported with texture baking:

| Category | GARC | Output Dir | Groups | Notes |
|----------|------|-----------|--------|-------|
| Pokemon battle models | `a/0/9/4` | `pokemon/` | 957 | pm0001-pm0888, split by Pokemon ID |
| Battle characters | `a/1/7/4` | `characters/battle/` | 201 | Trainer models with Chara_Rim baking |
| Field characters | `a/2/0/0` | `characters/field/` | 358 | Field NPCs with _fi rim texture baking |
| World terrain | `a/0/8/2` | `maps/world/` | 165 | Alola overworld tiles, ground/sea/road blending |
| Battle terrains | `a/0/8/2`* | `maps/battle-terrains/` | 166 | Battle background environments |
| Skybox | `a/0/7/2` | `maps/skybox/` | 1 | 6 baked textures |
| Map models | `a/0/8/5` | `maps/map-models/` | 0 | No recognized model formats |
| Overworld models | `a/0/8/6` | `maps/overworld-models/` | 0 | No recognized model formats |

**Total: 1,848 model groups, 19GB on disk**

### Supporting Infrastructure
- `GARC.cs` — GARC container reader with `FileShare.Read` for parallel access
- `LZSS.cs` — LZ11 decompression for compressed GARC entries
- `FormatIdentifier.cs` — Magic-number-based format detection (BCH, TEX, CGFX, GFLXPAK, GFPackage variants)
- Skeleton tracking across GARC entries (model entry sets `lastSkeleton`, subsequent animation entries inherit it for proper bone-name resolution)

---

## 2. What Work Remains

### a/0/8/5 and a/0/8/6 — 0 Exported Models
Both GARCs contain entries but FormatIdentifier doesn't recognize the format. These may use a different container format (not GFPackage, not BCH/CGFX), or the entries may not be 3D models despite being listed as "Map Models" and "Overworld Models" in the ROM structure. Investigation needed — hex dump the first few entries to check magic numbers.

### Material Animations Not Baked
Material animations (UV scrolling, color cycling) are exported as clip DAEs but the baker doesn't account for animated material parameters. If a material's combiner constants change per-frame, the baked texture only captures frame 0 state.

### Normal Map / Specular Map Handling
The baker currently only composites the diffuse channel. If materials use multi-texture blending for normal maps or specular maps, those aren't handled. Most Pokemon materials use Texture0 for diffuse and Texture1/2 for secondary diffuse layers, but terrain materials may blend normal maps.

### DAE Exporter Limitations (Upstream SPICA)
- SPICA's COLLADA exporter doesn't emit `<bind_material>` UV set mappings correctly for multi-UV-set meshes
- Skeletal animation clip DAEs include the full mesh geometry (bloated file sizes) — a future optimization would be skeleton-only clip DAEs

### Integration with Game Engine
The exported DAE/PNG/manifest structure needs a loader in `Starfield2026.3DModelLoader` that reads `manifest.json`, loads `model.dae` via the existing COLLADA importer, and wires up animation clips from `clips/`. The semantic names (Idle, Walk, Run, etc.) should map to the engine's animation state machine.

---

## 3. Optimizations — Prime Suspects

### 3.1 Texture Baker Memory Allocation (Highest Impact)
**Problem**: `BakeMaterial()` allocates `new float[]` arrays on every pixel, for every combiner stage source. For a 512x512 texture with 6 stages and 3 sources each, that's ~512*512*6*3 = 4.7M small array allocations per material.
**Fix**: Pre-allocate reusable source/result arrays outside the pixel loop. Use a `Span<float>` or stack-allocated buffers. This alone could cut bake time by 50%+ due to reduced GC pressure.

### 3.2 Parallel Material Baking
**Problem**: Materials are baked sequentially within each scene. For models with 10+ materials (common in terrain tiles), this is a bottleneck.
**Fix**: Use `Parallel.For` over materials with thread-local texture caches. The only shared state is the texture file cache (already using `Dictionary` — switch to `ConcurrentDictionary`). Each material writes to a separate output file, so there are no write conflicts.

### 3.3 GARC Entry Parallelism
**Problem**: GARC entries are processed sequentially because `lastSkeleton` state is passed between entries. In `--split-model-anims` mode, entries within a group are sequential, but independent groups could be parallelized.
**Fix**: Two-pass approach: (1) Sequential scan to identify group boundaries and assign skeletons, (2) Parallel export of independent groups. The main bottleneck is DAE serialization (XML writing) and PNG encoding, both of which are CPU-bound and parallelizable.

### 3.4 PNG Save Performance
**Problem**: `System.Drawing.Bitmap.Save(ImageFormat.Png)` uses GDI+ which is single-threaded and relatively slow for PNG compression.
**Fix**: Switch to `SkiaSharp` or `ImageSharp` for PNG encoding, which support parallel encoding. Alternatively, save as uncompressed TGA (faster I/O, larger files) and add a post-processing step to batch-convert to PNG.

---

## 4. Step-by-Step: Getting the App Fully Working with No Errors

### Step 1: Build SpicaCli
```bash
cd D:/Projects/Starfield-2026/src/Starfield2026.Spica/SpicaCli
dotnet build -c Release
```

### Step 2: Verify Single Model Export
```bash
dotnet run -c Release -- convert D:/Projects/Starfield-2026/src/Starfield2026.Tests/sun-moon-dump/romfs/a/0/9/4 \
  -o /tmp/test-export -n 20 --split-model-anims
```
This exports the first 20 GARC entries (~first 2 Pokemon). Check:
- Output directories created under `/tmp/test-export/pm0001_00/`
- `model.dae` opens in Blender with correct textures
- Baked textures (e.g., Body textures) show correct composited colors
- `manifest.json` lists all models, textures, and clips

### Step 3: Verify Texture Baking
Open Blender, import `model.dae` from a multi-texture Pokemon (e.g., pm0006_00 Charizard). The body should be orange (Body1 * Body2 blended), not pink (just Body1 alone). Fire effects should show correct color gradients.

### Step 4: Verify Animation Clips
Import `clips/clip_000.dae` in Blender alongside the model. The skeleton should animate. Check that bone names match between model and clip DAEs.

### Step 5: Full Export (All GARCs)
```bash
# Pokemon (largest — 10549 entries)
dotnet run -c Release -- convert romfs/a/0/9/4 -o spica-exported/pokemon --split-model-anims

# Maps
dotnet run -c Release -- convert romfs/a/0/8/2 -o spica-exported/maps/world
dotnet run -c Release -- convert romfs/a/0/7/2 -o spica-exported/maps/skybox

# Characters
dotnet run -c Release -- convert romfs/a/1/7/4 -o spica-exported/characters/battle --split-model-anims
dotnet run -c Release -- convert romfs/a/2/0/0 -o spica-exported/characters/field --split-model-anims
```

### Step 6: Spot-Check Results
- Pick 5-10 random Pokemon from different ID ranges (pm0025, pm0150, pm0380, pm0650, pm0800)
- Open each in Blender, verify textures, skeleton, animations
- Check terrain tiles in `maps/world/` — ground textures should show blended layers, not single-texture

---

## 5. How to Start / Test the App

### Running SpicaCli
```bash
# From the SpicaCli project directory:
cd D:/Projects/Starfield-2026/src/Starfield2026.Spica/SpicaCli

# Inspect a file
dotnet run -c Release -- info <path-to-garc-or-bch>

# Convert a single file
dotnet run -c Release -- convert <file> -o <output-dir>

# Convert GARC with split export (groups by Pokemon ID)
dotnet run -c Release -- convert <garc-path> -o <output-dir> --split-model-anims

# Limit to first N entries (for testing)
dotnet run -c Release -- convert <garc-path> -o <output-dir> -n 50 --split-model-anims
```

### GARC File Locations (Sun/Moon ROM Dump)
The ROM dump root is at: `D:\Projects\Starfield-2026\src\Starfield2026.Tests\sun-moon-dump\romfs\`

| GARC | Contents | Entries |
|------|----------|---------|
| `a/0/9/4` | Pokemon battle models + animations | 10,549 |
| `a/0/8/2` | Alola terrain tiles | 3,696 |
| `a/0/7/2` | Skybox models | Small |
| `a/1/7/4` | Battle character models | 316 |
| `a/2/0/0` | Field character models | 604 |
| `a/0/8/5` | Map models (unrecognized format) | - |
| `a/0/8/6` | Overworld models (unrecognized format) | 1,500 |

### Export Output Location
All exports currently go to: `D:\Projects\Starfield-2026\src\Starfield2026.Tests\sun-moon-dump\spica-exported\`

### Verifying in Blender
1. File > Import > COLLADA (.dae)
2. Navigate to any exported `model.dae`
3. Switch to Material Preview or Rendered mode (Z key)
4. Textures should appear automatically (referenced via relative paths in `./textures/`)

---

## 6. Known Issues & Strategies for Resolution

### Issue 1: a/0/8/5 and a/0/8/6 Export 0 Models
**Problem**: FormatIdentifier doesn't recognize entries from these GARCs. They may use a different format or may not contain 3D models.
**Strategy A**: Hex-dump the first 10 entries, check magic numbers against known 3DS formats (BCH=0x484342, CGFX=0x58464743, etc.). If they match a known format, the issue is in entry reading. If unknown, these may be collision meshes or other non-visual data.
**Strategy B**: Try opening these GARCs with other 3DS tools (pk3DS, Ohana3DS) to see if they recognize the format.
**Strategy C**: Check if entries need double-decompression (GARC → LZ11 → inner container → LZ11 → BCH). Some Gen 7 GARCs use nested compression.

### Issue 2: Vertex Color Rasterization Artifacts
**Problem**: The triangle rasterizer uses simple barycentric interpolation with no anti-aliasing. At UV seams, there may be 1-pixel gaps or bleeding between adjacent triangles.
**Strategy A**: Add a 1-pixel dilation pass after rasterization — for each uncovered pixel, sample the nearest covered neighbor. This fills seam gaps without affecting interior pixels.
**Strategy B**: Rasterize at 2x resolution and downsample (supersampling AA). More memory but cleaner edges.
**Strategy C**: Use conservative rasterization — extend triangle edges by 0.5 pixels to ensure full coverage at boundaries.

### Issue 3: Performance — Large Pokemon Export Takes Too Long
**Problem**: Exporting all 10,549 GARC entries from a/0/9/4 is slow (primarily due to per-pixel combiner evaluation and PNG encoding).
**Strategy A**: Implement the memory allocation optimizations from Section 3.1 (pre-allocate buffers). This is the single highest-impact change.
**Strategy B**: Skip baking for materials where all stages just do `Replace(Texture0)` — the `NeedsBaking()` check already handles this, but verify it's not triggering false positives.
**Strategy C**: Add a `--no-bake` flag for quick exports when you just need geometry/skeletons, skipping the expensive combiner evaluation entirely.
**Strategy D**: Cache baked textures across runs — if `{texName}_baked.hash` matches current input textures, skip re-baking. Useful for iterative development.

### Issue 4: COLLADA Compatibility Edge Cases
**Problem**: Some DAE files may not import correctly in all tools (Blender, Unity, Godot) due to SPICA's COLLADA output quirks.
**Strategy A**: Validate a sample of exported DAEs against the COLLADA schema using `xmllint --schema collada_schema_1_4.xsd model.dae`.
**Strategy B**: Test imports in multiple tools (Blender 4.x, Godot 4.x, Unity 2023+) to identify tool-specific issues.
**Strategy C**: Consider adding a `--format glTF` option using a DAE-to-glTF post-processor. glTF is better supported in modern engines and avoids COLLADA's XML verbosity.

---

## 7. Architecture & New Features

### Architecture Overview

```
SpicaCli/
  Program.cs                    — CLI entry point, GARC iteration, DAE export orchestration
  Formats/
    FormatIdentifier.cs         — Magic-number format detection (BCH, CGFX, GFPackage, etc.)
    PicaTextureBaker.cs         — PICA200 6-stage combiner evaluator + vertex color rasterizer
    GARC.cs                     — GARC container reader
    LZSS.cs                     — LZ11 decompression
    GFPkmnModel.cs              — GF Pokemon model package reader
    GFCharaModel.cs             — GF character model reader
    GFOWMapModel.cs             — GF overworld map model reader
    GFL2OverWorld.cs            — GFL2 overworld model reader
    GFPackage.cs                — GF package container (AD/BG/CM/GR/MM/PC/PT/PK/PB types)
    GFPackedTexture.cs          — GF packed texture reader

Spica.Core/                     — Upstream SPICA library (3DS format parsers)
  Formats/CtrH3D/               — H3D scene graph (models, materials, textures, animations)
  Formats/Generic/COLLADA/      — DAE exporter
  PICA/                         — PICA200 GPU definitions (combiners, attributes, commands)
```

### Key Data Flow
```
GARC file → GARC.ReadEntry() → LZSS.Decompress() → FormatIdentifier.IdentifyAndOpen()
  → H3D scene → Export textures as PNG → PicaTextureBaker.BakeScene() (mutates H3D in-memory)
  → DAE(scene, modelIdx, animIdx) → PatchDaeTexturePaths() → manifest.json
```

### New Features & Quick Wins

1. **`--dry-run` flag** — Run the full pipeline but skip file I/O. Print what would be exported (group names, material bake decisions, animation counts). Useful for validating GARC contents without waiting for full export.

2. **`--filter <pattern>` flag** — Only export groups matching a glob pattern (e.g., `--filter "pm006*"` for Charizard variants only). Quick win for targeted re-exports during development.

3. **Bake report/log** — Write a `bake_report.json` per group listing which materials were baked, which combiner stages were active, source textures used, and output dimensions. Invaluable for debugging visual artifacts ("why does this material look wrong?").

4. **Texture atlas generation** — Many Pokemon have 5-10 small textures (body, eyes, mouth, accessories). A post-processing step could atlas them into a single texture + UV remap, reducing draw calls when loaded in the game engine.

5. **Progressive export with resume** — Write a `_progress.json` tracking which GARC entries have been processed. On restart, skip already-completed entries. Essential for the 10,549-entry Pokemon GARC where crashes or interruptions lose hours of work.

6. **Material preview HTML** — Generate a simple HTML page per group showing each material's combiner pipeline as a visual graph (source → operand → combiner → result), with thumbnails of input and output textures. Low effort, high debugging value.

---

## 8. Key Files Reference

| File | Path | Purpose |
|------|------|---------|
| Program.cs | `src/Starfield2026.Spica/SpicaCli/Program.cs` | CLI entry, GARC processing, DAE export |
| PicaTextureBaker.cs | `src/Starfield2026.Spica/SpicaCli/Formats/PicaTextureBaker.cs` | Texture combiner evaluation |
| FormatIdentifier.cs | `src/Starfield2026.Spica/SpicaCli/Formats/FormatIdentifier.cs` | Format detection |
| GARC.cs | `src/Starfield2026.Spica/SpicaCli/Formats/GARC.cs` | GARC container reader |
| Export output | `src/Starfield2026.Tests/sun-moon-dump/spica-exported/` | All exported assets |
| ROM dump | `src/Starfield2026.Tests/sun-moon-dump/romfs/` | Source GARC files |
| ROM docs | `src/Starfield2026.Tests/README.md` | GARC location reference |
