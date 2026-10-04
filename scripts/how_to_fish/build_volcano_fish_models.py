"""Scientific-lure fish and the volcano ground snail, authored without source-game meshes."""
import math
import sys
from pathlib import Path
import bpy
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, ellipsoid, tube
from build_rocks_fish_models import fin, eyes
from build_volcano_models import glow
from fbx_export import save_and_export


def small_tail(y, z, height, length, color):
    pivot = empty("Tail", (0, y, z))
    fin("TailFin", [(0, 0, 0), (0, length, height), (0, length * .65, 0), (0, length, -height)], color, pivot)


def blobfish():
    pink = material("BlobfishSkin", (.71, .48, .43))
    pale = material("BlobfishLips", (.78, .55, .49))
    dark = material("DeepFishInk", (.035, .031, .029))
    ellipsoid("Body", (0, .04, .23), (.25, .36, .22), pink, 2)
    ellipsoid("Head", (0, -.18, .28), (.29, .24, .25), pink, 2)
    ellipsoid("DroopingNose", (0, -.397, .30), (.069, .075, .09), pale, 1)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), (side * .15, -.37, .39), (.029, .018, .028), dark, 1)
        tube("Lip" + str(side), (0, -.411, .15), (side * .17, -.36, .19), .025, .018, pale, 6)
        fin("Pectoral" + str(side), [(side * .22, -.02, .2), (side * .40, .19, .055), (side * .22, .28, .08)], pale)
    small_tail(.34, .18, .12, .18, pink)
    empty("Grip")
    empty("Hook", (0, -.48, .22))


def anglerfish():
    brown = material("AnglerfishSkin", (.26, .25, .17))
    black = material("DeepFishInk", (.035, .031, .029))
    ivory = material("AnglerfishTeeth", (.74, .69, .47))
    light = glow("AnglerfishGlow", (.51, .82, .27))
    ellipsoid("Body", (0, .01, .34), (.30, .38, .31), brown, 2)
    ellipsoid("Mouth", (0, -.33, .27), (.24, .069, .24), black, 2)
    eyes(.22, -.21, .5, .052, black, ivory)
    for i in range(14):
        angle = math.tau * i / 14
        x, z = math.cos(angle) * .22, .27 + math.sin(angle) * .21
        tube("Tooth" + str(i), (x, -.392, z), (x * .65, -.417, .27 + (z - .27) * .57), .014, 0, ivory, 5)
    points = [(0, -.04, .62), (0, -.08, .82), (0, -.31, .9), (0, -.46, .76)]
    for i, (a, b) in enumerate(zip(points, points[1:])): tube("LureStalk" + str(i), a, b, .014, .011, brown, 6)
    ellipsoid("LureLight", points[-1], (.055,) * 3, light, 1)
    for side in (-1, 1):
        fin("Pectoral" + str(side), [(side * .25, .04, .25), (side * .48, .30, .12), (side * .20, .34, .18)], brown)
    small_tail(.36, .25, .19, .25, brown)
    empty("Grip")
    empty("Hook", (0, -.45, .3))


def oarfish():
    silver = material("OarfishSilver", (.67, .70, .69))
    red = material("OarfishFins", (.66, .16, .17))
    black = material("DeepFishInk", (.035, .031, .029))
    rings = []
    for i in range(15):
        y, h = -.7 + i * .18, .13 * (1 - i / 18)
        z = .23 + math.sin(i * .25) * .045
        rings.append((y, z, h))
    vertices = [(x, y, z + dz * h) for y, z, h in rings for x, dz in ((-.027, -1), (.027, -1), (.027, 1), (-.027, 1))]
    faces = [(3, 2, 1, 0), tuple(range(56, 60))]
    for r in range(14):
        for i in range(4): faces.append((r * 4 + i, r * 4 + (i + 1) % 4, (r + 1) * 4 + (i + 1) % 4, (r + 1) * 4 + i))
    mesh("Body", vertices, faces, silver)
    for i in range(14):
        a, b = rings[i], rings[i + 1]
        fin("DorsalRibbon" + str(i), [(0, a[0], a[1] + a[2]), (0, b[0], b[1] + b[2]),
            (0, b[0], b[1] + b[2] + .055), (0, a[0], a[1] + a[2] + .09)], red)
    ellipsoid("Head", (0, -.75, .25), (.05, .13, .12), silver, 1)
    eyes(.041, -.79, .3, .022, black, silver)
    for i in range(3):
        a = (0, -.75 + i * .035, .35)
        b = (0, -.78 + i * .08, .58 + i * .04)
        c = (0, -.45 + i * .15, .55 + i * .045)
        tube("CrestStem" + str(i), a, b, .008, .006, red, 5)
        tube("CrestTip" + str(i), b, c, .006, 0, red, 5)
    empty("Tail", (0, rings[-1][0], rings[-1][1]))
    empty("Grip")
    empty("Hook", (0, -.91, .25))


