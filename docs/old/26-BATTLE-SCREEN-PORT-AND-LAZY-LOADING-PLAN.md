# 26 - Battle Screen Port & Lazy Loading Plan

## Summary

This session ported the 2D game's battle screen (3D scene rendering, camera animation, Pokemon models, UI overlay) into `BattleScreen3D` in Core, designed the async model preloading strategy, and identified the map editor enhancement needed to support per-map encounter editing.

---

## 1. What We Accomplished

### Battle Screen Port (2D → 3D POC)

The previous attempt by an automated agent fabricated a fake battle screen with gradient backgrounds and colored rectangle placeholders instead of porting the actual 2D code. This session replaced it with a faithful port of the 2D `Game1.cs` battle code:

**BattleScreen3D.cs** (802 lines) now contains:

| Feature | Source (2D Game1.cs) | Status |
|---------|---------------------|--------|
| 3D battle scene loading | `LoadBattleModels()` — Grass, TallGrass, Cave, Dark backgrounds + platforms | Ported |
| AlphaTestEffect rendering | `DrawBattle3D()` — backgrounds, platforms at correct positions | Ported |
| Pokemon 3D model loading | `LoadPokemonModel()` via `SpeciesRegistry` + `ModelLoader` | Ported |
| Model scaling | `FitModelScale()` — ally 3.5 height, foe 3.0 height | Ported |
| Camera animation | Zoom from foe close-up `(6.9, 7, 4.6)` to full view `(7, 7, 15)` with ease-out | Ported |
| Battle intro sequence | "Wild X appeared!" → camera zoom → "Go! Y!" → show menu | Ported |
| Battle background selection | `BattleBackground` enum per encounter type | Ported |
| Animation callbacks | `PlayIndex(0/1/2)` for idle/attack/faint wired to `BattleTurnManager` | Ported |
| Split draw pattern | `Draw3DScene()` at viewport res, `DrawUI()` in virtual coord space | New |
| Proportional UI layout | All margins/sizes computed from virtual dimensions, not hardcoded | New |

**Game1.cs** (3D POC) changes:
- Loads battle BG models at startup via `BattleScreen3D.LoadBattleModels()`
- Calls `ModelLoader.InitializeDevPaths()` for Pokemon model path resolution
- Split draw: `Draw3DScene()` then `DrawUI()` inside SpriteBatch with transform matrix

### Prior Session (Committed as e2001008)

- Decomposed monolithic Game1.cs into `BattleScreen3D`, `CubeCollectibleSystem`, `PersistenceManager3D`
- Moved all three to Core library (Battle/, Systems/, Save/)
- Fixed SaveManager.Load() schema migration bug
- Implemented virtual resolution UI scaling (1280x960, fontScale=5, PointClamp)

---

## 2. What Work Remains

### Immediate (Blocking Gameplay)

1. **Async Pokemon model preloading** — Currently `LoadPokemonModel()` is synchronous in `EnterBattle()`. With potentially hundreds of species, this causes a hitch on battle entry. Need background loading triggered on map/world load.

2. **Map editor encounter editing** — The BgEditor (Electron + React + Express) has no UI for setting which Pokemon appear per map. Without this, encounters must be hand-edited as JSON. Need:
   - Species picker (loads from `species.json` — 802 entries)
   - Per-map encounter table editor (encounter type, rate, species/level/weight entries)
   - Export to `Content/Data/Encounters/{mapId}.json`

3. **Pokemon model re-export** — Only 26 of 802 species have exported 3D models in `Pokemon3D/`. The battle encounter dumps need to be re-exported with the new manifest format (SpicaCli `--split-model-anims`).

4. **Party system in 3D POC** — The 3D POC uses test Pokemon for battles. Need to wire up actual party management (currently only in the 2D game).

### Near-Term

5. **Battle background selection in 3D POC** — `_currentBattleBackground` defaults to Grass. Need to wire it to tile encounter type (like the 2D game does via `BattleBackgroundResolver`).

6. **Move detail panel (TYPE/PP)** — Ported but only renders when KermFont is available. SpriteFont fallback needs the panel below the move grid.

7. **Overlay integration** — Bag and Pokemon screens need callbacks wired (`_onBagSelected`, `_onPokemonSelected`) with actual party/inventory data.

---

## 3. Optimizations — Prime Suspects

### 3.1 Async Model Preloading (Highest Priority)

**Problem:** `ModelLoader.Load()` parses Collada XML, builds vertex buffers, loads textures — ~50-200ms per model. Loading on battle entry = visible freeze.

**Solution:** Background preload on map/world load:

```
MapLoad → EncounterRegistry.LoadForMap(mapId)
        → Extract all SpeciesIds from encounter tables
        → PokemonModelCache.PreloadAsync(speciesIds, graphicsDevice)
        → Background thread: parse DAE, build CPU-side data
        → Main thread: create GPU resources (vertex buffers, textures)
```

