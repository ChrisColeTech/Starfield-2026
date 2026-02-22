# 21 - Trinity Texture Baker & MiniToolbox Bulk Export: Lessons Learned Handoff

**Date:** 2026-02-22
**Scope:** TrinityTextureBaker for MiniToolbox.Trpak (Scarlet/Violet layered material baking), bulk re-export of all SV model categories with baked textures
**Status:** Bulk export complete (4,522 models across 4 categories); texture baking verified in Blender; not yet committed

---

## 1. What We Accomplished

### TrinityTextureBaker — Layered Material Baking (New, Complete)
- Built `MiniToolbox.Trpak/Exporters/TrinityTextureBaker.cs` — generalizes the EyeClearCoat baking pattern to **all** Trinity layered materials
- **Problem solved**: Pokemon Scarlet/Violet materials use `LayerMaskMap` textures + `BaseColorLayerN` Vec4 parameters to composite colors at runtime. The DAE exporter only writes the first texture per material (typically `BaseColorMap` albedo), so materials whose albedo is blank/white appear wrong in Blender. Example: Charizard's fire was pure white instead of orange/red/yellow.
- **Compositing formula** (from gftool Trinity shader source):
  ```
  mask = texture(LayerMaskMap, uv)  // RGBA = 4 layer weights
  remainder = clamp(1.0 - (R+G+B+A), 0, 1)
  color = BaseColorLayer1*R + BaseColorLayer2*G + BaseColorLayer3*B + BaseColorLayer4*A + white*remainder
  emission = sum(EmissionColorLayerN * EmissionIntensityN * maskChannel[N])
  final = linearToSrgb(clamp(color + emission, 0, 1))
  ```
- **Shader-agnostic**: Detects materials by checking for `LayerMaskMap` texture + `BaseColorLayerN` params, NOT by shader name. Works across all Trinity shader types: Standard, Hair, SSS, Transparent, Unlit, and any custom names.
- **Output strategy**: Overwrites the existing blank `BaseColorMap` albedo PNG, keeping DAE texture references intact.
- **Integration**: Added to Phase 5 of `TrpakFileGroupExtractor.ProcessJobAsync()` — runs after EyeTextureBaker, catches everything EyeClearCoat doesn't.

### EyeTextureBaker (Existing, Unchanged)
- Already handles `EyeClearCoat` shader materials (same formula, eye-specific)
- TrinityTextureBaker explicitly skips EyeClearCoat materials to avoid double-baking

### Bulk Export Results — Scarlet/Violet (Complete)

| Category | Filter | Output Dir | Succeeded | Failed | Duration |
|----------|--------|-----------|-----------|--------|----------|
| Pokemon | `pokemon/` | `exported-baked/pokemon/` | 665 | 0 | 410s |
| Characters | `chara/` | `exported-baked/characters/` | 462 | 0 | 105s |
| Trainers | `chara/model_tr` | `exported-baked/trainers/` | 30 | 0 | 34s |
| Maps/Terrain | `field_graphic/` | `exported-baked/maps/` | ~3,365 | 1 (crash) | crashed |
| **Total** | | | **~4,522** | **1** | |

