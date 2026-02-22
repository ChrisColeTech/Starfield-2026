# BCA to DAE Animation Converter (R&D)

A CLI tool for analyzing and converting Pokken Tournament BCA/BCL/BCH animation formats.

## Status: Research & Development

This tool is currently in the reverse-engineering phase. BCA animations can be extracted as raw files, but full DAE conversion requires completing the format analysis.

## Installation

```bash
cd D:\Projects\Starfield2026\tools\drp-to-dae\BcaToDae
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../bca-publish
```

Executable: `D:\Projects\Starfield2026\tools\drp-to-dae\bca-publish\BcaToDae.exe`

## Usage

### Dump File Structure
```bash
bca-to-dae dump animation.bca
bca-to-dae dump skeleton.vbn
bca-to-dae dump bonelist.bcl
```

### Deep Analysis
```bash
bca-to-dae analyze animation.bca
```

### Batch Dump
```bash
bca-to-dae batch-dump ./extracted_folder/
```

### Convert (Future)
```bash
bca-to-dae convert animation.bca skeleton.vbn output.dae
```

## File Formats

| Format | Magic | Description | Analysis Status |
|--------|-------|-------------|-----------------|
| BCA | BCA | Bone Curve Animation | 🔄 Parsing keyframes |
| BCL | BCL | Bone Curve List | 🔄 Parsing bone indices |
| BCH | BCH | Bone Curve Header | 🔄 Parsing metadata |
| BCS | BCS | Bone Curve Scene | ✅ Scene scripts |
| BHA | BHA | Bone Hitbox Animation | ⏳ Not analyzed |
| VBN | VBN / NBV | Skeleton | ✅ 85 bones parsed |

## Key Findings

### BCA (Bone Curve Animation)
```
Offset  Value        Description
0x08    192          Track count
0x14    6.1618       Rotation (radians)
0x18    100          Frame count
0x1C    26           Key count
0x2C    2.9703       Another rotation value
```

### VBN (Skeleton)
- 85 bones for p006 (Charizard)
- Bone names: BASE, Spine1, Spine2, Hip, Neck1, Head, etc.
- Each bone has hash ID (e.g., `0x4F101F52` for "BASE")

### BCL (Bone List)
- 137 entries (not 85 - may be track count, not bone count)
- Sparse structure, mostly zeros
- May use indices instead of names

### BCS (Scene Script)
- Human-readable strings: "EventList.Start", "Camera.Start"
- Animation references: "eset_006_demo_StartA"
- Frame ranges for clips

## Folder Structure

```
chrmhd/master/
├── chrmhda038_000.drp    # Animations for character a038
├── chrmhda050_000.drp    # Animations for character a050
└── ...

extracted_p006/
├── BCA_0_p006_000_bca.raw    # Keyframe data
├── BCH_0_p006_000_bch.raw    # Header/metadata
├── BCL_0_p006_000_bcl.raw    # Bone list
├── BCS_0_p006_start_a_bcs.raw
├── BCS_1_p006_start_b_bcs.raw
└── ... (252 scene files, 1 BCA, 1 BCH, 1 BCL)
```

## Related Folders

| Folder | Contents | Animation Format |
|--------|----------|------------------|
| chrdep | Models + Textures | None |
| chrind | Models + Skeleton + Animations | OMO (working) |
| chrmhd | Animations only | BCA/BCL/BCH (needs RE) |

## Next Steps

1. Parse BCL to get bone name/index mapping
2. Parse BCH to get frame count and bone references
3. Parse BCA to extract keyframe data
4. Map BCA tracks to VBN bones
5. Export to Collada DAE animation format

## References

- Smash-Forge OMO parser: Working reference for animation parsing
- Pokken VBN skeleton: 85 bones with hash IDs
- DAE animation format: Matrix channels per bone
