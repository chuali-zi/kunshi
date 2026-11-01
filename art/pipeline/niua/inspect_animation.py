"""NIUA 动画 GLB 机检 + 棍图预览(纯标准库 + numpy/matplotlib)。

用法:
  python inspect_animation.py verify  art/exports/animations/AN_Player_Climb.glb --duration 2.0
  python inspect_animation.py preview art/exports/animations/AN_Player_Climb.glb -o art/previews/animations/AN_Player_Climb.png

verify:骨骼数(67)、时长与 24fps 帧数(±20%)、mixamorig 命名、首末帧髋部高度。
preview:全部帧叠加的正/侧视棍图(640x480,深色底),与 art/previews/animations/ 既有风格一致。
"""

from __future__ import annotations

import argparse
import json
import struct
import sys
from pathlib import Path

import numpy as np

for _s in (sys.stdout, sys.stderr):
    if _s is not None and hasattr(_s, "reconfigure"):
        _s.reconfigure(encoding="utf-8", errors="replace")

COMP = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def load_glb(path: Path):
    data = path.read_bytes()
    if data[:4] != b"glTF":
        raise SystemExit(f"{path} 不是 GLB")
    length = struct.unpack_from("<I", data, 8)[0]
    off, js, bin_ = 12, None, None
    while off < length:
        clen, ctype = struct.unpack_from("<II", data, off)
        off += 8
        chunk = data[off:off + clen]
        off += clen
        if ctype == 0x4E4F534A:
            js = json.loads(chunk.decode("utf-8"))
        elif ctype == 0x004E4942:
            bin_ = chunk
    return js, bin_


def read_accessor(js, bin_, idx):
    acc = js["accessors"][idx]
    bv = js["bufferViews"][acc["bufferView"]]
    fmt, size = COMP[acc["componentType"]]
    n = NCOMP[acc["type"]]
    stride = bv.get("byteStride") or size * n
    base = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    out = np.zeros((acc["count"], n), dtype=np.float32)
    for i in range(acc["count"]):
        out[i] = struct.unpack_from("<" + fmt * n, bin_, base + i * stride)
    return out


def quat_to_mat(q):
    x, y, z, w = q / (np.linalg.norm(q) + 1e-12)
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ])


def trs(t, r, s):
    m = np.eye(4)
    m[:3, :3] = quat_to_mat(r) * np.asarray(s)
    m[:3, 3] = t
    return m


class Rig:
    def __init__(self, js, bin_):
        self.js, self.bin = js, bin_
        self.nodes = js["nodes"]
        self.parent = [-1] * len(self.nodes)
        for i, nd in enumerate(self.nodes):
            for c in nd.get("children", []):
                self.parent[c] = i
        skin = js.get("skins", [{}])[0]
        self.joints = skin.get("joints", list(range(len(self.nodes))))
        self.names = [self.nodes[j].get("name", f"node{j}") for j in self.joints]
        # 动画通道: node -> {path: (times, values)}
        self.tracks: dict[int, dict[str, tuple[np.ndarray, np.ndarray]]] = {}
        anim = (js.get("animations") or [{}])[0]
        for ch in anim.get("channels", []):
            smp = anim["samplers"][ch["sampler"]]
            times = read_accessor(js, bin_, smp["input"]).ravel()
            vals = read_accessor(js, bin_, smp["output"])
            node = ch["target"]["node"]
            self.tracks.setdefault(node, {})[ch["target"]["path"]] = (times, vals)
        self.duration = max((t[-1] for tr in self.tracks.values() for t, _ in tr.values()), default=0.0)

    def _sample(self, node, path, t, default):
        tr = self.tracks.get(node, {}).get(path)
        if tr is None:
            return np.asarray(default, dtype=np.float32)
        times, vals = tr
        i = int(np.searchsorted(times, t, side="right")) - 1
        if i < 0:
            return vals[0]
        if i >= len(times) - 1:
            return vals[-1]
        t0, t1 = times[i], times[i + 1]
        a = 0.0 if t1 <= t0 else (t - t0) / (t1 - t0)
        v = vals[i] * (1 - a) + vals[i + 1] * a
        if path == "rotation":
            v = v / (np.linalg.norm(v) + 1e-12)
        return v

    def world_all(self, t) -> np.ndarray:
        """所有节点的世界坐标, shape (N, 3)。"""
        cache: dict[int, np.ndarray] = {}

        def world(node) -> np.ndarray:
            if node in cache:
                return cache[node]
            nd = self.nodes[node]
            if "matrix" in nd and node not in self.tracks:
                local = np.asarray(nd["matrix"], dtype=np.float32).reshape(4, 4).T
            else:
                local = trs(
                    self._sample(node, "translation", t, nd.get("translation", [0, 0, 0])),
                    self._sample(node, "rotation", t, nd.get("rotation", [0, 0, 0, 1])),
                    self._sample(node, "scale", t, nd.get("scale", [1, 1, 1])),
                )
            p = self.parent[node]
            m = local if p < 0 else world(p) @ local
            cache[node] = m
            return m

        return np.stack([world(i)[:3, 3] for i in range(len(self.nodes))])

    def world_positions(self, t) -> dict[str, np.ndarray]:
        all_pos = self.world_all(t)
        return {name: all_pos[j] for name, j in zip(self.names, self.joints)}


