import bpy, sys, math
from pathlib import Path
from mathutils import Vector
ROOT=Path('D:/game/con_game')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/previews/mv_20260911/lookdev_staging.blend'))
source=ROOT/'art/source/blender/props/composite_b08_preview.py'
code=source.read_text(encoding='utf-8').split('def set_camera')[0]
code=code.replace('_v002.blend','_v003.blend')
code=code.replace('loc=(2.15, -2.05, 0), rot_z=180','loc=(1.8, -1.5, 0), rot_z=200')
code=code.replace('loc=(0, 0, 2.8)','loc=(0, 0, 2.55)')
code=code.replace('loc=(-0.4, -1.3, 0)','loc=(0, -0.85, 0)')
exec(compile(code,str(source),'exec'),{'__file__':str(source)})
with bpy.data.libraries.load(str(ROOT/'art/source/blender/props/PRP_Blanket_Crumpled_v003.blend'),link=False) as (src,dst):
    dst.objects=list(src.objects)
for o in dst.objects:
    if o:
        bpy.context.collection.objects.link(o)
        if not o.parent:o.location=(-1.15,-2.1,.62)

sc=bpy.context.scene
sc.render.engine='CYCLES'
sc.cycles.samples=16
sc.cycles.use_denoising=True
sc.cycles.max_bounces=5
try:
    prefs=bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type='OPTIX';prefs.get_devices()
    for d in prefs.devices:d.use=d.type!='CPU'
    if any(d.use for d in prefs.devices):sc.cycles.device='GPU'
except Exception:pass
sc.render.resolution_x=1600;sc.render.resolution_y=900;sc.render.resolution_percentage=100
sc.render.image_settings.file_format='PNG'
sc.world.use_nodes=True
sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.11,.085,1)
sc.world.node_tree.nodes['Background'].inputs[1].default_value=.15
sc.view_settings.view_transform='AgX'
sc.view_settings.exposure=-.65

def light(name,typ,loc,power,color,size,target=None):
    d=bpy.data.lights.new(name,typ);d.energy=power;d.color=color
    if typ=='AREA':d.shape='DISK';d.size=size
    else:d.shadow_soft_size=size
    o=bpy.data.objects.new(name,d);sc.collection.objects.link(o);o.location=loc
    if target:o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    return o

light('Pendant warm pool','POINT',(0,0,2.02),95,(1,.82,.57),.06)
light('Dresser practical','POINT',(2.8,.02,1.21),45,(1,.7,.36),.13)
light('Camera side soft bounce','AREA',(0,2.35,1.7),24,(1,.9,.72),3,(0,-1.3,1))
light('Ceiling bounce','AREA',(0,-.5,2.6),30,(1,.9,.75),2.2,(0,-1.2,.5))
camdata=bpy.data.cameras.new('MV_Fixed_Camera');cam=bpy.data.objects.new('MV_Fixed_Camera',camdata)
sc.collection.objects.link(cam);sc.camera=cam;camdata.lens=18.5;camdata.clip_start=.03
def shot(name,loc,target,lens):
    cam.location=loc;cam.rotation_euler=(Vector(target)-Vector(loc)).to_track_quat('-Z','Y').to_euler();camdata.lens=lens
    sc.render.filepath=str(ROOT/'art/previews/mv_20260911'/name)
    bpy.ops.render.render(write_still=True)
shot('blender_room.png',(0,2.14,1.15),(0,-1.0,1.1),18.5)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/source/blender/environment/ROOM_MV_Lookdev_v003.blend'))
shot('blender_furniture.png',(-.6,.75,1.5),(2,-1.45,.95),28)
shot('blender_door.png',(1,.9,1.5),(-2.5,-.7,1.1),24)
print('MV_BLENDER_RENDER_DONE')
