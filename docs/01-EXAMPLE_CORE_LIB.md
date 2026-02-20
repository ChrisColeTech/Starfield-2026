# 31-Starfield-2026-Core-LIBRARY

Reference documentation for Starfield2026.Core - the shared game logic library powering both 2D and 3D Player game experiences.

## Purpose

Core contains all game systems independent of rendering backend:
- Battle system (turn management, damage, EXP, evolution)
- Skeletal animation pipeline (COLLADA loading, skinning, clip playback)
- Player data (species, stats, moves, growth rates)
- Map/Tile system (definitions, collision, encounters)
- UI framework (menus, message boxes, overlays)
- Item system (registry, effects, inventory)
- Persistence (save/load, story flags)

---

## Namespace: Starfield2026.Core.Battle

### BattleScreen3D

Orchestrator for 3D battle scenes. Delegates to BattleCamera, BattleSceneRenderer, and BattleUIManager.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `InBattle` | `bool` | True while battle is active |
| `HasLoadedModels` | `bool` | True when battle backgrounds are loaded |
| `AllyModel` | `SkeletalModelData?` | Player's Player 3D model |
| `FoeModel` | `SkeletalModelData?` | Enemy Player 3D model |
| `OnBattleExit` | `Action?` | Callback when battle ends |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Initialize` | `(SpriteBatch, Texture2D pixel, KermFontRenderer?, KermFont?, SpriteFont?)` | Store rendering dependencies |
| `SetPartyAndInventory` | `(Party, PlayerInventory)` | Set player data for battle UI |
| `LoadBattleModels` | `(GraphicsDevice, string basePath)` | Load BattleBG 3D models |
| `SetBattleBackground` | `(BattleBackground)` | Switch active background |
| `EnterBattle` | `(BattleBackground)` | Start battle with test Player |
| `EnterBattle` | `(BattlePlayer ally, BattlePlayer foe, BattleBackground)` | Start battle with specific Player |
| `CleanupBattle` | `()` | Clear battle state after transition |
| `Update` | `(float dt, InputState, double totalSeconds)` | Main update loop |
| `Draw3DScene` | `(GraphicsDevice?)` | Render 3D battle scene |
| `DrawUI` | `(int fontScale, int w, int h)` | Render 2D battle UI |

---

### BattleCamera

Animated camera with zoom-out intro sequence.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Position` | `Vector3` | Current camera position |
| `IsAnimating` | `bool` | True during zoom animation |
| `FOV` | `float` | Field of view in radians |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Reset` | `(float foeHeight, float allyHeight)` | Reset to start position, adjusted for Player sizes |
| `StartZoom` | `()` | Begin zoom-out animation |
| `Update` | `(float dt)` → `bool` | Advance animation, returns true on completion |
| `GetViewMatrix` | `()` → `Matrix` | Get view matrix for rendering |

---

### BattleSceneRenderer

Renders 3D battle backgrounds, platforms, and Player models.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `HasLoadedModels` | `bool` | True when backgrounds loaded |
| `Device` | `GraphicsDevice?` | Cached graphics device |
| `AllyModel` | `SkeletalModelData?` | Player Player model |
| `FoeModel` | `SkeletalModelData?` | Enemy Player model |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `LoadBattleModels` | `(GraphicsDevice, string basePath)` | Load all BattleBG sets |
| `SetBackground` | `(BattleBackground)` | Switch active background/platforms |
| `SetPlayerModels` | `(SkeletalModelData? ally, SkeletalModelData? foe)` | Set Player models |
| `ClearPlayerModels` | `()` | Remove Player models |
| `RandomizePlaceholderCubes` | `(Random?)` | Randomize cube sizes for camera testing |
| `ComputeFoeHeight` | `()` → `float` | Get foe height for camera adjustment |
| `ComputeAllyHeight` | `()` → `float` | Get ally height for camera adjustment |
| `Draw` | `(GraphicsDevice, Matrix view, float fovRadians)` | Render full scene |
| `DeployFoe` / `RecallFoe` | `()` | Animate foe appear/disappear |
| `DeployAlly` / `RecallAlly` | `()` | Animate ally appear/disappear |
| `UpdateModels` | `(float dt, double totalSeconds)` | Advance animations |
| `FitModelScale` | `(SkeletalModelData, float targetHeight)` → `float` (static) | Calculate scale to fit target height |

---

### BattlePlayer

Wrapper for a Player in battle with animated HP display.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Nickname` | `string` | Display name |
| `SpeciesId` | `int` | Species ID |
| `Level` | `int` | Current level |
| `CurrentHP` / `MaxHP` | `int` | HP values |
| `Gender` | `Gender` | Male/Female/Unknown |
| `StatusCondition` | `StatusCondition` | Active status ailment |
| `StatusAbbreviation` | `string?` | "PSN", "BRN", etc. |
| `Moves` | `BattleMove[]` | Available moves |
| `Source` | `PartyPlayer?` | Original party Player |
| `DisplayHP` | `float` | Smoothly animated HP for bar |
| `HPPercent` / `DisplayHPPercent` | `float` | HP ratios |
| `IsFainted` | `bool` | True when HP <= 0 |
| `EXPPercent` | `float` | EXP bar fill |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `ApplyDamage` | `(int damage)` | Reduce HP |
| `UpdateDisplayHP` | `(float deltaTime, float drainSpeed = 80f)` | Animate HP bar |
| `SyncToParty` | `()` | Copy state back to Source |
| `FromParty` | `(PartyPlayer)` → `BattlePlayer` (static) | Create from party Player |

