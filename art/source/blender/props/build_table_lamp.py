# PRP_TableLamp 两态 — asset_spec_3d.md §6
# 总高 0.45, 罩径 0.25; 原点=底座底面中心(拿起时原点即握点); 正面朝 -Y
# 拆件: Lamp_Base / Lamp_Switch(原点在旋轴) / Lamp_Shade / Lamp_Bulb / Lamp_Cord
# _Broken: 罩裂、泡碎 + 碎件 ≤6; 预算 M ≤4000/态
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
H_TOTAL = 0.45
SHADE_D = 0.25

MAT_METAL = lambda: make_mat('MAT_Metal_Dark', (0.04, 0.04, 0.045), metallic=0.8, rough=0.45)
MAT_FABRIC = lambda: make_mat('MAT_Fabric_Bedding', (0.75, 0.72, 0.68), rough=0.9)  # 布罩
MAT_BULB = lambda: make_mat('MAT_Lamp_Emissive', (0.9, 0.85, 0.75), rough=0.3,
                            emission=(1.0, 0.8, 0.5), estr=2.0)

STEM_TOP = 0.30   # 灯杆顶端(灯座)


def build_base(mat_m, root):
    parts = []
    # 底座: 圆盘+收腰柱 (旋成体, z 0→0.10)
    prof_base = [(0.000, 0.0001), (0.000, 0.070), (0.008, 0.072), (0.015, 0.060),
                 (0.030, 0.038), (0.050, 0.028), (0.090, 0.022), (0.100, 0.024)]
    parts.append(lathe('Lamp_BaseLower', prof_base, segments=28, axis='Z', mat=mat_m, parent=root))
    # 灯杆 z 0.10→STEM_TOP
    parts.append(cyl('Lamp_Stem', 0.010, STEM_TOP - 0.10, loc=(0, 0, (STEM_TOP + 0.10) / 2),
                     verts=16, mat=mat_m, parent=root))
    # 灯头座
    parts.append(lathe('Lamp_Holder', [(STEM_TOP, 0.024), (STEM_TOP + 0.015, 0.024),
                                       (STEM_TOP + 0.020, 0.018), (STEM_TOP + 0.025, 0.012)],
                       segments=20, axis='Z', mat=mat_m, parent=root, cap=False))
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    base = bpy.context.active_object
    base.name = 'Lamp_Base'
    base.parent = root
    return base


def build_switch(mat_m, root):
    """旋钮开关: 独立小件, 原点在旋轴 (底座侧前方)"""
    sw = cyl('Lamp_Switch', 0.012, 0.018, verts=16, mat=mat_m)
    # 旋轴沿 Y (从底座柱面水平伸出), 先旋转再定位
    sw.rotation_euler = (math.pi / 2, 0, 0)
    sw.location = (0, -0.030, 0.045)   # 正面 -Y 侧
    sw.parent = root
    return sw


def build_shade(mat_f, root, broken=False):
    """圆台布罩: 下口 0.25, 上口 0.17, 高 0.16, 顶缘在 H_TOTAL; 双面(内面同槽)"""
    z_top = H_TOTAL
    z_bot = z_top - 0.16
    prof = [(z_bot, 0.125), (z_bot + 0.004, 0.125), (z_top - 0.004, 0.085), (z_top, 0.085)]
    seg = 28
    shade = lathe('Lamp_Shade', prof, segments=seg, axis='Z', mat=mat_f, parent=root, cap=False)
    # 顶圈+底圈卷边
    if broken:
        # 罩裂: 删一段从底缘延伸到中部的裂口面片
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(shade.data)
        dels = []
        for f in bm.faces:
            c = f.calc_center_median()
            a = math.atan2(c.y, c.x)
            zf = (c.z - z_bot) / (z_top - z_bot)
            width = 0.30 * (1 - zf) + 0.05   # 裂口下宽上窄
            if -math.pi / 2 - width < a < -math.pi / 2 + width and zf < 0.75:
                dels.append(f)
        bmesh.ops.delete(bm, geom=dels, context='FACES')
        bm.to_mesh(shade.data)
        bm.free()
        shade.data.update()
    return shade


