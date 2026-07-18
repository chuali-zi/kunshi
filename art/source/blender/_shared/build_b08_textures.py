# B08-1 贴图烘焙: 程序化生成 BC/N/MRA (Cycles CPU, 节点噪声)
# 输出: art/exports/textures/TEX_<材质名>_{BC,N,MRA}.png (§1.5 命名)
import bpy, sys, os, math
import numpy as np

ROOT = r'D:\game\con_game\art'
TEX = os.path.join(ROOT, 'exports/textures')
os.makedirs(TEX, exist_ok=True)

sc = bpy.context.scene
sc.render.engine = 'CYCLES'
sc.cycles.samples = 8
sc.cycles.use_denoising = False
sc.render.image_settings.file_format = 'PNG'

# 烘焙平面
bpy.ops.mesh.primitive_plane_add(size=2)
plane = bpy.context.active_object


def new_img(name, size):
    img = bpy.data.images.get(name)
    if img: bpy.data.images.remove(img)
    img = bpy.data.images.new(name, size, size, alpha=False)
    img.colorspace_settings.name = 'sRGB'
    return img


def bake_mat(name, size, build_nodes, bake_normal=True):
    """build_nodes(nodes, links, bsdf) 搭图案; 先烘 BC(Diffuse COLOR) 再烘 N"""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    build_nodes(nodes, links, bsdf)
    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    plane.data.materials.clear()
    plane.data.materials.append(mat)

    img_bc = new_img(name + '_BC', size)
    nodes_img = nodes.new('ShaderNodeTexImage')
    nodes_img.image = img_bc
    nt.nodes.active = nodes_img
    sc.render.bake.use_clear = True
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, margin=8)
    img_bc.filepath_raw = os.path.join(TEX, name + '_BC.png')
    img_bc.save()
    nodes.remove(nodes_img)

    if bake_normal:
        img_n = new_img(name + '_N', size)
        img_n.colorspace_settings.name = 'Non-Color'
        nodes_img = nodes.new('ShaderNodeTexImage')
        nodes_img.image = img_n
        nt.nodes.active = nodes_img
        bpy.ops.object.bake(type='NORMAL', margin=8, normal_space='TANGENT')
        img_n.filepath_raw = os.path.join(TEX, name + '_N.png')
        img_n.save()
        nodes.remove(nodes_img)
    bpy.data.materials.remove(mat)


def mra(name, metallic, rough, ao=1.0, size=64):
    img = bpy.data.images.new(name + '_MRA', size, size, alpha=False)
    arr = np.zeros((size * size * 4,), dtype=np.float32)
    arr[0::4] = metallic
    arr[1::4] = rough
    arr[2::4] = ao
    arr[3::4] = 1.0
    img.pixels.foreach_set(arr)
    img.colorspace_settings.name = 'Non-Color'
    img.filepath_raw = os.path.join(TEX, name + '_MRA.png')
    img.save()
    bpy.data.images.remove(img)


def noise(nodes, scale, detail=4.0, rough=0.6):
    n = nodes.new('ShaderNodeTexNoise')
    n.inputs['Scale'].default_value = scale
    n.inputs['Detail'].default_value = detail
    n.inputs['Roughness'].default_value = rough
    return n


def ramp(nodes, stops):
    r = nodes.new('ShaderNodeValToRGB')
    cr = r.color_ramp
    cr.elements.remove(cr.elements[1])
    cr.elements[0].position = stops[0][0]
    cr.elements[0].color = (*stops[0][1], 1)
    for pos, col in stops[1:]:
        e = cr.elements.new(pos)
        e.color = (*col, 1)
    return r


def texcoord(nodes, links, dest_inputs):
    tc = nodes.new('ShaderNodeTexCoord')
    for d in dest_inputs:
        links.new(tc.outputs['Generated'], d)


