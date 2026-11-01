# PRP_KillerHolster: CH_Killer 风衣内侧枪套 + SOCKET_BackupPistol
# 决策依据: art/requirements/art_content_gap_spec_2026-08-06.md §3 低成本方案
#   - 风衣保持并入 CH_Killer 本体网格, 不做独立蒙皮风衣
#   - 备用枪来源可读性由本枪套件提供; 在 Unity 中 bone-parent 到 mixamorig:Spine1
# 坐标: 在 CH_Killer_rigged.fbx 绑定姿态世界空间中构建 (角色面朝 +Y, 左侧 -X,
#       全高约 0.9m, 与角色 FBX 同空间, 骨骼挂接后随角色缩放)
# 贴合方式: 对角色网格做 +Y→-Y 射线投射, 取风衣表面点偏移生成, 不靠手猜坐标
import sys
import math
from pathlib import Path

import bpy
from mathutils import Vector, Euler

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "_shared"))
from ak import (box, cyl, empty, make_mat, clean_scene, report,
                save_blend, preview)

ROOT = Path(__file__).resolve().parents[4]
KILLER_FBX = ROOT / "art" / "exports" / "characters" / "CH_Killer_rigged.fbx"
PISTOL_FBX = ROOT / "art" / "exports" / "props" / "PRP_Pistol_Backup.fbx"
OUT_FBX = ROOT / "art" / "exports" / "props" / "PRP_KillerHolster.fbx"
OUT_BLEND = ROOT / "art" / "source" / "blender" / "props" / "PRP_KillerHolster_v001.blend"

clean_scene()

# ---------------------------------------------------------------- 参考件(不导出)
bpy.ops.import_scene.fbx(filepath=str(KILLER_FBX))
ref_objs = set(bpy.data.objects)
# NIUA 合并管线产物里混有一个 1m Icosphere 残留, 删掉以免干扰射线贴合
for o in [o for o in bpy.data.objects if o.type == 'MESH'
          and not any(m.type == 'ARMATURE' for m in o.modifiers)]:
    bpy.data.objects.remove(o, do_unlink=True)
killer_mesh = next(o for o in bpy.data.objects if o.type == 'MESH')

bpy.ops.import_scene.fbx(filepath=str(PISTOL_FBX))
pistol_objs = [o for o in bpy.data.objects if o not in ref_objs]
pistol_root = next((o for o in pistol_objs if o.parent is None), pistol_objs[0])

deps = bpy.context.evaluated_depsgraph_get()


def coat_surface(x, z, back=0.0):
    """+Y 方向投射, 返回风衣前表面 y 坐标; back>0 时向外偏移"""
    hit, loc, *_ = bpy.context.scene.ray_cast(
        deps, Vector((x, 2.0, z)), Vector((0, -1, 0)))
    if not hit:
        raise RuntimeError(f"raycast miss at x={x} z={z}")
    return loc.y + back


def seg_strap(name, p1, p2, width, thick, mat, parent):
    """p1→p2 之间的带状段, 沿表面铺设"""
    p1, p2 = Vector(p1), Vector(p2)
    mid = (p1 + p2) / 2
    direction = p2 - p1
    length = direction.length
    o = box(name, width, thick, length, loc=mid, bev=0.002, mat=mat, parent=parent)
    o.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()
    return o


# ---------------------------------------------------------------- 材质
mat_leather = make_mat("MAT_Holster_Leather", (0.035, 0.030, 0.030), rough=0.7)
mat_metal = make_mat("MAT_Metal_Dark", (0.05, 0.05, 0.055), metallic=0.8, rough=0.4)

# ---------------------------------------------------------------- 枪套组件
root = empty("PRP_KillerHolster", (0, 0, 0), display='PLAIN_AXES')

