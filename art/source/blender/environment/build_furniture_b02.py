# B02 核心构图家具 — asset_spec_3d.md §5
# FUR_Armchair / FUR_Bed / FUR_WoodChair(+_Broken) / FUR_Bookcase / FUR_TVCabinet
# 全部正面朝 -Y(床: 床头板靠东+X); 原点=落地中心; 预算见规格
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'

MAT_WALNUT = lambda: make_mat('MAT_Wood_Walnut', (0.23, 0.15, 0.10), rough=0.5)
MAT_WORN = lambda: make_mat('MAT_Wood_Worn', (0.62, 0.46, 0.30), rough=0.6)
MAT_LEATHER = lambda: make_mat('MAT_Leather_Burgundy', (0.30, 0.09, 0.11), rough=0.55)
MAT_BEDDING = lambda: make_mat('MAT_Fabric_Bedding', (0.75, 0.72, 0.68), rough=0.9)


# ---------------------------------------------------------------- 扶手椅
def build_armchair():
    clean_scene()
    mat = MAT_LEATHER()
    root = empty('FUR_Armchair', display='CUBE', size=0.1)
    parts = []
    # 尺寸 0.95w × 0.95d × 1.15h; 座面高 0.42, 背高到 1.15 (遮蹲姿硬性)
    parts.append(box('base', 0.80, 0.76, 0.30, loc=(0, 0.03, 0.19), bev=0.05))        # 座台
    parts.append(box('back', 0.80, 0.26, 0.79, loc=(0, 0.35, 0.755), bev=0.07))       # 高背
    parts.append(box('backtop', 0.80, 0.26, 0.18, loc=(0, 0.34, 1.09), bev=0.08))     # 背顶卷
    for sx in (-1, 1):                                                                # 扶手
        parts.append(box('arm', 0.16, 0.80, 0.40, loc=(sx * 0.395, 0.02, 0.55), bev=0.06))
        parts.append(sphere('armfront', 0.085, loc=(sx * 0.395, -0.37, 0.50), scale=(0.95, 1.0, 1.25)))
    for fx in (-1, 1):                                                                # 短脚
        for fy in (-1, 1):
            parts.append(cyl('foot', 0.035, 0.08, loc=(fx * 0.33, fy * 0.28 + 0.02, 0.04), verts=12))
    body = join_into(parts, 'Armchair_Body', root)
    assign(body, mat)
    # 坐垫独立 (可被掀), 原点在垫底中心
    cushion = box('Armchair_Cushion', 0.62, 0.56, 0.16, bev=0.06, mat=mat)
    cushion.data.transform(Matrix.Translation(Vector((0, 0, 0.08))))   # 顶点: 底面齐局部 0
    cushion.location = (0, -0.03, 0.34)                                # 落在座台上
    cushion.parent = root
    save_blend(os.path.join(ROOT, 'source/blender/environment/FUR_Armchair_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/environment/FUR_Armchair.fbx'))
    report('FUR_Armchair', root, 8000, {'x': 0.95, 'y': 0.95, 'z': 1.15})
    preview(os.path.join(ROOT, 'previews/environment/FUR_Armchair.png'),
            target=(0, 0, 0.55), dist=2.2, angle=35, height=0.4)


