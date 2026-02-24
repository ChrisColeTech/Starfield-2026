# 26 - Tank Controls, Camera System & Pokemon Follow Behavior: Lessons Learned Handoff

**Date:** 2026-02-24
**Scope:** Tank-style player controls with backward-to-camera movement, smooth camera system with ease in/out damping, Pokemon follow behavior with leash system, codebase audit for configuration extraction
**Status:** Compiles clean (0 warnings, 0 errors). All features functional. No runtime crashes observed.

---

## 1. What We Accomplished

### Tank-Style Player Controls
- **Backward walks toward camera** — When pressing down/S, player turns to face camera and walks toward it instead of walking backward in the same direction
- **Camera stays stationary during backward movement** — Camera does not follow player rotation when moving backward
- **Camera resumes following on forward movement** — When releasing backward and pressing forward, camera smoothly re-centers behind player
- **Added `IsMovingBackward` property** to `PlayerController` for state tracking
- **Added `SetFacingCamera()` method** to set player yaw to match camera direction

### Camera System Overhaul
- **Smooth damp easing** — Replaced linear Lerp with critically-damped spring physics for all camera transitions
- **SmoothDamp/SmoothDamp3/SmoothDampAngle utilities** — Added to `FreeRoamScreen.cs` for consistent smooth motion
- **Position smoothing** (0.2s) — Fast but smooth player following
- **Distance smoothing** (0.4s) — Slower, cinematic zoom when running/walking
- **Yaw smoothing** (0.25s) — Smooth rotation blending
- **Walk/Run distance separation** — Camera at 7m when walking, zooms out to 12m when running
- **Manual zoom preserved** — User zoom input stacks on top of walk/run distance

### Pokemon Follow Behavior
- **Leash system** — Pokemon only follows when trainer is >4m away (no magnetic snapping)
- **Autonomy** — Pokemon does not rotate with trainer, has its own facing direction
- **Movement-based facing** — Pokemon faces its movement direction, not always the trainer
- **Recall facing** — Pokemon faces trainer during recall animation
- **Smooth turn during recall** — Trainer smoothly turns to face Pokemon (0.2s) instead of snapping

### Codebase Audit
- **87 issues identified** across 4 categories:
  - 38 hard-coded configuration values
  - 24 hard-coded game data instances
  - 13 game-specific logic violations
  - 12 SRP violations
- **Documented all magic numbers** with file locations and recommended fixes
- **Identified duplicate code** — SmoothDamp implemented in 3 separate files
- **Priority recommendations** for refactoring

---

## 2. What Work Remains

### Must-Have (Configuration Extraction)
- **Create `GameConfig.cs`** — Centralized configuration class with nested configs
- **Create `game_config.json`** — Load all magic numbers from JSON at startup
- **Extract `MovementConfig`** — walk speed, run speed, rotation speed, gravity, jump force
- **Extract `CameraConfig`** — all smooth times, distances, FOV, near/far planes
- **Extract `PokeballConfig`** — throw distances, arc heights, ball diameters per generation
- **Extract `PokemonConfig`** — target height, scale speed, follow/leash distances

### Should-Have (Game Data Extraction)
- **Create `GameConstants.cs`** — Body type folders, trainer patterns, party size
- **Create `SkeletonProfile` config** — Bone names for game generation detection
- **Create `AnimationTagPatterns.json`** — Tag patterns for `TagResolver.cs`
- **Move hand bone names** to game-specific configuration

### Nice-to-Have (Architecture)
- **Create `IGameGenerationProvider`** interface for game-specific behavior
- **Extract `SmoothDampUtilities.cs`** — Single implementation shared across codebase
- **Refactor `OverworldCharacter.cs`** — Split into smaller components (456 lines is too large)
- **Refactor `ModelLoaderGame.cs`** — Extract initialization, settings, screen management

---

## 3. Optimizations — Prime Suspects

### 3.1 SmoothDamp Code Duplication
`SmoothDamp`, `SmoothDamp3`, and `SmoothDampAngle` are implemented in:
- `FreeRoamScreen.cs` (lines 171-200)
- `OverworldCharacter.cs` (lines 396-430)
- Same functions copied twice

