"""Original volcano encounter, camp and RHIB source meshes in the shared meter contract."""
import math
import sys
from pathlib import Path
import bpy
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, ellipsoid, tube, box
from fbx_export import save_and_export


def glow(name, color):
    result = material(name, color)
    node = next(value for value in result.node_tree.nodes if value.type == "BSDF_PRINCIPLED")
    node.inputs["Emission Color"].default_value = (*color, 1)
    node.inputs["Emission Strength"].default_value = 3
    return result


def fluke(name, color, parent=None, scale=1):
    points = [(-1.2, .65, .02), (-.62, .12, .08), (-.13, -.1, .05), (0, .23, .06),
              (.13, -.1, .05), (.62, .12, .08), (1.2, .65, .02), (.50, .52, -.02),
              (0, .45, -.015), (-.50, .52, -.02)]
    top = [tuple(v * scale for v in point) for point in points]
    bottom = [(x, y, z - .055 * scale) for x, y, z in top]
    count = len(top)
    faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)]
    mesh(name, top + bottom, faces, color, parent)


def whale(mutated=False):
    scale = 1.35 if mutated else 1
    point = lambda p: tuple(value * scale for value in p)
    skin = material("MutatedWhaleSkin" if mutated else "BowheadSkin", (.10, .09, .13) if mutated else (.16, .21, .28))
    pale = material("WhaleJaw", (.25, .29, .34))
    ink = material("WhaleEye", (.015, .02, .026))
    lava = glow("VolcanoLavaCrack", (1, .22, .018)) if mutated else None
    ellipsoid("Body", point((0, -.25, .94)), point((.78, 1.93, .86)), skin, 2)
    ellipsoid("Head", point((0, -1.55, 1)), point((.76, .95, .82)), skin, 2)
    ellipsoid("LowerJaw", point((0, -1.65, .54)), point((.68, .82, .31)), pale, 2)
    tube("TailPeduncle", point((0, 1.05, .8)), point((0, 2.50, .51)), .56 * scale, .13 * scale, skin, 10)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), point((side * .66, -1.92, 1.1)), point((.06, .08, .05)), lava or ink, 1)
        for i in range(4):
            y = -2.20 + i * .22
            tube(f"MouthLine{side}_{i}", point((side * (.44 + i * .05), y, .69)),
                 point((side * (.49 + i * .045), y + .19, .64)), .016 * scale, .012 * scale, ink, 5)
        pivot = empty("FlipperLeft" if side > 0 else "FlipperRight", point((side * .60, -.65, .54)))
        vertices = [(0, 0, 0), (side * .72, .20, -.11), (side * .84, .58, -.18),
                    (side * .40, .65, -.15), (0, .33, -.02)]
        mesh("FlipperSurface" + str(side), [point(v) for v in vertices], [(0, 1, 2, 3, 4), (4, 3, 2, 1, 0)], skin, pivot)
    tail = empty("Tail", point((0, 2.45, .53)))
    fluke("TailFluke", skin, tail, scale)
    hole = empty("Blowhole", point((0, -.40, 1.86)))
    ellipsoid("BlowholeRim", (0, 0, 0), point((.13, .19, .045)), lava or ink, 1, hole)
    if mutated:
        def skin_x(y, z):
            body = .78 * math.sqrt(max(0, 1 - ((y + .25) / 1.93) ** 2 - ((z - .94) / .86) ** 2))
            head = .76 * math.sqrt(max(0, 1 - ((y + 1.55) / .95) ** 2 - ((z - 1) / .82) ** 2))
            return max(body, head) + .01
        for side in (-1, 1):
            for i in range(8):
                y = -1.8 + i * .39
                a = point((side * skin_x(y, 1.2), y, 1.2))
                b = point((side * skin_x(y + .13, .92), y + .13, .92))
                c = point((side * skin_x(y + .24, 1.1), y + .24, 1.10))
                tube(f"CrackA{side}_{i}", a, b, .025 * scale, .018 * scale, lava, 5)
                tube(f"CrackB{side}_{i}", b, c, .018 * scale, .014 * scale, lava, 5)
    empty("Grip")
    empty("Hook", point((0, -2.53, .93)))


