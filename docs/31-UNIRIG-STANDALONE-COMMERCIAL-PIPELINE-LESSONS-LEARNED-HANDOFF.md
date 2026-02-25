# 31 - UniRig Standalone Commercial Pipeline: Lessons Learned Handoff

**Date:** 2026-02-25
**Scope:** Analysis of ComfyUI-UniRig internals for extraction into a standalone, minimal-dependency commercial rigging pipeline.
**Status:** DAE parser complete and tested. Skeleton prediction working on DAE meshes. Skinning and animation transfer remain.

---

## 1. What We Accomplished

### DAE Parser — Complete (dae_parser.py)

Built a standalone Collada DAE parser using only stdlib `xml.etree.ElementTree` + numpy. Zero external dependencies. Replaces `trimesh`, `pycollada`, and `bpy`'s file I/O in 450 lines.

**Tested on three file types:**

| File Type | Result |
|-----------|--------|
| Pokemon model (`pm0001_00/model.dae`) | 9 meshes, 3589 verts, 5372 tris, 55-bone skeleton, 9 skin controllers, normals + UVs |
| Animation clip (`clips/clip_000.dae`) | 40 animation channels, 43 keyframes each, 4x4 matrices, 1.4s duration |
| Battle background (`Cave/Cave.dae`) | 1 mesh, 282 verts, 48 tris, static |

**What it extracts:**
- Mesh geometry: vertices, normals, UVs, triangle faces (with polylist/quad triangulation)
- Skeleton hierarchy: joint nodes with 4x4 local transforms, parent-child tree
- Skin controllers: bone names, inverse bind matrices, per-vertex weights (bone_idx + weight pairs)
- Animations: per-bone keyframe times + 4x4 transform matrices + interpolation type
- Combined mesh properties: `combined_vertices`, `combined_faces`, `combined_normals` for multi-mesh models

**Key design decisions:**
- Bone name extraction from animation channels strips `_bone_id` suffix automatically (`Waist_bone_id/transform` → `Waist`)
- Face indices validated — combined mesh offsets ensure correct indexing across sub-meshes
- Dataclass-based API (`DaeDocument`, `DaeMesh`, `DaeJoint`, `DaeAnimChannel`, `DaeSkinController`)
- CLI mode built in for quick testing: `python dae_parser.py model.dae`

**This eliminates the #1 blocker** from the previous session (DAE files loading as empty scenes in trimesh/pycollada).

### Full Reverse Engineering of ComfyUI-UniRig

Traced the entire ComfyUI-UniRig pipeline end-to-end to understand exactly what it does, what depends on what, and what can be eliminated. The wrapper has three major subsystems:

