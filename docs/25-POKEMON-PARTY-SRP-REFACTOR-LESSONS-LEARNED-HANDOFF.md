# 25 - Pokemon Party System & SRP Refactor: Lessons Learned Handoff

**Date:** 2026-02-24
**Scope:** Pokemon party loading/deploy/recall, pokeball throw mechanics, red recall beam, deploy scale animation, folder-based body type classification, root motion stripping fix, SRP refactor of OverworldCharacter and FreeRoamScreen
**Status:** Compiles clean (0 warnings, 0 errors). All features functional. No runtime crashes observed. Pokemon scaling, deploy timing, recall-cancel-on-movement all working.

---

## 1. What We Accomplished

### Pokemon Party System (New)
- **PokemonSlot.cs** (~100 lines) — Loads a single Pokemon skinned model via `AnimationSetLoader.Load()`, plays Idle animation, computes `fitScale = TargetPokemonHeight / modelHeight` for consistent overworld sizing
- **PokemonParty.cs** (~120 lines) — Manages 6 PokemonSlot instances, active index cycling (Ctrl), deploy/recall state, scale animation (grow on deploy, shrink on recall)
- **TrainerPartyAssignment.cs** (~60 lines) — Maps trainer folder name to 6 Pokemon paths via `trainer_parties.json`, paths are relative to `Models/Pokemon/`
- **trainer_parties.json** — Starter assignments for 4 trainers across PZLA, Scarlet, and Sun-Moon generations

### Deploy/Recall Effects
- Pokemon scales from 0 to full size over 0.25s when deployed (ball lands)
- Pokemon shrinks from full to 0 over 0.25s during recall
- Semi-transparent red beam drawn from Pokemon (waist height, tracks shrink) to pokeball during recall
- Beam rendered as two crossed quads via `DrawUserIndexedPrimitives` using the character's existing `BasicEffect`

### Recall Animation Cancellation
- Moving (WASD) or jumping during recall animation skips the animation but still completes the recall — Pokemon disappears instantly, character resumes locomotion with no momentum loss
- If a pending redeploy was queued (switching Pokemon), the throw still triggers after the skip

### Folder-Based Body Type Classification
- Replaced 3 hardcoded lookup tables (~60 entries total) with folder-path detection
- Trainers are organized into `boy/girl/man/woman` subfolders per generation
- `TrainerGender.Classify()` walks parent directories looking for body type folder names
- `IsTrainerFolder()` verifies the path sits under a recognized trainer directory

### Root Motion Stripping Fix
- PZLA skeletons have a 3-deep root chain: `chara_root -> origin -> waist`
- Old code only stripped depth 0-1, so `waist` bone baked Y-translation caused mid-air placement during throw/recall
- Fixed by computing `Skeleton.RootChain` at construction — walks single-child nodes from root to first branch, includes branch children
- `ClipSampler` now iterates `skeleton.RootChain` to lock translation to bind pose

### SRP Refactor
- **FollowCamera.cs** (~130 lines) — Extracted from FreeRoamScreen: all camera state, orbit math, smooth damping, view/projection computation
- **PokeballController.cs** (~230 lines) — Extracted from OverworldCharacter: flight state machine, hand bone detection, ball loading/sizing, ball drawing, ball world position query
- **OverworldCharacter.cs** — Reduced from 432 to ~220 lines. Now focused on character model/animation + AnimState orchestration
- **FreeRoamScreen.cs** — Reduced from 267 to ~140 lines. Orchestrates grid/camera/character, no longer contains camera math

---

## 2. What Work Remains

### Must-Have
- **Runtime testing across all trainers** — Only a few trainers have party assignments. Need to verify all 4 assigned trainers render Pokemon correctly at proper scale
- **Pokemon idle animation variety** — Currently all Pokemon just loop "Idle". Some Pokemon models may not have an Idle tag and fall back to bind pose (static T-pose)
- **More trainer_parties.json entries** — Only 4 trainers assigned. Need to populate for all trainers that should have Pokemon

### Should-Have
- **Pokemon follow/walk behavior** — Deployed Pokemon currently stand stationary at the ball landing position. They should follow the trainer at a distance or at least face the trainer as they move
- **Pokemon deploy position update** — If you walk far away from the deploy point, the Pokemon stays behind. Consider a leash distance where the Pokemon teleports or walks to catch up
- **Transition polish** — Deploy pop-in could use a white flash or particle burst. Recall shrink could fade to red tint before disappearing