---

### BattleTurnManager

State machine managing battle turns.

**Phases:** `Idle` → `PlayerAttack` → `FoeAttack` → `EXPReward` → `TurnEnd` → `BattleOver`

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Phase` | `TurnPhase` | Current battle phase |
| `OnAllyAttack` | `Action?` | Callback to trigger attack animation |
| `OnFoeAttack` | `Action?` | Callback to trigger foe attack |
| `OnAllyFaint` / `OnFoeFaint` | `Action?` | Faint animation callbacks |
| `OnReturnToIdle` | `Action?` | Return to idle pose |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `SetAlly` | `(BattlePlayer)` | Replace ally (for switch-in) |
| `StartTurn` | `(int moveIndex)` | Begin turn with selected move |

---

### BattleUIManager

Manages battle menus, messages, and overlays.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `IsOverlayActive` | `bool` | True when Bag/Party screen open |
| `IsMenuActive` | `bool` | True when menu accepting input |
| `Party` | `Party?` | Player's party |
| `OnMoveSelected` | `Action<int>?` | Fired when move selected |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Initialize` | `(SpriteBatch, Texture2D, KermFontRenderer?, KermFont?, SpriteFont?)` | Store dependencies |
| `SetMainMenuItems` | `(Action fight, Action bag, Action party, Action run)` | Wire main menu |
| `OpenFightMenu` | `(BattlePlayer?, Action closeFightMenu)` | Show move selection |
| `CloseFightMenu` | `(Action<Action,Action,Action,Action> setupMainMenu)` | Return to main menu |
| `ActivateMenu` / `DeactivateMenu` | `()` | Enable/disable menu input |
| `ShowMessage` | `(string text, Action? onFinished = null)` | Queue message display |
| `OpenBagScreen` / `OpenPartyScreen` | `()` | Push overlay |
| `PopOverlay` | `(Action<int> onResult)` | Close overlay, get result |
| `UpdateInput` | `(float dt, InputState)` | Handle input |
| `DrawUI` | `(int fontScale, int w, int h, ...)` | Render UI |

---

### BattleBackground (enum)

Background types: `Grass`, `TallGrass`, `Cave`, `Dark`

### BattleBackgroundResolver

Maps overlay behavior strings to backgrounds.

| Method | Signature | Purpose |
|--------|-----------|---------|
| `FromOverlayBehavior` | `(string? behavior)` → `BattleBackground` (static) | Resolve background from tile behavior |

---

## Namespace: Starfield2026.Core.Rendering.Skeletal

### AnimationController

High-level animation controller resolving clips by tag.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Animator` | `SkeletalAnimator` | Low-level animator |
| `AnimationSet` | `SplitModelAnimationSet` | Loaded clips |
| `ActiveTag` | `string?` | Currently playing tag |
| `SkinPose` | `Matrix[]` | Bone skin matrices |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Play` | `(string tag, bool loop = true, bool resetTime = true)` → `bool` | Play clip by tag |
| `HasClip` | `(string tag)` → `bool` | Check if tag exists |
| `Update` | `(float deltaSeconds)` | Advance animation |

---

### SkeletalAnimator

Low-level skeletal animation playback.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Rig` | `SkeletonRig` | Target skeleton |
| `ActiveClip` | `SkeletalAnimationClip?` | Currently playing clip |
| `Loop` | `bool` | Loop mode |
| `CurrentTimeSeconds` | `float` | Playback position |
| `LocalPose` | `Matrix[]` | Bone local transforms |
| `WorldPose` | `Matrix[]` | Bone world transforms |
| `SkinPose` | `Matrix[]` | Final skin matrices |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Play` | `(SkeletalAnimationClip, bool loop = true, bool resetTime = true)` | Start clip |
| `Stop` | `()` | Return to bind pose |
| `Update` | `(float deltaSeconds)` | Advance animation |

