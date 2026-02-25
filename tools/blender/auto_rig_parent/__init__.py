bl_info = {
    "name": "Auto Rig Parent",
    "author": "Starfield2026",
    "version": (1, 0, 0),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar > Rig",
    "description": "Fit Rigify metarig to mesh, generate game rigs, parent with auto weights",
    "category": "Rigging",
}

from . import ui


def register():
    ui.register()


def unregister():
    ui.unregister()