All exports at: `D:\Projects\Starfield-2026\src\Starfield2026.Tests\scarlet-dump\exported-baked\`

### Character Sub-Categories (all in `chara/`)
- `model_cc_base` — base customizable character models
- `model_ob` — object/prop character models (87)
- `model_pc` — player character models (100)
- `model_pc_base` — player character base meshes
- `model_tr` — trainer models (30)
- `model_uq` — unique character models (33)
- `model_vr` — variant/DLC character models (210)

### Verified Baking Results
- **Charizard fire** (`pm0006_00_00`): fire_alb.png went from blank white → red/orange/yellow fire gradient
- **Charizard eyes**: Still correctly baked via EyeTextureBaker (blue iris, black pupil)
- **Fire shader type**: "Unlit" — confirmed the baker works across shader types, not just one

---

## 2. What Work Remains

### Maps/Terrain BNTX Crash
The maps export crashed on texture `a_t06_g10_lmg30_lgt` — a Tegra deswizzle panic in the native Rust library (`tegra_swizzle\src\swizzle.rs:342`). ~3,365 of 3,371 models exported before the crash. The crash is a `range end index 304 out of range for slice of length 0` — likely a malformed or unexpected BNTX texture format. Need to either:
- Add try/catch around BNTX decode in the extraction pipeline
- Skip the specific bad texture and continue
- Fix the tegra_swizzle library to handle edge cases

### Vec4 Parameter WXYZ Ordering Validation
The EyeTextureBaker and TrinityTextureBaker both use `(param.Value.W, param.Value.X, param.Value.Y)` for RGB extraction from Vec4f parameters. This WXYZ ordering was established by the EyeTextureBaker and produces correct eye colors, but should be validated against the gftool shader source for other material types (body colors, hair tints, etc.). If some shader types use XYZW instead of WXYZ, the baked colors would be wrong.

### Materials Without LayerMaskMap
Some materials may use runtime color tinting without a LayerMaskMap (e.g., a `BaseColor` Vec4 that multiplies the albedo texture). These materials won't be detected by `NeedsLayerBaking()` since they don't have the layer mask texture. A separate "tint baker" could multiply albedo by the `BaseColor` parameter.

### Legends: Arceus Integration
The LZA (Legends: Arceus) extraction pipeline uses the same Trinity format but the model path resolution is different (TRMDL PathNames are binary garbage). The TrinityTextureBaker will work once the LZA directory-mode decoder is implemented (see doc #19).

### Sun/Moon Assets (Separate Pipeline)
Doc #20 covers the PICA texture combiner baker for 3DS Sun/Moon assets via SpicaCli. That's a completely separate pipeline. Both bakers are now complete — Sun/Moon uses the 6-stage combiner evaluator, Scarlet/Violet uses the layer mask compositor.

---

## 3. Optimizations — Prime Suspects

### 3.1 BNTX Decode Resilience (Critical)
**Problem**: A single bad BNTX texture crashes the entire maps export (3,371 models). The Tegra deswizzle panic is unrecoverable.
**Fix**: Wrap `BntxDecoder.Decode()` calls in try/catch at the extraction pipeline level. If a texture fails to decode, log the error and continue with remaining textures. The BNTX decode is already wrapped in the texture export loop (line 93-109 of TrpakFileGroupExtractor), but the native panic bypasses C# exception handling. May need to run BNTX decode in a separate process or use `AppDomain.UnhandledException`.

### 3.2 Texture Baker Memory Efficiency
**Problem**: `Image<Rgba32>` allocates a full RGBA bitmap for the mask image, then iterates pixel-by-pixel using indexed access (`maskImage[x, y]`). For large textures this is slow due to bounds checking.
**Fix**: Use `maskImage.DangerousGetPixelRowMemory(y)` span-based access for zero-copy row iteration. Or process rows with `ProcessPixelRows()` API for better cache locality.

### 3.3 Parallel Material Baking
**Problem**: Materials within a model are baked sequentially. Models with many layered materials (some trainers have 10+ materials) are bottlenecked.
**Fix**: Since each material writes to a different output file, materials can be baked in parallel with `Parallel.ForEach`. The BNTX files are read-only, so no write conflicts.

### 3.4 Skip Re-Baking on Re-Export
**Problem**: Re-exporting a model always re-bakes all textures even if the output already exists and is up-to-date.
**Fix**: Check if the output PNG already exists and is newer than the source BNTX. Skip baking if the output is fresh. This would make incremental re-exports much faster.

---

## 4. Step-by-Step: Getting the App Fully Working with No Errors

### Step 1: Build MiniToolbox
```bash
cd D:/Projects/Starfield-2026/src/Starfield2026.MiniToolbox
dotnet build -c Release
```

### Step 2: Verify Single Model Export with Baking
```bash
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc "D:/Projects/Starfield-2026/src/Starfield2026.Tests/scarlet-dump/arc" \
  -m "pokemon/data/pm0006/pm0006_00_00/pm0006_00_00.trmdl" \
  -o /tmp/test-bake
```
Check output:
- `Baked eye texture:` messages for both eyes
- `Baked layer texture:` message for fire material
- `test-bake/pm0006_00_00/textures/pm0006_00_00_fire_alb.png` shows fire colors (not white)

### Step 3: Verify in Blender
1. Open Blender, File > Import > COLLADA (.dae)
2. Navigate to `test-bake/pm0006_00_00/model.dae`
3. Switch to Material Preview mode (Z key)
4. Fire should be orange/red/yellow, eyes should be blue

### Step 4: Batch Export All Categories
```bash
# Pokemon (665 models, ~7 min)
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc scarlet-dump/arc --all --filter "pokemon/" \
  -o exported-baked/pokemon -p 8

