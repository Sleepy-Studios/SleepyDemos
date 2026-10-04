"""Original professional-lure fish meshes; export through the shared axis contract."""
import math
import sys
from pathlib import Path
import bpy
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, ellipsoid, tube, box
from fbx_export import save_and_export


def fin(name, points, color, parent=None):
    face = tuple(range(len(points)))
    return mesh(name, points, [face, tuple(reversed(face))], color, parent)


def eyes(x, y, z, size, pupil, pale, parent=None):
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), (side * x, y, z), (size, size * .7, size), pale, 1, parent)
        ellipsoid("Pupil" + str(side), (side * (x + size * .55), y - size * .2, z), (size * .48,) * 3, pupil, 1, parent)


def tail(y, z, height, color):
    pivot = empty("Tail", (0, y, z))
    fin("TailFin", [(0, 0, 0), (0, .22, height), (0, .14, 0), (0, .22, -height)], color, pivot)


def ordinary(name):
    # Distinct body proportions and anatomical details, not one fish with palette swaps.
    shapes = {"Bass": (.16, .48, .23, (.24, .34, .17)), "RedSnapper": (.14, .43, .25, (.71, .21, .16)),
              "Parrotfish": (.19, .41, .27, (.16, .57, .48)), "Tigerfish": (.13, .54, .21, (.64, .56, .33)),
              "FlyingFish": (.085, .43, .105, (.30, .47, .61)), "Sengarat": (.20, .62, .20, (.37, .43, .46))}
    width, length, height, color = shapes[name]
    skin = material(name + "Skin", color)
    pale = material(name + "Belly", tuple(min(.9, value * .5 + .43) for value in color))
    fins = material(name + "Fins", tuple(value * .68 for value in color))
    ink = material("RockFishInk", (.024, .031, .035))
    z = height + .025
    if name == "Sengarat":
        # Broad head, keel-like underside and long taper seen in the species silhouette.
        rings = [(-.62, .16, .12), (-.38, .20, .20), (.05, .15, .18), (.4, .065, .095), (.69, .018, .025)]
        vertices = [(math.cos(i * math.tau / 10) * w, y, z + math.sin(i * math.tau / 10) * h)
                    for y, w, h in rings for i in range(10)]
        faces = [tuple(reversed(range(10))), tuple(range(40, 50))]
        for r in range(4):
            for i in range(10): faces.append((r * 10 + i, r * 10 + (i + 1) % 10, (r + 1) * 10 + (i + 1) % 10, (r + 1) * 10 + i))
        mesh("Body", vertices, faces, skin)
        fin("LongAnalFin", [(0, -.12, .10), (0, .47, .1), (0, .55, .04), (0, .03, .015)], fins)
    else:
        ellipsoid("Body", (0, 0, z), (width, length, height), skin, 2)
        ellipsoid("Belly", (0, -.01, z - height * .5), (width * .83, length * .83, height * .42), pale, 1)
    eyes(width * (.96 if name == "Sengarat" else .7), -length * .77, z + height * .25, .027, ink, pale)
    if name == "Parrotfish":
        beak = material("ParrotBeak", (.77, .69, .48))
        ellipsoid("UpperBeak", (0, -.43, z + .025), (.11, .10, .10), beak, 1)
        ellipsoid("LowerBeak", (0, -.42, z - .07), (.10, .085, .045), beak, 1)
        for side in (-1, 1):
            for index in range(5):
                tube(f"CheekBand{side}_{index}", (side * .17, -.24 + index * .095, z + .1),
                     (side * .16, -.21 + index * .095, z - .05), .012, .009, fins, 4)
    elif name in ("Bass", "Tigerfish", "RedSnapper"):
        ellipsoid("Jaw", (0, -length * .86, z - .06), (width * .77, .16, .085), pale, 1)
        fin("MouthOpening", [(-width * .62, -length - .04, z), (width * .62, -length - .04, z),
             (width * .6, -length -.02, z - .06), (-width * .6, -length -.02, z - .06)], ink)
    if name == "Tigerfish":
        tooth = material("TigerTeeth", (.84, .81, .68))
        for side in (-1, 1):
            for index in range(7):
                y = -.32 + index * .11
                tube(f"TigerStripe{side}_{index}", (side * width * .91, y, z - .05),
                     (side * width * .75, y + .025, z + .13), .015, .006, ink, 4)
            for index in range(3):
                tube(f"Fang{side}_{index}", (side * (.027 + index * .025), -.58, z + .01),
                     (side * (.027 + index * .025), -.59, z - .04), .009, 0, tooth, 4)
    if name == "Bass":
        for side in (-1, 1):
            tube("LateralBand" + str(side), (side * width * .94, -.32, z), (side * width * .91, .30, z), .02, .013, fins, 5)
    if name == "FlyingFish":
        for side in (-1, 1):
            fin("GlidingWing" + str(side), [(side * .065, -.2, z), (side * .70, .04, z + .025),
                (side * .63, .46, z -.015), (side * .08, .26, z)], pale)
            for index in range(5):
                tube(f"FinRay{side}_{index}", (side * .075, -.15, z + .005),
                     (side * (.68 - index * .06), .055 + index * .085, z + .005), .004, .002, fins, 4)
    else:
        for side in (-1, 1):
            fin("Pectoral" + str(side), [(side * width * .7, -.18, z), (side * width * 2, .08, z - .12),
                (side * width * .75, .21, z - .1)], fins)
        if name != "Sengarat":
            points = [(0, -.25, z + height * .68)]
            for index in range(9): points.append((0, -.22 + index * .055, z + height * (1.5 if index % 2 == 0 else 1.05)))
            points.append((0, .27, z + height * .55))
            fin("SpinedDorsal", points, fins)
    tail(length * .92, z, height * .85, fins)
    empty("Grip")
    empty("Hook", (0, -length - .13, z))


