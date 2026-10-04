"""Self-authored desert candidates using the verified shared meter/axis exporter."""
import math
import sys
from pathlib import Path
import bpy
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, ellipsoid, tube, box
from fbx_export import save_and_export


def fish(name):
    ink = material("DesertInk", (.025, .035, .045))
    white = material("FishEye", (.92, .87, .67))
    colors = {"Needlefish": (.27, .48, .40), "Seahorse": (.83, .34, .46),
              "YellowBoxfish": (.76, .63, .19), "BlueShark": (.14, .32, .53), "Pufferfish": (.62, .59, .25)}
    skin = material(name + "Skin", colors[name])
    fin = material(name + "Fin", (.49, .21, .13) if name == "Pufferfish" else tuple(c * .7 for c in colors[name]))
    parent = None
    if name == "Pufferfish":
        parent = empty("Ball", (0, 0, .9))
        ellipsoid("SpinyBody", (0, 0, 0), (.88, .86, .9), skin, 2, parent)
        for ring in range(1, 6):
            latitude = math.pi * ring / 6
            for i in range(12):
                longitude = math.tau * (i + ring * .5) / 12
                direction = (math.sin(latitude) * math.cos(longitude), math.sin(latitude) * math.sin(longitude), math.cos(latitude))
                tube(f"Spine{ring}_{i}", tuple(v * .83 for v in direction), tuple(v * 1.02 for v in direction), .045, 0, fin, 5, parent)
        eye_y, eye_z, eye_x, eye_r = -.72, .27, .39, .15
        tail_y, tail_z, width = .84, 0, .3
        hook = (0, -.93, 0)
    elif name == "BlueShark":
        parent = empty("RollPivot", (0, 0, .5))
        ellipsoid("StreamlinedBody", (0, 0, 0), (.34, 1.15, .36), skin, 2, parent)
        ellipsoid("PaleBelly", (0, -.18, -.19), (.29, .87, .17), white, 2, parent)
        tube("PointedSnout", (0, -.65, -.02), (0, -1.5, -.05), .27, .065, skin, 7, parent)
        mesh("DorsalFin", [(0, -.2, .25), (0, .23, 1.0), (0, .48, .22)], [(0, 1, 2), (2, 1, 0)], fin, parent)
        for side in (-1, 1):
            mesh("Pectoral" + str(side), [(side * .21, -.45, -.07), (side * 1.0, .25, -.16), (side * .21, .36, -.1)], [(0, 1, 2), (2, 1, 0)], fin, parent)
            for gill in range(4):
                tube(f"Gill{side}_{gill}", (side * .33, -.42 + gill * .07, .12), (side * .30, -.43 + gill * .07, -.13), .01, .01, ink, 4, parent)
        eye_y, eye_z, eye_x, eye_r = -.9, .06, .21, .055
        tail_y, tail_z, width = 1.08, 0, .54
        hook = (0, -1.53, -.08)
    elif name == "Seahorse":
        ellipsoid("UprightBody", (0, .015, .26), (.085, .11, .18), skin)
        tube("BentNeck", (0, .02, .34), (0, -.025, .47), .058, .05, skin)
        ellipsoid("HorseHead", (0, -.06, .48), (.06, .085, .06), skin)
        tube("LongSnout", (0, -.11, .465), (0, -.22, .45), .027, .018, skin)
        for i in range(12):
            a, b = i * math.pi / 8, (i + 1) * math.pi / 8
            tube("CurledTail" + str(i), (0, .07 + math.sin(a) * .065, .105 + math.cos(a) * .065),
                 (0, .07 + math.sin(b) * .065, .105 + math.cos(b) * .065), .022 - i * .0014, .021 - i * .0014, skin, 5)
        for i in range(7):
            tube("BackSpine" + str(i), (0, .105, .15 + i * .035), (0, .15, .15 + i * .035), .014, 0, fin, 4)
        mesh("BackFin", [(0, .11, .22), (0, .2, .32), (0, .1, .34)], [(0, 1, 2), (2, 1, 0)], fin)
        eye_y, eye_z, eye_x, eye_r = -.09, .5, .048, .018
        tail_y = None
        hook = (0, -.23, .45)
    elif name == "YellowBoxfish":
        body = box("BoxBody", (0, 0, .18), (.25, .30, .25), skin)
        bpy.context.view_layer.objects.active = body
        bevel = body.modifiers.new("CarapaceCorners", "BEVEL")
        bevel.width = .035
        bevel.segments = 1
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        ellipsoid("Lips", (0, -.17, .15), (.045, .032, .03), fin, 1)
        for side in (-1, 1):
            for i in range(3):
                for j in range(3):
                    ellipsoid(f"Spot{side}_{i}_{j}", (side * .127, -.1 + i * .1, .09 + j * .085), (.006, .015, .015), ink, 1)
        eye_y, eye_z, eye_x, eye_r = -.12, .26, .107, .029
        tail_y, tail_z, width = .15, .18, .1
        hook = (0, -.21, .15)
    else:
        ellipsoid("NeedleBody", (0, 0, .10), (.042, .43, .047), skin)
        tube("LongBeak", (0, -.36, .10), (0, -.69, .10), .026, .002, fin)
        tube("LateralStripe", (-.04, -.3, .11), (-.033, .32, .11), .005, .003, white, 4)
        eye_y, eye_z, eye_x, eye_r = -.31, .13, .034, .015
        tail_y, tail_z, width = .41, .10, .072
        hook = (0, -.7, .1)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), (side * eye_x, eye_y, eye_z), (eye_r,) * 3, white, 1, parent)
        ellipsoid("Pupil" + str(side), (side * eye_x, eye_y - eye_r * .8, eye_z), (eye_r * .5,) * 3, ink, 1, parent)
    if tail_y is not None:
        tail = empty("Tail", (0, tail_y, tail_z), parent)
        mesh("TailFan", [(0, 0, 0), (0, width, width), (.02, width * .7, 0), (0, width, -width)], [(0, 1, 2), (0, 2, 3), (2, 1, 0), (3, 2, 0)], fin, tail)
    empty("Hook", hook, parent)
    empty("Grip")


