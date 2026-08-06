"""NIUA 生成 API 最小客户端(纯标准库,无第三方依赖)。

覆盖:presign 三步上传、同步/异步生成、任务轮询、下载、
Idempotency-Key、402 钱包不足与瞬时错误重试。
API 契约见 https://api.niua.ohao.tech/openapi.yaml
"""

from __future__ import annotations

import hashlib
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

# Windows 控制台默认 cp1252/GBK,中文输出会崩;统一强制 UTF-8
for _stream in (sys.stdout, sys.stderr):
    if _stream is not None and hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8", errors="replace")

BASE_URL = os.environ.get("NIUA_BASE_URL", "https://api.niua.ohao.tech")
USER_AGENT = "congame-niua-pipeline/1.0 (+https://github.com; python-stdlib)"
MODULE_DIR = Path(__file__).resolve().parent
REPO_ROOT = MODULE_DIR.parents[2]
STATE_DIR = MODULE_DIR / "state"


class NiuaError(RuntimeError):
    pass


class WalletEmptyError(NiuaError):
    """402:钱包余额不足,需要到 https://niua.ohao.tech 充值。"""


class JobFailedError(NiuaError):
    pass


def _load_dotenv() -> None:
    env_path = MODULE_DIR / ".env"
    if not env_path.exists():
        return
    for line in env_path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        os.environ.setdefault(key.strip(), value.strip().strip('"').strip("'"))


def require_api_key() -> str:
    _load_dotenv()
    key = os.environ.get("NIUA_API_KEY", "")
    if not key:
        raise SystemExit(
            "缺少 NIUA_API_KEY。请在 art/pipeline/niua/.env 写入一行:\n"
            "  NIUA_API_KEY=niua_live_xxx\n"
            "(key 在 https://niua.ohao.tech/settings?tab=developer 创建)"
        )
    return key


def idem_key(*parts: object) -> str:
    """由请求内容派生确定性 Idempotency-Key,重复提交同一内容不会重复扣费。"""
    blob = json.dumps(parts, sort_keys=True, ensure_ascii=False, default=str)
    return "congame-" + hashlib.sha256(blob.encode("utf-8")).hexdigest()[:32]