#### Subsystem A: Skeleton Prediction (already ported in POC)
- **What it does:** Takes a mesh (vertices + normals), runs OPT-350m + Michelangelo encoder, outputs joint positions + hierarchy + bone names
- **Key file:** `nodes/unirig/direct.py` → `predict_skeleton()` and `predict_skeleton_from_mesh()`
- **External deps used:** `torch`, `transformers`, `safetensors`, `numpy`, `einops`
- **ComfyUI deps used:** `comfy.ops` (nn.Linear shim), `comfy.utils` (safetensors loader), `comfy.model_management` (GPU device), `comfy.model_patcher` (memory management)
- **Status:** Already working in standalone POC at `D:\Projects\poc\unirig\`

#### Subsystem B: Skin Weight Prediction (not yet ported)
- **What it does:** Takes mesh + predicted skeleton, outputs per-vertex skin weights (N vertices × J joints)
- **Key files:** `nodes/unirig/unirig_skin.py` (UniRigSkin model), `nodes/unirig/direct.py` → `predict_skinning()`
- **Architecture:** PTv3 encoder (local mesh features) + Michelangelo encoder (global features) + BoneEncoder + cross-attention → per-vertex weight prediction
- **Critical deps:** `torch_scatter` (segment_csr for batched min), PTv3 encoder (`ptv3_encoder.py`), `torch_cluster` (for PTv3)
- **Voxelization:** `data_vertex_group.py` → `voxelization()` + `voxel_skin()` — computes spatial bone-to-vertex proximity weights as input conditioning
- **Status:** Model weights downloaded (`skin.safetensors`, 1.4 GB). Code not ported. Blocked by `torch_cluster`/PTv3.

#### Subsystem C: Animation Application + FBX Export (bpy-dependent)
- **What it does:** Takes rigged mesh + Mixamo animation FBX → transfers animation → exports animated FBX
- **Key files:** `nodes/unirig/direct_apply_animation.py`, `nodes/unirig/direct_export_fbx.py`
- **What bpy is used for:**
  1. FBX import/export (`bpy.ops.import_scene.fbx`, `bpy.ops.export_scene.fbx`)
  2. Armature creation (edit bones, parent hierarchy, vertex groups)
  3. Skin weight assignment (vertex groups per bone)
  4. Animation transfer (F-curve copy between armatures)
  5. T-pose conversion (SMPL and Mixamo normalization)
  6. Material/texture handling (RGBA→RGB conversion for FBX)
- **Status:** Fully dependent on `bpy`. This is what we need to replace.

### Key Insight: What bpy Actually Does

The `bpy` dependency in ComfyUI-UniRig is doing 6 things. Every single one can be replaced:

| bpy Usage | Replacement | Complexity |
|-----------|-------------|------------|
| FBX import | Not needed — our files are DAE | N/A |
| FBX export | Not needed — we output to our own renderer | N/A |
| Armature creation | Pure data structure (joints + parents + names) | Trivial |
| Skin weight assignment | Numpy array (V × J) — already what UniRig outputs | Trivial |
| Animation transfer | F-curve copy = keyframe arrays per bone per channel | Medium |
| T-pose / Mixamo normalization | Matrix math with numpy (rotation, scaling) | Medium |

**The entire bpy dependency exists because ComfyUI-UniRig's output format is FBX.** If your output goes to your own renderer (which it does — Starfield-2026), you don't need bpy at all. You need the raw data: skeleton, weights, animation transforms.

### Key Insight: The Animation Pipeline

`direct_apply_animation.py` reveals exactly how animation transfer works:

1. **Load model armature** — get bone names from rigged model
2. **Load animation armature** — get bone names + F-curves from animation file
3. **Match bones by name** — `model_bone_names.intersection(anim_bone_names)`
4. **If identical skeletons:** Direct action copy (all F-curves transferred)
5. **If partial match:** Copy only F-curves for matching bones
6. **Scale adjustment:** If armature scales differ, scale location keyframes by ratio

This is pure data manipulation. No 3D rendering needed. The F-curves are just: `bone_name → channel (location/rotation/scale) → array_index (x/y/z/w) → [(frame, value)]`

### Key Insight: Mixamo as the Universal Skeleton

UniRig's `--cls mixamo` predicts a 73-bone Mixamo skeleton on ANY mesh. ComfyUI-UniRig's animation pipeline requires `mixamorig:` prefixed bone names. This means:

- **Any mesh** → UniRig → Mixamo skeleton
- **Any Mixamo animation** → direct F-curve transfer
- **Your game animations** → one-time bone name mapping table → Mixamo convention → done

Mixamo is the "lingua franca" that makes universal rigging work.

---

## 2. What Work Remains

### Phase 1: DAE Parser (unlocks everything)

| Task | Priority | Notes |
|------|----------|-------|
| DAE mesh extraction | **Critical** | `<float_array>` vertices + `<p>` face indices. Stdlib `xml.etree`. Replaces trimesh+pycollada. |
| DAE skeleton extraction | **Critical** | `<node type="JOINT">` hierarchy + `<matrix>` bind poses. Needed for pre-rigged models. |
| DAE animation extraction | **Critical** | `<animation>` channels → bone name + transform type + keyframe arrays. This IS the F-curve data. |
| DAE mesh writer (optional) | Low | Only if you need to output DAE files. Probably not needed since output goes to your renderer. |

### Phase 2: Skin Weight Prediction (the hard one)

| Task | Priority | Notes |
|------|----------|-------|
| Port UniRigSkin model | High | `unirig_skin.py` — BoneEncoder + SkinweightPred + cross-attention. Pure PyTorch except for two deps. |
| Replace `torch_scatter.segment_csr` | High | Used once in `_get_predict()` for batched min. Can replace with `torch.min()` per-batch loop. |
| Replace PTv3 encoder | High | Used for local mesh features. This is the hardest part — PTv3 is a point cloud transformer. Options: (A) write pure-PyTorch PTv3, (B) use Michelangelo for both local+global, (C) use a simpler local encoder. |
| Port `data_vertex_group.py` | Medium | Voxelization + voxel_skin computation. Uses trimesh for voxelization — needs stdlib replacement or numpy-only approach. |

### Phase 3: Animation Retargeting

| Task | Priority | Notes |
|------|----------|-------|
| Bone name mapping tables | High | One dict per game: Sun/Moon bones → Mixamo bones. ~59 entries for Sun/Moon. |
| F-curve transfer engine | High | Port logic from `direct_apply_animation.py` without bpy. Pure numpy: iterate keyframes, match bones, copy transforms. |
| Proportion adjustment | Medium | Scale location keyframes by armature height ratio. Already done in ComfyUI code. |
| T-pose normalization | Medium | Port `_convert_smpl_tpose` and `_normalize_mixamo` from `direct_export_fbx.py`. Matrix math only. |

### Phase 4: Integration

| Task | Priority | Notes |
|------|----------|-------|
| CLI tool | High | `rig.py <model.dae> --animation <clip.dae> --output <result.json>` |
| Renderer integration | High | Feed skeleton + weights + animated transforms into Starfield-2026 renderer |
| Batch processing | Medium | Process multiple models without reloading the 1.4 GB model each time |

---

## 3. Optimizations — Prime Suspects

### Suspect 1: PTv3 Encoder Is the Dependency Bottleneck

The skin weight model requires PTv3 (Point Transformer V3) for local mesh features. PTv3 depends on `torch_cluster` (won't build on Win + CUDA 12.8) and `torch_scatter`. These two packages are the single biggest blocker for a standalone pipeline.

**Fix A: Pure-PyTorch PTv3.** Write a simplified PTv3 that uses standard attention instead of serialized point cloud attention. Loses some accuracy but eliminates the deps.

**Fix B: Skip PTv3, use Michelangelo for both.** The model uses Michelangelo for global features already. Could potentially use it for local features too, but would need retraining.

**Fix C: KNN + MLP.** Replace PTv3's local encoding with a simple KNN graph + shared MLP. Point cloud local features don't need a full transformer.

### Suspect 2: `torch_scatter.segment_csr` (1 Line of Code)

Used exactly once in `unirig_skin.py:331`:
```python
min_coord = torch_scatter.segment_csr(vertices.reshape(-1, 3), idx_ptr, reduce="min")
```
This computes per-batch minimum coordinates. For batch size 1 (inference), it's just `vertices.min(dim=1)`.

**Fix:** Replace with `torch.min()` in a loop over batch dimension. One line fix, zero deps.

### Suspect 3: Voxelization Uses Trimesh

`data_vertex_group.py` calls `trimesh.voxel.creation.voxelize()` to create a voxel grid for bone proximity weights. This adds trimesh as a dependency for the skinning pipeline.

**Fix A: Numpy-only voxelization.** Discretize vertex positions to grid cells, flood-fill. No trimesh needed.

**Fix B: Skip voxelization.** The voxel_skin is a conditioning input. Could try zeros or a simpler distance-based proxy. Quality may degrade.

### Suspect 4: Model Init + Inference Speed

Full pipeline (skeleton + skinning) takes ~30 seconds. Model initialization is ~5 sec each, beam search is ~12 sec.

**Fix A: bf16 default.** Halves VRAM, doubles throughput. Already works.

**Fix B: Greedy decoding.** `num_beams=1` is 15x faster than `num_beams=15`. Quality is slightly lower but often sufficient for humanoid meshes.

**Fix C: Model caching.** Keep models loaded across calls. ComfyUI already does this via `_loaded_models` dict in `direct.py`.

---

## 4. Step-by-Step: Get the Full Pipeline Working

### Step 1: Build the DAE Parser

```
D:\Projects\poc\unirig\dae_parser.py
```

Uses only `xml.etree.ElementTree` (stdlib). Must extract:
- Mesh: vertices (float array), faces (polygon indices), normals
- Skeleton: joint hierarchy (`<node type="JOINT">`), bind matrices
- Animation: channel targets (bone + transform), keyframe times + values

Test with: `D:\Projects\Starfield-2026\src\Starfield2026.Assets\Models\**\*.dae`

### Step 2: Wire DAE Parser into Existing POC

Replace `trimesh.load()` in `poc.py` with the new DAE parser:
```python
from dae_parser import parse_dae
mesh = parse_dae("model.dae")
verts, faces = mesh.vertices, mesh.faces
```

Verify skeleton prediction still works on DAE-loaded meshes.

### Step 3: Port Skin Weight Prediction

Copy `unirig_skin.py` to the POC. Replace:
- `torch_scatter.segment_csr` → `torch.min()` loop
- PTv3 encoder → pure-PyTorch alternative (see Suspect 1)
- Voxelization → numpy-only grid discretization

Test: run `predict_skinning()` on Carl.obj, verify weights are (V, J) shape with valid distributions.

### Step 4: Build Animation Transfer Engine

Port the logic from `direct_apply_animation.py`:
```python
def transfer_animation(model_skeleton, anim_dae, bone_mapping=None):
    """
    model_skeleton: dict with bone names from UniRig
    anim_dae: parsed DAE animation (bone → channel → keyframes)
    bone_mapping: optional dict mapping anim bone names → model bone names
    Returns: dict of bone_name → {channel → [(frame, value)]}
    """
