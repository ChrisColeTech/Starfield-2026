# 29 - UniRig POC & Game Rig Builder: Lessons Learned Handoff

**Date:** 2026-02-25
**Scope:** Standalone UniRig skeleton prediction POC, Game Rig Builder Blender addon (visible bones, bone collections, animation loading)
**Status:** UniRig POC functional — predicts skeletons from OBJ meshes. Game Rig Builder functional — generates upright Sun/Moon rig with visible bones, organized collections, animation playback.

---

## 1. What We Accomplished

### UniRig Standalone POC (`D:\Projects\poc\unirig\`)

Ported UniRig's ML skeleton prediction pipeline from ComfyUI into a standalone script with zero ComfyUI dependencies.

- **Comfy shim package** — 6 files replacing `comfy.ops`, `comfy.utils`, `comfy.model_management`, `comfy.model_patcher`, `comfy.ldm.modules.attention` with pure PyTorch equivalents (nn.Linear/LayerNorm, safetensors loader, F.scaled_dot_product_attention)
- **13 source files** copied from `D:\Projects\ComfyUI-UniRig\nodes\unirig\`, 2 modified:
  - `parse_encoder.py` — removed ptv3_encoder import (not needed for skeleton-only inference)
  - `model_parse.py` — removed unirig_skin import (skin model not used in POC)
- **Pure-PyTorch FPS fallback** — replaced `torch_cluster.fps` (fails to build on Win + CUDA 12.8 + torch 2.9) with a greedy farthest-point-sampling in `michelangelo_encoder.py`
- **`poc.py`** — ~150 line CLI script: loads mesh via trimesh, normalizes to [-1,1], samples 2048 surface points, runs OPT-350m + Michelangelo encoder, prints joint hierarchy with denormalized world positions
- **Test results on Carl.obj:** 34 joints with `articulationxl`, 73 joints with Mixamo naming (`mixamo` cls)

### Game Rig Builder Addon (`tools/blender/game_rig_builder/`)

Blender 4.0 addon that generates armatures from hardcoded game bone data.

- **Visible bones** — stub tails (0.01 units from Collada import) extended toward first child bone; leaf bones extend along parent direction; MAX_TAIL=30 cap prevents root bone stretching to ground
- **Organized bone collections** — 9 body-part groups (Root, Torso, Head, Left/Right Arm, Left/Right Fingers, Left/Right Leg) plus "Other" catchall. Assignment done in object mode via `arm_data.bones` (edit mode assignment silently fails)
- **Y-up → Z-up rotation** — `rotation_euler[0] = radians(90)` matches what Blender's Collada importer applies. Bone data stays in Y-up coordinates; the object-level rotation handles display.
- **Animation loading** — imports DAE clip, matches bones by name, transfers action to rig. 59/62 bones match for Sun/Moon clips. Frame range auto-set.
- **Animation unloading** — clears action, resets all pose bone transforms

### Model Downloads

UniRig safetensors models at `D:\models\Dev\unirig\`:
- `skeleton.safetensors` (1.4 GB) — OPT-350m + Michelangelo encoder for joint prediction
- `skin.safetensors` (1.4 GB) — PTv3 + Michelangelo for skinning weights (not used in POC)

---

## 2. What Work Remains

### UniRig POC

| Task | Priority | Notes |
|------|----------|-------|
| DAE mesh loading | High | `trimesh` + `pycollada` loads DAE files as empty scenes. Game trainer DAE files don't load. OBJ works fine. Need to either fix pycollada parsing or export OBJ from Blender. |
| Skin weight prediction | Medium | `skin.safetensors` downloaded but not integrated. Requires PTv3 encoder + `torch_cluster` (won't build). Would need pure-PyTorch PTv3 replacement. |
| Blender integration | Medium | Feed UniRig output back into Blender — create armature from predicted joints, assign predicted skin weights to mesh. |
| `articulationxl` bone naming | Low | The `articulationxl` cls produces generic `bone_N` names because it's not in the skeleton naming config. Mixamo/VRoid cls produce proper names. |
| Performance | Low | Full fp32 inference takes ~15 sec on GPU. Could try fp16/bf16 for 2-3x speedup. |

### Game Rig Builder

| Task | Priority | Notes |
|------|----------|-------|
| Fit Rig to Model | High | Operator stub exists but not implemented. Should scale/position generated rig to match imported mesh proportions. |
| Scarlet skeleton data | Medium | Need to dump bone data from Scarlet trainer DAE files into `skeletons.py` |
| PZLA skeleton data | Medium | Same for Legends: Arceus |
| README update | Low | README still says "bones are only visible in Stick display mode as dots" — no longer true |

---

## 3. Optimizations — Prime Suspects

### Suspect 1: Model Initialization Is Slow (~5 sec)

`AutoModelForCausalLM.from_config()` creates the full OPT-350m transformer on CPU, then it gets moved to GPU. HuggingFace's model factory does significant validation and config resolution. Every run re-initializes from scratch.

**Fix:** Cache the initialized model in a pickle/checkpoint after first run. Or use `torch.compile()` with `mode='reduce-overhead'` for repeated inference.

### Suspect 2: Beam Search with 15 Beams Is Expensive

The generation config uses `num_beams=15`, `do_sample=True`, `top_k=5`. Beam search with 15 beams means 15x forward passes per token. For 200+ tokens, that's 3000+ forward passes.

**Fix:** Try `num_beams=1` (greedy) or `num_beams=3` first. Quality may be acceptable with fewer beams. The `VocabSwitchingLogitsProcessor` already constrains outputs heavily.

### Suspect 3: FPS Fallback Uses O(N*K) Distance Computation

The pure-PyTorch FPS replacement calls `torch.cdist` per selected point. With 4096 input points and ratio=0.25 → 1024 FPS iterations, each computing distances to all points.

**Fix:** Use batch cdist once and update iteratively, or switch to random subsampling when `torch_cluster` is unavailable. The original code uses CUDA-accelerated FPS which is 10-100x faster.

### Suspect 4: Full fp32 for 1.4GB Model

The model uses fp32 by default. With 350M parameters at 4 bytes each = 1.4 GB VRAM. Attention and FFN computations are all fp32.

**Fix:** Run with `--dtype bf16`. The model was likely trained in bf16. This halves VRAM and doubles throughput on modern GPUs. Already supported via CLI arg.

---

## 4. Step-by-Step: Get App Fully Working

### Step 1: Set Up Python Environment

```bash
cd D:\Projects\poc\unirig
# Use existing venv
D:\Projects\poc\.venv\Scripts\python.exe -m pip install trimesh pycollada safetensors transformers einops
```

Required packages: `torch` (2.9+), `transformers`, `safetensors`, `trimesh`, `pycollada`, `numpy`, `einops`.

**Not required:** `torch_cluster` (replaced with pure-PyTorch FPS fallback), `flash_attn` (auto-disabled, uses SDPA).

### Step 2: Verify Model Weights Exist

```bash
ls D:\models\Dev\unirig\
# Should show: skeleton.safetensors (1.4 GB), skin.safetensors (1.4 GB)
```

If missing, download:
```bash
pip install huggingface_hub
python -c "from huggingface_hub import hf_hub_download; hf_hub_download('apozz/UniRig-safetensors', 'skeleton.safetensors', local_dir='D:/models/Dev/unirig')"
```

### Step 3: Run Skeleton Prediction

```bash
cd D:\Projects\poc\unirig
D:\Projects\poc\.venv\Scripts\python.exe poc.py "path/to/mesh.obj"

