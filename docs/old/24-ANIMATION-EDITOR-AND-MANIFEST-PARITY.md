# 24 - Animation Editor & Manifest Parity Lessons

## Summary

This document covers the BgEditor Animation Editor feature, manifest format parity between the TypeScript extraction pipeline and C# OhanaCli, and strategies for getting the full animation tagging → runtime playback pipeline working end-to-end.

---

## 1. What We Accomplished

### BgEditor Animation Editor (5 files)

Built a complete animation tagging UI that reads extracted `manifest.json` files, previews each clip in a 3D viewport, lets the user assign semantic tags, and saves the updated manifest back to disk.

| File | Purpose |
|------|---------|
| `backend/src/routes/manifests.ts` | Added `GET /api/manifests/read` and `POST /api/manifests/save` endpoints |
| `frontend/src/types/animation.ts` | `SplitManifest`, `SplitManifestClip` interfaces + `SEMANTIC_TAGS` (30 known tags) |
| `frontend/src/services/sceneService.ts` | Added `loadModelOnly()` and `loadClipDae()` for split-manifest loading |
| `frontend/src/store/animationEditorStore.ts` | Zustand store: `loadFolder`, `selectClip`, `tagClip`, `autoTag`, `save` |
| `frontend/src/pages/AnimationsPage.tsx` | Full rewrite: folder loader, clip list, tag dropdown, auto-tag, save |

### Manifest Format Parity (extraction.ts + extract-garc.ts)

Aligned the TypeScript extraction pipeline with C# OhanaCli to produce identical manifest structures:

| Change | Before | After |
|--------|--------|-------|
| Asset type detection | None (always used overworld map) | `detectAssetType()` checks texture/model name prefixes (`pm` → Pokemon, `tr`/`_fi` → Overworld) |
| Slot map selection | Single hardcoded overworld map | Separate `OVERWORLD_SLOT_MAP` (25 entries) and `POKEMON_SLOT_MAP` (1 entry) |
| Source index parsing | Regex-only (`anim_N`) | `parseSourceAnimIndex()` — matches C#'s `ParseSourceAnimIndex` |
| Source name keywords | Had extra guesses (`faint`, `sleep`, `attack`) | Removed — now matches C# exactly: `idle`, `walk`, `run`, `jump` only |
| Fallback for unknown types | `index-map-v2` base map | `sourceIndex === 0 ? 'Idle' : null` — matches C#'s `Unknown` branch |

### BgEditor Auto-Tag Parity

The `animationEditorStore.ts` `OVERWORLD_SLOT_MAP` was synced from 4 entries to the full 25-entry overworld slot map matching OhanaCli's `MapOverworldSlot`.

---

## 2. What Work Remains

### Critical Path

1. **Re-export all character assets** — On-disk `_fi` exports have zero clip DAEs. The animation editor and 3D POC both need re-exported assets with `--split-model-anims` to function. This is the single biggest blocker.

2. **Test the full round-trip** — Extract → BgEditor load manifest → preview clips → tag → save → 3D POC loads updated manifest → plays tagged animations. This has never been tested end-to-end.

3. **Viewport clip playback** — The `loadClipDae()` function uses the existing custom Collada animation parser (`parseColladaAnimations`). This works for animations embedded in a full model DAE, but has not been tested with standalone clip DAEs that reference bones from a separate model file. The bone-to-track mapping may fail if the clip DAE doesn't include the skeleton hierarchy.

### Nice-to-Have

- **Pokemon battle animation slots** — Only slot 0 (Idle) is mapped. Need to visually identify slots using BgEditor preview.
- **Animation crossfade** — Walk↔Run transitions are instant; needs pose blending.
- **Multi-model support** — AnimationsPage only uses `models[0]`. Manifests can have multiple models (alternate forms).
- **BgEditor: browse button** — Currently requires pasting a filesystem path. An Electron-style folder picker or backend directory listing would improve UX.

---

## 3. Optimizations — Prime Suspects

### 3.1 Lazy Clip Loading in 3D POC
**Impact: High** — `SplitModelAnimationSetLoader.Load()` eagerly parses ALL clip DAEs (16+ COLLADA XML files per character). Most clips are never played during a session.

