# Auto Rig Parent

Blender 4.0 addon that converts a Rigify human metarig into a game-specific skeleton. The workflow preserves Rigify's bone orientations and proportions while renaming, reparenting, and restructuring the hierarchy to match a target game skeleton.

## Workflow

1. **New Rig** — Creates a Rigify human metarig with arms straightened into a T-pose
2. **Load Model** — Import a DAE mesh to use as reference
3. **Fit Rig to Model** — BVH raycasting to snap metarig bones to mesh proportions (spine, arms, legs, fingers)
4. **Generate Game Rig** — Convert the fitted Rigify rig into the target game's skeleton:
   - Rename bones (Rigify names → game names)
   - Reparent hierarchy to match game skeleton
   - Create game-specific bones not present in Rigify
   - Delete Rigify-only bones
   - Preserve bone positions/orientations from the fitted rig
5. **Load Animation** — Import DAE animation clips onto the game rig
6. **Unload Animation** — Remove animation, reset pose

## Target Games

- **Sun/Moon** — Pokemon Sun/Moon trainer skeleton (~62 bones)
- **Scarlet** — Pokemon Scarlet/Violet (not yet implemented)
- **PZLA** — Pokemon Legends: Arceus (not yet implemented)

## Key Challenge

Game animations encode rotations relative to the game's rest pose bone orientations. When converting from Rigify orientations, the generator must either:

- **Transform bone orientations** to match the game model exactly (breaks visibility — game models use stub bones)
- **Keep Rigify orientations** and apply rotation corrections during animation import (retargeting approach)

The intended approach for this addon is to keep Rigify bone orientations/proportions from the fit step and handle any orientation mismatch during animation import, since the bones need to remain visible and usable in Blender.

## Files

| File | Description |
|------|-------------|
| `__init__.py` | Addon registration |
| `ui.py` | Panel, operators (New, Load Model, Load/Unload Animation, Fit, Generate, Reset View) |
| `fit.py` | BVH raycasting to fit metarig bones to mesh proportions |
| `generate.py` | Game rig generator (rename, reparent, create, delete bones) |
| `games/` | Game-specific skeleton definitions |

## Install

Copy the `auto_rig_parent/` folder to:
```
%APPDATA%/Blender Foundation/Blender/4.0/scripts/addons/
```

Enable in Blender: Edit > Preferences > Add-ons > search "Auto Rig Parent"

Requires the **Rigify** addon to be enabled.

## Panel Location

View3D > Sidebar (N) > Rig tab > "Auto Rig" panel
