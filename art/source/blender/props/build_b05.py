# B05 小道具 — asset_spec_3d.md §6
# PRP_Remote / PRP_Books_Set(A-E+Open) / PRP_Vase(+Roses+Broken) /
# PRP_WallClock(三针独立+Broken) / PRP_Mirror(+Broken) / PRP_DrawerClutter
# 正面朝 -Y, Z 向上; 小道具共用 MAT_Props_Small_Atlas 槽
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
PRP_DIR = os.path.join(ROOT, 'source/blender/props')
ATLAS = lambda: make_mat('MAT_Props_Small_Atlas', (0.42, 0.36, 0.30), rough=0.7)
GLASS = lambda: make_mat('MAT_Glass_Set', (0.85, 0.88, 0.90), metallic=0.0, rough=0.15)
PORCELAIN = lambda: make_mat('MAT_Glass_Set_Porcelain', (0.92, 0.90, 0.86), rough=0.25)
WALNUT = lambda: make_mat('MAT_Wood_Walnut', (0.23, 0.15, 0.10), rough=0.5)
ROSE_RED = lambda: make_mat('MAT_Props_Small_Atlas_Rose', (0.55, 0.05, 0.06), rough=0.6)
MIRROR = lambda: make_mat('MAT_Mirror_Surface', (0.75, 0.80, 0.85), metallic=0.9, rough=0.05)


# ---------------------------------------------------------------- 遥控器
def build_remote():
    clean_scene()
    mat = ATLAS()
    root = empty('PRP_Remote', display='CUBE', size=0.05)
    # 0.18×0.06×0.03, 原点在几何中心; 正面(按键面)朝上, 顶端朝 -Y(指向电视)
    body = box('Remote_Body', 0.06, 0.18, 0.024, loc=(0, 0, 0), bev=0.008, mat=mat, parent=root)
    # 大按键: 2 列 × 4 行 + 顶部电源圆键 (凸起 4mm); 按键不倒角控面数
    for r in range(4):
        for c in (-1, 1):
            box('key', 0.018, 0.016, 0.004, loc=(c * 0.014, -0.045 + r * 0.028, 0.014),
                bev=0.0, mat=mat, parent=root)
    cyl('key_power', 0.011, 0.005, loc=(0, -0.072, 0.0145), verts=12, mat=mat, parent=root)
    # 红外窗 (前端)
    box('ir', 0.03, 0.004, 0.01, loc=(0, -0.089, 0.002), bev=0.0, mat=mat, parent=root)
    save_blend(os.path.join(PRP_DIR, 'PRP_Remote_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Remote.fbx'))
    report('PRP_Remote', root, 1500, {'x': 0.06, 'y': 0.18, 'z': 0.03})
    preview(os.path.join(ROOT, 'previews/props/PRP_Remote.png'),
            target=(0, 0, 0), dist=0.45, angle=30, height=0.8)


# ---------------------------------------------------------------- 书本组
def _book(name, w, t, h, mat, root, open_state=False):
    """t=厚度(立放时 y 向深); 原点=书脊底面中心; 封面朝 -Y 侧为书脊"""
    if not open_state:
        parts = [
            box('cover', w, t, h, loc=(0, 0, h / 2), bev=0.002, mat=mat),
            # 书脊微凸 + 页块米色差 (同槽, 靠几何分面)
            box('pages', w - 0.008, t - 0.006, h - 0.008, loc=(0.004, 0, h / 2), bev=0.001, mat=mat),
        ]
        b = join_into(parts, name, root)
        return b
    # 摊开态: 左右两页块微张角(旋转先应用再合并), 中间书脊条; 原点=页块底面中心
    parts = []
    for side in (-1, 1):
        p = box('page', w, t, 0.004, loc=(side * w / 2, 0, 0.012), bev=0.001, mat=mat)
        p.rotation_euler = (0, side * math.radians(6), 0)
        bpy.context.view_layer.objects.active = p
        p.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        p.select_set(False)
        parts.append(p)
    parts.append(box('spine', 0.01, t, 0.006, loc=(0, 0, 0.004), bev=0.001, mat=mat))
    return join_into(parts, name, root)

