# Lessons Learned & Handoff: 3D Battle Screen Implementation

**Date:** 2026-02-15
**Sprint:** Encounter Detection, Battle Transitions, 3D Battle Rendering

---

## 1. What We Accomplished

### Encounter Detection System (Complete)
- Detects when the player steps onto `TileCategory.Encounter` tiles (overlay layer first, then base)
- 1-in-10 random chance per step onto encounter grass
- Checks both overlay and base layers — tall grass (ID 72) is placed as overlay, not base
- Integrated into `GameWorld.Update()` after player movement (step 7b)

### Battle Transition System (Complete)
- White flash (0.15s) → fade to black (0.3s) → battle screen → fade from black on exit
- Extended existing `TransitionState` enum with `FlashWhite`, `FadingToBattle`, `FadingFromBattle`
- Battle exit reuses existing `FadingOut`/`FadingIn` states via `_exitingBattle` flag
- Player resumes on the same tile after battle ends

### Game State System (Complete)
- `GameState { Overworld, Battle }` enum in `GameWorld`
- `Game1.Draw()` branches rendering: overworld tiles vs 3D battle scene
- `Game1.Update()` handles battle-specific input independently of `GameWorld.Update()`

### 3D Battle Rendering (Complete — Phase 2)
- **AssimpNet 4.1.0** loads `.dae` (Collada) models at runtime — no Content Pipeline conversion needed
- Three models loaded: `Grass.dae` (background sky/field), `GrassAlly.dae` (ally platform), `GrassFoe.dae` (foe platform)
- All textures load from PNG files referenced in the .dae materials
- Camera position and FOV replicated from old engine: pos=(7,7,15), yaw=-22°, pitch=13°, FOV=26°
- Platform positions from old engine: ally at (0,-0.20,3), foe at (0,-0.20,-15)
- `PointClamp` sampler for pixel-art texture filtering

### Battle Camera Animation (Complete)
- Camera starts zoomed in on foe platform: (6.9, 7, 4.6) — old engine's foe focus position
- Three-phase battle flow: `Intro` → `ZoomOut` → `Menu`
- **Intro**: "Wild POKEMON appeared!" text, camera on foe, press any key to continue
- **ZoomOut**: Smooth ease-out camera pan to default battle view (0.4s)
- **Menu**: 2x2 action grid (Fight/Bag/Pokemon/Run) with keyboard + mouse navigation

### Battle UI (Complete — Placeholder)
- 2x2 Pokemon-style action menu (Fight, Bag, Pokemon, Run) with arrow key/WASD navigation
- Mouse hover highlighting and click support
- "What will you do?" text box alongside the menu
- `DrawTextBox()` helper for bordered panels
- Only "Run" is functional — exits battle with fade transition

### MonoGame Content Pipeline (Complete)
- SpriteFont (`BattleFont.spritefont` — Arial Bold 14pt) compiled via MGCB
- Content Pipeline lives in `Starfield.Assets` project (not Core, not game shell)
- Local tool manifest (`.config/dotnet-tools.json`) with `dotnet-mgcb 3.8.4.1`

### Debug Mode (Complete)
- `DebugStartInBattle = true` const in `Game1` — launches directly into battle screen
- Bypasses encounter detection and transition for faster iteration

---

## 2. What Work Remains

### Phase 3: Platform Positioning & Camera Polish
- Verify platform positions match the classic Pokemon battle layout visually
- The old engine uses a custom projection matrix (DS BW2-derived, not standard perspective) — our standard `CreatePerspectiveFieldOfView` is an approximation. May need the exact 4x4 matrix for pixel-perfect accuracy.
- Lighting: old engine uses 4 point lights + ambient at 0.85. We currently render unlit (`LightingEnabled = false`). Toggle this once geometry is confirmed correct.

### Phase 4: Pokemon Sprites as Billboards
- Render Pokemon front/back sprites as camera-facing quads at platform positions
- Player's Pokemon: position (-0.5, floorY, 2) relative to ally platform, 2x scale (back sprite)
- Enemy Pokemon: position (1, floorY, -13.5) relative to foe platform, 1x scale (front sprite)
- Need placeholder or real Pokemon sprite PNGs