def stonefish():
    brown = material("StonefishSkin", (.39, .34, .25))
    moss = material("StonefishMottle", (.29, .34, .19))
    black = material("DeepFishInk", (.035, .031, .029))
    amber = material("StonefishEye", (.56, .46, .24))
    ellipsoid("Body", (0, .02, .2), (.29, .35, .19), brown, 2)
    for i in range(12):
        angle = i * 2.4
        ellipsoid("Wart" + str(i), (math.sin(angle) * .22, math.cos(angle) * .27, .27), (.085, .085, .074), moss if i % 2 else brown, 1)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), (side * .13, -.22, .35), (.045, .047, .033), amber, 1)
        ellipsoid("Pupil" + str(side), (side * .13, -.252, .364), (.017, .018, .012), black, 1)
        fin("Pectoral" + str(side), [(side * .23, -.17, .12), (side * .48, -.08, .045),
             (side * .42, .20, .045), (side * .19, .27, .11)], moss)
    for i in range(7):
        y = -.14 + i * .065
        tube("DorsalSpine" + str(i), (0, y, .34), (0, y + .02, .51 - i * .009), .023, 0, brown, 5)
    tube("Mouth", (-.12, -.306, .17), (.12, -.306, .17), .018, .018, black, 6)
    small_tail(.32, .15, .10, .17, brown)
    empty("Grip")
    empty("Hook", (0, -.38, .17))


def superdwarf():
    orange = material("SuperdwarfOrange", (.87, .36, .05))
    pale = material("SuperdwarfBelly", (.94, .62, .21))
    black = material("DeepFishInk", (.035, .031, .029))
    ellipsoid("Body", (0, 0, .02), (.013, .05, .017), orange, 1)
    ellipsoid("Belly", (0, -.004, .011), (.010, .038, .008), pale, 1)
    eyes(.010, -.034, .026, .0045, black, pale)
    small_tail(.045, .02, .018, .026, orange)
    fin("Dorsal", [(0, -.007, .034), (0, .008, .055), (0, .026, .031)], orange)
    empty("Grip")
    empty("Hook", (0, -.06, .02))


def foot_snail():
    shell = material("FootSnailShell", (.10, .115, .105))
    foot = material("FootSnailFoot", (.38, .095, .075))
    ellipsoid("Shell", (0, .025, .135), (.10, .15, .13), shell, 2)
    ellipsoid("Body", (0, -.02, .04), (.105, .18, .038), foot, 2)
    for side in (-1, 1):
        for i in range(6):
            y = -.1 + i * .041
            fin(f"FootScale{side}_{i}", [(side * .08, y, .037), (side * .125, y + .018, .025),
                 (side * .10, y + .038, .025)], shell)
    empty("Grip")
    empty("Hook", (0, -.21, .04))


def main():
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    recipes = dict(Blobfish=blobfish, Anglerfish=anglerfish, Oarfish=oarfish, Stonefish=stonefish,
                   SuperdwarfFish=superdwarf, FootSnail=foot_snail)
    for name in sys.argv[sys.argv.index("--") + 2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT = None
        art.ROOT = empty(name)
        recipes[name]()
        save_and_export(project, name)


if __name__ == "__main__":
    main()
