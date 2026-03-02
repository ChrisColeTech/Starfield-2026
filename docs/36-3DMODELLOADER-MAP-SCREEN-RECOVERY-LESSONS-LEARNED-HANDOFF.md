# 36 - 3DModelLoader Map Screen Recovery - Lessons Learned & Handoff

## Context

This handoff captures the recent recovery effort after map/loader files were removed and the runtime path became unstable. The goal was to reintroduce a map-capable screen path, restore compile/runtime viability, and document what still blocks full reliability.

---

## 1) What We Accomplished

1. **Build recovery after file deletion**
   - Recovered `Starfield2026.3DModelLoader` to a successful `dotnet build` state.
   - Reintroduced missing compile surface for map rendering paths.

2. **New map screen implementation**
   - Added `MapEditorMapScreen` as a dedicated map-mode screen.
   - Integrated map rendering/camera/player/character behavior into this screen.
   - Wired input so `F1` toggles FreeRoam <-> Map mode and `PgUp/PgDn` map cycling remains available in map mode.

3. **Framework-aligned map loading**
   - Used `MapCatalog` + generated `MapDefinition` classes as source of truth.
   - Default preferred map selection is `anime_map_v2`, with fallback to first registered map.

4. **FBX runtime implementation restored (non-stub)**
   - Replaced placeholder `FbxModel` stub with Assimp-backed loading and draw paths.
   - Added texture path resolution and batching for draw calls.
   - Added a basic `SampleHeight` implementation to support collision/terrain probing workflows.

---

## 2) What Work Remains

1. **Runtime confidence is not complete**
   - Build succeeds, but startup/runtime reliability still needs deterministic validation on target machines.

2. **Map/asset path mismatch hardening**
   - Many tile IDs still report missing FBX because registry `ModelId` names do not all exist in the assets folder.
   - Need clear policy: strict parity vs fallback rendering for missing models.

3. **FBX rendering quality/perf checks**
   - Current FBX loader works, but needs profiling and visual validation for alpha/textures/material correctness under all map assets.

4. **Diagnostic maturity**
   - Logging works, but we still need stronger startup diagnostics and failure categorization (asset not found, parse fail, GPU init fail, etc.).

---

## 3) Optimizations - Prime Suspects (Start Here)

1. **Tile model discovery/cache hot path**
   - `TileModelCache.LoadFromRegistry` recursively scans for each `ModelId` and can be expensive.
   - Build one-time index of all FBX stems at startup; map `ModelId -> fullPath` in O(1).

2. **FBX vertex/index upload churn**
   - Ensure each FBX is loaded once and buffers are reused across maps/screens.
   - Add model LRU limits and preload strategy for map-specific sets.

3. **Texture load duplication**
   - Current per-model texture handling should be centralized to avoid duplicate texture uploads.
   - Introduce a shared texture cache with ref counting.

4. **Map draw pass overdraw**
   - Tune frustum culling radius and reduce alpha pass work for distant foliage.
   - Consider per-tile visibility buckets or chunked culling.

---

## 4) Step-by-Step Path to Fully Working App (No Errors)

1. **Stabilize startup invariants**
   - Validate assets root, maps folder, and generated map registration before first frame.
   - Fail fast with explicit log category if any required path is missing.

2. **Enforce TileRegistry <-> assets parity report**
   - Add a startup parity check that prints:
     - missing FBX for model IDs
     - unused FBX not referenced by registry
   - Keep this as actionable output, not fatal.

3. **Lock map selection behavior**
   - Confirm preferred map (`anime_map_v2`) selection success/fallback path.
   - Keep map cycling only when multiple maps are present.

4. **Harden FBX loader behavior**
   - Verify transform handling, texture resolution variants, and alpha materials on representative anime tree assets.
   - Add guarded exception handling around scene import and per-node mesh processing.

5. **End-to-end smoke suite (manual + scripted checks)**
   - Launch app, toggle modes, load character, switch maps, move camera/player.
   - Confirm no unhandled exceptions and no fatal rendering errors.

6. **Regression pass**
   - Re-run build and runtime checks after each fix batch.
   - Keep a known-good log baseline for comparison.

---

## 5) How to Start/Test the API

There is currently **no separate HTTP API service** in this flow. The executable itself is the runtime under test.

### Start

```bash
dotnet build src/Starfield2026.3DModelLoader/Starfield2026.3DModelLoader.csproj
dotnet run --project src/Starfield2026.3DModelLoader/Starfield2026.3DModelLoader.csproj
```

### Runtime smoke test checklist

1. Launch to FreeRoam without crash.
2. Press `F1` to enter map mode.
3. Confirm map status shows selected map and dimensions.
4. Press `PgUp/PgDn` to cycle maps.
5. Press `Tab` to select character and verify model appears.
6. Verify `modelloader.log` updates with map/model load events.

Log location example:

`src/Starfield2026.3DModelLoader/bin/Debug/net9.0/modelloader.log`

---

## 6) Issues + 4 New Strategies to Solve Remaining Errors

### Current issues observed

1. Inconsistent runtime confidence despite successful build.
2. Missing model IDs for parts of TileRegistry.
3. Fragility after large file deletions/re-additions.
4. Limited automated safety nets around startup/map load regressions.

### Strategy A - Contract Tests for Asset Parity

- Add a test/tool that validates every non-empty `ModelId` resolves to an FBX path.
- Output machine-readable report and fail CI only on configured critical IDs.

### Strategy B - Golden Map Boot Test

- Add a lightweight integration test harness that boots renderer headless/minimal, loads `anime_map_v2`, and verifies no exceptions.
- Capture a deterministic success signal in logs.

### Strategy C - Recovery Branch Workflow

- During unstable refactors, keep a minimal safety branch that only guarantees app boot + free roam + map open.
- Cherry-pick feature increments into this branch to avoid compounding failures.

### Strategy D - Feature Flags for Map Paths

- Gate new map/FBX pipelines behind runtime flags:
  - legacy map path
  - new map screen path
  - strict parity mode
- Allows isolating failures quickly without blocking whole app startup.

---

## 7) Document Location

This handoff is written in:

`docs/36-3DMODELLOADER-MAP-SCREEN-RECOVERY-LESSONS-LEARNED-HANDOFF.md`

---

## 8) New Architecture/Features + Quick Wins

### New/updated architecture points

1. **Dedicated map mode screen** (`MapEditorMapScreen`) isolated from FreeRoam core loop.
2. **Map framework alignment** via `MapCatalog` + generated `MapDefinition` + `TileRegistry`.
3. **FBX load/render surface restored** through non-stub `FbxModel` implementation.

### Quick wins (high ROI)

1. Add explicit startup status line in window title/log: assets root, selected map ID, loaded model count.
2. Add one-shot parity report command to quickly identify missing tile models.
3. Cache FBX stem index at startup to remove repeated recursive file searches.
4. Add a single-key debug overlay toggle for map ID, tile under player, collision status, and FPS.

---

## Closing Note

The session achieved build recovery and reintroduced a concrete map screen path, but full runtime trust still requires a focused stabilization pass. The fastest path forward is to lock startup invariants, enforce registry/assets parity visibility, and add one golden boot test for `anime_map_v2`.