### Phase 5: UI Polish
- HP bars (green/yellow/red based on %, 3-layer rectangle drawing)
- Styled text panels (semi-transparent dark with thin white borders — already started)
- Pokemon name/level labels above HP bars
- Typewriter text effect for battle messages

### Battle Logic (Not Started)
- Pokemon/Move/Type data structures
- Damage calculation (simplified Gen 1 formula)
- Turn flow: player picks move → speed compare → attacks → HP update → faint check
- Encounter tables: which Pokemon appear in which grass areas
- See `docs/08-BATTLE-SYSTEM-DESIGN.md` for full specification

### Map Editor Metadata
- `codeGenService.ts` was updated with tile legend template feature
- `default.json` updated with tile IDs 72 (Dense Grass) and 80 (Cliff Wall)
- Generated `.g.cs` files have NOT been re-exported from the editor yet

---

## 3. Optimizations — Prime Suspects

### 3a. Model Loading is Synchronous and Uncached
`BattleModelLoader.Load()` runs on the main thread during `LoadContent()`, blocking startup. Each battle scene type (Grass, Cave, Water) loads 3 models × ~400 vertices each. Currently only Grass is loaded.

**Recommendation:** Cache loaded `BattleModelData` by scene type. Load asynchronously on a background thread and show a loading indicator. For now this is fine — the models are small (total ~1,300 vertices across 10 meshes).

### 3b. Texture Deduplication
The ally and foe platforms share the same texture files (`batt_bk01.png`, `batt_stage06.png`), but `BattleModelLoader` loads them as separate `Texture2D` instances. With 3 battle scene types this doubles texture memory for no reason.

**Recommendation:** Add a `Dictionary<string, Texture2D>` texture cache to `BattleModelLoader` that deduplicates by file path. Simple change — check cache before `Texture2D.FromStream()`.

### 3c. GPU Buffer Strategy
All buffers are created with `BufferUsage.WriteOnly` (correct for static geometry). However, `BoundsMin`/`BoundsMax` had to be computed from CPU-side vertex data before GPU upload because `GetData()` is not supported on WriteOnly buffers.

**Lesson:** Never call `GetData()` on a WriteOnly buffer — it throws and can silently prevent all subsequent code in the same try block from executing. Track any CPU-side metadata (bounds, vertex counts) BEFORE creating the GPU buffer.

### 3d. BasicEffect Limitations
`BasicEffect` supports one texture, one directional light, and no custom shaders. The old engine uses custom shaders for effects like team-colored platforms and Pokemon sprite outlines.

**Recommendation:** `BasicEffect` is sufficient for the current scope. When visual quality needs to improve, migrate to custom `Effect` files (.fx) compiled through the Content Pipeline. This is a significant refactor — delay until the battle system is functionally complete.

---

## 4. Step-by-Step to Get Fully Working (No Errors)

### Step 1: Verify Clean Build
```bash
cd D:\Projects\Starfield2026dotnet build
```
Currently builds with 0 errors.

### Step 2: Run with Debug Battle Mode
```bash
dotnet run --project src/Starfield/Starfield.csproj
```
With `DebugStartInBattle = true`, the game launches directly into the battle screen. Verify:
- 3D background renders (sky and grass field visible)
- Both platforms are visible
- Camera starts zoomed in on foe platform
- "Wild POKEMON appeared!" text visible in bottom panel
- Press any key → camera zooms out smoothly
- Menu appears with Fight/Bag/Pokemon/Run
- Arrow keys navigate the menu, selected item is yellow with `>` arrow
- Click or Enter on "Run" → fades to black → returns to overworld

### Step 3: Test Encounter Flow
Set `DebugStartInBattle = false` in `Game1.cs`. Run the game, walk into tall grass (overlay tile ID 72). After a few steps, encounter should trigger:
- White flash → fade to black → battle screen appears
- Select Run → fade to black → overworld resumes on the same tile