def bowlfish():
    glass = material("BowlGlass", (.58, .71, .92))
    glass.diffuse_color = (.58, .71, .92, .28)
    shader = next(node for node in glass.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Alpha"].default_value = .28
    glass.surface_render_method = "DITHERED"
    rim = material("BowlRim", (.83, .85, .78))
    orange = material("BowlResident", (.83, .28, .10))
    dark = material("BowlEye", (.03, .03, .04))
    rings = [(0, .22), (.08, .32), (.28, .43), (.50, .39), (.68, .25), (.72, .25)]
    vertices = [(math.cos(math.tau * i / 12) * radius, math.sin(math.tau * i / 12) * radius, z)
                for z, radius in rings for i in range(12)]
    faces = [(r * 12 + i, r * 12 + (i + 1) % 12, (r + 1) * 12 + (i + 1) % 12, (r + 1) * 12 + i)
             for r in range(len(rings) - 1) for i in range(12)]
    mesh("GlassBowl", vertices, faces, glass)
    for i in range(12):
        a, b = math.tau * i / 12, math.tau * (i + 1) / 12
        tube("Lip" + str(i), (math.cos(a) * .25, math.sin(a) * .25, .72),
             (math.cos(b) * .25, math.sin(b) * .25, .72), .018, .018, rim, 5)
    ellipsoid("ResidentFish", (0, -.02, .30), (.08, .18, .10), orange, 1)
    mesh("ResidentTail", [(0, .12, .30), (0, .25, .39), (0, .25, .21)], [(0, 1, 2), (2, 1, 0)], orange)
    for side in (-1, 1):
        ellipsoid("ResidentEye" + str(side), (side * .055, -.14, .33), (.017,) * 3, dark, 1)
        mesh("BowlFin" + str(side), [(side * .35, .02, .34), (side * .55, .09, .07), (side * .43, -.1, .04)],
             [(0, 1, 2), (2, 1, 0)], orange)
    empty("Grip")
    empty("Hook", (0, -.26, .72))


def palm():
    bark = material("PalmBark", (.40, .27, .16))
    green = material("PalmLeaves", (.27, .39, .09))
    for i in range(13):
        tube("TrunkRing" + str(i), (.025 * i, 0, i * .35), (.025 * (i + 1), 0, (i + 1) * .35), .24 - i * .008, .20 - i * .008, bark, 7)
    for i in range(9):
        angle = math.tau * i / 9
        direction = (math.cos(angle), math.sin(angle))
        side = (-direction[1], direction[0])
        vertices = []
        for step in range(5):
            r = step * .57
            z = 4.55 + math.sin(step * .8) * .5 - step * .16
            w = math.sin(math.pi * step / 4) * .3
            vertices.extend([(.325 + direction[0] * r + side[0] * w * s, direction[1] * r + side[1] * w * s, z) for s in (-1, 1)])
        faces = [(j * 2, j * 2 + 1, j * 2 + 3, j * 2 + 2) for j in range(4)]
        mesh("Frond" + str(i), vertices, faces + [tuple(reversed(face)) for face in faces], green)
    for x in (-.1, .1, .27):
        ellipsoid("Coconut", (x + .25, -.1, 4.35), (.16,) * 3, bark, 1)
    empty("Grip")
    empty("Forward", (0, -.3, 0))


def person(tourist):
    skin = material("DesertSkin", (.70, .49, .30))
    dark = material("SwimShorts", (.07, .075, .09))
    cloth = material("LifeVest" if tourist else "GrillShirt", (.95, .48, .07) if tourist else (.85, .84, .72))
    eye = material("DesertEye", (.95, .94, .78))
    hip, shoulder, head = (.26, .87, 1.14) if tourist else (.86, 1.48, 1.75)
    tube("Torso", (0, .05, hip), (0, .02, shoulder), .12, .17, cloth, 7)
    tube("Neck", (0, .02, shoulder), (0, -.01, head - .11), .05, .05, skin)
    ellipsoid("Head", (0, -.01, head), (.11, .105, .17), skin, 1)
    for side in (-1, 1):
        knee = (side * .16, -.4 if tourist else -.015, .17 if tourist else .47)
        ankle = (side * .19, -.82 if tourist else -.01, .07)
        tube("Shorts" + str(side), (side * .08, .05, hip), knee, .078, .062, dark)
        tube("Calf" + str(side), knee, ankle, .045, .029, skin)
        ellipsoid("Foot" + str(side), (ankle[0], ankle[1] - .08, .055), (.05, .13, .05), skin, 1)
        elbow = (side * .24, -.13, shoulder - .27)
        hand = (side * .2, -.4 if tourist else -.16, hip + .04)
        tube("Arm" + str(side), (side * .16, .02, shoulder - .03), elbow, .045, .032, skin)
        tube("Forearm" + str(side), elbow, hand, .032, .024, skin)
        ellipsoid("Hand" + str(side), hand, (.035, .06, .035), skin, 1)
        if tourist:
            ellipsoid("ArmFloat" + str(side), (side * .21, -.06, shoulder - .17), (.085, .085, .10), cloth, 1)
        ellipsoid("Eye" + str(side), (side * .055, -.10, head + .02), (.032, .025, .035), eye, 1)
        ellipsoid("Pupil" + str(side), (side * .055, -.125, head + .02), (.012,) * 3, dark, 1)
    if tourist:
        hat = material("BucketHat", (.40, .29, .15))
        tube("HatBrim", (0, 0, head + .1), (0, 0, head + .13), .18, .15, hat, 10)
        tube("HatCrown", (0, 0, head + .13), (0, 0, head + .26), .12, .10, hat, 9)
        box("VestZip", (0, -.13, shoulder - .27), (.015, .014, .43), dark)
    else:
        ellipsoid("Hair", (0, .015, head + .10), (.115, .105, .10), dark, 1)
        box("Apron", (0, -.12, 1.15), (.25, .025, .49), dark)
    empty("Face", (0, -.16, head))
    empty("Grip")


def prop(name):
    metal = material("DesertMetal", (.13, .14, .15))
    if name == "Grill":
        red = material("GrillRed", (.62, .11, .06))
        box("FireBox", (0, 0, .73), (.95, .54, .25), red)
        for x in (-.39, .39):
            for y in (-.2, .2):
                tube(f"Leg{x}_{y}", (x, y, .04), (x, y, .67), .027, .027, metal)
        for i in range(13):
            tube("Grate" + str(i), (-.43 + i * .072, -.24, .88), (-.43 + i * .072, .24, .88), .009, .009, metal, 5)
        box("BackLip", (0, .27, .88), (.97, .025, .15), red)
    elif name == "Carrot":
        orange = material("CarrotOrange", (.88, .28, .035))
        green = material("CarrotLeaf", (.20, .43, .08))
        tube("Carrot", (0, -.22, .05), (0, .13, .05), .005, .065, orange, 8)
        for side in (-1, 0, 1):
            tube("Leaf" + str(side), (0, .12, .05), (side * .08, .32, .10), .013, .003, green, 5)
    elif name == "FishMeat":
        pink = material("FishFlesh", (.86, .46, .35))
        white = material("FishBone", (.91, .84, .65))
        ellipsoid("MeatChunk", (0, 0, .09), (.17, .22, .08), pink, 1)
        tube("Bone", (0, -.26, .08), (0, .26, .08), .025, .025, white)
    elif name == "PufferfishFin":
        fin = material("PufferTrophy", (.54, .27, .12))
        for i in range(9):
            x = (i - 4) * .055
            tube("FinRay" + str(i), (0, .12, .02), (x, -.20 + abs(x) * .2, .03), .012, .003, fin, 5)
        mesh("FinWeb", [(0, .12, .02), (-.22, -.16, .02), (0, -.22, .02), (.22, -.16, .02)], [(0, 1, 2), (0, 2, 3), (2, 1, 0), (3, 2, 0)], fin)
    elif name == "Lighter":
        brass = material("LighterBrass", (.65, .51, .21))
        box("Body", (0, 0, .045), (.05, .08, .09), brass)
        box("Lid", (0, .055, .10), (.05, .04, .03), brass)
        tube("Wheel", (-.017, -.015, .105), (.017, -.015, .105), .012, .012, metal, 8)
    elif name == "PoisonPool":
        poison = material("PufferPoison", (.36, .055, .49))
        ellipsoid("Puddle", (0, 0, .025), (1.35, 1.35, .045), poison, 2)
        for i in range(9):
            a = math.tau * i / 9
            ellipsoid("Bubble" + str(i), (math.cos(a) * .75, math.sin(a) * .75, .065), (.08, .08, .10), poison, 1)
    empty("Grip")
    empty("Hook" if name in ("Carrot", "FishMeat", "PufferfishFin") else "Forward", (0, -.3 if name != "Grill" else -.4, .05))


def smg():
    metal = material("SMGSteel", (.14, .16, .18))
    grip = material("SMGGrip", (.07, .07, .08))
    box("Receiver", (0, -.13, .08), (.075, .36, .10), metal)
    tube("Barrel", (0, -.29, .08), (0, -.49, .08), .025, .022, metal, 9)
    box("PistolGrip", (0, .015, -.035), (.052, .065, .16), grip)
    mag = empty("Magazine", (0, -.17, .01))
    box("MagazineBody", (0, 0, -.11), (.044, .06, .22), metal, mag)
    box("Stock", (0, .17, .05), (.045, .22, .045), metal)
    box("Butt", (0, .27, .005), (.065, .035, .15), grip)
    for y in (-.23, -.18, -.13):
        box("Vent", (.039, y, .095), (.004, .025, .022), grip)
    empty("Muzzle", (0, -.50, .08))
    empty("Grip")


def lure():
    gold = material("StandardLure", (.70, .57, .18))
    steel = material("LureSteel", (.45, .49, .52))
    ellipsoid("Spoon", (0, -.035, .02), (.035, .085, .008), gold)
    for side in (-1, 1):
        tube("HookStem" + str(side), (0, .04, .02), (side * .025, .10, .02), .003, .003, steel)
        tube("HookTip" + str(side), (side * .025, .10, .02), (side * .03, .065, .02), .003, 0, steel)
    empty("Forward", (0, -.13, .02))
    empty("Grip")


def main():
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    recipes = {name: lambda name=name: fish(name) for name in ("Needlefish", "Seahorse", "YellowBoxfish", "BlueShark", "Pufferfish")}
    recipes.update({name: lambda name=name: prop(name) for name in ("Grill", "Carrot", "FishMeat", "PufferfishFin", "Lighter", "PoisonPool")})
    recipes.update(Bowlfish=bowlfish, Palm=palm, Tourist=lambda: person(True), GrillMaster=lambda: person(False), SMG=smg, StandardLure=lure)
    for name in sys.argv[sys.argv.index("--") + 2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT = None
        art.ROOT = empty(name)
        recipes[name]()
        save_and_export(project, name)


if __name__ == "__main__":
    main()