```

No bpy needed. Input is parsed DAE keyframes, output is a dict your renderer can consume.

### Step 5: Integration CLI

```bash
python rig.py model.dae --animation walk.dae --cls mixamo --output result.json
```

Output JSON contains:
```json
{
  "skeleton": {"joints": [...], "parents": [...], "names": [...]},
  "skin_weights": [[...], ...],
  "animation": {
    "frame_count": 60,
    "bones": {
      "mixamorig:Hips": {
        "location": {"x": [...], "y": [...], "z": [...]},
        "rotation": {"w": [...], "x": [...], "y": [...], "z": [...]}
      }
    }
  }
}
```

---

## 5. How to Start/Test

### Test DAE Parser (once built)

```bash
cd D:\Projects\poc\unirig

# Parse a game DAE and print mesh stats:
D:\Projects\poc\.venv\Scripts\python.exe -c "
from dae_parser import parse_dae
result = parse_dae(r'D:\Projects\Starfield-2026\src\Starfield2026.Assets\Models\Aqua Teen\Carl\Carl.dae')
print(f'Vertices: {len(result.vertices)}')
print(f'Faces: {len(result.faces)}')
print(f'Joints: {len(result.skeleton.joints) if result.skeleton else 0}')
print(f'Animations: {len(result.animations)}')
"
```

### Test Skeleton Prediction (already working)

```bash
cd D:\Projects\poc\unirig

