# 07 — Lessons Learned & Handoff: World Registry, Map System & Tile Architecture

**Date:** 2026-02-20  
**Session Focus:** Porting the world/map/tile/encounter registry from PokemonGreen, 3D map renderer, warp transitions, auto-save on location change  
**Status:** Core map system complete. Build clean (0 warnings, 0 errors). Game loads 16×16 tile-based maps with warp transitions between locations, auto-saves current map to DB.

---

## 1. What We Accomplished

### New Systems Created

| System | File | Purpose |
|--------|------|---------|
| `TileCategory` | `Core/Maps/TileCategory.cs` | 10-category enum: Terrain, Decoration, Interactive, Entity, Trainer, Encounter, Structure, Item, Transition, Spawn |
| `TileDefinition` | `Core/Maps/TileDefinition.cs` | Record with Id, Name, Walkable, Color, Height (3D extrusion), OverlayBehavior, EntityId, SpriteName |
| `TileRegistry` | `Core/Maps/TileRegistry.cs` | 120 Starfield-themed tiles in ID ranges 0–119 matching PokemonGreen conventions |
| `WarpConnection` | `Core/Maps/WarpConnection.cs` | `WarpTrigger` enum (Step/Interact), `WarpConnection`, `MapEdge`, `MapConnection` records |
| `WorldDefinition` | `Core/Maps/WorldDefinition.cs` | World metadata record (Id, Name, SpawnMapId, SpawnX, SpawnY) |
| `MapDefinition` | `Core/Maps/MapDefinition.cs` | Abstract base class — auto-registers in `MapCatalog`, stores tile arrays, warps, encounters |
| `MapCatalog` | `Core/Maps/MapCatalog.cs` | Static lookup by map ID, neighbor queries filtered by world |
| `WorldRegistry` | `Core/Maps/WorldRegistry.cs` | Static world registry with default "home_base" world |
| `EncounterEntry` | `Core/Encounters/EncounterEntry.cs` | Enemy species + level range + weight for weighted random selection |
| `EncounterTable` | `Core/Encounters/EncounterTable.cs` | Encounter type grouping with base encounter rate |
| `EncounterRegistry` | `Core/Encounters/EncounterRegistry.cs` | Loads per-map encounter tables, `TryEncounter()` with weighted random |
| `MapRenderer3D` | `Core/Rendering/MapRenderer3D.cs` | Renders tile maps as 3D geometry — flat quads for walkable, extruded cubes for walls |
| `HomeBaseCenter` | `Core/Maps/Generated/HomeBaseCenter.g.cs` | 16×16 test map: metal floor, walls, computer, encounter zone, door to hangar |
| `HomeBaseHangar` | `Core/Maps/Generated/HomeBaseHangar.g.cs` | 16×16 test map: cargo bay with crates/barrels, door back to center |

### Modified Systems

| File | Changes |
|------|---------|
| `OverworldScreen.cs` | Added `MapRenderer3D`, `LoadMap()`, `CurrentMapId`, `SetPlayerPosition()`, warp step detection, tile-based encounters via `EncounterRegistry` |
| `Starfield2026Game.cs` | Added `WorldRegistry.Initialize()`, map singleton init, saved map restore, `HandleMapTransition()`, `AutoSave()` |
| `PlayerProfile.cs` | Added `CurrentMapId` property (default: "home_base_center") |
| `GameDatabase.cs` | Added `current_map_id` column to schema, migration for existing DBs, save/load support |

### Persistence Updates

| Field | Added To | Purpose |
|-------|----------|---------|
| `CurrentMapId` | `PlayerProfile`, `GameDatabase` | Persist which map the player is on across sessions |
| Auto-save trigger | `HandleMapTransition` | Saves full profile every time the player warps to a new map |

### Tile ID Ranges Convention

