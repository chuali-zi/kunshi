# 检查 CH_Killer_rigged.fbx 骨架结构: 骨骼名/头部世界坐标/父子关系
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[4]
FBX = ROOT / "art" / "exports" / "characters" / "CH_Killer_rigged.fbx"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(FBX))

arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
print(f"ARMATURE {arm.name} scale={tuple(round(s,4) for s in arm.scale)}")

deps = bpy.context.evaluated_depsgraph_get()
bones = []
for b in arm.data.bones:
    head_w = arm.matrix_world @ b.head_local
    bones.append((b.name, b.parent.name if b.parent else '-',
                  round(head_w.x, 3), round(head_w.y, 3), round(head_w.z, 3)))
for name, parent, x, y, z in bones:
    print(f"BONE {name:20s} parent={parent:20s} head=({x:7.3f},{y:7.3f},{z:7.3f})")

# 网格包围盒, 确认人体朝向和胸口高度
for o in bpy.data.objects:
    if o.type == 'MESH':
        pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
        xs = [p.x for p in pts]; ys = [p.y for p in pts]; zs = [p.z for p in pts]
        print(f"MESH {o.name} x=({min(xs):.2f},{max(xs):.2f}) "
              f"y=({min(ys):.2f},{max(ys):.2f}) z=({min(zs):.2f},{max(zs):.2f})")
