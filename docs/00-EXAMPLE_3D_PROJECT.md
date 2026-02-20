# 30-Starfield-2026-3D-PROJECT

Overview of the Starfield2026.3D executable project - the 3D overworld exploration and battle game client.

## Purpose
so today we are creating a new 3d .net monogame. im calling it starfield 2026. its a cross between the classic starfield game and pokemon, where you travel through the universe and have random encounters with creatures, battle them, train them, fight other trainers and heavy influence from pokemon mechanics. read this doc D:\Projects\Starfield-2026\docs\00-EXAMPLE_3D_PROJECT.md we need two screens. one where we fly through space, and another where we are walking around the 3d world. random battles can happen at any time so later we will integrate the battle screen. for now, just create the two worlds that we need. the player should be representd as a 3d cube. start off wth just a moving grid and a cube floating through "space", and another screen with the playable character that walks around in the 3d space on the grid. 
dont use hacks. we need long term solutions that offer the most flexibility. we might have green coins, blue coins. we should not monkey patch and hack things along the way. pplan proper implementaitons

This project is the main entry point for the 3D Player game experience. It handles:
- 3D overworld exploration with skinned character models
- Real-time player movement and camera control
- Tile-based world rendering from 2D map definitions
- Wild encounter triggers and battle transitions
- Collectible system with persistence
- Battle screen integration (3D scene + 2D UI overlay)

## Dependencies

- `Starfield2026.Core` - All game logic, rendering, UI, and data systems
- `MonoGame.Framework.DesktopGL` - Graphics and input framework

## Entry Point

### Program.cs

Standard MonoGame entry point that instantiates and runs the game.

---

## Game1.cs

Main game class inheriting from `Microsoft.Xna.Framework.Game`. Manages the entire game loop, rendering, and subsystem coordination.

### Constants

| Name | Value | Purpose |
|------|-------|---------|
| `DebugStartInBattle` | `true` | Skips overworld, launches directly into battle for debugging |
| `VirtualWidth` | `1280` | Virtual resolution width for UI scaling |
| `VirtualHeight` | `960` | Virtual resolution height for UI scaling |
| `UIFontScale` | `5` | Scale factor for KermFont text rendering |
| `PlayerTargetHeight` | `2f` | Camera follow target height offset |
| `PlayerModelScale` | `0.015f` | Scale factor for character model |
| `JumpVelocity` | `13f` | Initial vertical velocity on jump |
| `Gravity` | `-35f` | Downward acceleration |
| `EncounterChance` | `0.15f` | 15% chance per encounter check |
| `EncounterStepInterval` | `0.4f` | Seconds between encounter checks |

### Key Fields

| Field | Type | Purpose |
|-------|------|---------|
| `_camera` | `Camera3D` | Third-person camera with pitch, yaw, distance, and follow behavior |
| `_input` | `ThirdPersonInputMapper` | Maps keyboard/mouse to movement, turning, pitch, zoom |
| `_tileMapMesh` | `TileMapMesh3D` | 3D mesh built from 2D map definitions |
| `_battleScreen` | `BattleScreen3D` | Handles battle scene rendering and UI |
| `_cubeSystem` | `CubeCollectibleSystem` | Manages collectible coins in the world |
| `_persistence` | `PersistenceManager3D` | Saves/loads player state, coins, character selection |
| `_animController` | `AnimationController` | Drives skeletal animations for player character |
| `_model` | `SkinnedDaeModel` | GPU resources for skinned character mesh |

### Methods

#### `Initialize()`

- Creates `BasicEffect` for 3D rendering with lighting
- Creates grid effect for debug grid lines
- Builds `TileMapMesh3D` from world ID "small_world"
- Spawns player at world center
- Loads persisted state (character, position, coins)
- Sets up pause menu items

#### `LoadContent()`

- Creates SpriteBatch and 1x1 white pixel texture
- Resolves assets root path relative to assembly location
- Scans for character folders with manifest.json
- Loads KermFont for UI text rendering
- Initializes `BattleScreen3D` subsystem
- Creates test party and inventory for battles
- Loads battle background 3D models
- Generates collectible coin spawns
- Loads default character model

#### `LoadCharacterModel(string folderName)`

Loads a character model from the assets directory.

**Parameters:**
- `folderName` - Character folder name (e.g., "tr0001_00")

**Behavior:**
- Loads split model animation set via `SplitModelAnimationSetLoader`
- Creates `AnimationController` with the animation set
- Loads skinned model with skeleton
- Starts with "Idle" animation

#### `Update(GameTime)`

Main update loop handling, in priority order:

1. **Transition animations** - Battle entry/exit fade and flash effects
2. **Battle state** - Delegates to `BattleScreen3D.Update()`
3. **Message box** - Blocks input while active
4. **Pause menu** - Handles menu navigation
5. **Character select overlay** - Tab/Escape to switch characters
6. **Overworld gameplay** - Movement, collision, encounters

**Overworld update behavior:**
- Maps input to camera-relative movement
- Wall sliding collision via `TileMapMesh3D.CanOccupy()`
- Jump physics with gravity
- Collectible pickup detection
- Wild encounter checks on encounter tiles
- Animation state selection (Idle/Walk/Run/Jump)
- Camera follow with turning delay
- Position persistence on changes

#### `Draw(GameTime)`

Renders either battle or overworld based on state.

**Battle rendering:**
1. Draw 3D battle scene
2. Draw 2D battle UI overlay (scaled to fill window)
3. Draw transition overlay if active

**Overworld rendering:**
1. Clear background
2. Draw debug grid lines
3. Draw tile map mesh
4. Draw collectible coins
5. Draw player character with skinned animation
6. Draw 2D UI overlay (coin counter, overlays, pause menu, message box)
7. Draw transition overlay if active

