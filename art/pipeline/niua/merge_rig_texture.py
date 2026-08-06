"""把 NIUA rig 产物(骨架+权重,无贴图)与原始 mesh(带贴图,无骨架)合并。

原理:两份网格几何几乎一致(rig 服务只是重焊了顶点),用 Data Transfer
按最近点把蒙皮权重传到带贴图网格上,再绑到骨架,导出 FBX(嵌入贴图)。

用法:
  blender -b -P merge_rig_texture.py
输出:art/exports/characters/CH_*_rigged.fbx(覆盖) + 校验预览渲染
"""

import math
from pathlib import Path

import bpy
import mathutils

ROOT = Path(__file__).resolve().parents[2]
CHAR_DIR = ROOT / "exports" / "characters"
PREVIEW_DIR = ROOT / "previews" / "niua"


def import_glb(path: Path) -> list[bpy.types.Object]:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    return [o for o in bpy.data.objects if o not in before]


def rename_bones_mixamo(armature: bpy.types.Object) -> None:
    """按拓扑把 raw rig 的 bone_N 重命名为 Mixamo 标准名。

    结构假设(NIUA rig 实测):根骨=髋,髋下一条向上的脊椎链(→颈→头,
    胸口分出双肩臂手,每只手两条手指链)和两条向下的腿链。
    左右按骨骼头部 X 坐标判定(模型面朝 +Y 时,角色左侧在 -X)。
    """
    bones = armature.data.bones
    roots = [b for b in bones if b.parent is None]
    hips = roots[0]

    def side(bone):  # 角色自身的左右
        return "Left" if bone.head_local.x < 0 else "Right"

    hips.name = "mixamorig:Hips"

    # 判定:链末端 Z 高于髋 → 脊椎;低于髋 → 腿
    spine = None
    legs = []
    for child in hips.children:
        chain_end = child
        while chain_end.children:
            chain_end = chain_end.children[0]
        if chain_end.head_local.z > hips.head_local.z:
            spine = child
        else:
            legs.append(child)

    for leg in legs:
        s = side(leg)
        names = [f"mixamorig:{s}UpLeg", f"mixamorig:{s}Leg", f"mixamorig:{s}Foot", f"mixamorig:{s}ToeBase"]
        b = leg
        for n in names:
            b.name = n
            b = b.children[0] if b.children else None
            if b is None:
                break

    # 脊椎:直到分叉点(颈+双肩)都是 Spine/Spine1/Spine2...
    chain = []
    b = spine
    while b is not None and len(b.children) <= 1:
        chain.append(b)
        b = b.children[0] if b.children else None
    branch = b  # 分叉点(胸口)
    spine_names = ["mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2"]
    for i, sb in enumerate(chain):
        sb.name = spine_names[i] if i < 3 else f"mixamorig:Spine{i}"
    if branch is not None:
        branch.name = spine_names[len(chain)] if len(chain) < 3 else "mixamorig:Spine2"

        # 肩骨根部贴近脊椎,不能用根部 x 判定;用链末端:最高的是颈(头顶),
        # 其余两条是手臂,左右按末端(手)的 x 判定
        def chain_end(bone):
            b2 = bone
            while b2.children:
                b2 = b2.children[0]
            return b2

        kids = sorted(branch.children, key=lambda c: chain_end(c).head_local.z, reverse=True)
        neck, arms = kids[0], kids[1:]
        neck.name = "mixamorig:Neck"
        if neck.children:
            neck.children[0].name = "mixamorig:Head"
        for arm in arms:
            s = "Left" if chain_end(arm).head_local.x < 0 else "Right"
            names = [f"mixamorig:{s}Shoulder", f"mixamorig:{s}Arm", f"mixamorig:{s}ForeArm", f"mixamorig:{s}Hand"]
            b = arm
            for i, n in enumerate(names):
                b.name = n
                if i < len(names) - 1:
                    b = b.children[0] if b.children else None
                    if b is None:
                        break
            if b is not None and b.name.endswith("Hand"):
                fingers = sorted(b.children, key=lambda f: f.head_local.y)
                finger_names = ["Thumb", "Index"]
                for fi, chain_root in enumerate(fingers[:2]):
                    fb = chain_root
                    for seg in range(1, 4):
                        fb.name = f"mixamorig:{s}Hand{finger_names[fi]}{seg}"
                        fb = fb.children[0] if fb.children else None
                        if fb is None:
                            break