# OBJ files work today:
D:\Projects\poc\.venv\Scripts\python.exe poc.py ^
  "D:\Projects\Starfield-2026\src\Starfield2026.Assets\Models\Aqua Teen\Carl\Carl.obj" ^
  --cls mixamo --dtype bf16
```

### Test Skin Weight Prediction (once ported)

```bash
D:\Projects\poc\.venv\Scripts\python.exe -c "
from direct import predict_skinning, predict_skeleton_from_mesh
import numpy as np

# Load mesh
from dae_parser import parse_dae
mesh = parse_dae('model.dae')

# Predict skeleton
skel, norm = predict_skeleton_from_mesh(
    mesh.vertices, mesh.faces,
    'D:/models/Dev/unirig/skeleton.safetensors',
    cls='mixamo', dtype='bf16'
)

# Predict skin weights
weights = predict_skinning(
    mesh.vertices, mesh.normals,
    skel['joints'], skel['parents'],
    'D:/models/Dev/unirig/skin.safetensors',
    faces=mesh.faces, dtype='bf16'
)
print(f'Weights shape: {weights.shape}')  # (V, J)
"
```

### Test Animation Transfer (once built)

```bash
D:\Projects\poc\.venv\Scripts\python.exe -c "
from dae_parser import parse_dae
from animation import transfer_animation

