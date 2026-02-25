# BgEditor Backend

Fastify API server and TypeScript Ohana extraction library for Pokemon 3D model assets.

## Quick Start

```bash
npm install
npm run dev    # Start dev server on http://localhost:3001
npm test       # Run unit tests
```

## Architecture

```
backend/
  src/
    index.ts              # Fastify server entry point
    cli-render.ts         # Headless 3D renderer (CLI + reusable functions)
    mcp-server.ts         # MCP server exposing render_model tool
    test-gl.ts            # WebGL render test harness
    routes/
      manifests.ts        # Manifest scan/generate API
      textures.ts         # Texture serving API
    lib/ohana/            # TypeScript port of OhanaCli (3DS binary formats)
      Compressions/       # BLZ, LZSS, LZSS_Ninty decompressors
      Containers/         # GARC, OContainer, PkmnContainer parsers
      Core/               # BinaryReader, FileIO, IOUtils, RenderBase
      Models/             # BCH, PICA200, GfModel, GfMotion, DAE exporter
      Textures/           # GfTexture, TextureCodec, texture format decoders
  test/
    extract-garc.ts       # CLI extraction tool (main pipeline)
    garc-pipeline.test.ts # Unit tests
    _test_gfmodel.ts      # GfModel smoke test
```

## GARC Extraction CLI

The primary tool for extracting 3D models from Nintendo 3DS GARC archives into a format the game engine can load directly.

### Usage

```bash
npx tsx test/extract-garc.ts <garc-path> <output-dir> [options]
```

### Options

| Flag | Description |
|------|-------------|
| `--split-model-anims` | Export model DAE + separate animation clip DAEs (recommended) |
| `-n <limit>` | Only process the first N GARC entries (useful for testing) |

### Examples

Extract Pokemon models (all 800+):
```bash
npx tsx test/extract-garc.ts \
  path/to/RomFS/a/0/9/4 \
  ../Starfield.Assets/Pokemon3D \
  --split-model-anims
```

Extract first 20 entries for testing:
```bash
npx tsx test/extract-garc.ts \
  path/to/RomFS/a/0/9/4 \
  ./test-output \
  --split-model-anims -n 20
```

### Pipeline Phases

1. **Parse** - Load GARC archive, decompress entries (LZSS), parse via FileIO router into models, textures, and animations
2. **Group** - Group consecutive entries: an entry with meshes starts a new group, subsequent texture/animation entries merge into it
3. **Export** - For each group: export model DAEs, animation clip DAEs, PNG textures, and a `manifest.json`

### Known GARC Archive Paths (Sun/Moon RomFS)

| Path | Contents |
|------|----------|
| `a/0/9/4` | Pokemon models (~10,549 entries, ~800 Pokemon) |
| `a/0/9/5` | Pokemon overworld models |
| `a/0/4/0` | Trainer battle models |
| `a/0/4/1` | Trainer overworld models |

## Output Format

### Folder Naming

Folder names follow the C# OhanaCli convention: `{groupIndex:D4}_{name}`.

The name component is derived from the first model name or first texture name:

| Archive | Example name | Derived folder | Notes |
|---------|-------------|----------------|-------|
| Pokemon | model `model` | `0000_model` | Most Pokemon models are named `model` |
| Trainer battle | model `tr0001_00` | `0002_tr0001_00` | Trainer models carry their ID |
| Trainer overworld | texture `tr0001_00_fi_Body` | `0002_tr0001_00_fi_Body` | Derived from first texture |

After extraction, a **mapping step** renames/copies folders to match registry expectations:
- `SpeciesRegistry`: `pm{id:D4}_00` (e.g., `pm0001_00`)
- `NPCRegistry`: explicit paths (e.g., `tr0001_00`, `tr0001_00_fi`)

### Folder Structure

