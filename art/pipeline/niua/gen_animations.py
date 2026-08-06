"""批量生成 P0 动画:manifests/animations.json → art/exports/animations/

text2motion 为异步接口,输出 mixamo 骨架的动画(GLB;job 里另有中间 BVH 时一并下载)。
Unity 导入时把 GLB 的 Rig 设为 Humanoid 即可重定向到双角色。

用法同 gen_sfx.py:--dry-run / --only NAME / --force / --seed N
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from niua_client import (
    MODULE_DIR,
    REPO_ROOT,
    JobFailedError,
    NiuaClient,
    check_glb,
    idem_key,
    load_state,
    save_state,
)

EXPORT_DIR = REPO_ROOT / "art" / "exports" / "animations"
PRICE_CENTS = 21


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append")
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--seed", type=int)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    data = json.loads((MODULE_DIR / "manifests" / "animations.json").read_text(encoding="utf-8"))
    defaults, items = data.get("defaults", {}), data["items"]
    if args.only:
        items = [i for i in items if i["name"] in set(args.only)]
        if not items:
            print("--only 未匹配任何条目", file=sys.stderr)
            return 2

    state = load_state("animations")
    todo = []
    for item in items:
        record = state["items"].get(item["name"], {})
        done_file = record.get("file")
        if not args.force and record.get("status") == "completed" and done_file and (REPO_ROOT / done_file).exists():
            continue
        todo.append(item)

    print(f"待生成 {len(todo)} 条动画,预估费用 ~{len(todo) * PRICE_CENTS}¢")
    for item in todo:
        print(f"  {item['name']:24s} {item['duration']:>4}s loop={item.get('loop', False)}")
    if args.dry_run or not todo:
        return 0

    client = NiuaClient()

    pending = []
    for item in todo:
        payload = {**defaults, **{k: v for k, v in item.items() if k != "name"}}
        if args.seed is not None:
            payload["seed"] = args.seed
        key = idem_key("text2motion", item["name"], payload)
        print(f"提交 {item['name']} ...")
        resp = client.generate("text2motion", payload, key)
        state["items"][item["name"]] = {
            "status": "submitted",
            "job_id": resp.get("job_id"),
            "payload": payload,
        }
        save_state("animations", state)
        pending.append((item, resp))

    failures = []
    for item, resp in pending:
        name = item["name"]
        try:
            done = client.resolve(resp, timeout=1800)
            # 主产物:url 指向的文件;后缀以 job 的 filename 为准(mixamo 模式应为 .glb)
            suffix = Path(done.get("filename") or "clip.glb").suffix or ".glb"
            dest = client.download(done["url"], EXPORT_DIR / f"{name}{suffix}")
        except Exception as err:  # 单条失败不拖垮整批
            print(f"  FAILED {name}: {err}", file=sys.stderr)
            state["items"][name]["status"] = "failed"
            save_state("animations", state)
            failures.append(name)
            continue
        valid = check_glb(dest) if suffix == ".glb" else dest.stat().st_size > 0
        state["items"][name] = {
            "status": "completed" if valid else "invalid",
            "job_id": done.get("job_id") or resp.get("job_id"),
            "r2_key": done.get("r2_key"),
            "url": done.get("url"),
            "file": str(dest.relative_to(REPO_ROOT)),
        }
        save_state("animations", state)
        print(f"  OK {name} -> {dest} ({dest.stat().st_size // 1024} KB, valid={valid})")
        if not valid:
            failures.append(name)

    if failures:
        print(f"\n{len(failures)} 条失败: {', '.join(failures)}(重跑:--only <name> --force --seed N)", file=sys.stderr)
        return 1
    print("\n全部完成。产物在 art/exports/animations/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
