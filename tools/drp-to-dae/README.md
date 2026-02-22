# DRP to DAE Converter

Converts Pokken Tournament DX character DRP files to COLLADA (DAE) format for use in Blender and other 3D applications.

## Features

- DRP decryption and extraction
- NUD model parsing with DAE export
- NUT texture extraction to PNG
- VBN skeleton parsing with proper bone hierarchy
- OMO/BCA/BCL animation parsing with DAE clip export
- **Two export modes:**
  - **Split mode** (default): Separate model.dae + animation clip DAEs
  - **Baked mode**: Full model+animation per clip (works directly in Blender)
- Batch processing with memory optimization
- Model + animation pairing from separate folders
- Manifest generation with metadata

## Installation

```bash
cd D:\Projects\Starfield2026\tools\drp-to-dae\DrpToDae
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../publish
```

Executable: `D:\Projects\Starfield2026\tools\drp-to-dae\publish\DrpToDae.exe`

## Export Modes

### Split Mode (Default)

Exports a single `model.dae` with separate animation clip DAEs. Requires manual Action transfer in Blender.

```
character_folder/
  model.dae           # Static model (mesh + skeleton + skin + materials)
  textures/
    Tex_*.png         # Extracted textures
  clips/
    clip_000.dae      # Animation-only DAE (skeleton + channels)
    clip_001.dae
    ...
  manifest.json       # Metadata (model info, texture list, clip list)
```

### Baked Mode (`--baked`)

Exports each animation as a complete DAE with full model + skeleton + animation baked together. **Works directly in Blender without manual Action transfer.**

```
character_folder/
  textures/
    Tex_*.png         # Extracted textures
  baked/
    clip_000.dae      # Full model + animation (mesh + skeleton + skin + animation)
    clip_001.dae
    ...
  manifest.json       # Metadata
```

## Usage

### Single File
```bash
drp-to-dae model.drp ./output/
```

### Directory
```bash
drp-to-dae ./chrdep/master ./output/ [-n count]
```

### Batch Mode - Split (Default)
```bash
drp-to-dae --batch <chrdep_folder> <chrmhd_folder> <output_folder> [-n count]
```

### Batch Mode - Baked (Recommended for Blender)
```bash
drp-to-dae --batch <chrdep_folder> <chrmhd_folder> <output_folder> --baked [-n count]
```

This pairs:
- `chrdep/depa038_000.drp` (model)
- `chrmhd/chrmhda038_000.drp` (animations)

The batch mode automatically:
- Matches model DRPs (chrdep) with animation DRPs (chrmhd) by character ID
- Looks for skeleton DRPs in sibling `chrind` folder
- Processes multiple characters in parallel

### Analyze
```bash
drp-to-dae --analyze model.drp
drp-to-dae --analyze ./folder report.csv
```

### Extract Raw Files
```bash
drp-to-dae --extract-raw model.drp ./raw_output/
```

## Options

| Option | Description |
|--------|-------------|
| `-n, --number N` | Limit number of files/characters to process |
| `--baked` | Export full model+animation per clip (works in Blender directly) |

## Test Data Location

Test data is located at:

```
D:\Projects\Starfield2026\src\Starfield.Tests\pokken-dump\NX\packdata
```

### Folder Structure

| Folder | Contents |
|--------|----------|
| `chrdep/master/` | Character model DRPs (mesh, textures, skeleton) |
| `chrmhd/master/` | Character animation DRPs (OMO/BCA/BCL clips) |
| `chrind/master/` | Character skeleton DRPs (VBN files) |

### Example: Batch Export (Split Mode)

```bash
# Export first 5 characters in split mode
drp-to-dae --batch ^
  "D:\Projects\Starfield2026\src\Starfield.Tests\pokken-dump\NX\packdata\chrdep\master" ^
  "D:\Projects\Starfield2026\src\Starfield.Tests\pokken-dump\NX\packdata\chrmhd\master" ^
  "D:\Projects\Starfield2026\tools\drp-to-dae\output" ^
  -n 5
```

### Example: Batch Export (Baked Mode)

```bash
# Export all characters in baked mode (works directly in Blender)
drp-to-dae --batch ^
  "D:\Projects\Starfield2026\src\Starfield.Tests\pokken-dump\NX\packdata\chrdep\master" ^
  "D:\Projects\Starfield2026\src\Starfield.Tests\pokken-dump\NX\packdata\chrmhd\master" ^
  "D:\Projects\Starfield2026\tools\drp-to-dae\output-baked" ^
  --baked
```

### Example: Single Character (Pikachu)

