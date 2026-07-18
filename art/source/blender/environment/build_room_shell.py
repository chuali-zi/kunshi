# ENV_RoomShell — asset_spec_3d.md §4
# 内净 6.2(X东西) × 5.2(Y南北) × 2.8(高); 墙厚 0.12; 原点=地面中心 (0,0,0)
# 坐标映射 (Unity 6000.4.6f1 实证, axisprobe): Blender(x,y,z) → Unity(-x, z, -y)
#   即: Unity 东(+X) = Blender -X; Unity 北(+Z) = Blender -Y; 上 = +Z
#   门洞: Unity 东墙偏北 z∈[0.1,1.0] → Blender x=-3.1 墙, y∈[-1.0,-0.1]
#   窗洞: Unity 北墙偏西 x=-1.2      → Blender y=-2.6 墙, x=+1.2, 窗台 0.9, 1.4×1.1
# 拆件: Shell_Floor/Ceiling/Wall_N/S/W/E/Skirting (命名按 Unity 侧语义)
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
X, Y, H, T = 6.2, 5.2, 2.8, 0.12   # 内净尺寸 / 墙厚
NY, SY = -Y / 2, Y / 2              # 北墙内面 y=-2.6, 南墙内面 y=+2.6 (Blender 方位)
EBX, WBX = -X / 2, X / 2            # "东墙"(Unity +X)在 Blender -X 侧

clean_scene()
mat_wall = make_mat('MAT_Wallpaper_FloralAged', (0.76, 0.70, 0.58), rough=0.85)
mat_floor = make_mat('MAT_Floor_DarkGreen', (0.14, 0.20, 0.16), rough=0.95)
mat_ceil = make_mat('MAT_Ceiling_White', (0.85, 0.84, 0.80), rough=0.9)

root = empty('ENV_RoomShell', display='CUBE', size=0.2)

# 地面 / 天花 (含厚度, 顶面齐 0 / 底面齐 2.8)
floor = box('Shell_Floor', X, Y, 0.10, loc=(0, 0, -0.05), mat=mat_floor, parent=root)
ceil = box('Shell_Ceiling', X, Y, 0.10, loc=(0, 0, H + 0.05), mat=mat_ceil, parent=root)

# 南墙(整面) / "西墙"(Blender +X 整面)
wall_s = box('Shell_Wall_S', X + 2 * T, T, H, loc=(0, SY + T / 2, H / 2), mat=mat_wall, parent=root)
wall_w = box('Shell_Wall_W', T, Y, H, loc=(WBX + T / 2, 0, H / 2), mat=mat_wall, parent=root)

# 北墙: 窗洞 1.4×1.1, 窗台 0.9, 中心 Blender x=+1.2 (Unity x=-1.2 偏西)
wall_n = box('Shell_Wall_N', X + 2 * T, T, H, loc=(0, NY - T / 2, H / 2), mat=mat_wall, parent=root)
hole_w = box('hole', 1.4, T * 3, 1.1, loc=(1.2, NY - T / 2, 0.9 + 1.1 / 2))
mod = wall_n.modifiers.new('WinHole', 'BOOLEAN'); mod.operation = 'DIFFERENCE'; mod.object = hole_w
apply_mod(wall_n, mod.name)
bpy.data.objects.remove(hole_w, do_unlink=True)

# "东墙"(Blender -X): 门洞 0.9×2.05, Blender y∈[-1.0,-0.1]
wall_e = box('Shell_Wall_E', T, Y, H, loc=(EBX - T / 2, 0, H / 2), mat=mat_wall, parent=root)
hole_d = box('hole', T * 3, 0.9, 2.05, loc=(EBX - T / 2, -0.55, 2.05 / 2))
mod = wall_e.modifiers.new('DoorHole', 'BOOLEAN'); mod.operation = 'DIFFERENCE'; mod.object = hole_d
apply_mod(wall_e, mod.name)
bpy.data.objects.remove(hole_d, do_unlink=True)

# 踢脚线: 高 0.08 厚 0.015, 贴各墙内面 ("东墙"避开门洞分两段)
sk = []
sk.append(box('sk', X, 0.015, 0.08, loc=(0, SY - 0.0075, 0.04)))          # 南
sk.append(box('sk', 0.015, Y, 0.08, loc=(WBX - 0.0075, 0, 0.04)))          # 西(Blender +X)
sk.append(box('sk', X, 0.015, 0.08, loc=(0, NY + 0.0075, 0.04)))          # 北
sk.append(box('sk', 0.015, 1.6, 0.08, loc=(EBX + 0.0075, -1.8, 0.04)))     # 东北段 y[-2.6,-1.0]
sk.append(box('sk', 0.015, 2.7, 0.08, loc=(EBX + 0.0075, 1.25, 0.04)))     # 东南段 y[-0.1,2.6]
skirting = join_into(sk, 'Shell_Skirting', root)
assign(skirting, mat_wall)

# 墙面平直 UV (接受击打 Decal)
for o in (wall_n, wall_s, wall_w, wall_e, floor, ceil, skirting):
    cube_uv(o)

save_blend(os.path.join(ROOT, 'source/blender/environment/ENV_RoomShell_v001.blend'))
export_fbx(os.path.join(ROOT, 'exports/environment/ENV_RoomShell.fbx'))
report('ENV_RoomShell', root, 8000, {'x': X + 2 * T, 'y': Y + 2 * T, 'z': H + 0.1})
