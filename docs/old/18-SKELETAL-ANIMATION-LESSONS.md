# 18 - Skeletal Animation & CPU Skinning: Lessons Learned & Handoff

**Date:** 2026-02-16
**Scope:** Skeletal animation system for Pokemon 3D models, COLLADA animation parsing, CPU skinning pipeline, texture UV mapping
**Codebase:** PokemonGreen.Assets (SkeletalModelData, BattleModelLoader, PokemonModelLoader), PokemonGreen (Game1 battle rendering)

---

## 1. What We Accomplished

### Custom COLLADA Animation Parser (Working)
- **Assimp cannot parse per-component Euler angle channels** (`rotation.X/Y/Z`, `translation.X/Y/Z`) from COLLADA files. `scene.HasAnimations` returns `False` for all 875+ animated Pokemon models.
- Built a custom XML parser (`ParseColladaAnimations`) that reads `<library_animations>` directly.
- Successfully extracts 38-41 bone channels per model with correct timing (0.8-1.4s idle loops).
- Groups per-axis channels (X/Y/Z) by bone name, merges into combined Vector3/Quaternion keyframes.
- Handles HERMITE interpolation curves (no tangent data = effectively linear interpolation).

### Skeletal Model Data Structure (New File)
- **`SkeletalModelData.cs`** — Complete CPU skeletal animation system:
  - `BoneInfo` struct: name, parent index, offset matrix (inverse bind pose), local bind pose, global/final transforms
  - `AnimChannel` struct: per-bone position keys (VecKey[]), rotation keys (QuatKey[]), scale keys (VecKey[])
  - `SkeletalMeshData` class: bind-pose vertices, per-vertex bone weights (top 4), DynamicVertexBuffer for CPU updates
  - `SkeletalModelData` class: bone hierarchy, animation channels, Update/Draw methods
- Full CPU skinning pipeline: interpolate keyframes, walk bone hierarchy, transform vertices, upload to GPU each frame.

### Bone Hierarchy Extraction (Working)
- Properly filters skeleton-relevant nodes from mesh instance nodes using `MarkAncestors` + `BuildBoneList`.
- 45-55 skin bones per model, 44-49 total hierarchy nodes (includes ancestor container nodes).
- Offset matrices (inverse bind matrices) correctly loaded from Assimp's mesh bone data.

### Texture Resolution Pipeline (Working)
- `ResolveDiffusePath()` correctly resolves Assimp's `_id` suffixed texture references to `.tga.png` files on disk.
- `FindTextureForMaterial()` handles material name → texture file matching with proper suffix mapping.
- Textures load correctly: Body, Eye, Iris, and special materials (FireStenA, FireCoreA) all resolved.
- Diagnostic log confirms all 7-8 meshes per model get their correct texture.

### Battle Rendering Integration (Working)
- `Game1.cs` loads skeletal models for ally/foe Pokemon during battle entry.
- Models positioned on battle platforms with proper scaling (FitModelScale).
- `LinearWrap` sampler state for tiled UV support on Pokemon meshes.
- Animation `Update()` called each frame with `gameTime.TotalGameTime.TotalSeconds`.

---

## 2. What Work Remains

### Critical: Bind-Pose Matrix Decomposition (Root Cause of Deformation)
**ALL Pokemon DAE models use `<matrix>` elements for bone transforms** — zero models have decomposed `<translate>/<rotate>/<scale>` elements. The animation channels target per-component values (`rotation.X`, etc.) that represent the decomposed components of this matrix. Our code builds animated local transforms FROM SCRATCH using only the animation values, completely ignoring the base rotation/translation embedded in the `<matrix>`.

**Example — Charmander Waist bone:**
| Property | Bind-Pose Matrix | Animated (our code) |
|----------|-----------------|---------------------|
| Rotation | 90deg Z-axis (diag 0,0,1) | ~1deg X-axis (diag 0.999,0.999,1) |
| Translation | (0, 21.36, -1.54) | (0, 20.21, 0.30) |

The animation provides ABSOLUTE per-component values for a decomposed T * Rz * Ry * Rx * S transform, but without the bind-pose defaults (like the 90deg Z rotation), the composed transform is wrong. **This is the #1 cause of model deformation/flattening.**

### Critical: Texture UV Mapping
- Assimp's `FlipUVs` post-process does `V = 1.0 - V` which destroys tiled UV coordinates (V > 1.0 becomes negative).
- Currently NOT using FlipUVs (raw UVs + raw textures + LinearWrap sampler).
- Textures may appear vertically flipped — needs verification once mesh deformation is fixed.
- Some meshes have tiled UVs (V up to 2.97 for eyes) — requires `TextureAddressMode.Wrap`, not Clamp.