| Range | Category | Examples |
|-------|----------|----------|
| 0–15 | Terrain | Water, Grass, MetalFloor, Lava, Void, TechFloor |
| 16–31 | Decoration | Tree, Rock, Crystal, Antenna, SupplyCrate, FuelTank |
| 32–47 | Interactive | Door, Warp, Computer, RepairStation, Teleporter |
| 48–55 | Entity | Mechanic, Vendor, Medic, Commander, Rival, Scientist |
| 56–71 | Trainer | PilotRookie...Admiral, PirateGrunt...PirateCaptain |
| 72–79 | Encounter | NebulaZone, AsteroidField, DarkNebula, MiningSpot |
| 80–95 | Structure | Wall, Ledges, Conveyors, Ramp, Cliff, EnergyBarrier |
| 96–111 | Item | AmmoPickup, HealthPack, ShieldBoost, Crystals, Rations |
| 112–115 | Transition | North/South/West/East edge connection markers |
| 116–119 | Spawn | PlayerSpawn, EnemySpawn, BossSpawn, ItemSpawn |

---

## 2. What Work Remains

### Critical (Blocks Gameplay)

1. **Tile Collision** — Player can walk through walls. Need to check `TileRegistry.GetTile(id).Walkable` before allowing movement in `PlayerController`.
2. **Player Spawn from Map** — Player always starts at (0, 0.75, 0). Should read `PlayerSpawn` tile (ID 116) from the map and position there.
3. **Map Editor Integration** — Editor reads `TileRegistry.cs` directly but hasn't been tested with the Starfield file yet. Need to verify parse compatibility.

### Important (Core Experience)

4. **Interact Warps** — `WarpTrigger.Interact` warps require facing the tile + pressing Enter. Currently only checks `ConfirmPressed` but doesn't verify player facing direction.
5. **Warp Visual Feedback** — No transition animation between maps (instant cut). Should have fade-to-black or screen flash.
6. **Encounter Integration** — `TryEncounter()` fires `OnRandomEncounter` but the battle screen doesn't exist yet. Need actual encounter resolution.
7. **NPC Rendering** — Entity tiles (Mechanic, Vendor, etc.) show as colored cubes. Need sprite or model rendering for NPCs.

### Nice to Have

8. **Multi-Layer Maps** — Overlay tile layer is defined but not rendered. Could add furniture/decoration overlays.
9. **Map Connections** — Adjacent maps via `MapConnection` (seamless transition at map edges) not implemented yet.
10. **Dynamic Encounters** — Encounter rate could scale with movement speed, time of day, or progression.

---

## 3. Optimizations — Prime Suspects

### Suspect 1: MapRenderer3D Rebuilds All Geometry Every Frame

**What happens:** `MapRenderer3D.Draw()` iterates every tile and calls `DrawUserPrimitives` per tile. For a 16×16 map that's 256 draw calls per frame.

**Root cause:** No mesh caching. The renderer builds vertex data on every `Draw()` call.

**Fix strategy:**
1. Build a single `VertexBuffer` + `IndexBuffer` for the entire map at `LoadMap()` time
2. Draw the entire map in 1–2 draw calls (flat tiles + extruded tiles)
3. Only rebuild when the map changes (warp transition)
4. Expected improvement: ~100x fewer draw calls

### Suspect 2: Warp Detection Checks Every Warp Every Frame

**What happens:** The `Update()` loop iterates all `_currentMap.Warps` every frame to check if the player is standing on a warp tile.

**Root cause:** Linear search through all warps. With 1–3 warps per map this is negligible, but would scale poorly with complex maps.

**Fix strategy:**
1. Build a `HashSet<(int x, int y)>` or `Dictionary<(int, int), WarpConnection>` at `LoadMap()` time
2. O(1) lookup instead of O(n) scan
3. Only matters for maps with many warps (10+)

### Suspect 3: Tile Coordinate Conversion Duplicated

**What happens:** The formula `(int)(player.Position.X / scale + map.Width / 2f)` is calculated in multiple places: warp detection, encounter checks, and (future) collision checks.

**Root cause:** No shared coordinate conversion method.

**Fix strategy:**
1. Add `MapDefinition.WorldToTile(Vector3 worldPos)` method
2. Add `MapDefinition.TileToWorld(int tileX, int tileY)` method
3. Use consistently in all systems
4. Prevents drift between different coordinate calculations

### Suspect 4: Static Initialization Order is Fragile