---

### SkeletonRig

Bone hierarchy with bind poses.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Bones` | `IReadOnlyList<SkeletonBone>` | Bone definitions |
| `BindLocalTransforms` | `Matrix[]` | Bind pose local |
| `BindWorldTransforms` | `Matrix[]` | Bind pose world |
| `InverseBindTransforms` | `Matrix[]` | Inverse bind matrices |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `TryGetBoneIndex` | `(string name, out int index)` → `bool` | Look up bone by name |

### SkeletonBone

| Property | Type | Description |
|----------|------|-------------|
| `Index` | `int` | Bone index |
| `Name` | `string` | Bone name |
| `NodeId` | `string` | COLLADA node ID |
| `ParentIndex` | `int` | Parent bone (-1 = root) |
| `BindLocalTransform` | `Matrix` | Bind pose transform |

---

### SkeletalAnimationClip

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Clip name |
| `DurationSeconds` | `float` | Clip duration |
| `Tracks` | `IReadOnlyList<BoneAnimationTrack>` | Per-bone animation |

### BoneAnimationTrack

| Property | Type | Description |
|----------|------|-------------|
| `BoneIndex` | `int` | Target bone |
| `Keyframes` | `IReadOnlyList<AnimationKeyframe>` | Transform keyframes |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Sample` | `(float timeSeconds)` → `Matrix` | Interpolate transform at time |

---

### SplitModelAnimationSet

Loaded animation set with rig and clips.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `ModelPath` | `string` | Path to model DAE |
| `Skeleton` | `SkeletonRig` | Skeleton hierarchy |
| `Clips` | `IReadOnlyDictionary<string, SkeletalAnimationClip>` | Clips by ID |
| `ClipsByTag` | `IReadOnlyDictionary<string, SkeletalAnimationClip>` | Clips by semantic tag |

### SplitModelAnimationSetLoader

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Load` | `(string groupFolderPath, string modelName = "model")` → `SplitModelAnimationSet` (static) | Load from manifest.json |

---

### ColladaSkeletalLoader

| Method | Signature | Purpose |
|--------|-----------|---------|
| `LoadSkeleton` | `(string daePath)` → `SkeletonRig` (static) | Parse skeleton from DAE |
| `LoadClip` | `(string clipDaePath, SkeletonRig, string clipName)` → `SkeletalAnimationClip` (static) | Load animation clip |

---

### SkinnedDaeModel

GPU-side skinned mesh with bone weights.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `VertexBuffer` | `VertexBuffer?` | GPU vertex buffer |
| `IndexBuffer` | `IndexBuffer?` | GPU index buffer |
| `PrimitiveCount` | `int` | Triangle count |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Load` | `(GraphicsDevice, string daePath, SkeletonRig)` | Load mesh from DAE |
| `UpdatePose` | `(GraphicsDevice, Matrix[] skinPose)` | Rebuild buffers with new pose |
| `Draw` | `(GraphicsDevice, BasicEffect)` | Render mesh |

---

## Namespace: Starfield2026.Core.Maps

### MapDefinition (abstract)

Generated map definition base class.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `WorldId` | `string` | World identifier |
| `Id` | `string` | Map identifier |
| `Name` | `string` | Display name |
| `Width` / `Height` | `int` | Tile dimensions |
| `TileSize` | `int` | Tile size in pixels |
| `WorldX` / `WorldY` | `int` | World grid position |
| `Warps` | `IReadOnlyList<WarpConnection>` | Warp points |
| `Connections` | `IReadOnlyList<MapConnection>` | Edge connections |
| `EncounterGroups` | `IReadOnlyList<EncounterTable>` | Wild encounters |
| `ProgressMultiplier` | `float` | Level scaling for encounters |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `CreateTileMap` | `()` → `TileMap` | Build tile map instance |
| `GetBaseTile` | `(int x, int y)` → `int` | Get base tile ID |
| `GetOverlayTile` | `(int x, int y)` → `int?` | Get overlay tile ID or null |
| `IsWalkableTile` | `(int tileId)` → `bool` | Check walkability |
| `GetWarp` | `(int x, int y, WarpTrigger)` → `WarpConnection?` | Get warp at position |
| `GetConnection` | `(MapEdge)` → `MapConnection?` | Get edge connection |

---

### TileRegistry

