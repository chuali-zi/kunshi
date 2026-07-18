# PRP_CeilingLamp 两态 — asset_spec_3d.md §6
# 黑罩直径 0.45, 吊杆全长 0.35, 天花安装点高 2.8 → 原点在顶端安装点, 灯体向下垂(-Z)
# 拆件: Lamp_Rod / Lamp_Shade / Lamp_Bulb(独立发光槽); _Broken: 罩缘破口+灯泡碎, 碎件≤6
# 预算 M ≤4000/态
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'

MAT_METAL = lambda: make_mat('MAT_Metal_Dark', (0.04, 0.04, 0.045), metallic=0.8, rough=0.45)
MAT_INNER = lambda: make_mat('MAT_LampShade_Inner', (0.80, 0.75, 0.68), rough=0.4)  # 罩内浅色反光面
MAT_BULB = lambda: make_mat('MAT_Lamp_Emissive', (0.9, 0.85, 0.75), rough=0.3,
                            emission=(1.0, 0.8, 0.5), estr=2.0)

ROD_LEN = 0.35          # 吊杆全长
SHADE_D = 0.45          # 罩径


def build_shade(name, mat_out, mat_in, root, broken=False):
    """半球罩+折边: 外侧旋成体 + 内侧反光面(独立面片, 反面).
    罩顶在吊杆底端 z=-ROD_LEN, 罩口朝下"""
    seg = 32
    prof_out = [  # (z, r) 外轮廓: 从罩顶到罩口
        (-ROD_LEN + 0.00, 0.030), (-ROD_LEN - 0.02, 0.075),
        (-ROD_LEN - 0.05, 0.130), (-ROD_LEN - 0.09, 0.185),
        (-ROD_LEN - 0.14, 0.215), (-ROD_LEN - 0.16, 0.225),
    ]
    shade = lathe(name, prof_out, segments=seg, axis='Z', mat=mat_out, parent=root, cap=False)
    # 内侧反光面 (略缩, 法线朝下)
    prof_in = [(z, r - 0.004) for z, r in prof_out[1:]]  # 径向内缩, 不外凸
    inner = lathe(name + '_Inner', prof_in, segments=seg, axis='Z', mat=mat_in,
                  parent=root, cap=False)
    for p in inner.data.polygons:
        p.flip()  # 法线向内下
    # 合并罩体为单对象(两材质槽)
    bpy.ops.object.select_all(action='DESELECT')
    shade.select_set(True); inner.select_set(True)
    bpy.context.view_layer.objects.active = shade
    bpy.ops.object.join()
    shade.name = name
    if broken:
        # 罩缘破口: 删除罩口附近一段扇区的面 (模拟缺口)
        me = shade.data
        doomed = []
        for p in me.polygons:
            c = p.center
            r = math.hypot(c.x, c.y)
            if r > 0.16:
                a = math.atan2(c.y, c.x)
                if -0.45 < a < 0.35:
                    doomed.append(p)
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(me)
        faces_to_del = []
        for f in bm.faces:
            c = f.calc_center_median()
            r = math.hypot(c.x, c.y)
            if r > 0.16:
                a = math.atan2(c.y, c.x)
                if -0.45 < a < 0.35:
                    faces_to_del.append(f)
        bmesh.ops.delete(bm, geom=faces_to_del, context='FACES')
        bm.to_mesh(me)
        bm.free()
        me.update()
    return shade


def build_bulb(name, mat, root, broken=False):
    """灯泡独立件: 梨形泡体; 破损态只留灯头+短残颈"""
    if not broken:
        prof = [(-ROD_LEN - 0.175, 0.0001), (-ROD_LEN - 0.180, 0.020),
                (-ROD_LEN - 0.200, 0.030), (-ROD_LEN - 0.225, 0.026),
                (-ROD_LEN - 0.245, 0.012), (-ROD_LEN - 0.250, 0.0001)]
    else:
        prof = [(-ROD_LEN - 0.175, 0.0001), (-ROD_LEN - 0.180, 0.020),
                (-ROD_LEN - 0.195, 0.018), (-ROD_LEN - 0.198, 0.008),
                (-ROD_LEN - 0.200, 0.0001)]
    return lathe(name, prof, segments=20, axis='Z', mat=mat, parent=root)


def build_state(state):
    clean_scene()
    mat_m, mat_i, mat_b = MAT_METAL(), MAT_INNER(), MAT_BULB()
    root = empty('PRP_CeilingLamp_' + state, display='CIRCLE', size=0.1)

    # 天花安装底座 + 吊杆 (原点=安装点, 向下 -Z)
    canopy = lathe('Lamp_Canopy', [(0.0, 0.0001), (-0.005, 0.045), (-0.03, 0.050),
                                   (-0.045, 0.030), (-0.050, 0.0001)],
                   segments=24, axis='Z', mat=mat_m, parent=root)
    rod = cyl('Lamp_Rod', 0.008, ROD_LEN, loc=(0, 0, -ROD_LEN / 2), verts=16,
              mat=mat_m, parent=root)
    # 合并底座+杆为 Lamp_Rod 单件
    bpy.ops.object.select_all(action='DESELECT')
    canopy.select_set(True); rod.select_set(True)
    bpy.context.view_layer.objects.active = rod
    bpy.ops.object.join()
    rod.name = 'Lamp_Rod'

    build_shade('Lamp_Shade', mat_m, mat_i, root, broken=(state == 'Broken'))
    build_bulb('Lamp_Bulb', mat_b, root, broken=(state == 'Broken'))

    if state == 'Broken':
        import random
        rnd = random.Random(11)
        for k in range(5):  # 玻璃碎件 ≤6, 落在灯正下方地面(z=-2.8 相对房内, 这里放在罩口下方)
            th = 0.002
            n = 5
            pts = []
            for i in range(n):
                a = 2 * math.pi * i / n + rnd.uniform(-0.3, 0.3)
                r = rnd.uniform(0.008, 0.02)
                pts.append((r * math.cos(a), r * math.sin(a)))
            verts = [(x, y, -th) for x, y in pts] + [(x, y, th) for x, y in pts]
            faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
            for i in range(n):
                i2 = (i + 1) % n
                faces.append((i, i2, n + i2, n + i))
            me = bpy.data.meshes.new('Lamp_Shard_mesh')
            me.from_pydata(verts, [], faces)
            me.update()
            o = bpy.data.objects.new('Lamp_Shard_%02d' % k, me)
            bpy.context.scene.collection.objects.link(o)
            o.parent = root
            o.location = (rnd.uniform(-0.2, 0.2), rnd.uniform(-0.2, 0.2), -2.795)
            assign(o, mat_b)
    return root


for state in ('Intact', 'Broken'):
    root = build_state(state)
    aid = 'PRP_CeilingLamp_' + state
    save_blend(os.path.join(ROOT, 'source/blender/props/%s_v001.blend' % aid))
    export_fbx(os.path.join(ROOT, 'exports/props/%s.fbx' % aid))
    expect = {'x': SHADE_D, 'y': SHADE_D} if state == 'Intact' else {}
    report(aid, root, 4000, expect)
    preview(os.path.join(ROOT, 'previews/props/%s.png' % aid),
            target=(0, 0, -0.25), dist=1.4, angle=25, height=0.55)
