# PRP_CRTTV 三态 — asset_spec_3d.md §6 ★结局本体
# 整机 0.75w × 0.55d × 0.66h, 4:3 屏 0.35×0.26; 屏幕中心 z=0.50
#   (规格冲突记录: "整机约0.6高" 与 "上柜后屏心≈1.15(硬性)" 不可兼得,
#    取硬性强条, 高度 0.6→0.66 (+10%), 详见 art/.log/)
# 正面朝 -Y; 原点=落地中心; 预算 L ≤8000/态
# 拆件: TV_Shell / TV_Screen(独立曲面玻璃) / SOCKET_ScreenCenter
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
W, D, H = 0.75, 0.55, 0.66
SCR_CZ = 0.50                 # 屏幕中心高 (0.65 柜 + 0.50 = 1.15 = 相机高)
SCR_W, SCR_H = 0.35, 0.2625   # 4:3
FRONT_Y = -D / 2              # -0.275 前面
CAB_BACK = 0.145              # 前机箱后界 (深 0.42)

MAT_WOOD = lambda: make_mat('MAT_Wood_Walnut', (0.23, 0.15, 0.10), rough=0.5)
MAT_GLASS = lambda: make_mat('MAT_Glass_Set', (0.05, 0.05, 0.055), rough=0.15, metallic=0.1)
MAT_INNER = lambda: make_mat('MAT_CRT_Inner', (0.03, 0.03, 0.032), rough=0.8)