**What happens:** Generated map classes use `static readonly` fields + a static `Instance` property. If `Instance` is declared before the data arrays, the constructor runs before data is initialized → null reference crash.

**Root cause:** C# initializes static fields in declaration order. The class has no explicit control over this.

**Fix strategy:**
1. **Current fix:** Always declare `Instance` LAST in generated classes (after all data fields)
2. **Better:** Use `Lazy<T>` wrapper: `public static HomeBaseCenter Instance => _lazy.Value;`
3. **Best:** Use a factory method instead of singleton: `MapCatalog.Register(new HomeBaseCenter(...))` at startup
4. **Guard:** Add a code analyzer or comment convention that generated maps must follow

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
```
.NET SDK 9.0.203+
```

### First-Time Setup (Important!)

The database schema changed. Old databases may crash. Delete the old DB:

```powershell
del "D:\Projects\Starfield-2026\src\Starfield2026.3D\bin\Debug\net9.0\starfield2026.db"
```

> [!NOTE]
> The new migration code (`ALTER TABLE ADD COLUMN current_map_id`) should handle existing DBs automatically, but deleting is safest for a clean start.

### Build & Run
```powershell
cd D:\Projects\Starfield-2026
dotnet build src\Starfield2026.sln
dotnet run --project src\Starfield2026.3D
```

### Verify All Systems