**Impact:** Maintenance burden, inconsistency risk, code bloat

**Fix:** Create `Utilities/SmoothDamp.cs` with static methods. Reference from all consumers.

### 3.2 Hard-coded Configuration Values
38 magic numbers scattered across 10+ files. Examples:
- `PlayerController.cs`: `_walkSpeed = 6f`, `_runSpeed = 12f`, `_gravity = 60f`
- `FollowCamera.cs`: `WalkDist = 7f`, `RunDist = 12f`, `PositionSmoothTime = 0.2f`
- `OverworldCharacter.cs`: `FollowDistance = 3f`, `LeashDistance = 4f`

**Impact:** Tuning requires code changes, no runtime configuration, difficult to test different values

**Fix:** Create `Config/GameConfig.cs` with nested classes, load from JSON at startup.

### 3.3 Game-Specific Logic Mixed with Shared Code
`SharedAnimationResolver.cs` contains hard-coded bone names for game detection:
```csharp
if (skeleton.Bones.Any(b => b.Name == "Waist") && 
    skeleton.Bones.Any(b => b.Name == "LThigh"))
    return "sun-moon";
```

**Impact:** Adding new game generations requires modifying shared code

**Fix:** Create `IGameFamilyDetector` interface with per-generation implementations.

### 3.4 SRP Violations in Core Classes
`OverworldCharacter.cs` (456 lines) handles:
- Character model loading
- Animation playback
- Pokeball controller management
- Pokemon party management
- Throw/recall state machine
- Pokemon follow behavior
- Beam effect rendering
- SmoothDamp utilities

**Impact:** Difficult to test, difficult to modify, tight coupling

**Fix:** Extract `PokemonFollowBehavior`, `BeamEffectRenderer`, `CharacterAnimationController` as separate classes.

---

## 4. Step-by-Step: Get the App Fully Working

### Prerequisites
- .NET 9 SDK
- MonoGame 3.8+ (pulled via NuGet)
- Assets folder at `Starfield2026.Assets/` with Models/ directory

### Build & Run
```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.3DModelLoader
dotnet build
dotnet run
```

### Controls
| Key | Action |
|-----|--------|
| WASD | Move (tank controls) |
| S / Down | Walk toward camera |
| Shift | Toggle run mode |
| Space | Jump |
| Mouse / Arrow Keys | Orbit camera |
| Scroll / +/- | Zoom camera |
| Tab | Open character select |
| Esc | Toggle Character/Map mode |
| Ctrl | Cycle Pokemon slot |
| Alt | Throw/Recall pokeball |

### First Launch Checklist
1. Window opens titled "Starfield 3D Model Loader"
2. Grid floor renders with character model (or cyan cube fallback)
3. WASD moves character with tank controls
4. S makes character turn and walk toward camera
5. Camera smoothly follows when moving forward
6. Camera stays in place when moving backward
7. Shift toggles run, camera zooms out smoothly
8. Tab opens character select overlay
9. Ctrl cycles Pokemon slots (if party loaded)
10. Alt throws pokeball, Pokemon appears at landing spot
11. Pokemon stays at deploy position until trainer walks >4m away
12. Pokemon follows smoothly, faces movement direction
13. Alt recalls Pokemon with smooth trainer turn animation

### Verify Logging
Check `modelloader.log` for:
- `Database initialized: ...`
- `Found N model entries`
- `[Party] Loaded N Pokemon for trainer`
- `[Pokemon] Loaded: pmXXXX, height=X.XXX, fitScale=X.XXXXXX`

---

## 5. How to Start/Test

### Running the Application
```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.3DModelLoader
dotnet run
```

### Testing Tank Controls
1. Load any character via Tab menu
2. Press W — character walks forward, camera follows behind
3. Press S — character turns to face camera, walks toward it
4. Release S, press W — camera smoothly re-centers behind character
5. Hold Shift while moving — character runs, camera zooms out

### Testing Camera Smoothing
1. Start moving forward, then stop — camera eases to rest (no snap)
2. Toggle run mode — camera smoothly zooms out/in over 0.4s
3. Manual zoom with scroll wheel — smooth, stacks on run distance
4. Rotate camera with mouse — smooth yaw/pitch with no jitter

