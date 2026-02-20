# 05 — Lessons Learned & Handoff: Session 2 — Overworld Tuning, Starfield, Boss, Driving Screen

**Date:** 2026-02-20  
**Session Focus:** Driving screen creation, starfield world-space tracking, boss hit feedback, overworld feel tuning  
**Status:** All three screens functional. Build clean. Ready for gameplay polish.

---

## 1. What We Accomplished

### Driving Screen (✅ New)
- Created `RoadRenderer.cs` — procedural road with asphalt, red/white rumble shoulders, yellow center divider, dashed lane markings
- Created `DrivingScreen.cs` — car physics, `Camera3D` chase-cam, coin spawning on road
- Wired into `Starfield2026Game.cs` — 3-screen cycle: Space → Overworld → Driving → Space

### Starfield World-Space Tracking (✅ Fixed)
**Root cause:** `StarfieldRenderer` spawned/recycled stars at the world origin. As the ship flew into negative Z, stars were left behind.  
**Fix:** Stars now spawn and recycle in world space around a tracked `centerPosition`. All three screens pass their entity position to `Update()`.

### Boss Hit Flash (✅ Fixed)
**Root cause:** `CubeRenderer.Draw(color)` accepted a color parameter but hardcoded `Color(255, 255, 100)` — the `color` argument was completely ignored. Boss was always drawn solid-color.  
**Fix:** `CubeRenderer` now tracks `_lastSolidColor` and rebuilds the vertex buffer when color changes. Boss uses normal per-face colored cube and flashes **solid red** when hit.

### Overworld Feel Tuning (✅ Tuned)
| Parameter | Before | After | Why |
|-----------|--------|-------|-----|
| Walk speed | 8 | 11 | Felt sluggish |
| Run multiplier | 1.8× | 2.0× | Running didn't feel fast enough |
| Jump force | 14 | 18 | Couldn't reach high coins |
| Gravity | 35 | 45 | Jumps felt floaty |
| Camera pitch | -0.35 | -0.18 | Camera looked down instead of behind |
| World bounds | 60 | 180 | Matched grid edge so all coins reachable |
| Window size | 1280×720 | 800×600 | Window was too large on high-DPI displays |

---

## 2. Overworld Settings Deep Dive

All overworld gameplay constants live in two files. Here's every tunable parameter and what it controls.

### PlayerController (`Core/Controllers/PlayerController.cs`)

| Field | Default | Range | Effect |
|-------|---------|-------|--------|
| `_moveSpeed` | `11f` | 5–20 | Walk speed in units/sec. Below 8 feels sluggish, above 15 feels twitchy. |
| `_runMultiplier` | `2.0f` | 1.5–3.0 | Multiplied with `_moveSpeed` when Shift is held. At 2.0, running = 22 units/sec. |
| `_turnSpeed` | `2.5f` | 1.0–5.0 | How fast A/D rotates the player (radians/sec). Higher = snappier turns. |
| `_jumpForce` | `18f` | 10–25 | Initial upward velocity on Space press. Determines jump height via `h = v²/2g`. At 18 with gravity 45, max height ≈ 3.6 units. |
| `_gravity` | `45f` | 20–60 | Downward acceleration. Higher = snappier landings. Must balance with `_jumpForce`. |
| `_worldHalfSize` | `180f` | Any | Player movement clamp in X and Z. Should match grid extent (`GridHalfSize × Spacing`). |

**Jump height formula:** `maxHeight = jumpForce² / (2 × gravity)`
- Current: 18² / (2 × 45) = **3.6 units**
- To reach 5 units high: set jumpForce = √(2 × 45 × 5) = **21.2**

**Ground Y:** Hardcoded at `0.75f` (half-cube height). If you change cube scale, update this.

### OverworldScreen (`Core/Screens/OverworldScreen.cs`)

| Setting | Location | Default | Effect |
|---------|----------|---------|--------|
| Grid `Spacing` | Line ~42 | `2f` | Distance between grid lines. Larger = more open terrain feel. |
| Grid `GridHalfSize` | Line ~43 | `60` | Number of grid cells in each direction. World extent = `GridHalfSize × Spacing`. |
| Grid `GridColor` | Line ~45 | `(40,180,80,150)` | Green tint. Change RGB for different biomes (blue=water, tan=desert). Alpha controls transparency. |
| Camera `Distance` | Line ~54 | `14f` | How far the camera orbits behind the player. |
| Camera `Pitch` | Line ~55 | `-0.18f` | Vertical camera angle. -0.1 = nearly behind player. -0.5 = bird's eye. |
| Camera `MinDistance` | Line ~57 | `4f` | Closest zoom with scroll wheel. |
| Camera `MaxDistance` | Line ~58 | `30f` | Farthest zoom. |
| Camera `FarPlane` | Line ~59 | `200f` | Draw distance. Increase if world gets bigger. |
| `_cameraTurnSpeed` | Line ~20 | `0.5f` | Q/E orbit speed (radians/sec). |
| `_baseDistance` | Line ~21 | `14f` | Camera distance when standing still. |
| `_runDistance` | Line ~22 | `20f` | Camera zooms out to this when running. |
| Starfield count | Line ~62 | `400` | Number of sky particles. Reduce for performance. |
| Coin `SpawnInterval` | Line ~73 | `2f` | Seconds between coin spawns. Lower = more coins. |
| Coin spawn radius | Line ~131 | `WorldHalfSize × 0.7` | Coins spawn within 70% of world bounds. |
| Encounter chance | Line ~35 | `0.08f` | 8% chance per check. |
| Encounter interval | Line ~34 | `0.5f` | Check every 0.5 seconds while moving. |

