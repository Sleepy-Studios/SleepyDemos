"""Rocks island source models: original geometry, shared validated FBX coordinates."""
import math
import sys
from pathlib import Path
import bpy
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, ellipsoid, tube, box
from fbx_export import save_and_export


def tuna():
    blue = material("TunaBack", (.19, .31, .41))
    silver = material("TunaBelly", (.64, .72, .74))
    fin = material("TunaFin", (.45, .42, .20))
    ink = material("TunaEye", (.025, .035, .04))
    ellipsoid("Body", (0, 0, .48), (.38, 1.1, .46), blue, 2)
    ellipsoid("Belly", (0, -.10, .30), (.33, .95, .22), silver, 2)
    tube("Snout", (0, -.65, .47), (0, -1.22, .47), .28, .09, blue, 7)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), (side * .21, -.91, .59), (.055,) * 3, silver, 1)
        ellipsoid("Pupil" + str(side), (side * .24, -.94, .59), (.028,) * 3, ink, 1)
        mesh("Pectoral" + str(side), [(side * .30, -.50, .40), (side * .67, .20, .28), (side * .32, .1, .35)], [(0, 1, 2), (2, 1, 0)], fin)
    mesh("Dorsal", [(0, -.25, .87), (0, .03, 1.18), (0, .40, .85)], [(0, 1, 2), (2, 1, 0)], fin)
    for i in range(4):
        mesh("Finlet" + str(i), [(0, .43 + i * .14, .71 - i * .08), (0, .47 + i * .14, .85 - i * .08), (0, .54 + i * .14, .66 - i * .07)], [(0, 1, 2), (2, 1, 0)], fin)
    tail = empty("Tail", (0, 1.0, .48))
    mesh("CrescentTail", [(0, 0, 0), (0, .35, .57), (0, .22, .05), (0, .38, -.51), (0, -.03, -.07)],
         [(0, 1, 2), (0, 2, 3, 4), (2, 1, 0), (4, 3, 2, 0)], fin, tail)
    empty("Grip", (0, 0, .45))
    empty("Hook", (0, -1.24, .47))


def albatross(head_only=False):
    white = material("AlbatrossFeathers", (.88, .87, .78))
    dark = material("AlbatrossWingEdge", (.12, .13, .14))
    gold = material("AlbatrossBeak", (.88, .54, .10))
    eye = material("AlbatrossEye", (.025, .024, .035))
    neck = empty("Head", (0, 0, .16) if head_only else (0, -.53, .45))
    ellipsoid("Skull", (0, -.12, .13), (.15, .25, .18), white, 1, neck)
    tube("UpperBeak", (0, -.28, .10), (0, -.66, .07), .078, .015, gold, 6, neck)
    tube("LowerBeak", (0, -.27, .04), (0, -.61, .025), .048, .008, gold, 5, neck)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), (side * .132, -.23, .18), (.025,) * 3, eye, 1, neck)
    if not head_only:
        ellipsoid("Body", (0, .04, .35), (.29, .70, .32), white, 2)
        for side in (-1, 1):
            wing = empty("WingLeft" if side > 0 else "WingRight", (side * .2, -.1, .48))
            vertices = [(0, 0, 0), (side * .9, -.1, .03), (side * 2.0, .30, -.02),
                        (side * 1.7, .59, -.01), (side * .8, .42, -.02), (0, .34, 0)]
            faces = [(0, 1, 4, 5), (1, 2, 3, 4)]
            mesh("WingSurface" + str(side), vertices, faces + [tuple(reversed(face)) for face in faces], white, wing)
            for i in range(7):
                x = .75 + i * .17
                mesh("FlightFeather" + str(side) + "_" + str(i), [(side * x, .23 + i * .028, -.025),
                     (side * (x + .14), .28 + i * .028, -.025), (side * (x + .22), .66 + i * .018, -.03),
                     (side * x, .60 + i * .018, -.03)], [(0, 1, 2, 3), (3, 2, 1, 0)], dark, wing)
            tube("Leg" + str(side), (side * .12, .30, .17), (side * .13, .57, .04), .025, .019, gold)
            mesh("WebFoot" + str(side), [(side * .13, .54, .04), (side * .23, .76, .025), (side * .04, .76, .025)], [(0, 1, 2), (2, 1, 0)], gold)
        mesh("Tail", [(-.13, .55, .31), (.13, .55, .31), (.28, .99, .30), (-.28, .99, .30)], [(0, 1, 2, 3), (3, 2, 1, 0)], dark)
    empty("Grip")
    empty("Hook", (0, -.7 if head_only else -1.22, .26 if head_only else .53))