### Testing Pokemon Follow Behavior
1. Deploy Pokemon with Alt
2. Rotate trainer in place — Pokemon does NOT rotate with you
3. Walk away >4m — Pokemon starts following
4. Stop and rotate — Pokemon stays in place, faces its last movement direction
5. Recall with Alt — trainer smoothly turns to face Pokemon

### Testing Smooth Recall Turn
1. Deploy Pokemon
2. Walk around so Pokemon is behind you
3. Press Alt to recall
4. Trainer smoothly turns to face Pokemon (no snap)

---

## 6. Known Issues & Strategies

### Issue 1: Too Many Hard-coded Values
38 configuration values embedded in code. Tuning requires recompilation.

**Strategy A:** Create `GameConfig.cs` with nested classes, load from JSON
```csharp
public class GameConfig
{
    public MovementConfig Movement { get; set; }
    public CameraConfig Camera { get; set; }
    public PokeballConfig Pokeball { get; set; }
}
```

**Strategy B:** Use `appsettings.json` pattern with `IOptions<GameConfig>` injection

**Strategy C:** Create in-game debug menu for live tuning (save to JSON on exit)

### Issue 2: SmoothDamp Duplication
Same math functions implemented in multiple files.

**Strategy A:** Create `Utilities/SmoothDamp.cs` with static methods
```csharp
public static class SmoothDamp
{
    public static float Float(float current, float target, ref float velocity, float smoothTime, float dt);
    public static Vector3 Vector3(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime, float dt);
    public static float Angle(float current, float target, ref float velocity, float smoothTime, float dt);
}
```

**Strategy B:** Create `SmoothDamper` class with instance state (encapsulates velocity)

