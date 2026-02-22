# 15 - Skeletal Animation & Framework Reference

**Date:** 2026-02-21 (updated)
**Scope:** COLLADA skeletal animation, CPU skinning, tag-based animation framework, manifest system, LZ4 compression
**Models:** `src/Starfield2026.Assets/Models/Pokemon/` (25 species), `Models/Characters/` (33 trainers)

---

## 1. Model & Animation Format

### COLLADA DAE Structure

All Pokemon and character models are exported as COLLADA `.dae` files. Character models come from Switch-era Trinity format (TRMDL/TRSKL), Pokemon from 3DS.

| Element | Characters | Pokemon |
|---------|-----------|---------|
| `library_images` | 40+ images (alb, ao, nrm, msk, etc.) | 11-15 images |
| `library_controllers` | Skin controllers with bone hierarchy | 7-9 skin controllers, 40-55 bones |
| `library_animations` | Matrix-based `/transform` channels | Per-component Euler channels |
| `library_visual_scenes` | Bone hierarchy with `<matrix>` transforms | Same |
| Animation FPS | 60 fps | 30 fps |
| Clips per model | 60-68 | 35 |

### Split-Clip Architecture

Both character and Pokemon models use **split-clip** architecture: one `model.dae` for geometry + skeleton, and separate `clip_NNN.dae` files for each animation. A `manifest.json` file ties them together.

**Character Manifest Format (flat):**
```json
{
  "version": 1,
  "format": "dae",
  "modelFile": "model.dae",
  "animationMode": "baked",
  "textures": ["textures/body_alb.png", "..."],
  "clips": [{
    "index": 0,
    "id": "clip_000",
    "sourceName": "tr0000_00_00000_defaultwait01_loop",
    "file": "animations/clip_000.dae",
    "frameCount": 120,
    "fps": 60
  }]
}
```

**Pokemon Manifest Format (nested):**
```json
{
  "version": 1,
  "mode": "split-model-anims",
  "textures": ["pm0001_00_BodyA1.png"],
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

### Split vs Baked Loading

Both methods parse COLLADA XML for animation channels and bone hierarchy. The difference is structural:

| Aspect | Baked (old) | Split (current) |
|--------|------------|-----------------|
| Files | 1 DAE per clip (model+anim) | 1 model DAE + N clip DAEs |
| Skeleton | Loaded per clip | Loaded once from model.dae |
| Clip loading | Full geometry parse each time | Animation channels only |
| Manifest | Not needed | Required (maps clips to files) |

**The methods are interchangeable.** Split is preferred because the skeleton is loaded once, and clip DAEs are smaller (animation-only). The loader ignores geometry in clip DAEs and only parses animation data.

---

## 2. Tag-Based Animation Framework

### How Tagging Works

Tags are semantic strings ("Idle", "Walk", "Run") that decouple game logic from exporter naming. The flow:

```
manifest.json                    Runtime
    │                               │
    ├── clip.semanticName ──────┐   │
    ├── clip.sourceName ────┐   │   │
    └── clip.index ─────┐   │   │   │
                        │   │   │   │
                   InferTag │   │   │
                        │   │   │   │
                        └───┴───┘   │
                            │       │
                     ClipsByTag     │
                     dictionary     │
                            │       │
              AnimationController.Play("Walk")
                            │
                     SkeletalAnimator.Play(clip)
                            │
                     EvaluatePose → SkinPose
```

1. **At load time:** `SplitModelAnimationSetLoader` reads the manifest and builds two dictionaries:
   - `Clips` — keyed by clip ID (e.g., "clip_000")
   - `ClipsByTag` — keyed by semantic tag (e.g., "Walk"), case-insensitive

2. **Tag resolution priority:**
   - `clip.semanticName` (explicit tag from manifest — highest priority)
   - `InferTagFromSourceName()` (pattern-match the descriptive name)
   - `InferTagFromIndex()` (fallback: index 0=Idle, 1=Walk, 2=Run)

3. **At runtime:** `AnimationController.Play("Walk")` does `ClipsByTag["Walk"]` → delegates to `SkeletalAnimator`

### Character SourceName Tag Inference

Character clips have descriptive `sourceName` fields. The loader extracts the suffix after the 5-digit code and matches patterns:

| SourceName Pattern | Inferred Tag |
|-------------------|-------------|
| `*_defaultwait*_loop` | Idle |
| `*_walk01_loop` | Walk |
| `*_walkup01_loop` | WalkUp |
| `*_walkdown01_loop` | WalkDown |
| `*_run01*_loop` | Run |
| `*_battlewait*_loop` | BattleIdle |
| `*_ballthrow*` | BallThrow |
| `*_speak*_loop` | Speak |
| `*_turn_r090` | TurnRight |
| `*_turn_l090` | TurnLeft |
| `*_loopaction01*_loop` | Action1 |
| `*_chairwait*_loop` | Sit |
| `*_byebye*_loop` | Wave |

Only clips that resolve to a tag are loaded (optimization: skips face anims, etc.).

### AnimationController API

```csharp
_animController.Play("Idle");                    // Play by tag (looping)
_animController.Play("Walk", loop: true, resetTime: false);  // Continue if same
bool has = _animController.HasClip("Jump");      // Check availability
_animController.Update(deltaSeconds);            // Advance time
Matrix[] pose = _animController.SkinPose;        // Get bone matrices
string? tag = _animController.ActiveTag;         // Current tag
```

---

## 3. ManifestScanner — Directory Tree Walker

The `ManifestScanner` finds all `manifest.json` files by walking the directory tree recursively. No hardcoded folder names.

```csharp
// Scan entire Characters directory
var manifests = ManifestScanner.ScanDirectory("Models/Characters");
// Returns List<ManifestEntry> with FolderPath + parsed Manifest for each hit