def islander():
    red = material("IslanderShirt", (.75, .11, .055))
    blue = material("IslanderTrousers", (.10, .12, .36))
    skin = material("IslanderSkin", (.67, .45, .24))
    hair = material("IslanderHair", (.35, .22, .095))
    white = material("IslanderEye", (.96, .90, .73))
    dark = material("IslanderPupil", (.03, .025, .025))
    tube("Shirt", (0, 0, .89), (0, 0, 1.43), .13, .17, red, 7)
    tube("Neck", (0, 0, 1.42), (0, 0, 1.62), .048, .05, skin)
    ellipsoid("Head", (0, 0, 1.72), (.115, .105, .18), skin, 1)
    ellipsoid("Hair", (0, .025, 1.82), (.119, .10, .1), hair, 1)
    for side in (-1, 1):
        tube("Leg" + str(side), (side * .07, 0, .93), (side * .11, 0, .08), .065, .035, blue)
        ellipsoid("Shoe" + str(side), (side * .11, -.065, .06), (.055, .13, .05), dark, 1)
        tube("Sleeve" + str(side), (side * .13, 0, 1.43), (side * .21, 0, 1.24), .065, .058, red)
        tube("Arm" + str(side), (side * .21, 0, 1.24), (side * .19, -.02, .76), .035, .026, skin)
        ellipsoid("Hand" + str(side), (side * .19, -.02, .73), (.033, .025, .065), skin, 1)
        ellipsoid("Eye" + str(side), (side * .056, -.096, 1.75), (.044, .028, .044), white, 1)
        ellipsoid("Pupil" + str(side), (side * .056, -.123, 1.75), (.014,) * 3, dark, 1)
    empty("Grip")
    empty("Face", (0, -.17, 1.72))


def sniper():
    steel = material("SniperSteel", (.14, .15, .16))
    wood = material("SniperStock", (.31, .20, .12))
    black = material("SniperRubber", (.035, .035, .04))
    box("Receiver", (0, -.21, .06), (.072, .40, .09), steel)
    tube("Barrel", (0, -.37, .06), (0, -1.02, .06), .025, .016, steel, 10)
    box("HandGuard", (0, -.48, .005), (.075, .32, .05), wood)
    box("Stock", (0, .17, .0), (.065, .30, .09), wood)
    box("Butt", (0, .31, -.03), (.076, .035, .16), black)
    tube("GripHandle", (0, .035, .02), (0, .08, -.15), .032, .027, wood)
    for side in (-1,1): box("RearSight"+str(side),(side*.018,-.055,.13),(.009,.02,.025),steel)
    box("FrontSight",(0,-.91,.118),(.008,.02,.025),steel)
    mag = empty("Magazine", (0, -.20, .01))
    box("MagazineBody", (0, 0, -.06), (.047, .085, .12), steel, mag)
    bolt = empty("Bolt", (-.04, -.09, .08))
    tube("BoltHandle", (0, 0, 0), (-.065, .035, -.03), .011, .011, steel, 5, bolt)
    empty("Grip")
    empty("Muzzle", (0, -1.04, .06))