### Step 4: Check Model Loading Logs
After running, check `bin/Debug/net9.0/battle3d_log.txt`:
```
[Battle3D] Grass.dae: 2 meshes, 422 verts, 2 textured, bounds: (-69.6,0.0,-69.5) to (69.6,60.1,29.8)
[Battle3D] GrassAlly.dae: 4 meshes, 432 verts, 4 textured, bounds: (-5.5,0.2,-5.5) to (5.5,1.2,5.5)
[Battle3D] GrassFoe.dae: 4 meshes, 440 verts, 4 textured, bounds: (-5.5,0.2,-5.5) to (5.5,1.0,5.5)
```
If this file shows `FAILED:` instead, the .dae files are missing from the output directory. Check that `BattleBG` assets are being copied (Content item in `.csproj`).

### Step 5: Verify Content Pipeline
The SpriteFont is compiled by the MonoGame Content Pipeline during build. If font loading fails:
```bash
dotnet tool restore   # installs dotnet-mgcb locally
dotnet build          # Content Pipeline runs as build target
```
The compiled `BattleFont.xnb` should appear in `bin/Debug/net9.0/Content/Fonts/`.

---

## 5. How to Start/Test

### Run the Game
```bash
cd D:\Projects\Starfield2026dotnet run --project src/Starfield/Starfield.csproj
```

### Debug Battle Screen Directly
In `src/Starfield/Game1.cs` line 50:
```csharp
private const bool DebugStartInBattle = true;  // set to false for normal gameplay
```

### Run the Map Editor
```bash
cd D:\Projects\Starfield2026\src\Starfield.MapEditor
npm run dev
```
Open `http://localhost:5173`. Import/export `.g.cs` maps via File menu.

### Verify Builds
```bash
# C# game + all libraries
dotnet build D:\Projects\Starfield2026
# TypeScript type check (map editor)
cd D:\Projects\Starfield2026\src\Starfield.MapEditor && npx tsc --noEmit
```

### Check 3D Model Loading
After running, inspect `bin/Debug/net9.0/battle3d_log.txt` for mesh counts, vertex counts, and bounding box data. If the file contains error messages, the .dae assets are missing or AssimpNet failed to load them.

---

## 6. Known Issues + Strategies

### Issue 1: Camera Angle Approximation
**Current state:** We use `Matrix.CreatePerspectiveFieldOfView(26°)` to approximate the old engine's custom projection matrix. The old engine uses a hardcoded 4x4 matrix derived from DS BW2 with 12-bit fractional precision. Our approximation is close but not pixel-perfect.

**Strategy A — Use the exact custom matrix:** Construct a `Matrix` with the old engine's exact values: M11=3.209, M22=4.333, M33=-1.004, M34=-1, M43=-2.004. Swap out `CreatePerspectiveFieldOfView`. Risk: MonoGame's DesktopGL may have different matrix conventions than the old engine's System.Numerics.

**Strategy B — Tune visually:** Adjust FOV and near/far plane until the view matches screenshots of the old engine. Faster iteration, good enough for gameplay.

**Strategy C — Dual rendering debug:** Render both the old matrix and our approximation side-by-side on a split screen. Compare geometry placement and find the correct values empirically.

### Issue 2: No Pokemon Sprites on Platforms
**Current state:** Platforms render but no Pokemon are visible. The battle screen is 3D background + 2D UI only.

**Strategy A — Billboard quads:** Create a textured quad (2 triangles) at each platform's sprite position. Orient the quad to always face the camera. Use `Texture2D.FromStream` to load Pokemon PNGs. This matches how the old engine renders sprites in 3D space.

**Strategy B — 2D overlay sprites:** Draw Pokemon sprites in 2D screen space using SpriteBatch, positioned to align with the 3D platforms. Simpler to implement but breaks the 3D illusion during camera movement.

**Strategy C — Pre-rendered scene:** Render the 3D background to a RenderTarget2D once, then composite Pokemon sprites and UI in 2D. Eliminates per-frame 3D rendering cost and simplifies sprite placement. Loses camera animation capability.

### Issue 3: Battle Logic Does Not Exist
**Current state:** Only "Run" works. Fight/Bag/Pokemon do nothing. No Pokemon data, no damage, no turns.

**Strategy A — Hardcoded starter battle:** Create 2 Pokemon (Bulbasaur vs Rattata) with 4 moves each, implement simplified damage formula, wire up the turn flow. Get the complete loop working end-to-end before adding data-driven content.