def whale_fin():
    skin = material("WhaleFinSkin", (.30, .31, .36))
    fluke("WhaleFluke", skin)
    empty("Grip")
    empty("Hook", (0, -.20, .06))


def scientist():
    yellow = material("ScientistSuit", (.78, .73, .14))
    light = material("ScientistZip", (.90, .87, .51))
    skin = material("ScientistSkin", (.61, .53, .43))
    brown = material("ScientistBoots", (.25, .22, .14))
    dark = material("ScientistHair", (.06, .055, .055))
    white = material("ScientistEyes", (.92, .91, .81))
    blue = material("ScientistMask", (.33, .68, .71))
    tube("Torso", (0, 0, .88), (0, 0, 1.45), .13, .17, yellow, 7)
    tube("Zip", (0, -.145, .91), (0, -.17, 1.47), .014, .014, light, 4)
    for side in (-1, 1):
        tube("Trouser" + str(side), (side * .075, 0, .94), (side * .11, 0, .15), .062, .04, yellow, 7)
        ellipsoid("Boot" + str(side), (side * .11, -.05, .10), (.065, .13, .105), brown, 1)
        tube("Sleeve" + str(side), (side * .14, 0, 1.43), (side * .21, -.015, .91), .057, .035, yellow, 7)
        ellipsoid("Hand" + str(side), (side * .215, -.02, .83), (.04, .029, .075), skin, 1)
        ellipsoid("HoodSide" + str(side), (side * .12, .01, 1.73), (.049, .12, .20), yellow, 1)
    ellipsoid("Face", (0, -.04, 1.74), (.11, .09, .17), skin, 1)
    ellipsoid("HoodTop", (0, .015, 1.9), (.14, .13, .055), yellow, 1)
    ellipsoid("Hair", (0, -.052, 1.86), (.112, .075, .052), dark, 1)
    box("Mask", (0, -.133, 1.70), (.19, .026, .09), blue)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), (side * .047, -.124, 1.8), (.039, .027, .04), white, 1)
        ellipsoid("Pupil" + str(side), (side * .047, -.151, 1.8), (.014,) * 3, dark, 1)
        tube("MaskStrap" + str(side), (side * .089, -.144, 1.72), (side * .105, -.03, 1.73), .004, .004, white, 4)
    empty("Grip")
    empty("FaceFront", (0, -.19, 1.77))


def bucket():
    gray = material("FishBucketSteel", (.69, .72, .69))
    dark = material("FishBucketHandle", (.19, .24, .27))
    water = material("FishBucketWater", (.14, .43, .51))
    fish = material("BucketFish", (.47, .21, .16))
    count = 12
    vertices = [(math.cos(i * math.tau / count) * radius, math.sin(i * math.tau / count) * radius, z)
                for radius, z in ((.135, .015), (.18, .27), (.162, .27), (.125, .03)) for i in range(count)]
    faces = []
    for ring in range(3):
        for i in range(count): faces.append((ring * count + i, ring * count + (i + 1) % count, (ring + 1) * count + (i + 1) % count, (ring + 1) * count + i))
    faces.append(tuple(range(36, 48)))
    faces.append(tuple(reversed(range(12))))
    mesh("Bucket", vertices, faces, gray)
    mesh("Water", [(math.cos(i * math.tau / count) * .153, math.sin(i * math.tau / count) * .153, .21) for i in range(count)], [tuple(range(count))], water)
    for i in range(3): ellipsoid("BaitFish" + str(i), ((i - 1) * .065, -.025, .22), (.028, .105, .032), fish, 1)
    for i in range(10):
        a, b = i * math.pi / 10, (i + 1) * math.pi / 10
        tube("Handle" + str(i), (.18 * math.cos(a), 0, .26 + .18 * math.sin(a)),
             (.18 * math.cos(b), 0, .26 + .18 * math.sin(b)), .009, .009, dark, 5)
    empty("Grip")
    empty("Forward", (0, -.2, .2))