```
0000_model/                         # {groupIndex:D4}_{name}
  manifest.json                     # Metadata for model loaders
  model.dae                         # Skeleton + geometry (no animations)
  model_1.dae                       # Alternate form (if present)
  clips/
    model/
      clip_000.dae                  # Animation clip (Idle)
      clip_001.dae                  # Animation clip (Walk)
      clip_002.dae                  # Animation clip (Run)
      ...
    model_1/
      clip_000.dae
      ...
  pm0001_00_BodyA1.png              # Textures as PNG
  pm0001_00_Eye1.png
  ...
```

### manifest.json

Matches the C# `SplitExportManifest` schema exactly. All fields are present for `SplitModelAnimationSetLoader` compatibility.

```json
{
  "version": 1,
  "mode": "split-model-anims",
  "textures": ["pm0001_00_BodyA1.png", "pm0001_00_Eye1.png"],
  "models": [
    {
      "name": "model",
      "modelFile": "model.dae",
      "clips": [
        {
          "index": 0,
          "id": "clip_000",
          "name": "clip_000",
          "sourceName": "anim_0",
          "semanticName": "Idle",
          "semanticSource": "index-map-v1",
          "file": "clips/model/clip_000.dae",
          "frameCount": 42,
          "fps": 30
        },
        {
          "index": 1,
          "id": "clip_001",
          "name": "clip_001",
          "sourceName": "anim_0",
          "semanticName": "Walk",
          "semanticSource": "index-map-v1",
          "file": "clips/model/clip_001.dae",
          "frameCount": 44,
          "fps": 30
        },
        {
          "index": 2,
          "id": "clip_002",
          "name": "clip_002",
          "sourceName": "anim_0",
          "semanticName": "Run",
          "semanticSource": "index-map-v1",
          "file": "clips/model/clip_002.dae",
          "frameCount": 15,
          "fps": 30
        }
      ]
    }
  ]
}
```

### Clip Manifest Fields

| Field | Description | Example |
|-------|-------------|---------|
| `index` | Clip index from GARC ordering | `0` |
| `id` | Stable runtime key (primary lookup) | `clip_000` |
| `name` | Display name (same as `id`) | `clip_000` |
| `sourceName` | Raw name from GfMotion parser | `anim_0` |
| `semanticName` | Gameplay intent label (nullable) | `Idle`, `Walk`, `Run`, `Jump` |
| `semanticSource` | How semantic was resolved | `source-name` or `index-map-v1` |
| `file` | Relative path to clip DAE | `clips/model/clip_000.dae` |
| `frameCount` | Source frame count | `42` |
| `fps` | Sampling rate | `30` |

