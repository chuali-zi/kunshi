# B08-2 贴图应用: 重开各 .blend, 换纹理材质(同名), 按类投影 UV, 存 _v002 并重导 FBX
# 映射: 材质名 → TEX 组; UV: 立方体投影(墙面4m/家具2m/金属1m) / 小道具入 Atlas 格
import bpy, sys, os
sys.path.insert(0, os.path.dirname(__file__))
from ak import clean_scene, export_fbx

ROOT = r'D:\game\con_game\art'
TEXDIR = os.path.join(ROOT, 'exports/textures')

TEXMAP = {
    'MAT_Wallpaper_FloralAged': ('TEX_MAT_Wallpaper_FloralAged', 0.0, 0.9),
    'MAT_Wallpaper_Damage': ('TEX_MAT_Wallpaper_FloralAged', 0.0, 0.9),
    'MAT_Floor_DarkGreen': ('TEX_MAT_Floor_DarkGreen', 0.0, 0.95),
    'MAT_Floor_DarkGreen_Rug': ('TEX_MAT_Floor_DarkGreen', 0.0, 0.98),
    'MAT_Wood_Walnut': ('TEX_MAT_Wood_Walnut', 0.0, 0.5),
    'MAT_Wood_Worn': ('TEX_MAT_Wood_Worn', 0.0, 0.6),
    'MAT_Leather_Burgundy': ('TEX_MAT_Leather_Burgundy', 0.0, 0.55),
    'MAT_Fabric_Bedding': ('TEX_MAT_Fabric_Bedding', 0.0, 0.85),
    'MAT_Metal_Dark': ('TEX_MAT_Metal_Dark', 0.85, 0.45),
    'MAT_Metal_Dark_Backup': ('TEX_MAT_Metal_Dark', 0.85, 0.35),
    'MAT_Props_Small_Atlas': ('TEX_MAT_Props_Small_Atlas', 0.0, 0.7),
    'MAT_Props_Small_Atlas_Rose': ('TEX_MAT_Props_Small_Atlas', 0.0, 0.7),
}