def eel():
    skin = material("EelSkin", (.30, .29, .15))
    cream = material("EelBelly", (.61, .56, .35))
    ink = material("RockFishInk", (.024, .031, .035))
    points = [(math.sin(i * .5) * .09, -.75 + i * .16, .13) for i in range(11)]
    for i, (a, b) in enumerate(zip(points, points[1:])):
        radius = .09 * (1 - i / 12)
        tube("Segment" + str(i), a, b, radius, max(.012, radius - .007), skin, 8)
    ellipsoid("Head", (0, -.77, .13), (.09, .16, .085), skin, 1)
    eyes(.076, -.81, .16, .018, ink, cream)
    for i in range(9):
        a, b = points[i], points[i + 1]
        fin("RibbonFin" + str(i), [(a[0], a[1], .18), (b[0], b[1], .18),
            (b[0], b[1], .26 - i * .006), (a[0], a[1], .27 - i * .006)], cream)
    empty("Tail", points[-2])
    empty("Grip")
    empty("Hook", (0, -.95, .13))


def halibut():
    brown = material("HalibutTop", (.34, .29, .18))
    pale = material("HalibutUnderside", (.65, .60, .41))
    dark = material("RockFishInk", (.024, .031, .035))
    ellipsoid("Body", (0, 0, .12), (.39, .56, .10), brown, 2)
    ellipsoid("Underside", (0, 0, .075), (.36, .50, .045), pale, 1)
    for x in (-.05, .11):
        ellipsoid("RaisedEye", (x, -.36, .20), (.04, .042, .035), pale, 1)
        ellipsoid("Pupil", (x, -.37, .229), (.018, .025, .008), dark, 1)
    for side in (-1, 1):
        points = [(0, -.5, .1)]
        for i in range(11):
            angle = math.pi * i / 10
            points.append((side * math.sin(angle) * (.46 if i % 2 == 0 else .42), -.48 + i * .095, .095))
        fin("Fringe" + str(side), points, pale)
    pivot = empty("Tail", (0, .49, .10))
    fin("FlatTail", [(0, 0, 0), (-.17, .25, 0), (.17, .25, 0)], brown, pivot)
    empty("Grip")
    empty("Hook", (0, -.58, .1))


def voxelfish():
    red = material("VoxelRed", (.69, .20, .16))
    light = material("VoxelLight", (.82, .43, .35))
    pale = material("VoxelFin", (.85, .72, .63))
    black = material("RockFishInk", (.024, .031, .035))
    size = .075
    for x in range(-2, 3):
        for y in range(-4, 5):
            for z in range(-2, 3):
                if (x / 2.2) ** 2 + (y / 4.4) ** 2 + (z / 2.4) ** 2 > 1.15: continue
                box(f"BodyVoxel_{x}_{y}_{z}", (x * size, y * size, .23 + z * size), (size,) * 3, red if (x + y + z) % 3 else light)
    for side in (-1, 1):
        box("SquareEye" + str(side), (side * .16, -.19, .28), (.025, .06, .06), black)
        for i in range(3): box(f"SideFin{side}_{i}", (side * (.18 + i * .055), .02 + i * .03, .20), (.07, .11, .045), pale)
    pivot = empty("Tail", (0, .34, .23))
    for i in range(-2, 3): box("TailVoxel" + str(i), (0, .09 + abs(i) * .025, i * size), (.065, .10, size), pale, pivot)
    for i in range(4): box("TopFin" + str(i), (0, -.03 + i * .055, .43), (.045, .075, .075), pale)
    empty("Grip")
    empty("Hook", (0, -.4, .23))


def dripper():
    white = material("DripperShoes", (.82, .81, .74))
    red = material("DripperSoles", (.59, .07, .16))
    gray = material("DripperBody", (.20, .22, .23))
    ink = material("RockFishInk", (.024, .031, .035))
    for side in (-1, 1):
        x = side * .15
        ellipsoid("Sole" + str(side), (x, -.16, .045), (.12, .29, .045), red, 1)
        ellipsoid("Toe" + str(side), (x, -.23, .11), (.112, .20, .08), white, 1)
        tube("HighTop" + str(side), (x, .01, .10), (x, .04, .39), .105, .072, white, 6)
        box("Tongue" + str(side), (x, -.07, .28), (.07, .025, .20), white)
        for index in range(6):
            z = .13 + index * .035
            tube(f"Lace{side}_{index}", (x - .045, -.10, z), (x + .045, -.10, z + .024), .005, .005, ink, 4)
            tube(f"CrossLace{side}_{index}", (x + .045, -.10, z), (x - .045, -.10, z + .024), .005, .005, white, 4)
    ellipsoid("Body", (0, 0, .48), (.12, .30, .16), gray, 2)
    eyes(.103, -.2, .5, .024, ink, red)
    tail(.25, .48, .15, gray)
    fin("Dorsal", [(0, -.05, .6), (0, .02, .75), (0, .13, .61)], red)
    empty("Grip")
    empty("Hook", (0, -.34, .47))


def main():
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    recipes = {name: lambda name=name: ordinary(name) for name in ("Bass", "RedSnapper", "Parrotfish", "Tigerfish", "FlyingFish", "Sengarat")}
    recipes.update(Eel=eel, Halibut=halibut, Voxelfish=voxelfish, Dripper=dripper)
    for name in sys.argv[sys.argv.index("--") + 2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT = None
        art.ROOT = empty(name)
        recipes[name]()
        save_and_export(project, name)


if __name__ == "__main__":
    main()
