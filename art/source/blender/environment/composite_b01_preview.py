# B01 合成验收渲染: 房间壳 + 门组 + 黑走廊
# 固定机位(§2): Unity (0,1.15,-2.14) 注视 (0,1.0,0.4), FOV 62, near 0.03
#   → Blender (0, 2.14, 1.15) 注视 (0, -0.4, 1.0)
import bpy, sys, os, math
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import clean_scene

ROOT = r'D:\game\con_game\art'
PREV = os.path.join(ROOT, 'previews/environment')

clean_scene()

# 从各 .blend 追加根对象(连同子级)
def append_asset(blend_path, root_name):
    with bpy.data.libraries.load(blend_path, link=False) as (data_from, data_to):
        data_to.objects = [n for n in data_from.objects]
    for o in data_to.objects:
        if o is not None:
            bpy.context.scene.collection.objects.link(o)

for name in ('ENV_RoomShell', 'ENV_DoorSet', 'ENV_BlackCorridor'):
    append_asset(os.path.join(ROOT, 'source/blender/environment/%s_v001.blend' % name), name)

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

# 1) 固定机位验收视角
set_camera((0, 2.14, 1.15), (0, -0.4, 1.0))
render(os.path.join(PREV, 'B01_fixed_camera.png'))

# 2) 顶视布局 (藏天花, 检查净空/门洞/走廊接位)
ceil = bpy.data.objects.get('Shell_Ceiling')
if ceil: ceil.hide_render = True
set_camera((0, 0, 12), (0, 0, 0), ortho=True, ortho_scale=9)
render(os.path.join(PREV, 'B01_topdown.png'))
if ceil: ceil.hide_render = False

# 3) 门开 70°: 验证黑走廊在开门瞬间可见
panel = bpy.data.objects.get('Door_Panel')
if panel:
    panel.rotation_euler = (0, 0, math.radians(-70))  # 向房内(西北)开
set_camera((0, 2.14, 1.15), (-1.5, -0.5, 1.1))  # 门在 Blender -X 墙
render(os.path.join(PREV, 'B01_door_open.png'))
print('COMPOSITE_DONE')