1. **Space Flight** — Game opens here by default. WASD moves, Z toggles ammo type, SPACE fires.
2. **Overworld** — ESC transitions to overworld. You should see the 16×16 HomeBaseCenter map rendered as colored 3D tiles:
   - Gray floor (MetalFloor #808090)
   - Dark walls around the perimeter (Wall #404040, extruded height 2)
   - Blue computer tile at (3,3)
   - Green encounter zone at top-right (NebulaZone #5a9c3a)
   - Brown door at bottom-center
3. **Warp Test** — Walk to the brown door tile at the bottom wall. Stepping on it should warp you to HomeBaseHangar (cargo bay themed). The top door in the hangar warps back.
4. **Auto-Save** — After warping, close and restart. You should spawn on the last map you were on.
5. **Driving** — ESC from overworld. W/S speed, A/D steer.

---

## 5. How to Start & Test

### Starting the Application
```powershell
cd D:\Projects\Starfield-2026
dotnet run --project src\Starfield2026.3D
```

### Manual Test Checklist

- [ ] Game launches without crash
- [ ] Overworld shows colored 3D tile map (not just the old green grid)
- [ ] Walls are extruded cubes, floor is flat quads
- [ ] Walk to bottom-center door tile → warp to Hangar
- [ ] Hangar has different color scheme (CargoBayFloor #a0522d), crates and barrels visible
- [ ] Walk to top-center door in Hangar → warp back to Center
- [ ] Close game, restart → spawns on the last map you visited
- [ ] ESC still transitions between screens (Overworld → Driving → Space)
- [ ] Coins still spawn and collect in Overworld
- [ ] Debug output shows ">> MAP TRANSITION: home_base_center -> home_base_hangar"

### Testing Persistence

```powershell
# 1. Start game, navigate to overworld, warp to hangar
# 2. Close game
# 3. Verify DB has current_map_id
sqlite3 "D:\Projects\Starfield-2026\src\Starfield2026.3D\bin\Debug\net9.0\starfield2026.db" "SELECT current_map_id FROM player_profile WHERE id = 1;"
# Should output: home_base_hangar

# 4. Restart game — should load in hangar, not center
```

---

## 6. Known Issues & Strategies

### Issue 1: Player Walks Through Walls

**Symptom:** Player can walk freely over non-walkable tiles (walls, crates, etc.)

**Root Cause:** `PlayerController.Update()` moves the player without checking `TileRegistry.GetTile(targetTile).Walkable`. No collision system exists yet.

**Strategies:**
1. **Pre-movement check** — Before applying velocity, calculate target tile. If not walkable, zero out that velocity component. Allows sliding along walls.
2. **Grid-based movement** — Lock player to tile grid (like PokemonGreen). Move one tile at a time with animation. Simplest collision model.
3. **Collision rectangle** — Treat player as a 1×1 bounding box, check all 4 corners against walkability. Allows smooth movement with proper blocking.
4. **Physics layer** — Build a walkability bitmask at map load. Each bit = 1 tile. Fast O(1) checks via bit operations.

### Issue 2: Static Init Order Crash (Fixed but Fragile)

**Symptom:** `TypeInitializationException` → `ArgumentNullException` on `HomeBaseCenter.Instance`

**Root Cause:** C# static fields initialize top-to-bottom. If `Instance` is declared before data arrays, the constructor receives null arrays.

**Strategies:**
1. ✅ **Declaration order** — Always declare `Instance` after all data fields (current fix)
2. **Lazy initialization** — `static Lazy<HomeBaseCenter> _lazy = new(() => new HomeBaseCenter());`
3. **Explicit registration** — Remove singletons, register maps explicitly in `Initialize()`
4. **Source generator** — Use a Roslyn source generator to auto-generate map classes with correct ordering

### Issue 3: Coordinate System Mismatch Risk

**Symptom:** Warp detection might not match tile rendering if scale changes

**Root Cause:** The 3D world uses `scale = 2f` to convert tile coordinates to world units. This magic number appears in both `MapRenderer3D` and `OverworldScreen`. If one changes, the other breaks.

**Strategies:**
1. **Single constant** — Define `MapDefinition.TileScale = 2f` and reference everywhere
2. **Coordinate methods** — Add `WorldToTile()` and `TileToWorld()` to `MapDefinition`
3. **Unit test** — Write a test that renders and queries the same tile to verify alignment
4. **Assert in debug** — Add runtime checks that player tile position is valid before using it

### Issue 4: Auto-Save Could Cause Stutters

**Symptom:** Potential frame drop on map transition due to synchronous SQLite write

**Root Cause:** `AutoSave()` calls `SaveProfile()` synchronously on the game thread during map transition.

**Strategies:**
1. **Async save** — Use `Task.Run()` to save on a background thread
2. **Deferred save** — Queue save, execute during next loading screen / fade transition
3. **Batch saves** — Only save every N seconds, plus on exit. Reduces total DB writes
4. **Accept it** — SQLite writes for 1 row are <1ms. For now this is fine.

---

## 7. Architecture & New Features

### Current Architecture (Post-World Registry)

```
Starfield2026.3D (Desktop App)
├── Program.cs                        → Entry point
├── Starfield2026Game.cs                → Screen router, HUD, transitions, WorldRegistry init,
│                                       map transition handler, auto-save

Starfield2026.Core (Library)
├── Controllers/
│   ├── PlayerController.cs           → Overworld movement, jump, run
│   ├── VehicleController.cs          → Driving physics, steering, turbo
│   └── ShipController.cs             → Space flight movement, gear system
├── Encounters/
│   ├── EncounterEntry.cs             → Species/enemy + level range + weight
│   ├── EncounterTable.cs             → Encounter type grouping with base rate
│   └── EncounterRegistry.cs          → Per-map encounter loading, weighted random TryEncounter
├── Input/
│   ├── InputManager.cs               → Polls hardware → InputSnapshot
│   └── InputSnapshot.cs              → Immutable per-frame input state
├── Maps/
│   ├── MapCatalog.cs                 → Static map lookup by ID, neighbor queries
│   ├── MapDefinition.cs              → Abstract base for generated maps (auto-registers)
│   ├── TileCategory.cs               → 10-category enum
│   ├── TileDefinition.cs             → Tile properties record (walkable, color, height)
│   ├── TileRegistry.cs               → 120 Starfield tiles (readable by Map Editor via C# parse)
│   ├── WarpConnection.cs             → Warp/connection records
│   ├── WorldDefinition.cs            → World metadata record
│   ├── WorldRegistry.cs              → Static world registry (default: "home_base")
│   └── Generated/
│       ├── HomeBaseCenter.g.cs       → 16×16 center map with walls, encounters, door
│       └── HomeBaseHangar.g.cs       → 16×16 hangar map with crates, warp back
├── Rendering/
│   ├── Camera3D.cs                   → Third-person orbit camera
│   ├── CoinRenderer.cs              → 3D coin mesh
│   ├── CubeRenderer.cs              → Per-face colored 3D cube
│   ├── GridRenderer.cs              → Configurable grid plane
│   ├── MapRenderer3D.cs             → Renders MapDefinition as 3D colored tile geometry
│   ├── PixelFont.cs                 → 5×7 bitmap glyph renderer
│   └── StarfieldRenderer.cs        → Dots/streaks starfield
├── Screens/
│   ├── IGameScreen.cs               → Screen interface
│   ├── SpaceFlightScreen.cs         → Space flight mode
│   ├── OverworldScreen.cs           → 3D map exploration, warp detection, encounters
│   └── DrivingScreen.cs            → Driving mode
├── Systems/
│   ├── AmmoSystem.cs                → Ammo tracking
│   ├── CoinCollectibleSystem.cs     → Coin spawning and collection
│   ├── BossSystem.cs                → Boss spawning, damage
│   ├── EnemySystem.cs               → Enemy AI (partial)
│   ├── PlayerHealthSystem.cs        → HP tracking
│   └── ProjectileSystem.cs         → Projectile spawning, collision
└── Save/
    ├── GameDatabase.cs              → SQLite persistence (+ current_map_id migration)
    └── PlayerProfile.cs            → Saveable player data (+ CurrentMapId)
```

### Design Patterns Used

| Pattern | Implementation | Benefit |
|---------|----------------|---------|
| **Registry** | `TileRegistry`, `MapCatalog`, `WorldRegistry`, `EncounterRegistry` | Centralized lookup, decoupled from consuming code |
| **Generated Code** | `*.g.cs` map classes | Maps authored in editor, exported to C# — no runtime JSON parsing |
| **Auto-Registration** | `MapDefinition` constructor calls `MapCatalog.Register(this)` | Maps automatically discoverable without manual wiring |
| **Singleton** | `HomeBaseCenter.Instance`, `HomeBaseHangar.Instance` | Single canonical instance per map, no duplication |
| **Event-Driven** | `OnMapTransition`, `OnRandomEncounter` | Screens signal intent, Game class handles orchestration |
| **Migration** | `ALTER TABLE ADD COLUMN` in `GameDatabase.Initialize()` | Backward-compatible schema evolution |

### How the Map Editor Works

The PokemonGreen Map Editor (Electron app) directly parses `TileRegistry.cs` to build the tile palette. Key compatibility requirements:

1. **File must be named `TileRegistry.cs`** — editor scans for this filename
2. **`TileDefinition` constructors must follow the record pattern** — editor parses the constructor arguments
3. **Tile IDs must be unique integers** — editor uses `[id] = new TileDefinition(id, ...)` syntax
4. **Categories must match `TileCategory` enum** — editor groups tiles by category in the palette

The editor exports map data as `.g.cs` generated classes that extend `MapDefinition`.

### Quick Wins

1. **Tile collision** — Add walkability check in `PlayerController.Update()` by reading `TileRegistry.GetTile()`. Prevents walking through walls. ~30 min.

2. **Spawn position** — Scan map for tile ID 116 (PlayerSpawn) in `LoadMap()` and call `SetPlayerPosition()`. Player starts at the correct spawn point. ~15 min.

3. **Warp transition fade** — Reuse the existing `_isTransitioning` / `_transitionAlpha` fade logic from `TransitionTo()` for map warps. ~20 min.

4. **Coordinate helper methods** — Add `MapDefinition.WorldToTile()` and `TileToWorld()` to eliminate magic number `2f` duplication. ~10 min.

5. **Map-to-VertexBuffer cache** — Build a single `VertexBuffer` for the entire map at `LoadMap()` instead of per-frame. Huge perf win. ~45 min.

6. **More test maps** — Create a 32×32 outdoor area with grass, trees, water. Tests larger maps + decoration rendering. ~30 min.

---

## 8. Key Lessons for Next Session

1. **C# static field initialization order matters.** Static fields initialize in declaration order. If a singleton `Instance` is declared before its data arrays, the constructor receives null. Always declare `Instance` last or use `Lazy<T>`.

2. **Port entire subsystems, not individual files.** The world registry port touched 14 new files because maps depend on tiles which depend on categories which depend on encounters. Porting piecemeal would have created broken intermediate states.

3. **The Map Editor reads C# directly — no JSON needed.** Creating a separate `default.json` was unnecessary duplication. The editor parses `TileRegistry.cs` at the source level. One source of truth.

4. **Migration code is mandatory when adding DB columns.** The `ALTER TABLE ADD COLUMN` migration pattern (check column existence → add if missing) prevents crashes on existing save files. Always include migration with schema changes.

5. **Event-driven transitions are clean.** `OverworldScreen` fires `OnMapTransition(targetMapId)` without knowing about `MapCatalog`, `GameDatabase`, or player positioning. `Starfield2026Game` handles all orchestration. This keeps screens lightweight and testable.

6. **Warp re-trigger prevention is essential.** When warping, the player must be placed 1 tile inward from the arrival warp. Otherwise they immediately re-trigger the warp on the next frame, creating an infinite loop.

7. **Auto-save on transitions, not on timers.** Saving on every map transition is natural and predictable. The player always knows their progress is saved because they see the location change. No need for timed auto-save during normal gameplay.

8. **Tile height drives 3D rendering.** The `Height` property on `TileDefinition` controls whether a tile renders as a flat quad (height 0) or an extruded cube. This simple property creates surprisingly convincing 3D environments from 2D tile data.

---

## 9. Commit Message Template

```
feat(world): Add world registry, tile maps, warp transitions, auto-save

- Port world/map/tile/encounter architecture from PokemonGreen (14 new files)
- Create TileRegistry with 120 Starfield-themed tiles (ID 0-119)
- Create MapDefinition base class with auto-registration in MapCatalog
- Create MapRenderer3D: flat quads for walkable tiles, extruded cubes for walls
- Create HomeBaseCenter + HomeBaseHangar test maps (16x16 each)
- Add warp step detection (Step and Interact triggers)
- Add HandleMapTransition with player repositioning at arrival warp
- Add CurrentMapId to PlayerProfile + GameDatabase (with ALTER TABLE migration)
- Add AutoSave() on every map transition and game exit
- Restore saved map on startup from profile.CurrentMapId
- Wire WorldRegistry.Initialize() and map singleton registration in Game

Build: 0 errors, 0 warnings
```

---

## 10. File Summary

| File | Purpose | Status |
|------|---------|--------|
| `Core/Maps/TileCategory.cs` | 10-category tile enum | ✅ New |
| `Core/Maps/TileDefinition.cs` | Tile properties record (+ Height) | ✅ New |
| `Core/Maps/TileRegistry.cs` | 120 Starfield tiles | ✅ New |
| `Core/Maps/WarpConnection.cs` | Warp/connection records | ✅ New |
| `Core/Maps/WorldDefinition.cs` | World metadata | ✅ New |
| `Core/Maps/MapDefinition.cs` | Abstract map base class | ✅ New |
| `Core/Maps/MapCatalog.cs` | Static map registry | ✅ New |
| `Core/Maps/WorldRegistry.cs` | Static world registry | ✅ New |
| `Core/Maps/Generated/HomeBaseCenter.g.cs` | 16×16 center test map | ✅ New |
| `Core/Maps/Generated/HomeBaseHangar.g.cs` | 16×16 hangar test map | ✅ New |
| `Core/Encounters/EncounterEntry.cs` | Enemy type + level + weight | ✅ New |
| `Core/Encounters/EncounterTable.cs` | Encounter type grouping | ✅ New |
| `Core/Encounters/EncounterRegistry.cs` | Per-map encounter loading | ✅ New |
| `Core/Rendering/MapRenderer3D.cs` | 3D tile map renderer | ✅ New |
| `Core/Screens/OverworldScreen.cs` | Map loading, warps, encounters | ✅ Modified |
| `Core/Save/PlayerProfile.cs` | + CurrentMapId | ✅ Modified |
| `Core/Save/GameDatabase.cs` | + current_map_id column/migration | ✅ Modified |
| `3D/Starfield2026Game.cs` | World init, map transitions, auto-save | ✅ Modified |