# (blend相对路径, 导出fbx相对路径, 立方体UV边长, Atlas格映射{对象名片段:(cx,cy)} 或 None)
JOBS = [
    ('source/blender/environment/ENV_RoomShell_v001.blend', 'exports/environment/ENV_RoomShell.fbx', 4.0, None),
    ('source/blender/environment/ENV_DoorSet_v001.blend', 'exports/environment/ENV_DoorSet.fbx', 2.0, None),
    ('source/blender/environment/ENV_BlackCorridor_v001.blend', 'exports/environment/ENV_BlackCorridor.fbx', 2.0, None),
    ('source/blender/environment/ENV_BoardedWindow_v001.blend', 'exports/environment/ENV_BoardedWindow.fbx', 2.0, None),
    ('source/blender/environment/ENV_WallDamageKit_v001.blend', 'exports/environment/ENV_WallDamageKit.fbx', 1.0, None),
    ('source/blender/environment/FUR_Armchair_v001.blend', 'exports/environment/FUR_Armchair.fbx', 2.0, None),
    ('source/blender/environment/FUR_Bed_v001.blend', 'exports/environment/FUR_Bed.fbx', 2.0, None),
    ('source/blender/environment/FUR_Bookcase_v001.blend', 'exports/environment/FUR_Bookcase.fbx', 2.0, None),
    ('source/blender/environment/FUR_Dresser_v001.blend', 'exports/environment/FUR_Dresser.fbx', 2.0, None),
    ('source/blender/environment/FUR_SideTable_v001.blend', 'exports/environment/FUR_SideTable.fbx', 2.0, None),
    ('source/blender/environment/FUR_TVCabinet_v001.blend', 'exports/environment/FUR_TVCabinet.fbx', 2.0, None),
    ('source/blender/environment/FUR_WoodChair_v001.blend', 'exports/environment/FUR_WoodChair.fbx', 2.0, None),
    ('source/blender/environment/FUR_WoodChair_Broken_v001.blend', 'exports/environment/FUR_WoodChair_Broken.fbx', 2.0, None),
    ('source/blender/props/PRP_BaseballBat_v001.blend', 'exports/props/PRP_BaseballBat.fbx', 2.0, None),
    ('source/blender/props/PRP_Blanket_Crumpled_v001.blend', 'exports/props/PRP_Blanket_Crumpled.fbx', 2.0, None),
    ('source/blender/props/PRP_Blanket_Covered_v001.blend', 'exports/props/PRP_Blanket_Covered.fbx', 2.0, None),
    ('source/blender/props/PRP_Blanket_FlatTrap_v001.blend', 'exports/props/PRP_Blanket_FlatTrap.fbx', 2.0, None),
    ('source/blender/props/PRP_Blanket_Draped_v001.blend', 'exports/props/PRP_Blanket_Draped.fbx', 2.0, None),
    ('source/blender/props/PRP_CRTTV_Intact_v001.blend', 'exports/props/PRP_CRTTV_Intact.fbx', 2.0, None),
    ('source/blender/props/PRP_CRTTV_Cracked_v001.blend', 'exports/props/PRP_CRTTV_Cracked.fbx', 2.0, None),
    ('source/blender/props/PRP_CRTTV_Destroyed_v001.blend', 'exports/props/PRP_CRTTV_Destroyed.fbx', 2.0, None),
    ('source/blender/props/PRP_CeilingLamp_Intact_v001.blend', 'exports/props/PRP_CeilingLamp_Intact.fbx', 1.0, None),
    ('source/blender/props/PRP_CeilingLamp_Broken_v001.blend', 'exports/props/PRP_CeilingLamp_Broken.fbx', 1.0, None),
    ('source/blender/props/PRP_TableLamp_Intact_v001.blend', 'exports/props/PRP_TableLamp_Intact.fbx', 1.0, None),
    ('source/blender/props/PRP_TableLamp_Broken_v001.blend', 'exports/props/PRP_TableLamp_Broken.fbx', 1.0, None),
    ('source/blender/props/PRP_Pistol_Main_v001.blend', 'exports/props/PRP_Pistol_Main.fbx', 1.0, None),
    ('source/blender/props/PRP_Pistol_Backup_v001.blend', 'exports/props/PRP_Pistol_Backup.fbx', 1.0, None),
    ('source/blender/props/PRP_Flashlight_v001.blend', 'exports/props/PRP_Flashlight.fbx', 1.0, None),
    ('source/blender/props/PRP_Rug_v001.blend', 'exports/props/PRP_Rug.fbx', 4.0, None),
    ('source/blender/props/PRP_Mirror_v001.blend', 'exports/props/PRP_Mirror.fbx', 2.0, None),
    ('source/blender/props/PRP_Mirror_Broken_v001.blend', 'exports/props/PRP_Mirror_Broken.fbx', 2.0, None),
    ('source/blender/props/PRP_Vase_v001.blend', 'exports/props/PRP_Vase.fbx', 1.0,
     {'Vase_Roses': (2, 1)}),
    ('source/blender/props/PRP_Vase_Broken_v001.blend', 'exports/props/PRP_Vase_Broken.fbx', 1.0,
     {'Vase_Roses_Fallen': (2, 1)}),
    ('source/blender/props/PRP_Remote_v001.blend', 'exports/props/PRP_Remote.fbx', 1.0,
     {'': (0, 0)}),
    ('source/blender/props/PRP_Books_Set_v001.blend', 'exports/props/PRP_Books_Set.fbx', 1.0,
     {'Book_A': (1, 0), 'Book_B': (2, 0), 'Book_C': (3, 0),
      'Book_D': (0, 1), 'Book_E': (1, 1), 'Book_Open': (2, 2)}),
    ('source/blender/props/PRP_DrawerClutter_v001.blend', 'exports/props/PRP_DrawerClutter.fbx', 1.0,
     {'Clutter_Tape': (1, 2), 'Clutter_Newspaper': (0, 2), 'Clutter_Paper': (2, 2)}),
    ('source/blender/props/PRP_WallClock_v001.blend', 'exports/props/PRP_WallClock.fbx', 1.0,
     {'Clock_Body': (3, 1), 'Clock_Hand': (0, 0)}),
    ('source/blender/props/PRP_WallClock_Broken_v001.blend', 'exports/props/PRP_WallClock_Broken.fbx', 1.0,
     {'Clock_Body': (3, 1), 'Clock_Hand': (0, 0)}),
]