### Nice-to-Have
- **Pokemon battle cries / sound effects** on deploy/recall
- **Multiple pokeball types** — Currently hardcoded to `ob0201_00` (standard pokeball)
- **Pokemon species-specific scaling** — `TargetPokemonHeight = 0.8f` is one-size-fits-all. A Wailord and a Joltik shouldn't be the same height

---

## 3. Optimizations — Prime Suspects

### 3.1 CPU Skinning Is the Hot Path
`SkinnedModel.RebuildBuffers()` runs every frame for every visible skinned model. It:
1. Allocates a new `VertexPositionNormalTexture[]` per mesh per frame
2. Calls `CpuSkinner.Transform()` to skin every vertex on CPU
3. Disposes and recreates the `VertexBuffer` and `IndexBuffer` every frame
4. Uses `List<>.AddRange()` and `.ToArray()` causing GC pressure

**Impact:** With 6 Pokemon + 1 trainer = 7 skinned models, this runs 7x per frame. Each model has 1k-10k vertices.

**Fix:** Pre-allocate vertex/index arrays once at load time. Use `VertexBuffer.SetData()` to update in-place instead of disposing/recreating. Better yet, move skinning to GPU with a vertex shader (bone palette uniform + bone indices/weights in vertex data — the `SkinnedVertex` struct already carries this data but it's thrown away after CPU skinning).

### 3.2 Pokemon Models Load Synchronously on Character Switch
`PokemonParty.LoadAll()` calls `PokemonSlot.Load()` 6 times sequentially. Each call:
- Parses `manifest.json`
- Loads + parses the model `.dae` (XML parsing via `XDocument.Load`)
- Loads + parses all animation `.dae` files
- Loads textures from disk, forces alpha to 255 on every pixel
- Builds vertex buffers

**Impact:** 6 Pokemon with 3-5 clips each = 18-30 DAE files parsed synchronously. Noticeable hitch on character switch.

**Fix:** Load Pokemon on a background thread, show a loading indicator, and swap in when ready. `AnimationSetLoader.Load()` is pure file I/O + math until the `GraphicsDevice` calls, so the parsing can be off-thread and only buffer creation happens on the main thread.

### 3.3 Texture Loading Forces Full Alpha Pass
`SkinnedModel.LoadTexture()` calls `Texture2D.FromStream()` then reads back ALL pixels with `GetData()`, forces `Alpha = 255` on every pixel, and writes back with `SetData()`. This is a full GPU round-trip per texture.

**Impact:** Every texture load does a readback + writeback. With 7+ models, that's potentially 20+ textures.

**Fix:** If the alpha issue is only on certain models, detect it at bake time in the asset pipeline and pre-multiply. Or use a shader that ignores alpha. The `GetData/SetData` round-trip is expensive and should be avoided at runtime.

### 3.4 No Model Caching Across Character Switches
Switching between trainers disposes all models and reloads from scratch. If you switch back to a previously loaded trainer, everything is reparsed and rebuilt.

**Fix:** Implement an LRU cache for AnimationSets and loaded models. Key by folder path, evict after N entries. Most expensive part (DAE parsing, skeleton building) can be cached indefinitely since assets don't change at runtime.

---

## 4. Step-by-Step: Get the App Fully Working

### Prerequisites
- .NET 9 SDK
- MonoGame 3.8+ (pulled via NuGet)
- Assets folder at `Starfield2026.Assets/` with Models/ directory containing character and Pokemon model folders

### Build & Run
```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.3DModelLoader
dotnet build
dotnet run
```

### First Launch Checklist
1. App opens a window titled "Starfield 3D Model Loader"
2. A grid floor renders with a character model (or cyan fallback cube if no models found)
3. **WASD** — move character, **Shift** toggles run, **Space** jumps
4. **Mouse/Arrow keys** — orbit camera
5. **Tab** — opens character select overlay (category > subfolder > character)
6. **Esc** — toggles between Character mode and Map mode
7. **Ctrl** — cycles active Pokemon slot (shown in title bar: `[1/6: pm0025_00_00]`)
8. **Alt** — throws pokeball, Pokemon appears at landing spot with scale-up animation
9. **Alt** again — recalls Pokemon with shrink animation + red beam
10. **Ctrl** while Pokemon deployed, then **Alt** — recalls current, auto-deploys new Pokemon
11. Moving/jumping during recall skips the animation but still recalls

### Verify Logging
Check `modelloader.log` in the output directory for:
- `Database initialized: ...`
- `Found N model entries`
- `Loaded N characters from database`
- `Shared animation folders: ...`
- `Trainer parties: N entries`
- `[Pokemon] Loaded: pmXXXX, height=X.XXX, fitScale=X.XXXXXX`
- `[Party] Loaded N Pokemon for trainer`

### If Models Don't Appear
1. Check that `FindAssetsRoot()` is resolving correctly — it walks up from `bin/` looking for `Starfield2026.Assets/`
2. Verify `Models/` directory exists with character subfolders containing `manifest.json` + `model.dae`
3. Check log for `Failed to load` messages
4. Ensure body type subfolders exist (`boy/girl/man/woman`) under each generation's trainer directory

---

## 5. How to Start/Test

### Running the Application
```bash
# From solution root
cd src/Starfield2026.3DModelLoader
dotnet run
```

### Testing Pokemon Party Specifically
1. Edit `Starfield2026.Assets/trainer_parties.json` to assign Pokemon to a trainer:
```json
{
  "tr0007_00": [
    "PZLA/pm0025_00_00",
    "PZLA/pm0006_00_00",
    null,
    null,
    null,
    null
  ]
}
```
2. Paths are relative to `Models/Pokemon/`. Use `null` for empty slots.
3. Run the app, Tab to select that trainer
4. Ctrl to cycle slots, Alt to throw/recall
5. Watch log output for `[Party]` and `[Pokemon]` lines

### Testing Body Type Classification
1. Move a trainer folder into the wrong body type subfolder (e.g., move a `girl` model into `man/`)
2. Observe that the shared animation retargeting picks the wrong reference
3. Move it back — animations should match again

### Testing Root Motion Stripping
1. Load a PZLA trainer
2. Press Alt for ball throw — character should stay grounded
3. If character floats up during animation, the root chain isn't stripping deep enough

---

## 6. Known Issues & Strategies

### Issue 1: Pokemon Scale May Be Wrong for Extreme Models
Pokemon with very tall or very flat proportions (e.g., Onix, Diglett) will all be normalized to 0.8m. This looks odd for canonically large or small Pokemon.

**Strategy A:** Add a per-species scale override table in `trainer_parties.json`:
```json
{ "PZLA/pm0095_00_00": { "path": "PZLA/pm0095_00_00", "scale": 2.5 } }
```

**Strategy B:** Use Pokedex height data to compute target height per species. Parse species ID from folder name (`pm0025` = Pikachu = 0.4m).

**Strategy C:** Bucket Pokemon into size categories (tiny/small/medium/large/huge) with preset target heights.

### Issue 2: CPU Skinning Perf Degrades with Many Models
7 skinned models per frame (trainer + 6 Pokemon, though only 1 Pokemon renders at a time currently) creates buffer churn. When Pokemon follow behavior is added, multiple Pokemon could be visible.

**Strategy A:** GPU skinning — pass bone matrices as shader uniforms, do skinning in the vertex shader. The `SkinnedVertex` struct already has `BoneIndices` and `BoneWeights`.

**Strategy B:** Pre-allocate buffers at model load, `SetData()` to update in-place. No disposal/recreation per frame.

**Strategy C:** LOD system — Pokemon far from camera use a simpler mesh or skip animation updates. Only the deployed Pokemon needs full skinning.

### Issue 3: Synchronous Loading Causes Frame Hitches
Loading a trainer + 6 Pokemon parses 10-30+ DAE files (XML) on the main thread. This can freeze the app for 0.5-2 seconds on slower disks.

**Strategy A:** Background thread loading — parse DAE files off-thread, only call `GraphicsDevice` APIs on main thread. Use a loading flag to show a fallback cube while loading.

**Strategy B:** Binary asset cache — convert DAE to a compact binary format at first load, use the cached version on subsequent loads. Dramatically faster than XML parsing.

**Strategy C:** Lazy loading — only load the active Pokemon slot. Load others on demand when Ctrl cycles to them. Spread the cost over time.

### Issue 4: No Animation Blending Between States
Transitions between Idle/Walk/Run/Jump are instant clip swaps. This causes visible pops, especially at higher movement speeds.

**Strategy A:** Crossfade blending — maintain two active clips with a blend weight that transitions over 0.15-0.3s. `ClipSampler` samples both clips, `lerp` the local poses.

**Strategy B:** Additive blending for partial-body animations — throw/recall animations could be upper-body-only overlays on top of walk/run, eliminating the need to stop movement.

**Strategy C:** Animation state machine with configurable transition durations per edge (Idle->Walk: 0.2s, Walk->Run: 0.15s, any->Jump: 0.05s).

---

## 7. New Architecture & Features

### Architecture After Refactor

```
ModelLoaderGame (orchestrator)
  |
  +-- FreeRoamScreen (scene orchestrator, ~140 lines)
  |     +-- FollowCamera (orbit camera, smooth damping)
  |     +-- PlayerController (WASD movement, physics)
  |     +-- OverworldCharacter (model + state machine, ~220 lines)
  |     |     +-- PokeballController (ball flight, hand bone, drawing)
  |     |     +-- PokemonParty (6 slots, deploy/recall/scale)
  |     |           +-- PokemonSlot[6] (individual Pokemon model)
  |     +-- QuadrantGridRenderer
  |     +-- CubeRenderer (shadow, fallback)
  |
  +-- MapViewerScreen (map mode)
  +-- CharacterSelectOverlay / MapSelectOverlay
  +-- CharacterDatabase (SQLite persistence)
```

### Key Design Decisions
- **CPU skinning** kept for simplicity — GPU skinning is the clear upgrade path but requires shader work
- **Party loads with character** — all 6 slots loaded upfront, not on-demand, to avoid mid-gameplay hitches
- **trainer_parties.json** is hand-edited — no UI for assignment. Keeps the tool simple
- **Recall cancel skips animation, still recalls** — preserves gameplay momentum without leaving Pokemon stranded

### Quick Wins

1. **Pre-allocate skinning buffers** — Change `RebuildBuffers()` to reuse arrays. ~30 min, eliminates biggest per-frame GC source. Edit `SkinnedModel.cs` to allocate `VertexPositionNormalTexture[]` and `int[]` at load time, `SetData()` to update.

2. **Pokemon follow trainer** — In `OverworldCharacter.Update()`, when deployed, lerp `_deployPosition` toward a point behind the trainer. ~20 lines of code. Use same smooth damp as camera.

3. **Texture atlas per model** — Many models have 5-10 small textures. Bake into a single atlas at pipeline time to reduce draw calls from ~8 to 1 per model.

4. **Binary animation cache** — After first DAE parse, serialize `AnimationClip` to a binary file. On subsequent loads, read binary directly (skip XML parsing). 10-50x faster load times.

---

## 8. File Reference

### New Files This Session
| File | Lines | Purpose |
|------|-------|---------|
| `Controllers/PokeballController.cs` | ~230 | Ball flight, hand bone, throw/recall mechanics |
| `Rendering/FollowCamera.cs` | ~130 | Third-person orbit camera with smooth damping |
| `Helpers/PokemonSlot.cs` | ~100 | Single Pokemon model: load, animate, draw |
| `Helpers/PokemonParty.cs` | ~120 | 6 slots, cycling, deploy/recall, scale animation |
| `Helpers/TrainerPartyAssignment.cs` | ~60 | JSON-based trainer-to-Pokemon mapping |
| `Starfield2026.Assets/trainer_parties.json` | ~35 | Party data for 4 test trainers |

### Modified Files This Session
| File | Before | After | Key Changes |
|------|--------|-------|-------------|
| `Helpers/OverworldCharacter.cs` | 432 | ~220 | Extracted pokeball to controller, added party integration, red beam, recall cancel |
| `Screens/FreeRoamScreen.cs` | 267 | ~140 | Extracted camera, added party/pokemon root config |
| `ModelLoaderGame.cs` | 377 | ~390 | Added trainer party JSON loading, Pokemon root path |
| `Helpers/TrainerGender.cs` | ~130 | ~70 | Replaced lookup tables with folder-path detection |
| `DTOs/Skeleton.cs` | - | +30 | Added RootChain property + BuildRootChain |
| `Animations/ClipSampler.cs` | - | ~5 | Use skeleton.RootChain for root motion stripping |