def shop():
    red = material("RocksRedSiding", (.43, .09, .075))
    wood = material("RocksWood", (.33, .24, .17))
    cream = material("RocksWindowTrim", (.78, .70, .49))
    roof = material("RocksRoofTile", (.36, .20, .105))
    for i in range(20): box("FloorPlank" + str(i), (-2.375 + i * .25, 0, .07), (.24, 4.2, .14), wood)
    for side in (-1, 1):
        for i in range(17):
            y = -2 + i * .25
            if -1.3 < y < -.3:
                box(f"WindowSillWall{side}_{i}", (side * 2.5, y, .55), (.13, .24, 1.0), red)
                box(f"WindowUpperWall{side}_{i}", (side * 2.5, y, 2.55), (.13, .24, .6), red)
            else: box(f"SideWall{side}_{i}", (side * 2.5, y, 1.45), (.13, .24, 2.8), red)
        for z in (1.05, 2.25): box(f"WindowHorizontal{side}_{z}", (side * 2.52, -.8, z), (.18, 1.25, .09), cream)
        for y in (-1.42, -.8, -.18): box(f"WindowVertical{side}_{y}", (side * 2.52, y, 1.65), (.18, .07, 1.3), cream)
    for i in range(21):
        x = -2.5 + i * .25
        box("BackWall" + str(i), (x, 2.07, 1.45), (.24, .13, 2.8), red)
        if abs(x) > .75: box("FrontWall" + str(i), (x, -2.07, 1.45), (.24, .13, 2.8), red)
    for x in (-.8, .8): box("DoorPost", (x, -2.1, 1.15), (.14, .20, 2.3), cream)
    box("DoorHeader", (0, -2.1, 2.35), (1.75, .2, .16), cream)
    for side in (-1, 1):
        # Each tile is a closed wedge so the editor can give the roof a real cover collider.
        for i in range(11):
            y = -2.3 + i * .44
            vertices = [(0, y, 3.75), (side * 2.85, y, 2.8), (side * 2.85, y + .43, 2.8), (0, y + .43, 3.75),
                        (0, y, 3.68), (side * 2.85, y, 2.73), (side * 2.85, y + .43, 2.73), (0, y + .43, 3.68)]
            faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (3, 2, 6, 7), (1, 5, 6, 2), (0, 3, 7, 4)]
            mesh(f"RoofTile{side}_{i}", vertices, [tuple(reversed(face)) for face in faces] if side < 0 else faces, roof)
    for i in range(3): box("Shelf" + str(i), (-2.25, .8, .8 + i * .62), (.42, 1.6, .075), wood)
    box("Counter", (.65, .7, .95), (2.2, .65, .12), wood)
    for x in (-.25, 1.55): box("CounterLeg", (x, .7, .5), (.09, .5, .9), wood)
    empty("Grip")
    empty("DoorFront", (0, -2.5, .1))


def lure():
    green = material("ProfessionalLure", (.20, .39, .13))
    silver = material("ProfessionalHook", (.50, .53, .47))
    ellipsoid("LureBody", (0, -.03, .035), (.04, .10, .04), green, 1)
    for side in (-1, 1):
        tube("Treble" + str(side), (0, .03, .01), (side * .035, .10, .01), .004, .003, silver)
        tube("Tip" + str(side), (side * .035, .10, .01), (side * .04, .065, .01), .003, 0, silver)
    empty("Grip")
    empty("Forward", (0, -.15, .03))


def dropping():
    chalk = material("BirdDropping", (.82, .81, .72))
    ellipsoid("Drop", (0, 0, .12), (.11, .11, .16), chalk, 1)
    ellipsoid("Splash", (0, -.02, .055), (.16, .13, .05), chalk, 1)
    empty("Grip")
    empty("Forward", (0, -.15, .05))


def main():
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    recipes = dict(Tuna=tuna, Albatross=albatross, AlbatrossHead=lambda: albatross(True), Islander=islander,
                   SniperRifle=sniper, RocksShop=shop, ProfessionalLure=lure, BirdDropping=dropping)
    for name in sys.argv[sys.argv.index("--") + 2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT = None
        art.ROOT = empty(name)
        recipes[name]()
        save_and_export(project, name)


if __name__ == "__main__":
    main()