**Strategy B — PBE integration:** Add the PokemonBattleEngine NuGet package used by the old engine. It handles all battle math, type charts, abilities, and status effects. Communicate through its packet/event system. Heavy but complete.

**Strategy C — JSON-driven data:** Define Pokemon, moves, and encounter tables in JSON files. Build a minimal battle engine that reads from them. More flexible than hardcoding, but more work upfront.

### Issue 4: Single Battle Scene Type
**Current state:** Only the Grass scene is implemented. The old engine has ~20 battle backgrounds (Cave, Water, Desert, Building, etc.).

**Strategy A — Scene registry:** Create a `BattleSceneRegistry` that maps scene IDs to .dae file paths. Load scenes on demand when a battle starts. Each map zone specifies its battle scene ID.

**Strategy B — Load all at startup:** Pre-load all battle scenes during `LoadContent()`. Fast scene switching but higher memory usage and slower startup.

**Strategy C — Scene-per-biome:** Map each `OverlayBehavior` value (wild_encounter, cave_encounter, water_encounter) to a battle scene. Automatically selects the correct scene based on the tile the encounter triggered on.

---

## 7. Architecture & New Features

### Current Battle Architecture

```
Game1.cs (rendering + input)
├── LoadBattleModels()          → BattleModelLoader.Load() × 3
├── Update()
│   ├── Battle input (phase-aware)
│   │   ├── Intro: any key → ZoomOut
│   │   ├── ZoomOut: wait for animation
│   │   └── Menu: arrow nav + confirm
│   ├── Camera lerp animation
│   └── State transition detection
├── DrawBattlePlaceholder()
│   ├── DrawBattle3D()          → 3D background + platforms
│   └── 2D UI overlay           → text boxes + action menu
└── ConfirmBattleMenu()         → Run exits battle

GameWorld.cs (state + encounters)
├── GameState { Overworld, Battle }
├── TransitionState { ..., FlashWhite, FadingToBattle, FadingFromBattle }
├── IsEncounterTile()           → checks overlay then base layer
├── BeginBattleTransition()     → flash → fade → state change
├── ExitBattle()                → _exitingBattle flag → fade out/in
└── DebugEnterBattle()          → instant state change

BattleModelLoader.cs (asset loading)
├── BattleMeshData              → VertexBuffer + IndexBuffer + Texture2D
├── BattleModelData             → List<BattleMeshData> + bounds + Draw()
└── BattleModelLoader.Load()    → AssimpNet → extract verts/indices/textures → GPU buffers
```

### Key Data Flow

```
Encounter tile stepped on
  → GameWorld.BeginBattleTransition()
  → FlashWhite (0.15s white overlay)
  → FadingToBattle (0.3s black fade)
  → State = Battle
  → FadingFromBattle (0.3s black fade out, revealing battle screen)

Game1 detects State == Battle
  → DrawBattle3D() renders 3D scene
  → BattlePhase.Intro: camera on foe, encounter text
  → User presses key → BattlePhase.ZoomOut
  → Camera lerps from (6.9,7,4.6) to (7,7,15) over 0.4s
  → BattlePhase.Menu: action grid visible

User selects Run
  → GameWorld.ExitBattle()
  → _exitingBattle = true
  → FadingOut (0.3s black fade)
  → State = Overworld (at peak black)
  → FadingIn (0.3s black fade out, revealing overworld)
```

### Files Created This Sprint

| File | Purpose |
|------|---------|
| `Assets/BattleModelLoader.cs` | AssimpNet .dae loader — extracts meshes, textures, bounds → GPU buffers |
| `Assets/BattleBG/Grass/*` | Grass background model + textures (Grass.dae, batt_sky01.png, batt_field01.png) |
| `Assets/BattleBG/PlatformGrassAlly/*` | Ally platform model + textures |
| `Assets/BattleBG/PlatformGrassFoe/*` | Foe platform model + textures |
| `Assets/Content/Content.mgcb` | MonoGame Content Pipeline project |
| `Assets/Content/Fonts/BattleFont.spritefont` | Arial Bold 14pt SpriteFont definition |
| `.config/dotnet-tools.json` | Local tool manifest (dotnet-mgcb) |