def load_img(name, srgb=True):
    path = os.path.join(TEXDIR, name + '.png')
    img = bpy.data.images.get(name) or bpy.data.images.load(path)
    img.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
    return img


def rebuild_material(mat):
    key = mat.name.split('.')[0]
    if key not in TEXMAP:
        return False
    tex, metal, rough = TEXMAP[key]
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    tbc = nt.nodes.new('ShaderNodeTexImage')
    tbc.image = load_img(tex + '_BC', True)
    nt.links.new(tbc.outputs['Color'], bsdf.inputs['Base Color'])
    n_path = os.path.join(TEXDIR, tex + '_N.png')
    if os.path.exists(n_path):
        tn = nt.nodes.new('ShaderNodeTexImage')
        tn.image = load_img(tex + '_N', False)
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    mask_path = os.path.join(TEXDIR, tex + '_Mask.png')
    if os.path.exists(mask_path):
        tm = nt.nodes.new('ShaderNodeTexImage')
        tm.image = load_img(tex + '_Mask', False)
        sep = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(tm.outputs['Color'], sep.inputs['Color'])
        nt.links.new(sep.outputs['Red'], bsdf.inputs['Metallic'])
        inv = nt.nodes.new('ShaderNodeMath')
        inv.operation = 'SUBTRACT'
        inv.inputs[0].default_value = 1.0
        nt.links.new(tm.outputs['Alpha'], inv.inputs[1])
        nt.links.new(inv.outputs[0], bsdf.inputs['Roughness'])
    bsdf.inputs['Metallic'].default_value = metal
    bsdf.inputs['Roughness'].default_value = rough
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    return True


def cube_uv(obj, size):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.cube_project(cube_size=size)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)


def uv_to_cell(obj, cell):
    """把当前 UV 整体缩进 Atlas 格 (内缩 0.02 防深 mip 渗色)"""
    cx, cy = cell
    uv = obj.data.uv_layers.active.data
    for d in uv:
        d.uv = (d.uv.x * 0.21 + cx * 0.25 + 0.02,
                d.uv.y * 0.21 + cy * 0.25 + 0.02)


def apply_job(rel, fbx_rel, cube_size, cells):
    path = os.path.join(ROOT, rel.replace('/', os.sep))
    bpy.ops.wm.open_mainfile(filepath=path)
    # 材质换纹理
    n_tex = 0
    for mat in bpy.data.materials:
        if rebuild_material(mat):
            n_tex += 1
    # UV
    for o in bpy.data.objects:
        if o.type != 'MESH':
            continue
        cube_uv(o, cube_size)
        if cells:
            for frag, cell in cells.items():
                if frag and frag in o.name:
                    uv_to_cell(o, cell)
                    break
            else:
                if '' in cells:
                    uv_to_cell(o, cells[''])
    # 存 v002 + 重导 FBX
    out_blend = path.replace('_v001.blend', '_v002.blend')
    bpy.ops.wm.save_as_mainfile(filepath=out_blend)
    export_fbx(os.path.join(ROOT, fbx_rel.replace('/', os.sep)))
    print('APPLIED %s (%d textured mats) -> %s' % (os.path.basename(path), n_tex, fbx_rel))


if __name__ == '__main__':
    for job in JOBS:
        apply_job(*job)
    print('B08_APPLY_DONE')
