# 03 — Lessons Learned & Handoff Document

**Date:** 2026-02-19  
**Session Focus:** Establishing Starfield 2026 project foundation — 3D MonoGame with dual-screen architecture  
**Status:** Overworld screen functional, Space flight screen needs fundamental rework  

---

## 1. What We Accomplished

### Project Structure (✅ Complete)
- Created `Starfield2026.sln` with two projects:
  - **`Starfield2026.Core`** (.NET 9.0 class library) — all shared game logic, renderers, screens, input
  - **`Starfield2026.3D`** (.NET 9.0 desktop app) — MonoGame entry point, screen management, HUD
- `Starfield2026.Core` is referenced by `Starfield2026.3D` via ProjectReference
- Dependencies: `MonoGame.Framework.DesktopGL 3.8.4.1`

### Core Rendering Components (✅ Complete)
| Component | File | Status |
|-----------|------|--------|
| `Camera3D` | `Core/Rendering/Camera3D.cs` | ✅ Third-person orbit camera with yaw/pitch/distance, follow, zoom |
| `GridRenderer` | `Core/Rendering/GridRenderer.cs` | ✅ Configurable grid planes (Horizontal, WallX, WallZ orientations) |
| `CubeRenderer` | `Core/Rendering/CubeRenderer.cs` | ✅ Colored cube with per-face colors, position/rotation/scale |
| `StarfieldRenderer` | `Core/Rendering/StarfieldRenderer.cs` | ✅ Streaming star particles with streak trails |

### Input System (✅ Complete)
| Component | File | Purpose |
|-----------|------|---------|
| `InputSnapshot` | `Core/Input/InputSnapshot.cs` | Per-frame input state: movement axes, camera, actions, raw state |
| `InputManager` | `Core/Input/InputManager.cs` | Polls hardware once per frame, produces `InputSnapshot` |

### Screen System (✅ Architecture, ⚠️ Space Screen Needs Work)
| Component | File | Status |
|-----------|------|--------|
| `IGameScreen` | `Core/Screens/IGameScreen.cs` | ✅ Clean interface: `Initialize`, `Update(gameTime, input)`, `Draw`, `OnEnter/OnExit` |
| `OverworldScreen` | `Core/Screens/OverworldScreen.cs` | ✅ Working — 3D exploration, camera-relative movement, random encounters |
| `SpaceFlightScreen` | `Core/Screens/SpaceFlightScreen.cs` | ❌ **Needs fundamental rework** — see section 3 |

### Game Shell (✅ Complete)
| Component | File | Purpose |
|-----------|------|---------|
| `Starfield2026Game` | `3D/Starfield2026Game.cs` | Screen transitions (fade to black), HUD overlay, input routing |
| `Program.cs` | `3D/Program.cs` | Entry point |

### Key Fixes Applied
- `IndexBuffer` constructor: replaced `IndexElementSize.SixteenBits` with `typeof(short)` for MonoGame 3.8.4.1 compatibility
- Renamed `Game1` → `Starfield2026Game` for clarity
- ESC key toggles between worlds (was previously Exit)

---

## 2. What Work Remains

### Critical (Blocks Gameplay)
1. **Space Flight Screen** — The core problem is the illusion of movement through space. Multiple approaches were attempted (scrolling grid, static grid + moving cube, grid-centered-on-cube tiling) and none produced the desired "flying through an endless grid" feel. This is the **#1 priority**.
2. **Battle Screen** — Not yet implemented. Needs design for turn-based creature combat.
3. **SpriteFont / Text Rendering** — Currently using colored pixel blocks as character placeholders. Need to load a real `SpriteFont` for readable HUD text.

### Important (Core Experience)
4. **Starfield integration with cube movement** — The starfield renders at world origin and doesn't track with the player. Needs to be relative to the camera or cube position.
5. **Sound system** — No audio implemented yet.
6. **Creature data model** — No Pokemon-like creature definitions, stats, or catch mechanics.