# ---------------------------------------------------------------- 各材质图案
def wallpaper(nodes, links, bsdf):
    """米白底小粉玫瑰碎花 + 竖向旧痕 + 轻微起皮"""
    tc = nodes.new('ShaderNodeTexCoord')
    # 碎花点: voronoi 到点距离 → 小圆点 (玫瑰花)
    vor = nodes.new('ShaderNodeTexVoronoi')
    vor.feature = 'F1'
    vor.inputs['Scale'].default_value = 34.0
    links.new(tc.outputs['Generated'], vor.inputs['Vector'])
    dots = ramp(nodes, [(0.10, (0.70, 0.38, 0.42)), (0.16, (0.88, 0.84, 0.76))])  # 花粉→米白
    links.new(vor.outputs['Distance'], dots.inputs['Fac'])
    # 第二遍错位的绿叶小点
    vor2 = nodes.new('ShaderNodeTexVoronoi')
    vor2.feature = 'F1'
    vor2.inputs['Scale'].default_value = 37.0
    mapping_off = nodes.new('ShaderNodeMapping')
    mapping_off.inputs['Location'].default_value = (0.37, 0.41, 0.0)
    links.new(tc.outputs['Generated'], mapping_off.inputs['Vector'])
    links.new(mapping_off.outputs['Vector'], vor2.inputs['Vector'])
    leaves = ramp(nodes, [(0.07, (0.48, 0.55, 0.40)), (0.12, (1, 1, 1))])
    links.new(vor2.outputs['Distance'], leaves.inputs['Fac'])
    mix1 = nodes.new('ShaderNodeMixRGB')
    mix1.blend_type = 'MULTIPLY'
    links.new(dots.outputs['Color'], mix1.inputs[1])
    links.new(leaves.outputs['Color'], mix1.inputs[2])
    # 竖向旧痕 (低频波带 X)
    wave = nodes.new('ShaderNodeTexWave')
    wave.wave_type = 'BANDS'
    wave.bands_direction = 'X'
    wave.inputs['Scale'].default_value = 6.0
    wave.inputs['Distortion'].default_value = 3.0
    wave.inputs['Detail'].default_value = 3.0
    links.new(tc.outputs['Generated'], wave.inputs['Vector'])
    age = ramp(nodes, [(0.35, (0.86, 0.82, 0.74)), (0.65, (0.98, 0.94, 0.86))])
    links.new(wave.outputs['Color'], age.inputs['Fac'])
    mix2 = nodes.new('ShaderNodeMixRGB')
    mix2.blend_type = 'MULTIPLY'
    mix2.inputs[0].default_value = 0.18
    links.new(mix1.outputs['Color'], mix2.inputs[1])
    links.new(age.outputs['Color'], mix2.inputs[2])
    links.new(mix2.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.9
    # 起皮凹凸
    n = noise(nodes, 90, 2)
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.25
    bump.inputs['Distance'].default_value = 0.002
    links.new(tc.outputs['Generated'], n.inputs['Vector'])
    links.new(n.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])


def floor_green(nodes, links, bsdf):
    tc = nodes.new('ShaderNodeTexCoord')
    n1 = noise(nodes, 5, 3)
    links.new(tc.outputs['Generated'], n1.inputs['Vector'])
    c = ramp(nodes, [(0.3, (0.10, 0.15, 0.11)), (0.7, (0.16, 0.23, 0.16))])
    links.new(n1.outputs['Fac'], c.inputs['Fac'])
    links.new(c.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.95
    n2 = noise(nodes, 120, 2)
    links.new(tc.outputs['Generated'], n2.inputs['Vector'])
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.3
    bump.inputs['Distance'].default_value = 0.0015
    links.new(n2.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])