### Minor: Diagnostic Logging Cleanup
- `skeletal_log.txt` file logging should be removed or gated behind a debug flag once animation works.
- First-frame matrix diagnostics can be removed after verification.

### Minor: Model Bounds After Skinning
- `BoundsMin/Max` are computed from raw bind-pose vertices, not skinned vertices.
- For FitModelScale to be accurate, bounds should reflect the actual rendered positions.

---

## 3. Optimizations — Prime Suspects

### 3.1 Matrix Decomposition for Animation Defaults (THE FIX)
The `ParseNodeDefaults()` method reads `<translate>/<rotate>/<scale>` from the DAE XML but **none exist** — all nodes use `<matrix>`. Must instead decompose each bone's `LocalBindPose` matrix (from Assimp) into per-component defaults:
```
Translation: matrix row 4 (XNA) = (M41, M42, M43)
Scale: row magnitudes of upper-left 3x3
Rotation: ZYX Euler decomposition of normalized 3x3
  beta = asin(-R[2,0])
  alpha = atan2(R[2,1]/cos(beta), R[2,2]/cos(beta))
  gamma = atan2(R[1,0]/cos(beta), R[0,0]/cos(beta))
```
These decomposed values become defaults for non-animated axes. For Charmander's Waist: rotation.Z defaults to 90deg (from the matrix), so even if only rotation.X is animated, the 90deg base rotation is preserved.

### 3.2 Euler Angle Convention Verification
COLLADA uses intrinsic ZYX rotation order: `T * Rz * Ry * Rx * S`. Our quaternion composition is `qx * qy * qz` (XNA: X first, then Y, then Z). This matches COLLADA's intrinsic X-Y-Z order. However, the decomposition must use the SAME convention. If wrong, rotations will be subtly incorrect on all bones.

**Verification method:** For any bone, compose the animated local transform at t=0 using decomposed defaults. Compare against Assimp's `node.Transform` (the bind-pose matrix). The two matrices should be nearly identical (within floating-point tolerance). The diagnostic log shows they currently DON'T match — confirming this is the issue.

### 3.3 CPU Skinning Performance
Current implementation transforms ALL vertices on CPU every frame. For 5K-15K vertices per model (2 models in battle), this is ~30K vertex transforms per frame — trivial on modern CPUs. However:
- Consider skipping Update when the model isn't visible or the animation hasn't changed.
- DynamicVertexBuffer upload could be batched if multiple models share timing.

### 3.4 Assimp Import Flags
Currently importing with only `Triangulate | GenerateSmoothNormals`. Consider:
- `JoinIdenticalVertices` — reduces vertex count for faster skinning.
- NOT using `PreTransformVertices` (correct — needed for skeleton).
- NOT using `FlipUVs` (correct — tiled UVs would break).

---

## 4. Step by Step: Getting Skeletal Animation Fully Working

### Prerequisites
- .NET 9.0 SDK
- 957+ Pokemon3D model folders in `src/PokemonGreen.Assets/Pokemon3D/`
- Build: `dotnet build src/PokemonGreen/PokemonGreen.csproj`

### Step 1: Implement Bind-Pose Matrix Decomposition
In `SkeletalModelData.cs`, add a `DecomposeBindPose(XnaMatrix m)` method:
```csharp
private static Dictionary<string, float> DecomposeBindPose(XnaMatrix m)
{
    var d = new Dictionary<string, float>();
    // Translation from row 4 (XNA convention)
    d["translation.X"] = m.M41;
    d["translation.Y"] = m.M42;
    d["translation.Z"] = m.M43;
    // Scale = row magnitudes of upper-left 3x3
    float sx = new Vector3(m.M11, m.M12, m.M13).Length();
    float sy = new Vector3(m.M21, m.M22, m.M23).Length();
    float sz = new Vector3(m.M31, m.M32, m.M33).Length();
    d["scale.X"] = sx; d["scale.Y"] = sy; d["scale.Z"] = sz;
    // Normalized rotation matrix
    float r00 = m.M11/sx, r01 = m.M12/sx, r02 = m.M13/sx;
    float r10 = m.M21/sy, r11 = m.M22/sy, r12 = m.M23/sy;
    float r20 = m.M31/sz, r21 = m.M32/sz, r22 = m.M33/sz;
    // ZYX Euler decomposition (matches Rx * Ry * Rz composition)
    float beta = MathF.Asin(MathHelper.Clamp(-r20, -1f, 1f));
    float cosBeta = MathF.Cos(beta);
    float alpha, gamma;
    if (MathF.Abs(cosBeta) > 1e-6f)
    {
        alpha = MathF.Atan2(r21 / cosBeta, r22 / cosBeta);
        gamma = MathF.Atan2(r10 / cosBeta, r00 / cosBeta);
    }
    else // gimbal lock
    {
        alpha = MathF.Atan2(-r12, r11);
        gamma = 0f;
    }
    d["rotation.X"] = MathHelper.ToDegrees(alpha);
    d["rotation.Y"] = MathHelper.ToDegrees(beta);
    d["rotation.Z"] = MathHelper.ToDegrees(gamma);
    return d;
}
```

