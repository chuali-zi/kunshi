"""Blender 无头模式:GLB → FBX(嵌入贴图)。

用法:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b -P convert_glb_fbx.py -- input.glb output.fbx
"""

import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
bpy.ops.export_scene.fbx(
    filepath=dst,
    path_mode="COPY",
    embed_textures=True,
    add_leaf_bones=False,
    bake_anim=False,
)
print(f"converted {src} -> {dst}")
