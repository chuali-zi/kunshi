"""角色管线:参考图 → 标准化立绘 → 图生 3D → 自动绑骨(Mixamo Humanoid)。

分阶段执行,每阶段结果写入 state/characters.json,重跑自动跳过已完成阶段。
standardize 之后必须人工看图确认再进 mesh(避免在不满意的立绘上花 49¢+49¢)。

用法:
  python gen_characters.py --char CH_Player --stage standardize
  # 打开 art/exports/characters/CH_Player_sheet.png 检查,不满意:
  python gen_characters.py --char CH_Player --stage standardize --force --seed 42
  # 满意后:
  python gen_characters.py --char CH_Player --stage mesh
  python gen_characters.py --char CH_Player --stage rig
  # 或一次跑完 mesh+rig:
  python gen_characters.py --char CH_Player --stage all
费用:standardize 9¢ / mesh 49¢ / rig 49¢(rig 成功率约 60%,脚本自动重试 3 次)
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

from niua_client import (
    MODULE_DIR,
    REPO_ROOT,
    JobFailedError,
    NiuaClient,
    NiuaError,
    check_glb,
    idem_key,
    load_state,
    save_state,
)

EXPORT_DIR = REPO_ROOT / "art" / "exports" / "characters"
STATE_NAME = "characters"


def load_manifest() -> dict:
    return json.loads((MODULE_DIR / "manifests" / "characters.json").read_text(encoding="utf-8"))


def get_record(state: dict, char: str) -> dict:
    return state["items"].setdefault(char, {"stages": {}})


def stage_done(record: dict, stage: str) -> bool:
    info = record["stages"].get(stage, {})
    if info.get("status") != "completed":
        return False
    file = info.get("file")
    return not file or (REPO_ROOT / file).exists()


def run_standardize(client: NiuaClient, manifest: dict, char: str, record: dict, seed: int | None) -> None:
    spec = manifest["characters"][char]
    ref_path = REPO_ROOT / spec["reference"]
    if not ref_path.exists():
        # 同名不同扩展名也接受(png/jpg/jpeg/webp)
        candidates = list(ref_path.parent.glob(ref_path.stem + ".*")) if ref_path.parent.exists() else []
        if candidates:
            ref_path = candidates[0]
        else:
            raise SystemExit(f"参考图不存在: {spec['reference']}(请把 {char} 的参考图放到 art/references/characters/)")

    print(f"[{char}] 上传参考图 {ref_path.name} ...")
    upload = client.upload_file(ref_path)
    payload = {
        "prompt": spec["sheet_prompt"],
        "reference_image": upload["r2_key"],
        "aspect_ratio": spec.get("aspect_ratio", "3:4"),
    }
    if seed is not None:
        payload["seed"] = seed
    elif spec.get("seed") is not None:
        payload["seed"] = spec["seed"]

    print(f"[{char}] 生成标准化立绘 ...")
    resp = client.resolve(client.generate("image", payload, idem_key("sheet", char, payload)), timeout=600)
    dest = client.download(resp["url"], EXPORT_DIR / f"{char}_sheet.png")
    record["stages"]["standardize"] = {
        "status": "completed",
        "r2_key": resp.get("r2_key"),
        "reference_r2_key": upload["r2_key"],
        "file": str(dest.relative_to(REPO_ROOT)),
        "seed": payload.get("seed"),
    }
    print(f"[{char}] 立绘已保存 -> {dest}\n  请人工检查;满意后运行 --stage mesh,不满意 --force --seed N 重跑(9¢/次)")


def run_mesh(client: NiuaClient, manifest: dict, char: str, record: dict, seed: int | None) -> None:
    sheet = record["stages"].get("standardize", {})
    if sheet.get("status") != "completed":
        raise SystemExit(f"[{char}] 请先完成 standardize 阶段并人工确认立绘")
    payload = {**manifest["mesh"], "image_key": sheet["r2_key"]}
    if seed is not None:
        payload["seed"] = seed
    print(f"[{char}] 图生 3D(数分钟,49¢)...")
    resp = client.resolve(client.generate("mesh", payload, idem_key("mesh", char, payload), timeout=3600), timeout=3600)
    dest = client.download(resp["url"], EXPORT_DIR / f"{char}.glb")
    if not check_glb(dest):
        raise NiuaError(f"[{char}] 下载的 mesh 不是有效 GLB: {dest}")
    record["stages"]["mesh"] = {
        "status": "completed",
        "r2_key": resp.get("r2_key"),
        "job_id": resp.get("job_id"),
        "file": str(dest.relative_to(REPO_ROOT)),
    }
    print(f"[{char}] 白模+贴图 GLB -> {dest}({dest.stat().st_size // 1024} KB)")


def run_rig(client: NiuaClient, manifest: dict, char: str, record: dict) -> None:
    mesh = record["stages"].get("mesh", {})
    if mesh.get("status") != "completed":
        raise SystemExit(f"[{char}] 请先完成 mesh 阶段")
    payload = {**manifest["rig"], "mesh_key": mesh["r2_key"]}

    last_err: Exception | None = None
    for attempt in range(1, 4):
        try:
            print(f"[{char}] 自动绑骨 mixamo-humanoid(第 {attempt}/3 次,49¢/次,约 10 分钟)...")
            resp = client.resolve(
                client.generate("rig", payload, idem_key("rig", char, payload, attempt), timeout=3600),
                timeout=3600,
            )
            suffix = Path(resp.get("filename") or "rig.glb").suffix or ".glb"
            dest = client.download(resp["url"], EXPORT_DIR / f"{char}_rigged{suffix}")
            rigged_ok = suffix != ".glb" or b"mixamorig" in dest.read_bytes()
            record["stages"]["rig"] = {
                "status": "completed",
                "r2_key": resp.get("r2_key"),
                "job_id": resp.get("job_id"),
                "file": str(dest.relative_to(REPO_ROOT)),
                "mixamo_bones_found": rigged_ok,
            }
            print(f"[{char}] 绑骨完成 -> {dest}(mixamo 骨骼检测: {rigged_ok})")
            if not rigged_ok:
                print(f"[{char}] 警告:GLB 中未检测到 mixamorig 骨骼名,导入 Unity 前请人工确认", file=sys.stderr)
            return
        except (JobFailedError, NiuaError) as err:
            last_err = err
            print(f"[{char}] 绑骨失败: {err}", file=sys.stderr)
            if attempt < 3:
                time.sleep(20)
    raise SystemExit(
        f"[{char}] 绑骨 3 次均失败({last_err})。兜底方案见 README:把 {char}.glb 转 FBX 后用 "
        "https://www.mixamo.com 网页自动绑定(免费),导出 FBX 放回 art/exports/characters/。"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--char", required=True, choices=["CH_Player", "CH_Killer"])
    parser.add_argument("--stage", required=True, choices=["standardize", "mesh", "rig", "all"])
    parser.add_argument("--force", action="store_true", help="强制重跑该阶段(下游阶段结果随之失效)")
    parser.add_argument("--seed", type=int)
    args = parser.parse_args()

    manifest = load_manifest()
    state = load_state(STATE_NAME)
    record = get_record(state, args.char)
    client = NiuaClient()

    stages = ["mesh", "rig"] if args.stage == "all" else [args.stage]
    for stage in stages:
        if not args.force and stage_done(record, stage):
            print(f"[{args.char}] {stage} 已完成,跳过(--force 重跑)")
            continue
        if args.force:
            # 该阶段重跑后,依赖它的下游结果不再有效
            downstream = {"standardize": ["mesh", "rig"], "mesh": ["rig"], "rig": []}[stage]
            for d in downstream:
                record["stages"].pop(d, None)
        try:
            if stage == "standardize":
                run_standardize(client, manifest, args.char, record, args.seed)
            elif stage == "mesh":
                run_mesh(client, manifest, args.char, record, args.seed)
            elif stage == "rig":
                run_rig(client, manifest, args.char, record)
        finally:
            save_state(STATE_NAME, state)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
