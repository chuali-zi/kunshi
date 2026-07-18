# PRP_BaseballBat — asset_spec_3d.md §6
# 全长 0.85m, 粗端直径 0.065m; 原点在握持区中部(距细端 0.15m), 长轴沿 -Y
# 拆件: 单件; 材质: MAT_Wood_Worn (浅色木); 预算 S ≤1500
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
clean_scene()

mat = make_mat('MAT_Wood_Worn', (0.62, 0.46, 0.30), rough=0.6)

# s=距细端距离; y = 0.15 - s (细端在 +Y 0.15, 粗端在 -Y 0.70, 原点=握持区)
profile_s = [  # (s, r)
    (0.000, 0.0001), (0.004, 0.017), (0.010, 0.0215), (0.018, 0.0225),  # 尾帽
    (0.028, 0.016), (0.050, 0.0135), (0.150, 0.0140),                  # 握把(0.15=原点)
    (0.300, 0.0165), (0.450, 0.0230), (0.550, 0.0285),                 # 过渡
    (0.650, 0.0315), (0.720, 0.0325), (0.780, 0.0325),                 # 粗端 0.065
    (0.830, 0.0300), (0.845, 0.0240), (0.850, 0.0001),                 # 端头圆角
]
profile = [(0.15 - s, r) for s, r in profile_s]

# 单件资产, 网格本体即根对象 (规格: 单件 PRP_BaseballBat)
bat = lathe('PRP_BaseballBat', profile, segments=24, axis='Y', mat=mat)
root = bat

save_blend(os.path.join(ROOT, 'source/blender/props/PRP_BaseballBat_v001.blend'))
export_fbx(os.path.join(ROOT, 'exports/props/PRP_BaseballBat.fbx'))
report('PRP_BaseballBat', root, 1500, {'y': 0.85, 'x': 0.065, 'z': 0.065})
preview(os.path.join(ROOT, 'previews/props/PRP_BaseballBat.png'),
        target=(0, -0.25, 0), dist=1.6, angle=30, height=0.35)