### How to Change Biome Feel

**Forest** (default):
```csharp
GridColor = new Color(40, 180, 80, 150), // green
```

**Desert:**
```csharp
GridColor = new Color(210, 180, 100, 150), // sandy
Spacing = 4f, // wider, sparse feel
```

**Snow:**
```csharp
GridColor = new Color(180, 200, 255, 180), // icy blue-white
```

**Night:**
```csharp
GridColor = new Color(20, 40, 80, 120), // dark blue
// Also change device.Clear color in Draw()
```

---

## 3. Optimizations — Prime Suspects

### Suspect 1: StarfieldRenderer Allocates LineVerts Array Every Frame
`Draw()` creates `new VertexPositionColor[_starCount * 2]` each frame (600 stars = 2400 bytes allocating + GC pressure every frame).

**Fix:** Pre-allocate `_lineVerts` array in `Initialize()`. Reuse it in `Draw()`. Zero allocations per frame.

### Suspect 2: RoadRenderer Rebuilds Lane Dashes Every Frame
`BuildLaneLines()` creates a `List<VertexPositionColor>` and calls `.ToArray()` each frame.

**Fix:** Pre-allocate a fixed-size array. Write into it with an index counter. Track `_activeDashCount` instead of using array length.

### Suspect 3: CubeRenderer Rebuilds Solid VertexBuffer on Color Change
Boss flash toggles between red and normal every 150ms, causing `BuildSolidCube()` to run on every flash. This creates a new `VertexBuffer` each time.

**Fix:** Use `BasicEffect.DiffuseColor` for tinting instead of rebuilding geometry. Set `VertexColorEnabled = false` momentarily and use `DiffuseColor = flashColor`. No vertex buffer rebuild needed.

### Suspect 4: CoinCollectibleSystem Never Caps Coin Count
Coins accumulate without limit. After 5 minutes on overworld (spawn every 2s), there are 150+ active coins all being iterated in `Update()` and `Draw()`.

**Fix:** Cap at 30–50 active coins. Stop spawning when at cap. Remove oldest uncollected coins when at limit.

---

## 4. Step by Step — Getting App Fully Working

### Prerequisites
- .NET SDK 9.0.203+
- MonoGame (pulled via NuGet automatically)

### Build
```powershell
cd D:\Projects\Starfield-2026
dotnet build src\Starfield2026.sln
```
Expected: `Build succeeded with 0 errors`

### Run
```powershell
dotnet run --project src\Starfield2026.3D
```

### Verify Each Screen
1. **Space Flight** → WASD to move, Space to shoot, `1` to spawn boss, ESC to transition
2. **Overworld** → WASD to explore, Shift to run, Space to jump, Q/E to orbit camera, ESC to transition
3. **Driving** → WASD to drive, Shift for turbo, Space to shoot, ESC to transition
4. **Full cycle** → ESC from each screen transitions to the next in order

---

## 5. How to Start & Test

### Starting
```powershell
cd D:\Projects\Starfield-2026
dotnet run --project src\Starfield2026.3D
```
Window opens at 800×600. Game starts on last saved screen (or Space Flight if first run).

### Full Controls

| Key | Space Flight | Overworld | Driving |
|-----|-------------|-----------|---------|
| W/S | Up/Down | Forward/Back | Accel/Brake |
| A/D | Left/Right | Turn | Steer |
| Space | Shoot | Jump | Shoot |
| Shift | — | Toggle Run | Toggle Turbo |
| Q/E | — | Orbit Camera | — |
| Alt/Ctrl | Gear Up/Down | — | — |
| 1 | Spawn Boss | — | — |
| ESC | → Overworld | → Driving | → Space |

### Testing Boss Flash
1. Start on Space Flight
2. Press `1` to spawn a boss cube (appears ahead)
3. Hold `Space` to fire projectiles
4. Boss should flash **solid red** briefly each time a projectile hits
5. After 20 hits, boss is destroyed

