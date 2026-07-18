# B04 次级家具 — asset_spec_3d.md §4/§5/§6
# FUR_Dresser(三屉) / FUR_SideTable / PRP_Rug / ENV_BoardedWindow
# 坐标映射: Unity 东(+X)=Blender -X, 北(+Z)=Blender -Y
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
ENV_DIR = os.path.join(ROOT, 'source/blender/environment')
PRP_DIR = os.path.join(ROOT, 'source/blender/props')
MAT_WALNUT = lambda: make_mat('MAT_Wood_Walnut', (0.23, 0.15, 0.10), rough=0.5)
MAT_METAL = lambda: make_mat('MAT_Metal_Dark', (0.04, 0.04, 0.045), metallic=0.8, rough=0.45)
MAT_WORN = lambda: make_mat('MAT_Wood_Worn', (0.62, 0.46, 0.30), rough=0.6)


# ---------------------------------------------------------------- 三屉抽屉柜
def build_dresser():
    clean_scene()
    mat_w, mat_m = MAT_WALNUT(), MAT_METAL()
    root = empty('FUR_Dresser', display='CUBE', size=0.1)
    # 1.1 × 0.48 × 0.85, 正面 -Y; 三屉独立, 原点=滑轨后端中心, 沿 -Y 抽出
    W, D, H = 1.1, 0.48, 0.85
    parts = []
    parts.append(box('side', 0.025, D, H - 0.10, loc=(-W / 2 + 0.0125, 0, 0.10 + (H - 0.10) / 2), bev=0.006))
    parts.append(box('side', 0.025, D, H - 0.10, loc=(W / 2 - 0.0125, 0, 0.10 + (H - 0.10) / 2), bev=0.006))
    parts.append(box('top', W, D, 0.03, loc=(0, 0, H - 0.015), bev=0.008))
    parts.append(box('bottom', W - 0.05, D - 0.02, 0.03, loc=(0, 0, 0.115), bev=0.005))
    parts.append(box('back', W - 0.05, 0.015, H - 0.15, loc=(0, D / 2 - 0.008, 0.10 + (H - 0.15) / 2), bev=0.003))
    parts.append(box('plinth', W - 0.06, D - 0.05, 0.10, loc=(0, 0.01, 0.05), bev=0.008))  # 底托
    # 两道屉间隔板 (内腔真实: 三格)
    for z in (0.345, 0.585):
        parts.append(box('divider', W - 0.05, D - 0.04, 0.02, loc=(0, 0.005, z), bev=0.003))
    body = join_into(parts, 'Dresser_Body', root)
    assign(body, mat_w)

    # 抽屉: 面板+盒体(无顶)+拉手; 在最终位置建模, 再把原点搬到滑轨后端中心
    drawer_z = [0.225, 0.465, 0.705]   # Bot/Mid/Top 面板中心
    names = ['Dresser_Drawer_Bot', 'Dresser_Drawer_Mid', 'Dresser_Drawer_Top']
    for nm, zc in zip(names, drawer_z):
        zb = zc - 0.105                # 屉盒底
        dp = []
        dp.append(box('front', 1.02, 0.025, 0.21, loc=(0, -0.2275, zc), bev=0.008, mat=mat_w))
        dp.append(box('dbottom', 0.94, 0.405, 0.015, loc=(0, -0.0125, zb + 0.008), bev=0.002, mat=mat_w))
        dp.append(box('dside', 0.015, 0.405, 0.14, loc=(-0.47, -0.0125, zb + 0.078), bev=0.002, mat=mat_w))
        dp.append(box('dside', 0.015, 0.405, 0.14, loc=(0.47, -0.0125, zb + 0.078), bev=0.002, mat=mat_w))
        dp.append(box('dback', 0.94, 0.015, 0.14, loc=(0, 0.1825, zb + 0.078), bev=0.002, mat=mat_w))
        dp.append(box('hbase', 0.03, 0.02, 0.02, loc=(0, -0.243, zc), bev=0.004, mat=mat_m))
        dp.append(cyl('hpull', 0.012, 0.10, loc=(0, -0.256, zc), rot=(0, math.pi / 2, 0), verts=12, mat=mat_m))
        dr = join_into(dp, nm, root)   # 材质槽保留胡桃木+深金属两槽
        # 原点 → 滑轨后端中心 (屉盒后缘底中)
        P = Vector((0, 0.19, zb + 0.02))
        dr.data.transform(Matrix.Translation(-P))
        dr.location = P
    # 台面锚点
    empty('SOCKET_LampTop', loc=(-0.32, 0.0, H), parent=root, display='ARROWS', size=0.06)
    empty('SOCKET_RemoteTop', loc=(0.28, -0.08, H), parent=root, display='ARROWS', size=0.06)
    save_blend(os.path.join(ENV_DIR, 'FUR_Dresser_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/environment/FUR_Dresser.fbx'))
    report('FUR_Dresser', root, 8000, {'x': W, 'y': 0.51, 'z': H})  # y 含抽屉拉手外凸
    preview(os.path.join(ROOT, 'previews/environment/FUR_Dresser.png'),
            target=(0, 0, 0.42), dist=1.8, angle=30, height=0.35)


