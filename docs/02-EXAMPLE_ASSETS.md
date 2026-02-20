# 32-Starfield-2026-Assets

Reference documentation for the Starfield2026.Assets folder - contains all game assets loaded at runtime.

## Folder Structure

```
Starfield2026.Assets/
├── Player3D/           # 3D Player and character models
│   ├── pm0001_00/       # Bulbasaur (species 1, form 0)
│   ├── pm0004_00/       # Charmander
│   ├── ...
│   └── characters/      # NPCs and player characters
│       ├── overworld/   # Overworld character groups
│       ├── player/      # Player models
│       └── trainers/    # Trainer battle models
├── BattleBG/            # 3D battle backgrounds
│   ├── Grass/           # Grass field background
│   ├── Cave/            # Cave background
│   ├── Dark/            # Dark/night background
│   └── Platform*/       # Battle platforms (ally/foe)
├── Content/             # MonoGame content
│   ├── Fonts/Kerm/      # KermFont bitmap fonts
│   └── Data/Encounters/ # Encounter table JSON
└── Data/                # Game data
    └── gamedata.db      # SQLite database
```

---

## Player3D/ - 3D Models

### Battle Player (`pmXXXX_XX/`)

Player battle models extracted from ROM. Each folder contains:

| File | Description |
|------|-------------|
| `manifest.json` | Model and animation metadata |
| `model.dae` | Skeleton + mesh (COLLADA) |
| `model_1.dae` | Alternate model variant (optional) |
| `*.png` | Texture files (Body, Eye, etc.) |
| `clips/` | Animation clip DAE files |

**Folder naming:** `pm{speciesId}_{formId}_{variant}`
- `pm0004_00` = Charmander (species 4, form 0)
- `pm0006_51` = Charizard Mega X (species 6, form 51)
- `pm0009_51` = Blastoise Mega (species 9, form 51)

### Characters (`characters/`)

| Subfolder | Purpose |
|-----------|---------|
| `overworld/` | Overworld NPC and player models |
| `player/` | Player character variants |
| `trainers/` | Trainer battle models |
| `trainers-battle/` | Trainer battle animations |

**Character group structure (`overworld/group_XXXX/`):**

| File | Description |
|------|-------------|
| `manifest.json` | Clip metadata with semantic tags |
| `model.dae` | Skeleton + mesh |
| `textures/` | Character texture files |
| `clips/` | Animation clips |

---

## manifest.json Format

### Version 1 (Split Model Mode)

```json
{
  "version": 1,
  "mode": "split-model-anims",
  "textures": ["texture1.png", "texture2.png"],
  "models": [
    {
      "name": "model",
      "modelFile": "model.dae",
      "clips": [
        {
          "index": 0,
          "id": "clip_000",
          "name": "anim_0",
          "sourceName": "Motion_0",
          "file": "clips/clip_000.dae",
          "frameCount": 35,
          "fps": 30,
          "semanticName": "Idle",
          "semanticSource": "slot-map-v1"
        }
      ]
    }
  ]
}
```

### Clip Entry Fields

| Field | Type | Description |
|-------|------|-------------|
| `index` | `int` | Sequential clip index |
| `id` | `string` | Unique clip identifier |
| `name` | `string` | Clip name |
| `sourceName` | `string` | Original animation name from ROM |
| `file` | `string` | Path to clip DAE (relative to manifest) |
| `frameCount` | `int` | Number of keyframes |
| `fps` | `int` | Frames per second (typically 30) |
| `semanticName` | `string?` | Semantic tag: "Idle", "Walk", "Run", etc. |
| `semanticSource` | `string?` | Source of semantic tag |

### Semantic Tags

Tags are assigned by the exporter or manually in BgEditor:

| Tag | Index | Description |
|-----|-------|-------------|
| `Idle` | 0 | Standing idle |
| `Walk` | 1 | Walking cycle |
| `Run` | 2 | Running cycle |
| `Jump` | - | Jump animation |
| null | - | Untagged clip |

**Tag inference:** If `semanticName` is null, `SplitModelAnimationSetLoader.InferTag()` maps trailing index:
- Index 0 → "Idle"
- Index 1 → "Walk"  
- Index 2 → "Run"