def key():
    metal = material("MilitaryKeySteel", (.48, .53, .49))
    tag = material("MilitaryKeyTag", (.29, .33, .18))
    for i in range(10):
        a, b = i * math.tau / 10, (i + 1) * math.tau / 10
        tube("KeyRing" + str(i), (.035 * math.cos(a), .035 * math.sin(a), .01),
             (.035 * math.cos(b), .035 * math.sin(b), .01), .005, .005, metal, 5)
    box("KeyShaft", (0, -.095, .01), (.018, .12, .012), metal)
    for i in range(3): box("KeyTooth" + str(i), (.015, -.14 + i * .023, .01), (.028, .012, .012), metal)
    box("Tag", (-.055, .005, .01), (.045, .065, .015), tag)
    empty("Grip")
    empty("Forward", (0, -.17, .01))


def tent():
    canvas = material("MilitaryCanvas", (.23, .27, .16))
    seams = material("MilitaryTentSeam", (.13, .17, .12))
    interior = material("MilitaryTentInterior", (.16, .19, .12))
    arch = [(-1.6, .02), (-1.6, .85)] + [(math.cos(math.pi - i * math.pi / 8) * 1.6, .85 + math.sin(i * math.pi / 8) * 1.55) for i in range(1, 8)] + [(1.6, .85), (1.6, .02)]
    vertices = [(x, y, z) for y in (-2.1, 2.1) for x, z in arch]
    count = len(arch)
    faces = [(i, i + 1, i + 1 + count, i + count) for i in range(count - 1)]
    mesh("CanvasShell", vertices, faces + [tuple(reversed(face)) for face in faces], canvas)
    mesh("BackCanvas", [(x, 2.1, z) for x, z in arch], [tuple(range(count)), tuple(reversed(range(count)))], interior)
    for side in (-1, 1):
        box("DoorSide" + str(side), (side * 1.12, -2.1, .88), (.92, .04, 1.72), canvas)
        tube("DoorZip" + str(side), (side * .65, -2.135, .02), (side * .65, -2.135, 1.8), .011, .011, seams, 4)
    mesh("DoorArch", [(-1.6, -2.1, 1.72), (-.9, -2.1, 2.15), (0, -2.1, 2.4), (.9, -2.1, 2.15), (1.6, -2.1, 1.72)],
         [(0, 1, 2, 3, 4), (4, 3, 2, 1, 0)], canvas)
    for y in (-2.09, 0, 2.09):
        for i, (a, b) in enumerate(zip(arch, arch[1:])):
            tube("Seam" + str(y) + "_" + str(i), (a[0], y, a[1]), (b[0], y, b[1]), .016, .016, seams, 5)
    empty("Grip")
    empty("DoorFront", (0, -2.35, .85))


def crate():
    wood = material("SupplyCrateWood", (.31, .24, .16))
    light = material("SupplyCrateRim", (.42, .32, .20))
    metal = material("SupplyCrateClasp", (.15, .18, .17))
    for i in range(5):
        for side in (-1, 1): box(f"LongPlank{side}_{i}", (side * .56, 0, .08 + i * .15), (.08, .8, .14), wood)
        for side in (-1, 1): box(f"EndPlank{side}_{i}", (0, side * .4, .08 + i * .15), (1.16, .07, .14), wood)
    for i in range(5): box("LidPlank" + str(i), (-.46 + i * .23, 0, .80), (.22, .85, .065), light)
    for x in (-.43, .43): box("Brace" + str(x), (x, -.45, .41), (.10, .035, .8), light)
    box("Clasp", (0, -.46, .68), (.11, .04, .16), metal)
    empty("Grip")
    empty("Forward", (0, -.49, .4))


def plank():
    wood = material("VolcanoWalkwayWood", (.31, .21, .14))
    edge = material("VolcanoWalkwayBrace", (.18, .12, .095))
    for i in range(6): box("Board" + str(i), (-1.175 + i * .47, 0, 0), (.455, 1.05, .10), wood)
    for y in (-.32, .32): box("UnderBrace" + str(y), (0, y, -.075), (2.9, .09, .08), edge)
    empty("Grip")
    empty("Forward", (0, -.56, 0))


