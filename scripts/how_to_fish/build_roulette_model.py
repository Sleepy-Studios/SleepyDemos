"""Self-authored faceted roulette table, using the established HowToFish axis contract."""
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


def slab(name, width, depth, lower, upper, mat):
    bevel = .12
    outline = [(-width / 2 + bevel, -depth / 2), (width / 2 - bevel, -depth / 2),
               (width / 2, -depth / 2 + bevel), (width / 2, depth / 2 - bevel),
               (width / 2 - bevel, depth / 2), (-width / 2 + bevel, depth / 2),
               (-width / 2, depth / 2 - bevel), (-width / 2, -depth / 2 + bevel)]
    vertices = [(x, y, z) for z in (lower, upper) for x, y in outline]
    faces = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces += [(i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8)]
    return mesh(name, vertices, faces, mat)


def roulette():
    wood = material("RouletteWalnut", (.20, .073, .027))
    edge = material("RouletteWoodEdge", (.34, .15, .055))
    gold = material("RouletteBrass", (.76, .51, .14))
    felt = material("RouletteFelt", (.025, .15, .105))
    red = material("RouletteRed", (.64, .035, .035))
    black = material("RouletteBlack", (.024, .030, .037))
    green = material("RouletteGreen", (.025, .49, .21))
    ivory = material("RouletteIvory", (.96, .93, .80))
    slab("WalnutTop", 2.8, 1.8, .85, .97, wood)
    slab("BrassTopBorder", 2.75, 1.75, .97, .99, gold)
    slab("FeltPlayingSurface", 2.64, 1.64, .99, 1.0, felt)
    for x in (-1.10, 1.10):
        for y in (-.58, .58):
            tube("CarvedLeg", (x, y, .055), (x, y, .86), .075, .10, wood, 8)
            tube("BrassFoot", (x, y, 0), (x, y, .06), .088, .083, gold, 8)
            tube("LegCollar", (x, y, .68), (x, y, .74), .106, .106, edge, 8)
        tube("SideBrace", (x, -.58, .26), (x, .58, .26), .035, .035, edge, 6)
    tube("LongBrace", (-1.10, 0, .26), (1.10, 0, .26), .04, .04, wood, 8)
    for name, x, color in (("Red", .65, red), ("Black", 0, black), ("Green", -.65, green)):
        box(name + "BetFrame", (x, -.30, 1.002), (.59, .36, .002), gold)
        box(name + "BetFelt", (x, -.30, 1.004), (.55, .32, .002), color)
        empty(name + "Bet", (x, -.30, 1.005))
    wheel = empty("Wheel", (0, .35, 1.05))
    tube("WheelWoodHousing", (0, 0, -.05), (0, 0, -.012), .495, .495, edge, 37, parent=wheel)
    tube("WheelBrassBed", (0, 0, -.012), (0, 0, 0), .482, .482, gold, 37, parent=wheel)
    for index in range(37):
        # Unity +Y rotation maps to decreasing source XY angle. Slot 0 faces source -Y.
        angle = -math.pi / 2 - index * math.tau / 37
        start, end = angle + math.pi / 37 - .005, angle - math.pi / 37 + .005
        points = [(radius * math.cos(a), radius * math.sin(a), .02)
                  for radius, a in ((.305, start), (.305, end), (.470, end), (.470, start))]
        mesh("Pocket" + str(index), points, [(0, 1, 2, 3)],
             green if index == 0 else red if index % 2 else black, parent=wheel)
        bpy.ops.object.text_add()
        number = bpy.context.object
        number.name = "Number" + str(index)
        number.data.body = str(index)
        number.data.size = .036
        number.data.align_x = "CENTER"
        number.data.align_y = "CENTER"
        number.data.extrude = .0004
        number.data.materials.append(ivory)
        number.location = (.434 * math.cos(angle), .434 * math.sin(angle), .022)
        number.rotation_euler.z = angle - math.pi / 2
        bpy.ops.object.convert(target="MESH")
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        number.parent = wheel
    tube("InnerBrassLip", (0, 0, .001), (0, 0, .028), .304, .295, gold, 37, parent=wheel)
    tube("WheelBowl", (0, 0, .029), (0, 0, .045), .276, .24, wood, 24, parent=wheel)
    tube("CentralSpindle", (0, 0, .045), (0, 0, .135), .065, .036, gold, 12, parent=wheel)
    for angle in (0, math.tau / 3, math.tau * 2 / 3):
        tube("HubSpoke", (0, 0, .10), (.16 * math.cos(angle), .16 * math.sin(angle), .075),
             .019, .013, gold, 6, parent=wheel)
    ball = empty("Ball", (0, -.05, 1.095))
    ellipsoid("BallVisual", (0, 0, 0), (.025, .025, .025), ivory, 2, parent=ball)
    empty("Grip")


if __name__ == "__main__":
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    art.ROOT = None
    art.ROOT = empty("RouletteTable")
    roulette()
    bpy.context.view_layer.update()
    objects = list(bpy.context.scene.objects)
    pockets = [obj for obj in objects if obj.name.startswith("Pocket")]
    assert len(pockets) == 37
    assert all(obj.data.polygons[0].normal.z > .99 for obj in pockets)
    assert sum(obj.data.materials[0].name == "RouletteRed" for obj in pockets) == 18
    assert sum(obj.data.materials[0].name == "RouletteBlack" for obj in pockets) == 18
    assert sum(obj.data.materials[0].name == "RouletteGreen" for obj in pockets) == 1
    assert bpy.data.objects["Pocket0"].data.vertices[0].co.y < 0
    assert bpy.data.objects["Ball"].parent == art.ROOT
    assert all(all(abs(value - 1) < 1e-6 for value in obj.scale) for obj in objects)
    assert all(all(abs(value) < 1e-6 for value in obj.rotation_euler) for obj in objects)
    corners = [obj.matrix_world @ Vector(corner) for obj in objects
               if obj.type == "MESH" for corner in obj.bound_box]
    low = [min(point[axis] for point in corners) for axis in range(3)]
    high = [max(point[axis] for point in corners) for axis in range(3)]
    assert abs(high[0] - low[0] - 2.8) < .01 and abs(high[1] - low[1] - 1.8) < .01
    mounts = {name: list(bpy.data.objects[name].location) for name in
              ("Grip", "Wheel", "Ball", "RedBet", "BlackBet", "GreenBet")}
    report = save_and_export(project, "RouletteTable")
    report["source_bounds_m"] = {"min": low, "max": high}
    report["source_mounts_m"] = mounts
    report["design_note"] = "Self-authored inferred table. Slots 0..36, 0 green, odd red, even black; slot 0 Unity +Z, increasing around Unity +Y."
    (project / "ArtSource~/how_to_fish/RouletteTable.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("SOURCE_BOUNDS_METRES", low, high)
    print("SOURCE_MOUNTS_METRES", mounts)
