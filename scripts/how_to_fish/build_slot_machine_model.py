"""Self-authored slot machine candidate; dimensions and styling are inferred."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, tube, box, ellipsoid
from fbx_export import save_and_export


def slot_machine():
    teal = material("SlotTeal", (.035, .23, .24))
    dark = material("SlotDark", (.025, .035, .045))
    gold = material("SlotBrass", (.76, .48, .10))
    cream = material("SlotReels", (.91, .86, .68))
    red = material("SlotRed", (.72, .055, .035))
    blue = material("SlotBlue", (.04, .32, .64))
    box("Base", (0, 0, .075), (1.08, .72, .15), dark)
    box("Cabinet", (0, .025, .85), (1.00, .57, 1.40), teal)
    for side in (-1, 1):
        box("CornerTrim", (side * .48, -.275, .90), (.04, .04, 1.46), gold)
    box("Marquee", (0, -.005, 1.665), (1.04, .63, .27), gold)
    box("RewardPanel", (0, -.331, 1.665), (.91, .025, .18), dark)
    for index, x in enumerate((-.29, 0, .29)):
        # Three reward-tier jewels on the front display, not source-game artwork.
        mesh("RewardJewel" + str(index), [(x, -.349, 1.735), (x + .065, -.349, 1.665),
             (x, -.349, 1.595), (x - .065, -.349, 1.665)], [(3, 2, 1, 0)],
             (cream, blue, gold)[index])
    box("ReelShadow", (0, -.276, 1.265), (.88, .022, .40), dark)
    for index, x in enumerate((-.29, 0, .29)):
        reel = empty("Reel" + str(index), (x, -.297, 1.265))
        tube("Roller" + str(index), (-.117, 0, 0), (.117, 0, 0), .157, .157, cream, 16, parent=reel)
        mesh("ReelDiamond" + str(index), [(0, -.159, .075), (.053, -.159, 0),
             (0, -.159, -.075), (-.053, -.159, 0)], [(3, 2, 1, 0)], red, parent=reel)
    for x in (-.45, -.145, .145, .45):
        box("ReelDivider", (x, -.451, 1.265), (.025, .025, .37), gold)
    for z in (1.06, 1.47):
        box("ReelFrame", (0, -.38, z), (.94, .18, .055), gold)
    box("ControlShelf", (0, -.345, .965), (.96, .25, .08), dark)
    tube("StartButton", (.27, -.39, 1.005), (.27, -.39, 1.045), .055, .055, red, 10)
    # An open intake mouth with a recessed back; separate sides avoid a solid front plate.
    box("IntakeBack", (0, -.272, .58), (.66, .025, .32), dark)
    for side in (-1, 1):
        box("IntakeSide", (side * .345, -.375, .58), (.045, .22, .36), gold)
    for z in (.40, .76):
        box("IntakeLip", (0, -.375, z), (.735, .22, .04), gold)
    box("LowerPanel", (0, -.273, .255), (.78, .025, .15), dark)
    tube("LeverMount", (-.50, .03, 1.18), (-.575, .03, 1.18), .072, .06, gold, 10)
    lever = empty("Lever", (-.575, .03, 1.18))
    tube("LeverStem", (0, 0, 0), (0, -.12, .30), .018, .018, gold, 8, parent=lever)
    ellipsoid("LeverKnob", (0, -.12, .30), (.052, .052, .052), red, 2, parent=lever)
    empty("Intake", (0, -.405, .58))
    empty("Display", (0, -.36, 1.665))
    empty("Grip")


if __name__ == "__main__":
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    art.ROOT = None
    art.ROOT = empty("SlotMachine")
    slot_machine()
    bpy.context.view_layer.update()
    objects = list(bpy.context.scene.objects)
    corners = [obj.matrix_world @ Vector(corner) for obj in objects
               if obj.type == "MESH" for corner in obj.bound_box]
    low = [min(point[axis] for point in corners) for axis in range(3)]
    high = [max(point[axis] for point in corners) for axis in range(3)]
    assert len([obj for obj in objects if obj.type == "EMPTY" and obj.name.startswith("Reel")]) == 3
    assert all(all(abs(value - 1) < 1e-6 for value in obj.scale) for obj in objects)
    assert 1.1 < high[0] - low[0] < 1.3 and .75 < high[1] - low[1] < .9
    assert abs(high[2] - low[2] - 1.8) < .01
    for name in ("Intake", "Display"):
        assert bpy.data.objects[name].parent == art.ROOT and bpy.data.objects[name].location.y < 0
    assert bpy.data.objects["Grip"].parent == art.ROOT and bpy.data.objects["Grip"].location.length == 0
    for obj in objects:
        if obj.name.startswith(("RewardJewel", "ReelDiamond")):
            assert all(polygon.normal.y < -.99 for polygon in obj.data.polygons), obj.name + " must face source -Y"
    report = save_and_export(project, "SlotMachine")
    report["source_bounds_m"] = {"min": low, "max": high}
    report["design_note"] = "Self-authored inferred cabinet, three reels, intake and reward display. Not original-game visual verification."
    (project / "ArtSource~/how_to_fish/SlotMachine.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("SOURCE_BOUNDS_METRES", low, high)