---

## BattleBG/ - Battle Backgrounds

3D scenes for battle screen backgrounds.

| Folder | Background | Contents |
|--------|------------|----------|
| `Grass/` | Grass field | `Grass.dae`, sky/field textures |
| `Cave/` | Underground cave | `Cave.dae`, rock textures |
| `Dark/` | Night/dark scene | `Dark.dae`, dark textures |
| `PlatformGrassAlly/` | Player platform | `GrassAlly.dae` |
| `PlatformGrassFoe/` | Enemy platform | `GrassFoe.dae` |
| `PlatformTallGrassAlly/` | Tall grass ally platform | `TallGrassAlly.dae` |
| `PlatformTallGrassFoe/` | Tall grass foe platform | `TallGrassFoe.dae` |
| `PlatformCaveAlly/` | Cave ally platform | `CaveAlly.dae` |
| `PlatformCaveFoe/` | Cave foe platform | `CaveFoe.dae` |
| `PlatformDark/` | Dark platform | `Dark.dae` |
| `PlatformRotation/` | Rotating platform | Rotation animation |

**Loading:** `BattleSceneRenderer.LoadBattleModels()` loads all background sets and caches them by `BattleBackground` enum.

---

## Content/ - MonoGame Content

### Fonts/Kerm/

KermFont bitmap fonts for UI rendering.

| File | Purpose |
|------|---------|
| `Battle.kermfont` | Main battle UI font |

**Loading:** `KermFont` class loads `.kermfont` files with configurable palette.

### Data/Encounters/

Encounter table JSON files (baked into map definitions).

---

## Data/ - Game Data

### gamedata.db

SQLite database containing:

| Table | Contents |
|-------|----------|
| `species` | Player species data (stats, types, abilities) |
| `moves` | Move definitions (power, type, PP) |
| `evolutions` | Evolution chains and triggers |
| `learnsets` | Level-up move lists |
| `items` | Item definitions |

**Access:** `GameDataDb` static class provides query methods:
- `GetSpecies(int id)` → `SpeciesData`
- `GetMove(int id)` → `MoveData`
- `GetEvolutions(int speciesId)` → `List<EvolutionData>`
- `GetMovesLearnedAtLevel(int speciesId, int level)` → `List<(int moveId, int level)>`

---

## Asset Loading

### Models (Player/Characters)

```csharp
var animSet = SplitModelAnimationSetLoader.Load(folderPath);
// Loads:
// - manifest.json
// - model.dae (skeleton + mesh)
// - clips/*.dae (animations)
// - textures (via material references)
```

### Battle Backgrounds

```csharp
_renderer.LoadBattleModels(device, "BattleBG/");
// Loads all folders as BattleModelData
```

### Textures

Textures are loaded automatically by `SkinnedDaeModel.Load()` via COLLADA material references:
1. Parse `<instance_material>` for symbol → material ID
2. Resolve material → effect → image path
3. Load PNG from same folder or `textures/` subfolder

---

## Texture Formats

| Type | Format | Notes |
|------|--------|-------|
| Model textures | PNG | RGBA, various sizes |
| Battle BG textures | PNG | RGBA, alpha test for cutouts |

**UV convention:** COLLADA uses origin top-left; textures are flipped vertically during loading.

---

## Coordinate System

COLLADA uses right-handed Y-up. XNA/MonoGame uses right-handed Y-up.
Transforms are transposed during loading to convert row-major to row-vector convention.

---

## File Size Guidelines

| Asset Type | Typical Size |
|------------|--------------|
| Player model + clips | 100KB - 2MB |
| Character model + clips | 500KB - 5MB |
| Battle background | 50KB - 200KB |
| KermFont | 10KB - 50KB |

---

## Adding New Assets

1. **Player:** Extract from ROM using Ohana3DS or Spica, export via `Starfield2026.OhanaCli`
2. **Characters:** Same pipeline, place in `characters/overworld/`
3. **Battle BG:** Create in Blender, export COLLADA, place in `BattleBG/`
4. **Update manifest:** Run exporter to regenerate `manifest.json`
