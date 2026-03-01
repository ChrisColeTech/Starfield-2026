# 33 - Mountain Scene FBX Loading Lessons Learned Handoff

## 1) What We Accomplished

- Implemented fast Pokemon swap behavior in overworld flow:
  - Added interrupt-style recall acceleration on repeated Alt input.
  - Added fast recall scale-down path in party logic.
  - Kept queued redeploy behavior so swap timing is more battle-friendly.
- Improved camera and runtime player integration:
  - Added terrain-height setter on `PlayerController`.
  - Extended follow-camera behavior to respond to deployed Pokemon height.
  - Added direct deployed Pokemon position/height exposure from character state.
- Added a new FBX loading pipeline for scene experiments:
  - Added `AssimpNet` package to `Starfield2026.3DModelLoader`.
  - Added `FbxModel` runtime loader (`Loaders/FbxLoader.cs`).
  - Added `MountainSceneScreen` and integrated mode cycling in `ModelLoaderGame`.
- Improved map loading resilience:
  - `MapViewerScreen` now loads first `*.dae` if `model.dae` is absent.

## 2) What Work Remains

- Scale normalization is not finalized:
  - Scene object sizing is still too inconsistent across Mountain assets.
  - Placement currently uses randomized multi-instance scatter, which made debugging harder.
- Rendering consistency still needs pass:
  - Some meshes may appear too bright/flat depending on missing texture bindings.
  - Model coordinate basis and up-axis assumptions need explicit validation.
- Screen strategy needs simplification:
  - There are now multiple experimental paths (map viewer, territories concepts, mountain scene).
  - We should lock one test loop and avoid parallel prototype branches.

## 3) Optimizations - Prime Suspects to Start With

1. **Draw call pressure in scene placement**
   - Replace many unique draw submissions with batched/instanced paths for repeated assets (trees, rocks, grass).
2. **Texture/material resolution in FBX loader**
   - Build deterministic material-to-texture binding and cache resolved textures globally.
3. **Scale and bounds calibration pass**
   - Precompute canonical scene-unit scale per source pack (Mountain, Tropical, Stylized Nature) and persist profile data.
4. **Culling and LOD strategy**
   - Add frustum culling + distance tiers (full mesh, simplified mesh, billboard/skip).

## 4) Step-by-Step to Get App Fully Working (No Errors)

1. Build clean and verify runtime dependencies:
   - `dotnet restore`
   - `dotnet build src/Starfield2026.3DModelLoader/Starfield2026.3DModelLoader.csproj`
2. Confirm asset paths exist:
   - `src/Starfield2026.Assets/Models/Maps/Mountain/Models/*.fbx`
   - `src/Starfield2026.Assets/Models/Maps/Mountain/Textures/*`
3. Run app and verify mode cycle:
   - Start app, use existing mode key cycle to enter Mountain mode.
4. Reduce scope to one model first (critical):
   - In `MountainSceneScreen.LoadScene`, place only one model (for example `Mountain01.fbx`) at origin.
   - Validate bounds, radius, and visual size before enabling additional instances.
5. Add models back one-by-one:
   - Rock -> Tree -> Bush -> Grass -> Flower -> Pebbles.
   - Tune each model family scale from measured bounds, not hardcoded guessed values.
6. Stabilize texture bindings:
   - Confirm each model resolves to expected `_ALB.png`.
   - Add explicit fallback mapping table where material metadata is incomplete.
7. Add performance guardrails:
   - Keep distance culling active.
   - Cap initial instance count low until stable.

## 5) How to Start/Test the API

### 3D Model Loader App (primary workstream)

- Build:
  - `dotnet build src/Starfield2026.3DModelLoader/Starfield2026.3DModelLoader.csproj`
- Run:
  - `dotnet run --project src/Starfield2026.3DModelLoader/Starfield2026.3DModelLoader.csproj`
- Manual test checklist:
  - App launches with no exceptions.
  - Character mode renders.
  - Mode cycle reaches map and mountain modes.
  - Mountain mode renders at least one FBX model correctly scaled.

### MCP/API-adjacent tooling (if needed)

- MiniToolbox MCP server:
  - See `src/Starfield2026.MiniToolboxMCP/README.md`.
  - Start using documented stdio command and validate MCP initialization handshake.

## 6) Issues Seen + 4 Strategies to Solve All Errors

### Issues observed

- Inconsistent FBX asset scale leading to oversized scene objects.
- Texture lookup mismatch for some converted/packed assets.
- Too many simultaneous experimental changes increased regression risk.

### Strategies

1. **Single-variable debugging protocol**
   - One model, one placement, one scale at a time; no bulk spawning until baseline is verified.
2. **Asset profile registry**
   - Introduce per-pack metadata (`unitScale`, `upAxis`, `textureRoot`, `defaultMaterial`) and load profiles by map pack.
3. **Golden scene snapshot tests**
   - Add automated render snapshots for known camera positions to catch scale/texture regressions quickly.
4. **Feature flags for experimental screens**
   - Keep Mountain/Territories experiments behind explicit flags so core workflows remain stable.

## 7) Location

- This handoff document is saved at:
  - `docs/33-MOUNTAIN-SCENE-FBX-LOADING-LESSONS-LEARNED-HANDOFF.md`

## 8) New Architecture/Features and Quick Wins

### New architecture/features introduced

- Runtime FBX loading path via Assimp (`FbxModel`).
- Dedicated mountain-scene screen for non-grid world composition experiments.
- Expanded character/pokemon integration hooks (height-aware camera and faster switching).

### Quick wins (high impact, low effort)

1. Add `SceneDebugMode = SingleModel` toggle in `MountainSceneScreen`.
2. Log per-model dimensions and final applied scale each load.
3. Replace random scatter with a tiny deterministic layout JSON for repeatable testing.
4. Add `GlobalSceneScale` slider hotkey for fast runtime tuning.
