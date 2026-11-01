"""Restore separate outer/inner shade materials on the v003 lamp exports."""
import bpy
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from ak import make_mat

ROOT=Path('D:/game/con_game')
bpy.ops.wm.read_factory_settings(use_empty=True)

for state in ('Intact','Broken'):
    src=ROOT/'art/source/blender/props'/f'PRP_CeilingLamp_{state}_v003.blend'
    dst=ROOT/'art/source/blender/props'/f'PRP_CeilingLamp_{state}_v004.blend'
    with bpy.data.libraries.load(str(src),link=False) as (lib,loaded):
        loaded.objects=list(lib.objects)
    for obj in loaded.objects:
        if obj:bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.update()
    shade=bpy.data.objects.get('Lamp_Shade')
    if shade is None or shade.type!='MESH':
        raise RuntimeError(f'{src.name} is missing Lamp_Shade')
    old_indices=[p.material_index for p in shade.data.polygons]
    outer=make_mat('MAT_MV_LampShadeOuter',(.30,.31,.27),rough=.78)
    inner=make_mat('MAT_LampShade_Inner',(.80,.75,.68),rough=.4)
    shade.data.materials.clear()
    shade.data.materials.append(outer)
    shade.data.materials.append(inner)
    for poly,idx in zip(shade.data.polygons,old_indices):
        poly.material_index=0 if idx==0 else 1
    for lib in list(bpy.data.libraries):
        bpy.data.libraries.remove(lib)
    bpy.ops.wm.save_as_mainfile(filepath=str(dst))
    out=ROOT/'art/exports/props'/f'PRP_CeilingLamp_{state}.fbx'
    unity=ROOT/'Assets/_Game/Art/Props'/out.name
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(out),object_types={'EMPTY','MESH'},bake_space_transform=True,
        axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',use_mesh_modifiers=True,add_leaf_bones=False)
    unity.parent.mkdir(parents=True,exist_ok=True)
    import shutil
    shutil.copy2(out,unity)
    print('LAMP_V004_DONE',state,len(shade.data.polygons),str(dst),str(out),str(unity))
    bpy.ops.wm.read_factory_settings(use_empty=True)
