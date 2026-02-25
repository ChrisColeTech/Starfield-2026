bl_info = {
    "name": "Game Rig Builder",
    "author": "Starfield2026",
    "version": (1, 0, 0),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar > GameRig",
    "description": "Create game-specific armatures from model data, load DAE animations",
    "category": "Rigging",
}

from . import ui


def register():
    ui.register()


def unregister():
    ui.unregister()