### Nice to Have
7. **Skybox** — Dark space background is flat. A proper skybox would add depth.
8. **Player model** — Replace cube with actual 3D model (ship for space, character for overworld).
9. **Particle effects** — Thruster glow behind the cube, encounter sparkles.

---

## 3. Optimizations — Prime Suspects for Space Flight

The space flight screen was reworked 4+ times during this session. Each attempt had a different fundamental flaw. Here's a forensic analysis:

### Suspect 1: Grid Scrolling Math Was Wrong
**What happened:** The original `GridRenderer` used `Math.Floor(offset / spacing) * spacing` to "snap" the translation. This threw away the fractional offset — the grid jumped between identical positions instead of sliding smoothly. Later it was changed to direct translation, but by then the approach had already shifted.

**Root cause:** Misunderstanding of what "infinite scrolling grid" means mathematically. The correct formula is: `translation = offset % spacing` (modulo), which keeps the fractional part and wraps seamlessly since all grid cells look identical.

**Fix:** The `GridRenderer.Draw()` should use `%` (modulo) on the scroll offset, not `Math.Floor`. This preserves the sub-cell fractional movement that makes the grid appear to slide smoothly. The grid geometry stays at origin and only shifts by 0 to `spacing` units.

### Suspect 2: Conflating Cube Movement With Camera Movement
**What happened:** When the cube "dodged" left/right/up/down, the camera followed with a fixed offset. This made it look like the camera was moving and the grid was stationary, rather than the cube flying through the grid while the camera observes.

**Root cause:** No separation between "the world" and "the player's viewport." The camera was rigidly locked to the cube (fixed Vector3 offset), so dodging the cube just panned the entire view. The grid — being either static or snapped — appeared motionless.

**Fix strategy:** Consider a *loose follow* camera with lerp-based smoothing instead of a rigid offset. `camPos = Vector3.Lerp(camPos, targetPos, smoothFactor * dt)`. This creates the parallax effect where the grid visibly shifts relative to the cube as it dodges.

### Suspect 3: The Grid Wasn't Visually Conveying Motion
**What happened:** Even when the grid was technically "scrolling," the visual effect wasn't compelling. The grid lines were too far from the camera (Floor at Y=-40, Ceiling at Y=+80) to feel like you were flying *through* them.

**Root cause:** In classic Starfield, the grid is close to the player and the perspective makes the lines clearly race past. When the grid is far below/above, the scroll is barely perceptible — especially from a trailing camera angle.

**Fix strategy:** 
- Bring the floor closer (Y = -5 to -8, not -40)
- Smaller grid spacing (2-3 units, not 5-8)
- Camera lower (Y offset = 3-4 above cube, not 15)
- Wider FOV so grid lines stretch toward the horizon

### Suspect 4: No Reference Implementation Was Studied
**What happened:** The space flight screen was built from instinct, with each iteration reactively fixing the last complaint without a clear reference design.

**Root cause:** Without a concrete reference (e.g. Starfield SNES grid, Tron light-cycle grid, or F-Zero track), the design oscillated between conflicting paradigms:
- "Static grid, moving cube" (cube appears tiny and motionless against vast grid)
- "Scrolling grid, stationary cube" (no sense of actual flight)
- "Both moving" (confusing, no clear visual anchor)

**Fix strategy:** Study a specific reference. The **Starfield SNES** approach is:
1. Camera is behind and slightly above the Arwing
2. A single ground grid scrolls toward the camera at constant speed
3. The Arwing dodges left/right/up/down *relative to the camera* (small offsets)
4. The grid is the only thing creating the illusion of speed
5. The grid extends to the horizon and fades with distance fog

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
```
.NET SDK 9.0.203+
```

### Build & Run
```powershell
cd D:\Projects\Starfield-2026
dotnet build src\Starfield2026.sln
dotnet run --project src\Starfield2026.3D
```