// Load a specific manifest
ModelManifest? m = ManifestScanner.LoadManifest("Models/Characters/tr0000_00/c5280867888774c5");
```

Handles both character format (flat `clips[]`) and Pokemon format (nested `models[].clips[]`).

---

## 4. Architecture

### Loading Pipeline

```
ManifestScanner.ScanDirectory("Models/Characters")
  → Finds all manifest.json files recursively
  → Returns ManifestEntry[] with FolderPath + parsed manifest

SplitModelAnimationSetLoader.Load(folderPath)
  → Reads manifest.json
  → Handles both character (flat) and Pokemon (nested) formats
  → ColladaSkeletalLoader.LoadSkeleton(model.dae)
    - Parses JOINT nodes from visual_scene
    - Builds SkeletonRig with bind transforms
  → For each tagged clip:
    ColladaSkeletalLoader.LoadClip(clip.dae, skeleton, tag)
    - Parses animation channels (bone → keyframe matrices)
    - Returns SkeletalAnimationClip
  → Returns SplitModelAnimationSet { Skeleton, Clips, ClipsByTag }

OverworldCharacter.Load(device, folderPath)
  → SplitModelAnimationSetLoader.Load()
  → AnimationController wrapping the set
  → SkinnedDaeModel.Load(device, model.dae, skeleton)
    - Parses geometry, skin controllers, materials, textures
    - Builds SkinnedVertex[] with bone weights (top 4)
```

### Runtime Loop

```
OverworldCharacter.Update(dt, isMoving, isRunning, isGrounded):
  1. Determine tag: Idle / Walk / Run / Jump based on player state
  2. AnimationController.Play(tag) → resolves clip from ClipsByTag
  3. SkeletalAnimator.Update(dt)
     a. Advance time: t = (t + dt) % duration
     b. Copy bind pose as baseline
     c. For each animated bone: sample track keyframes at time t
     d. Rebuild world transforms: child.World = child.Local * parent.World
     e. Compute skin matrices: Skin[i] = InverseBind[i] * World[i]
  4. SkinnedDaeModel.UpdatePose(device, skinPose)
     a. CPU skin: for each vertex, blend position/normal by bone weights
     b. Upload to VertexBuffer

OverworldCharacter.Draw(device, view, proj, position, yaw, scale):
  → Sets World/View/Projection on BasicEffect
  → SkinnedDaeModel.Draw() renders all mesh batches
