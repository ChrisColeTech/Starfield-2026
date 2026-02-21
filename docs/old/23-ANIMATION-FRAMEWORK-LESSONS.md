# 23 - Animation Framework & Manifest Unification Lessons

## Summary

This document covers the tag-based animation framework, semantic manifest generation across all three CLIs, and the unification of manifest formats between OhanaCli, SpicaCli, and Spica.Registry so that BgEditor can read manifests from any exporter. It captures what was accomplished, critical issues discovered during review, and strategies for getting the full pipeline working end-to-end.

---

## 1. What We Accomplished

### Tag-Based Animation Framework (3 files)

Replaced brittle hardcoded clip key lookups (`FindClip(set, "Motion_0", "clip_000", "anim_0")`) with a semantic tag system where animations are resolved by purpose ("Idle", "Walk", "Run", "Jump") rather than exporter-specific naming.

| File | Change |
|------|--------|
| `Core/Rendering/Skeletal/AnimationController.cs` | **New.** High-level wrapper: `Play("Idle")`, `HasClip("Jump")`, `Update(dt)`, exposes `SkinPose` |
| `Core/Rendering/Skeletal/SplitModelAnimationSet.cs` | Added `ClipsByTag` dictionary (case-insensitive). Loader reads `SemanticName` from manifest, falls back to `InferTag` for old manifests |
| `3D/Game1.cs` | Replaced `_animator`/`_animationSet`/`_activeClip`/`FindClip`/`ResolveMovementClip` with single `_animController` field |

### Semantic Naming in All Three CLIs

All exporters now write `semanticName` and `semanticSource` fields on clip entries, using shared slot-map logic:

| CLI | Semantic Changes |
|-----|-----------------|
| **OhanaCli** (`OhanaCli.App/Program.cs`) | `ResolveSemanticMetadata` with `AnimAssetType` enum, separate `MapOverworldSlot`/`MapPokemonSlot`, `ParseSourceAnimIndex` |
| **SpicaCli** (`SpicaCli/Program.cs`) | Same slot maps and detection logic |
| **Spica.Registry** (`Spica.Registry/Program.cs`) | Same slot maps and detection logic |

### Manifest Format Unification

All three CLIs now generate the **same** manifest structure:

```json
{
  "version": 1,
  "mode": "split-model-anims",
  "textures": ["textures/tex.png"],
  "models": [{
    "name": "model",
    "modelFile": "model.dae",
    "clips": [{
      "index": 0,
      "id": "clip_000",
      "name": "clip_000",
      "sourceName": "Motion_0",
      "semanticName": "Idle",
      "semanticSource": "slot-map-v1",
      "file": "clips/clip_000.dae",
      "frameCount": 40,
      "fps": 30
    }]
  }]
}
```

This matches BgEditor's TypeScript interface (`frontend/src/types/animation.ts: SplitManifest`) exactly. Previously SpicaCli/Registry generated an incompatible format (top-level `clips[]`, missing `id`/`sourceName`/`mode`, textures as objects instead of strings).

### BgEditor Animation Editor (already existed)

The animation editor UI was already built in a prior session:
- `frontend/src/pages/AnimationsPage.tsx` — clip list, playback controls, tag editor dropdown
- `frontend/src/store/animationEditorStore.ts` — loads manifest via `/api/manifests/read`, tag/save/auto-tag actions
- `frontend/src/types/animation.ts` — `SplitManifest` interface + `SEMANTIC_TAGS` constant
- `backend/src/routes/manifests.ts` — `/api/manifests/read` and `/api/manifests/save` endpoints

---

## 2. What Work Remains

### Critical: On-Disk Assets Are Stale

**This is the most important finding.** The manifests and clip files currently on disk do NOT match what the updated code generates:

| Folder | Exporter | Clips on disk? | Manifest format | SemanticName? |
|--------|----------|---------------|-----------------|---------------|
| `tr0001_00/` | Spica (old) | 16 clip DAEs | Old (top-level `clips[]`, no `id`/`sourceName`) | No |
| `tr0001_00_fi/` | OhanaCli (old) | **None** — `Clips: []` | Old (PascalCase, empty clips) | No |
| `tr0002_00_fi/` ... `tr0018_00_fi/` | OhanaCli (old) | **None** | Old | No |

**Impact:** The 3D POC currently loads `tr0001_00_fi` as default, which has **zero clip DAEs**. The animation framework silently returns `false` from `Play()` — the character renders but never animates. Only `tr0001_00` (Spica export) has clips, but its manifest uses the old format without semantic names.

**Fix:** Re-export all characters with the updated CLIs, or change the default to `tr0001_00` as an interim fix.

### Remaining Tasks

- **Re-export overworld characters** with updated OhanaCli (with `--split-model-anims` flag) to generate clip DAEs and semantic manifests
- **Re-export `tr0001_00`** with updated SpicaCli to get new manifest format
- **Animation crossfade** — Walk↔Run transitions are instant; needs pose blending in `SkeletalAnimator`
- **Visual clip identification** — Most slots still have generic names ("ShortAction1", "Action7"); need BgEditor preview to identify what they actually do
- **Pokemon battle animation slots** — Only slot 0 ("Idle") is mapped; rest unknown
- **BgEditor auto-tag** only maps 4 slots (0=Idle, 1=Walk, 2=Run, 4=Jump); should match the full overworld slot map from the CLIs

---

## 3. Optimizations — Prime Suspects

### 3.1 Re-export Once, Not Per Session
**Impact: High** — Right now there are no clip DAEs for the `_fi` characters. Every time someone clones the repo and runs the 3D POC, they get a static character. The export step should be documented and ideally run as a one-time build script.

**Approach:** Add a `scripts/export-overworld.sh` that runs OhanaCli against the GARC with `--split-model-anims` and outputs to `PokemonGreen.Assets/Pokemon3D/characters/overworld/`. Commit the generated assets.

### 3.2 Lazy Clip Loading
**Impact: Medium** — `SplitModelAnimationSetLoader.Load()` parses ALL clip DAEs eagerly. For characters with 16+ clips, this means 16 COLLADA XML parses on character switch. Most clips (emotes, sitting, actions) are never played.

**Approach:** Store clip paths in the dictionary but defer `ColladaSkeletalLoader.LoadClip()` until first `AnimationController.Play()`. Add a `LoadClipOnDemand` wrapper in the animation set.

### 3.3 Buffer Reuse in SkinnedDaeModel
**Impact: Medium** — `UpdatePose()` recreates `VertexBuffer`/`IndexBuffer` every frame. Allocate `DynamicVertexBuffer` once during `Load()`, call `SetData()` per frame.

### 3.4 Texture Cache Across Characters
**Impact: Low-Medium** — Characters share rim textures (`Chara_Rim_1_fi.png`, `Chara_Rim_Black_fi.png`). A static `Dictionary<string, Texture2D>` cache would eliminate redundant disk reads and GPU uploads on character switch.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
- .NET 9.0 SDK
- Node.js 18+ (for BgEditor)
- MonoGame 3.8 (pulled via NuGet automatically)
- Character assets exported to `src/PokemonGreen.Assets/Pokemon3D/characters/overworld/`

### Step 1: Re-export characters (REQUIRED for animations)

The on-disk `_fi` exports have no clip DAEs. You must re-export:

```bash
# OhanaCli — export overworld characters with split animations
cd D:\Projects\PokemonGreen\src\PokemonGreen.OhanaCli
dotnet run --project src/OhanaCli.App -- <path-to-bch-file> --split-model-anims -o <output-dir>
```