class NiuaClient:
    def __init__(self) -> None:
        self._key = require_api_key()

    # ---- 基础 HTTP ----

    def _request(
        self,
        method: str,
        path: str,
        body: dict | None = None,
        extra_headers: dict | None = None,
        timeout: int = 120,
        retries: int = 3,
    ) -> dict:
        url = path if path.startswith("http") else BASE_URL + path
        headers = {
            "Authorization": f"Bearer {self._key}",
            "Accept": "application/json",
            "User-Agent": USER_AGENT,
        }
        data = None
        if body is not None:
            data = json.dumps(body).encode("utf-8")
            headers["Content-Type"] = "application/json"
        if extra_headers:
            headers.update(extra_headers)

        last_err: Exception | None = None
        for attempt in range(retries):
            req = urllib.request.Request(url, data=data, method=method, headers=headers)
            try:
                with urllib.request.urlopen(req, timeout=timeout) as resp:
                    return json.loads(resp.read().decode("utf-8"))
            except urllib.error.HTTPError as err:
                detail = err.read().decode("utf-8", "replace")[:500]
                if err.code == 402:
                    raise WalletEmptyError(f"402 钱包余额不足: {detail}") from err
                if err.code in (429, 500, 502, 503, 504) and attempt < retries - 1:
                    last_err = err
                    time.sleep(5 * (attempt + 1))
                    continue
                raise NiuaError(f"{method} {path} -> HTTP {err.code}: {detail}") from err
            except (urllib.error.URLError, TimeoutError, ConnectionError) as err:
                if attempt < retries - 1:
                    last_err = err
                    time.sleep(5 * (attempt + 1))
                    continue
                raise NiuaError(f"{method} {path} 网络失败: {err}") from err
        raise NiuaError(f"{method} {path} 重试耗尽: {last_err}")

    # ---- 上传(presign → PUT → finalize)----

    def upload_file(self, file_path: str | Path) -> dict:
        file_path = Path(file_path)
        payload = file_path.read_bytes()
        content_type = mimetypes.guess_type(file_path.name)[0] or "application/octet-stream"
        presign = self._request(
            "POST",
            "/api/v1/upload/presign",
            {
                "filename": file_path.name,
                "content_type": content_type,
                "size_bytes": len(payload),
            },
        )
        put = urllib.request.Request(
            presign["upload_url"],
            data=payload,
            method="PUT",
            headers={"Content-Type": content_type, "User-Agent": USER_AGENT},
        )
        with urllib.request.urlopen(put, timeout=300) as resp:
            if resp.status not in (200, 201, 204):
                raise NiuaError(f"presigned PUT 失败: HTTP {resp.status}")
        return self._request("POST", "/api/v1/upload/finalize", {"asset_id": presign["asset_id"]})

    # ---- 生成与轮询 ----

    def generate(self, modality: str, payload: dict, idempotency: str, timeout: int = 1800) -> dict:
        """同步接口直接返回 completed;异步接口返回 processing + job_id。"""
        return self._request(
            "POST",
            f"/api/v1/generate/{modality}",
            payload,
            extra_headers={"Idempotency-Key": idempotency},
            timeout=timeout,
            retries=1,  # 生成请求不自动重试,避免语义不明的双重提交
        )

    def job(self, job_id: str) -> dict:
        return self._request("GET", f"/api/v1/jobs/{job_id}")

    def wait_job(self, job_id: str, poll_seconds: int = 8, timeout: int = 1800) -> dict:
        deadline = time.time() + timeout
        while time.time() < deadline:
            info = self.job(job_id)
            status = info.get("status")
            # completed 但 url 尚为 null:服务端先写状态后写 URL,继续短轮询
            if status == "completed" and info.get("url"):
                return info
            if status == "failed":
                raise JobFailedError(f"job {job_id} 失败: {json.dumps(info, ensure_ascii=False)}")
            time.sleep(poll_seconds)
        raise NiuaError(f"job {job_id} 轮询超时({timeout}s)")

    def resolve(self, response: dict, timeout: int = 1800) -> dict:
        """把 generate 的返回统一成 completed 且带 url 的结果。"""
        if response.get("url"):
            return response
        job_id = response.get("job_id")
        if not job_id:
            raise NiuaError(f"无法识别的生成返回: {json.dumps(response, ensure_ascii=False)[:400]}")
        return self.wait_job(job_id, timeout=timeout)

    # ---- 下载 ----

    def download(self, url: str, dest: str | Path) -> Path:
        dest = Path(dest)
        dest.parent.mkdir(parents=True, exist_ok=True)
        # CDN 由 Cloudflare 托管,默认 Python UA 会被 403;统一带正常 UA。
        # CDN URL 公开可读,不带 Authorization(带上反而可能与签名机制冲突)。
        req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        with urllib.request.urlopen(req, timeout=600) as resp:
            dest.write_bytes(resp.read())
        return dest


# ---- 简单状态存档(防重复扣费 / 断点续跑)----


def load_state(name: str) -> dict:
    path = STATE_DIR / f"{name}.json"
    if path.exists():
        return json.loads(path.read_text(encoding="utf-8"))
    return {"items": {}}


def save_state(name: str, state: dict) -> None:
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    path = STATE_DIR / f"{name}.json"
    path.write_text(json.dumps(state, indent=2, ensure_ascii=False), encoding="utf-8")


# ---- 产物校验 ----


def check_wav(path: Path) -> bool:
    head = path.read_bytes()[:12]
    return len(head) == 12 and head[:4] == b"RIFF" and head[8:12] == b"WAVE"


def check_glb(path: Path) -> bool:
    return path.read_bytes()[:4] == b"glTF"