---

## 6. Known Issues & Strategies

### Issue 1: Grid extent doesn't match at all zoom levels
At `FarPlane = 200` and grid extent = 120 units (`60 × 2`), the grid edge is visible when zoomed out. Player can walk to 180 but only sees grid to 120.

**Strategies:**
1. **Increase GridHalfSize to 100** — grid extends to 200 units, past the FarPlane. Simple, costs more vertices.
2. **Use distance fog** — `BasicEffect.FogEnabled = true` fades the grid edge to the sky color. Grid extent doesn't matter.
3. **Dynamic grid LOD** — only render grid lines within N units of the camera. Skip distant lines.

### Issue 2: Coins spawn at random Y heights — some too high to reach
Coin spawn Y is hardcoded in `CoinCollectibleSystem`. Some coins appear above max jump height.

**Strategies:**
1. **Clamp spawn Y** to `[0.5, maxJumpHeight - 0.5]` — guarantee reachability
2. **Tiered spawning** — most coins at ground level (Y = 1), rare coins at jump height (Y = 3), no coins above 3.5
3. **Magnet mechanic** — coins within 3 units are pulled toward the player. Even slightly-too-high coins get vacuumed in during a jump

### Issue 3: No visual feedback for collection
Coins silently disappear when collected. No particle, no sound, no animation.

**Strategies:**
1. **Scale-to-zero animation** — on collection, shrink the coin to 0 over 200ms with a yellow flash
2. **Rising text** — display "+1" floating text that rises and fades (like RPG damage numbers)
3. **Particle burst** — spawn 5-10 yellow point particles that scatter outward on collection

### Issue 4: Camera clips through geometry at close distances
At `MinDistance = 4`, the camera can get between the player cube and nearby wall geometry.

**Strategies:**
1. **Raycast camera** — cast a ray from the player to desired camera position. If it hits geometry, move the camera closer.
2. **Increase MinDistance** — set to 8 to keep camera further. Simple but limits close-up view.
3. **Fade player alpha** — when camera distance < 6, fade the player cube to transparent. Camera can be anywhere without visual issues.

---

## 7. Architecture & Quick Wins

### Current Three-Screen Architecture
```
Space Flight ──ESC──▶ Overworld ──ESC──▶ Driving ──ESC──▶ Space Flight
     ↑                    ↑                   ↑
     │                    │                   │
  ShipController     PlayerController    VehicleController
  ProjectileSystem   CoinCollectible     CoinCollectible
  BossSystem         Camera3D (orbit)    Camera3D (chase)
  StarfieldRenderer  StarfieldRenderer   StarfieldRenderer
  GridRenderer ×2    GridRenderer        GridRenderer
```

### Quick Wins
1. **Distance fog on all screens** — `_effect.FogEnabled = true; FogStart = 50; FogEnd = 200; FogColor = background` — instant depth. ~5 min per screen.
2. **Coin collect animation** — scale coin to 0 over 200ms when `Collected = true` before removing. ~15 min.
3. **Speedometer on driving screen** — draw a colored bar in the HUD showing current speed vs max. ~10 min.
4. **Boss health bar** — draw a red/green bar above the boss cube showing HP%. Uses `SpriteBatch.Draw` with a 1px texture. ~15 min.
5. **Biome-per-screen-enter** — randomize `GridColor` on each `OnEnter()` to vary the visual feel. ~2 min.

---

## 8. Key Session Lessons

1. **Stars must live in world space.** The first fix (translating the world matrix in Draw) looked correct but didn't work because stars recycled at fixed local Z bounds. The proper fix spawns and recycles stars at world-space coordinates around the tracked center. When in doubt, keep entities in world space.

2. **Ignore function parameters = silent bugs.** `CubeRenderer.Draw(color)` accepted a `Color` but never used it — `BuildSolidCube()` hardcoded yellow. No compiler warning, no runtime error. The boss was just always the wrong color. Lesson: when adding a parameter to a draw call, grep for all existing callers and verify the parameter is actually consumed by the rendering code.

3. **Physics tuning is perception engineering.** Jump height and gravity are the same formula (`h = v²/2g`), but they *feel* different. High jump + low gravity = floaty (Mario Galaxy). High jump + high gravity = snappy (Celeste). We chose snappy (18 force, 45 gravity) because the overworld is fast-paced exploration, not platforming.

4. **Camera pitch defines the genre.** At pitch = -0.35, the game felt like a real-time strategy (top-down view). At -0.18, it feels like a third-person action game. One float changed the entire vibe. Camera angles are not technical details — they're design decisions.

5. **World bounds must match visual bounds.** Having `worldHalfSize = 60` but `GridHalfSize × Spacing = 120` meant 50% of the visible world was unreachable. Always set gameplay bounds to match or exceed visual bounds.