Or use SpicaCli against the GARC:
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.Spica\SpicaCli
dotnet run -- split <garc-file> -o D:\Projects\PokemonGreen\src\PokemonGreen.Assets\Pokemon3D\characters\overworld
```

### Step 2: Verify manifest format

After re-export, check that `manifest.json` has:
- `"mode": "split-model-anims"` (not `"pokemonId"`)
- Clips nested under `models[0].clips[]` (not top-level)
- Each clip has `id`, `name`, `sourceName`, `semanticName`, `file`
- `textures` is a flat string array (not objects)

### Step 3: Build and run the 3D POC

```bash
cd D:\Projects\PokemonGreen
dotnet build src/PokemonGreen.3D/PokemonGreen.3D.csproj
dotnet run --project src/PokemonGreen.3D/PokemonGreen.3D.csproj
```

### Step 4: Verify animations work

1. Character renders on grid floor — should be playing idle animation
2. WASD moves → walk animation; Shift+WASD → run animation
3. Space → jump (only on characters with Jump clip, e.g. tr0001)
4. Tab → character select overlay; switch characters; animations continue

### Step 5: Test BgEditor animation tagging (optional)

```bash
# Terminal 1 — backend
cd D:\Projects\PokemonGreen\src\PokemonGreen.BgEditor\backend
npm run dev

# Terminal 2 — frontend
cd D:\Projects\PokemonGreen\src\PokemonGreen.BgEditor\frontend
npm run dev
```

Open `http://localhost:5173`, navigate to Animations page, paste a character folder path (e.g. `D:\Projects\PokemonGreen\src\PokemonGreen.Assets\Pokemon3D\characters\overworld\tr0001_00`), click Load. The clip list should appear with semantic tags. You can manually tag clips and save.

---

## 5. How to Start/Test the APIs

### 3D POC (MonoGame)
```bash
dotnet run --project src/PokemonGreen.3D/PokemonGreen.3D.csproj
```
No API server — self-contained desktop app. Reads manifests directly from disk.

### BgEditor Backend (Fastify on port 3001)
```bash
cd src/PokemonGreen.BgEditor/backend
npm install   # first time only
npm run dev   # starts on http://localhost:3001
```