def build_screen(name, mat, crack=False):
    """微凸 CRT 曲面屏: 24×18 网格, 中心向 -Y 鼓出 0.02"""
    nx, ny = 24, 18
    verts, faces = [], []
    bulge = 0.015
    for j in range(ny + 1):
        for i in range(nx + 1):
            x = (i / nx - 0.5) * SCR_W
            z = SCR_CZ + (j / ny - 0.5) * SCR_H
            fx, fz = x / (SCR_W / 2), (z - SCR_CZ) / (SCR_H / 2)
            y = FRONT_Y - 0.004 - bulge * max(0.0, (1 - fx * fx)) * max(0.0, (1 - fz * fz))
            if crack:
                # 蛛网裂纹态: 以撞击点为中心做径向+环向折痕起伏 (同占位, 微起伏)
                ix, iz = SCR_W * 0.18, SCR_CZ - SCR_H * 0.10
                r = math.hypot(x - ix, z - iz) + 1e-5
                ang = math.atan2(z - iz, x - ix)
                ridge = 0.0018 * math.sin(ang * 9) * math.exp(-r * 9) \
                      + 0.0012 * math.sin(r * 110) * math.exp(-r * 7)
                dent = -0.004 * math.exp(-r * 14)   # 撞击点微凹
                y += ridge + dent
            verts.append((x, y, z))
    for j in range(ny):
        for i in range(nx):
            a = j * (nx + 1) + i
            faces.append((a, a + 1, a + 1 + nx + 1, a + nx + 1))
    me = bpy.data.meshes.new(name + '_mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    for p in me.polygons: p.use_smooth = True
    assign(o, mat)
    return o


def build_shell(root, mat):
    parts = []
    # 前机箱, 脚垫 0.02 高 → 箱体 z 0.02~0.66
    parts.append(box('TV_Cabinet', W, 0.42, H - 0.02,
                     loc=(0, FRONT_Y + 0.21, 0.02 + (H - 0.02) / 2), bev=0.012, mat=mat, parent=root))
    # 尾部显像管罩 (向后收窄)
    parts.append(box('TV_RearHump', 0.48, 0.13, 0.38,
                     loc=(0, CAB_BACK + 0.065, 0.32), bev=0.03, mat=mat, parent=root))
    # 屏幕前框 bezel 四边
    bt = 0.028
    parts.append(box('TV_Bezel_T', SCR_W + 2 * bt, 0.03, bt,
                     loc=(0, FRONT_Y - 0.002, SCR_CZ + SCR_H / 2 + bt / 2), bev=0.006, mat=mat, parent=root))
    parts.append(box('TV_Bezel_B', SCR_W + 2 * bt, 0.03, bt,
                     loc=(0, FRONT_Y - 0.002, SCR_CZ - SCR_H / 2 - bt / 2), bev=0.006, mat=mat, parent=root))
    parts.append(box('TV_Bezel_L', bt, 0.03, SCR_H,
                     loc=(-(SCR_W / 2 + bt / 2), FRONT_Y - 0.002, SCR_CZ), bev=0.006, mat=mat, parent=root))
    parts.append(box('TV_Bezel_R', bt, 0.03, SCR_H,
                     loc=(SCR_W / 2 + bt / 2, FRONT_Y - 0.002, SCR_CZ), bev=0.006, mat=mat, parent=root))
    # 右侧旋钮面板: 2 旋钮 + 2 按键
    px = SCR_W / 2 + bt + 0.055
    parts.append(cyl('TV_Knob_1', 0.018, 0.02, loc=(px, FRONT_Y - 0.006, 0.58),
                     rot=(math.pi / 2, 0, 0), verts=20, mat=mat, parent=root))
    parts.append(cyl('TV_Knob_2', 0.018, 0.02, loc=(px, FRONT_Y - 0.006, 0.52),
                     rot=(math.pi / 2, 0, 0), verts=20, mat=mat, parent=root))
    parts.append(box('TV_Btn_1', 0.045, 0.012, 0.012, loc=(px, FRONT_Y - 0.006, 0.455), bev=0.003, mat=mat, parent=root))
    parts.append(box('TV_Btn_2', 0.045, 0.012, 0.012, loc=(px, FRONT_Y - 0.006, 0.425), bev=0.003, mat=mat, parent=root))
    # 下部喇叭格栅 (屏幕下方整排)
    for k in range(9):
        parts.append(box('TV_Speaker_%02d' % k, 0.55, 0.012, 0.008,
                         loc=(-0.02, FRONT_Y - 0.005, 0.24 - k * 0.02), bev=0.002, mat=mat, parent=root))
    # 脚垫 ×4
    for fx in (-1, 1):
        for fy in (-1, 1):
            parts.append(box('TV_Foot', 0.06, 0.06, 0.02,
                             loc=(fx * (W / 2 - 0.06), FRONT_Y + 0.21 + fy * 0.16, 0.01),
                             bev=0.005, mat=mat, parent=root))
    return parts


def join_parts(objs, name, root):
    """合并壳体件为单网格 TV_Shell (材质同槽)"""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    # join 后顶点位于活动件局部空间; 把位置偏移烘进网格, 原点归到资产根(落地中心)
    o.data.transform(Matrix.Translation(o.location.copy()))
    o.location = (0, 0, 0)
    o.name = name
    o.parent = root
    return o


def shard(name, size, mat, root, seed):
    """闭合凸包玻璃碎片, 平放于地面 (规格 §1.7: 直接作 Convex MeshCollider)"""
    import random
    rnd = random.Random(seed)
    pts = []
    n = 6
    for i in range(n):
        a = 2 * math.pi * i / n + rnd.uniform(-0.3, 0.3)
        r = size * rnd.uniform(0.6, 1.0)
        pts.append((r * math.cos(a), r * math.sin(a)))
    th = 0.002
    verts = [(x, y, -th) for x, y in pts] + [(x, y, th) for x, y in pts]
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
    for i in range(n):
        i2 = (i + 1) % n
        faces.append((i, i2, n + i2, n + i))
    me = bpy.data.meshes.new(name + '_mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.parent = root
    assign(o, mat)
    return o


def build_state(state):
    clean_scene()
    mat_wood, mat_glass, mat_inner = MAT_WOOD(), MAT_GLASS(), MAT_INNER()
    root = empty('PRP_CRTTV_' + state, display='CUBE', size=0.1)

    if state in ('Intact', 'Cracked'):
        parts = build_shell(root, mat_wood)
        join_parts(parts, 'TV_Shell', root)
        scr = build_screen('TV_Screen', mat_glass, crack=(state == 'Cracked'))
        scr.parent = root
    else:  # Destroyed: 壳完好轮廓 + 屏幕炸开留锯齿边框 + 内部框架 + 碎件
        parts = build_shell(root, mat_wood)
        join_parts(parts, 'TV_Shell', root)
        nx, ny = 24, 18
        verts, faces = [], []
        hole_c = (SCR_W * 0.10, SCR_CZ - SCR_H * 0.05)
        hole_r = 0.075
        for j in range(ny + 1):
            for i in range(nx + 1):
                x = (i / nx - 0.5) * SCR_W
                z = SCR_CZ + (j / ny - 0.5) * SCR_H
                d = math.hypot(x - hole_c[0], z - hole_c[1])
                if d < hole_r:
                    # 洞口边缘锯齿化: 向外推到洞界并抖动
                    f = max(d / hole_r, 0.05)
                    jag = 1.0 + 0.10 * math.sin(math.atan2(z - hole_c[1], x - hole_c[0]) * 13)
                    x = hole_c[0] + (x - hole_c[0]) / f * jag
                    z = hole_c[1] + (z - hole_c[1]) / f * jag
                fx, fz = x / (SCR_W / 2), (z - SCR_CZ) / (SCR_H / 2)
                y = FRONT_Y - 0.004 - 0.015 * max(0.0, (1 - fx * fx)) * max(0.0, (1 - fz * fz))
                verts.append((x, y, z))
        for j in range(ny):
            for i in range(nx):
                cx = ((i + 0.5) / nx - 0.5) * SCR_W
                cz = SCR_CZ + ((j + 0.5) / ny - 0.5) * SCR_H
                if math.hypot(cx - hole_c[0], cz - hole_c[1]) < hole_r * 0.95:
                    continue  # 炸开的洞
                a = j * (nx + 1) + i
                faces.append((a, a + 1, a + 1 + nx + 1, a + nx + 1))
        me = bpy.data.meshes.new('TV_Screen_mesh')
        me.from_pydata(verts, [], faces)
        me.update()
        scr = bpy.data.objects.new('TV_Screen', me)
        bpy.context.scene.collection.objects.link(scr)
        scr.parent = root
        assign(scr, mat_glass)
        # 内部框架与暗腔 (从洞口可见)
        box('TV_InnerCavity', SCR_W - 0.05, 0.05, SCR_H - 0.05,
            loc=(0, FRONT_Y + 0.06, SCR_CZ), bev=0.005, mat=mat_inner, parent=root)
        box('TV_InnerRib_H', SCR_W - 0.06, 0.03, 0.03,
            loc=(0, FRONT_Y + 0.035, SCR_CZ), bev=0.004, mat=mat_inner, parent=root)
        box('TV_InnerRib_V', 0.03, 0.03, SCR_H - 0.06,
            loc=(hole_c[0], FRONT_Y + 0.035, hole_c[1]), bev=0.004, mat=mat_inner, parent=root)
        # 散落玻璃碎件 ×7 (落在电视前下方地面)
        import random
        rnd = random.Random(7)
        for k in range(7):
            sh = shard('TV_GlassShard_%02d' % k, rnd.uniform(0.02, 0.045),
                       mat_glass, root, seed=k * 13 + 1)
            sh.location = (rnd.uniform(-0.30, 0.30), rnd.uniform(-0.55, -0.35), 0.003)
            sh.rotation_euler = (0, 0, rnd.uniform(0, math.pi))

    # 屏幕中心 SOCKET (= 相机位, 三态同位置)
    empty('SOCKET_ScreenCenter', loc=(0, FRONT_Y - 0.023, SCR_CZ), parent=root,
          display='SPHERE', size=0.02)
    return root


for state in ('Intact', 'Cracked', 'Destroyed'):
    root = build_state(state)
    aid = 'PRP_CRTTV_' + state
    save_blend(os.path.join(ROOT, 'source/blender/props/%s_v001.blend' % aid))
    export_fbx(os.path.join(ROOT, 'exports/props/%s.fbx' % aid))
    expect = {'x': 0.75, 'z': 0.66} if state == 'Destroyed' else {'x': 0.75, 'y': 0.574, 'z': 0.66}
    report(aid, root, 8000, expect)
    preview(os.path.join(ROOT, 'previews/props/%s.png' % aid),
            target=(0, 0, 0.33), dist=1.7, angle=30, height=0.35)
