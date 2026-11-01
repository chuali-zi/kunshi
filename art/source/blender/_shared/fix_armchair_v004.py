"""Fix the imported armchair tufted-back winding and export v004.

The detail is already joined into Armchair_Body in v003, so this locates the
quilted connected component by its generated dimensions and flips only those
faces.  The rest of the chair and its gameplay hierarchy stay untouched.
"""
import bmesh
import bpy
import shutil
from pathlib import Path

ROOT = Path("D:/game/con_game")
SOURCE = ROOT / "art/source/blender/environment/FUR_Armchair_v003.blend"
DEST = ROOT / "art/source/blender/environment/FUR_Armchair_v004.blend"
FBX = ROOT / "art/exports/environment/FUR_Armchair.fbx"
UNITY = ROOT / "Assets/_Game/Art/Environment/FUR_Armchair.fbx"

bpy.ops.wm.read_factory_settings(use_empty=True)

with bpy.data.libraries.load(str(SOURCE), link=False) as (src, dst):
    dst.objects = list(src.objects)
for obj in dst.objects:
    if obj:
        bpy.context.scene.collection.objects.link(obj)
bpy.context.view_layer.update()

body = bpy.data.objects.get("Armchair_Body")
if body is None or body.type != "MESH":
    raise RuntimeError("FUR_Armchair_v003 is missing Armchair_Body")

mesh = body.data
bm = bmesh.new()
bm.from_mesh(mesh)
bm.faces.ensure_lookup_table()

# Find disconnected islands without relying on polygon index order after FBX
# round trips.  The generated back is a 32x32 quad grid with these extents.
seen = set()
target = None
for start in bm.faces:
    if start in seen:
        continue
    stack = [start]
    seen.add(start)
    component = []
    while stack:
        face = stack.pop()
        component.append(face)
        for edge in face.edges:
            for linked in edge.link_faces:
                if linked not in seen:
                    seen.add(linked)
                    stack.append(linked)
    verts = {vert for face in component for vert in face.verts}
    xs = [vert.co.x for vert in verts]
    ys = [vert.co.y for vert in verts]
    zs = [vert.co.z for vert in verts]
    dims = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    if (
        len(component) == 32 * 32
        and 0.59 < dims[0] < 0.61
        and 0.06 < dims[1] < 0.09
        and 0.45 < dims[2] < 0.47
    ):
        target = component
        break

if target is None:
    bm.free()
    raise RuntimeError("Could not identify the joined Tufted_back component")

for face in target:
    face.normal_flip()
bm.normal_update()
bm.to_mesh(mesh)
mesh.update()
bm.free()

# Keep the source self-contained and free of the linked library bookkeeping.
for lib in list(bpy.data.libraries):
    bpy.data.libraries.remove(lib)
bpy.ops.wm.save_as_mainfile(filepath=str(DEST))

FBX.parent.mkdir(parents=True, exist_ok=True)
UNITY.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=str(FBX),
    object_types={"EMPTY", "MESH"},
    bake_space_transform=True,
    axis_forward="-Z",
    axis_up="Y",
    apply_scale_options="FBX_SCALE_NONE",
    use_mesh_modifiers=True,
    add_leaf_bones=False,
)
shutil.copy2(FBX, UNITY)
print("ARMCHAIR_V004_DONE", len(target), str(DEST), str(FBX), str(UNITY))