def build_books():
    clean_scene()
    mat = ATLAS()
    root = empty('PRP_Books_Set', display='CUBE', size=0.05)
    # 5 变体: (宽, 厚, 高) 立放, 沿 X 排开仅便于预览, 各自原点=书脊底面中心
    variants = [(0.14, 0.022, 0.20), (0.13, 0.030, 0.21), (0.15, 0.038, 0.19),
                (0.14, 0.045, 0.22), (0.13, 0.050, 0.20)]
    x = 0.0
    for i, (w, t, h) in enumerate(variants):
        b = _book('Book_%s' % 'ABCDE'[i], w, t, h, mat, root)
        # join_into 原点在世界原点; 各自搬到排布位再烘回原点是复杂化 —— 书各自独立原点:
        # 改为建模时即在排布位生成, 然后原点搬回书脊底面中心
        P = Vector((x, 0, 0))
        b.data.transform(Matrix.Translation(-P))
        b.location = P
        x += 0.06
    op = _book('Book_Open', 0.14, 0.20, 0, mat, root, open_state=True)
    P = Vector((x + 0.08, 0, 0))
    op.data.transform(Matrix.Translation(-P))
    op.location = P
    save_blend(os.path.join(PRP_DIR, 'PRP_Books_Set_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Books_Set.fbx'))
    report('PRP_Books_Set', root, 7500, {})
    preview(os.path.join(ROOT, 'previews/props/PRP_Books_Set.png'),
            target=(0.2, 0, 0.1), dist=0.7, angle=25, height=0.6)


# ---------------------------------------------------------------- 花瓶 + 玫瑰 + 碎件
def build_vase():
    clean_scene()
    mat_p, mat_r = PORCELAIN(), ROSE_RED()
    root = empty('PRP_Vase', display='CUBE', size=0.05)
    # 白瓷瓶 高0.3: 旋成轮廓 (t=z, r)
    prof = [(0.0001, 0.045), (0.01, 0.055), (0.06, 0.075), (0.14, 0.085),
            (0.20, 0.070), (0.24, 0.045), (0.27, 0.038), (0.30, 0.045)]
    body = lathe('Vase_Body', prof, segments=24, axis='Z', mat=mat_p, parent=root)
    # 玫瑰 3 支: 茎(细柱)+花头(压扁球)+叶; 几何从 z=0 起, 对象原点搬到瓶口 z=0.26
    roses = []
    for i, (a, tilt, L) in enumerate([(0, 4, 0.17), (2.1, 10, 0.15), (4.2, 8, 0.13)]):
        parts = []
        stem = cyl('stem', 0.0035, L, loc=(0, 0, L / 2), verts=8, mat=mat_r)
        parts.append(stem)
        head = sphere('head', 0.028, loc=(0, 0, L + 0.015), scale=(1, 1, 0.75), seg=12, rings=8, mat=mat_r)
        parts.append(head)
        bud = sphere('bud', 0.016, loc=(0, 0, L + 0.045), scale=(1, 1, 1.1), seg=10, rings=8, mat=mat_r)
        parts.append(bud)
        r = join_into(parts, 'rose_tmp')
        # 摆位: 绕瓶口散开 (原点在茎底, 再整体抬到瓶口)
        r.rotation_euler = (math.radians(tilt), 0, a)
        r.location = (0.012 * math.cos(a), 0.012 * math.sin(a), 0.26)
        roses.append(r)
    rose_grp = join_into(roses, 'Vase_Roses', root)
    save_blend(os.path.join(PRP_DIR, 'PRP_Vase_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Vase.fbx'))
    report('PRP_Vase', root, 1500, {'z': 0.5})
    preview(os.path.join(ROOT, 'previews/props/PRP_Vase.png'),
            target=(0, 0, 0.25), dist=0.9, angle=25, height=0.5)

