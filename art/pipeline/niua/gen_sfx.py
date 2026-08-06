"""批量生成音效:manifests/sfx.json → art/exports/audio/*.wav

用法:
  python gen_sfx.py --dry-run          # 只打印将生成的条目与预估费用
  python gen_sfx.py                    # 生成所有未完成条目
  python gen_sfx.py --only SFX_Gunshot # 只生成指定条目
  python gen_sfx.py --force --only X   # 强制重跑(换 seed 用 --seed)
"""

from __future__ import annotations

import argparse
import json
import sys

from niua_client import (
    MODULE_DIR,
    REPO_ROOT,
    JobFailedError,
    NiuaClient,
    check_wav,
    idem_key,
    load_state,
    save_state,
)

EXPORT_DIR = REPO_ROOT / "art" / "exports" / "audio"
PRICE_CENTS = 5


def load_manifest() -> tuple[dict, list[dict]]:
    data = json.loads((MODULE_DIR / "manifests" / "sfx.json").read_text(encoding="utf-8"))
    return data.get("defaults", {}), data["items"]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append", help="只处理指定 name,可重复")
    parser.add_argument("--force", action="store_true", help="忽略已完成状态强制重跑")
    parser.add_argument("--seed", type=int, help="重跑时指定 seed 换结果")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    defaults, items = load_manifest()
    if args.only:
        items = [i for i in items if i["name"] in set(args.only)]
        if not items:
            print("--only 未匹配任何条目", file=sys.stderr)
            return 2

    state = load_state("sfx")
    todo = []
    for item in items:
        record = state["items"].get(item["name"], {})
        out = EXPORT_DIR / f"{item['name']}.wav"
        if not args.force and record.get("status") == "completed" and out.exists():
            continue
        todo.append(item)

    print(f"待生成 {len(todo)} 条,预估费用 ~{len(todo) * PRICE_CENTS}¢")
    for item in todo:
        print(f"  {item['name']:32s} {item['seconds']:>4}s loop={item.get('loop', False)}")
    if args.dry_run or not todo:
        return 0

    client = NiuaClient()

    # 先全部提交(异步),再统一轮询,减少总等待时间
    pending: list[tuple[dict, dict]] = []
    for item in todo:
        payload = {**defaults, **{k: v for k, v in item.items() if k not in ("name", "group")}}
        if args.seed is not None:
            payload["seed"] = args.seed
        key = idem_key("sfx", item["name"], payload)
        print(f"提交 {item['name']} ...")
        resp = client.generate("sfx", payload, key)
        state["items"][item["name"]] = {
            "status": "submitted",
            "job_id": resp.get("job_id"),
            "payload": payload,
        }
        save_state("sfx", state)
        pending.append((item, resp))

    failures = []
    for item, resp in pending:
        name = item["name"]
        try:
            done = client.resolve(resp, timeout=900)
            dest = client.download(done["url"], EXPORT_DIR / f"{name}.wav")
        except Exception as err:  # 单条失败不拖垮整批
            print(f"  FAILED {name}: {err}", file=sys.stderr)
            state["items"][name]["status"] = "failed"
            save_state("sfx", state)
            failures.append(name)
            continue
        ok = check_wav(dest)
        state["items"][name] = {
            "status": "completed" if ok else "invalid",
            "job_id": done.get("job_id") or resp.get("job_id"),
            "r2_key": done.get("r2_key"),
            "url": done.get("url"),
            "file": str(dest.relative_to(REPO_ROOT)),
        }
        save_state("sfx", state)
        print(f"  OK {name} -> {dest} ({dest.stat().st_size // 1024} KB, RIFF={ok})")
        if not ok:
            failures.append(name)

    if failures:
        print(f"\n{len(failures)} 条失败: {', '.join(failures)}(重跑:--only <name> --force --seed N)", file=sys.stderr)
        return 1
    print("\n全部完成。产物在 art/exports/audio/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