# ---------------------------------------------------------------- 小圆几
def build_sidetable():
    clean_scene()
    mat = MAT_WALNUT()
    root = empty('FUR_SideTable', display='CUBE', size=0.1)
    parts = []
    parts.append(lathe('top', [(0.53, 0.20), (0.545, 0.225), (0.555, 0.225), (0.555, 0.0)],
                       segments=28, axis='Z', cap=False))
    parts.append(lathe('column', [(0.08, 0.0001), (0.08, 0.14), (0.14, 0.030), (0.50, 0.030),
                                  (0.53, 0.035)], segments=20, axis='Z'))
    parts.append(lathe('base', [(0.005, 0.0001), (0.01, 0.16), (0.05, 0.15), (0.08, 0.05)],
                       segments=24, axis='Z'))
    body = join_into(parts, 'SideTable_Body', root)
    assign(body, mat)
    empty('SOCKET_Vase', loc=(0, 0, 0.555), parent=root, display='ARROWS', size=0.05)
    save_blend(os.path.join(ENV_DIR, 'FUR_SideTable_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/environment/FUR_SideTable.fbx'))
    report('FUR_SideTable', root, 1500, {'x': 0.45, 'y': 0.45, 'z': 0.555})
    preview(os.path.join(ROOT, 'previews/environment/FUR_SideTable.png'),
            target=(0, 0, 0.27), dist=1.2, angle=30, height=0.4)


# ---------------------------------------------------------------- 长条地毯
def build_rug():
    clean_scene()
    mat = make_mat('MAT_Floor_DarkGreen_Rug', (0.20, 0.26, 0.20), rough=1.0)  # 比满铺更旧
    root = empty('PRP_Rug', display='CUBE', size=0.1)
    # 3.5 × 0.9, 厚 0.02 薄网格; 远端角(+X+Y)卷翘+加密, 角部空物体 Rug_GrabCorner
    def z(x, y):
        h = 0.02
        d = math.hypot(x - 1.62, y - 0.38)
        h += 0.035 * math.exp(-(d / 0.16) ** 2)          # 卷翘角
        h += 0.004 * math.sin(x * 6) * math.cos(y * 8)   # 微起伏
        return h
    rug = grid_sheet('Rug_Body', 70, 22, 3.5, 0.9, z, mat=mat, parent=root, skirt_z=0.0)
    empty('Rug_GrabCorner', loc=(1.68, 0.40, 0.05), parent=root, display='SPHERE', size=0.03)
    save_blend(os.path.join(PRP_DIR, 'PRP_Rug_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Rug.fbx'))
    report('PRP_Rug', root, 4000, {'x': 3.5, 'y': 0.9})
    preview(os.path.join(ROOT, 'previews/props/PRP_Rug.png'),
            target=(0, 0, 0), dist=3.4, angle=35, height=0.8)


# ---------------------------------------------------------------- 封死窗
def build_boarded_window():
    clean_scene()
    mat_w = make_mat('MAT_Wood_BoardDark', (0.13, 0.085, 0.055), rough=0.85)  # 深色旧板, 读作挂画
    mat_brick = make_mat('MAT_Brick_Aged', (0.35, 0.20, 0.16), rough=0.9)
    root = empty('ENV_BoardedWindow', display='CUBE', size=0.1)
    # 1.4 × 1.1; 房间侧 = +Y (放北墙前面朝南墙, Unity 中即朝 -Z ✓); 原点=窗台洞口底中
    # 外框: 深色厚重画框感, 凸出墙面
    fr = []
    fr.append(box('fr', 1.44, 0.05, 0.06, loc=(0, 0.02, 1.11), bev=0.008))
    fr.append(box('fr', 1.44, 0.05, 0.06, loc=(0, 0.02, -0.01), bev=0.008))
    fr.append(box('fr', 0.06, 0.05, 1.14, loc=(-0.71, 0.02, 0.55), bev=0.008))
    fr.append(box('fr', 0.06, 0.05, 1.14, loc=(0.71, 0.02, 0.55), bev=0.008))
    frame = join_into(fr, 'Window_Frame', root)
    assign(frame, mat_w)
    # 砖墙 (木板后 0.1m)
    brick = box('Window_BrickWall', 1.34, 0.08, 1.04, loc=(0, -0.06, 0.55), bev=0.004,
                mat=mat_brick, parent=root)
    cube_uv(brick)
    # 5 条横向木板, 独立对象独立原点(各自中心), 缝隙不透光(微叠)
    board_z = [0.135 + k * 0.208 for k in range(5)]
    for k, zc in enumerate(board_z):
        b = box('Window_Board_%02d' % (k + 1), 1.32, 0.028, 0.215,
                loc=(0, 0.005, zc), bev=0.004, mat=mat_w, parent=root)
        b.rotation_euler = (0, 0, math.radians((k % 3 - 1) * 0.6))  # 微不等角旧木感
        # 断裂替换件: 同中心同占位的两半(裂缝错牙)
    for k, zc in enumerate(board_z):
        segs = []
        segs.append(box('segL', 0.68, 0.028, 0.215, loc=(-0.33, 0.005, zc + 0.004), bev=0.004))
        segs.append(box('segR', 0.68, 0.028, 0.215, loc=(0.34, 0.005, zc - 0.006), bev=0.004))
        br = join_into(segs, 'Window_Board_Broken_%02d' % (k + 1), root)
        assign(br, mat_w)
        br.location = (0, 0.005, zc)   # 与完好板同中心
        br.data.transform(Matrix.Translation(Vector((0, -0.005, -zc))))
    save_blend(os.path.join(ENV_DIR, 'ENV_BoardedWindow_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/environment/ENV_BoardedWindow.fbx'))
    report('ENV_BoardedWindow', root, 4000, {'x': 1.44, 'z': 1.14})  # 含完好+断裂两套板
    preview(os.path.join(ROOT, 'previews/environment/ENV_BoardedWindow.png'),
            target=(0, 0, 0.55), dist=2.2, angle=160, height=0.15)  # 正面在 +Y 侧


build_dresser()
build_sidetable()
build_rug()
build_boarded_window()
print('B04_DONE')
