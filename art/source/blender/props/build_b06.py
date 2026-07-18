# B06 装备道具(角色跳过) — asset_spec_3d.md §6
# PRP_Pistol_Main / PRP_Pistol_Backup / PRP_Flashlight
# 枪口/照射方向 = -Y, 原点=握持点(§1.2-3)
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
PRP_DIR = os.path.join(ROOT, 'source/blender/props')
METAL = lambda: make_mat('MAT_Metal_Dark', (0.04, 0.04, 0.045), metallic=0.8, rough=0.45)
METAL2 = lambda: make_mat('MAT_Metal_Dark_Backup', (0.05, 0.05, 0.055), metallic=0.8, rough=0.35)


def _pistol(root, mat, compact=False):
    """半自动手枪, 枪口 -Y; 原点=握把与扳机护圈交界; 全长含握把后凸"""
    L = 0.145 if compact else 0.18         # 枪身(套筒)段长
    slide_L = L - 0.02
    parts = []
    # 套筒 (上部)
    parts.append(box('slide', 0.028, slide_L, 0.028, loc=(0, -L / 2 + 0.01, 0.032), bev=0.004))
    # 枪身 (下部, 含扳机护圈横梁)
    parts.append(box('frame', 0.026, L - 0.045, 0.020, loc=(0, -L / 2 + 0.0225, 0.008), bev=0.003))
    # 枪口帽
    parts.append(cyl('muzzle', 0.009, 0.006, loc=(0, -L + 0.008, 0.032),
                     rot=(math.pi / 2, 0, 0), verts=16))
    # 扳机护圈 (前后两柱+底梁)
    parts.append(box('tg_f', 0.004, 0.004, 0.020, loc=(0, -0.052, -0.006), bev=0.001))
    parts.append(box('tg_b', 0.004, 0.004, 0.020, loc=(0, -0.020, -0.006), bev=0.001))
    parts.append(box('tg_d', 0.004, 0.036, 0.004, loc=(0, -0.036, -0.016), bev=0.001))
    # 扳机
    parts.append(box('trigger', 0.006, 0.006, 0.014, loc=(0, -0.030, -0.008), bev=0.002))
    # 握把 (向后下倾 12°, 前移收全长)
    grip = box('grip', 0.030, 0.024, 0.075 if not compact else 0.060,
               loc=(0, 0.004, -0.038), bev=0.005)
    grip.rotation_euler = (math.radians(12), 0, 0)
    bpy.context.view_layer.objects.active = grip
    grip.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    grip.select_set(False)
    parts.append(grip)
    # 准星/照门
    parts.append(box('sight_f', 0.004, 0.004, 0.008, loc=(0, -L + 0.018, 0.052), bev=0.0))
    parts.append(box('sight_r', 0.012, 0.004, 0.008, loc=(0, -0.006, 0.052), bev=0.0))
    body = join_into(parts, 'Pistol_Body', root)
    assign(body, mat)
    return body


def build_pistol_main():
    clean_scene()
    mat = METAL()
    root = empty('PRP_Pistol_Main', display='CUBE', size=0.05)
    _pistol(root, mat, compact=False)
    # 枪管下导轨挂点: 套筒底面前段
    empty('SOCKET_RailLight', loc=(0, -0.13, 0.0), parent=root, display='ARROWS', size=0.02)
    # 导轨凸条 (挂接手电的实体)
    box('rail', 0.020, 0.05, 0.005, loc=(0, -0.13, -0.004), bev=0.001, mat=mat, parent=root)
    save_blend(os.path.join(PRP_DIR, 'PRP_Pistol_Main_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Pistol_Main.fbx'))
    report('PRP_Pistol_Main', root, 4000, {'y': 0.20})
    preview(os.path.join(ROOT, 'previews/props/PRP_Pistol_Main.png'),
            target=(0, -0.08, 0.01), dist=0.5, angle=35, height=0.35)


def build_pistol_backup():
    clean_scene()
    mat = METAL2()
    root = empty('PRP_Pistol_Backup', display='CUBE', size=0.05)
    _pistol(root, mat, compact=True)
    save_blend(os.path.join(PRP_DIR, 'PRP_Pistol_Backup_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Pistol_Backup.fbx'))
    report('PRP_Pistol_Backup', root, 4000, {'y': 0.16})
    preview(os.path.join(ROOT, 'previews/props/PRP_Pistol_Backup.png'),
            target=(0, -0.06, 0.01), dist=0.45, angle=35, height=0.35)