Recommended runtime lookup (from C# `SplitModelAnimationSetLoader`):
- Primary key: `id` (then `name` fallback)
- Display/gameplay: `semanticName` > `sourceName` > `name` > `id`
- Index 0–2 map to Idle/Walk/Run via `index-map-v1` when source name has no semantic info

## Integration with Game Engine

### How the registries find models

**Pokemon** (`SpeciesRegistry`):
1. `species.json` has species ID (e.g., `1` for Bulbasaur)
2. Registry derives folder: `pm0001_00` (pattern: `pm{id:D4}_00`)
3. `ModelLoader.Load("pm0001_00", graphicsDevice)` resolves to `Pokemon3D/pm0001_00/`

**NPCs** (`NPCRegistry`):
1. `npcs.json` has explicit paths: `"battleModel": "characters/trainers/tr0001_00"`, `"overworldModel": "characters/overworld/tr0001_00_fi"`
2. `ModelLoader.Load("characters/trainers/tr0001_00", graphicsDevice)` resolves to `Pokemon3D/characters/trainers/tr0001_00/`

### Model loading chain

```
ModelLoader.Load(folderName)
  -> reads manifest.json
  -> gets models[0].modelFile (e.g., "model.dae")
  -> ColladaSkeletalLoader.LoadSkeleton(model.dae) -> SkeletonRig
  -> for each clip: ColladaSkeletalLoader.LoadClip(clip.dae, rig) -> SkeletalAnimationClip
  -> returns SkeletalModelData (cached with LRU eviction, max 16)
```

### Extraction + mapping workflow

Extraction produces `{index}_{name}` folders (e.g. `0000_model`). A mapping step then renames/copies them to match registry folder names.

**Step 1: Extract**
```bash
# Pokemon models
npx tsx test/extract-garc.ts \
  path/to/RomFS/a/0/9/4 \
  ./exports/pokemon \
  --split-model-anims

# Trainer battle models
npx tsx test/extract-garc.ts \
  path/to/RomFS/a/0/4/0 \
  ./exports/trainers-battle \
  --split-model-anims

# Trainer overworld models
npx tsx test/extract-garc.ts \
  path/to/RomFS/a/0/4/1 \
  ./exports/trainers-overworld \
  --split-model-anims
```

**Step 2: Map to registry folders**

Use a scripted copy/mapping step (based on `species.json` and `npcs.json`) to place each export folder at the path the registry expects:

```
exports/pokemon/0000_model/  ->  Pokemon3D/pm0001_00/
exports/pokemon/0001_model/  ->  Pokemon3D/pm0002_00/
exports/trainers-battle/0002_tr0001_00/  ->  Pokemon3D/characters/trainers/tr0001_00/
exports/trainers-overworld/0002_tr0001_00_fi/  ->  Pokemon3D/characters/overworld/tr0001_00_fi/
```

The manifest format is immediately recognized by `SplitModelAnimationSetLoader` — no manifest editing needed.

## Ohana Library (src/lib/ohana/)

TypeScript port of the C# OhanaCli library for parsing Nintendo 3DS binary formats.

### Format Support

| Module | Format | Description |
|--------|--------|-------------|
| `GARC` | GARC archives | Container for all game assets (models, textures, animations) |
| `PkmnContainer` | PC/PS containers | Nested sub-file containers within GARC entries |
| `LZSS_Ninty` | LZSS compression | Nintendo variant of LZSS used for .pc entries |
| `BLZ` | BLZ compression | Bottom-LZ compression for overlays |
| `GfModel` | GfModel (0x15122117) | Game Freak skeletal model format (bones, meshes, materials) |
| `GfMotion` | GfMotion | Game Freak skeletal animation format (keyframes, interpolation) |
| `GfTexture` | GfTexture (0x15041213) | Game Freak texture format (GPU-native pixel data) |
| `BCH` | BCH containers | Older model container format |
| `DAE` | COLLADA 1.4.1 export | Exports models and animations to .dae files |

### Data Flow

```
GARC file on disk
  -> GARC.loadFile() -> OContainer (entries with offsets + lengths)
  -> extract entry bytes, decompress if flagged
  -> FileIO.load() -> routes by magic number:
       0x15122117 -> GfModel (skeletal model)
       0x15041213 -> GfTexture (texture)
       "PC"       -> PkmnContainer (recurse into sub-entries)
       "CM"       -> CM container -> GfModel
       ...
  -> OModelGroup (models + textures + animations)
  -> DAE.exportModelOnly() / DAE.exportClipOnly() -> .dae files
```

## Headless CLI Renderer

Renders 3D models from the command line using a headless WebGL context (via the `gl` npm package). Supports manifest.json or raw .dae files.

### Usage

```bash
npx tsx src/cli-render.ts --manifest <path/to/manifest.json> [options]
npx tsx src/cli-render.ts --model <path/to/model.dae> [options]
```

### Options

| Flag | Description | Default |
|------|-------------|---------|
| `--manifest <path>` | Path to manifest.json | — |
| `--model <path>` | Path to .dae model file | — |
| `--output <path>` | Output PNG path | `render.png` |
| `--width <n>` | Image width | `1920` |
| `--height <n>` | Image height | `1080` |
| `--bg <hex>` | Background color | `1a1a2e` |
| `--camera <preset>` | `front`, `back`, `left`, `right`, `top`, `3/4` | `3/4` |
| `--distance <n>` | Camera distance multiplier | `1.0` |
| `--lighting <preset>` | `studio`, `flat`, `dramatic` | `studio` |
| `--rotate <degrees>` | Rotate model Y-axis | `0` |
| `--clip <file>` | Clip DAE to bake into model | — |
| `--frame <n>` | Animation frame to render | `0` |

### Examples

Render a Pokemon from its manifest:
```bash
npx tsx src/cli-render.ts \
  --manifest ../Starfield.Assets/Pokemon3D/pm0003_00/manifest.json \
  --output venusaur.png --camera front
```

Render a raw DAE file:
```bash
npx tsx src/cli-render.ts \
  --model path/to/model.dae \
  --output render.png --width 512 --height 512
```

### Rendering Pipeline

1. **Load** — Parse DAE with ColladaLoader, extract image references from DAE XML
2. **Decode textures** — Use `sharp` to decode PNG/TGA textures into raw RGBA DataTextures
3. **Match textures** — Fuzzy-match texture names to material names (handles prefixes, suffixes, `_alb`, `_skin` variants)
4. **Apply materials** — Assign MeshPhongMaterial (lit) or MeshBasicMaterial (eyes) with sRGB color pipeline
5. **Render** — Set up camera angle, studio lighting, render via headless WebGL
6. **Save** — Read pixels, flip vertically, write PNG with `pngjs`

## MCP Server

Exposes the renderer as an MCP tool for AI agent integration.

### VS Code Setup

Add to `.vscode/mcp.json` (already configured):
```json
{
  "mcpServers": {
    "starfield-renderer": {
      "command": "npx",
      "args": ["tsx", "src/mcp-server.ts"],
      "cwd": "src/Starfield2026.BgEditor/backend"
    }
  }
}
```

### Claude Code Setup

Register globally (available in all projects):
```bash
claude mcp add --scope user starfield-renderer \
  -- cmd /c npx tsx D:/Projects/Starfield-2026/src/Starfield2026.BgEditor/backend/src/mcp-server.ts
```

Or register for this project only:
```bash
claude mcp add starfield-renderer \
  -- cmd /c npx tsx D:/Projects/Starfield-2026/src/Starfield2026.BgEditor/backend/src/mcp-server.ts
```

Verify it's registered:
```bash
claude mcp list
```

Expected output should show `starfield-renderer: ... - ✓ Connected`.

### Testing

Test the MCP server responds to initialization:
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}' | npx tsx src/mcp-server.ts
```

Expected response includes `"name":"starfield-renderer"` and `"tools":{"listChanged":true}`.

Test the tool listing:
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' | npx tsx src/mcp-server.ts
```