### Fixing the Space Flight Screen (Recommended Approach)
1. **Reset to simplest possible version:**
   ```
   - Fixed camera at position (0, 5, 20) looking at (0, 0, -100)
   - Single floor grid at Y = -5, spacing = 3, halfSize = 200
   - Grid uses modulo scroll: ScrollOffset = new Vector3(0, 0, scrollZ % spacing)
   - scrollZ += speed * dt (one line, always running)
   - Cube at fixed position (0, 0, 0) — DON'T move it yet
   ```
2. **Verify the grid animates smoothly** before adding any cube movement.
3. **Add W/S = up/down, A/D = left/right** as small offsets (±10 max).
4. **Add ceiling grid** using same scroll offset.
5. **Add camera smoothing** (lerp toward cube position, not rigidly locked).
6. **Test at each step** — don't add the next feature until the previous one looks right.

### Controls (Current)
| Key | Space Screen | Overworld Screen |
|-----|-------------|-----------------|
| W/S | Up/Down | Forward/Backward |
| A/D | Left/Right | Left/Right |
| Q/E | — | Camera rotate |
| Shift | — | Run |
| ESC | → Overworld | → Space |

---

## 5. How to Start & Test

### Starting the Application
```powershell
cd D:\Projects\Starfield-2026
dotnet run --project src\Starfield2026.3D
```

### What You Should See
- Game window opens at 1280×720: "Starfield 2026 — Space Creature Trainer"
- Starts in **Space Flight** mode (dark background, grid, cube, starfield)
- **HUD**: Top bar shows title + mode indicator, bottom bar shows controls
- Press **ESC** to transition to **Overworld** (fade to black, then green grid world)
- Press **ESC** again to return to **Space**

### Testing Overworld
- **WASD** moves the cube relative to the camera
- **Q/E** rotates the camera
- **Shift** makes the cube run faster  
- Walk around to trigger random encounters (debug output in console)

### Testing Space Flight
- Current state: cube auto-flies forward, WASD dodges, but the grid doesn't convey motion properly
- This is the primary area needing rework (see Section 3 & 4)

### Verifying the Build
```powershell
dotnet build src\Starfield2026.sln
# Should output: "Build succeeded" with 0 errors
```

---

## 6. Known Issues & New Strategies

### Issue 1: Space flight grid doesn't convey forward motion
**3 Strategies:**
1. **Pure-scroll approach:** Fix the camera in place, don't move it at all. Put the cube at a fixed screen position. Only scroll the grid via modulo on the World matrix translation. The cube dodges by offsetting within the fixed camera view. This is the Starfield SNES approach. The grid is the only moving thing.
2. **Shader-based grid:** Instead of geometry, render the grid floor as a single quad with a procedural shader that scrolls UV coordinates. This gives perfect infinite scrolling with a single draw call and no tiling edge artifacts. The shader calculates grid lines via `frac(worldPos / spacing)`.
3. **Hybrid: cube moves, grid re-centers via modulo in shader.** Move the cube in world space, but the grid shader uses `frac((worldPos - cubePos) / spacing)` so the grid always appears centered on the cube and the lines smoothly scroll as the cube moves.

### Issue 2: Camera feels stiff / rigid
**3 Strategies:**
1. **Lerp-based smooth follow:** `cameraPos = Vector3.Lerp(cameraPos, targetPos, 0.05f)`. The camera lags behind the cube, creating a natural "chase cam" effect. When the cube dodges, the camera catches up over several frames.
2. **Spring-based camera:** Model the camera position as a spring attached to the cube. Apply Hooke's law with damping: `vel += (target - pos) * spring - vel * damping; pos += vel * dt;`. This creates oscillation and organic-feeling motion.
3. **Camera on rails with offset:** Keep the camera on a fixed path (always behind, always looking forward) but offset its look-at target by the cube's dodge position. This keeps the camera stable while the cube appears to move within the frame.

