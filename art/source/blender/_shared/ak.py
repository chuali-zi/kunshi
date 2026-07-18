# 《困室》资产构建共享工具库 (Blender 5.1 headless)
# 规范来源: art/requirements/asset_spec_3d.md §1
#   - 1 BU = 1m, Z 轴向上, 物体正面朝 -Y
#   - 导出前 Apply All Transforms, Scale=(1,1,1)
#   - FBX: Forward=-Z, Up=Y, bake space transform
import bpy
import math
import os
import json
from math import sin, cos, pi
from mathutils import Vector, Matrix

# ---------------------------------------------------------------- 场景

def clean_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                  bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            if item.users == 0:
                block.remove(item)

# ---------------------------------------------------------------- 材质

def make_mat(name, color, metallic=0.0, rough=0.55, emission=None, estr=0.0):
    """按 §6/§7 材质组命名创建简化 PBR 材质 (P0 阶段允许简化, 贴图在 B08 补)"""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = (*color, 1.0)  # Workbench 预览取 viewport 色
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*color, 1.0)
    b.inputs['Metallic'].default_value = metallic
    b.inputs['Roughness'].default_value = rough
    if emission is not None:
        b.inputs['Emission Color'].default_value = (*emission, 1.0)
        b.inputs['Emission Strength'].default_value = estr
    return m

def assign(obj, mat):
    if obj.type == 'MESH':
        obj.data.materials.append(mat)

# ---------------------------------------------------------------- 基础件

def _finish(obj, name, loc, mat, parent):
    obj.name = name
    obj.location = loc
    if mat: assign(obj, mat)
    if parent: obj.parent = parent
    return obj

def apply_mod(obj, mod_name):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod_name)
    obj.select_set(False)

