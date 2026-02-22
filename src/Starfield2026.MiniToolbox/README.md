# MiniToolbox

Multi-format game asset extraction utility for Nintendo 3DS and Switch titles. Extracts 3D models, textures, skeletal animations, and metadata from proprietary archive formats and exports them as standard COLLADA DAE or Wavefront OBJ files.

## Supported Formats

| Format | Games | Output | Status |
|--------|-------|--------|--------|
| **GARC** | Pokemon Sun/Moon, X/Y (3DS) | DAE, OBJ | Full (models, textures, animations) |
| **TRPAK** | Pokemon Scarlet/Violet (Switch) | DAE | Full (models, textures, animations) |
| **GDB1** | Star Fox Zero/Guard (Wii U) | OBJ | Full (models, textures, animations) |

## Requirements

- .NET 8.0 SDK (Windows)
- Windows 10+ (required by `System.Drawing.Common` and native DLLs)

## Building

```bash
dotnet build src/MiniToolbox.App/MiniToolbox.App.csproj
```

Or build the full solution:

```bash
dotnet build MiniToolbox.sln
```

## Quick Start

```bash
# Show help
dotnet run --project src/MiniToolbox.App/MiniToolbox.App.csproj -- help

# Inspect a GARC archive
dotnet run --project src/MiniToolbox.App/MiniToolbox.App.csproj -- garc -i path/to/garc --info

# Extract trainer models from Sun/Moon
dotnet run --project src/MiniToolbox.App/MiniToolbox.App.csproj -- garc -i romfs/a/1/7/4 --extract -o ./trainers --filter tr

# List models in a Switch archive
dotnet run --project src/MiniToolbox.App/MiniToolbox.App.csproj -- trpak --arc ./romfs --list

# Extract a Star Fox model
dotnet run --project src/MiniToolbox.App/MiniToolbox.App.csproj -- gdb1 --input ./Resources --model 00b1486b -o ./output
```

For brevity, the examples below use `minitoolbox` as an alias for `dotnet run --project src/MiniToolbox.App/MiniToolbox.App.csproj --`.

---

## Commands

### `garc` - 3DS Pokemon GARC Archives

Extracts models, textures, and skeletal animations from GARC container files used in Pokemon Sun/Moon, X/Y, and other 3DS titles. Supports LZSS-compressed entries, nested Pokemon containers, and automatic file type detection via magic bytes.

#### Usage

```
minitoolbox garc --input <garcFile> --info
minitoolbox garc --input <garcFile> --list [--skip N] [-n N]
minitoolbox garc --input <garcFile> --extract -o <outputDir> [options]
```

#### Options

| Option | Description |
|--------|-------------|
| `-i, --input` | Input GARC file (required) |
| `-o, --output` | Output directory (default: `./garc_export`) |
| `-f, --format` | Output format: `dae` (default), `obj` |
| `-n, --limit` | Max entries to process (0 = all) |
| `--skip` | Skip first N entries |
| `--filter` | Only extract entries whose model/texture name contains this string |
| `--info` | Show GARC file summary (entry count, type breakdown) |
| `--list` | List all entries with detected types and model names |
| `--extract` | Extract models, textures, and animations |

#### Modes

**Info** - Quick summary of a GARC file:
```bash
minitoolbox garc -i romfs/a/0/9/4 --info
# Output: Entries: 10549, Sample: model: 15, container: 5
```

**List** - Browse entries with type detection:
```bash
minitoolbox garc -i romfs/a/1/7/4 --list -n 10
# [00000] file_00000.cm [model] (p1_base)
# [00001] file_00001.cm [model] (p2_base)
# [00002] file_00002.cm [model] (tr0001_00)
# ...
```