#### `BuildInputState(KeyboardState)` → `InputState`

Converts keyboard state to UI input state.

**Returns:** `InputState` with directional flags, confirm/cancel, any key, page navigation, and mouse state.

#### `GetUITransform()` → `Matrix`

Creates transform matrix for letterboxed UI scaling. Maintains virtual 1280x960 aspect ratio.

#### `GetBattleUITransform(out int virtualW, out int virtualH)` → `Matrix`

Creates transform matrix for battle UI that fills full window width, scaling by height.

#### `CheckEncounterTile()`

Checks if player is on an encounter tile and triggers random encounter check.

**Behavior:**
- Gets overlay behavior from tile map
- Checks for "encounter" in behavior string
- Rolls encounter chance at defined intervals
- On success, resolves battle background and begins transition

#### `BeginBattleTransition()`

Initiates the battle transition sequence:
1. Flash white
2. Fade to black
3. Enter battle
4. Fade from black (revealing battle)

---

## TileMapMesh3D.cs

Generates and renders 3D geometry from 2D tile map definitions. Supports multi-map worlds with grid placement.

### Properties

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `TileWorldSize` | `float` | `2f` | World-space size of each tile |
| `BlockHeight` | `float` | `1.2f` | Height of extruded blocking overlays |
| `GroundY` | `float` | `0f` | Y position of ground plane |
| `Origin` | `Vector3` | `Zero` | World-space offset for entire mesh |
| `TriangleCount` | `int` | - | Number of triangles in mesh |

### Methods

#### `BuildWorld(GraphicsDevice, string worldId)`

Builds mesh for all maps in a world. Maps are placed at their WorldX/WorldY grid positions.

**Parameters:**
- `device` - Graphics device for buffer creation
- `worldId` - World identifier to filter maps

**Behavior:**
- Loads all maps matching worldId from `MapCatalog`
- Iterates tiles, creating ground quads from base tile colors
- Extrudes non-walkable overlays as colored blocks
- Creates vertex and index buffers

#### `Build(GraphicsDevice, MapDefinition)`

Builds mesh for a single map at origin. Used for single-map rendering.

#### `Draw(GraphicsDevice, BasicEffect)`

Renders the mesh using the provided effect.

#### `GetWorldCenter(string worldId, float tileWorldSize, float groundY)` → `Vector3` (static)

Calculates world-space center of a world's bounds.

**Returns:** Center point of all maps' combined bounding box.

#### `TryGetTile(float worldX, float worldZ, out MapDefinition, out int tileX, out int tileY)` → `bool`

Resolves world position to map and tile coordinates.

**Returns:** `true` if position is within a map, `false` if out of bounds.

#### `IsWalkable(float worldX, float worldZ)` → `bool`

Simple walkability check.

**Returns:** `true` if tile is walkable, `false` if blocked or out of bounds.

#### `IsInBounds(float worldX, float worldZ)` → `bool`

**Returns:** `true` if position is within any map's bounds.

#### `CanOccupy(float worldX, float worldZ, float playerFootY)` → `bool`

Extended collision check allowing landing on jump-standable overlays.

**Returns:** `true` if player can occupy this position at the given height.

**Jump-standable logic:**
- Blocking overlays with specific IDs can be landed on
- Player must be at or above the block's top surface

#### `GetSupportHeight(float worldX, float worldZ)` → `float`

Returns the surface height at a position for ground collision.

**Returns:** Ground Y or top of jump-standable block.

#### `GetOverlayBehavior(float worldX, float worldZ)` → `string?`

Gets overlay behavior string (e.g., "wild_encounter") at position.

**Returns:** Behavior string or `null` if no overlay/behavior.

---

## Subsystem Dependencies

Game1 depends on these Core subsystems:

| Subsystem | Namespace | Purpose |
|-----------|-----------|---------|
| `BattleScreen3D` | `Starfield2026.Core.Battle` | 3D battle scene + UI |
| `Camera3D` | `Starfield2026.Core.Rendering` | Third-person camera |
| `ThirdPersonInputMapper` | `Starfield2026.Core.Systems` | Input to camera-relative movement |
| `CubeCollectibleSystem` | `Starfield2026.Core.Systems` | Coin spawning, animation, collection |
| `PersistenceManager3D` | `Starfield2026.Core.Save` | JSON file save/load |
| `AnimationController` | `Starfield2026.Core.Rendering.Skeletal` | Skeletal animation playback |
| `SkinnedDaeModel` | `Starfield2026.Core.Rendering.Skeletal` | GPU skinned mesh |
| `SplitModelAnimationSetLoader` | `Starfield2026.Core.Rendering.Skeletal` | Loads split DAE assets |
| `MapRegistry` / `MapCatalog` | `Starfield2026.Core.Maps` | Map data access |
| `TileRegistry` | `Starfield2026.Core.Maps` | Tile definition lookup |
| `KermFont` / `KermFontRenderer` | `Starfield2026.Core.UI.Fonts` | Bitmap font rendering |
| `MessageBox` / `MenuBox` | `Starfield2026.Core.UI` | UI dialogs |
| `CharacterSelectScreen` | `Starfield2026.Core.UI.Screens` | Character selection overlay |

---

## Asset Paths

| Asset Type | Path |
|------------|------|
| Character models | `Starfield2026.Assets/Player3D/characters/overworld/{folder}/` |
| Battle backgrounds | `Starfield2026.Assets/BattleBG/` |
| KermFont | `Starfield2026.Assets/Content/Fonts/Kerm/Battle.kermfont` |

---

## State Flow

```
Initialize → LoadContent
                ↓
            [Game Loop]
                ↓
    Update: Check transitions → Check battle → Check UI → Process input
                ↓
    Draw: Render 3D scene → Render 2D UI overlay → Render transitions
```