# ---------------------------------------------------------------- 单人床
def build_bed():
    clean_scene()
    mat_w, mat_f = MAT_WALNUT(), MAT_BEDDING()
    root = empty('FUR_Bed', display='CUBE', size=0.1)
    # 2.0 长(X, 头靠东+X) × 1.0 宽(Y); 床底净空 0.4; 床面高约 0.6
    parts = []
    for fx in (-1, 1):
        for fy in (-1, 1):
            parts.append(box('leg', 0.07, 0.07, 0.42, loc=(fx * 0.93, fy * 0.435, 0.21), bev=0.008))
    # 侧帮 (底沿 0.40 → 保床底净空; 无横撑挡拖拽)
    parts.append(box('rail', 1.94, 0.05, 0.14, loc=(0, -0.465, 0.47), bev=0.008))
    parts.append(box('rail', 1.94, 0.05, 0.14, loc=(0, 0.465, 0.47), bev=0.008))
    parts.append(box('rail', 0.05, 0.88, 0.14, loc=(-0.945, 0, 0.47), bev=0.008))     # 床尾
    # 床板条 (在侧帮之间, 不挡床底)
    for k in range(8):
        parts.append(box('slat', 0.10, 0.86, 0.03, loc=(-0.82 + k * 0.22, 0, 0.435), bev=0.004))
    # 床头板 (东端, 高 1.0)
    parts.append(box('hboard', 0.06, 1.0, 0.62, loc=(0.97, 0, 0.71), bev=0.015))
    parts.append(box('htop', 0.08, 1.02, 0.08, loc=(0.97, 0, 1.0), bev=0.02))
    frame = join_into(parts, 'Bed_Frame', root)
    assign(frame, mat_w)
    # 床垫 (独立): 面高约 0.44+0.16=0.60
    matt = box('Bed_Mattress', 1.86, 0.88, 0.16, bev=0.03, mat=mat_f)
    matt.data.transform(Matrix.Translation(Vector((0, 0, 0.08))))
    matt.location = (-0.02, 0, 0.46)
    matt.parent = root
    # 枕头 (独立, 靠东)
    pil = box('Bed_Pillow', 0.42, 0.62, 0.13, bev=0.05, mat=mat_f)
    pil.data.transform(Matrix.Translation(Vector((0, 0, 0.065))))
    pil.location = (0.62, 0, 0.62)
    pil.parent = root
    save_blend(os.path.join(ROOT, 'source/blender/environment/FUR_Bed_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/environment/FUR_Bed.fbx'))
    report('FUR_Bed', root, 8000, {'x': 2.0, 'y': 1.0, 'z': 1.04})
    preview(os.path.join(ROOT, 'previews/environment/FUR_Bed.png'),
            target=(0, 0, 0.4), dist=3.0, angle=30, height=0.45)


# ---------------------------------------------------------------- 直背木椅 (+散架)
def chair_parts(root, mat):
    """四构件: Legs_Front(前腿对+前撑) / Legs_Back(后腿对延为背柱+侧撑) /
       Seat(座板+望板) / Back(背板+上横杆)"""
    A = []  # 前腿对: 两前腿 + 前横撑
    for sx in (-1, 1):
        A.append(box('fl', 0.04, 0.04, 0.45, loc=(sx * 0.185, -0.21, 0.225), bev=0.005))
    A.append(cyl('fstr', 0.015, 0.33, loc=(0, -0.21, 0.12), rot=(0, math.pi / 2, 0), verts=10))
    B = []  # 后腿对延为背柱(到 0.95) + 侧撑
    for sx in (-1, 1):
        B.append(box('bl', 0.04, 0.04, 0.95, loc=(sx * 0.185, 0.21, 0.475), bev=0.005))
        B.append(cyl('sstr', 0.015, 0.38, loc=(sx * 0.185, 0, 0.12), rot=(math.pi / 2, 0, 0), verts=10))
    C = []  # 座板 0.45×0.5 + 望板
    C.append(box('seat', 0.45, 0.50, 0.035, loc=(0, 0, 0.4325), bev=0.008))
    C.append(box('apron', 0.37, 0.04, 0.06, loc=(0, -0.20, 0.38), bev=0.005))
    C.append(box('apron', 0.37, 0.04, 0.06, loc=(0, 0.20, 0.38), bev=0.005))
    D = []  # 背板 + 上横杆(圆, ≤0.05, 与背板留指缝)
    D.append(box('slat', 0.34, 0.025, 0.12, loc=(0, 0.21, 0.72), bev=0.008))
    D.append(cyl('toprail', 0.0225, 0.38, loc=(0, 0.195, 0.93), rot=(0, math.pi / 2, 0), verts=12))
    names = ('Chair_Legs_Front', 'Chair_Legs_Back', 'Chair_Seat', 'Chair_Back')
    out = []
    for nm, grp in zip(names, (A, B, C, D)):
        o = join_into(grp, nm, root)
        assign(o, mat)
        out.append(o)
    return out


def build_woodchair():
    for state in ('', '_Broken'):
        clean_scene()
        mat = MAT_WORN()
        root = empty('FUR_WoodChair' + state, display='CUBE', size=0.1)
        parts = chair_parts(root, mat)
        if state == '':
            # 完好态: 合并为单件 FUR_WoodChair, 网格即根对象(同 PRP_BaseballBat)
            body = join_into(parts, 'FUR_WoodChair', root)
            body.parent = None
            bpy.data.objects.remove(root, do_unlink=True)
            body.name = 'FUR_WoodChair'   # 根空物体删除后收回正式名
            root = body
        else:
            # 散架态: 四件同占位原地微散(不旋转: 各件原点在世界原点, 旋转会穿地)
            import random
            rnd = random.Random(5)
            for o in parts:
                o.location = (rnd.uniform(-0.02, 0.02), rnd.uniform(-0.02, 0.02), 0.0)
        aid = 'FUR_WoodChair' + state
        save_blend(os.path.join(ROOT, 'source/blender/environment/%s_v001.blend' % aid))
        export_fbx(os.path.join(ROOT, 'exports/environment/%s.fbx' % aid))
        report(aid, root, 4000, {'x': 0.45, 'y': 0.5, 'z': 0.95})
        preview(os.path.join(ROOT, 'previews/environment/%s.png' % aid),
                target=(0, 0, 0.45), dist=1.8, angle=30, height=0.35)