Static tile definition lookup.

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `GetTile` | `(int id)` → `TileDefinition?` (static) | Look up tile by ID |
| `GetTilesByCategory` | `(TileCategory)` → `IEnumerable<TileDefinition>` (static) | Filter by category |
| `AllTiles` | `IEnumerable<TileDefinition>` (static) | All tile definitions |
| `Count` | `int` (static) | Total tile count |

### TileDefinition

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `int` | Tile ID |
| `Name` | `string` | Tile name |
| `Walkable` | `bool` | Can walk on this tile |
| `Color` | `string` | Hex color for 3D rendering |
| `Category` | `TileCategory` | Terrain/Decoration/Interactive/etc. |
| `OverlayBehavior` | `string?` | Behavior string (e.g., "wild_encounter") |
| `SpriteName` | `string?` | Sprite name for 2D rendering |
| `AnimationFrames` | `int` | Number of animation frames |
| `EntityId` | `int?` | Linked entity ID |

---

### MapCatalog

Runtime map registry.

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `TryRegister` | `(MapDefinition)` → `bool` (static) | Register a map |
| `GetMap` | `(string id)` → `MapDefinition?` (static) | Get map by ID |
| `GetAllMaps` | `()` → `IEnumerable<MapDefinition>` (static) | All registered maps |

---

## Namespace: Starfield2026.Core.Player

### PartyPlayer

Player in player's party with full stat tracking.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Nickname` | `string` | Display name |
| `SpeciesId` | `int` | Species ID |
| `Level` | `int` | Current level (1-100) |
| `CurrentHP` / `MaxHP` | `int` | HP values |
| `Gender` | `Gender` | Male/Female/Unknown |
| `StatusCondition` | `StatusCondition` | Active status |
| `HeldItemId` | `int?` | Held item |
| `Attack` / `Defense` / `SpAttack` / `SpDefense` / `Speed` | `int` | Stats |
| `ExperiencePoints` | `uint` | Total EXP |
| `GrowthRate` | `GrowthRate` | Level-up curve type |
| `IVs` / `EVs` | `int[6]` | Individual/effort values |
| `MoveIds` / `MovePPs` | `int[]` | Moves and PP |
| `HPPercent` / `EXPPercent` | `float` | Bar fill ratios |
| `IsFainted` | `bool` | HP <= 0 |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `AddEXP` | `(uint amount)` → `LevelUpResult` | Add EXP, handle level-up |
| `CheckEvolution` | `()` → `EvolutionData?` | Check for evolution trigger |
| `Evolve` | `(int newSpeciesId)` | Perform evolution |
| `RecalculateStats` | `()` | Recalc from base/IV/EV/level |
| `Create` | `(int speciesId, int level, Gender, Random?)` → `PartyPlayer` (static) | Create new Player |

### LevelUpResult

| Property | Type | Description |
|----------|------|-------------|
| `LevelsGained` | `int` | Levels gained |
| `NewMoveIds` | `List<int>` | Moves learned |
| `ReplacedMoveIds` | `List<int>` | Moves replaced |
| `PendingEvolution` | `EvolutionData?` | Evolution trigger |

---

### SpeciesRegistry

Static species data lookup (delegates to GameDataDb).

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Initialize` | `(string? dataDirectory)` (static) | Initialize data source |
| `GetSpecies` | `(int speciesId)` → `SpeciesData?` (static) | Get species data |
| `GetAllSpecies` | `()` → `IReadOnlyCollection<SpeciesData>` (static) | All species |
| `Count` | `int` (static) | Total species count |

---

## Namespace: Starfield2026.Core.Systems

### CubeCollectibleSystem

Collectible coin spawning, animation, collection, and rendering.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `CubeCount` | `int` | Coins collected |
| `TotalCubes` | `int` | Total in world |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `GenerateSpawnsForWorld` | `(string worldId, float tileWorldSize, Func<float,float,bool> canPlace)` | Generate spawns from walkable tiles |
| `LoadFromFlags` | `(HashSet<string> storyFlags)` | Load collected state |
| `UpdateAnimation` | `(float dt)` | Advance animations |
| `CheckCollection` | `(Vector3 playerPosition, HashSet<string> storyFlags)` → `bool` | Check for pickup |
| `NotifyCoinCollected` | `()` | Trigger pickup feedback |
| `ResetAll` | `(HashSet<string> storyFlags)` | Reset all coins |
| `DrawCubes` | `(Matrix view, Matrix projection)` | Render 3D coins |
| `DrawCounter` | `(int fontScale)` | Draw HUD counter |
| `DrawPickupFeedback` | `(int virtualWidth, int fontScale)` | Draw pickup notification |

---

### ThirdPersonInputMapper

Maps keyboard to camera-relative movement.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `State` | `ThirdPersonInputState` | Current input state |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Update` | `()` | Poll keyboard |
| `Consume` | `()` | Sync state without emitting actions |

