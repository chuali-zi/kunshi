# B07 墙面破损件 — asset_spec_3d.md §4 ENV_WallDamageKit
# 三档剥落 Decal 平面(贴图 B08 补) + 5 块微凸墙皮碎屑(每块 ≤50 面)
import sys, os, math, random
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
from ak import *

ROOT = r'D:\game\con_game\art'
ENV_DIR = os.path.join(ROOT, 'source/blender/environment')

clean_scene()
mat = make_mat('MAT_Wallpaper_Damage', (0.45, 0.40, 0.33), rough=0.9)
root = empty('ENV_WallDamageKit', display='CUBE', size=0.05)

# 三档剥落 Decal 平面: 0.15 / 0.35 / 0.6m, 法线贴图 B08 补; 面朝 -Y(贴墙凸向房内)
for i, size in enumerate((0.15, 0.35, 0.60)):
    random.seed(40 + i)
    # 不规则多边形轮廓 (8~10 点), 避免正圆假感
    n = 8 + i
    pts = []
    for k in range(n):
        a = k / n * 2 * math.pi
        r = size / 2 * random.uniform(0.65, 1.0)
        pts.append((r * math.cos(a), 0, r * math.sin(a)))
    me = bpy.data.meshes.new('peel_mesh')
    me.from_pydata(pts, [], [list(range(n))])
    o = bpy.data.objects.new('WallDamage_Decal_%d' % (i + 1), me)
    bpy.context.scene.collection.objects.link(o)
    o.location = (i * 0.8 - 0.8, 0, 0.5)
    assign(o, mat)
    o.parent = root

# 5 块微凸翘皮碎屑: 弯折小片 (每块 ≤50 面), 闭合凸包
random.seed(7)
for i in range(5):
    w = random.uniform(0.04, 0.09)
    h = random.uniform(0.04, 0.10)
    t = random.uniform(0.004, 0.010)
    bend = random.uniform(8, 20)
    parts = []
    p1 = box('flake', w, t, h / 2, loc=(0, 0, h / 4), bev=0.0)
    p2 = box('flake', w, t, h / 2, loc=(0, -t * 0.8, h * 0.72), bev=0.0)
    p2.rotation_euler = (math.radians(-bend), 0, 0)
    bpy.context.view_layer.objects.active = p2
    p2.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    p2.select_set(False)
    parts += [p1, p2]
    f = join_into(parts, 'WallDamage_Flake_%02d' % (i + 1), root)
    assign(f, mat)
    f.location = (i * 0.25 - 0.5, 0, 1.2)

save_blend(os.path.join(ENV_DIR, 'ENV_WallDamageKit_v001.blend'))
export_fbx(os.path.join(ROOT, 'exports/environment/ENV_WallDamageKit.fbx'))
report('ENV_WallDamageKit', root, 1500, {})
preview(os.path.join(ROOT, 'previews/environment/ENV_WallDamageKit.png'),
        target=(0, 0, 0.8), dist=1.6, angle=20, height=0.25)
print('B07_DONE')
