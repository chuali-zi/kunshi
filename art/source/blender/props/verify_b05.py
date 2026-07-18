# B05 复核: 重开 .blend 验证 (修正后)
import sys, os, json
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
CASES = [
    (r'source/blender/props/PRP_Remote_v001.blend', 'PRP_Remote', 1500,
     {'x': 0.06, 'y': 0.18, 'z': 0.03}, ['Remote_Body', 'key_power', 'ir']),
    (r'source/blender/props/PRP_Books_Set_v001.blend', 'PRP_Books_Set', 7500,
     {}, ['Book_A', 'Book_B', 'Book_C', 'Book_D', 'Book_E', 'Book_Open']),
    (r'source/blender/props/PRP_Vase_v001.blend', 'PRP_Vase', 1500,
     {'z': 0.5}, ['Vase_Body', 'Vase_Roses']),
    (r'source/blender/props/PRP_Vase_Broken_v001.blend', 'PRP_Vase_Broken', 2000,
     {}, ['Vase_Roses_Fallen'] + ['Vase_Shard_%02d' % k for k in range(1, 9)]),
    (r'source/blender/props/PRP_WallClock_v001.blend', 'PRP_WallClock', 1500,
     {'x': 0.32, 'z': 0.32}, ['Clock_Body', 'Clock_Glass', 'Clock_Hand_H', 'Clock_Hand_M', 'Clock_Hand_S']),
    (r'source/blender/props/PRP_WallClock_Broken_v001.blend', 'PRP_WallClock_Broken', 2000,
     {'x': 0.32, 'z': 0.32}, ['Clock_Body', 'Clock_Hand_H', 'Clock_Hand_M', 'Clock_Hand_S',
                            'Clock_Shard_01', 'Clock_Shard_02', 'Clock_Shard_03']),
    (r'source/blender/props/PRP_Mirror_v001.blend', 'PRP_Mirror', 4000,
     {'x': 0.74, 'z': 1.25}, ['Mirror_Frame', 'Mirror_Plane']),
    (r'source/blender/props/PRP_Mirror_Broken_v001.blend', 'PRP_Mirror_Broken', 4000,
     {'x': 0.74, 'z': 1.25}, ['Mirror_Frame'] + ['Mirror_Crack_%02d' % k for k in range(1, 5)] +
     ['Mirror_Shard_%02d' % k for k in range(1, 4)]),
    (r'source/blender/props/PRP_DrawerClutter_v001.blend', 'PRP_DrawerClutter', 1500,
     {}, ['Clutter_Tape', 'Clutter_Newspaper', 'Clutter_Paper_1', 'Clutter_Paper_2']),
]

for rel, aid, budget, dims, must in CASES:
    path = os.path.join(ROOT, rel.replace('/', os.sep))
    bpy.ops.wm.open_mainfile(filepath=path)
    root = bpy.data.objects.get(aid)
    assert root, aid + ' root missing'
    names = {o.name for o in [root] + list(root.children_recursive)}
    missing = [m for m in must if m not in names]
    res = report(aid, root, budget, dims)
    res['missing_parts'] = missing
    # 单件尺寸明细 (书本逐册/玫瑰高度)
    if aid == 'PRP_Books_Set':
        det = {}
        for nm in ['Book_A', 'Book_B', 'Book_C', 'Book_D', 'Book_E', 'Book_Open']:
            o = bpy.data.objects[nm]
            zs = [(o.matrix_world @ Vector(c)).z for c in o.bound_box]
            det[nm] = {'z_min': round(min(zs), 3), 'z_max': round(max(zs), 3)}
        res['books'] = det
    if aid == 'PRP_Vase_Broken':
        # 碎片区直径
        pts = []
        for o in [root] + list(root.children_recursive):
            if o.type == 'MESH' and 'Shard' in o.name:
                pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
        xs = [p.x for p in pts]; ys = [p.y for p in pts]
        res['shard_area'] = {'dx': round(max(xs) - min(xs), 3), 'dy': round(max(ys) - min(ys), 3)}
    print('VERIFY ' + json.dumps(res))

print('B05_VERIFY_DONE')