### Step 2: Replace ParseNodeDefaults with Matrix Decomposition
In `ParseColladaAnimations`, after building the bone hierarchy, decompose each bone's `LocalBindPose`:
```csharp
// Replace ParseNodeDefaults entirely
var nodeDefaults = new Dictionary<string, Dictionary<string, float>>();
foreach (var (boneName, boneIdx) in boneNameToIndex)
    nodeDefaults[boneName] = DecomposeBindPose(result.Bones[boneIdx].LocalBindPose);
```

### Step 3: Verify Bind-Pose Roundtrip
Add a one-time log that compares the composed animated local transform at t=0 against the Assimp bind-pose local transform. They should match within ~0.01 tolerance for all bones.

### Step 4: Fix Texture UV Mapping
Once mesh shape is correct (Step 1-3), verify texture orientation:
1. If textures appear vertically flipped: flip the texture data on load (not the UVs).
2. If tiled UVs wrap incorrectly: ensure `SamplerState.LinearWrap` is active during Pokemon draws.
3. The DAE wrap modes suggest `MIRROR` for U and `WRAP` for T — may need a custom sampler state.

### Step 5: Clean Up and Verify
- Remove diagnostic `skeletal_log.txt` file logging (or gate behind `#if DEBUG`).
- Remove first-frame matrix logging.
- Test with multiple Pokemon species to ensure animation plays correctly.

### Running the Game
```bash
dotnet build src/PokemonGreen/PokemonGreen.csproj
dotnet run --project src/PokemonGreen/PokemonGreen.csproj
```
Walk into tall grass, trigger an encounter. Battle screen should show:
- 3D background (Grass/Cave depending on encounter type)
- Ally and foe platforms
- Pokemon models on platforms with idle breathing animation
- Correct textures mapped to body/eyes/iris

---

## 5. How to Test & Debug

### Build & Run
```bash
dotnet build src/PokemonGreen/PokemonGreen.csproj
dotnet run --project src/PokemonGreen/PokemonGreen.csproj
```

### Diagnostic Log
The skeletal model loader writes to `bin/Debug/net9.0/skeletal_log.txt`:
- Root node transform (should be identity)
- Bone hierarchy (name, parent index)
- Animation channel count and duration
- Per-mesh: material name, UV presence, vertex count, bone count, texture filename
- First-frame bone matrices (bind-pose vs animated local, final matrix)
- Sample vertex before/after skinning

### Key Metrics to Check
- **Animation channels > 0** — confirms COLLADA XML parser is working
- **FinalMatrix diag near (1,1,1,1)** for root/identity bones — confirms skinning math is correct
- **Waist FinalMatrix diag near (0,0,1,1)** — confirms 90deg Z rotation is preserved (currently BROKEN)
- **Vert[0] bind ≈ skinned** — at bind pose, skinning should not change vertex positions

### Manual DAE Inspection
```bash
# Check if a model uses <matrix> vs decomposed transforms:
grep -c "<matrix>" Pokemon3D/pm0001_00/model.dae    # Should show ~50+ matrices
grep -c "<translate>" Pokemon3D/pm0001_00/model.dae  # Should show 0
grep -c "<rotate" Pokemon3D/pm0001_00/model.dae      # Should show 0

# Check animation channel count:
grep -c '<channel' Pokemon3D/pm0001_00/model.dae     # ~91 for Bulbasaur

# Check animation target format:
grep '<channel' Pokemon3D/pm0001_00/model.dae | head -5
# Expected: <channel source="#..._sampler" target="Waist_bone_id/rotation.X" />
```

---

## 6. Known Issues & New Strategies