### Files Modified This Sprint

| File | Changes |
|------|---------|
| `Game1.cs` | Battle rendering (3D + 2D), camera animation, phase system, debug mode, input handling |
| `GameWorld.cs` | GameState enum, encounter detection, battle transitions (flash + fade), ExitBattle, DebugEnterBattle |
| `Assets/Starfield.Assets.csproj` | Added AssimpNet 4.1.0, Content.Builder.Task, BattleBG content copy |
| `MapEditor/services/codeGenService.ts` | Tile legend template, isTerrainTile update |
| `MapEditor/data/registries/default.json` | Added tile IDs 72, 80 with categories |

### Quick Wins

1. **Pokemon sprite billboards** — Create a textured quad at each platform position. Load a placeholder PNG (colored square with name). ~40 lines. Immediately makes the battle screen look populated.

2. **Enable lighting** — Set `LightingEnabled = true` and configure `AmbientLightColor` + `EnableDefaultLighting()` in `DrawBattle3D()`. ~3 lines changed. Adds depth and dimension to the flat-looking textured models.

3. **Multiple battle scenes** — Copy Cave and Water .dae assets from the old engine the same way Grass was copied. Add a `battleSceneId` parameter. Each encounter tile type maps to a scene. ~20 lines of loader logic.

4. **Typewriter text effect** — Track `_visibleChars` counter, increment by `charsPerSecond * deltaTime`, draw `message.Substring(0, _visibleChars)`. ~15 lines. Classic Pokemon feel for battle messages.

5. **HP bar rendering** — Three-layer rectangle (border → empty → fill), color changes at 50%/20% thresholds. Already designed in `08-BATTLE-SYSTEM-DESIGN.md`. ~20 lines.

### New Feature Ideas

- **Battle scene transitions** — Cross-fade or dissolve between scenes when switching battle backgrounds
- **Platform shadow** — Render a dark ellipse under each platform for ground contact
- **Encounter animation variety** — Different flash patterns for rare encounters (double flash, colored flash)
- **Camera shake** — Small random offset on hit for impact feedback
- **Day/night lighting** — Tint the battle scene based on game time (warm sunset, blue night)

---

## 8. Critical Bugs Fixed This Sprint

### Bug: `GetData()` on WriteOnly GPU Buffer
**Symptom:** Black screen — no 3D models rendering despite successful .dae loading.
**Root cause:** `LogModelBounds()` called `VertexBuffer.GetData()` on buffers created with `BufferUsage.WriteOnly`. This threw a `NotSupportedException` inside the `try` block of `LoadBattleModels()`, which silently caught the exception and left all model references as `null`. With no models loaded, `DrawBattle3D()` was never called, and the fallback dark background showed instead.
**Fix:** Compute `BoundsMin`/`BoundsMax` from CPU-side vertex arrays during `Load()` BEFORE creating GPU buffers. `LogModelBounds()` reads pre-computed properties instead of GPU data.
**Lesson:** Never read from WriteOnly GPU buffers. Track any metadata you need before the GPU upload. A single exception inside a shared try block can silently prevent unrelated subsequent code from executing.

### Bug: Keyboard Input Not Working on Battle Screen
**Symptom:** Enter/Space/E keys did nothing on the battle screen.
**Root cause:** `GameWorld.Update()` returns early when `State == Battle`, which skipped `Input.Update()`. Battle input was checked in `Game1.Update()` using `Keyboard.GetState()` directly, but without tracking previous state for rising-edge detection.
**Fix:** Added `_previousKeyboardState` tracking in `Game1.Update()` with a `KeyPressed()` helper that compares current vs previous frame.
**Lesson:** When bypassing the game's input system for a new screen, you must implement your own rising-edge detection.

### Bug: Ambiguous `PrimitiveType` Reference
**Symptom:** Build error — `PrimitiveType` is ambiguous between `Assimp.PrimitiveType` and `Microsoft.Xna.Framework.Graphics.PrimitiveType`.
**Fix:** Full qualification: `Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList`.
**Lesson:** When mixing Assimp and MonoGame namespaces, several types collide (PrimitiveType, Matrix, Vector3). Use full qualification or targeted `using` aliases.