### Issue 3: Starfield doesn't track the player
**Strategy:** Anchor the starfield to the camera position instead of world origin. In `Draw()`, set `_effect.World = Matrix.CreateTranslation(cameraPosition)`. Stars stream past relative to where the camera is, so they work regardless of how far the cube has traveled.

### Issue 4: No readable text (pixel-block HUD)
**Strategy:** Add a `.spritefont` to the Content pipeline and load it via `Content.Load<SpriteFont>()`. MonoGame's content pipeline compiles bitmap fonts at build time. This is a quick win — just needs the font XML file and one line change in `DrawText`.

---

## 7. Architecture & New Features

### Current Architecture
```
Starfield2026.3D (Desktop App)
├── Program.cs               → Entry point
├── Starfield2026Game.cs       → Screen manager, HUD, transitions
│
Starfield2026.Core (Library)
├── Input/
│   ├── InputManager.cs      → Polls hardware → InputSnapshot
│   └── InputSnapshot.cs     → Immutable per-frame input state
├── Rendering/
│   ├── Camera3D.cs          → Third-person orbit camera
│   ├── CubeRenderer.cs      → Colored 3D cube (GPU vertex/index buffers)
│   ├── GridRenderer.cs      → Configurable grid plane (horizontal/wall orientations)
│   └── StarfieldRenderer.cs → Particle star streaks
└── Screens/
    ├── IGameScreen.cs       → Interface: Initialize, Update(input), Draw, OnEnter/OnExit
    ├── SpaceFlightScreen.cs → ❌ Needs rework
    └── OverworldScreen.cs   → ✅ Working
```

### Recommended New Features (Quick Wins)
1. **SpriteFont HUD** — Replace pixel-block text with real font rendering. ~30 min. Dramatically improves visual quality.
2. **Distance fog** — Add `_effect.FogEnabled = true` with `FogStart/End/Color` on BasicEffect. Makes the grid fade to black at the horizon. ~10 min per renderer. Huge visual impact.
3. **Smooth camera** — Replace rigid offset with `Vector3.Lerp()` chase cam. ~20 min. Solves the "stiff" feel.
4. **Gamepad support** — `GamePad.GetState()` in `InputManager`, map left stick to movement, right stick to camera. ~30 min. Big UX win.

### Architectural Improvements to Consider
1. **Screen stack** instead of single `_activeScreen` — allows overlay screens (pause menu, battle transition) without destroying the underlying screen.
2. **Entity system** — Currently the "player" is just a Vector3 + CubeRenderer call. Extract into a `PlayerEntity` class with position, velocity, facing, and visual representation. This enables NPC creatures on the overworld.
3. **Game state persistence** — A `GameState` class holding player position, creatures caught, current screen, etc. Serializable for save/load.
4. **Procedural shader grid** — Replace `GridRenderer`'s vertex-based approach with a fullscreen quad + fragment shader for perfectly infinite, configurable grids. Eliminates all tiling/scrolling edge cases.

---

## 8. Key Lessons for Next Session

1. **Build incrementally.** The space flight screen failed because features were stacked before the foundation (smooth grid scroll) was verified. Next time: get one thing working, test it, then add the next.

2. **Study a reference.** Without a concrete visual reference (Starfield SNES, F-Zero), the design oscillated between incompatible approaches. Pin down a reference video/screenshot before writing code.

3. **Separate visual motion from gameplay motion.** The grid scroll (visual illusion of speed) and cube dodge (gameplay positioning) are independent concerns. Mixing them (cube moves forward + grid re-centers on cube) creates confusion. The grid should scroll on its own; the cube should dodge relative to the camera.

4. **Camera is king.** The camera determines what the player perceives. A rigid camera makes everything feel stiff. Invest early in smooth camera behavior (lerp, spring) before tuning gameplay feel.

5. **The overworld works well.** It uses Camera3D with follow + orbit, which gives good results. The space screen abandoned Camera3D in favor of manual `Matrix.CreateLookAt()`, losing the smooth follow behavior. Consider using Camera3D for both screens.