def merge(char: str) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)

    rig_objs = import_glb(CHAR_DIR / f"{char}_rigged.glb")
    armature = next(o for o in rig_objs if o.type == "ARMATURE")
    gray_mesh = next(o for o in rig_objs if o.type == "MESH")

    tex_objs = import_glb(CHAR_DIR / f"{char}.glb")
    tex_mesh = next(o for o in tex_objs if o.type == "MESH")

    # 传权重:最近点插值 + 生成同名顶点组
    bpy.context.view_layer.objects.active = tex_mesh
    mod = tex_mesh.modifiers.new("weights", "DATA_TRANSFER")
    mod.object = gray_mesh
    mod.use_vert_data = True
    mod.data_types_verts = {"VGROUP_WEIGHTS"}
    mod.vert_mapping = "NEAREST"
    bpy.ops.object.datalayout_transfer(modifier=mod.name)
    bpy.ops.object.modifier_apply(modifier=mod.name)

    # 绑到骨架
    arm_mod = tex_mesh.modifiers.new("Armature", "ARMATURE")
    arm_mod.object = armature
    tex_mesh.parent = armature

    # 清掉灰模和多余对象
    for obj in rig_objs:
        if obj.type == "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)
    for obj in tex_objs:
        if obj != tex_mesh and obj.type != "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)

    # glTF 导入后模型面朝 +Y;项目规范正面朝 -Y(对应 Unity root +Z 为视觉正面),
    # 旋转 180° 由 FBX 节点携带
    armature.rotation_euler.z += math.pi

    rename_bones_mixamo(armature)

    # 贴图落盘成带名字的 PNG(内嵌导出会变成无扩展名的 Image_0,Unity 不认),
    # FBX 引用外部文件,path_mode=COPY 把 PNG 复制到 FBX 旁边
    for i, img in enumerate(list(bpy.data.images)):
        if img.size[0] == 0 or img.name in ("Render Result", "Viewer Node"):
            continue
        img.name = f"TEX_{char}_{i}"
        img.filepath_raw = str(CHAR_DIR / f"TEX_{char}_{i}.png")
        img.file_format = "PNG"
        img.save()

    out = CHAR_DIR / f"{char}_rigged.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(out),
        path_mode="COPY",
        embed_textures=False,
        add_leaf_bones=False,
        bake_anim=False,
    )
    print(f"[merged] {out.name}")

    # 校验预览(正面,带贴图)
    mins = mathutils.Vector((1e9,) * 3)
    maxs = mathutils.Vector((-1e9,) * 3)
    for v in tex_mesh.bound_box:
        w = tex_mesh.matrix_world @ mathutils.Vector(v)
        mins = mathutils.Vector(map(min, mins, w))
        maxs = mathutils.Vector(map(max, maxs, w))
    center = (mins + maxs) / 2
    height = maxs.z - mins.z

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    bpy.context.collection.objects.link(cam)
    cam.location = (center.x, center.y - height * 1.9, center.z + height * 0.05)
    cam.rotation_euler = (math.radians(87), 0, 0)
    bpy.context.scene.camera = cam
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 4
    sun.rotation_euler = (math.radians(50), math.radians(20), math.radians(150))
    bpy.context.collection.objects.link(sun)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 540
    sc.render.resolution_y = 960
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    sc.render.filepath = str(PREVIEW_DIR / f"{char}_front.png")
    bpy.ops.render.render(write_still=True)
    print(f"[preview] {sc.render.filepath}")


for char in ["CH_Player", "CH_Killer"]:
    merge(char)
print("done")
