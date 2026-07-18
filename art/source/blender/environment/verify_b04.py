# B04 复核: 重开 .blend 源文件做 §1.9 自检 (不重建, 只验)
# 检查: 三角面预算 / 包围盒 / 关键拆件与原点 / SOCKET 挂点
import sys, os, json
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
CASES = [
    # (blend路径, 资产ID, 预算, 期望尺寸, 必须存在的对象名片段, 原点检查)
    (r'source/blender/environment/FUR_Dresser_v001.blend', 'FUR_Dresser', 8000,
     {'x': 1.1, 'z': 0.85},
     ['Dresser_Body', 'Dresser_Drawer_Bot', 'Dresser_Drawer_Mid', 'Dresser_Drawer_Top',
      'SOCKET_LampTop', 'SOCKET_RemoteTop'], 'dresser'),
    (r'source/blender/environment/FUR_SideTable_v001.blend', 'FUR_SideTable', 1500,
     {'x': 0.45, 'z': 0.555},
     ['SideTable_Body', 'SOCKET_Vase'], None),
    (r'source/blender/props/PRP_Rug_v001.blend', 'PRP_Rug', 4000,
     {'x': 3.5, 'y': 0.9},
     ['Rug_Body', 'Rug_GrabCorner'], None),
    (r'source/blender/environment/ENV_BoardedWindow_v001.blend', 'ENV_BoardedWindow', 4000,
     {'x': 1.44, 'z': 1.14},
     ['Window_Frame', 'Window_BrickWall'] +
     ['Window_Board_%02d' % k for k in range(1, 6)] +
     ['Window_Board_Broken_%02d' % k for k in range(1, 6)], None),
]

for rel, aid, budget, dims, must, special in CASES:
    path = os.path.join(ROOT, rel.replace('/', os.sep))
    bpy.ops.wm.open_mainfile(filepath=path)
    root = bpy.data.objects.get(aid)
    assert root, aid + ' root missing'
    names = {o.name for o in [root] + list(root.children_recursive)}
    missing = [m for m in must if m not in names]
    res = report(aid, root, budget, dims)
    res['missing_parts'] = missing
    # 抽屉原点: 应位于屉盒后缘(局部 +Y 侧), 不在几何中心 → 包围盒中心应明显偏离原点
    if special == 'dresser':
        off = {}
        for d in ('Dresser_Drawer_Bot', 'Dresser_Drawer_Mid', 'Dresser_Drawer_Top'):
            o = bpy.data.objects[d]
            cx = sum((o.matrix_world @ Vector(c)).y for c in o.bound_box) / 8
            off[d] = round(cx, 4)   # 滑轨后端原点 → 盒心应在原点前方(-Y), 即 cy < -0.1
        res['drawer_center_y'] = off
        res['drawer_origin_ok'] = all(v < -0.1 for v in off.values())
    print('VERIFY ' + json.dumps(res))

print('B04_VERIFY_DONE')