def build_vase_broken():
    clean_scene()
    mat_p, mat_r = PORCELAIN(), ROSE_RED()
    root = empty('PRP_Vase_Broken', display='CUBE', size=0.05)
    # 8 块闭合凸包碎片, 铺出直径约 0.8m 碎片区; 原点=整件落地中心(与完好态同原点)
    import random
    random.seed(11)
    for i in range(8):
        a = i / 8 * 2 * math.pi + random.uniform(-0.2, 0.2)
        d = random.uniform(0.08, 0.38)
        sx = random.uniform(0.05, 0.12)
        sy = random.uniform(0.03, 0.08)
        sz = random.uniform(0.008, 0.03)
        s = box('Vase_Shard_%02d' % (i + 1), sx, sy, sz,
                loc=(d * math.cos(a), d * math.sin(a), sz / 2), bev=min(sx, sy, sz) * 0.25,
                mat=mat_p, parent=root)
        s.rotation_euler = (0, 0, random.uniform(0, math.pi))
    # 散落玫瑰 (瓶碎后花落地): 整体平躺; 圆柱旋转先应用再合并, 避免残留变换
    parts = []
    stem = cyl('stem', 0.0035, 0.20, loc=(0, 0, 0.004), rot=(0, math.pi / 2, 0), verts=8, mat=mat_r)
    bpy.context.view_layer.objects.active = stem
    stem.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    stem.select_set(False)
    parts.append(stem)
    parts.append(sphere('head', 0.028, loc=(0.12, 0, 0.02), scale=(1, 1, 0.75), seg=12, rings=8, mat=mat_r))
    r = join_into(parts, 'Vase_Roses_Fallen', root)
    r.location = (0.15, -0.2, 0)
    r.rotation_euler = (0, 0, 0.7)
    save_blend(os.path.join(PRP_DIR, 'PRP_Vase_Broken_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Vase_Broken.fbx'))
    report('PRP_Vase_Broken', root, 2000, {})
    preview(os.path.join(ROOT, 'previews/props/PRP_Vase_Broken.png'),
            target=(0, 0, 0.02), dist=1.2, angle=30, height=0.9)


# ---------------------------------------------------------------- 挂钟 (三针独立 + 破碎态)
def _clock_pieces(root, mat_a, mat_g, broken=False):
    # 钟体: 圆盘+框环+玻璃罩, 盘面朝 -Y; 原点=盘面轴心
    body_parts = [
        cyl('case', 0.16, 0.05, rot=(math.pi / 2, 0, 0), verts=32, mat=mat_a),
        cyl('dial', 0.145, 0.052, rot=(math.pi / 2, 0, 0), verts=32, mat=mat_a),
    ]
    # 12 个时刻凸点 (不画指针; 不生成可读数字 → 用圆点)
    for k in range(12):
        a = k / 12 * 2 * math.pi
        body_parts.append(cyl('tick', 0.006 if k % 3 else 0.009, 0.004,
                              loc=(0.128 * math.sin(a), -0.027, 0.128 * math.cos(a)),
                              rot=(math.pi / 2, 0, 0), verts=10, mat=mat_a))
    body = join_into(body_parts, 'Clock_Body', root)
    glass = cyl('Clock_Glass', 0.14, 0.003, loc=(0, -0.0285, 0),
                rot=(math.pi / 2, 0, 0), verts=32, mat=mat_g, parent=root)
    # 三针: 原点均在轴心, 初始指 12 点(+Z); 沿 -Y 层叠防穿插
    def hand(nm, length, w, y):
        h = box(nm, w, 0.0025, length, loc=(0, y, 0), bev=0.001, mat=mat_a, parent=root)
        # 针尖在 +Z, 尾端短配重; 原点保持轴心: 网格上移 length/2 - 尾段
        h.data.transform(Matrix.Translation(Vector((0, 0, length / 2 - 0.015))))
        return h
    hh = hand('Clock_Hand_H', 0.075, 0.010, -0.030)
    mh = hand('Clock_Hand_M', 0.110, 0.007, -0.032)
    sh = hand('Clock_Hand_S', 0.120, 0.003, -0.034)
    if broken:
        # 玻璃落地碎片 3 块 + 钟体微倾 + 秒针下垂
        for i, (dx, dz) in enumerate([(-0.06, 0.02), (0.05, 0.05), (0.0, -0.04)]):
            s = box('Clock_Shard_%02d' % (i + 1), 0.05, 0.002, 0.03,
                    loc=(dx, -0.05, dz), bev=0.002, mat=mat_g, parent=root)
            s.rotation_euler = (math.pi / 2, 0, i * 1.1)
        sh.rotation_euler = (0, 0, math.radians(150))  # 垂向 6 点
        mh.rotation_euler = (0, 0, math.radians(-20))
        glass.hide_render = True   # 罩面碎失: 碎件见 Clock_Shard
        glass.name = 'Clock_Glass_Gone'
    return body

