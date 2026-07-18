# 列出所有 .blend 源文件中的材质名 (为 B08 贴图映射做准备)
import bpy, sys, os, json

ROOT = r'D:\game\con_game\art\source\blender'
found = {}
for sub in ('environment', 'props'):
    d = os.path.join(ROOT, sub)
    for f in sorted(os.listdir(d)):
        if not f.endswith('_v001.blend'):
            continue
        bpy.ops.wm.open_mainfile(filepath=os.path.join(d, f))
        mats = sorted({m.name.split('.')[0] for m in bpy.data.materials})
        found[f] = mats
print('MATLIST ' + json.dumps(found, ensure_ascii=False, indent=1))