**Approach:** Store clip manifest entries in the dictionary but defer `ColladaSkeletalLoader.LoadClip()` until first `AnimationController.Play()`. Use a `Lazy<SkeletalAnimationClip>` wrapper or a simple null-check-and-load pattern.

### 3.2 GPU Skinning
**Impact: High** — `SkinnedDaeModel.UpdatePose()` does CPU skinning (transforms every vertex by bone matrices) and recreates vertex/index buffers every frame. For a 10k-vertex character at 60fps, that's 600k vertex transforms per second on the CPU.

**Approach:** Upload bone matrices as shader constants, do skinning in a custom vertex shader. Use `DynamicVertexBuffer` allocated once. This is the standard approach in modern game engines.

### 3.3 Clip DAE Deduplication
**Impact: Medium** — When a GARC group has multiple models (e.g., model + model_1 for alternate forms), the same animation clips get exported as separate DAE files for each model. The DAE content is identical — only the skeleton reference differs.

**Approach:** During export, hash clip DAE content and create symlinks or shared files instead of duplicates. Or: store clips at `clips/clip_000.dae` (not `clips/model/clip_000.dae`) and let the loader map them to any model.

### 3.4 BgEditor: Cache Loaded Scenes
**Impact: Medium** — Every clip selection in the animation editor calls `loadClipDae()` which fetches the DAE over HTTP, parses XML, and runs the animation parser. For rapid A/B comparison of clips, this adds noticeable latency.

**Approach:** Cache parsed `THREE.AnimationClip` objects in the store keyed by clip file path. Invalidate on folder change.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
- .NET 9.0 SDK (for 3D POC and OhanaCli)
- Node.js 18+ (for BgEditor)
- Extracted GARC assets or BCH files from Sun/Moon RomFS

### Step 1: Export characters with split animations

```bash
# Option A: Using the TS CLI (extract-garc.ts)
cd D:\Projects\PokemonGreen\src\PokemonGreen.BgEditor\backend
npx tsx test/extract-garc.ts \
  <path-to-garc> \
  D:\Projects\PokemonGreen\src\PokemonGreen.Assets\Pokemon3D\characters\overworld \
  --split-model-anims

# Option B: Using OhanaCli (C#)
cd D:\Projects\PokemonGreen\src\PokemonGreen.OhanaCli
dotnet run --project src/OhanaCli.App -- <path-to-bch> --split-model-anims -o <output-dir>
```

### Step 2: Verify manifest format

Open any `manifest.json` and confirm:
- Top-level keys: `version`, `mode`, `textures`, `models`
- `mode` is `"split-model-anims"`
- `textures` is a flat string array `["tex1.png", "tex2.png"]`
- `models[0].clips` has entries with `id`, `name`, `sourceName`, `semanticName`, `semanticSource`, `file`, `frameCount`, `fps`
- Clip `file` paths exist on disk (e.g., `clips/model/clip_000.dae`)

### Step 3: Tag animations in BgEditor

```bash
# Terminal 1
cd D:\Projects\PokemonGreen\src\PokemonGreen.BgEditor\backend
npm install && npm run dev

# Terminal 2
cd D:\Projects\PokemonGreen\src\PokemonGreen.BgEditor\frontend
npm install && npm run dev
```

1. Open http://localhost:5173
2. Navigate to **Animations** page
3. Paste folder path (e.g., `D:\...\Pokemon3D\characters\overworld\tr0001_00_fi`)
4. Click **Load** — model renders, clip list populates
5. Click each clip to preview in viewport
6. Use tag dropdown to assign semantic names (Idle, Walk, Run, Jump, etc.)
7. Click **Auto-tag** to fill untagged clips using slot map
8. Click **Save** — writes updated `manifest.json` to disk

### Step 4: Build and run the 3D POC

```bash
dotnet run --project src/PokemonGreen.3D/PokemonGreen.3D.csproj
```

1. Character renders on grid — should play Idle animation
2. WASD → Walk; Shift+WASD → Run; Space → Jump
3. Tab → character select overlay

### Step 5: Verify tag-based playback

