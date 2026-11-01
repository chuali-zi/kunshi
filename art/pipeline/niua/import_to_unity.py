"""把 art/exports 新产物复制进 Unity Assets, 并按 P0 模板生成 .meta。

- 动画 FBX: 复制到 Assets/_Game/Art/Animations/, meta 模板取
  AN_Player_Idle(循环) / AN_Player_Swing(非循环); 替换 guid、clip 名、lastFrame。
  takeName/internalID 与模板一致(由 NIUA GLB 的 action 名决定, 见 P0 meta 规律)。
- 道具 FBX: 复制到 Assets/_Game/Art/Props/, meta 模板取 PRP_Pistol_Backup。

已存在且内容未变的文件自动跳过。Unity 首次打开工程时会按 meta 完成导入。

用法: python import_to_unity.py [--dry-run]
"""

from __future__ import annotations

import argparse
import re
import shutil
import sys
import uuid
from pathlib import Path

MODULE_DIR = Path(__file__).resolve().parent
REPO_ROOT = MODULE_DIR.parents[2]
EXPORTS = REPO_ROOT / "art" / "exports"
ART_DIR = REPO_ROOT / "Assets" / "_Game" / "Art"

LOOP_TEMPLATE = ART_DIR / "Animations" / "AN_Player_Idle.fbx.meta"
ONCE_TEMPLATE = ART_DIR / "Animations" / "AN_Player_Swing.fbx.meta"
PROP_TEMPLATE = ART_DIR / "Props" / "PRP_Pistol_Backup.fbx.meta"

# 名称 → (时长秒, 是否循环); 与 manifests/animations.json 一致
ANIMATIONS = {
    "AN_Player_Climb": (2.0, False),
    "AN_Player_Pickup": (2.5, False),
    "AN_Player_Throw": (2.5, False),
    "AN_Killer_DodgeSide": (2.0, False),
    "AN_Killer_KickDoor": (2.5, False),
    "AN_Killer_Stumble": (3.0, False),
    "AN_Killer_Fall": (3.0, False),
    "AN_Killer_BlockLight": (3.0, False),
    "AN_Killer_DrawBackupFire": (4.0, False),
    "POSE_Player_SitChair": (2.0, True),
    "POSE_Player_CrouchHide": (2.0, True),
    "POSE_Player_Prone": (2.0, True),
    "POSE_Player_KneelHandsUp": (2.0, True),
    "POSE_Player_HoldSmall_R": (2.0, True),
    "POSE_Player_HoldBat": (2.0, True),
    "POSE_Player_CarryLarge": (2.0, True),
    "POSE_Player_AimPistol": (2.0, True),
    "POSE_Killer_LowReady": (2.0, True),
    "POSE_Killer_Kneel": (2.0, True),
    "PAIR_D08_PlayerSide": (6.0, False),
    "PAIR_D08_KillerSide": (6.0, False),
    "PAIR_D11_PlayerSide": (3.0, False),
    "PAIR_D11_KillerSide": (3.0, False),
    "PAIR_D21_PlayerSide": (8.0, False),
    "PAIR_D21_KillerSide": (8.0, False),
    "AN_Player_PutDownLarge": (2.5, False),
    "AN_Killer_AimEnter": (1.0, False),
    "AN_Killer_AimHold": (3.0, True),
    "AN_Killer_Fire": (1.0, False),
    "AN_Killer_SearchLoop": (4.0, True),
    "AN_Killer_Run": (1.2, True),
}

PROPS = ["PRP_KillerHolster"]


def new_guid() -> str:
    return uuid.uuid4().hex


def build_anim_meta(name: str, duration: float, loop: bool) -> str:
    template = (LOOP_TEMPLATE if loop else ONCE_TEMPLATE).read_text(encoding="utf-8")
    base = "AN_Player_Idle" if loop else "AN_Player_Swing"
    text = template.replace(base, name)
    text = re.sub(r"guid: [0-9a-f]{32}", f"guid: {new_guid()}", text, count=1)
    last_frame = int(round(duration * 24)) - 1
    text = re.sub(r"lastFrame: \d+", f"lastFrame: {last_frame}", text, count=1)
    return text


def build_prop_meta() -> str:
    text = PROP_TEMPLATE.read_text(encoding="utf-8")
    return re.sub(r"guid: [0-9a-f]{32}", f"guid: {new_guid()}", text, count=1)


def install(src: Path, dst: Path, meta_text: str, dry: bool) -> str:
    meta_path = dst.with_suffix(dst.suffix + ".meta")
    if dst.exists() and meta_path.exists() and \
            dst.read_bytes() == src.read_bytes():
        return f"skip  {dst.name}"
    if not dry:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        meta_path.write_text(meta_text, encoding="utf-8")
    return f"copy  {dst.name} -> {dst.parent.name}/ (+meta)"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    n = 0
    for name, (duration, loop) in ANIMATIONS.items():
        src = EXPORTS / "animations" / f"{name}.glb".replace(".glb", ".fbx")
        if not src.exists():
            print(f"MISS  {src}", file=sys.stderr)
            n += 1
            continue
        meta = build_anim_meta(name, duration, loop)
        print(install(src, ART_DIR / "Animations" / f"{name}.fbx", meta, args.dry_run))
    for name in PROPS:
        src = EXPORTS / "props" / f"{name}.fbx"
        if not src.exists():
            print(f"MISS  {src}", file=sys.stderr)
            n += 1
            continue
        print(install(src, ART_DIR / "Props" / f"{name}.fbx", build_prop_meta(), args.dry_run))

    if n:
        print(f"{n} 个源文件缺失", file=sys.stderr)
        return 1
    print("done" + (" (dry-run)" if args.dry_run else ""))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
