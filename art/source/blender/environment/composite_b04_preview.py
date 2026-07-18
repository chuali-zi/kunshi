# B04 合成验收: 在 B02 布局上加入抽屉柜/圆几/地毯/封死窗/台灯
# 验收点: 抽屉抽拉无穿插 / 封死窗固定机位读作深色挂画 / 地毯斜铺动线
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
    for r in append_asset(path):
        r.location = loc
        r.rotation_euler = (0, 0, math.radians(rot_z))

# B01/B02 已有 (映射: Blender = (-Unity_x, -Unity_z))
place(os.path.join(ENV, 'ENV_RoomShell_v001.blend'))
place(os.path.join(ENV, 'ENV_DoorSet_v001.blend'))
place(os.path.join(ENV, 'FUR_Bookcase_v001.blend'), loc=(2.925, -1.8, 0), rot_z=-90)
place(os.path.join(ENV, 'FUR_WoodChair_v001.blend'), loc=(0, -2.35, 0), rot_z=180)
place(os.path.join(ENV, 'FUR_Armchair_v001.blend'), loc=(2.15, -2.05, 0), rot_z=180)
place(os.path.join(ENV, 'FUR_Bed_v001.blend'), loc=(-1.3, -2.1, 0), rot_z=180)
place(os.path.join(ENV, 'FUR_TVCabinet_v001.blend'), loc=(0, 2.375, 0))
place(os.path.join(PRP, 'PRP_CRTTV_Intact_v001.blend'), loc=(0, 2.438, 0.65))
place(os.path.join(PRP, 'PRP_Blanket_Crumpled_v001.blend'), loc=(-1.15, -2.1, 0.62))
place(os.path.join(PRP, 'PRP_CeilingLamp_Intact_v001.blend'), loc=(0, 0, 2.8))

# B04 新增
place(os.path.join(ENV, 'FUR_Dresser_v001.blend'), loc=(2.86, -0.3, 0), rot_z=-90)   # 西墙书柜南
place(os.path.join(ENV, 'FUR_SideTable_v001.blend'), loc=(-2.55, -2.1, 0))           # 床右侧
place(os.path.join(ENV, 'ENV_BoardedWindow_v001.blend'), loc=(1.2, -2.61, 0.9))      # 北墙窗洞
place(os.path.join(PRP, 'PRP_Rug_v001.blend'), loc=(-0.4, -1.3, 0), rot_z=-16)       # 斜铺动线
place(os.path.join(PRP, 'PRP_TableLamp_Intact_v001.blend'), loc=(2.86, 0.02, 0.85), rot_z=-90)  # 抽屉柜台面

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

# 1) 固定机位: 封死窗应读作深色挂画
set_camera((0, 2.14, 1.15), (0, -0.4, 1.0))
render(os.path.join(PREV, 'B04_fixed_camera.png'))

# 2) 抽屉中屉抽出 0.35m: 沿柜体正面(Blender -X)平移, 检查无穿插/内腔
dr = bpy.data.objects.get('Dresser_Drawer_Mid')
if dr:
    dr.location = dr.location + Vector((-0.35, 0, 0))
set_camera((1.6, 0.9, 0.9), (2.8, -0.3, 0.5), fov_deg=45)
render(os.path.join(PREV, 'B04_drawer_pulled.png'))
if dr:
    dr.location = dr.location + Vector((0.35, 0, 0))

# 3) 顶视: 地毯斜铺动线 + 新家具摆位
ceil = bpy.data.objects.get('Shell_Ceiling')
if ceil: ceil.hide_render = True
set_camera((0, 0, 12), (0, 0, 0), ortho=True, ortho_scale=9)
render(os.path.join(PREV, 'B04_topdown.png'))
print('B04_COMPOSITE_DONE')
