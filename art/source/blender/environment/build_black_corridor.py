# ENV_BlackCorridor — asset_spec_3d.md §4
# 约 1.2 宽 × 3 深 × 2.6 高, 接在门洞外(东墙外侧), 尽头无门无窗吞黑
# 单件 Corridor_Shell; 单槽近黑哑光; 面数越省越好
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
# "东墙"(Unity +X)外侧 = Blender -X 侧, 走廊向 -X 延伸
X0 = -3.22           # 东墙外侧面
DEPTH, W, H = 3.0, 1.2, 2.6
YC = -0.55           # 与门洞对中 (门洞 y∈[-1.0,-0.1])

clean_scene()
mat = make_mat('MAT_Corridor_Black', (0.008, 0.008, 0.009), rough=1.0)

root = empty('ENV_BlackCorridor', display='CUBE', size=0.1)
parts = []
parts.append(box('c', DEPTH, W, 0.1, loc=(X0 - DEPTH / 2, YC, -0.05)))          # 地
parts.append(box('c', DEPTH, W, 0.1, loc=(X0 - DEPTH / 2, YC, H + 0.05)))       # 顶
parts.append(box('c', DEPTH, 0.1, H, loc=(X0 - DEPTH / 2, YC - W / 2 - 0.05, H / 2)))  # 北壁
parts.append(box('c', DEPTH, 0.1, H, loc=(X0 - DEPTH / 2, YC + W / 2 + 0.05, H / 2)))  # 南壁
parts.append(box('c', 0.1, W + 0.2, H, loc=(X0 - DEPTH - 0.05, YC, H / 2)))     # 尽头封死
shell = join_into(parts, 'Corridor_Shell', root)
assign(shell, mat)

save_blend(os.path.join(ROOT, 'source/blender/environment/ENV_BlackCorridor_v001.blend'))
export_fbx(os.path.join(ROOT, 'exports/environment/ENV_BlackCorridor.fbx'))
report('ENV_BlackCorridor', root, 1500, {'x': DEPTH + 0.1, 'y': W + 0.2, 'z': H + 0.1})