def build_flashlight():
    clean_scene()
    mat = METAL()
    root = empty('PRP_Flashlight', display='CUBE', size=0.05)
    # 长 0.13 圆柱, 照射 -Y; 原点=筒身中心
    body_parts = [
        # 筒身
        cyl('tube', 0.013, 0.095, loc=(0, 0.0125, 0), rot=(math.pi / 2, 0, 0), verts=20),
        # 灯头(前段粗环)
        cyl('head', 0.019, 0.030, loc=(0, -0.050, 0), rot=(math.pi / 2, 0, 0), verts=20),
        # 尾盖
        cyl('tail', 0.014, 0.008, loc=(0, 0.0625, 0), rot=(math.pi / 2, 0, 0), verts=20),
    ]
    for p in body_parts:    # 圆柱旋转先应用, 避免合并残留
        bpy.context.view_layer.objects.active = p
        p.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        p.select_set(False)
    # 防滚棱: 筒身两侧切面凸条 (可控滚动)
    body_parts.append(box('antiroll', 0.030, 0.05, 0.004, loc=(0, 0.01, 0.012), bev=0.001))
    body = join_into(body_parts, 'Flashlight_Body', root)
    assign(body, mat)
    # 灯头镜片 (发光槽, 光锥 Unity 做)
    lens = cyl('Flashlight_Lens', 0.016, 0.002, loc=(0, -0.0655, 0),
               rot=(math.pi / 2, 0, 0), verts=20, parent=root)
    lens.data.materials.append(make_mat('MAT_Flashlight_Emissive', (0.9, 0.85, 0.7),
                                        emission=(0.9, 0.85, 0.7), estr=2.0))
    # 导轨挂接点: 卡扣位 (筒顶 0.013 + 导轨底 0.007 → 0.020 贴合无穿插)
    empty('SOCKET_RailMount', loc=(0, -0.005, 0.020), parent=root, display='ARROWS', size=0.02)
    save_blend(os.path.join(PRP_DIR, 'PRP_Flashlight_v001.blend'))
    export_fbx(os.path.join(ROOT, 'exports/props/PRP_Flashlight.fbx'))
    report('PRP_Flashlight', root, 1500, {'y': 0.13})
    preview(os.path.join(ROOT, 'previews/props/PRP_Flashlight.png'),
            target=(0, 0, 0), dist=0.35, angle=35, height=0.4)


def build_dock_test():
    """验收: 手电经 SOCKET_RailLight 挂到主枪导轨, 渲染检查无穿插"""
    bpy.ops.wm.read_homefile(use_empty=True)   # 当前文件已是手电筒, 须先开空场景
    clean_scene()
    for f in ('PRP_Pistol_Main_v001.blend', 'PRP_Flashlight_v001.blend'):
        with bpy.data.libraries.load(os.path.join(PRP_DIR, f), link=False) as (df, dt):
            dt.objects = list(df.objects)
        for o in dt.objects:
            if o is not None:
                bpy.context.scene.collection.objects.link(o)
    gun = bpy.data.objects['PRP_Pistol_Main']
    torch = bpy.data.objects['PRP_Flashlight']
    bpy.context.view_layer.update()
    rail = bpy.data.objects['SOCKET_RailLight']
    mount = bpy.data.objects['SOCKET_RailMount']
    # 手电平移: mount 对准 rail (同向挂接)
    offset = rail.matrix_world.translation - mount.matrix_world.translation
    torch.location = torch.location + offset
    bpy.context.view_layer.update()
    # 碰撞粗查: 手电顶面 z 应贴合枪底 (0.015+0.013 ≈ rail 下方)
    zs = [ (torch.matrix_world @ Vector(c)).z for c in bpy.data.objects['Flashlight_Body'].bound_box ]
    dz = rail.matrix_world.translation.z - max(zs)
    print('DOCK_CHECK gap_z=%.4f (期望约 0, 手电顶贴导轨)' % dz)
    report('Docked', gun, 6000, {})
    preview(os.path.join(ROOT, 'previews/props/PRP_Pistol_Main_Docked.png'),
            target=(0, -0.09, 0.0), dist=0.5, angle=30, height=0.3)


build_pistol_main()
build_pistol_backup()
build_flashlight()
build_dock_test()
print('B06_DONE')