def bevel(obj, width=0.005, segments=3):
    m = obj.modifiers.new('Bevel', 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
    apply_mod(obj, m.name)

def apply_scale(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)

def box(name, sx, sy, sz, loc=(0,0,0), bev=0.0, mat=None, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    o = bpy.context.active_object
    o.scale = (sx, sy, sz)
    apply_scale(o)
    if bev > 0: bevel(o, bev)
    return _finish(o, name, loc, mat, parent)

def cyl(name, r, depth, loc=(0,0,0), rot=None, verts=24, bev=0.0,
        mat=None, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth)
    o = bpy.context.active_object
    if rot: o.rotation_euler = rot
    if bev > 0: bevel(o, bev)
    return _finish(o, name, loc, mat, parent)

def sphere(name, r, loc=(0,0,0), scale=(1,1,1), seg=24, rings=16,
           mat=None, parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=r)
    o = bpy.context.active_object
    o.scale = scale
    apply_scale(o)
    for p in o.data.polygons: p.use_smooth = True
    return _finish(o, name, loc, mat, parent)

def empty(name, loc=(0,0,0), parent=None, display='PLAIN', size=0.05):
    o = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    o.empty_display_type = display
    o.empty_display_size = size
    if parent: o.parent = parent
    return o

# ---------------------------------------------------------------- 旋成体

def lathe(name, profile, segments=24, axis='Z', mat=None, parent=None,
          smooth=True, cap=True):
    """profile: [(t, r), ...] 沿轴参数 t 与半径 r; axis='Y' 时长轴沿 Y (手持物)"""
    verts, faces = [], []
    n = len(profile)
    for i in range(segments):
        a = 2 * pi * i / segments
        ca, sa = cos(a), sin(a)
        for t, r in profile:
            if axis == 'Z':
                verts.append((r * ca, r * sa, t))
            else:  # 'Y'
                verts.append((r * ca, t, r * sa))
    for i in range(segments):
        i2 = (i + 1) % segments
        for j in range(n - 1):
            a = i * n + j
            b = i2 * n + j
            faces.append((a, b, b + 1, a + 1))
    if cap:
        for end, rev in ((0, True), (n - 1, False)):
            c = len(verts)
            t = profile[end][0]
            verts.append((0, t, 0) if axis == 'Y' else (0, 0, t))
            for i in range(segments):
                i2 = (i + 1) % segments
                f = (c, i2 * n + end, i * n + end) if rev else \
                    (c, i * n + end, i2 * n + end)
                faces.append(f)
    me = bpy.data.meshes.new(name + '_mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    if smooth:
        for p in me.polygons: p.use_smooth = True
    if mat: assign(o, mat)
    if parent: o.parent = parent
    return o

# ---------------------------------------------------------------- 网格面片(被子/碎布)

def grid_sheet(name, nx, ny, size_x, size_y, zfunc, mat=None, parent=None,
               skirt_z=None, smooth=True):
    """nx*ny 网格, z=zfunc(x,y) 位移; skirt_z: 边缘向下封口到该 z (底面开放)"""
    verts, faces = [], []
    for j in range(ny + 1):
        for i in range(nx + 1):
            x = (i / nx - 0.5) * size_x
            y = (j / ny - 0.5) * size_y
            verts.append((x, y, zfunc(x, y)))
    for j in range(ny):
        for i in range(nx):
            a = j * (nx + 1) + i
            faces.append((a, a + 1, a + 1 + nx + 1, a + nx + 1))
    if skirt_z is not None:
        # 边界点复制一圈并下压, 形成侧边
        boundary = []
        for i in range(nx + 1): boundary.append(i)
        for j in range(1, ny + 1): boundary.append(j * (nx + 1) + nx)
        for i in range(nx - 1, -1, -1): boundary.append(ny * (nx + 1) + i)
        for j in range(ny - 1, 0, -1): boundary.append(j * (nx + 1))
        base = len(verts)
        for idx in boundary:
            x, y, _ = verts[idx]
            verts.append((x, y, skirt_z))
        for k in range(len(boundary)):
            k2 = (k + 1) % len(boundary)
            a, b = boundary[k], boundary[k2]
            faces.append((a, base + k, base + k2, b))
    me = bpy.data.meshes.new(name + '_mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    if smooth:
        for p in me.polygons: p.use_smooth = True
    if mat: assign(o, mat)
    if parent: o.parent = parent
    return o

def join_into(objs, name, parent=None):
    """合并为单网格; 位置偏移烘进网格, 原点归世界原点(归组静态件用)"""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.data.transform(Matrix.Translation(o.location.copy()))
    o.location = (0, 0, 0)
    o.name = name
    if parent: o.parent = parent
    return o

def cube_uv(obj, cube_size=2.0):
    """立方体投影 UV (墙面接受 Decal 需要平直 UV); texel 密度 B08 再校"""
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.cube_project(cube_size=cube_size)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)

# ---------------------------------------------------------------- 检查与交付

def tri_count(obj):
    if obj.type != 'MESH': return 0
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)

def report(asset_id, root, budget, expect_dims):
    """打印并返回自检数据: 三角面/包围盒尺寸/原点"""
    tris = {}
    total = 0
    members = [root] + list(root.children_recursive)
    for o in members:
        if o.type == 'MESH':
            t = tri_count(o)
            tris[o.name] = t
            total += t
    pts = []
    for o in members:
        if o.type == 'MESH':
            pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    dims = {}
    if pts:
        xs = [p.x for p in pts]; ys = [p.y for p in pts]; zs = [p.z for p in pts]
        dims = {'x': round(max(xs) - min(xs), 4), 'y': round(max(ys) - min(ys), 4),
                'z': round(max(zs) - min(zs), 4),
                'z_min': round(min(zs), 4), 'z_max': round(max(zs), 4)}
    ok_tris = total <= budget
    tol_ok = True
    for k, v in expect_dims.items():
        if k in dims and abs(dims[k] - v) / v > 0.05:
            tol_ok = False
    result = {'asset': asset_id, 'tris_total': total, 'budget': budget,
              'tris_ok': ok_tris, 'dims': dims, 'expect': expect_dims,
              'dims_ok': tol_ok, 'parts': tris}
    print('SELF_CHECK ' + json.dumps(result))
    return result

def save_blend(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=path)

def export_fbx(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        object_types={'EMPTY', 'MESH'},
        bake_space_transform=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_scale_options='FBX_SCALE_NONE',
        use_mesh_modifiers=True,
        add_leaf_bones=False,
    )

# ---------------------------------------------------------------- 预览

def preview(path, target=(0, 0, 0.3), dist=2.2, angle=35, height=0.55):
    """Workbench 预览渲染: 相机在正面(-Y)偏右俯视"""
    a = math.radians(angle)
    cam_data = bpy.data.cameras.new('PreviewCam')
    cam = bpy.data.objects.new('PreviewCam', cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cx = dist * sin(a)
    cy = -dist * cos(a)
    cam.location = (cx, cy, target[2] + dist * height)
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam_data.lens = 50
    sc = bpy.context.scene
    sc.camera = cam
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.show_shadows = True
    sc.display.shading.show_cavity = True
    sc.display.shading.cavity_type = 'WORLD'
    sc.display.shading.color_type = 'MATERIAL'
    sc.render.resolution_x = 720
    sc.render.resolution_y = 720
    sc.render.film_transparent = True
    sc.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