anim = parse_dae('walk_clip.dae')
# ... transfer to rigged model
"
```

---

## 6. Issues & Strategies

### Issue 1: `torch_cluster` / PTv3 Won't Build on Windows

**Symptom:** `pip install torch_cluster` fails with CUDA 13 / torch 2.9 ABI incompatibility. PTv3 requires it for point cloud operations.

**Strategy A: Write a minimal PTv3 replacement.** PTv3's core is serialized attention over point cloud patches. Replace with standard multi-head attention over KNN-grouped points. Loses the serialization optimization but works everywhere. ~200 lines of PyTorch.

**Strategy B: Use a pre-built wheel from conda.** `conda install pyg::torch-cluster` may have compatible builds. Keeps full PTv3 quality but adds conda dependency.

**Strategy C: Train a Michelangelo-only skin model.** The skeleton model uses only Michelangelo (no PTv3). If we could retrain the skin model with Michelangelo for local features too, we'd eliminate the PTv3 dep entirely. Requires access to training data and compute.

**Strategy D: Replace PTv3 with a simple PointNet encoder.** PointNet (shared MLPs + max pooling) gives local features without any exotic deps. Much simpler than PTv3. Quality will be lower but may be sufficient.

### Issue 2: DAE Files Don't Load in Trimesh/Pycollada

**Symptom:** `trimesh.load("model.dae")` returns empty scenes. Game DAE files have geometry but pycollada can't parse it.

**Strategy A: Write custom DAE parser.** Use stdlib `xml.etree.ElementTree`. DAE is XML — vertices are in `<float_array>`, faces in `<p>`, skeleton in `<node type="JOINT">`, animations in `<animation>`. This is the recommended approach — eliminates trimesh AND pycollada.

**Strategy B: Use `pyassimp`.** Wraps the C++ `assimp` library which handles more DAE variants. Adds a native dependency but is battle-tested.

**Strategy C: Parse with Blender headless.** `blender --background --python convert.py` can convert DAE to a simpler format. But this adds Blender as a runtime dependency, which we're trying to avoid.

### Issue 3: Bone Name Mapping Between Game Skeletons and Mixamo

**Symptom:** Game animations use bone names like `Spine1`, `LeftArm` while UniRig predicts `mixamorig:Spine1`, `mixamorig:LeftArm`. Different games use different conventions.

**Strategy A: Strip-prefix matching.** Try removing common prefixes (`mixamorig:`, `Bip01_`, etc.) and match on base name. Works for many conventions.

**Strategy B: Fuzzy matching with fallback.** Use string similarity (Levenshtein, common substring) to match bones. If confidence is low, fall back to position-based matching (nearest joint by distance).

**Strategy C: Hardcoded mapping tables.** One dict per game. Most reliable. Sun/Moon has 59 bones — mapping it once takes 10 minutes and works forever.

**Strategy D: Position-based matching.** Match bones by 3D position proximity rather than name. Compare joint positions between the animation skeleton and the predicted skeleton. Works regardless of naming convention but requires both skeletons to be in similar poses.

### Issue 4: Voxelization Without Trimesh

**Symptom:** `data_vertex_group.py` uses `trimesh.voxel.creation.voxelize()` for the voxel_skin conditioning input to the skinning model.

**Strategy A: Numpy-only voxelization.** Discretize vertex positions: `grid_coords = ((vertices - min) / (max - min) * grid_size).astype(int)`. Flood-fill or boolean occupancy. ~30 lines of numpy.

**Strategy B: Skip voxelization entirely.** Pass zeros for voxel_skin. The model may still produce reasonable weights since the attention mechanism and bone encoder provide the primary signal. Test quality empirically.

**Strategy C: Distance-based proxy.** Instead of voxel occupancy, compute per-vertex distance to each bone segment. Normalize to [0,1] and use as voxel_skin substitute. Captures the same spatial relationship without a voxel grid.

---

## 7. Architecture & New Features

### Target Architecture: Zero-bpy Standalone Pipeline

```
D:\Projects\poc\unirig\
├── rig.py                      # CLI entry point
│   ├── parse_dae(model_path)   # Extract mesh + existing skeleton
│   ├── predict_skeleton()      # UniRig skeleton (if no skeleton exists)
│   ├── predict_skinning()      # UniRig skin weights
│   ├── parse_dae(anim_path)    # Extract animation keyframes
│   ├── transfer_animation()    # Map animation → predicted skeleton
│   └── output JSON/binary      # For renderer consumption
│
├── dae_parser.py               # Stdlib xml.etree DAE parser
│   ├── parse_mesh()            # Vertices, faces, normals, UVs
│   ├── parse_skeleton()        # Joint hierarchy, bind matrices
│   └── parse_animation()       # Channels, keyframes, interpolation
│
├── unirig/                     # ML models (existing + skin port)
│   ├── unirig_ar.py            # Skeleton prediction (working)
│   ├── unirig_skin.py          # Skin weight prediction (to port)
│   ├── michelangelo_encoder.py # Mesh encoder (working)
│   └── ...
│
├── comfy/                      # Shim package (existing, ~80 lines)
│
├── animation.py                # Animation transfer engine
│   ├── match_bones()           # Name matching + mapping tables
│   ├── transfer_fcurves()      # Copy keyframes between skeletons
│   └── adjust_proportions()    # Scale transforms for size differences
│
└── bone_maps/                  # Bone name mapping tables
    ├── sunmoon.py              # Sun/Moon → Mixamo (59 bones)
    ├── scarlet.py              # Scarlet → Mixamo
    └── mixamo.py               # Mixamo identity (passthrough)
