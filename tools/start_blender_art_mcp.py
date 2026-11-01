import sys
sys.path.insert(0, r'C:\Users\chual\AppData\Roaming\Blender Foundation\Blender\5.1\scripts\addons')
import bpy, addon
if not hasattr(bpy.types.Scene, 'blendermcp_port'):
    addon.register()
if not getattr(bpy.types, 'blendermcp_server', None):
    bpy.types.blendermcp_server = addon.BlenderMCPServer(host='127.0.0.1', port=9876)
    bpy.types.blendermcp_server.start()