# ---------------------------------------------------------------- 高书柜
def build_bookcase():
    clean_scene()
    mat = MAT_WALNUT()
    root = empty('FUR_Bookcase', display='CUBE', size=0.1)
    # 1.0w × 0.35d × 2.1h, 五层, 开口朝 -Y; 层板净高 ≥0.32
    parts = []
    parts.append(box('side', 0.03, 0.35, 2.1, loc=(-0.485, 0, 1.05), bev=0.005))
    parts.append(box('side', 0.03, 0.35, 2.1, loc=(0.485, 0, 1.05), bev=0.005))
    parts.append(box('top', 1.0, 0.35, 0.03, loc=(0, 0, 2.085), bev=0.005))
    parts.append(box('bottom', 1.0, 0.35, 0.05, loc=(0, 0, 0.025), bev=0.005))
    parts.append(box('back', 0.97, 0.015, 2.04, loc=(0, 0.1675, 1.05), bev=0.003))
    shelf_z = [0.05 + 0.025 + 0.36 * i for i in range(1, 5)]   # 4 块中层板
    for i, z in enumerate(shelf_z):
        parts.append(box('shelf', 0.94, 0.33, 0.025, loc=(0, -0.005, z), bev=0.003))
    body = join_into(parts, 'Bookcase_Body', root)
    assign(body, mat)
    # SOCKET: 各层书本锚点(层板面上, 靠前) + 顶层球棍斜靠位
    levels = [0.075] + [z + 0.0125 for z in shelf_z]           # 5 层板面 z
    for i, z in enumerate(levels):
        empty('SOCKET_Shelf_%02d' % (i + 1), loc=(0, -0.02, z), parent=root,
              display='ARROWS', size=0.06)
    empty('SOCKET_Bat', loc=(0.30, 0.0, 2.10), parent=root, display='ARROWS', size=0.06)
    save_blend(os.path.join(ROOT, 'source/blender/environment/FUR_Bookcase_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/environment/FUR_Bookcase.fbx'))
    report('FUR_Bookcase', root, 8000, {'x': 1.0, 'y': 0.35, 'z': 2.1})
    preview(os.path.join(ROOT, 'previews/environment/FUR_Bookcase.png'),
            target=(0, 0, 1.0), dist=2.8, angle=25, height=0.25)


# ---------------------------------------------------------------- 电视柜
def build_tvcabinet():
    clean_scene()
    mat = MAT_WALNUT()
    root = empty('FUR_TVCabinet', display='CUBE', size=0.1)
    # 1.1 × 0.45 × 0.65; 正面简洁不抢戏; SOCKET_TV 顶面中心
    parts = []
    parts.append(box('body', 1.1, 0.45, 0.56, loc=(0, 0, 0.37), bev=0.012))
    parts.append(box('plinth', 1.02, 0.39, 0.09, loc=(0, 0.01, 0.045), bev=0.01))  # 内收底托
    for sx in (-1, 1):   # 双开门板(与身齐平, 细缝)
        parts.append(box('door', 0.50, 0.016, 0.44, loc=(sx * 0.265, -0.222, 0.37), bev=0.006))
        parts.append(cyl('knob', 0.015, 0.02, loc=(sx * 0.05, -0.232, 0.37),
                         rot=(math.pi / 2, 0, 0), verts=12))
    body = join_into(parts, 'TVCabinet_Body', root)
    assign(body, mat)
    empty('SOCKET_TV', loc=(0, 0, 0.65), parent=root, display='ARROWS', size=0.08)
    save_blend(os.path.join(ROOT, 'source/blender/environment/FUR_TVCabinet_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/environment/FUR_TVCabinet.fbx'))
    report('FUR_TVCabinet', root, 4000, {'x': 1.1, 'y': 0.45, 'z': 0.65})
    preview(os.path.join(ROOT, 'previews/environment/FUR_TVCabinet.png'),
            target=(0, 0, 0.32), dist=1.8, angle=30, height=0.35)


build_armchair()
build_bed()
build_woodchair()
build_bookcase()
build_tvcabinet()
print('B02_DONE')