# 枪套口袋: 左胸(角色左侧=-X), 斜置, 枪口朝前下, 握把朝后上(右手交叉拔枪角)
POCKET_X, POCKET_Z = -0.062, 0.660
POCKET_ROT = Euler((math.radians(-18), 0, math.radians(12)), 'XYZ')
surf = coat_surface(POCKET_X, POCKET_Z, back=0.018)  # 口袋中心略离表面
pocket = box("Holster_Pocket", 0.050, 0.030, 0.115,
             loc=(POCKET_X, surf, POCKET_Z), bev=0.006,
             mat=mat_leather, parent=root)
pocket.rotation_euler = POCKET_ROT

# 口袋固定带
strap_y = coat_surface(POCKET_X, POCKET_Z + 0.030, back=0.034)
strap = box("Holster_RetainStrap", 0.058, 0.012, 0.016,
            loc=(POCKET_X, strap_y, POCKET_Z + 0.030), bev=0.003,
            mat=mat_leather, parent=root)
strap.rotation_euler = POCKET_ROT

# 斜挎肩带: 右腰 → 左肩, 分 4 段贴合胸面
sash_line = [(0.085, 0.500), (0.045, 0.585), (0.0, 0.660), (-0.055, 0.720)]
sash_pts = [(x, coat_surface(x, z, back=0.006), z) for x, z in sash_line]
for i in range(len(sash_pts) - 1):
    seg_strap(f"Holster_Sash_{i}", sash_pts[i], sash_pts[i + 1],
              0.020, 0.005, mat_leather, root)

# 胸围横带: 左肋 → 右肋, 分 3 段
belt_line = [(-0.080, 0.555), (-0.027, 0.550), (0.027, 0.550), (0.080, 0.555)]
belt_pts = [(x, coat_surface(x, z, back=0.005), z) for x, z in belt_line]
for i in range(len(belt_pts) - 1):
    seg_strap(f"Holster_Belt_{i}", belt_pts[i], belt_pts[i + 1],
              0.020, 0.005, mat_leather, root)

# 金属按扣
snap = cyl("Holster_Snap", 0.006, 0.008,
           loc=(POCKET_X, strap_y + 0.010, POCKET_Z + 0.030),
           rot=(math.radians(90), 0, 0), verts=12, mat=mat_metal, parent=root)

# 备用枪 socket: 位于口袋口, 朝向与口袋一致; Unity 中 Attach/Detach 用
socket_loc = Vector((POCKET_X, surf, POCKET_Z)) + Vector((0, 0, 0.062))
socket_loc = Vector((POCKET_X, surf, POCKET_Z)) + POCKET_ROT.to_matrix() @ Vector((0, 0, 0.062))
socket = empty("SOCKET_BackupPistol", socket_loc, parent=root,
               display='ARROWS', size=0.03)
socket.rotation_euler = POCKET_ROT

# ---------------------------------------------------------------- 预览用手枪(不导出; 半尺寸匹配角色空间)
if pistol_root:
    pistol_root.scale = tuple(s * 0.5 for s in pistol_root.scale)  # 角色空间约半尺寸
    pistol_root.location = socket_loc - POCKET_ROT.to_matrix() @ Vector((0, 0, 0.045))  # 插入袋中
    pistol_root.rotation_euler = POCKET_ROT

# ---------------------------------------------------------------- 自检/导出/预览
rep = report("PRP_KillerHolster", root, 1500, {})

save_blend(str(OUT_BLEND))

bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
for c in root.children_recursive:
    c.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=str(OUT_FBX),
    use_selection=True,
    object_types={'EMPTY', 'MESH'},
    bake_space_transform=True,
    axis_forward='-Z',
    axis_up='Y',
    apply_scale_options='FBX_SCALE_NONE',
    add_leaf_bones=False,
)
print(f"[fbx] -> {OUT_FBX}")

preview(str(ROOT / "art" / "previews" / "PRP_KillerHolster_v001.png"),
        target=(-0.02, 0.05, 0.66), dist=0.55, angle=195, height=0.2)
print("done")