The 3D POC's `AnimationController` resolves clips by `semanticName`:
- `Play("Idle")` → looks up `ClipsByTag["Idle"]`
- Tags come from manifest `semanticName` field (set by export or BgEditor)
- Fallback: `InferTag` parses `clip_000` → index 0 → "Idle" (only 0/1/2 are inferred)

If a clip plays correctly in BgEditor but not in the 3D POC, check:
1. Manifest was saved (no dirty indicator in BgEditor)
2. `semanticName` is set (not null) in the saved manifest.json
3. 3D POC is reading from the same folder path

---

## 5. How to Start/Test the APIs

### BgEditor Backend (Fastify, port 3001)

```bash
cd src/PokemonGreen.BgEditor/backend
npm run dev
```

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/manifests/read?dir=<path>` | Read manifest.json from a folder |
| POST | `/api/manifests/save` | Write manifest to disk (`{dir, manifest}`) |
| GET | `/api/manifests?dir=<path>` | Scan directory tree for all manifests |
| POST | `/api/manifests/generate` | Auto-generate manifests for model folders |
| GET | `/serve/<base64-dir>/<filename>` | Serve files (models, textures) to Three.js |

Test the manifest read endpoint:
```bash
curl "http://localhost:3001/api/manifests/read?dir=D:/Projects/PokemonGreen/src/PokemonGreen.Assets/Pokemon3D/characters/overworld/tr0001_00"
```

Test the manifest save endpoint:
```bash
curl -X POST http://localhost:3001/api/manifests/save \
  -H "Content-Type: application/json" \
  -d '{"dir":"D:/path/to/folder","manifest":{"version":1,"mode":"split-model-anims","textures":[],"models":[]}}'