### Issue 1: Animated Local Transform Doesn't Match Bind Pose (CRITICAL)
**Symptom:** Models appear flattened/deformed in battle. Diagnostic shows Waist bind-pose has 90deg Z rotation but animated local has near-identity rotation.
**Root Cause:** ALL DAE nodes use `<matrix>` elements. `ParseNodeDefaults` looks for `<translate>/<rotate>/<scale>` elements that don't exist. Missing defaults cause the 90deg base rotation to be lost when composing the animated transform.

**Strategies:**
1. **Decompose bind-pose matrix** — Extract per-component defaults (translate, Euler XYZ, scale) from each bone's `LocalBindPose` matrix using ZYX Euler decomposition. Use these as defaults for non-animated axes. ~50 lines of code. **This is the prime fix.**
2. **Multiplicative animation** — Instead of building a new local transform from scratch, apply animation as a DELTA on the bind-pose matrix. Decompose the difference between animated and bind-pose values. More complex but handles edge cases (gimbal lock).
3. **Hybrid approach** — Use the bind-pose matrix directly and only REPLACE the specific components that have animation channels. Parse `<matrix>` once to get per-component values, then at each frame swap in the animated component before recomposing. Requires maintaining per-bone component arrays.
4. **Pre-process DAE files** — Write a one-time converter that replaces all `<matrix>` elements with decomposed `<translate>/<rotate sid="rotation.Z/Y/X">/<scale>` elements. Then `ParseNodeDefaults` works as designed. Run once on all 957 models.

### Issue 2: Texture UV Mapping (MODERATE)
**Symptom:** Textures may appear vertically flipped on Pokemon meshes.
**Root Cause:** Assimp's `FlipUVs` does `V = 1.0 - V` which destroys tiled UVs (V > 1.0 for eyes). Currently using raw UVs with no flip.

**Strategies:**
1. **Flip textures on load** — Instead of flipping UVs, vertically flip the texture pixel data during `Texture2D.FromStream`. This preserves tiled UV coordinates while fixing the orientation mismatch.
2. **Manual UV flip with wrap-aware math** — In the vertex loading loop, apply `V = fract(V) → 1.0 - fract(V) + floor(V)` to flip within each tile while preserving wrapping.
3. **Custom sampler with MirrorOnce** — Use `TextureAddressMode.Mirror` for V axis instead of Wrap. This effectively flips every other tile, which may produce correct results for the specific tiling pattern used.
4. **Compare with Ohana3DS viewer** — Ohana3DS-Rebirth has its own model viewer. Load a Pokemon model there and screenshot the correct texture mapping. Use as ground truth for our UV/texture settings.

### Issue 3: Assimp Cannot Parse COLLADA Per-Component Animations (RESOLVED)
**Symptom:** `scene.HasAnimations = False` for all Pokemon DAE files.
**Root Cause:** AssimpNet 4.1.0's COLLADA importer doesn't support `rotation.X/Y/Z` channel targets (per-component Euler angles).
**Resolution:** Custom `ParseColladaAnimations` method reads COLLADA XML directly. Successfully extracts 38-41 channels per model.