Expected: returns `render_model` tool with its schema.

### Tool: `render_model`

Renders a 3D model from 4 angles (front, 3/4, side, back).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `inputPath` | string | ✓ | Path to manifest.json or .dae model file |
| `outputDir` | string | ✓ | Directory to save rendered PNG files |
| `width` | number | | Image width (default: 512) |
| `height` | number | | Image height (default: 512) |

Output files: `front.png`, `3-4.png`, `side.png`, `back.png`

## API Server

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/manifests` | List all manifests in assets directory |
| GET | `/api/manifests/config` | Get default paths and supported formats |
| POST | `/api/manifests/generate` | Scan directory and generate manifest.json files |
| GET | `/api/file?dir=...&name=...` | Serve a file from disk (model/texture) |
| GET | `/serve/<base64dir>/<filename>` | Serve files with Three.js-compatible relative paths |
| POST | `/api/render-angles` | Render model from 4 angles, save PNGs to disk |

### POST `/api/render-angles`

Request body:
```json
{
  "manifestPath": "path/to/manifest.json",
  "outputDir": "path/to/output",
  "width": 512,
  "height": 512
}
```

Alternatively use `modelPath` instead of `manifestPath` for raw .dae files. Returns `{ saved: ["path/to/front.png", ...] }`.

## Tests

```bash
npm test
```

Tests verify the full pipeline against real GARC archives:
- GARC loading (10k+ entries)
- Entry decompression (LZSS)
- PC container parsing (sub-entries with model/texture data)
- GfModel loading via FileIO routing
- End-to-end extraction with DAE export