def wood(base_dark, base_light, rough):
    def fn(nodes, links, bsdf):
        tc = nodes.new('ShaderNodeTexCoord')
        wave = nodes.new('ShaderNodeTexWave')
        wave.wave_type = 'BANDS'
        wave.bands_direction = 'X'
        wave.inputs['Scale'].default_value = 7.0
        wave.inputs['Distortion'].default_value = 6.0
        wave.inputs['Detail'].default_value = 5.0
        links.new(tc.outputs['Generated'], wave.inputs['Vector'])
        c = ramp(nodes, [(0.3, base_dark), (0.7, base_light)])
        links.new(wave.outputs['Color'], c.inputs['Fac'])
        links.new(c.outputs['Color'], bsdf.inputs['Base Color'])
        bsdf.inputs['Roughness'].default_value = rough
        bump = nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.35
        bump.inputs['Distance'].default_value = 0.003
        links.new(wave.outputs['Color'], bump.inputs['Height'])
        links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return fn


def leather(nodes, links, bsdf):
    tc = nodes.new('ShaderNodeTexCoord')
    vor = nodes.new('ShaderNodeTexVoronoi')
    vor.inputs['Scale'].default_value = 55.0
    links.new(tc.outputs['Generated'], vor.inputs['Vector'])
    c = ramp(nodes, [(0.25, (0.24, 0.05, 0.07)), (0.75, (0.38, 0.10, 0.12))])
    links.new(vor.outputs['Distance'], c.inputs['Fac'])
    links.new(c.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.55
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.4
    bump.inputs['Distance'].default_value = 0.002
    links.new(vor.outputs['Distance'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])


def fabric(nodes, links, bsdf):
    tc = nodes.new('ShaderNodeTexCoord')
    n1 = noise(nodes, 25, 3)
    links.new(tc.outputs['Generated'], n1.inputs['Vector'])
    c = ramp(nodes, [(0.35, (0.80, 0.78, 0.72)), (0.65, (0.90, 0.88, 0.83))])
    links.new(n1.outputs['Fac'], c.inputs['Fac'])
    links.new(c.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.85
    n2 = noise(nodes, 200, 2)
    links.new(tc.outputs['Generated'], n2.inputs['Vector'])
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.2
    bump.inputs['Distance'].default_value = 0.001
    links.new(n2.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])


def metal(nodes, links, bsdf):
    tc = nodes.new('ShaderNodeTexCoord')
    n1 = noise(nodes, 60, 2)
    links.new(tc.outputs['Generated'], n1.inputs['Vector'])
    c = ramp(nodes, [(0.3, (0.035, 0.035, 0.04)), (0.7, (0.07, 0.07, 0.075))])
    links.new(n1.outputs['Fac'], c.inputs['Fac'])
    links.new(c.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Metallic'].default_value = 0.85
    bsdf.inputs['Roughness'].default_value = 0.45
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.15
    bump.inputs['Distance'].default_value = 0.001
    links.new(n1.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])


def props_atlas(nodes, links, bsdf):
    """2K 小道具 Atlas: 4×4 格 (每格 512px), UV 留白 20% (右列+上行留空)
    格位: (col,row) 0~3, 原点左下
      (0,0)遥控深灰塑料 (1,0)书封A酒红 (2,0)书封B墨绿 (3,0)书封C藏蓝
      (0,1)书封D褐 (1,1)书封E灰绿 (2,1)玫瑰红 (3,1)钟面米白
      (0,2)报纸灰 (1,2)胶带黄褐 (2,2)空白纸白  其余留空备用
    """
    tc = nodes.new('ShaderNodeTexCoord')
    sep = nodes.new('ShaderNodeSeparateXYZ')
    links.new(tc.outputs['Generated'], sep.inputs[0])
    cells = {
        (0, 0): (0.16, 0.16, 0.18), (1, 0): (0.35, 0.10, 0.12),
        (2, 0): (0.12, 0.20, 0.16), (3, 0): (0.10, 0.14, 0.26),
        (0, 1): (0.28, 0.18, 0.10), (1, 1): (0.30, 0.32, 0.28),
        (2, 1): (0.55, 0.05, 0.06), (3, 1): (0.85, 0.82, 0.72),
        (0, 2): (0.55, 0.54, 0.50), (1, 2): (0.48, 0.38, 0.22),
        (2, 2): (0.88, 0.87, 0.84),
    }
    # 逐格比较: x∈[c/4,(c+1)/4), y∈[r/4,(r+1)/4) → 取该格色, 否则米灰底
    col = None
    last = None
    base = nodes.new('ShaderNodeRGB')
    base.outputs[0].default_value = (0.5, 0.48, 0.45, 1)
    last = base.outputs[0]
    for (cx, cy), rgb in cells.items():
        for axis, idx in ((sep.outputs[0], cx), (sep.outputs[1], cy)):
            lo = nodes.new('ShaderNodeMath'); lo.operation = 'GREATER_THAN'
            lo.inputs[1].default_value = idx / 4.0
            links.new(axis, lo.inputs[0])
            hi = nodes.new('ShaderNodeMath'); hi.operation = 'LESS_THAN'
            hi.inputs[1].default_value = (idx + 1) / 4.0
            links.new(axis, hi.inputs[0])
            if axis == sep.outputs[0]:
                lo_x, hi_x = lo, hi
            else:
                lo_y, hi_y = lo, hi
        a1 = nodes.new('ShaderNodeMath'); a1.operation = 'MULTIPLY'
        links.new(lo_x.outputs[0], a1.inputs[0]); links.new(hi_x.outputs[0], a1.inputs[1])
        a2 = nodes.new('ShaderNodeMath'); a2.operation = 'MULTIPLY'
        links.new(lo_y.outputs[0], a2.inputs[0]); links.new(hi_y.outputs[0], a2.inputs[1])
        both = nodes.new('ShaderNodeMath'); both.operation = 'MULTIPLY'
        links.new(a1.outputs[0], both.inputs[0]); links.new(a2.outputs[0], both.inputs[1])
        mix = nodes.new('ShaderNodeMixRGB')
        rgbn = nodes.new('ShaderNodeRGB')
        rgbn.outputs[0].default_value = (*rgb, 1)
        links.new(both.outputs[0], mix.inputs[0])
        links.new(last, mix.inputs[1])
        links.new(rgbn.outputs[0], mix.inputs[2])
        last = mix.outputs['Color']
    # 格内细噪
    n1 = noise(nodes, 150, 2)
    links.new(tc.outputs['Generated'], n1.inputs['Vector'])
    grain = ramp(nodes, [(0.4, (0.92, 0.92, 0.92)), (0.6, (1.0, 1.0, 1.0))])
    links.new(n1.outputs['Fac'], grain.inputs['Fac'])
    mixg = nodes.new('ShaderNodeMixRGB')
    mixg.blend_type = 'MULTIPLY'
    links.new(last, mixg.inputs[1])
    links.new(grain.outputs['Color'], mixg.inputs[2])
    links.new(mixg.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.7


JOBS = [
    ('TEX_MAT_Wallpaper_FloralAged', 2048, wallpaper, True, (0.0, 0.9)),
    ('TEX_MAT_Floor_DarkGreen', 1024, floor_green, True, (0.0, 0.95)),
    ('TEX_MAT_Wood_Walnut', 1024, wood((0.10, 0.06, 0.035), (0.20, 0.13, 0.08), 0.5), True, (0.0, 0.5)),
    ('TEX_MAT_Wood_Worn', 1024, wood((0.30, 0.20, 0.11), (0.45, 0.33, 0.20), 0.6), True, (0.0, 0.6)),
    ('TEX_MAT_Leather_Burgundy', 1024, leather, True, (0.0, 0.55)),
    ('TEX_MAT_Fabric_Bedding', 1024, fabric, True, (0.0, 0.85)),
    ('TEX_MAT_Metal_Dark', 1024, metal, True, (0.85, 0.45)),
    ('TEX_MAT_Props_Small_Atlas', 2048, props_atlas, False, (0.0, 0.7)),
]

for name, size, fn, has_n, (met, rgh) in JOBS:
    bake_mat(name, size, fn, bake_normal=has_n)
    mra(name, met, rgh)
    print('BAKED ' + name)

print('B08_TEXTURES_DONE')
