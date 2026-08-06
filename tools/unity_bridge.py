"""直连 MCP-for-Unity 编辑器 TCP 桥(默认 127.0.0.1:6400)。

协议:连接后服务端发一行握手 "WELCOME UNITY-MCP 1 FRAMING=1\n",
之后双向都是 8 字节大端长度前缀 + UTF-8 JSON。
命令信封:{"type": "<tool>", "params": {...}}

用法:
  python tools/unity_bridge.py ping
  python tools/unity_bridge.py call manage_editor '{"action":"play"}'
  python tools/unity_bridge.py call read_console '{"action":"get","types":["error"]}'
"""

from __future__ import annotations

import json
import socket
import struct
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


class UnityBridge:
    def __init__(self, port: int = 6400, timeout: float = 120.0):
        self.sock = socket.create_connection(("127.0.0.1", port), timeout=10)
        self.sock.settimeout(timeout)
        handshake = b""
        while not handshake.endswith(b"\n"):
            chunk = self.sock.recv(1)
            if not chunk:
                raise IOError("握手中断")
            handshake += chunk
        self.handshake = handshake.decode("ascii", "replace").strip()

    def _recv_exact(self, n: int) -> bytes:
        data = b""
        while len(data) < n:
            chunk = self.sock.recv(n - len(data))
            if not chunk:
                raise IOError("连接关闭")
            data += chunk
        return data

    def call(self, command_type: str, params: dict | None = None) -> dict:
        payload = json.dumps({"type": command_type, "params": params or {}}).encode("utf-8")
        self.sock.sendall(struct.pack(">Q", len(payload)) + payload)
        length = struct.unpack(">Q", self._recv_exact(8))[0]
        return json.loads(self._recv_exact(length).decode("utf-8"))

    def ping(self) -> dict:
        payload = b"ping"
        self.sock.sendall(struct.pack(">Q", len(payload)) + payload)
        length = struct.unpack(">Q", self._recv_exact(8))[0]
        return json.loads(self._recv_exact(length).decode("utf-8"))


def main() -> int:
    bridge = UnityBridge()
    print(f"# {bridge.handshake}", file=sys.stderr)
    cmd = sys.argv[1] if len(sys.argv) > 1 else "ping"
    if cmd == "ping":
        print(json.dumps(bridge.ping(), ensure_ascii=False))
        return 0
    if cmd == "call":
        params = json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
        result = bridge.call(sys.argv[2], params)
        print(json.dumps(result, ensure_ascii=False, indent=1)[:6000])
        return 0
    print("unknown command", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
