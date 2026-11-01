"""Local client for the installed Blender MCP addon."""
import json, socket, sys
from pathlib import Path

def call(kind, params=None):
    with socket.create_connection(('127.0.0.1', 9876), timeout=15) as s:
        s.settimeout(600)
        s.sendall(json.dumps({'type': kind, 'params': params or {}}).encode())
        data = b''
        while True:
            part = s.recv(65536)
            if not part:
                raise RuntimeError('Blender disconnected')
            data += part
            try:
                return json.loads(data)
            except json.JSONDecodeError:
                pass

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    if len(sys.argv) > 1:
        path = str(Path(sys.argv[1]).resolve())
        print(json.dumps(call('execute_code', {'code': 'import runpy; runpy.run_path(' + repr(path) + ', run_name="__main__")'}), ensure_ascii=False))
    else:
        print(json.dumps(call('get_scene_info'), ensure_ascii=False))