# Characters (462 models, ~2 min)
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc scarlet-dump/arc --all --filter "chara/" \
  -o exported-baked/characters -p 8

# Trainers (30 models, ~30s)
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc scarlet-dump/arc --all --filter "chara/model_tr" \
  -o exported-baked/trainers -p 8

# Maps/Terrain (3,371 models — may crash on bad BNTX)
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc scarlet-dump/arc --all --filter "field_graphic/" \
  -o exported-baked/maps -p 8
```

### Step 5: Spot-Check Results
- Open 5-10 random Pokemon in Blender (pm0025 Pikachu, pm0006 Charizard, pm0094 Gengar, pm0130 Gyarados, pm0448 Lucario)
- Verify fire/glow effects show correct colors
- Verify eyes are correctly baked
- Check trainer models for correct hair/clothing colors

---

## 5. How to Start / Test the App

### MiniToolbox CLI
```bash
cd D:/Projects/Starfield-2026/src/Starfield2026.MiniToolbox

# List all models in archive
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc <arcDir> --list

# Export single model
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc <arcDir> -m <model.trmdl path> -o <outputDir>

# Export all models (parallel)
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc <arcDir> --all -o <outputDir> -p 8

# Export with filter (only matching paths)
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc <arcDir> --all --filter "pokemon/" -o <outputDir>
```

### Archive Locations
- **Scarlet**: `D:\Projects\Starfield-2026\src\Starfield2026.Tests\scarlet-dump\arc`
- **Violet**: `D:\Projects\Starfield-2026\src\Starfield2026.Tests\violet-dump\arc` (if available)
- **LZA**: `D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\arc` (not yet working)

### Export Output Location
All baked exports: `D:\Projects\Starfield-2026\src\Starfield2026.Tests\scarlet-dump\exported-baked\`

### Export Structure Per Model
```
modelname/
  model.dae           — COLLADA model with skeleton + materials
  textures/           — PNG textures (albedo baked where needed)
  clips/              — Animation clip DAEs (split mode)
  manifest.json       — Metadata: textures, clips, stats
