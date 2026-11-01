# 棍图预览: 每条新动画取 首/中/末 三帧画骨骼棍图, 快速目检姿态质量
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
ANIM_DIR = ROOT / "exports" / "animations"
OUT_DIR = ROOT / "previews" / "animations"
OUT_DIR.mkdir(parents=True, exist_ok=True)

CLIPS = [
    "AN_Player_Pickup", "AN_Player_Throw", "AN_Player_HitFall",
    "AN_Killer_DodgeSide", "AN_Killer_KickDoor", "AN_Killer_Stumble",
    "AN_Killer_Fall", "AN_Killer_BlockLight", "AN_Killer_DrawBackupFire",
    "POSE_Player_SitChair", "POSE_Player_CrouchHide", "POSE_Player_Prone",
    "POSE_Player_KneelHandsUp", "POSE_Player_HoldSmall_R", "POSE_Player_HoldBat",
    "POSE_Player_CarryLarge", "POSE_Player_AimPistol",
    "POSE_Killer_LowReady", "POSE_Killer_Kneel",
    "PAIR_D08_PlayerSide", "PAIR_D08_KillerSide",
    "PAIR_D11_PlayerSide", "PAIR_D11_KillerSide",
    "PAIR_D21_PlayerSide", "PAIR_D21_KillerSide",
]

# 只画主干骨, 避免手指骨密集成团
KEEP = ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
        "Shoulder", "Arm", "ForeArm", "Hand",
        "UpLeg", "Leg", "Foot", "ToeBase")


def stick_mesh(name, segments):
    """segments: [(p1, p2)] 圆棍合并网格"""
    verts, faces = [], []
    sides = 6
    for p1, p2 in segments:
        p1, p2 = Vector(p1), Vector(p2)
        d = p2 - p1
        if d.length < 1e-4:
            continue
        q = d.to_track_quat('Z', 'Y')
        base = len(verts)
        r = max(0.005, min(d.length * 0.06, 0.02))
        for i in range(sides):
            a = 2 * math.pi * i / sides
            off = q @ Vector((r * math.cos(a), r * math.sin(a), 0))
            verts.append(tuple(p1 + off))
            verts.append(tuple(p2 + off))
        for i in range(sides):
            i2 = (i + 1) % sides
            a0, a1 = base + i * 2, base + i * 2 + 1
            b0, b1 = base + i2 * 2, base + i2 * 2 + 1
            faces.append((a0, b0, b1, a1))
    me = bpy.data.meshes.new(name + "_mesh")
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def render_clip(clip):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ANIM_DIR / f"{clip}.glb"))
    arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
    if not arm:
        print(f"SKIP {clip}: no armature")
        return
    act = arm.animation_data.action if arm.animation_data else None
    if not act:
        print(f"SKIP {clip}: no action")
        return
    f0, f1 = act.frame_range
    samples = [f0, (f0 + f1) / 2, f1]

    # 骨架世界高度, 用于机位与帧间距
    bpy.context.scene.frame_set(int(f0))
    heads = [arm.matrix_world @ p.head for p in arm.pose.bones]
    h = max(p.z for p in heads) - min(p.z for p in heads)
    spacing = h * 0.9

    all_pts = []
    for i, f in enumerate(samples):
        bpy.context.scene.frame_set(int(f))
        bpy.context.view_layer.update()
        segs = []
        off = Vector(((i - 1) * spacing, 0, 0))
        for pb in arm.pose.bones:
            if not any(k in pb.name for k in KEEP):
                continue
            w1 = arm.matrix_world @ pb.head + off
            w2 = arm.matrix_world @ pb.tail + off
            segs.append((w1, w2))
            all_pts += [w1, w2]
        stick_mesh(f"{clip}_f{i}", segs)

    # 相机: 按棍图实际包围盒取景, 正面 3/4 视角
    xs = [p.x for p in all_pts]; ys = [p.y for p in all_pts]; zs = [p.z for p in all_pts]
    mid = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2))
    span = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    dist = span * 1.1
    a = math.radians(160)
    cam.location = mid + Vector((dist * math.sin(a), dist * math.cos(a), span * 0.25))
    d = mid - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam_data.lens = 40
    cam_data.clip_end = max(100.0, span * 10)

    sc = bpy.context.scene
    sc.camera = cam
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'SINGLE'
    sc.display.shading.single_color = (0.15, 0.35, 0.6)
    sc.render.resolution_x = 640
    sc.render.resolution_y = 480
    sc.render.film_transparent = False
    sc.world = bpy.data.worlds.new("w") if not sc.world else sc.world
    sc.render.filepath = str(OUT_DIR / f"{clip}.png")
    bpy.ops.render.render(write_still=True)
    print(f"OK {clip}")


for clip in CLIPS:
    try:
        render_clip(clip)
    except Exception as err:
        print(f"FAIL {clip}: {err}")
print("done")