def cmd_verify(args) -> int:
    js, bin_ = load_glb(args.glb)
    rig = Rig(js, bin_)
    ok = True

    def check(label, cond, detail):
        nonlocal ok
        ok = ok and cond
        print(f"  {'PASS' if cond else 'FAIL'} {label}: {detail}")

    check("GLB 头", True, f"{args.glb.name} {args.glb.stat().st_size // 1024} KB")
    check("骨骼数=67", len(rig.joints) == 67, f"{len(rig.joints)}")
    mixamo = sum(1 for n in rig.names if n.startswith("mixamorig"))
    check("mixamorig 命名", mixamo >= 60, f"{mixamo}/{len(rig.names)}")
    expected = args.duration
    check("时长±20%", abs(rig.duration - expected) <= expected * 0.2,
          f"{rig.duration:.3f}s vs 目标 {expected}s ({rig.duration * 24:.0f} 帧@24fps)")
    if "mixamorig:Hips" in rig.names or "mixamorigHips" in rig.names:
        hips = next(n for n in rig.names if "Hips" in n)
        p0 = rig.world_positions(0.0)[hips]
        p1 = rig.world_positions(rig.duration)[hips]
        drift = float(np.linalg.norm(p0[[0, 2]] - p1[[0, 2]]))
        print(f"  INFO 髋部首帧 {np.round(p0, 3)} 末帧 {np.round(p1, 3)} 水平漂移 {drift:.3f}m(In-Place 参考)")
    return 0 if ok else 1


def cmd_preview(args) -> int:
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    js, bin_ = load_glb(args.glb)
    rig = Rig(js, bin_)
    frames = np.linspace(0.0, rig.duration, 25)
    pairs = [(i, rig.parent[i]) for i in range(len(rig.nodes)) if rig.parent[i] >= 0]

    fig, axes = plt.subplots(1, 2, figsize=(6.4 * 2, 4.8), dpi=100)
    fig.patch.set_facecolor("#2b2b2b")
    for ax, (haxis, title) in zip(axes, ((0, "front (X)"), (2, "side (Z)"))):
        ax.set_facecolor("#2b2b2b")
        for t in frames:
            pts = rig.world_all(float(t))
            for a, b in pairs:
                pa, pb = pts[a], pts[b]
                ax.plot([pa[haxis], pb[haxis]], [pa[1], pb[1]],
                        color="#7fb2e5", lw=0.6, alpha=0.35)
        ax.set_title(title, color="#cccccc", fontsize=8)
        ax.set_aspect("equal")
        ax.axis("off")
    args.out.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(args.out, facecolor=fig.get_facecolor(), bbox_inches="tight")
    plt.close(fig)
    print(f"preview -> {args.out}")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    v = sub.add_parser("verify")
    v.add_argument("glb", type=Path)
    v.add_argument("--duration", type=float, required=True)
    v.set_defaults(fn=cmd_verify)
    p = sub.add_parser("preview")
    p.add_argument("glb", type=Path)
    p.add_argument("-o", "--out", type=Path, required=True)
    p.set_defaults(fn=cmd_preview)
    args = ap.parse_args()
    return args.fn(args)


if __name__ == "__main__":
    raise SystemExit(main())
