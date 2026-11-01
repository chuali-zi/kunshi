# 校验 2026-08-06 新增动画 FBX: 骨架/动作/帧数/位移(In-Place)
import json
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
ANIM_DIR = ROOT / "exports" / "animations"

EXPECT = {
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

results = []
for name, dur in sorted(EXPECT.items()):
    fbx = ANIM_DIR / f"{name}.fbx"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx))
    arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    actions = list(bpy.data.actions)
    rec = {"name": name, "bones": 0, "frames": 0, "fps": 0.0,
           "ok": False, "note": ""}
    if not arms:
        rec["note"] = "no armature"
    elif not actions:
        rec["note"] = "no action"
    else:
        arm = arms[0]
        act = max(actions, key=lambda a: a.frame_range[1] - a.frame_range[0])
        f0, f1 = act.frame_range
        scene_fps = bpy.context.scene.render.fps
        frames = f1 - f0
        rec["bones"] = len(arm.data.bones)
        rec["frames"] = round(frames, 1)
        rec["fps"] = scene_fps
        expected_frames = dur * scene_fps
        if abs(frames - expected_frames) > expected_frames * 0.2:
            rec["note"] = f"frames {frames:.0f} != expect {expected_frames:.0f}"
        else:
            rec["ok"] = True
    results.append(rec)
    print("VERIFY " + json.dumps(rec))

bad = [r["name"] for r in results if not r["ok"]]
print(f"SUMMARY ok={len(results) - len(bad)}/{len(results)} bad={bad}")