**Extract** - Export models + textures + animation clips:
```bash
# All entries
minitoolbox garc -i romfs/a/1/7/4 --extract -o ./export

# Only trainer models (name contains "tr")
minitoolbox garc -i romfs/a/1/7/4 --extract -o ./trainers --filter tr

# First 20 Pokemon from the big GARC
minitoolbox garc -i romfs/a/0/9/4 --extract -o ./pokemon -n 20

# Export as OBJ (no animations)
minitoolbox garc -i romfs/a/1/7/4 --extract -o ./obj-export -f obj --filter tr
```

#### GARC Output Structure

```
output/
  tr0001_00/
    model.dae              # COLLADA model with skeleton
    clips/
      clip_000.dae         # Skeletal animation clip 0
      clip_001.dae         # Skeletal animation clip 1
      ...
    tr0001_00_BodyA1.png   # Body texture
    tr0001_00_BodyA2.png
    tr0001_00_Eye1.png     # Eye texture
    ...
  tr0002_00/
    ...
```

#### Known GARC Files (Sun/Moon)

| Path | Size | Entries | Content |
|------|------|---------|---------|
| `a/0/9/4` | 1.3 GB | 10,549 | Pokemon 3D battle models |
| `a/1/7/4` | 110 MB | 316 | Battle character models (trainers, player, objects) |
| `a/2/0/0` | 71 MB | 604 | Field character models (low-poly overworld) |
| `a/0/8/5` | - | - | Map models |
| `a/0/8/7` | 171 MB | 7,205 | Z-move visuals, battle sprites, animations |

#### Naming Conventions (Sun/Moon)

| Prefix | Meaning | Example |
|--------|---------|---------|
| `pm####_##` | Pokemon model + form | `pm0025_00` = Pikachu |
| `tr####_##` | Trainer class + variant | `tr0001_00` = first trainer class |
| `ob####_##` | Object/prop model | `ob0004_00` = Pokeball prop |
| `p1` / `p2` | Male / female player | `p1_base` = male player |
| `*_fi` | Field (low-poly overworld) | `tr0001_00_fi` |

---

### `trpak` - Pokemon Scarlet/Violet Archives

Extracts models from TRPFS/TRPAK archives used in Pokemon Scarlet/Violet. Supports FlatBuffer-based pack files with Oodle compression.

#### Usage

```
minitoolbox trpak --arc <arcDir> [options]
```

#### Options

| Option | Description |
|--------|-------------|
| `-a, --arc` | Archive directory containing `data.trpfd`/`data.trpfs` (required) |
| `-m, --model` | Model path within archive (e.g., `pokemon/pm0025/pm0025_00.trmdl`) |
| `-o, --output` | Output directory (default: current dir) |
| `-f, --format` | Output format: `dae` (default), `obj` |
| `-p, --parallel` | Max parallel jobs (default: CPU count) |
| `--list` | List all available `.trmdl` models |
| `--all` | Extract all models in parallel |
| `--list-packs` | Dump all pack names |
| `--generate-hashes` | Build hash list from archive structure |
| `--scan` | Find models by header inspection (no hash list needed) |
| `--split` | Export animations as separate clip DAEs (default) |
| `--baked` | Export animations embedded in model DAEs |

#### Examples

```bash
# List all models in the archive
minitoolbox trpak --arc ./romfs --list

# Extract a specific Pokemon model
minitoolbox trpak --arc ./romfs --model pokemon/pm0025/pm0025_00.trmdl -o ./pikachu

# Batch extract all models (8 parallel)
minitoolbox trpak --arc ./romfs --all -o ./export_all -p 8

# Scan for models without a hash list
minitoolbox trpak --arc ./romfs --scan -o ./scan_results
```

#### TRPAK Output Structure

```
output/
  pm0025_00/
    model.dae              # Static mesh + skeleton
    clips/
      clip_000.dae         # Animation clip
      clip_001.dae
      ...
    pm0025_00_BodyA1.png   # Textures (decoded from BNTX)
    ...
    manifest.json          # Metadata
```

---