def assault_rifle():
    steel = material("AssaultSteel", (.18, .20, .21))
    black = material("AssaultPolymer", (.075, .095, .08))
    light = material("AssaultSight", (.48, .51, .49))
    box("Receiver", (0, -.19, .035), (.075, .40, .11), steel)
    tube("Barrel", (0, -.37, .04), (0, -.87, .04), .024, .014, steel, 10)
    box("HandGuard", (0, -.48, .03), (.085, .24, .09), black)
    for i in range(6): box("TopRail" + str(i), (0, -.41 + i * .08, .106), (.07, .03, .025), black)
    box("RearSight", (0, -.07, .15), (.055, .024, .072), steel)
    box("FrontSight", (0, -.64, .14), (.016, .018, .065), light)
    tube("BufferTube", (0, .01, .045), (0, .26, .045), .028, .026, steel, 8)
    box("Stock", (0, .20, .0), (.066, .20, .10), black)
    box("ButtPlate", (0, .30, -.025), (.078, .032, .17), black)
    tube("PistolGrip", (0, .005, .005), (0, .052, -.16), .033, .027, black, 6)
    mag = empty("Magazine", (0, -.18, -.018))
    vertices = [(x, y, z) for x in (-.027, .027) for y, z in ((-.06, 0), (.065, 0), (.04, -.22), (-.105, -.20))]
    mesh("MagazineBody", vertices, [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], black, mag)
    bolt = empty("Bolt", (-.042, -.15, .066))
    box("BoltHandle", (-.013, 0, 0), (.025, .045, .014), light, bolt)
    empty("Grip")
    empty("Muzzle", (0, -.9, .04))


def scientific_lure():
    pale = material("ScientificLureBody", (.70, .76, .73))
    green = glow("ScientificLureGlow", (.17, .65, .37))
    metal = material("ScientificLureHook", (.28, .34, .35))
    tube("LureBody", (0, -.1, .04), (0, .09, .04), .03, .025, pale, 8)
    for y in (-.06, 0, .06): tube("GlowRing" + str(y), (0, y -.009, .04), (0, y + .009, .04), .032, .032, green, 8)
    for side in (-1, 1):
        tube("Hook" + str(side), (0, .07, .025), (side * .04, .14, .015), .004, .003, metal, 5)
        tube("HookTip" + str(side), (side * .04, .14, .015), (side * .045, .1, .015), .003, 0, metal, 5)
    empty("Grip")
    empty("Forward", (0, -.12, .04))


def lava_effect(pool=False):
    orange = glow("VolcanoLavaOrange", (1, .18, .012))
    yellow = glow("VolcanoLavaYellow", (1, .56, .035))
    dark = material("VolcanoLavaCrust", (.20, .055, .035))
    if pool:
        points = [(math.cos(i * math.tau / 16) * (1.4 + math.sin(i * 2.1) * .12), math.sin(i * math.tau / 16) * 1.4, .025) for i in range(16)]
        mesh("LavaSurface", [(0, 0, .04)] + points, [(0, i + 1, (i + 1) % 16 + 1) for i in range(16)], orange)
        for i in range(9):
            angle = i * 2.4
            ellipsoid("MoltenPatch" + str(i), (math.sin(angle) * .9, math.cos(angle) * .9, .035), (.24, .18, .035), yellow if i % 2 else dark, 1)
    else:
        ellipsoid("LavaCore", (0, 0, 0), (.19, .17, .23), orange, 1)
        for i in range(5): ellipsoid("Crust" + str(i), (math.cos(i * 1.3) * .15, math.sin(i * 1.3) * .13, .03), (.075, .07, .1), dark, 1)
    empty("Grip")
    empty("Forward", (0, -1.55 if pool else -.25, .03))


