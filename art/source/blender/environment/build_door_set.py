# ENV_DoorSet — asset_spec_3d.md §4
# 门板 0.9×2.05×0.04, 把手高 1.0; 门轴在洞口北侧, 向房内开
# 坐标映射 (Unity 实证): 东墙(Unity +X) = Blender -X; 铰链 Unity z=1.0 → Blender y=-1.0
# 拆件: Door_Frame(原点同房间壳) / Door_Panel(原点在铰链轴底端) / SOCKET_Handle / Door_Handle
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
EX = -3.1           # "东墙"内面 (Blender -X)
HINGE_Y = -1.0      # 铰链轴 (洞口北缘)
PW, PH, PT = 0.9, 2.05, 0.04

clean_scene()
mat_wood = make_mat('MAT_Wood_Worn', (0.70, 0.66, 0.58), rough=0.7)   # 旧白浅木
mat_metal = make_mat('MAT_Metal_Dark', (0.04, 0.04, 0.045), metallic=0.8, rough=0.45)

root = empty('ENV_DoorSet', display='CUBE', size=0.1)

# ---- 门框 (原点=世界原点) ----
fj = []  # 门套: 两侧梃 + 上楣, 覆盖墙厚 0.12, 梃宽 0.06
fj.append(box('fj', 0.16, 0.06, 2.11, loc=(EX, HINGE_Y - 0.03, 2.11 / 2)))          # 北梃
fj.append(box('fj', 0.16, 0.06, 2.11, loc=(EX, HINGE_Y + PW + 0.03, 2.11 / 2)))     # 南梃
fj.append(box('fj', 0.16, 0.9 + 0.12, 0.06, loc=(EX, HINGE_Y + PW / 2, 2.05 + 0.03)))  # 上楣
frame = join_into(fj, 'Door_Frame', root)
assign(frame, mat_wood)

# ---- 门板: 原点在铰链轴底端 (x=EX, y=HINGE_Y, z=0) ----
# 网格局部: x 0→-PT(向西进墙厚), y 0→0.89(向南), z 0→2.04; 房内侧面 = +X 侧
panel_parts = []
panel_parts.append(box('panel', PT, PW - 0.01, PH - 0.02,
                       loc=(-PT / 2, (PW - 0.01) / 2 + 0.005, PH / 2), bev=0.004))

def mold(name, sy, sz, y, z):
    # 房内侧面(x=0 侧)上的装饰线框条, 微微凸出板面
    return box(name, 0.012, sy, sz, loc=(0.002, y, z), bev=0.003)

# 上部装饰线框 (z 1.2~1.9), 下部 0.9~1.0 留平整区(椅背顶门接触位)
panel_parts.append(mold('mold', 0.73, 0.03, 0.445, 1.20))
panel_parts.append(mold('mold', 0.73, 0.03, 0.445, 1.90))
panel_parts.append(mold('mold', 0.03, 0.70, 0.08, 1.55))
panel_parts.append(mold('mold', 0.03, 0.70, 0.81, 1.55))
# 下部浅饰板 (z 0.15~0.80, 避开 0.9~1.0 接触区)
panel_parts.append(mold('mold', 0.73, 0.03, 0.445, 0.15))
panel_parts.append(mold('mold', 0.73, 0.03, 0.445, 0.80))
panel_parts.append(mold('mold', 0.03, 0.65, 0.08, 0.475))
panel_parts.append(mold('mold', 0.03, 0.65, 0.81, 0.475))
panel = join_into(panel_parts, 'Door_Panel', root)
assign(panel, mat_wood)
panel.location = (EX, HINGE_Y, 0)   # 原点即铰链轴底端

# ---- 把手: 独立件, 原点在转轴 (面板南侧末端, 高 1.0) ----
HANDLE_LOCAL_Y = 0.72   # 距铰链 0.72 (自由端)
hy = HINGE_Y + HANDLE_LOCAL_Y
hz = 1.0
handle_parts = []
# 局部: 主轴沿 X, x∈[-PT-0.035, 0.035], x=0 侧朝房内
handle_parts.append(cyl('h', 0.025, 0.008, loc=(0, 0, 0), rot=(0, math.pi / 2, 0), verts=20))
handle_parts.append(cyl('h', 0.008, PT + 0.02, loc=(-PT / 2, 0, 0), rot=(0, math.pi / 2, 0), verts=12))
handle_parts.append(cyl('h', 0.025, 0.008, loc=(-PT, 0, 0), rot=(0, math.pi / 2, 0), verts=20))
handle_parts.append(box('h', 0.015, 0.10, 0.018, loc=(0.035, -0.05, 0), bev=0.004))   # 房内执手
handle_parts.append(box('h', 0.015, 0.10, 0.018, loc=(-PT - 0.035, -0.05, 0), bev=0.004))
handle = join_into(handle_parts, 'Door_Handle', root)
assign(handle, mat_metal)
handle.location = (EX + 0.01, hy, hz)   # 房内一侧起

# ---- 把手 SOCKET (门板子件, 随门转动) ----
empty('SOCKET_Handle', loc=(-0.02, HANDLE_LOCAL_Y, 1.0), parent=panel, display='SPHERE', size=0.02)

save_blend(os.path.join(ROOT, 'source/blender/environment/ENV_DoorSet_v001.blend'))
export_fbx(os.path.join(ROOT, 'exports/environment/ENV_DoorSet.fbx'))
report('ENV_DoorSet', root, 4000, {'y': 1.0, 'z': 2.11})