def build_wallclock():
    clean_scene()
    mat_a, mat_g = ATLAS(), GLASS()
    root = empty('PRP_WallClock', display='CUBE', size=0.05)
    _clock_pieces(root, mat_a, mat_g, broken=False)
    save_blend(os.path.join(PRP_DIR, 'PRP_WallClock_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_WallClock.fbx'))
    report('PRP_WallClock', root, 1500, {'x': 0.32, 'z': 0.32})
    preview(os.path.join(ROOT, 'previews/props/PRP_WallClock.png'),
            target=(0, 0, 0), dist=0.8, angle=10, height=0.1)

def build_wallclock_broken():
    clean_scene()
    mat_a, mat_g = ATLAS(), GLASS()
    root = empty('PRP_WallClock_Broken', display='CUBE', size=0.05)
    _clock_pieces(root, mat_a, mat_g, broken=True)
    save_blend(os.path.join(PRP_DIR, 'PRP_WallClock_Broken_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_WallClock_Broken.fbx'))
    report('PRP_WallClock_Broken', root, 2000, {'x': 0.32, 'z': 0.32})
    preview(os.path.join(ROOT, 'previews/props/PRP_WallClock_Broken.png'),
            target=(0, 0, 0), dist=0.8, angle=10, height=0.1)


# ---------------------------------------------------------------- 半身镜 (+破碎态)
def _mirror(root, mat_w, mat_m, broken=False):
    # 0.7×1.2, 原点=框底边中心(挂墙件, 摆位时底边离地 0.5); 镜面朝 -Y
    fr = []
    fr.append(box('fr', 0.74, 0.045, 0.05, loc=(0, 0, 1.225), bev=0.008))
    fr.append(box('fr', 0.74, 0.045, 0.05, loc=(0, 0, 0.025), bev=0.008))
    fr.append(box('fr', 0.05, 0.045, 1.24, loc=(-0.345, 0, 0.625), bev=0.008))
    fr.append(box('fr', 0.05, 0.045, 1.24, loc=(0.345, 0, 0.625), bev=0.008))
    frame = join_into(fr, 'Mirror_Frame', root)
    assign(frame, mat_w)
    if not broken:
        box('Mirror_Plane', 0.64, 0.006, 1.15, loc=(0, -0.012, 0.625), bev=0.0,
            mat=mat_m, parent=root)
    else:
        # 裂纹镜面: 4 大块原位微倾 + 3 块落地碎片
        segs = [(-0.16, 0.90, 0.32, 0.5, -2), (0.16, 0.90, 0.32, 0.5, 3),
                (-0.16, 0.35, 0.32, 0.55, 2), (0.16, 0.35, 0.32, 0.55, -4)]
        for i, (cx, cz, sx, sz, tilt) in enumerate(segs):
            p = box('Mirror_Crack_%02d' % (i + 1), sx, 0.006, sz,
                    loc=(cx, -0.012, cz), bev=0.0, mat=mat_m, parent=root)
            p.rotation_euler = (0, math.radians(tilt), 0)
        import random
        random.seed(7)
        for i in range(3):
            s = box('Mirror_Shard_%02d' % (i + 1), 0.10, 0.004, 0.06,
                    loc=(-0.15 + i * 0.15, -0.10 - 0.03 * i, 0.003), bev=0.002,
                    mat=mat_m, parent=root)
            s.rotation_euler = (0, 0, random.uniform(0, math.pi))