```

### BgEditor Frontend (Vite, port 5173)

```bash
cd src/PokemonGreen.BgEditor/frontend
npm run dev
```

Type-check both projects:
```bash
cd backend && npx tsc --noEmit
cd ../frontend && npx tsc --noEmit
```

---

## 6. Known Issues & Error Strategies

### Issue 1: Clip DAEs May Not Load in BgEditor Viewport

**Problem:** `loadClipDae()` fetches a standalone clip DAE and parses animations. The clip DAE references bone names from the skeleton, but the custom `parseColladaAnimations()` parser maps Collada node IDs to Three.js bone objects in the scene. If the clip DAE doesn't include `<library_visual_scenes>` with matching bone node IDs, the parser can't map animation channels to bones.

**Strategy A (Likely fix):** The DAE exporter's `exportClipOnly()` should include the full skeleton hierarchy in the clip file's visual scene. Verify by opening a clip DAE and checking for `<node>` elements with bone `id` attributes.

**Strategy B (Fallback):** Modify `parseColladaAnimations()` to accept an optional bone-name-to-id mapping from the model DAE, so it can resolve targets even when the clip DAE has no visual scene.

**Strategy C (Quick test):** Load a clip DAE in a standalone Three.js scene with ColladaLoader. If `collada.animations` is populated, the built-in parser works and the custom parser isn't needed for clips.

### Issue 2: FrameCount Type Mismatch

**Problem:** C# `SplitClipManifestEntry.FrameCount` is `float`, TS `ManifestClip.frameCount` is `number`. JSON doesn't distinguish — both serialize as numbers. But if a C# exporter writes `42.0` and the TS code expects an integer, display quirks may occur.

**Strategy:** Not a real bug. JSON `42` and `42.0` are identical. Both deserialize correctly in both languages. Monitor for floating-point frame counts from animations that aren't frame-aligned.

### Issue 3: Duplicate Semantic Tags in ClipsByTag

**Problem:** The C# `ClipsByTag` dictionary uses `TryAdd`, so only the FIRST clip with a given tag wins. If two clips are both tagged "Idle" (e.g., from auto-tag + manual tag), the second one is silently dropped. The BgEditor has no validation preventing duplicate tags across clips.

**Strategy A:** Add a warning in the AnimationsPage when two clips share the same `semanticName`. Display a yellow badge or border on duplicates.

**Strategy B:** In the `tagClip` action, check if another clip already has this tag and show a confirmation or auto-clear the previous one.

### Issue 4: Stale Manifests After Re-Export

**Problem:** If a user tags clips in BgEditor, saves the manifest, then re-exports from GARC, the re-export overwrites the manifest with auto-generated tags, losing all manual tags.

**Strategy A:** Before overwriting, read the existing manifest and merge `semanticName`/`semanticSource` fields where `semanticSource === "manual"`. Only overwrite auto-generated tags.

**Strategy B:** Export to a staging directory, then use BgEditor's auto-tag to fill in machine-guessed tags, manually verify, and copy to the assets directory.

---

## 7. Architecture & Quick Wins

### Current Data Flow

```
ROM/GARC ──► OhanaCli / TS CLI ──► manifest.json + model.dae + clips/*.dae
                                         │
                                         ├──► BgEditor (preview + tag + save)
                                         │         │
                                         │         ▼
                                         │    manifest.json (updated semanticName)
                                         │
                                         └──► 3D POC Runtime
                                                  │
                                              AnimationController.Play("Idle")
                                                  │
                                              ClipsByTag["Idle"] → SkeletalAnimationClip
```

### Manifest Parity Matrix

All three exporters now produce identical manifest structures:

| Field | OhanaCli (C#) | TS extraction.ts | TS extract-garc.ts |
|-------|--------------|------------------|-------------------|
| `version` | 1 | 1 | 1 |
| `mode` | `"split-model-anims"` | `"split-model-anims"` | `"split-model-anims"` |
| `textures` | `string[]` | `string[]` | `string[]` |
| `models[].name` | model name | model name | model name |
| `models[].modelFile` | `"model.dae"` | `"model.dae"` | `"model.dae"` |
| `clips[].index` | sequential int | sequential int | sequential int |
| `clips[].id` | `"clip_000"` | `"clip_000"` | `"clip_000"` |
| `clips[].name` | `"clip_000"` | `"clip_000"` | `"clip_000"` |
| `clips[].sourceName` | from GfMotion | from GfMotion | from GfMotion |
| `clips[].semanticName` | slot-map or null | slot-map or null | slot-map or null |
| `clips[].semanticSource` | `"slot-map-v1"` / `"source-name"` / null | same | same |
| `clips[].file` | `"clips/model/clip_000.dae"` | `"clips/model/clip_000.dae"` | `"clips/model/clip_000.dae"` |
| `clips[].frameCount` | float (from anim) | number (from anim) | number (from anim) |
| `clips[].fps` | 30 | 30 | 30 |
| Asset type detection | `DetectAssetType()` | `detectAssetType()` | `detectAssetType()` |
| Overworld slot map | 25 entries | 25 entries | 25 entries |
| Pokemon slot map | 1 entry (Idle) | 1 entry (Idle) | 1 entry (Idle) |

### C# Runtime Loader Compatibility

The `SplitModelAnimationSetLoader` reads manifests with `PropertyNameCaseInsensitive = true`, so JSON property casing (camelCase from TS, PascalCase from C#) doesn't matter. Key compatibility points:

| Loader Behavior | Manifest Field Used |
|----------------|-------------------|
| Clip key (dictionary key) | `Id ?? Name ?? "clip_{Index:D3}"` |
| Clip display name | `SemanticName ?? SourceName ?? Name ?? Id` |
| Tag (ClipsByTag key) | `SemanticName ?? InferTag(Name ?? Id)` |
| Clip file path | `File ?? "clips/clip_{Index:D3}.dae"` |

### Quick Wins

1. **Duplicate tag warning** — Add validation in AnimationsPage: if two clips share a `semanticName`, show a warning badge. Prevents silent clip drops in the runtime `ClipsByTag` dictionary.

2. **Clip cache in BgEditor** — Cache `THREE.AnimationClip` by file path in `animationEditorStore`. Eliminates re-fetch/re-parse when clicking back to a previously viewed clip.

3. **Manifest merge on re-export** — Before writing manifest.json, check for existing file and preserve `semanticSource: "manual"` entries. Prevents loss of manual tags on re-export.

4. **Default character fix** — Change `Game1.cs` `_currentCharacterFolder` from `"tr0001_00_fi"` to whichever folder actually has clip DAEs on disk. Zero-effort fix for "character doesn't animate".