**Strategy C:** Use existing library (e.g., implement from Unity's Mathf.SmoothDamp reference)

### Issue 3: Game Generation Logic Coupled to Shared Code
`SharedAnimationResolver` and `PokeballController` contain game-specific checks.

**Strategy A:** Create `IGameGenerationProvider` interface
```csharp
public interface IGameGenerationProvider
{
    string GenerationId { get; }
    bool Detect(Skeleton skeleton);
    IEnumerable<string> HandBoneNames { get; }
    float ThrowReleasePoint { get; }
    float PokeballDiameter { get; }
}
```

**Strategy B:** Use attribute-based detection with `[GameGeneration("plza")]` on providers

**Strategy C:** Load game generation configs from JSON files in `Assets/Generations/`

### Issue 4: SRP Violations in Core Classes
`OverworldCharacter.cs` is 456 lines with 8+ responsibilities.

**Strategy A:** Extract following components:
- `PokemonFollowBehavior` — leash logic, movement, facing
- `BeamEffectRenderer` — beam vertex generation and drawing
- `CharacterAnimationController` — state machine for animations

**Strategy B:** Use composition over inheritance — inject behaviors

**Strategy C:** Apply Mediator pattern — character publishes events, behaviors subscribe

---

## 7. New Architecture & Features

### New Architecture Components

```
PlayerController (tank controls)
  |
  +-- IsMovingBackward property
  +-- SetFacingCamera() method
  +-- Yaw only updates when NOT moving backward

FreeRoamScreen (camera orchestration)
  |
  +-- FollowCamera (extracted, ~130 lines)
  |     +-- SmoothDamp position/distance/yaw
  |     +-- Walk/Run distance switching
  |     +-- Deploy zoom offset
  |
  +-- SmoothDamp utilities (local, should extract)

OverworldCharacter (character + Pokemon)
  |
  +-- PokeballController (extracted)
  +-- PokemonParty (extracted)
  +-- Pokemon follow behavior
  |     +-- Leash system (4m threshold)
  |     +-- Movement-based facing
  |     +-- SmoothDamp position/yaw
  |
  +-- BeamEffectRenderer (inline, should extract)
  +-- SmoothDamp utilities (duplicated, should extract)
```

### Key Design Decisions
- **Tank controls** — Forward always moves in facing direction, backward turns to camera
- **Leash system** — Pokemon has autonomy, only follows when trainer is far enough
- **SmoothDamp over Lerp** — Critically-damped springs for natural ease in/out
- **Separate walk/run camera distance** — Visual feedback for movement speed
- **Movement-based Pokemon facing** — Pokemon faces where it's going, not where trainer is

### Quick Wins

1. **Extract SmoothDamp utilities** — 30 min, eliminates duplication
   - Create `Utilities/SmoothDamp.cs`
   - Update `FreeRoamScreen.cs` and `OverworldCharacter.cs` to use it

2. **Create `GameConfig.cs`** — 1 hour, enables tuning without recompilation
   - Start with `MovementConfig` and `CameraConfig`
   - Load from JSON at startup in `ModelLoaderGame.cs`

3. **Extract `PokemonFollowBehavior`** — 45 min, cleaner separation
   - Move leash logic, follow velocity, facing to separate class
   - Inject into `OverworldCharacter`

4. **Create `IGameGenerationProvider`** — 2 hours, extensible game support
   - Interface with `Detect()`, `HandBoneNames`, `ThrowConfig`
   - Implement for Sun-Moon, Scarlet, PLZA

---

## 8. File Reference

### Modified Files This Session
| File | Key Changes |
|------|-------------|
| `Controllers/PlayerController.cs` | Added `IsMovingBackward`, `SetFacingCamera()`, conditional yaw updates |
| `Screens/FreeRoamScreen.cs` | Added SmoothDamp utilities, camera only follows on forward movement |
| `Helpers/OverworldCharacter.cs` | Leash system, movement-based Pokemon facing, smooth recall turn, SmoothDamp utilities |

### No New Files Created
All changes were modifications to existing files.

---

## 9. Configuration Audit Summary

### Hard-coded Values by Category

| Category | Count | Primary Files |
|----------|-------|---------------|
| Movement/Physics | 6 | `PlayerController.cs` |
| Camera | 14 | `FollowCamera.cs`, `FreeRoamScreen.cs` |
| Pokeball | 9 | `PokeballController.cs` |
| Pokemon | 4 | `OverworldCharacter.cs`, `PokemonSlot.cs` |
| Rendering | 5 | `QuadrantGridRenderer.cs`, `MinimapHUD.cs` |

### Recommended Config Structure

```csharp
// Config/GameConfig.cs
public class GameConfig
{
    public MovementConfig Movement { get; set; } = new();
    public CameraConfig Camera { get; set; } = new();
    public PokeballConfig Pokeball { get; set; } = new();
    public PokemonConfig Pokemon { get; set; } = new();
    public PhysicsConfig Physics { get; set; } = new();
    public GridConfig Grid { get; set; } = new();
    public ThemeConfig Theme { get; set; } = new();
}

public class MovementConfig
{
    public float WalkSpeed { get; set; } = 6f;
    public float RunSpeed { get; set; } = 12f;
    public float RotationSpeed { get; set; } = 3f;
}

public class CameraConfig
{
    public float PositionSmoothTime { get; set; } = 0.2f;
    public float DistSmoothTime { get; set; } = 0.4f;
    public float YawSmoothTime { get; set; } = 0.25f;
    public float WalkDist { get; set; } = 7f;
    public float RunDist { get; set; } = 12f;
    public float MinDist { get; set; } = 3f;
    public float MaxDist { get; set; } = 40f;
}

public class PokemonConfig
{
    public float FollowDistance { get; set; } = 3f;
    public float LeashDistance { get; set; } = 4f;
    public float FollowSmoothTime { get; set; } = 0.5f;
    public float FaceSmoothTime { get; set; } = 0.2f;
    public float TargetHeight { get; set; } = 0.8f;
    public float ScaleSpeed { get; set; } = 4f;
    public int MaxPartySize { get; set; } = 6;
}
```

---

## 10. Next Session Recommendations

1. **Start with SmoothDamp extraction** — Quick win, immediate code quality improvement
2. **Create GameConfig infrastructure** — Enables all subsequent tuning work
3. **Implement IGameGenerationProvider** — Unlocks support for new game generations
4. **Refactor OverworldCharacter** — Break into smaller, testable components