def build_mirror():
    clean_scene()
    _mirror(empty('PRP_Mirror', display='CUBE', size=0.05), WALNUT(), MIRROR(), broken=False)
    root = bpy.data.objects['PRP_Mirror']
    save_blend(os.path.join(PRP_DIR, 'PRP_Mirror_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Mirror.fbx'))
    report('PRP_Mirror', root, 4000, {'x': 0.74, 'z': 1.25})
    preview(os.path.join(ROOT, 'previews/props/PRP_Mirror.png'),
            target=(0, 0, 0.6), dist=1.6, angle=15, height=0.2)

def build_mirror_broken():
    clean_scene()
    _mirror(empty('PRP_Mirror_Broken', display='CUBE', size=0.05), WALNUT(), MIRROR(), broken=True)
    root = bpy.data.objects['PRP_Mirror_Broken']
    save_blend(os.path.join(PRP_DIR, 'PRP_Mirror_Broken_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Mirror_Broken.fbx'))
    report('PRP_Mirror_Broken', root, 4000, {'x': 0.74, 'z': 1.25})
    preview(os.path.join(ROOT, 'previews/props/PRP_Mirror_Broken.png'),
            target=(0, 0, 0.6), dist=1.6, angle=15, height=0.2)


# ---------------------------------------------------------------- 抽屉杂物
def build_clutter():
    clean_scene()
    mat = ATLAS()
    root = empty('PRP_DrawerClutter', display='CUBE', size=0.05)
    # 胶带卷: 圆环躺放
    bpy.ops.mesh.primitive_torus_add(major_radius=0.030, minor_radius=0.018,
                                     major_segments=20, minor_segments=10,
                                     location=(0, 0, 0.018))
    tape = bpy.context.active_object
    tape.name = 'Clutter_Tape'
    assign(tape, mat)
    tape.parent = root
    # 旧报纸: 对折薄片 (字迹不可读 → 无贴图文字)
    news = box('Clutter_Newspaper', 0.20, 0.14, 0.004, loc=(0.08, 0.06, 0.002),
               bev=0.001, mat=mat, parent=root)
    news.rotation_euler = (0, 0, 0.3)
    # 空白纸张 2 张错落
    for i in range(2):
        p = box('Clutter_Paper_%d' % (i + 1), 0.21, 0.148, 0.001,
                loc=(-0.09 + i * 0.01, 0.05 + i * 0.008, 0.0005 + i * 0.001),
                bev=0.0, mat=mat, parent=root)
        p.rotation_euler = (0, 0, 0.15 * (i * 2 - 1))
    save_blend(os.path.join(PRP_DIR, 'PRP_DrawerClutter_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_DrawerClutter.fbx'))
    report('PRP_DrawerClutter', root, 1500, {})
    preview(os.path.join(ROOT, 'previews/props/PRP_DrawerClutter.png'),
            target=(0, 0, 0.01), dist=0.55, angle=30, height=0.8)


build_remote()
build_books()
build_vase()
build_vase_broken()
build_wallclock()
build_wallclock_broken()
build_mirror()
build_mirror_broken()
build_clutter()
print('B05_DONE')