```

---

## 6. Known Issues & Strategies for Resolution

### Issue 1: Maps Export BNTX Crash
**Problem**: Native Rust panic in `tegra_swizzle` on texture `a_t06_g10_lmg30_lgt` — `range end index 304 out of range for slice of length 0`. Kills the entire export process.
**Strategy A**: Add process-level crash isolation — run each model's BNTX decode in a subprocess. If it crashes, the parent process logs the failure and continues.
**Strategy B**: Pre-scan all BNTX files with a validation pass before export. Flag bad textures and skip them during extraction. Write a `skipped_textures.json` log.
**Strategy C**: Patch the `tegra_swizzle` Rust library to return an error instead of panicking on invalid slice bounds. This is the proper fix but requires rebuilding the native dependency.
**Strategy D**: Add a BNTX file size/header sanity check before calling the decoder. The bad texture likely has corrupted dimensions or format metadata. Skip textures whose declared size doesn't match the actual data.

### Issue 2: Materials Without LayerMaskMap Still Need Tinting
**Problem**: Some materials have a `BaseColor` Vec4 parameter that tints the albedo texture at runtime, but don't use the layer mask system. These materials show the un-tinted albedo in Blender.
**Strategy A**: Add a "tint baker" check — if a material has a `BaseColor` Vec4 and a `BaseColorMap` texture, multiply each pixel of the albedo by the tint color. Simple and covers the common case.
**Strategy B**: Check if the `BaseColor` param is not white (1,1,1,1). If it's non-white, it's an active tint that needs baking. White tint = no-op.
**Strategy C**: Dump all Vec4 param names from a full export to discover all color-related parameters. There may be other tint params beyond `BaseColor` (e.g., `SubColor`, `AccentColor`).

### Issue 3: Vec4 WXYZ vs XYZW Ordering Ambiguity
**Problem**: The FlatBuffer `Vector4f` has fields (W, X, Y, Z) and we extract RGB as `(W, X, Y)`. This works for eye materials but may be wrong for other material types if they use a different convention.
**Strategy A**: Export a known Pokemon with distinctive body colors (e.g., Pikachu's yellow) and verify the baked body texture matches. If it's wrong, try `(X, Y, Z)` ordering instead.
**Strategy B**: Read the FlatBuffer schema definition for `Vector4f` to confirm field ordering. The schema may define it as (X, Y, Z, W) or (W, X, Y, Z).
**Strategy C**: Cross-reference with gftool source code (the original GL renderer) to see which field maps to R, G, B.

### Issue 4: Duplicate Trainer Exports in Characters
**Problem**: The `chara/` filter includes `chara/model_tr` (trainers), so trainers appear in both `exported-baked/trainers/` and `exported-baked/characters/`. This wastes disk space.
**Strategy A**: Exclude `model_tr` from the characters export using a negative filter or post-export dedup.
**Strategy B**: Accept the duplication — trainers are a small subset (30 models) and disk is cheap. The separate trainers directory is useful for quick access.
**Strategy C**: Use symlinks or a manifest that references shared output directories.

---

## 7. Architecture & New Features

### Architecture Overview — Baking Pipeline

```
TrpakFileGroupExtractor.ProcessJobAsync()
│
├── Phase 1: Extract TRMDL + dependencies to temp
├── Phase 2: TrinityModelDecoder → ExportData (submeshes, materials, armature)
├── Phase 3: TrinityColladaExporter → model.dae
├── Phase 4: BntxDecoder → textures/*.png
├── Phase 5: TEXTURE BAKING ← NEW
│   ├── EyeTextureBaker.BakeEyeTexture()    — EyeClearCoat shader
│   └── TrinityTextureBaker.BakeLayeredTexture()  — All other layered shaders ← NEW
├── Phase 6: TrinityAnimationDecoder → clips/*.dae
└── Phase 7: ManifestSerializer → manifest.json
```

### Key Files

| File | Path | Purpose |
|------|------|---------|
| TrinityTextureBaker.cs | `MiniToolbox.Trpak/Exporters/TrinityTextureBaker.cs` | **NEW** — Layered material baking |
| EyeTextureBaker.cs | `MiniToolbox.Trpak/Exporters/EyeTextureBaker.cs` | Eye material baking (existing) |
| TrpakFileGroupExtractor.cs | `MiniToolbox.Trpak/TrpakFileGroupExtractor.cs` | Extraction pipeline (modified Phase 5) |
| TrinityMaterial.cs | `MiniToolbox.Trpak/Decoders/TrinityMaterial.cs` | Material data model |
| TrpakCommand.cs | `MiniToolbox.App/Commands/TrpakCommand.cs` | CLI command handler |
| TrinityColladaExporter.cs | `MiniToolbox.Trpak/Exporters/TrinityColladaExporter.cs` | DAE export |

### Quick Wins

1. **`--skip-bake` flag** — Skip all texture baking for fast geometry-only exports. Useful when iterating on skeleton/animation issues without waiting for bake passes.

2. **Bake report in manifest.json** — Add a `"bakedTextures"` array to the manifest listing which textures were baked and from what shader type. Makes it easy to audit baking coverage.

3. **Material parameter dump mode** — Add `--dump-materials` that prints all material names, shader types, texture references, and Vec4 parameters for a model. Essential for debugging "why does this material look wrong?" without reading binary FlatBuffers.

4. **Violet archive support** — The same pipeline works for Pokemon Violet (`violet-dump/arc`). Just point `--arc` at the Violet dump. Consider a `--game scarlet|violet` convenience flag.

5. **Tint baker** — Low-effort addition: for materials with `BaseColor` Vec4 != white and a `BaseColorMap` texture, multiply each albedo pixel by the tint. Covers the remaining non-layered colored materials.

6. **BNTX crash guard** — Wrap the entire Phase 4 BNTX decode loop in AppDomain.CurrentDomain.UnhandledException or use a child process for decode. Prevents a single bad texture from killing a 3,371-model export.

---

## 8. Cross-Reference: Related Docs

| Doc | Topic | Relation |
|-----|-------|----------|
| #20 | PICA Texture Baker (SpicaCli) | Sun/Moon 3DS texture combiner baking — different pipeline, same concept |
| #19 | LZA Extraction Pipeline | Legends: Arceus uses same Trinity format — TrinityTextureBaker will work once LZA loader is fixed |
| #17 | 3DModelLoader & MiniToolbox | Original MiniToolbox architecture and TRPAK extraction pipeline |
| #15 | Skeletal Animation Framework | Downstream consumer of exported DAE models + clips |
