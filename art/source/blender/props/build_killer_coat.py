"""Low-poly hanging coat prop for the mysterious killer silhouette.

The coat is deliberately made from a few readable fabric panels: a draped body,
sloping sleeves, raised collar/lapels, and shallow raised fold strips.  It has
no rig or gameplay assumptions; Unity can mount the PRP_KillerCoat root where
the previous placeholder was used.
"""
import math
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "_shared"))
from ak import clean_scene, empty, make_mat, preview, report, save_blend

ROOT = Path(__file__).resolve().parents[4]
OUT_BLEND = ROOT / "art/source/blender/props/PRP_KillerCoat_v001.blend"
OUT_FBX = ROOT / "art/exports/props/PRP_KillerCoat.fbx"
UNITY_FBX = ROOT / "Assets/_Game/Art/Props/PRP_KillerCoat.fbx"


def extruded(name, outline, depth, mat, parent, y=0.0):
    """Extrude an x/z outline along y, with a front face toward -Y."""
    n = len(outline)
    verts = [(x, y - depth / 2, z) for x, z in outline]
    verts += [(x, y + depth / 2, z) for x, z in outline]
    # The outline is supplied clockwise in x/z. Reverse the front winding so
    # the visible face has a -Y normal for Unity's default back-face culling.
    faces = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.data.materials.append(mat)
    return obj


def panel(name, points, depth, mat, root, y):
    return extruded(name, points, depth, mat, root, y)


clean_scene()
root = empty("PRP_KillerCoat", (0, 0, 0), display="PLAIN_AXES", size=0.08)

cloth = make_mat("MAT_MV_Coat_Charcoal", (0.028, 0.032, 0.036), rough=0.91)
cloth_mid = make_mat("MAT_MV_Coat_Fold", (0.050, 0.055, 0.058), rough=0.94)
trim = make_mat("MAT_MV_Coat_Trim", (0.010, 0.012, 0.014), rough=0.72)
lining = make_mat("MAT_MV_Coat_Lining", (0.070, 0.052, 0.047), rough=0.96)
metal = make_mat("MAT_MV_Coat_Hook", (0.065, 0.072, 0.073), metallic=0.72, rough=0.38)

# Main body: shoulders are broad, the hem hangs unevenly to keep the prop from
# reading as a rectangular placeholder.  The face is front-facing (-Y).
body_outline = [
    (-0.27, 2.03), (-0.53, 1.91), (-0.49, 1.48), (-0.47, 0.92),
    (-0.50, 0.20), (-0.22, 0.14), (0.02, 0.17), (0.49, 0.20),
    (0.47, 0.92), (0.49, 1.48), (0.53, 1.91), (0.27, 2.03),
]
panel("Coat_Body", body_outline, 0.12, cloth, root, 0.0)

# Sleeves slope away from the shoulders, with slightly asymmetric hanging ends.
left_sleeve = [(-0.25, 1.99), (-0.52, 1.91), (-0.76, 1.50), (-0.70, 1.39),
               (-0.46, 1.62), (-0.22, 1.78)]
right_sleeve = [(0.25, 1.99), (0.52, 1.91), (0.73, 1.36), (0.67, 1.27),
                (0.45, 1.62), (0.22, 1.78)]
panel("Coat_Sleeve_L", left_sleeve, 0.105, cloth, root, 0.005)
panel("Coat_Sleeve_R", right_sleeve, 0.105, cloth, root, 0.005)

# Raised collar and open lapels establish the garment construction at a small
# in-game scale.  The warm lining peeks through the opening without becoming a
# bright color block.
collar = [(-0.27, 2.04), (-0.10, 2.16), (0.10, 2.16), (0.27, 2.04),
          (0.18, 1.94), (0.0, 2.00), (-0.18, 1.94)]
panel("Coat_Collar", collar, 0.15, cloth, root, -0.01)
lapel_l = [(-0.23, 2.01), (-0.06, 1.88), (-0.14, 1.45), (-0.31, 1.75)]
lapel_r = [(0.23, 2.01), (0.06, 1.88), (0.14, 1.45), (0.31, 1.75)]
panel("Coat_Lapel_L", lapel_l, 0.024, trim, root, -0.082)
panel("Coat_Lapel_R", lapel_r, 0.024, trim, root, -0.082)

# A narrow inner opening and hem reveal the coat layers while staying simple.
panel("Coat_InnerOpening", [(-0.055, 1.90), (0.055, 1.90), (0.048, 0.34),
                             (-0.048, 0.34)], 0.018, lining, root, -0.078)
panel("Coat_Hem_Lining", [(-0.46, 0.20), (-0.22, 0.14), (0.02, 0.17),
                           (0.02, 0.24), (-0.22, 0.22), (-0.46, 0.27)],
      0.020, lining, root, -0.073)

# Raised fold strips are geometry rather than a material-only workaround.  The
# small angular changes catch the room light and keep folds readable in a flat
# shaded or backface-culled Unity material.
folds = [
    ("L0", [(-0.39, 1.73), (-0.34, 1.75), (-0.30, 0.56), (-0.35, 0.50)]),
    ("L1", [(-0.22, 1.66), (-0.18, 1.67), (-0.15, 0.42), (-0.20, 0.38)]),
    ("R0", [(0.39, 1.73), (0.34, 1.75), (0.30, 0.56), (0.35, 0.50)]),
    ("R1", [(0.22, 1.66), (0.18, 1.67), (0.15, 0.42), (0.20, 0.38)]),
    ("SL", [(-0.52, 1.82), (-0.48, 1.85), (-0.68, 1.48), (-0.71, 1.47)]),
    ("SR", [(0.52, 1.82), (0.48, 1.85), (0.67, 1.42), (0.70, 1.40)]),
]
for key, points in folds:
    panel("Coat_Fold_" + key, points, 0.016, cloth_mid, root, -0.073)

# Simple cuffs and a small hanger neck make the hanging state clear when the
# asset is placed without a character rig.
panel("Coat_Cuff_L", [(-0.74, 1.50), (-0.68, 1.39), (-0.62, 1.44),
                       (-0.69, 1.55)], 0.12, trim, root, 0.005)
panel("Coat_Cuff_R", [(0.73, 1.36), (0.67, 1.27), (0.61, 1.33),
                       (0.68, 1.43)], 0.12, trim, root, 0.005)

neck = empty("Coat_HangerNeck", (0, 0.0, 2.16), parent=root, display="PLAIN_AXES", size=0.025)
hook = panel("Coat_HangerHook", [(-0.045, 2.16), (-0.045, 2.31), (0.0, 2.38),
                                  (0.045, 2.31), (0.045, 2.16)], 0.035,
             metal, root, 0.0)
socket = empty("SOCKET_CoatMount", (0, 0, 2.34), parent=root,
               display="ARROWS", size=0.035)

report("PRP_KillerCoat", root, 1400, {"x": 1.5, "z": 2.2})
save_blend(str(OUT_BLEND))

bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
for child in root.children_recursive:
    child.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=str(OUT_FBX), use_selection=True,
    object_types={"EMPTY", "MESH"}, bake_space_transform=True,
    axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_NONE",
    add_leaf_bones=False,
)
UNITY_FBX.parent.mkdir(parents=True, exist_ok=True)
import shutil
shutil.copy2(OUT_FBX, UNITY_FBX)
preview(str(ROOT / "art/previews/PRP_KillerCoat_v001.png"),
        target=(0, 0, 1.2), dist=3.0, angle=25, height=0.15)
print(f"[fbx] -> {OUT_FBX}")
print("KILLER_COAT_DONE")