Key constraint: GPU resource creation (VertexBuffer, Texture2D) must happen on the main thread. The background thread can do file I/O and XML parsing, then queue GPU uploads.

### 3.2 World-Level Preloading

**Problem:** Maps connect into worlds. Player can walk between maps without loading screens. Need models for ALL maps in the current world, not just the current map.

**Solution:** When a world loads, collect species from all maps in that world:

```csharp
// Pseudocode
HashSet<int> worldSpecies = new();
foreach (var mapId in world.MapIds)
{
    var encounters = EncounterRegistry.PeekEncounters(mapId);
    foreach (var entry in encounters)
        worldSpecies.Add(entry.SpeciesId);
}
// Also add party Pokemon
foreach (var pokemon in party)
    worldSpecies.Add(pokemon.SpeciesId);

modelCache.PreloadAsync(worldSpecies);
```

### 3.3 Model Cache with LRU Eviction

**Problem:** Can't keep every model in memory forever with 802 species.

**Solution:** LRU cache with capacity limit. Models for the current world stay pinned. When loading a new world, evict models that aren't needed.

### 3.4 Battle Scene Model Sharing

**Problem:** `LoadBattleModels()` loads all 4 background sets (Grass, TallGrass, Cave, Dark) at startup. The model cache already deduplicates (Grass bg shared with TallGrass), but unused sets waste VRAM.

**Solution:** Lazy-load battle backgrounds too — only load the set needed for the current map's encounter types. A grass-only map doesn't need Cave/Dark backgrounds.

---

## 4. Step by Step — Getting the App Fully Working

### Phase 1: Battle Screen Verification

```bash
cd D:\Projects\Starfield2026dotnet build src/Starfield.3D/Starfield.3D.csproj
dotnet run --project src/Starfield.3D/Starfield.3D.csproj
```

1. Set `DebugStartInBattle = true` in 3D Game1.cs
2. Verify: 3D battle background renders (Grass scene with platforms)
3. Verify: Camera starts zoomed on foe, message "Wild X appeared!" shows
4. Verify: Pressing Enter/Z dismisses message, camera zooms out with ease-out
5. Verify: "Go! Y!" message shows, ally info bar appears
6. Verify: Main menu (Fight/Bag/Pokemon/Run) appears with "What will you do?"
7. Verify: Fight opens move grid, moves have PP display
8. Verify: Run shows "You got away safely!" and exits battle

### Phase 2: Model Loading Verification

1. Confirm `BattleBG/` directory exists relative to the 3D project
2. Confirm `ModelLoader.InitializeDevPaths()` resolves `Pokemon3D/` correctly
3. Check console for `[Battle3D] Pokemon #N (Name): X meshes` debug output
4. If models don't load: check `battle3d_log.txt` in build output directory

### Phase 3: Async Preloading Implementation

1. Add `PokemonModelCache` class to Core
2. Add `EncounterRegistry.GetAllSpeciesForMap(mapId)` helper
3. Wire cache into `BattleScreen3D.EnterBattle()` — pull from cache first
4. Wire preload trigger into map load event
5. Test: model loads should happen during map transition fade, not on battle entry

### Phase 4: Map Editor Enhancement

1. Add `/api/species` route to BgEditor backend (reads `species.json`)
2. Add `/api/encounters/:mapId` GET/PUT routes
3. Build encounter editor UI panel in EditorPage.tsx
4. Test: create encounter table, verify JSON output matches schema

---

## 5. How to Start / Test

### 3D POC (MonoGame)

```bash
dotnet run --project src/Starfield.3D/Starfield.3D.csproj
```

- WASD: move, Shift: run, Space: jump, Tab/Esc: character select, Enter: pause
- Set `DebugStartInBattle = true` to test battle screen directly
- Arrow keys / Enter / Z: navigate battle menus
- Battle uses test Pokemon (Charmander vs Pidgey by default)

### 2D Game (MonoGame)

```bash
dotnet run --project src/Starfield/Starfield.csproj
```

- Arrow keys: move, E/Enter/Space: confirm, Esc: pause menu
- Walk into tall grass/fire tiles for random encounters
- Full battle system with 3D scene backgrounds

### BgEditor (Electron + React)

```bash
cd src/Starfield.BgEditor
npm install
npm run dev        # starts both backend (Express :3001) and frontend (Vite :5173)
```

- EditorPage: tile/map editing (currently backgrounds only)
- AnimationsPage: animation clip preview and semantic tagging
- ExtractionPage: GARC extraction pipeline
- ToolsPage: batch operations

---

## 6. Known Issues & Strategies

### Issue 1: BattleBG Models May Not Exist in 3D POC Build Output

The 3D POC looks for `BattleBG/` at `AppDomain.CurrentDomain.BaseDirectory` first, then falls back to the Assets project source directory. If neither exists, the fallback is a solid dark background.

**Strategy:** Add a post-build copy target in the 3D csproj to copy `BattleBG/` from Assets, or add a symlink. Alternatively, make the path configurable.

