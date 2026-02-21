# 15 - Skeletal Animation & Framework Reference

**Date:** 2026-02-21 
**Scope:** COLLADA skeletal animation, CPU skinning, tag-based animation framework, manifest system 
**Models:** `src/Starfield2026.Assets/Models/Pokemon/` (25 species), `Models/Characters/` (18 trainers)

---

## 1. Model & Animation Format

### COLLADA DAE Structure (All Models)

All Pokemon and character models are exported as COLLADA `.dae` files from 3DS game assets. They share these characteristics:

| Element | Details |
|---------|---------|
| `library_images` | 11-15 images, relative paths like `./filename.png` |
| `library_effects` | 5-8 effects with diffuse texture samplers |
| `library_materials` | BodyA, BodyB, Eye, LIris, RIris, etc. |
| `library_geometries` | 7-9 meshes, multiple sub-meshes per material |
| `library_controllers` | 7-9 skin controllers, identity bind_shape_matrix, 40-55 bones each |
| `library_animations` | 91-122 per-component channels (rotation.X/Y/Z, translation.X/Y/Z) |
| `library_visual_scenes` | Bone hierarchy using `<matrix>` transforms (**NOT decomposed**) |

### Animation Channel Format

```xml
<animation id="Waist_rotation_X">
  <source id="..._input"><float_array>0 0.1 0.7 1.37 1.4</float_array></source>
  <source id="..._output"><float_array>1.04 1.04 0.99 1.04 1.04</float_array></source>
  <channel target="Waist_bone_id/rotation.X" />
</animation>
```

- **Target format:** `{BoneName}_bone_id/{property}.{axis}`
- **Properties:** `rotation`, `translation`, `scale`
- **Values:** degrees for rotation, world units for translation
- **Duration:** 0.8-1.4 seconds per idle loop (TicksPerSecond = 1.0)

### Split-Clip Manifest Format

Pokemon models use a `manifest.json` with separate per-clip DAE files:

```json
{
  "version": 1,
  "mode": "split-model-anims",
  "textures": ["pm0001_00_BodyA1.png", "..."],
  "models": [{
    "name": "model",
    "modelFile": "model.dae",
    "clips": [{
      "index": 0,
      "name": "anim_0",
      "semanticName": "Idle",
      "file": "clips/model/clip_000.dae",
      "frameCount": 42,
      "fps": 30
    }]
  }]
}
```

Pokemon models have 35 clips each. Character models currently have **empty clips arrays** — they need re-export with `--split-model-anims` to generate clip DAEs.

---

## 2. Known Issues & Required Fixes

### CRITICAL: Bind-Pose Matrix Decomposition

> [!CAUTION]
> ALL DAE nodes use `<matrix>` elements for bone transforms — zero models have decomposed `<translate>/<rotate>/<scale>`. Animation channels target per-component values (e.g. `rotation.X`) that represent the decomposed parts of this matrix. Without extracting bind-pose defaults from the matrix, base rotations (like a 90° Z on the Waist bone) are lost, causing **model deformation/flattening**.

**The fix:** Decompose each bone's `LocalBindPose` matrix into per-component defaults:

```csharp
// Translation from row 4 (XNA convention)
d["translation.X"] = m.M41;
d["translation.Y"] = m.M42;
d["translation.Z"] = m.M43;

// Scale = row magnitudes of upper-left 3x3
float sx = new Vector3(m.M11, m.M12, m.M13).Length();

// ZYX Euler decomposition of normalized 3x3
float beta = MathF.Asin(MathHelper.Clamp(-r20, -1f, 1f));
float cosBeta = MathF.Cos(beta);
float alpha = MathF.Atan2(r21 / cosBeta, r22 / cosBeta);  // rotation.X
float gamma = MathF.Atan2(r10 / cosBeta, r00 / cosBeta);  // rotation.Z
```

### CRITICAL: Assimp Cannot Parse Per-Component Animations

Assimp's `scene.HasAnimations` returns `False` for all Pokemon DAEs because it doesn't support `rotation.X/Y/Z` channel targets. **Solution:** Custom XML parser that reads `<library_animations>` directly, extracting per-axis channels and merging into combined keyframes.

### Texture UV Mapping

- Assimp's `FlipUVs` does `V = 1.0 - V` which **destroys tiled UVs** (V > 1.0 for eyes)
- Use raw UVs with `SamplerState.LinearWrap` (not Clamp)
- If textures appear flipped: flip the texture pixel data on load, not the UVs
- Some meshes have tiled UVs (V up to 2.97) — requires `TextureAddressMode.Wrap`

---

## 3. Architecture

### Loading Pipeline