# With Mixamo naming:
D:\Projects\poc\.venv\Scripts\python.exe poc.py "path/to/mesh.obj" --cls mixamo

# With bf16 for speed:
D:\Projects\poc\.venv\Scripts\python.exe poc.py "path/to/mesh.obj" --dtype bf16
```

### Step 4: Install/Test Game Rig Builder

```
1. Copy game_rig_builder/ to %APPDATA%/Blender Foundation/Blender/4.0/scripts/addons/
2. Enable in Blender: Edit > Preferences > Add-ons > "Game Rig Builder"
3. In 3D Viewport sidebar > GameRig tab:
   - Select "Sun/Moon" game
   - Click "Generate Rig" → 59-bone armature appears upright
   - Click "Load Animation" → browse to a Sun/Moon clip DAE
   - Press Space to play animation
```

---

## 5. How to Start/Test

### UniRig POC Quick Test

```bash
cd D:\Projects\poc\unirig

# Test with an OBJ file (DAE files don't load via trimesh):
D:\Projects\poc\.venv\Scripts\python.exe poc.py ^
  "D:\Projects\Starfield-2026\src\Starfield2026.Assets\Models\Aqua Teen\Carl\Carl.obj"
```

Expected output: table of ~30-70 predicted joints with positions and parent hierarchy.

### Import Test (Verify Shims Work)

```bash
cd D:\Projects\poc\unirig
D:\Projects\poc\.venv\Scripts\python.exe -c "
from unirig.configs import AR_MODEL_CONFIG; print('configs OK')
from unirig.model_parse import get_model; print('model_parse OK')
from unirig.tokenizer_parse import get_tokenizer; print('tokenizer OK')
import comfy.ops; print('comfy.ops OK')
import comfy.ldm.modules.attention; print('attention OK')
"
```

### Game Rig Builder Test (Blender)

1. Open Blender 4.0
2. Enable addon (if not already)
3. GameRig panel → Generate Rig
4. Verify: 59 bones, organized collections visible in Properties > Bone Collections, rig standing upright

---

## 6. Issues & Strategies

### Issue 1: DAE Files Don't Load in Trimesh

**Symptom:** `trimesh.load("model.dae")` returns a Scene with 0 geometries. Game trainer DAE files produced by MiniToolbox have geometry, but `pycollada` can't parse it.

**Root cause:** Game DAE files may use features or namespace quirks that pycollada doesn't handle. Blender's Collada importer (C++ based) handles them fine.

**Strategy A: Pre-convert to OBJ.** Use Blender's Python API in batch mode to convert DAE → OBJ before running UniRig. One-liner: `blender --background --python convert_dae_to_obj.py -- input.dae output.obj`

**Strategy B: Use `collada2gltf` or `assimp`.** Install `pyassimp` and load via `assimp` which handles more DAE variants. Or convert DAE to GLTF first.

**Strategy C: Parse DAE XML directly.** The mesh data is in `<float_array>` elements. A simple XML parser can extract vertices and face indices without a full Collada library. We already have `dump_dae_bones.py` that parses DAE XML.

### Issue 2: `torch_cluster` Won't Build on Windows

**Symptom:** `pip install torch_cluster` fails with CUDA 13 / torch 2.9 ABI incompatibility (`std: ambiguous symbol` in nvcc compilation).

**Root cause:** Pre-built wheels for `torch_cluster` lag behind latest PyTorch releases. The package requires CUDA compilation with matching torch headers.

**Strategy A: Use the pure-PyTorch FPS fallback.** Already implemented. ~10x slower than CUDA FPS but works everywhere.

**Strategy B: Install from conda.** `conda install pyg::torch-cluster` may have compatible builds.

**Strategy C: Replace FPS with random subsampling.** For inference (not training), random subsampling of 1024 points from 4096 may produce comparable results. Skip FPS entirely.

### Issue 3: `articulationxl` Produces Generic Bone Names

**Symptom:** Using `--cls articulationxl` gives `bone_0`, `bone_1`, etc. instead of anatomical names like `Hips`, `Spine`.

**Root cause:** The `SKELETONS` config in `configs.py` only has entries for `vroid` and `mixamo`. The `articulationxl` class is a training-time aggregation class that doesn't have its own naming template.

**Strategy:** Use `--cls mixamo` for Mixamo-style naming or `--cls vroid` for VRoid naming. The joint positions are the same regardless of cls — only the naming differs. Could also add a custom `articulationxl` skeleton naming config.

### Issue 4: Blender Bone Collection Assignment Fails in Edit Mode

**Symptom:** Creating bone collections and assigning bones in edit mode results in 0 bones in the collection.

**Root cause:** Blender 4.0's `BoneCollection.assign()` requires pose/object-mode `Bone` objects, not edit-mode `EditBone` objects. The API accepts EditBones without error but silently fails.

**Strategy:** Always switch to object mode before collection assignment. Use `arm_data.bones.get(name)` (object mode) not `arm_data.edit_bones.get(name)` (edit mode).

---

## 7. Architecture & New Features

### UniRig POC Architecture

```
D:\Projects\poc\unirig\
├── poc.py                    # CLI entry point (150 lines)
│   ├── Load mesh (trimesh)
│   ├── Normalize to [-1, 1]
│   ├── Sample 2048 surface points + normals
│   ├── Build tokenizer (TokenizerConfig → TokenizerPart)
│   ├── Build model  (AR_MODEL_CONFIG → UniRigAR)
│   │   ├── OPT-350m transformer (HuggingFace AutoModelForCausalLM)
│   │   └── Michelangelo mesh encoder (Perceiver cross-attention)
│   ├── Load safetensors weights
│   ├── Generate skeleton (autoregressive token prediction)
│   │   ├── Encode mesh → 512 latent tokens
│   │   ├── Prepend [BOS, CLS] tokens
│   │   ├── Beam search with VocabSwitchingLogitsProcessor
│   │   └── Detokenize → joints (x,y,z), parents, names
│   └── Print denormalized joint table
│
├── unirig/                   # Vendored model code (no edits except 2 files)
│   ├── unirig_ar.py          # UniRigAR model (OPT + encoder + output proj)
│   ├── michelangelo_encoder.py  # Perceiver encoder (Fourier + cross-attn + self-attn)
│   ├── attention.py           # SDPA attention dispatch
│   ├── configs.py             # Inlined model/tokenizer/skeleton configs
│   ├── tokenizer_part.py      # Token → joint detokenization
│   ├── parse_encoder.py       # MODIFIED: removed ptv3 import
│   ├── model_parse.py         # MODIFIED: removed skin model import
│   └── ... (8 more unchanged files)
│
└── comfy/                    # Shim package (6 new files, ~80 lines total)
    ├── ops.py                # disable_weight_init → nn.Linear/LayerNorm
    ├── utils.py              # load_torch_file → safetensors.torch.load_file
    ├── model_management.py   # get_torch_device → torch.device('cuda')
    ├── model_patcher.py      # ModelPatcher → passthrough wrapper
    └── ldm/modules/attention.py  # optimized_attention → F.scaled_dot_product_attention
