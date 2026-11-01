"""Blender 无头批量转换:NIUA 产物 GLB → Unity 用 FBX。

- 角色(rigged GLB):静态骨骼+蒙皮,不烘焙动画
- 动画(mixamo 骨架 GLB):烘焙动画曲线

用法:
  blender -b -P convert_all_fbx.py
"""

import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[2]  # art/
CHAR_DIR = ROOT / "exports" / "characters"
ANIM_DIR = ROOT / "exports" / "animations"


def convert(src: Path, dst: Path, with_anim: bool) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(src))
    bpy.ops.export_scene.fbx(
        filepath=str(dst),
        path_mode="COPY",
        embed_textures=True,
        add_leaf_bones=False,
        bake_anim=with_anim,
    )
    print(f"[fbx] {src.name} -> {dst.name}")


for glb in sorted(CHAR_DIR.glob("CH_*_rigged.glb")):
    convert(glb, glb.with_suffix(".fbx"), with_anim=False)

for pattern in ("AN_*.glb", "POSE_*.glb", "PAIR_*.glb"):
    for glb in sorted(ANIM_DIR.glob(pattern)):
        fbx = glb.with_suffix(".fbx")
        if fbx.exists() and fbx.stat().st_mtime >= glb.stat().st_mtime:
            continue  # 已转换且未更新,跳过
        convert(glb, fbx, with_anim=True)

print("done")