```bash
drp-to-dae ^
  "D:\Projects\Starfield2026\src\Starfield.Tests\pokken-dump\NX\packdata\chrdep\master\depp025_000.drp" ^
  "D:\Projects\Starfield2026\tools\drp-to-dae\test-output\p025"
```

## manifest.json Format

```json
{
  "version": 1,
  "characterId": "p025",
  "model": {
    "file": "model.dae",
    "meshCount": 9,
    "boneCount": 115
  },
  "textures": [
    { "file": "textures/Tex_0x00000000.png" }
  ],
  "clips": [
    {
      "index": 0,
      "name": "idle",
      "file": "clips/clip_000.dae",
      "frameCount": 60,
      "fps": 30
    }
  ]
}
```

## Blender Import

### Baked Mode (Recommended)

1. File > Import > Collada (.dae)
2. Select any `baked/clip_XXX.dae`
3. Model imports with armature, mesh, AND animation ready to play
4. Press Space to play animation

### Split Mode

Blender's COLLADA importer creates a separate armature for each clip DAE. To use clips with the model:

1. Import `model.dae` (creates main armature)
2. Import `clips/clip_000.dae` (creates temp armature with Action)
3. In Dope Sheet > Action Editor, find the new Action
4. Select the main armature, assign the Action to it
5. Delete the temp armature
6. Repeat for each clip

A helper script for automated clip import is available at `blender_diagnostic.py`.

## File Format Support

| Format | Magic | Description | Status |
|--------|-------|-------------|--------|
| DRP | - | Encrypted archive | Decrypt/Extract |
| NUD | NDP3/NDWD | 3D model | Parse/Export DAE |
| NUT | NTP3/NTWD/NTWU | Textures | Export PNG |
| VBN | VBN / NBV | Skeleton | Parse with hierarchy |
| OMO | OMO | Animation (Smash 4) | Export DAE/JSON |
| BCA | BCA | Animation curves | Export DAE |
| BCL | BCL | Bone list | Parse |

## Dependencies

- .NET 8.0
- System.Numerics
- No external DLLs required (zlib built-in)

## Architecture

```
DrpToDae/
  Program.cs              # CLI entry point, batch processing
  Formats/
    Animation/
      AnimationExporter.cs  # DAE clip export (split mode)
      AnimationData.cs      # Animation keyframe data structures
      OMOReader.cs          # OMO format parser
      BCAReader.cs          # BCA/BCL format parser
    Collada/
      ColladaExporter.cs    # DAE model export + baked animation support
    DRP/
      DRPFile.cs            # DRP container parser
      DrpDecrypter.cs       # DRP decryption
    NUD/
      NUD.cs                # Model geometry parser
    NUT/
      NutExporter.cs        # Texture extraction
    VBN/
      VBN.cs                # Skeleton parser with hierarchy building
      Bone.cs               # Bone data with parent/children relationships
```

## Recent Changes

### Baked Export Mode
- Added `--baked` flag for SPICA-compatible export
- Each clip DAE contains full model + skeleton + animation
- Works directly in Blender without manual Action transfer

### Animation Matrix Ordering Fix
- Fixed: Animation matrices now use correct COLLADA column-major ordering
- Result: Animations play correctly instead of being distorted

### Bone Rest Pose Fix
- Fixed: Clip DAE skeleton nodes now include proper local transforms from VBN
- Previously: All bone matrices were identity, causing incorrect rest poses

### Bone Hierarchy (AnimationExporter)
- Fixed: Animation DAEs now export bones as nested `<node>` hierarchy instead of flat siblings
- Result: Blender imports as 1 armature with N bones instead of N separate armatures

### Skeleton Root ID (ColladaExporter)
- Fixed: `<skeleton>` tag now references actual root bone ID dynamically instead of hardcoded `#Bone_0_id`
- Result: Blender correctly binds mesh to armature

### Output Structure
- Changed: Animation folder renamed from `animations/` to `clips/` (split mode) or `baked/` (baked mode)
- Changed: Animation files renamed from `{name}_anim.dae` to `clip_NNN.dae`
- Added: `manifest.json` generation with model/texture/clip metadata

## Troubleshooting

**"No NUD model found"**: The DRP may not contain model data. Check with `--analyze`.

**"Animations: 0"**: Animations are in separate folders. Use `--batch` mode.

**Memory issues**: Tool auto-flushes memory after each character in batch mode.

**Blender shows 115 armatures**: Update to the latest version with the bone hierarchy fix.

**Mesh not attached to skeleton**: Update to the latest version with the skeleton root ID fix.

**Animation not playing / distorted**: Update to the latest version with the matrix ordering fix.

**Want animations to work directly in Blender?**: Use `--baked` mode.