Key endpoints:
| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/manifests/read?dir=<path>` | Read manifest.json from a folder |
| POST | `/api/manifests/save` | Write updated manifest back (body: `{dir, manifest}`) |
| GET | `/api/manifests?dir=<path>` | Scan directory tree for all manifests |
| POST | `/api/manifests/generate` | Auto-generate manifests for model folders |
| GET | `/serve/<base64-dir-token>/<filename>` | Serve model/texture files to Three.js |

### BgEditor Frontend (Vite on port 5173)
```bash
cd src/PokemonGreen.BgEditor/frontend
npm install   # first time only
npm run dev   # starts on http://localhost:5173
```

### OhanaCli
```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App -- <bch-file> --split-model-anims -o <output-dir>
```

### SpicaCli
```bash
dotnet run --project src/PokemonGreen.Spica/SpicaCli -- split <garc-file> -o <output-dir>
```

---

## 6. Known Issues & Strategies

### Issue 1: Stale Assets — No Clip DAEs for _fi Characters

**Problem:** OhanaCli `_fi` exports on disk were generated before the split-animation feature was added. `Clips: []` in manifest, no DAE files in `clips/` folder. Characters load but never animate.

**Strategy A (Quick fix):** Change Game1.cs default character from `tr0001_00_fi` to `tr0001_00` (Spica export that has clips). Update `Characters` array to reference available animated exports.

**Strategy B (Proper fix):** Re-run OhanaCli with `--split-model-anims` on all 18 characters. Commit the generated clip DAEs and updated manifests.

**Strategy C (Hybrid):** Re-export one character with OhanaCli to validate the pipeline, then batch-export the rest.

### Issue 2: Old Spica Manifest Format on Disk

**Problem:** The existing `tr0001_00/manifest.json` uses the old format (top-level `clips[]`, no `id`/`sourceName`/`mode`). The runtime loader handles this via fallback, but BgEditor's TypeScript interface expects the new format and will fail to display clips correctly.

**Strategy:** Re-export `tr0001_00` with updated SpicaCli. The new code generates the unified format. Alternatively, write a one-time migration script that restructures old manifests in-place.

### Issue 3: InferTag Only Covers 3 Animations

**Problem:** The `InferTag` fallback in `SplitModelAnimationSetLoader` only maps index 0→Idle, 1→Walk, 2→Run. For old manifests without `semanticName`, clips like Jump (slot 4) are invisible to the animation framework.

**Strategy:** This is by design — `InferTag` is a minimal safety net. The real fix is ensuring all manifests have `semanticName` via re-export or BgEditor tagging. Don't expand `InferTag` — it would just mask the problem.

### Issue 4: BgEditor Auto-Tag Is Minimal

**Problem:** `animationEditorStore.ts` has a `BASE_SLOT_MAP` with only 4 entries (0=Idle, 1=Walk, 2=Run, 4=Jump). The CLIs have 20+ slot mappings. Users clicking "Auto-tag" in BgEditor will miss most clips.

**Strategy:** Sync `BASE_SLOT_MAP` with the full overworld slot map from the CLIs. Import from a shared constant or duplicate the map in the store. Also add asset-type detection so Pokemon manifests use the Pokemon slot map instead.

---

## 7. Architecture & New Features

### Current Architecture

```
Export Pipeline (offline):
  BCH/GARC → OhanaCli or SpicaCli → manifest.json + model.dae + clips/*.dae

Runtime (3D POC):
  manifest.json
    → SplitModelAnimationSetLoader.Load()
      → builds ClipsByTag dictionary (keyed by semanticName)
    → AnimationController
      → Play("Idle") / Play("Walk") / HasClip("Jump")
      → delegates to SkeletalAnimator
    → SkinnedDaeModel.UpdatePose(skinMatrices)

Tagging (BgEditor):
  manifest.json
    → /api/manifests/read
    → AnimationsPage.tsx shows clip list + tag editor
    → user assigns semanticName per clip
    → /api/manifests/save writes updated manifest
```

### Data Flow: Tag Assignment

```
                    ┌─────────────────┐
                    │   ROM / GARC    │
                    └────────┬────────┘
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
         OhanaCli       SpicaCli     Spica.Registry
              │              │              │
              │  MapOverworldSlot / MapPokemonSlot
              │  (index → semanticName)     │
              ▼              ▼              ▼
         manifest.json  manifest.json  manifest.json
              │              │              │
              └──────────────┼──────────────┘
                             │
                    ┌────────┴────────┐
                    ▼                 ▼
               BgEditor          3D POC Runtime
            (manual tagging)    (AnimationController)
                    │                 │
                    ▼                 │
            manifest.json ────────────┘
            (updated tags)
```

### Quick Wins

1. **Switch default to Spica export** — Change `_currentCharacterFolder` from `tr0001_00_fi` to `tr0001_00` in Game1.cs. Instant fix for "no animations" with zero re-export work.

2. **Sync BgEditor slot map** — Copy the full overworld slot map (20+ entries) into `animationEditorStore.ts` `BASE_SLOT_MAP`. Auto-tag will then cover Jump, Land, Action, etc.

3. **Add "Duck"/"Dodge" tags** — Once a slot is visually identified in BgEditor, add it to:
   - `SEMANTIC_TAGS` in `types/animation.ts` (UI dropdown)
   - `MapOverworldSlot` in all three CLIs (auto-assignment on re-export)
   - Game1.cs input handler: `_animController.Play("Duck")`

4. **Manifest migration script** — A simple Node.js script that reads old Spica manifests and restructures them to the unified format. Run once to fix existing assets without re-exporting from GARC.

### Future Architecture

- **GPU skinning** — Move bone transforms to vertex shader. Eliminates per-frame CPU skinning and buffer upload.
- **Animation blending** — Add `CrossFade(fromTag, toTag, duration)` to AnimationController. Interpolate between two pose arrays during transition.
- **Clip preview thumbnails** — BgEditor could render a single frame of each clip as a thumbnail for faster visual identification.
- **Shared tag registry** — A single `animation-tags.json` consumed by all three CLIs, BgEditor, and the runtime. Single source of truth for slot maps and known tags.