### `gdb1` - Star Fox Zero/Guard Resources

Extracts models from GDB1 resource databases used in Star Fox Zero and Star Fox Guard.

#### Usage

```
minitoolbox gdb1 --input <resourceDir> [options]
```

#### Options

| Option | Description |
|--------|-------------|
| `-i, --input` | Input directory containing `.modelgdb`/`.texturegdb` files (required) |
| `-m, --model` | Model ID (filename without extension) |
| `-o, --output` | Output directory (default: current dir) |
| `-f, --format` | Output format: `obj` (default), `dae` |
| `-p, --parallel` | Max parallel jobs (default: CPU count) |
| `--scan` | Scan and report resource counts |
| `--list` | List all available models |
| `--all` | Extract all models in parallel |
| `--split` | Export animations as separate clip files (default) |
| `--baked` | Export animations embedded in model files |

#### Examples

```bash
# Scan resources
minitoolbox gdb1 --input ./Resources --scan

# List available models
minitoolbox gdb1 --input ./Resources --list

# Extract one model
minitoolbox gdb1 --input ./Resources --model 00b1486b -o ./starfox

# Batch extract
minitoolbox gdb1 --input ./Resources --all -o ./export_all -p 8
```

#### GDB1 Output Structure

```
output/
  00b1486b/
    model.obj              # Wavefront OBJ model
    model.mtl              # Material library
    texture_0.png          # Textures
    texture_1.png
    animation_0.json       # Animation data
    manifest.json          # Metadata
```

---

## Architecture

```
MiniToolbox.sln
  MiniToolbox.App/          CLI entry point + command routing
  MiniToolbox.Core/         Shared: BNTX textures, pipeline framework, Oodle decompression
  MiniToolbox.Garc/         3DS GARC: containers, LZSS/BLZ compression, BCH/GfModel parsers, DAE/OBJ export
  MiniToolbox.Trpak/        Switch TRPAK: FlatBuffer archive, TRMDL decoder, COLLADA export
  MiniToolbox.Gdb1/         Wii U GDB1: resource database, model/texture/animation extraction
  MiniToolbox.Hashes/       FNV-64 hash cache for TRPAK file lookup
```

### GARC Internal Format

GARC (Game ARChive) is the container format used in 3DS Pokemon games:

```
Header:  "GARC" magic, version 0x400, section count, data offset
FATO:    File Allocation Table Offsets (entry count + offset array)
FATB:    File Allocation Table Block (32-bit flags per FATO entry)
Data:    Raw/LZSS-compressed file data
```

Each entry is auto-detected by magic bytes:
- `PC` = Pokemon character model (GfModel)
- `CM` = Character model
- `BCH` = BCH model container
- `PT` = Pokemon texture
- `AD` = Animation descriptor
- `0x11` first byte = LZSS_Ninty compressed (recursive decompression)

### Extraction Pipeline

All format extractors implement `IFileGroupExtractor`:
1. **Enumerate** - discover extraction jobs from the archive
2. **Process** - decode format-specific data (models, textures, animations)
3. **Export** - write DAE/OBJ + PNG textures to disk
4. **Validate** - verify output completeness

The pipeline supports parallel extraction with configurable concurrency.

## File Formats Produced

### COLLADA DAE (`.dae`)
- Full mesh geometry (positions, normals, UVs, vertex colors)
- Skeletal hierarchy (`JOINT` nodes with `transform` matrices)
- Skin controllers (bone weights + inverse bind matrices)
- Material references to PNG textures
- Animation clips: matrix keyframes at 30 FPS

### Wavefront OBJ (`.obj`)
- Static mesh geometry only (no animation)
- Material references via `usemtl`
- UVs mapped to texture PNGs

### Textures (`.png`)
- Decoded from proprietary GPU formats (ETC1, RGBA8, etc.)
- Named after in-game material references
- Common sizes: 64x64, 128x128, 256x256, 512x512