### Issue 4: Some Bones Have Identity Offset Matrices (MINOR)
**Symptom:** Container bones (vs_model_id, pm####_bone_id, Origin_bone_id) have Identity offset matrices because they're not referenced by skin controllers.
**Impact:** These bones are never used as skinning targets (no vertex weights reference them), so Identity offset matrices are harmless. They serve only as hierarchy containers.

---

## 7. Architecture & New Features

### Current Architecture
```
Game1.EnterBattle()
  -> PokemonModelLoader.Load(species.ModelFolder, graphicsDevice)
     -> SkeletalModelData.Load(daeFilePath, graphicsDevice)
        1. Assimp imports DAE (no FlipUVs, no PreTransformVertices)
        2. Build bone hierarchy from skin-referenced nodes + ancestors
        3. Extract offset matrices from Assimp mesh bones
        4. ParseColladaAnimations: custom XML parser for per-component channels
           - Reads library_animations for rotation.X/Y/Z, translation.X/Y/Z
           - Groups by bone, merges into VecKey[]/QuatKey[] arrays
           - ParseNodeDefaults: reads bind-pose component defaults (BROKEN: nodes use <matrix>)
        5. Extract mesh vertices + bone weights (top 4 per vertex)
        6. Resolve textures via ResolveDiffusePath + FindTextureForMaterial
     -> Returns SkeletalModelData

Game1.Update() (battle)
  -> model.Update(totalSeconds)
     1. Loop animation time: t = (totalSeconds % duration) * ticksPerSecond
     2. Start with bind-pose local transforms for all bones
     3. Override animated bones: compose Scale * Rotation(euler->quat) * Translate
     4. Walk hierarchy: child.Global = child.Local * parent.Global
     5. Final = Offset * Global (inverse bind * animated global)
     6. CPU skin: v_out = sum(v_bind * FinalMatrix[bone] * weight)
     7. Upload to DynamicVertexBuffer

Game1.DrawBattle3D()
  -> Background.Draw (BattleModelData, AlphaTestEffect, PointClamp)
  -> Platforms.Draw (BattleModelData, AlphaTestEffect, PointClamp)
  -> FoeModel.Draw (SkeletalModelData, AlphaTestEffect, LinearWrap)
  -> AllyModel.Draw (SkeletalModelData, AlphaTestEffect, LinearWrap)
```

### Key Design Decisions
| Decision | Rationale |
|----------|-----------|
| CPU skinning (not GPU) | Pokemon have 5K-15K vertices — trivial for CPU. Avoids custom shaders/vertex types. Keeps AlphaTestEffect compatibility. |
| Custom animation parser | Assimp can't handle per-component Euler channels. Only alternative is a different library or format conversion. |
| No FlipUVs | Tiled UVs (V up to 2.97) break with `V = 1-V` flip. Raw UVs + texture flip is safer. |
| LinearWrap sampler | Pokemon textures tile (especially eyes/iris). PointClamp used for pixel-art backgrounds. |
| DynamicVertexBuffer | Required for CPU skinning — re-uploaded every frame with transformed vertices. |

### Quick Wins

1. **Implement `DecomposeBindPose`** (~50 lines) — Extracts per-component defaults from bind-pose matrices. This single fix should resolve all model deformation. Estimated effort: 30 minutes.

2. **Flip textures on load** (~10 lines) — After `Texture2D.FromStream`, flip pixel rows vertically. Fixes UV orientation without breaking tiled coordinates. Estimated effort: 15 minutes.

3. **Gate diagnostic logging** (~5 lines) — Wrap `Log()` calls with `#if DEBUG` or a static bool. Currently every model load writes to `skeletal_log.txt`. Estimated effort: 5 minutes.

4. **Verify with Bulbasaur specifically** — pm0001_00 was the most thoroughly analyzed model. Use it as the "ground truth" test case for animation correctness. If Bulbasaur works, other models likely will too.

---

## 8. File Reference

| File | Status | Purpose |
|------|--------|---------|
| `src/PokemonGreen.Assets/SkeletalModelData.cs` | **NEW** | Skeletal animation system: bone hierarchy, COLLADA anim parsing, CPU skinning, Draw |
| `src/PokemonGreen.Assets/PokemonModelLoader.cs` | Modified | Changed to return `SkeletalModelData` instead of `BattleModelData` |
| `src/PokemonGreen.Assets/BattleModelLoader.cs` | Modified (prev commit) | Made `ResolveDiffusePath`/`FindTextureForMaterial` internal for SkeletalModelData access |
| `src/PokemonGreen/Game1.cs` | Modified | Changed model types to SkeletalModelData, wired animation Update, removed sine-wave bob |
| `bin/Debug/net9.0/skeletal_log.txt` | Runtime | Diagnostic output from model loading and first-frame skinning |

### COLLADA DAE Model Structure (All Pokemon)
```
library_images:     11-15 images with ./filename.tga.png relative paths
library_effects:    5-8 effects, each with one diffuse texture sampler
library_materials:  5-8 materials (BodyA, BodyB, Eye, LIris, RIris, etc.)
library_geometries: 7-9 meshes (multiple sub-meshes per material)
library_controllers: 7-9 skin controllers, identity bind_shape_matrix, 40-55 bones each
library_animations:  91-122 per-component channels (rotation.X/Y/Z, translation.X/Y/Z)
library_visual_scenes: bone hierarchy using <matrix> transforms (NOT decomposed)
```

### Animation Channel Format
```xml
<animation id="Waist_rotation_X">
  <source id="..._input"><float_array>0 0.1 0.7 1.37 1.4</float_array></source>
  <source id="..._output"><float_array>1.04 1.04 0.99 1.04 1.04</float_array></source>
  <channel target="Waist_bone_id/rotation.X" />
</animation>
```
- Target format: `{BoneName}_bone_id/{property}.{axis}`
- Properties: `rotation`, `translation`, `scale`
- Axes: `X`, `Y`, `Z`
- Values: degrees for rotation, world units for translation
- Times: seconds (TicksPerSecond = 1.0)
- Duration: 0.8-1.4 seconds per idle loop