```
manifest.json
  → ManifestLoader.Load(folderPath)
    → Assimp imports model.dae (Triangulate, GenerateNormals, NO FlipUVs)
    → Build bone hierarchy (MarkAncestors + BuildBoneList)
    → Extract offset matrices from skin controllers
    → ParseColladaAnimations: custom XML parser
      - Reads per-component Euler channels (rotation.X/Y/Z)
      - Groups by bone, merges into VecKey[]/QuatKey[] arrays
      - DecomposeBindPose: extracts defaults from <matrix> elements
    → Extract mesh vertices + bone weights (top 4 per vertex)
    → Resolve textures: ResolveDiffusePath + FindTextureForMaterial
  → Returns SkeletalModelData

Each clip DAE loaded separately via ColladaSkeletalLoader.LoadClip()
  → AnimationController.Play("Idle")
```

### Runtime Loop

```
Update(dt):
  1. AnimationController selects clip by semantic tag
  2. Loop animation time: t = (totalSeconds % duration)
  3. Interpolate keyframes for each bone
  4. Compose: Scale * Rotation(euler→quat) * Translate
  5. Walk hierarchy: child.Global = child.Local * parent.Global
  6. Final = Offset * Global (inverse bind × animated global)
  7. CPU skin: v_out = sum(v_bind * FinalMatrix[bone] * weight)
  8. Upload to DynamicVertexBuffer

Draw():
  → AlphaTestEffect + LinearWrap sampler for Pokemon/Character meshes
  → AlphaTestEffect + PointClamp sampler for battle backgrounds
```

### Key Data Structures

| Struct | Purpose |
|--------|---------|
| `BoneInfo` | name, parent index, offset matrix, local bind pose, global/final transforms |
| `AnimChannel` | per-bone position keys (VecKey[]), rotation keys (QuatKey[]), scale keys (VecKey[]) |
| `SkeletalMeshData` | bind-pose vertices, per-vertex bone weights (top 4), DynamicVertexBuffer |
| `SkeletalModelData` | bone hierarchy, animation channels, Update/Draw methods |
| `AnimationController` | `Play("Idle")`, `HasClip("Jump")`, `Update(dt)`, tag-based clip resolution |

---

## 4. Tag-Based Animation Framework

### Semantic Tags

Animations are resolved by purpose, not by exporter-specific naming:

| Slot | Overworld Tag | Pokemon Tag |
|------|--------------|-------------|
| 0 | Idle | Idle |
| 1 | Walk | Attack1 |
| 2 | Run | Attack2 |
| 3 | FastRun | Attack3 |
| 4 | Jump | Faint |
| 5 | Land | HitReact |
| 6 | ShortAction1 | Special1 |
| 7 | LookAround | Special2 |
| 8-15 | Action2-9 | Action3-10 |

### AnimationController API

```csharp
_animController.Play("Idle");         // Start playing by tag
_animController.Play("Walk");         // Switch to walk
bool has = _animController.HasClip("Jump");  // Check availability
_animController.Update(deltaTime);    // Advance animation
Matrix[] pose = _animController.SkinPose;    // Get bone matrices
```

---

## 5. Files to Create/Port

### Current State in Starfield-2026

| File | Status |
|------|--------|
| `Core/Rendering/Battle/SkeletalModelData.cs` | **STUB** — empty methods, needs full implementation |
| `AnimationController.cs` | **Missing** — needs to be created |
| `ColladaSkeletalLoader.cs` | **Missing** — custom COLLADA parser |
| `ManifestLoader.cs` | **Missing** — reads manifest.json |
| `Models/Pokemon/*` | ✅ 25 species with clips & manifests |
| `Models/Characters/*` | ⚠️ 18 trainers, DAEs present but **no clip DAEs** |

### Implementation Order

1. **`ColladaSkeletalLoader.cs`** — COLLADA XML parser + bone hierarchy builder + mesh extractor
2. **`SkeletalModelData.cs`** — Full CPU skinning pipeline (replace stub)
3. **`ManifestLoader.cs`** — Read manifest.json, discover clip files
4. **`AnimationController.cs`** — Tag-based clip selection, playback, pose output
5. Wire into battle rendering (`BattleScreen3D`) and overworld (`OverworldScreen`)

---

## 6. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| CPU skinning (not GPU) | 5K-15K vertices per model — trivial for CPU. Avoids custom shaders. Keeps AlphaTestEffect compatibility. |
| Custom COLLADA parser | Assimp can't handle per-component Euler channels. XML parsing is straightforward. |
| No FlipUVs | Tiled UVs break with `V = 1-V`. Raw UVs + LinearWrap is correct. |
| DynamicVertexBuffer | Required for CPU skinning — re-uploaded every frame. |
| Tag-based clips | Decouples game logic from exporter naming. `Play("Idle")` works regardless of source. |
| Lazy clip loading | Only parse clip DAEs on first `Play()`. Most clips are never used. |

---

## 7. Future Optimizations

- **GPU skinning** — Move bone transforms to vertex shader (post-MVP)
- **Animation blending** — `CrossFade("Walk", "Run", 0.2f)` for smooth transitions
- **Buffer reuse** — Allocate DynamicVertexBuffer once, call SetData per frame
- **Texture cache** — Characters share rim textures; cache across model loads
- **Skip off-screen** — Don't Update when model isn't visible
