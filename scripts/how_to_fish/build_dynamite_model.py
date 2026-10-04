"""Self-authored visual prop; shared metre/axis export, no real explosive construction data."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, tube
from fbx_export import save_and_export


def dynamite():
    red = material("DynamiteRed", (.60, .035, .025))
    alternate = material("DynamiteRedLight", (.76, .065, .035))
    dark = material("DynamiteBands", (.055, .060, .065))
    white = material("DynamiteFuses", (.90, .88, .78))
    centers = [(0, 0)] + [(math.cos(i * math.tau / 6) * .052,
                           math.sin(i * math.tau / 6) * .052) for i in range(6)]
    for index, (x, z) in enumerate(centers):
        tube("RedStick" + str(index), (x, .15, z), (x, -.15, z),
             .029, .029, red if index % 2 else alternate, 6)
        # Visible strands converge into a single stylized fuse tip.
        points = [(x, -.151, z), (x * .75, -.19, z + .028),
                  (0, -.235, .065), (.016, -.275, .115)]
        for segment in range(2):
            tube(f"Fuse{index}_{segment}", points[segment], points[segment + 1],
                 .0032, .0032, white, 5)
    tube("FuseTip", (0, -.235, .065), (.016, -.275, .115), .004, .0032, white, 5)
    for index, y in enumerate((-.092, .092)):
        # Hollow faceted bands leave the individual red end faces visible.
        vertices = [(math.cos(i * math.tau / 12) * radius, depth,
                     math.sin(i * math.tau / 12) * radius)
                    for depth in (y - .013, y + .013)
                    for radius in (.083, .078) for i in range(12)]
        faces = []
        for i in range(12):
            j = (i + 1) % 12
            faces.extend([(i, i + 24, j + 24, j),
                          (i + 12, j + 12, j + 36, i + 36),
                          (i, j, j + 12, i + 12),
                          (i + 24, i + 36, j + 36, j + 24)])
        mesh("BindingBand" + str(index), vertices, faces, dark)
    empty("Grip")
    empty("Fuse", (.016, -.275, .115))


if __name__ == "__main__":
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    art.ROOT = None
    art.ROOT = empty("Dynamite")
    dynamite()
    bpy.context.view_layer.update()
    objects = list(bpy.context.scene.objects)
    corners = [obj.matrix_world @ Vector(corner) for obj in objects
               if obj.type == "MESH" for corner in obj.bound_box]
    low = [min(point[axis] for point in corners) for axis in range(3)]
    high = [max(point[axis] for point in corners) for axis in range(3)]
    assert len([obj for obj in objects if obj.name.startswith("RedStick")]) == 7
    assert len([obj for obj in objects if obj.name.startswith("BindingBand")]) == 2
    assert all(all(abs(value - 1) < 1e-6 for value in obj.scale) for obj in objects)
    assert high[1] - low[1] < .5 and max(high[0] - low[0], high[2] - low[2]) < .25
    assert bpy.data.objects["Grip"].parent == art.ROOT
    assert bpy.data.objects["Fuse"].location.y < bpy.data.objects["Grip"].location.y
    report = save_and_export(project, "Dynamite")
    report["source_bounds_m"] = {"min": low, "max": high}
    report["design_note"] = "Self-authored candidate visual; dimensions estimated for game handling. Grip at bundle center."
    (project / "ArtSource~/how_to_fish/Dynamite.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("SOURCE_BOUNDS_METRES", low, high)
