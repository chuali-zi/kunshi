# B02 合成验收: 按 items_interactions.md §一 摆位
# 固定机位(§2): Blender (0, 2.14, 1.15) 注视 (0, -0.4, 1.0), FOV 62
# 验收点: 人物遮挡(扶手椅后蹲姿胶囊不露头) / 床底净空 / 电视屏心高=相机高
import bpy, sys, os, math
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import clean_scene, box, cyl, make_mat

ROOT = r'D:\game\con_game\art'
ENV = os.path.join(ROOT, 'source/blender/environment')
PRP = os.path.join(ROOT, 'source/blender/props')
PREV = os.path.join(ROOT, 'previews/environment')

clean_scene()

def append_asset(blend_path):
    with bpy.data.libraries.load(blend_path, link=False) as (data_from, data_to):
        data_to.objects = list(data_from.objects)
    roots = []
    for o in data_to.objects:
        if o is not None:
            bpy.context.scene.collection.objects.link(o)
            if o.parent is None:
                roots.append(o)
    return roots

def place(path, loc=(0, 0, 0), rot_z=0.0):
    for r in append_asset(path):
        r.location = loc
        r.rotation_euler = (0, 0, math.radians(rot_z))

# 场景结构
place(os.path.join(ENV, 'ENV_RoomShell_v001.blend'))
place(os.path.join(ENV, 'ENV_DoorSet_v001.blend'))
# 家具 (Blender 坐标: -Y=北)
place(os.path.join(ENV, 'FUR_Bookcase_v001.blend'), loc=(2.925, -1.8, 0), rot_z=-90)   # 贴西墙北段, 开口朝东
place(os.path.join(ENV, 'FUR_WoodChair_v001.blend'), loc=(0, -2.35, 0), rot_z=180)     # 贴北墙正中, 座面朝南
place(os.path.join(ENV, 'FUR_Armchair_v001.blend'), loc=(2.15, -2.05, 0), rot_z=180)   # 书柜右, 略靠南
place(os.path.join(ENV, 'FUR_Bed_v001.blend'), loc=(-1.3, -2.1, 0), rot_z=180)         # 贴北墙偏东, 头靠东
place(os.path.join(ENV, 'FUR_TVCabinet_v001.blend'), loc=(0, 2.375, 0))               # 贴南墙中央
# 道具
# 电视嵌南墙: 屏幕面与相机平面重合 (TV 原点 y = 2.14+0.298), 屏心 (0,2.14,1.15)
place(os.path.join(PRP, 'PRP_CRTTV_Intact_v001.blend'), loc=(0, 2.438, 0.65))
place(os.path.join(PRP, 'PRP_Blanket_Crumpled_v001.blend'), loc=(-1.15, -2.1, 0.62))  # 床上团被
place(os.path.join(PRP, 'PRP_CeilingLamp_Intact_v001.blend'), loc=(0, 0, 2.8))        # 天花中央
place(os.path.join(PRP, 'PRP_BaseballBat_v001.blend'), loc=(2.95, -1.5, 2.13), rot_z=85)  # 书柜顶

# 蹲姿胶囊 (高 1.0, 半径 0.3) 藏于扶手椅背后(北侧)
mat_cap = make_mat('MAT_Debug_Capsule', (0.9, 0.2, 0.2), rough=0.5)
cap = cyl('Debug_CrouchCapsule', 0.3, 0.94, loc=(2.15, -2.62, 0.5), verts=20, mat=mat_cap)

def set_camera(loc, target, fov_deg=62, ortho=False, ortho_scale=8):
    cd = bpy.data.cameras.new('cam')
    cam = bpy.data.objects.new('cam', cd)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    if ortho:
        cd.type = 'ORTHO'; cd.ortho_scale = ortho_scale
    else:
        cd.angle = math.radians(fov_deg)
    cd.clip_start = 0.03
    bpy.context.scene.camera = cam
    return cam

def render(path, resx=960, resy=540):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.show_shadows = True
    sc.display.shading.show_cavity = True
    sc.display.shading.color_type = 'MATERIAL'
    sc.render.resolution_x = resx
    sc.render.resolution_y = resy
    sc.render.film_transparent = False
    sc.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)

# 1) 固定机位验收 (含蹲姿胶囊遮挡检查)
# 屏幕面与相机平面重合, near 0.03 自然裁掉屏幕玻璃与前框 (同 Unity 屏内画面语义)
set_camera((0, 2.14, 1.15), (0, -0.4, 1.0))
render(os.path.join(PREV, 'B02_fixed_camera.png'))

# 2) 顶视布局 (藏天花)
ceil = bpy.data.objects.get('Shell_Ceiling')
if ceil: ceil.hide_render = True
set_camera((0, 0, 12), (0, 0, 0), ortho=True, ortho_scale=9)
render(os.path.join(PREV, 'B02_topdown.png'))

# 3) 床底净空特写: 剖视高度看床底 0.4 净空 (侧视床)
set_camera((-1.3, 0.5, 0.35), (-1.3, -2.1, 0.35), fov_deg=50)
render(os.path.join(PREV, 'B02_bed_clearance.png'))

# 4) 电视屏心高度特写: 前侧视, 屏心应=1.15
set_camera((1.3, 1.0, 1.15), (0, 2.4, 1.15), fov_deg=50)
render(os.path.join(PREV, 'B02_tv_height.png'))
print('B02_COMPOSITE_DONE')