### Issue 2: Only 26 of 802 Species Have Exported Models

The `Pokemon3D/` directory only has models for species 1-14 plus some variants. Most encounter tables reference species (16, 19, 21, 25, etc.) that don't have models yet.

**Strategy:** `LoadPokemonModel()` already returns null gracefully when the model folder doesn't exist. The battle renders without models — just shows the background scene. Re-export is needed but non-blocking.

### Issue 3: GPU Resource Creation on Background Thread

MonoGame/OpenGL requires GPU resource creation (VertexBuffer, Texture2D) on the main thread. Naive async loading will crash.

**Strategy:** Split the loading pipeline:
1. **Background thread:** File I/O, XML parsing, vertex/index array building (CPU work)
2. **Main thread:** Create VertexBuffer/IndexBuffer/Texture2D from the pre-built arrays
3. Use a `ConcurrentQueue<PendingModel>` that the main thread drains each frame

### Issue 4: Map Editor Has No Encounter Editing UI

Encounters are currently hand-authored JSON files. No way to visually assign species to maps.

**Strategy:** The data pipeline already exists:
- `species.json` has all 802 species with names, types, stats
- `{mapId}.json` encounter files have a clean schema
- BgEditor already has Express routes for manifests/textures
- Add: species API route, encounter CRUD routes, React encounter panel
- The encounter panel needs: species search/autocomplete, level range sliders, weight inputs, encounter type dropdown

---

## 7. New Architecture & Features

### PokemonModelCache (New Class — Core)

Central async cache for Pokemon 3D models:

```
PokemonModelCache
├── PreloadForWorld(worldId)      — collect species from all maps, load async
├── PreloadForMap(mapId)          — collect species from one map, load async
├── PreloadParty(party)           — load party Pokemon models
├── Get(speciesId) → model?       — instant lookup, null if not loaded
├── IsLoading(speciesId) → bool   — check if background load in progress
└── DrainPendingUploads(device)   — main thread: create GPU resources
```

### EncounterRegistry Enhancements

```
EncounterRegistry
├── GetAllSpeciesForMap(mapId) → HashSet<int>    — NEW: all species in a map's tables
├── GetAllSpeciesForWorld(worldId) → HashSet<int> — NEW: union across all world maps
└── PeekEncounters(mapId) → MapEncounterData?     — NEW: load without setting as current
```

### Map Editor Encounter Panel (BgEditor)

New panel in EditorPage.tsx:

```
┌─ Encounters ──────────────────────────────┐
│ Type: [tall_grass ▾]  Rate: [26]          │
│                                           │
│ #016 Pidgey     Lv 5-8   Weight: 30  [x] │
│ #019 Rattata    Lv 5-7   Weight: 25  [x] │
│ #025 Pikachu    Lv 6-9   Weight: 10  [x] │
│                                           │
│ [+ Add Species]  [+ Add Encounter Group]  │
└───────────────────────────────────────────┘
```

Backend routes:
- `GET  /api/species` — returns species list (id, name, type1, type2) from `species.json`
- `GET  /api/encounters/:mapId` — returns encounter data
- `PUT  /api/encounters/:mapId` — saves encounter data

---

## 8. Quick Wins

1. **`EncounterRegistry.GetAllSpeciesForMap()`** — 10 lines of code, unlocks the entire preloading pipeline. Just iterate encounter groups and collect species IDs into a HashSet.

2. **Species API route** — Read `species.json`, return as JSON. The data is already in the right format. One Express route, maybe 15 lines.

3. **Wire `BattleBackgroundResolver` in 3D POC** — The 2D game already resolves encounter tile type → battle background enum. The 3D POC just needs to call `SetBattleBackground()` before `EnterBattle()`.

4. **Copy BattleBG to 3D build output** — Add one MSBuild target to the 3D csproj. Eliminates the dev-path fallback hack.

---

## File Reference

| File | Lines | Role |
|------|-------|------|
| `src/Starfield.Core/Battle/BattleScreen3D.cs` | 802 | Full battle screen: 3D scene + UI + state machine |
| `src/Starfield.3D/Game1.cs` | 691 | 3D POC shell: overworld + delegates to BattleScreen3D |
| `src/Starfield/Game1.cs` | 1312 | 2D game: original battle code (reference) |
| `src/Starfield.Core/Encounters/EncounterRegistry.cs` | 186 | Map encounter loading + weighted random selection |
| `src/Starfield.Core/Pokemon/SpeciesRegistry.cs` | ~120 | Species data: name, stats, ModelFolder |
| `src/Starfield.Assets/BattleModelLoader.cs` | 302 | DAE model loading for battle backgrounds |
| `src/Starfield.Assets/SkeletalModelData.cs` | ~300 | Skinned Pokemon model with animation |
| `src/Starfield.Assets/Data/species.json` | 12031 | 802 species definitions |
| `src/Starfield.BgEditor/` | — | Electron map/animation editor |
