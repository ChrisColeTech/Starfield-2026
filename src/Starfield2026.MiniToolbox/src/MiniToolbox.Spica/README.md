# MiniToolbox.Spica

3DS model/texture/animation library for Pokemon Sun/Moon, X/Y, and other titles using the PICA200 GPU. Handles GFModel, GFTexture, GFMotion, BCH (H3D), and CGFX formats with export to COLLADA DAE.

Forked from [SPICA](https://github.com/gdkchan/SPICA) and integrated into MiniToolbox with namespace `MiniToolbox.Spica`.

## Directory Structure

```
Formats/
  CtrH3D/          H3D scene graph (models, textures, animations, materials, skeleton)
  CtrGfx/          CGFX format support
  GFL2/            Game Freak GFModel/GFTexture/GFMotion (Sun/Moon, X/Y)
  GFL/             Game Freak gen-1 motion format (older titles)
  GFLX/            GFLXPack format (Let's Go, Sword/Shield)
  Generic/
    COLLADA/        DAE export (model + skeleton + skinning + animation clips)
    StudioMdl/      SMD import
    WavefrontOBJ/   OBJ import
  ModelBinary/      MBn format
  MTFramework/      MT Framework model/shader/texture (Monster Hunter)
  Packages/         GFPackage container format
  Common/           Shared utilities (CRC32, FNV1a, texture transforms)

PICA/
  Commands/         PICA200 GPU register definitions
  Converters/       Vertex buffer, texture, and bone matrix converters
  Shader/           PICA200 shader disassembly

Serialization/      Binary serializer/deserializer with attribute-driven layout
Math3D/             Vector, matrix, RGBA types
Compression/        LZ4 decompression

Cli/
  Formats/          Format identification, GARC reader, LZSS decompression,
                    Pokemon model/texture/animation loaders (GFPkmnModel, etc.),
                    PICA texture combiner baker (PicaTextureBaker)
```

## Key Types

| Type | Description |
|------|-------------|
| `H3D` | Top-level scene container. Models, textures, animations. `H3D.Open(byte[])` for BCH files. |
| `H3DModel` | Model with meshes, materials, skeleton. |
| `H3DTexture` | Texture with `ToBitmap()` and `ToRGBA()` for decoding PICA200 formats (ETC1, RGBA8, etc). |
| `DAE` | COLLADA exporter. `new DAE(scene, modelIndex)` for models, `new DAE(scene, modelIndex, animIndex, clipOnly: true)` for animation clips. |
| `FormatIdentifier` | Auto-detects format from stream and returns `H3D` scene. Handles BCH, GFPackage (PC/PK/PT/etc), GFModel, GFTexture, GFMotion. |
| `GARC` | Reads GARC archive entries (offsets + lengths). |
| `LZSS` | Nintendo LZ11 decompression (header byte `0x11`). |
| `PicaTextureBaker` | Software rasterizer for the PICA200 6-stage texture combiner. Bakes multi-texture materials into single diffuse PNGs. |

## Usage from MiniToolbox.App

The `garc` command uses this library:

```
minitoolbox garc -i <garcFile> --extract -o <outputDir>
```

Pipeline: GARC entries -> LZSS decompress -> FormatIdentifier -> H3D scene -> merge textures/animations -> DAE export + PNG textures + clip DAEs + manifest.json
