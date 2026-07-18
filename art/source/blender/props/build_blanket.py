# PRP_Blanket 四形态 — asset_spec_3d.md §6
# 同材质同 UV 网格切换: Crumpled / Covered / FlatTrap / Draped
# 原点=底面中心; 材质 MAT_Fabric_Bedding (暖白偏灰, 黑暗中仍读得出轮廓)
# 预算 M ≤4000/形态; 褶皱走大形, 底面开放(不可见删面 §1.4)
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
MAT = lambda: make_mat('MAT_Fabric_Bedding', (0.75, 0.72, 0.68), rough=0.9)


def gauss(x, y, cx, cy, sx, sy):
    return math.exp(-(((x - cx) / sx) ** 2 + ((y - cy) / sy) ** 2))


def build_crumpled(mat, root):
    """床上团状 1.2×0.85×0.25: 多峰起伏 + 不对称挤压"""
    def z(x, y):
        h = 0.18 * gauss(x, y, -0.15, 0.05, 0.38, 0.30)
        h += 0.14 * gauss(x, y, 0.22, -0.12, 0.28, 0.24)
        h += 0.08 * gauss(x, y, 0.05, 0.25, 0.22, 0.18)
        h += 0.025 * math.sin(x * 14 + y * 6) * math.cos(y * 11)  # 大褶皱
        h += 0.012 * math.sin(x * 26 - y * 17)                     # 次褶皱
        # 边缘收拢到地面
        edge = max(abs(x) / 0.60, abs(y) / 0.425)
        h *= max(0.0, 1.0 - max(0.0, edge - 0.75) / 0.25) ** 0.7
        return max(h, 0.005)
    return grid_sheet('PRP_Blanket_Crumpled', 44, 34, 1.2, 0.85, z,
                      mat=mat, parent=root, skirt_z=0.0)


def build_covered(mat, root):
    """人形隆起 1.5×0.9×0.45: 躯干大丘 + 头/肩小丘, 一眼读出"下面藏了人" """
    def z(x, y):
        # 人体沿 X 轴: 头在 -X, 脚在 +X
        h = 0.42 * gauss(x, y, 0.05, 0.0, 0.42, 0.26)     # 躯干
        h += 0.16 * gauss(x, y, -0.52, 0.0, 0.16, 0.14)   # 头肩丘
        h += 0.12 * gauss(x, y, 0.48, 0.0, 0.22, 0.15)    # 腿部
        h += 0.02 * math.sin(x * 9) * math.exp(-abs(y) * 4)  # 沿身体纵向折痕
        edge = max(abs(x) / 0.75, abs(y) / 0.45)
        h *= max(0.0, 1.0 - max(0.0, edge - 0.78) / 0.22) ** 0.8
        return max(h, 0.004)
    return grid_sheet('PRP_Blanket_Covered', 52, 34, 1.5, 0.9, z,
                      mat=mat, parent=root, skirt_z=0.0)


def build_flattrap(mat, root):
    """门口平铺 1.4×1.0×0.06: 主体贴地, 边缘自然褶皱, 一角卷翘(暗示能抓)"""
    def z(x, y):
        h = 0.02
        h += 0.014 * math.exp(-((abs(x) - 0.55) / 0.12) ** 2)      # 两侧边褶
        h += 0.010 * math.sin(y * 16 + x * 4) * math.exp(-(abs(x) / 0.5) ** 2)
        # 右下角卷翘 (Rug_GrabCorner 同款语言), 峰值与规格 0.06 对齐
        d = math.hypot(x - 0.62, y + 0.42)
        h += 0.038 * math.exp(-(d / 0.14) ** 2)
        return h
    return grid_sheet('PRP_Blanket_FlatTrap', 48, 36, 1.4, 1.0, z,
                      mat=mat, parent=root, skirt_z=0.0)


def build_draped(mat, root):
    """披挂盖灯: 罩在台灯罩上的锥形垂布 (罩径 0.25, 罩高 0.16 → 垂布直径约 0.34)"""
    nr, ns = 12, 40   # 径向 × 周向
    verts, faces = [], []
    z_top = 0.47      # 比灯罩顶(0.45)略高
    z_bot = 0.28      # 垂到灯罩下缘(0.29)略低
    for j in range(nr + 1):
        t = j / nr
        r = 0.015 + t * 0.135         # 顶 0.03 → 底约 0.30
        zbase = z_top - t * (z_top - z_bot)
        for i in range(ns):
            a = 2 * math.pi * i / ns
            # 垂布波浪: 越往下摆幅越大
            wave = (0.012 + 0.020 * t) * math.sin(a * 7 + t * 3)
            rr = r + wave
            verts.append((rr * math.cos(a), rr * math.sin(a), zbase + 0.006 * math.sin(a * 7)))
    for j in range(nr):
        for i in range(ns):
            i2 = (i + 1) % ns
            a = j * ns + i
            b = j * ns + i2
            faces.append((a, b, b + ns, a + ns))
    # 顶面封口
    top_c = len(verts)
    verts.append((0, 0, z_top + 0.002))
    for i in range(ns):
        i2 = (i + 1) % ns
        faces.append((top_c, i2, i))
    me = bpy.data.meshes.new('PRP_Blanket_Draped_mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new('PRP_Blanket_Draped', me)
    bpy.context.scene.collection.objects.link(o)
    o.parent = root
    assign(o, mat)
    for p in me.polygons: p.use_smooth = True
    return o


builders = {
    'Crumpled': build_crumpled,
    'Covered': build_covered,
    'FlatTrap': build_flattrap,
    'Draped': build_draped,
}
expects = {
    'Crumpled': {'x': 1.2, 'y': 0.85, 'z': 0.25},
    'Covered': {'x': 1.5, 'y': 0.9, 'z': 0.45},
    'FlatTrap': {'x': 1.4, 'y': 1.0, 'z': 0.07},   # 规格约0.06+卷翘角, ≥5cm 视觉厚度达标
    'Draped': {'x': 0.35, 'y': 0.35, 'z': 0.20},
}

for form, fn in builders.items():
    clean_scene()
    mat = MAT()
    holder = empty('ROOT_TMP', display='CUBE', size=0.1)
    mesh = fn(mat, holder)
    # 单网格资产: 网格本体即根对象, 解除临时父级避免重名
    mesh.parent = None
    bpy.data.objects.remove(holder, do_unlink=True)
    root = mesh
    aid = 'PRP_Blanket_' + form
    save_blend(os.path.join(ROOT, 'source/blender/props/%s_v001.blend' % aid))
    export_fbx(os.path.join(ROOT, 'exports/props/%s.fbx' % aid))
    report(aid, root, 4000, expects[form])
    tgt_z = expects[form]['z'] * 0.5
    preview(os.path.join(ROOT, 'previews/props/%s.png' % aid),
            target=(0, 0, tgt_z), dist=2.0, angle=35, height=0.7)