```

### Game Rig Builder Architecture

```
tools/blender/game_rig_builder/
├── __init__.py         # bl_info, register/unregister
├── skeletons.py        # Hardcoded bone data: SUNMOON (59 bones)
│                       #   Format: (name, (hx,hy,hz), (tx,ty,tz), roll, parent_name)
│                       #   Data is Y-up (matching Collada import)
├── ui.py               # All operators + panel
│   ├── GAMERIG_OT_generate      # Creates armature from bone data
│   │   ├── Edit mode: create bones, set parents
│   │   ├── Extend tails toward children (MAX_TAIL=30)
│   │   ├── Object mode: assign bone collections by body part
│   │   └── Rotate 90° X for Z-up display
│   ├── GAMERIG_OT_load_model    # Collada DAE import
│   ├── GAMERIG_OT_fit_rig       # STUB — not implemented
│   ├── GAMERIG_OT_load_animation # Transfer action from imported DAE clip
│   ├── GAMERIG_OT_unload_animation # Clear action, reset pose
│   └── GAMERIG_OT_reset_view    # Front orthographic
└── README.md
```

### Quick Win 1: DAE → OBJ Batch Converter (15 min)

Write a Blender batch script that converts all trainer DAE files to OBJ for UniRig consumption:

```python
# convert_all_dae.py — run with blender --background --python
import bpy, glob, os
for dae in glob.glob("trainers/**/model.dae"):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.wm.collada_import(filepath=dae)
    obj_path = dae.replace('.dae', '.obj')
    bpy.ops.wm.obj_export(filepath=obj_path)