```

### Key Data Structures

| Class | File | Purpose |
|-------|------|---------|
| `SkeletonBone` | SkeletonRig.cs | Index, Name, NodeId, ParentIndex, BindLocalTransform |
| `SkeletonRig` | SkeletonRig.cs | Bone list, BindLocal/World/InverseBind transforms, name lookup |
| `AnimationKeyframe` | SkeletalAnimationClip.cs | TimeSeconds + Transform matrix |
| `BoneAnimationTrack` | SkeletalAnimationClip.cs | Per-bone keyframes with `Sample(t)` interpolation |
| `SkeletalAnimationClip` | SkeletalAnimationClip.cs | Named clip with duration and bone tracks |
| `SplitModelAnimationSet` | SplitModelAnimationSet.cs | ModelPath, Skeleton, Clips dict, ClipsByTag dict |
| `AnimationController` | AnimationController.cs | Tag-based Play/HasClip/Update, exposes SkinPose |
| `SkeletalAnimator` | SkeletalAnimator.cs | Low-level playback: pose evaluation, hierarchy walk |
| `SkinnedDaeModel` | SkinnedDaeModel.cs | COLLADA mesh loader, CPU skinning, Draw batches |
| `OverworldCharacter` | OverworldCharacter.cs | Wraps model + animation for overworld use |
| `ManifestScanner` | ManifestScanner.cs | Recursive directory walker for manifest.json files |
| `ModelManifest` | ManifestScanner.cs | Unified manifest DTO (character + Pokemon formats) |

---

## 5. LZ4 Compression

### DaeToGltf Tool (Compression Utility)

Located at `tools/dae-to-gltf/DaeToGltf/`. Despite the name, this tool compresses DAE + texture files using LZ4:

```bash
DaeToGltf --input=<folder> --output=<folder> [--no-texture-compress]
```

| Input | Output |
|-------|--------|
| `*.dae` | `*.dae.lz4` (LZ4 Pickler compressed) |
| `*.png` (with compress) | `*.dds.lz4` (BC7 DDS + LZ4) |
| `*.png` (no compress) | `*.png` (copied as-is) |

### Runtime Decompression

`ColladaSkeletalLoader.LoadDaeDocument()` transparently handles LZ4:

```csharp
private static XDocument LoadDaeDocument(string path)
{
    string lz4Path = path + ".lz4";
    if (File.Exists(lz4Path))
    {
        byte[] compressed = File.ReadAllBytes(lz4Path);
        byte[] decompressed = LZ4Pickler.Unpickle(compressed);
        using var ms = new MemoryStream(decompressed);
        return XDocument.Load(ms);
    }
    return XDocument.Load(path);
}
```

NuGet: `K4os.Compression.LZ4` version 1.3.8.

---

## 6. File Reference

### Skeletal Animation Framework (all new)

| File | Location | Purpose |
|------|----------|---------|
| `SkeletonRig.cs` | `Core/Rendering/Skeletal/` | Bone hierarchy + bind transforms |
| `SkeletalAnimationClip.cs` | `Core/Rendering/Skeletal/` | Keyframes, tracks, interpolation |
| `ColladaSkeletalLoader.cs` | `Core/Rendering/Skeletal/` | COLLADA XML parser (skeleton + clips), LZ4 support |
| `ManifestScanner.cs` | `Core/Rendering/Skeletal/` | Directory tree walker for manifest.json |
| `SplitModelAnimationSet.cs` | `Core/Rendering/Skeletal/` | Animation set + loader with tag inference |
| `SkeletalAnimator.cs` | `Core/Rendering/Skeletal/` | Low-level animation playback engine |
| `AnimationController.cs` | `Core/Rendering/Skeletal/` | High-level tag-based controller |
| `SkinnedDaeModel.cs` | `Core/Rendering/Skeletal/` | COLLADA mesh loader + CPU skinning + rendering |
| `OverworldCharacter.cs` | `Core/Rendering/Skeletal/` | Overworld integration wrapper |

### Modified Files

| File | Change |
|------|--------|
| `Starfield2026.Core.csproj` | Added K4os.Compression.LZ4 NuGet |
| `Screens/OverworldScreen.cs` | Loads character model, replaces cube with animated character |

---

## 7. Known Issues & Required Fixes

### CRITICAL: Bind-Pose Matrix Decomposition (Pokemon only)

Pokemon DAE animation channels use per-component Euler targets (`rotation.X/Y/Z`). Character DAEs use full matrix `/transform` targets which don't have this issue.

For Pokemon: decompose each bone's `LocalBindPose` matrix into per-component defaults to provide base rotation/translation for non-animated axes.

### Texture UV Mapping

- Don't use Assimp's `FlipUVs` — destroys tiled UVs (V > 1.0 for eyes)
- Use raw UVs with `SamplerState.LinearWrap`
- If textures appear flipped: flip texture pixel data on load, not UVs

---

## 8. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| CPU skinning (not GPU) | Models have manageable vertex counts. Avoids custom shaders. |
| Custom COLLADA parser | Full control over bone/animation parsing. Assimp strips skeleton with PreTransformVertices. |
| No FlipUVs | Tiled UVs break with `V = 1-V`. Raw UVs + LinearWrap is correct. |
| Tag-based clips | `Play("Idle")` works regardless of exporter naming. |
| Tag-gated loading | Only load clips that resolve to a semantic tag. Skips face/expression anims. |
| ManifestScanner tree walk | No hardcoded folder names. Works with any asset organization. |
| LZ4 compression | Transparent decompression at load time. Reduces disk I/O. |

---

## 9. Future Optimizations

- **GPU skinning** — Move bone transforms to vertex shader (post-MVP)
- **Animation blending** — `CrossFade("Walk", "Run", 0.2f)` for smooth transitions
- **DynamicVertexBuffer reuse** — Allocate once during Load(), call SetData per frame
- **Texture cache** — Characters share rim textures; cache across model loads
- **Skip off-screen** — Don't Update when model isn't visible
- **GLTF conversion** — Pre-convert DAE to GLTF/GLB for faster loading
