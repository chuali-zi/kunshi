# 轴向约定实证测试: 标记物 (东+X 放锥, 北+Y 放柱, 上+Z 放球)
# 用规格导出参数走 FBX 往返, 验证 Blender 方位 → FBX 方位映射
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
from ak import *

clean_scene()
# 标记: +X=红锥(东), +Y=绿柱(北), 原点黄球
mE = make_mat('M_E', (0.9, 0.1, 0.1))
mN = make_mat('M_N', (0.1, 0.8, 0.1))
mO = make_mat('M_O', (0.9, 0.8, 0.1))
bpy.ops.mesh.primitive_cone_add(radius1=0.2, depth=0.4, location=(1.0, 0, 0))
bpy.context.active_object.data.materials.append(mE)
bpy.ops.mesh.primitive_cylinder_add(radius=0.15, depth=0.6, location=(0, 1.0, 0))
bpy.context.active_object.data.materials.append(mN)
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.15, location=(0, 0, 0))
bpy.context.active_object.data.materials.append(mO)

tmp = r'D:\game\con_game\art\.log\_axis_test.fbx'
export_fbx(tmp)

# 重开并导入, 模拟外部 Y-up 读取方
clean_scene()
bpy.ops.import_scene.fbx(filepath=tmp)
for o in bpy.data.objects:
    if o.type == 'MESH':
        # 世界包围盒中心
        from mathutils import Vector
        pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
        c = sum(pts, Vector()) / 8
        print('AXIS_MARKER name=%s center=(%.3f, %.3f, %.3f)' % (o.name, c.x, c.y, c.z))
