# Game Rig Builder

Blender 4.0 addon that creates game-specific armatures from scratch using bone data extracted from game model DAE files. No Rigify dependency.

## Approach

Instead of converting a Rigify metarig, this addon creates armatures directly from game model bone data. Each bone's position, tail, roll, and parent hierarchy are copied exactly from the game's Collada model export, ensuring animations play back correctly with 1:1 bone matching.

## Supported Games

- **Sun/Moon** — Pokemon Sun/Moon trainer skeleton (59 bones from tr0010_00/model.dae)
- **Scarlet** — Pokemon Scarlet/Violet (not yet implemented)
- **PZLA** — Pokemon Legends: Arceus (not yet implemented)

## Workflow

1. **Generate Game Rig** — Creates a fresh armature from the game's bone data
2. **Load Animation** — Import DAE animation clips (direct bone name matching)
3. **Unload Animation** — Remove animation, reset pose

## Limitations

- Bones use stub tails (0.01 units) matching the Collada import format — bones are only visible in Stick display mode as dots
- No mesh fitting — the rig uses fixed proportions from the game model
- Animations play back correctly because bone orientations exactly match the game's rest pose
- Centimeter scale (matching game DAE files)

## Bone Data Source

Bone data is extracted from game model DAE files using the Blender Collada importer. The exact head, tail, roll, and parent values are dumped from edit mode and hardcoded into the addon. See `dump_dae_bones.py` in the parent directory for the extraction script.

## Files

| File | Description |
|------|-------------|
| `__init__.py` | Addon registration |
| `generate.py` | Armature creation from hardcoded bone data |

## Install

Copy the `game_rig_builder/` folder to:
```
%APPDATA%/Blender Foundation/Blender/4.0/scripts/addons/
```

Enable in Blender: Edit > Preferences > Add-ons > search "Game Rig Builder"