```

### Quick Win 2: UniRig → Blender Pipeline (30 min)

Add a `--output json` flag to `poc.py` that writes joints/parents/names to a JSON file. Then write a Blender operator that reads this JSON and creates an armature from UniRig's predictions. This connects the ML skeleton to the Game Rig Builder workflow.

### Quick Win 3: bf16 Default (5 min)

Change `poc.py` default dtype from `fp32` to `bf16`. The model works correctly in bf16 (tested in ComfyUI) and runs 2x faster with half the VRAM.

### Quick Win 4: Greedy Decoding Option (5 min)

Add `--fast` flag that sets `num_beams=1, do_sample=False`. Greedy decoding is 15x faster than beam search. Quality is slightly lower but often adequate for humanoid meshes.

---

## 8. Key Files Reference

| File | Location | Purpose |
|------|----------|---------|
| `poc.py` | `D:\Projects\poc\unirig\` | Standalone UniRig inference script |
| `comfy/ops.py` | `D:\Projects\poc\unirig\comfy\` | nn.Linear/LayerNorm shim |
| `comfy/ldm/modules/attention.py` | `D:\Projects\poc\unirig\comfy\` | SDPA attention shim |
| `unirig/michelangelo_encoder.py` | `D:\Projects\poc\unirig\unirig\` | Perceiver encoder + FPS fallback |
| `skeleton.safetensors` | `D:\models\Dev\unirig\` | 1.4 GB model weights |
| `ui.py` | `tools\blender\game_rig_builder\` | Blender addon operators |
| `skeletons.py` | `tools\blender\game_rig_builder\` | Sun/Moon 59-bone data |
| `check_deps.py` | `D:\Projects\poc\unirig\` | Dependency checker script |

### Key Insights

1. **ComfyUI's `comfy.ops` is just nn.Linear/LayerNorm with dtype awareness.** The shim is 10 lines. The entire comfy dependency chain collapses to ~80 lines of pure PyTorch.

2. **UniRig's OPT-350m is genuinely small.** 1.4 GB safetensors, loads in ~3 sec, generates a full skeleton in ~12 sec. This is practical for real-time tooling, not just research.

3. **The autoregressive tokenization is clever.** Joints are encoded as 3 discretized coordinates (256 bins × 3 axes). Branch tokens indicate hierarchy splits. The `VocabSwitchingLogitsProcessor` constrains the output to only valid token sequences — the model can't produce malformed skeletons.

4. **Bone collection assignment in Blender 4.0 is mode-sensitive.** Edit mode `EditBone` objects can't be assigned to collections. Must use object mode `Bone` objects. No error is raised — it just silently does nothing.

5. **Collada Y-up bone data should NOT be coordinate-swapped.** The bone positions are correct as-is. Blender handles the Y→Z display via an object-level 90° X rotation. Manually swapping Y/Z corrupts the bone orientations and breaks animation playback.