def build_bulb(mat_b, root, broken=False):
    z0 = STEM_TOP + 0.005
    if not broken:
        prof = [(z0, 0.014), (z0 + 0.01, 0.020), (z0 + 0.035, 0.028),
                (z0 + 0.06, 0.018), (z0 + 0.075, 0.0001)]
    else:
        prof = [(z0, 0.014), (z0 + 0.008, 0.018), (z0 + 0.014, 0.006), (z0 + 0.016, 0.0001)]
    return lathe('Lamp_Bulb', prof, segments=20, axis='Z', mat=mat_b, parent=root)


def build_cord(mat_m, root):
    """短截扯断电线: 从底座背面(+Y)伸出, 末端上翘断头; 拾取态可见"""
    import bmesh
    pts = [(0, 0.065, 0.015), (0.01, 0.10, 0.008), (0.03, 0.13, 0.006), (0.045, 0.15, 0.012)]
    # 用圆截面扫掠
    seg_r = 8
    verts, faces = [], []
    for i, p in enumerate(pts):
        ring = []
        for k in range(seg_r):
            a = 2 * math.pi * k / seg_r
            ring.append(len(verts))
            verts.append((p[0] + 0.004 * math.cos(a), p[1], p[2] + 0.004 * math.sin(a)))
        if i > 0:
            prev = [(i - 1) * seg_r + k for k in range(seg_r)]
            for k in range(seg_r):
                k2 = (k + 1) % seg_r
                faces.append((prev[k], prev[k2], ring[k2], ring[k]))
    faces.append(tuple(range(seg_r - 1, -1, -1)))
    faces.append(tuple(range((len(pts) - 1) * seg_r, len(pts) * seg_r)))
    me = bpy.data.meshes.new('Lamp_Cord_mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new('Lamp_Cord', me)
    bpy.context.scene.collection.objects.link(o)
    o.parent = root
    assign(o, mat_m)
    for p in me.polygons: p.use_smooth = True
    return o


def build_state(state):
    clean_scene()
    mat_m, mat_f, mat_b = MAT_METAL(), MAT_FABRIC(), MAT_BULB()
    root = empty('PRP_TableLamp_' + state, display='CIRCLE', size=0.1)
    build_base(mat_m, root)
    build_switch(mat_m, root)
    build_shade(mat_f, root, broken=(state == 'Broken'))
    build_bulb(mat_b, root, broken=(state == 'Broken'))
    build_cord(mat_m, root)
    if state == 'Broken':
        import random
        rnd = random.Random(23)
        for k in range(5):  # 玻璃碎件 ≤6
            th = 0.002
            n = 5
            pts = []
            for i in range(n):
                a = 2 * math.pi * i / n + rnd.uniform(-0.3, 0.3)
                r = rnd.uniform(0.006, 0.016)
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
            o.location = (rnd.uniform(-0.15, 0.15), rnd.uniform(-0.18, -0.05), 0.003)
            assign(o, mat_b)
    return root


for state in ('Intact', 'Broken'):
    root = build_state(state)
    aid = 'PRP_TableLamp_' + state
    save_blend(os.path.join(ROOT, 'source/blender/props/%s_v001.blend' % aid))
    export_fbx(os.path.join(ROOT, 'exports/props/%s.fbx' % aid))
    expect = {'x': SHADE_D, 'y': 0.275, 'z': H_TOTAL} if state == 'Intact' else {'z': H_TOTAL}  # y 含尾部电线
    report(aid, root, 4000, expect)
    preview(os.path.join(ROOT, 'previews/props/%s.png' % aid),
            target=(0, 0, 0.22), dist=1.2, angle=30, height=0.3)
