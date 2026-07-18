# B05 合成验收: 在 B04 场景上加入遥控器/书/花瓶/挂钟/镜子/杂物
# 验收点: 固定机位小道具可辨 / 玫瑰红色焦点 / 镜斜 12° 收门+电视 / 挂钟北墙偏东
import bpy, sys, os, math
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import clean_scene

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
    rs = []
    for r in append_asset(path):
        r.location = loc
        r.rotation_euler = (0, 0, math.radians(rot_z))
        rs.append(r)
    return rs

# B01/B02/B04 场景 (Blender = (-Unity_x, -Unity_z))
place(os.path.join(ENV, 'ENV_RoomShell_v001.blend'))
place(os.path.join(ENV, 'ENV_DoorSet_v001.blend'))
place(os.path.join(ENV, 'FUR_Bookcase_v001.blend'), loc=(2.925, -1.8, 0), rot_z=-90)
place(os.path.join(ENV, 'FUR_WoodChair_v001.blend'), loc=(0, -2.35, 0), rot_z=180)
place(os.path.join(ENV, 'FUR_Armchair_v001.blend'), loc=(2.15, -2.05, 0), rot_z=180)
place(os.path.join(ENV, 'FUR_Bed_v001.blend'), loc=(-1.3, -2.1, 0), rot_z=180)
place(os.path.join(ENV, 'FUR_TVCabinet_v001.blend'), loc=(0, 2.375, 0))
place(os.path.join(PRP, 'PRP_CRTTV_Intact_v001.blend'), loc=(0, 2.438, 0.65))
place(os.path.join(PRP, 'PRP_CeilingLamp_Intact_v001.blend'), loc=(0, 0, 2.8))
place(os.path.join(ENV, 'FUR_Dresser_v001.blend'), loc=(2.86, -0.3, 0), rot_z=-90)
place(os.path.join(ENV, 'FUR_SideTable_v001.blend'), loc=(-2.45, -1.35, 0))  # 床尾与门之间(设计值)
place(os.path.join(ENV, 'ENV_BoardedWindow_v001.blend'), loc=(1.2, -2.61, 0.9))
place(os.path.join(PRP, 'PRP_Rug_v001.blend'), loc=(-0.4, -1.3, 0), rot_z=-16)
place(os.path.join(PRP, 'PRP_TableLamp_Intact_v001.blend'), loc=(2.86, 0.02, 0.85), rot_z=-90)

# ---- B05 新增
def socket_world(name):
    bpy.context.view_layer.update()   # 刷新矩阵, 否则读到放置前的旧值
    o = bpy.data.objects.get(name)
    return o.matrix_world.translation.copy() if o else None

# 遥控器 → 抽屉柜 SOCKET_RemoteTop
p = socket_world('SOCKET_RemoteTop')
remote = place(os.path.join(PRP, 'PRP_Remote_v001.blend'), loc=p if p else (2.58, -0.38, 0.88), rot_z=-90)
# 书本 → 书柜 SOCKET_Shelf_02 (立放一排 5 册)
p2 = socket_world('SOCKET_Shelf_02')
if p2:
    books = place(os.path.join(PRP, 'PRP_Books_Set_v001.blend'), loc=p2, rot_z=-90)
    # 书组排布方向沿书柜宽度; Book_Open 单独放抽屉柜台面
    bo = bpy.data.objects.get('Book_Open')
    if bo and p:
        bo.location = (p.x + 0.1, p.y + 0.15, p.z)
# 花瓶 → 圆几 SOCKET_Vase
p3 = socket_world('SOCKET_Vase')
place(os.path.join(PRP, 'PRP_Vase_v001.blend'), loc=p3 if p3 else (-2.45, -1.35, 0.555))
# 挂钟 → 北墙偏东床上方 (Unity x≈+1.3, z=2.55 → Blender (-1.3, -2.55)); 面朝房内(+Y) rot 180
place(os.path.join(PRP, 'PRP_WallClock_v001.blend'), loc=(-1.3, -2.56, 1.85), rot_z=180)
# 半身镜 → 东墙南段 (Unity x=+3.0, z≈-1.0 → Blender (-3.0, 1.0)); 面朝 -X 入房 + 内偏 12°
place(os.path.join(PRP, 'PRP_Mirror_v001.blend'), loc=(-3.0, 1.0, 0.5), rot_z=78)
# 抽屉杂物 → 抽屉柜前地面散落
place(os.path.join(PRP, 'PRP_DrawerClutter_v001.blend'), loc=(2.45, 0.05, 0), rot_z=40)

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

# 1) 固定机位全景
set_camera((0, 2.14, 1.15), (0, -0.4, 1.0))
render(os.path.join(PREV, 'B05_fixed_camera.png'))
# 2) 圆几花瓶特写: 玫瑰红色焦点 (南侧看向圆几, 避开床体)
set_camera((-1.5, -0.5, 1.1), (-2.45, -1.35, 0.8), fov_deg=40)
render(os.path.join(PREV, 'B05_vase_focus.png'))
# 3) 镜面角度: 从床区看东墙, 检查斜置可读
set_camera((-1.0, -1.5, 1.3), (-3.0, 1.0, 1.1), fov_deg=45)
render(os.path.join(PREV, 'B05_mirror_angle.png'))
# 4) 顶视
ceil = bpy.data.objects.get('Shell_Ceiling')
if ceil: ceil.hide_render = True
set_camera((0, 0, 12), (0, 0, 0), ortho=True, ortho_scale=9)
render(os.path.join(PREV, 'B05_topdown.png'))
print('B05_COMPOSITE_DONE')