def military_boat():
    rubber = material("RHIBRubber", (.10, .13, .15))
    hull = material("RHIBHull", (.17, .20, .20))
    deck = material("RHIBDeck", (.25, .27, .25))
    steel = material("RHIBSteel", (.48, .53, .52))
    black = material("RHIBSeats", (.07, .085, .09))
    outline = [(-1.12, 3), (-1.30, 1.8), (-1.27, -2.05), (-.87, -3.05), (0, -3.65), (.87, -3.05), (1.27, -2.05), (1.30, 1.8), (1.12, 3)]
    vertices = [(x, y, .1) for x, y in outline] + [(x * .7, y * .9, -.37) for x, y in outline]
    count = len(outline)
    faces = [(i + count, (i + 1) % count + count, (i + 1) % count, i) for i in range(count)]
    faces += [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    mesh("Hull", vertices, faces, hull)
    for i in range(8): box("Deck" + str(i), (-.875 + i * .25, .2, .15), (.24, 5.3, .035), deck)
    for i in range(len(outline) - 1):
        a, b = outline[i], outline[i + 1]
        tube("InflatableTube" + str(i), (a[0], a[1], .35), (b[0], b[1], .35), .32, .32, rubber, 10)
    for side in (-1, 1):
        for i, y in enumerate((-2.0, -1.15, 1.25, 2.1)):
            x = side * .85
            tube(f"SeatPost{side}_{i}", (x, y, .16), (x, y, .60), .025, .025, steel, 6)
            box(f"SeatCushion{side}_{i}", (x, y, .61), (.37, .35, .07), black)
            box(f"SeatBack{side}_{i}", (x, y + .16, .8), (.37, .055, .32), black)
        for i in range(6):
            y = -1.8 + i * .7
            tube(f"SafetyRope{side}_{i}", (side * 1.58, y, .38), (side * 1.58, y + .5, .38), .012, .012, steel, 5)
        tube("SternArchSide" + str(side), (side * 1.12, 2.85, .25), (side * 1.12, 2.64, 1.72), .048, .048, steel, 8)
        motor = empty("MotorLeft" if side > 0 else "MotorRight", (side * .62, 3.24, .22))
        ellipsoid("MotorCover" + str(side), (0, .02, .14), (.27, .30, .34), rubber, 1, motor)
        box("MotorLeg" + str(side), (0, .1, -.36), (.12, .14, .48), hull, motor)
        tube("PropellerHub" + str(side), (0, .11, -.57), (0, .30, -.57), .05, .035, steel, 7, motor)
        box("PropellerBlade" + str(side), (0, .28, -.57), (.32, .022, .045), black, motor)
    tube("SternArchTop", (-1.12, 2.64, 1.72), (1.12, 2.64, 1.72), .048, .048, steel, 8)
    for x in (-.65, .65): ellipsoid("NavigationLight", (x, 2.64, 1.82), (.09, .10, .09), black, 1)
    box("Console", (0, -.15, .64), (.75, .62, .96), hull)
    box("Windshield", (0, -.36, 1.26), (.76, .045, .27), deck)
    wheel = empty("SteeringWheel", (0, .22, 1.06))
    for i in range(10):
        a, b = math.tau * i / 10, math.tau * (i + 1) / 10
        tube("WheelRim" + str(i), (.24 * math.cos(a), 0, .24 * math.sin(a)), (.24 * math.cos(b), 0, .24 * math.sin(b)), .021, .021, black, 6, wheel)
    for i in range(3):
        a = i * math.tau / 3
        tube("WheelSpoke" + str(i), (0, 0, 0), (.22 * math.cos(a), 0, .22 * math.sin(a)), .012, .012, steel, 5, wheel)
    empty("DriverSeat", (0, .9, .2))
    empty("ExitPoint", (0, 1.6, .28))
    empty("Grip")
    empty("Bow", (0, -3.7, .38))


def main():
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    recipes = dict(BowheadWhale=whale, MutatedBowheadWhale=lambda: whale(True), WhaleFin=whale_fin,
                   Scientist=scientist, FishBucket=bucket, MilitaryBoatKey=key, MilitaryTent=tent,
                   SupplyCrate=crate, VolcanoPlank=plank, AssaultRifle=assault_rifle,
                   ScientificLure=scientific_lure, LavaGlob=lambda: lava_effect(False),
                   LavaPool=lambda: lava_effect(True), MilitaryBoat=military_boat)
    for name in sys.argv[sys.argv.index("--") + 2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT = None
        art.ROOT = empty(name)
        recipes[name]()
        save_and_export(project, name)


if __name__ == "__main__":
    main()
