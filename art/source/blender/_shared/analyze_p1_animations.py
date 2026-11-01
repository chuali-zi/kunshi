# 数值化校验新动画: 首/中/末帧关键关节世界位置(髋/头/双手/双脚高度与前后)
import json
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[3]
ANIM_DIR = ROOT / "exports" / "animations"

CLIPS = {
    "AN_Player_Pickup": 2.5, "AN_Player_Throw": 2.5,
    "AN_Killer_DodgeSide": 2.0, "AN_Killer_KickDoor": 2.5,
    "AN_Killer_Stumble": 3.0, "AN_Killer_Fall": 3.0,
    "AN_Killer_BlockLight": 3.0, "AN_Killer_DrawBackupFire": 4.0,
    "POSE_Player_SitChair": 2.0, "POSE_Player_CrouchHide": 2.0,
    "POSE_Player_Prone": 2.0, "POSE_Player_KneelHandsUp": 2.0,
    "POSE_Player_HoldSmall_R": 2.0, "POSE_Player_HoldBat": 2.0,
    "POSE_Player_CarryLarge": 2.0, "POSE_Player_AimPistol": 2.0,
    "POSE_Killer_LowReady": 2.0, "POSE_Killer_Kneel": 2.0,
    "PAIR_D08_PlayerSide": 6.0, "PAIR_D08_KillerSide": 6.0,
    "PAIR_D11_PlayerSide": 3.0, "PAIR_D11_KillerSide": 3.0,
    "PAIR_D21_PlayerSide": 8.0, "PAIR_D21_KillerSide": 8.0,
}

JOINTS = ["Hips", "Head", "LeftHand", "RightHand", "LeftFoot", "RightFoot"]


def find_bone(arm, key):
    for pb in arm.pose.bones:
        if pb.name.endswith(key) or pb.name.split(":")[-1] == key:
            return pb
    return None


for clip, dur in sorted(CLIPS.items()):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ANIM_DIR / f"{clip}.glb"))
    arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
    act = arm.animation_data.action if arm and arm.animation_data else None
    if not act:
        print(f"ANALYZE {json.dumps({'name': clip, 'ok': False, 'note': 'no action'})}")
        continue
    f0, f1 = act.frame_range
    frames = {"start": int(f0), "mid": int((f0 + f1) / 2), "end": int(f1)}
    out = {"name": clip, "ok": True, "frames": round(f1 - f0, 1), "poses": {}}
    for label, f in frames.items():
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        pose = {}
        for j in JOINTS:
            pb = find_bone(arm, j)
            if pb:
                w = arm.matrix_world @ pb.head
                pose[j] = [round(w.x, 3), round(w.y, 3), round(w.z, 3)]
        out["poses"][label] = pose
    print("ANALYZE " + json.dumps(out))
print("done")