### ThirdPersonInputState

| Field | Type | Description |
|-------|------|-------------|
| `ExitRequested` | `bool` | Escape pressed |
| `MoveX` / `MoveZ` | `float` | Movement (-1 to 1) |
| `Turn` | `float` | Camera turn input |
| `Pitch` | `float` | Camera pitch input |
| `Zoom` | `float` | Zoom input |
| `JumpPressed` | `bool` | Jump pressed this frame |
| `IsRunning` | `bool` | Run toggle active |

---

## Namespace: Starfield2026.Core.UI

### MessageBox

Text display with typewriter effect and message queue.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `IsActive` | `bool` | Message being displayed |
| `IsTextFullyRevealed` | `bool` | Typing complete |
| `IsFinished` | `bool` | All messages shown |
| `OnFinished` | `Action?` | Callback when last message dismissed |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Show` | `(string message)` | Queue message |
| `ShowMultiple` | `(params string[] messages)` | Queue multiple messages |
| `Clear` | `()` | Reset state |
| `Update` | `(float deltaTime, bool confirmPressed)` | Advance typing/messages |
| `Draw` | `(SpriteBatch, SpriteFont, Texture2D pixel, Rectangle bounds)` | Render (SpriteFont) |
| `Draw` | `(SpriteBatch, KermFontRenderer, Texture2D pixel, Rectangle bounds, int fontScale)` | Render (KermFont) |

---

### MenuBox

Navigable menu with grid layout.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Columns` | `int` | Grid columns (default 2) |
| `SelectedIndex` | `int` | Selected item |
| `IsActive` | `bool` | Accepting input |
| `UseStandardStyle` | `bool` | Overworld vs battle style |
| `OnCancel` | `Action?` | Cancel callback |
| `Items` | `IReadOnlyList<MenuItem>` | Menu items |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `SetItems` | `(params MenuItem[] items)` | Set menu options |
| `Update` | `(bool left, right, up, down, confirm, cancel, Point mouse, bool mouseClick)` | Handle input |
| `Draw` | `(SpriteBatch, SpriteFont, Texture2D pixel, Rectangle bounds)` | Render (SpriteFont) |
| `Draw` | `(SpriteBatch, KermFontRenderer, KermFont, Texture2D pixel, Rectangle bounds, int fontScale)` | Render (KermFont) |

### MenuItem

| Property | Type | Description |
|----------|------|-------------|
| `Label` | `string` | Display text |
| `Enabled` | `bool` | Selectable |
| `OnConfirm` | `Action?` | Selection callback |

---

### InputState

UI input snapshot.

| Field | Type | Description |
|-------|------|-------------|
| `Left` / `Right` / `Up` / `Down` | `bool` | D-pad pressed |
| `Confirm` / `Cancel` | `bool` | Action buttons |
| `AnyKey` | `bool` | Any key pressed |
| `PageLeft` / `PageRight` | `bool` | Page navigation |
| `MousePosition` | `Point` | Mouse position |
| `MouseClicked` | `bool` | Left click |

---

## Namespace: Starfield2026.Core.Rendering

### Camera3D

Third-person camera with follow behavior.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Yaw` / `Pitch` | `float` | Camera rotation |
| `Distance` | `float` | Zoom distance |
| `MinDistance` / `MaxDistance` | `float` | Zoom limits |
| `ViewMatrix` / `ProjectionMatrix` | `Matrix` | Rendering matrices |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Rotate` | `(float yawDelta, float pitchDelta)` | Rotate camera |
| `Zoom` | `(float delta)` | Adjust distance |
| `Follow` | `(Vector3 target, float targetHeight)` | Set follow target |
| `Update` | `(float aspectRatio)` | Recalculate matrices |

---

## Namespace: Starfield2026.Core.Save

### PersistenceManager3D

JSON-based save system for 3D game state.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `StoryFlags` | `HashSet<string>` | Collected coins, triggers |
| `RestoredCharacterFolder` | `string?` | Last selected character |

**Methods:**

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Load` | `()` | Load from JSON file |
| `Save` | `(Vector3 position, string characterFolder, int coinCount)` | Save to JSON file |

---

## Dependencies

- `MonoGame.Framework.DesktopGL` - Graphics framework
- `SixLabors.ImageSharp` - Image loading
- `K4os.Compression.LZ4` - Asset compression
- `Microsoft.Data.Sqlite` - SQLite for game data
- `AssimpNet` - 3D model loading (battle backgrounds)