```

### Dependency Comparison

| Dependency | ComfyUI-UniRig | Standalone Target | Notes |
|------------|---------------|-------------------|-------|
| `torch` | Yes | Yes | Unavoidable for ML inference |
| `transformers` | Yes | Yes | OPT-350m model loading |
| `safetensors` | Yes | Yes | Weight file loading |
| `numpy` | Yes | Yes | Array math |
| `einops` | Yes | Yes | Tensor reshaping in model code |
| `bpy` | Yes | **No** | Replaced by DAE parser + data structures |
| `trimesh` | Yes | **No** | Replaced by DAE parser |
| `pycollada` | Yes (transitive) | **No** | Replaced by DAE parser |
| `torch_cluster` | Yes | **No** | Replaced by pure-PyTorch FPS fallback |
| `torch_scatter` | Yes | **No** | Replaced by torch.min() loop |
| PTv3 | Yes | **TBD** | Needs pure-PyTorch replacement or alternative |
| `PIL` | Yes | **No** | Was for RGBA→RGB texture conversion for FBX |
| ComfyUI | Yes | **No** | Fully shimmed |

**Target: 5 pip packages** (`torch`, `transformers`, `safetensors`, `numpy`, `einops`) vs ComfyUI-UniRig's 12+.

### Quick Win 1: DAE Mesh Parser (2 hours)

Write `dae_parser.py` using stdlib `xml.etree`. Start with mesh-only extraction:
- Find `<mesh>` elements
- Extract `<float_array>` for positions, normals
- Extract `<triangles>` or `<polylist>` `<p>` for face indices
- Handle `<source>` → `<input>` offset indirection

This immediately unblocks skeleton prediction on game DAE files.

### Quick Win 2: bf16 + Greedy Defaults (5 minutes)

Change `poc.py` defaults:
```python
parser.add_argument("--dtype", default="bf16")  # was fp32
# Add --fast flag:
parser.add_argument("--fast", action="store_true")
# In generation: num_beams = 1 if args.fast else 15
```

2x faster inference from bf16, optionally 15x faster with greedy decoding.

### Quick Win 3: `torch_scatter` Elimination (10 minutes)

In `unirig_skin.py`, replace:
```python
min_coord = torch_scatter.segment_csr(vertices.reshape(-1, 3), idx_ptr, reduce="min")
```
With:
```python
min_coord = torch.stack([vertices[i].min(dim=0).values for i in range(B)])
```

One import eliminated, one line changed.

### Quick Win 4: Bone Name Mapping for Sun/Moon (30 minutes)

Write `bone_maps/sunmoon.py` — a dict mapping the 59 Sun/Moon bone names to Mixamo equivalents. Cross-reference the bone names from `tools/blender/game_rig_builder/skeletons.py` against Mixamo's standard 65-bone naming.

---

## 8. Key Files Reference

| File | Location | Purpose |
|------|----------|---------|
| `poc.py` | `D:\Projects\poc\unirig\` | Working skeleton prediction CLI |
| `direct.py` | `D:\Projects\ComfyUI-UniRig\nodes\unirig\` | Full pipeline reference (skeleton + skinning + caching) |
| `unirig_skin.py` | `D:\Projects\ComfyUI-UniRig\nodes\unirig\` | Skin weight model — needs porting |
| `direct_apply_animation.py` | `D:\Projects\ComfyUI-UniRig\nodes\unirig\` | Animation transfer — port logic, drop bpy |
| `direct_export_fbx.py` | `D:\Projects\ComfyUI-UniRig\nodes\unirig\` | FBX export + T-pose + Mixamo normalization — port math only |
| `data_vertex_group.py` | `D:\Projects\ComfyUI-UniRig\nodes\unirig\` | Voxelization + voxel_skin — needs trimesh-free port |
| `skinning.py` | `D:\Projects\ComfyUI-UniRig\nodes\` | ComfyUI skinning node — shows full data flow |
| `skeleton.safetensors` | `D:\models\Dev\unirig\` | 1.4 GB skeleton model weights |
| `skin.safetensors` | `D:\models\Dev\unirig\` | 1.4 GB skinning model weights |
| `skeletons.py` | `tools\blender\game_rig_builder\` | Sun/Moon 59-bone data for mapping reference |

### Key Insights

1. **bpy is only used for FBX I/O and armature manipulation.** Since we use DAE (not FBX) and output to our own renderer (not Blender), the entire bpy dependency is unnecessary. Every operation it performs is either data parsing (replace with DAE parser) or matrix math (replace with numpy).

2. **The animation transfer is simpler than it looks.** `direct_apply_animation.py` is 389 lines, but the core logic is: match bone names → copy F-curves → scale locations. The rest is bpy ceremony (scene cleanup, material fixing, FBX export settings). Without bpy, the transfer engine is ~50 lines.

3. **Mixamo naming is the key to universal animation.** UniRig's `--cls mixamo` predicts a standardized 73-bone skeleton on any mesh. If all animations are mapped to Mixamo naming (one-time mapping per game), then ANY mesh + ANY animation "just works" through UniRig.

4. **PTv3 is the last hard dependency.** Skeleton prediction uses only Michelangelo (already ported). Skinning uses PTv3 + Michelangelo. Replacing PTv3 with a simpler encoder is the single hardest remaining task. Everything else is straightforward porting.

5. **The skinning model's voxel_skin input is a spatial prior, not a hard requirement.** It conditions the attention weights with bone proximity information. Passing a simpler distance-based proxy (or even zeros) may produce acceptable results. Worth testing before investing in a full voxelization port.

6. **ComfyUI-UniRig's T-pose normalization is valuable reference code.** `direct_export_fbx.py` contains SMPL T-pose conversion and Mixamo normalization (scale to 1.7m, feet at ground, Y-up conversion, bone roll alignment). This math is pure numpy/matrix operations — port it directly without bpy.
