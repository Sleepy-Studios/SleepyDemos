"""Self-authored faceted first-island models, generated in isolated Blender only."""
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from fbx_export import save_and_export

ROOT = None
MATERIALS = {}


def material(name, color):
    if name not in MATERIALS:
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*color, 1)
        mat.use_nodes = True
        shader = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
        shader.inputs["Base Color"].default_value = (*color, 1)
        shader.inputs["Roughness"].default_value = 0.78
        MATERIALS[name] = mat
    return MATERIALS[name]


def empty(name, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent or ROOT
    obj.location = location
    return obj


def mesh(name, vertices, faces, mat, parent=None, pivot=(0, 0, 0)):
    data = bpy.data.meshes.new(name)
    offset = Vector(pivot)
    data.from_pydata([tuple(Vector(vertex) - offset) for vertex in vertices], [], faces)
    data.materials.append(mat)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent or ROOT
    obj.location = pivot
    return obj


def ellipsoid(name, center, radius, mat, subdivisions=2, parent=None):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name
    for vertex in obj.data.vertices:
        vertex.co.x *= radius[0]
        vertex.co.y *= radius[1]
        vertex.co.z *= radius[2]
    obj.location = center
    obj.parent = parent or ROOT
    obj.data.materials.append(mat)
    return obj


def tube(name, a, b, ra, rb, mat, sides=6, parent=None, pivot=(0, 0, 0)):
    start, end = Vector(a), Vector(b)
    rotation = (end - start).to_track_quat("Z", "Y")
    vertices = []
    for point, radius in [(start, ra), (end, rb)]:
        for i in range(sides):
            angle = math.tau * i / sides
            vertices.append(point + rotation @ Vector((math.cos(angle) * radius, math.sin(angle) * radius, 0)))
    faces = [tuple(reversed(range(sides))), tuple(range(sides, sides * 2))]
    faces += [(i, (i + 1) % sides, (i + 1) % sides + sides, i + sides) for i in range(sides)]
    return mesh(name, vertices, faces, mat, parent, pivot)


def shell(name, color, size=1, spider=False):
    carapace = material(name + "Shell", color)
    edge = material(name + "Edge", tuple(channel * 0.68 for channel in color))
    ivory = material("Ivory", (0.91, 0.83, 0.59))
    black = material("Ink", (0.045, 0.027, 0.065))
    ellipsoid("Carapace", (0, 0, 0.12 * size), (0.17 * size, 0.12 * size, 0.085 * size), carapace)
    ellipsoid("Abdomen", (0, 0.018 * size, 0.065 * size), (0.125 * size, 0.1 * size, 0.04 * size), edge)
    for side in (-1, 1):
        for leg in range(4):
            front = (-0.07 + leg * 0.05) * size
            reach = (0.43 if spider else 0.25) * size
            a = (side * 0.12 * size, front, 0.12 * size)
            b = (side * reach, front + (leg - 1.5) * 0.065 * size, 0.14 * size)
            c = (side * reach * 1.17, b[1] + 0.025 * size, 0.008 * size)
            joint = empty(f"Leg{side}_{leg}", a)
            tube(f"LegUpper{side}_{leg}", (0, 0, 0), Vector(b) - Vector(a), 0.021 * size, 0.014 * size, carapace, parent=joint)
            tube(f"LegTip{side}_{leg}", Vector(b) - Vector(a), Vector(c) - Vector(a), 0.014 * size, 0.003 * size, edge, parent=joint)
        a = (side * 0.12 * size, -0.075 * size, 0.12 * size)
        b = (side * 0.23 * size, -0.17 * size, 0.13 * size)
        tube(f"Arm{side}", a, b, 0.025 * size, 0.025 * size, edge)
        ellipsoid(f"Claw{side}", b, (0.047 * size, 0.068 * size, 0.035 * size), carapace)
        for finger in (-1, 1):
            tube(f"Finger{side}_{finger}", (b[0] + finger * 0.025 * size, b[1] - 0.035 * size, b[2]),
                 (b[0] + finger * 0.009 * size, b[1] - 0.105 * size, b[2]), 0.017 * size, 0.002 * size, ivory)
        eye = (side * 0.065 * size, -0.093 * size, 0.208 * size)
        tube(f"EyeStalk{side}", (eye[0], eye[1] + 0.017 * size, 0.16 * size), eye, 0.009 * size, 0.008 * size, edge)
        ellipsoid(f"Eye{side}", eye, (0.019 * size,) * 3, ivory, 1)
        ellipsoid(f"Pupil{side}", (eye[0], eye[1] - 0.017 * size, eye[2]), (0.008 * size,) * 3, black, 1)
    empty("Hook", (0, -0.12 * size, 0.12 * size))
    empty("Grip", (0, 0, 0.13 * size))


def prawn(lobster=False):
    body = material("LobsterShell" if lobster else "ShrimpShell", (0.5, 0.14, 0.065) if lobster else (0.86, 0.48, 0.31))
    pale = material("ShellHighlights", (0.98, 0.73, 0.48))
    ink = material("Ink", (0.045, 0.027, 0.065))
    for i in range(7):
        y = -0.13 + i * 0.042
        r = 0.052 * (1 - i * 0.075)
        ellipsoid("Segment" + str(i), (0, y, 0.08 + math.sin(i * 0.45) * 0.018), (r, 0.033, r * 0.8), body)
        for side in (-1, 1):
            tube(f"Leg{side}_{i}", (side * r * 0.6, y, 0.07), (side * r * 1.35, y - 0.028, 0.02), 0.003, 0.001, pale, 4)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), (side * 0.029, -0.157, 0.115), (0.009,) * 3, ink, 1)
        tube("Antenna" + str(side), (side * 0.024, -0.15, 0.09), (side * 0.10, -0.34, 0.17), 0.003, 0.0008, pale, 4)
        if lobster:
            tube("ClawArm" + str(side), (side * 0.04, -0.10, 0.065), (side * 0.12, -0.21, 0.06), 0.017, 0.02, body)
            ellipsoid("Claw" + str(side), (side * 0.13, -0.245, 0.065), (0.04, 0.06, 0.025), body)
            tube("ClawTip" + str(side), (side * 0.15, -0.28, 0.065), (side * 0.13, -0.32, 0.065), 0.018, 0.002, pale)
    for i in (-1, 0, 1):
        mesh("TailFan" + str(i), [(0, 0.12, 0.08), (i * 0.027 - 0.026, 0.205, 0.047), (i * 0.027 + 0.026, 0.205, 0.047)], [(0, 1, 2), (2, 1, 0)], pale)
    empty("Hook", (0, -0.15, 0.09))
    empty("Grip", (0, 0, 0.08))


def clam():
    shellmat = material("ClamShell", (0.77, 0.64, 0.45))
    light = material("ClamRidges", (0.93, 0.84, 0.66))
    for side in (-1, 1):
        vertices = [(0, 0.04, 0.02)]
        for i in range(17):
            angle = math.pi * i / 16
            vertices.append((math.cos(angle) * 0.10, 0.04 - math.sin(angle) * 0.13, 0.022 + side * 0.03 * math.sin(angle)))
        mesh("Shell" + str(side), vertices, [(0, i + 1, i + 2) for i in range(16)], shellmat)
        for i in range(1, 16, 2):
            tube(f"Ridge{side}_{i}", vertices[0], vertices[i + 1], 0.003, 0.0018, light, 4)
    empty("Hook", (0, -0.06, 0.02))
    empty("Grip", (0, 0, 0.02))


def rod():
    grip = material("RodGrip", (0.07, 0.04, 0.11))
    metal = material("RodMetal", (0.24, 0.43, 0.54))
    bright = material("Steel", (0.65, 0.73, 0.71))
    tube("Handle", (0, 0.32, 0), (0, -0.22, 0), 0.035, 0.027, grip, 8)
    for i in range(4):
        tube("RodSection" + str(i), (0, -0.22 - i * 0.4, 0), (0, -0.62 - i * 0.4, 0.01 * i),
             0.022 - i * 0.0045, 0.0175 - i * 0.0045, metal, 8)
    for i in range(5):
        y = -0.25 - i * 0.37
        tube("GuideSupport" + str(i), (0, y, 0), (0, y, 0.05), 0.005, 0.004, bright, 4)
        for j in range(8):
            a, b = math.tau * j / 8, math.tau * (j + 1) / 8
            tube(f"Guide{i}_{j}", (math.cos(a) * 0.018, y, 0.05 + math.sin(a) * 0.018),
                 (math.cos(b) * 0.018, y, 0.05 + math.sin(b) * 0.018), 0.003, 0.003, bright, 4)
    tube("ReelMount", (0, 0.04, 0), (0, 0.04, -0.09), 0.014, 0.012, metal)
    tube("ReelSpool", (-0.04, 0.04, -0.11), (0.04, 0.04, -0.11), 0.049, 0.049, metal, 10)
    tube("Crank", (0.05, 0.04, -0.11), (0.08, -0.04, -0.11), 0.009, 0.009, grip)
    tube("CrankHandle", (0.08, -0.04, -0.11), (0.125, -0.04, -0.11), 0.016, 0.016, grip)
    empty("RodTip", (0, -1.82, 0.048))
    empty("Grip", (0, 0, 0))


def knife():
    steel = material("BladeSteel", (0.65, 0.69, 0.65))
    handle = material("KnifeHandle", (0.11, 0.07, 0.085))
    tube("Handle", (0, 0.14, 0), (0, 0, 0), 0.025, 0.025, handle, 8)
    vertices = [(-0.024, 0, -0.003), (0.024, 0, -0.003), (0.024, -0.19, -0.003), (-0.006, -0.27, 0),
                (-0.024, -0.10, -0.003), (-0.024, 0, 0.003), (0.024, 0, 0.003), (0.024, -0.19, 0.003), (-0.024, -0.10, 0.003)]
    mesh("Blade", vertices, [(0, 4, 3, 2, 1), (5, 6, 7, 3, 8), (0, 1, 6, 5), (1, 2, 7, 6), (2, 3, 7), (4, 8, 3), (0, 5, 8, 4)], steel)
    for i in range(3):
        tube("Rivet" + str(i), (-0.027, 0.03 + i * 0.04, 0), (0.027, 0.03 + i * 0.04, 0), 0.004, 0.004, steel)
    empty("Grip", (0, 0.07, 0))
    empty("Strike", (0, -0.23, 0))


def crab_meat():
    flesh = material("CrabFlesh", (0.92, 0.81, 0.68))
    red = material("CrabMeatEdge", (0.72, 0.19, 0.10))
    for i in range(3):
        x = (i - 1) * .09
        ellipsoid("Meat" + str(i), (x, -.025 * (i % 2), .07), (.085, .16, .065), flesh)
        tube("RedFiber" + str(i), (x, .10, .10), (x + .025, -.14, .10), .013, .007, red)
    empty("Hook", (0, -.17, .07))
    empty("Grip", (0, 0, .07))


def box(name, center, size, mat, parent=None):
    vertices = [(center[0] + x * size[0] / 2, center[1] + y * size[1] / 2, center[2] + z * size[2] / 2)
                for x, y, z in [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                                (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]]
    return mesh(name, vertices, [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], mat, parent)


def fishing_boat():
    wood = material("StarterBoatWood", (.29, .17, .105))
    light = material("StarterBoatPlank", (.39, .25, .16))
    dark = material("StarterBoatSeams", (.115, .065, .06))
    metal = material("StarterBoatSteel", (.19, .26, .31))
    outline = [(-.9, 2.2), (-1.05, .8), (-.95, -1.2), (-.65, -2), (0, -2.55), (.65, -2), (.95, -1.2), (1.05, .8), (.9, 2.2)]
    count = len(outline)
    for layer in range(4):
        bottom, top = -.42 + layer * .25, -.42 + (layer + 1) * .25
        lower_scale, upper_scale = .70 + layer * .075, .70 + (layer + 1) * .075
        vertices = [(x * lower_scale, y * lower_scale, bottom) for x, y in outline] + [(x * upper_scale, y * upper_scale, top) for x, y in outline]
        mesh("HullPlank" + str(layer), vertices, [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)], wood if layer % 2 else light)
        for i, (x, y) in enumerate(outline):
            nx, ny = outline[(i + 1) % count]
            tube(f"Seam{layer}_{i}", (x * upper_scale, y * upper_scale, top), (nx * upper_scale, ny * upper_scale, top), .011, .011, dark, 4)
    mesh("Floor", [(x * .89, y * .89, .09) for x, y in outline], [tuple(range(count))], wood)
    # Inner faces are separate geometry so the open hull remains visible from its cockpit.
    mesh("InnerHull", [(x, y, .58) for x, y in outline] + [(x * .89, y * .89, .09) for x, y in outline],
         [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)], light)
    for i in range(9):
        box("DeckPlank" + str(i), ((i - 4) * .19, .35, .105), (.177, 3.0, .025), wood if i % 2 else light)
    box("ForwardBench", (0, -.8, .43), (1.75, .36, .14), light)
    box("SternBench", (0, 1.85, .43), (1.65, .30, .14), light)
    box("HelmConsole", (.35, -.08, .60), (.85, .22, .95), wood)
    wheel = empty("SteeringWheel", (.40, .14, 1.0))
    for i in range(8):
        a, b = math.tau * i / 8, math.tau * (i + 1) / 8
        tube("WheelRim" + str(i), (.27 * math.cos(a), 0, .27 * math.sin(a)), (.27 * math.cos(b), 0, .27 * math.sin(b)), .022, .022, metal, parent=wheel)
    for i in range(4):
        a = math.tau * i / 4
        tube("WheelSpoke" + str(i), (0, 0, 0), (.25 * math.cos(a), 0, .25 * math.sin(a)), .014, .014, metal, parent=wheel)
    box("ThrottleBase", (-.35, -.04, .83), (.15, .2, .15), metal)
    tube("ThrottleLever", (-.35, -.04, .85), (-.35, -.13, 1.06), .025, .025, metal)
    empty("Bow", (0, -2.55, .58))
    empty("Grip", (0, 0, 0))


def supplies(name):
    if name == "Beer":
        gold = material("BeerGold", (.78, .54, .12))
        steel = material("CanSteel", (.62, .64, .57))
        tube("Can", (0, 0, -.07), (0, 0, .07), .034, .034, gold, 12)
        for z in (-.07, .07):
            tube("Rim" + str(z), (0, 0, z - .002), (0, 0, z + .002), .035, .035, steel, 12)
        box("Label", (0, -.034, 0), (.038, .001, .05), material("BeerLabel", (.88, .82, .59)))
        box("Tab", (0, -.006, .074), (.009, .022, .004), steel)
    elif name == "HotDog":
        bread = material("HotDogBread", (.72, .43, .19))
        red = material("HotDogSausage", (.48, .11, .055))
        for x in (-.025, .025):
            ellipsoid("Bun" + str(x), (x, 0, 0), (.026, .10, .03), bread)
        ellipsoid("Sausage", (0, 0, .01), (.018, .108, .021), red)
        tube("Mustard", (0, -.085, .030), (0, .085, .030), .003, .003, material("Mustard", (.86, .66, .10)), 4)
    else:
        case = material("RadarCase", (.07, .095, .105))
        green = material("RadarScreen", (.14, .47, .23))
        box("Case", (0, 0, 0), (.15, .035, .21), case)
        box("Screen", (0, .019, .02), (.12, .004, .13), green)
        tube("Antenna", (.055, 0, .1), (.055, 0, .23), .008, .004, case)
        for i in range(3):
            box("Button" + str(i), ((i - 1) * .035, .022, -.07), (.015, .008, .013), green)
    empty("Grip", (0, 0, 0))
    empty("Forward", (0, -.15, 0))


def lighthouse():
    stone = material("TowerStone", (.76, .76, .70))
    pale = material("TowerPale", (.83, .81, .74))
    trim = material("TowerTrim", (.21, .22, .25))
    wood = material("TowerDoor", (.18, .09, .085))
    light = material("TowerLight", (1, .91, .65))
    tube("Tower", (0, 0, 0), (0, 0, 8.2), 1.7, 1.15, stone, 12)
    for i, z in enumerate((.12, 3.4, 6.8, 8.15)):
        radius = 1.7 - z / 8.2 * .55 + .12
        tube("Cornice" + str(i), (0, 0, z), (0, 0, z + .13), radius, radius, trim, 12)
    # Separate plank geometry and an angular arch keep the door readable at player height.
    for i in range(6):
        x = (i - 2.5) * .19
        height = 2.0 - abs(x) * .4
        box("DoorPlank" + str(i), (x, -1.685, height / 2), (.18, .065, height), wood)
    tube("DoorHandle", (.36, -1.75, .85), (.36, -1.75, 1.03), .022, .022, light)
    for i, z in enumerate((4.6, 7.0)):
        radius = 1.7 - z / 8.2 * .55
        box("WindowFrame" + str(i), (0, -radius, z), (.75, .14, .85), pale)
        box("WindowLight" + str(i), (0, -radius - .08, z), (.53, .02, .64), light)
        box("WindowCrossV" + str(i), (0, -radius - .10, z), (.065, .02, .66), pale)
        box("WindowCrossH" + str(i), (0, -radius - .10, z), (.55, .02, .065), pale)
    tube("LanternBase", (0, 0, 8.3), (0, 0, 8.48), 1.65, 1.65, trim, 12)
    for i in range(8):
        a = math.tau * i / 8
        x, y = math.cos(a) * 1.03, math.sin(a) * 1.03
        tube("LanternPost" + str(i), (x, y, 8.4), (x, y, 9.65), .045, .045, trim)
    ellipsoid("Beacon", (0, 0, 9.05), (.38, .38, .62), light, 1)
    tube("Roof", (0, 0, 9.65), (0, 0, 10.3), 1.4, .1, trim, 12)
    empty("DoorFront", (0, -1.8, .8))
    empty("Grip", (0, 0, 0))


def keeper():
    coat = material("KeeperCoat", (.13, .065, .17))
    skin = material("KeeperSkin", (.59, .45, .31))
    hair = material("KeeperHair", (.56, .55, .46))
    dark = material("KeeperBoot", (.055, .035, .075))
    eye = material("KeeperEye", (.88, .83, .66))
    bench = material("BenchWood", (.30, .19, .13))
    box("BenchSeat", (0, .08, .49), (.90, .38, .10), bench)
    for x in (-.36, .36):
        for y in (-.04, .2):
            box(f"BenchLeg{x}_{y}", (x, y, .245), (.075, .075, .49), bench)
    tube("Body", (0, .04, .56), (0, -.03, 1.13), .14, .18, coat, 7)
    tube("Neck", (0, -.04, 1.12), (0, -.07, 1.24), .06, .06, skin)
    ellipsoid("Head", (0, -.06, 1.36), (.12, .105, .18), skin, 1)
    for side in (-1, 1):
        tube("Thigh" + str(side), (side * .095, .03, .54), (side * .11, -.35, .47), .07, .062, coat)
        tube("Shin" + str(side), (side * .11, -.35, .47), (side * .11, -.32, .10), .057, .045, dark)
        ellipsoid("Boot" + str(side), (side * .11, -.39, .075), (.07, .15, .07), dark, 1)
        tube("SleeveUpper" + str(side), (side * .17, -.04, 1.08), (side * .19, -.12, .79), .064, .052, coat)
        tube("SleeveLower" + str(side), (side * .19, -.12, .79), (side * .13, -.35, .63), .055, .041, coat)
        ellipsoid("Hand" + str(side), (side * .13, -.38, .60), (.04, .075, .045), skin, 1)
        ellipsoid("Eye" + str(side), (side * .064, -.145, 1.39), (.035, .035, .028), eye, 1)
        ellipsoid("Pupil" + str(side), (side * .064, -.177, 1.39), (.012, .01, .014), dark, 1)
        tube("Moustache" + str(side), (side * .02, -.18, 1.30), (side * .12, -.16, 1.29), .025, .005, hair)
        ellipsoid("SideHair" + str(side), (side * .102, -.008, 1.36), (.05, .085, .145), hair, 1)
    tube("Nose", (0, -.14, 1.38), (0, -.23, 1.32), .038, .024, skin, 5)
    tube("HatBrim", (0, -.04, 1.50), (0, -.04, 1.525), .20, .20, dark, 10)
    tube("TopHat", (0, -.04, 1.52), (0, -.04, 1.72), .12, .13, coat, 10)
    tube("HatRibbon", (0, -.04, 1.54), (0, -.04, 1.57), .125, .128, dark, 10)
    empty("Face", (0, -.24, 1.36))
    empty("Grip", (0, 0, 0))


def right_hand():
    cloth = material("FisherSleeve", (.065, .025, .10))
    skin = material("FisherHand", (.75, .62, .41))
    tube("Sleeve", (-.085, .31, -.14), (-.055, .055, -.035), .082, .045, cloth, 7)
    tube("Cuff", (-.055, .085, -.045), (-.05, .045, -.025), .049, .049, cloth, 8)
    ellipsoid("Palm", (-.047, -.008, -.012), (.036, .065, .023), skin, 1)
    for finger in range(4):
        y = -.048 + finger * .025
        tube("Finger" + str(finger), (-.04, y, .004), (.017, y, .029), .011, .009, skin)
        tube("Fingertip" + str(finger), (.017, y, .029), (.034, y, -.003), .009, .008, skin)
    tube("Thumb", (-.07, -.043, -.012), (-.035, -.071, .024), .014, .012, skin)
    empty("Grip", (0, 0, 0))
    empty("Forward", (0, -.1, 0))


def pine():
    bark = material("PineBark", (.19, .115, .075))
    leaves = material("PineNeedles", (.12, .23, .085))
    tips = material("PineTips", (.19, .30, .12))
    tube("Trunk", (0, 0, 0), (0, 0, 3.9), .18, .06, bark, 7)
    for i, (z, radius, height) in enumerate(((1.1, 1.35, 1.8), (2.0, 1.10, 1.6), (2.9, .78, 1.5))):
        tube("Canopy" + str(i), (0, 0, z), (0, 0, z + height), radius, .015, leaves if i % 2 else tips, 9)
    empty("Grip", (0, 0, 0))
    empty("Forward", (0, -1, 0))


def leech():
    skin = material("LeechSkin", (.23, .055, .07))
    for i in range(13):
        y = -.24 + i * .04
        radius = .026 + .018 * math.sin(math.pi * i / 12)
        ellipsoid("Segment" + str(i), (.024 * math.sin(i * .6), y, .045), (radius, .03, radius), skin, 1)
    empty("Hook", (0, -.28, .045))
    empty("Grip")


def forest_lady():
    shirt = material("LadyShirt", (.55, .12, .12))
    jeans = material("LadyOveralls", (.12, .22, .39))
    skin = material("LadySkin", (.72, .53, .35))
    hat = material("LadyHat", (.55, .45, .25))
    dark = material("LadyBoots", (.09, .065, .055))
    white = material("LadyEyes", (.92, .90, .76))
    tube("Torso", (0, 0, .8), (0, 0, 1.43), .15, .18, shirt)
    box("Bib", (0, -.13, 1.04), (.26, .06, .35), jeans)
    for side in (-1, 1):
        tube("Leg" + str(side), (side * .085, 0, .86), (side * .11, 0, .13), .075, .055, jeans)
        ellipsoid("Boot" + str(side), (side * .11, -.075, .07), (.08, .14, .07), dark, 1)
        tube("Strap" + str(side), (side * .1, -.145, 1.07), (side * .1, -.13, 1.42), .018, .018, jeans)
        tube("Sleeve" + str(side), (side * .17, 0, 1.38), (side * .23, -.04, 1.05), .065, .045, shirt)
        tube("Arm" + str(side), (side * .23, -.04, 1.05), (side * .20, -.1, .8), .04, .028, skin)
        ellipsoid("Hand" + str(side), (side * .20, -.11, .77), (.04, .04, .065), skin, 1)
    tube("Neck", (0, 0, 1.4), (0, 0, 1.53), .055, .055, skin)
    ellipsoid("Head", (0, -.025, 1.65), (.125, .115, .17), skin, 1)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), (side * .065, -.137, 1.67), (.05, .046, .045), white, 1)
        ellipsoid("Pupil" + str(side), (side * .065, -.177, 1.67), (.018, .012, .022), dark, 1)
    tube("Nose", (0, -.13, 1.64), (0, -.20, 1.60), .032, .018, skin)
    tube("Brim", (0, 0, 1.78), (0, 0, 1.805), .24, .24, hat, 10)
    tube("Hat", (0, 0, 1.8), (0, 0, 1.94), .15, .11, hat, 10)
    empty("Face", (0, -.21, 1.65))
    empty("Grip")


def piranha(giant=False, skeleton=False):
    scale = 4 if giant else 1
    body = material("PiranhaBody", (.30, .40, .25))
    belly = material("PiranhaBelly", (.60, .20, .12))
    bone = material("FishBone", (.85, .81, .62))
    black = material("FishEyes", (.04, .03, .025))
    def p(x, y, z): return (x * scale, y * scale, z * scale)
    if skeleton:
        tube("Spine", p(0, -.18, .2), p(0, .3, .2), .022 * scale, .012 * scale, bone)
        for i in range(6):
            y = -.1 + i * .055
            for side in (-1, 1):
                tube(f"Rib{side}_{i}", p(0, y, .2), p(side * .12, y + .025, .07), .012 * scale, .007 * scale, bone)
    else:
        ellipsoid("Body", p(0, .01, .19), p(.12, .24, .16), body, 2)
        ellipsoid("Belly", p(0, -.04, .095), p(.10, .19, .07), belly, 1)
    ellipsoid("Head", p(0, -.20, .2), p(.13, .10, .13), bone if skeleton else body, 1)
    jaw = empty("Jaw", p(0, -.21, .12))
    ellipsoid("JawMesh", p(0, -.045, -.015), p(.115, .09, .04), bone if skeleton else belly, 1, jaw)
    for side in (-1, 1):
        ellipsoid("Eye" + str(side), p(side * .115, -.245, .24), p(.025, .035, .026), black, 1)
        for i in range(4):
            tube(f"Tooth{side}_{i}", p(side * (.023 + i * .025), -.28, .14), p(side * (.023 + i * .025), -.28, .185), .012 * scale, 0, bone)
    tail = empty("Tail", p(0, .22, .19))
    mesh("TailFin", [p(0, 0, 0), p(0, .16, .12), p(0, .12, 0), p(0, .16, -.1)], [(0, 1, 2), (0, 2, 3)], bone if skeleton else belly, tail)
    if not skeleton:
        mesh("DorsalFin", [p(0, -.03, .31), p(0, .11, .45), p(0, .18, .27)], [(0, 1, 2)], body)
    empty("Hook", p(0, -.31, .18))
    empty("Grip")


def pistol():
    steel = material("PistolSteel", (.07, .075, .085))
    grip = material("PistolGrip", (.035, .03, .045))
    silver = material("GunSilver", (.44, .46, .47))
    box("GripMesh", (0, .015, -.035), (.048, .08, .13), grip)
    slide = empty("Slide", (0, 0, .065))
    box("SlideMesh", (0, -.08, 0), (.052, .23, .048), steel, slide)
    tube("Barrel", (0, -.17, .06), (0, -.205, .06), .014, .014, silver, 10)
    tube("Bore", (0, -.206, .06), (0, -.208, .06), .009, .009, grip, 10)
    for side in (-1, 1):
        tube("TriggerGuard" + str(side), (side * .015, -.015, -.01), (side * .015, -.07, .025), .004, .004, silver)
    box("Trigger", (0, -.04, .025), (.008, .012, .025), silver)
    magazine = empty("Magazine", (0, .02, -.075))
    box("MagazineBase", (0, 0, -.012), (.05, .055, .025), silver, magazine)
    box("RearSight", (0, .01, .096), (.04, .015, .018), steel)
    box("FrontSight", (0, -.18, .095), (.006, .012, .012), silver)
    empty("Grip")
    empty("Muzzle", (0, -.21, .06))


def shotgun():
    wood = material("ShotgunWood", (.31, .115, .065))
    steel = material("ShotgunSteel", (.18, .23, .27))
    dark = material("GunBore", (.035, .035, .04))
    silver = material("GunSilver", (.44, .46, .47))
    mesh("Stock", [(-.035, .02, -.05), (.035, .02, -.05), (-.05, .26, -.12), (.05, .26, -.12),
        (-.035, .02, .05), (.035, .02, .05), (-.05, .26, .04), (.05, .26, .04)],
        [(0, 2, 3, 1), (4, 5, 7, 6), (0, 4, 6, 2), (1, 3, 7, 5), (2, 6, 7, 3), (0, 1, 5, 4)], wood)
    box("Receiver", (0, -.045, .035), (.08, .14, .09), steel)
    hinge = empty("Breech", (0, -.11, .03))
    for side in (-1, 1):
        tube("Barrel" + str(side), (side * .025, 0, .025), (side * .025, -.49, .025), .025, .022, steel, 10, hinge)
        tube("Mouth" + str(side), (side * .025, -.491, .025), (side * .025, -.495, .025), .017, .017, dark, 10, hinge)
    box("Foregrip", (0, -.24, -.005), (.082, .20, .045), wood, hinge)
    tube("TriggerGuard", (0, .01, -.015), (0, -.04, -.045), .01, .01, silver)
    empty("Grip")
    empty("Muzzle", (0, -.61, .055))


def forest_shop():
    wood = material("ShopTimber", (.50, .43, .32))
    trim = material("ShopTrim", (.27, .12, .10))
    roof = material("ShopRoof", (.35, .31, .24))
    for i in range(12):
        z = .12 + i * .20
        box("BackPlank" + str(i), (0, .7, z), (4.6, .10, .18), wood)
        for side in (-1, 1):
            box(f"Side{side}_{i}", (side * 2.3, 0, z), (.10, 1.5, .18), wood)
    for side in (-1, 1):
        box("Post" + str(side), (side * 2.25, -.7, 1.4), (.15, .15, 2.8), trim)
    box("Counter", (0, -.7, .9), (4.6, .6, .14), trim)
    box("LowerFront", (0, -.68, .4), (4.6, .12, .75), wood)
    box("GunBoard", (-1.1, -.54, 1.63), (1.9, .12, 1.25), trim)
    mesh("Roof", [(-2.55, -1, 2.5), (2.55, -1, 2.5), (-2.55, 0, 3.05), (2.55, 0, 3.05), (-2.55, 1, 2.5), (2.55, 1, 2.5)],
        [(0, 2, 3, 1), (2, 4, 5, 3), (0, 1, 3, 2), (2, 3, 5, 4)], roof)
    empty("CounterFront", (0, -1, 1))
    empty("Grip")


def freshwater_fish(name):
    shapes = {
        "Mackerel": ((.07, .28, .10), (.17, .34, .44)),
        "Gar": ((.045, .35, .055), (.28, .36, .18)),
        "Pike": ((.075, .30, .09), (.25, .34, .14)),
        "Cod": ((.12, .27, .16), (.47, .43, .27)),
        "Goldfish": ((.12, .16, .16), (.89, .38, .07)),
        "Perch": ((.075, .22, .13), (.40, .47, .15)),
        "Triggerfish": ((.045, .19, .19), (.33, .45, .50)),
        "Goby": ((.07, .17, .065), (.43, .29, .19)),
        "Salmon": ((.09, .34, .13), (.40, .48, .45)),
    }
    radius, color = shapes[name]
    width, length, height = radius
    z = height + .025
    body = material(name + "Skin", color)
    fin = material(name + "Fins", tuple(value * .7 for value in color))
    belly = material(name + "Belly", tuple(min(1, value * .5 + .42) for value in color))
    dark = material("FishPupil", (.018, .023, .026))
    if name == "Triggerfish":
        mesh("Body", [(0, -length, z), (0, length, z), (0, 0, z + height), (0, 0, z - height),
            (-width, 0, z), (width, 0, z)], [(0, 4, 2), (0, 3, 4), (1, 2, 4), (1, 4, 3),
            (0, 2, 5), (0, 5, 3), (1, 5, 2), (1, 3, 5)], body)
    else:
        ellipsoid("Body", (0, 0, z), radius, body, 2)
        ellipsoid("Belly", (0, -.01, z - height * .52), (width * .8, length * .82, height * .42), belly, 1)
    snout = .18 if name == "Gar" else .10 if name == "Pike" else .03
    head_width = width * (1.35 if name == "Goby" else .75)
    ellipsoid("Head", (0, -length * .66, z), (head_width, length * .4, height * .7), body, 1)
    if name in ("Gar", "Pike"):
        tube("Snout", (0, -length * .7, z), (0, -length - snout, z), width * .55, width * .28, body, 6)
    for side in (-1, 1):
        eye_z = z + height * (.7 if name == "Goby" else .25)
        ellipsoid("Eye" + str(side), (side * head_width * .85, -length * .79, eye_z), (.018, .025, .022), dark, 1)
        mesh("Pectoral" + str(side), [(side * width * .5, -length * .2, z), (side * width * 2.2, length * .05, z - height * .5),
            (side * width * .6, length * .4, z - height * .5)], [(0, 1, 2), (2, 1, 0)], fin)
    tail = empty("Tail", (0, length * .85, z))
    fan = height * (1.4 if name == "Goldfish" else .8)
    mesh("TailFin", [(0, 0, 0), (0, .18, fan), (0, .12, 0), (0, .18, -fan)],
        [(0, 1, 2), (2, 1, 0), (0, 2, 3), (3, 2, 0)], fin, tail)
    dorsal_start = length * .3 if name in ("Gar", "Pike") else -length * .25
    points = [(0, dorsal_start, z + height * .7)]
    for i in range(7):
        points.append((0, dorsal_start + i * length * .09, z + height * (1.55 if name == "Perch" and i % 2 == 0 else 1.2)))
    points.append((0, length * .6, z + height * .55))
    mesh("DorsalFin", points, [tuple(range(len(points))), tuple(reversed(range(len(points))))], fin)
    if name == "Cod":
        tube("ChinBarbel", (0, -length * .9, z - height * .3), (0, -length * 1.1, .015), .009, .002, belly)
    if name in ("Mackerel", "Perch"):
        for i in range(5):
            y = -length * .35 + i * length * .22
            for side in (-1, 1):
                tube(f"Stripe{side}_{i}", (side * width * .93, y, z + height * .15),
                    (side * width * .72, y + .025, z + height * .6), .012, .007, fin, 4)
    if name == "Salmon":
        for side in (-1, 1):
            for i in range(7):
                ellipsoid(f"Spot{side}_{i}", (side * width * .92, -.1 + i * .045, z + .025), (.007, .009, .007), dark, 1)
    empty("Hook", (0, -length - snout - .015, z))
    empty("Grip")


def fishing_rod():
    cork = material("FishingRodCork", (.55, .37, .17))
    carbon = material("FishingRodCarbon", (.055, .16, .11))
    steel = material("FishingRodReel", (.45, .50, .48))
    tube("Handle", (0, .35, 0), (0, -.23, 0), .028, .025, cork, 8)
    for i in range(5):
        tube("Blank" + str(i), (0, -.23 - i * .4, 0), (0, -.63 - i * .4, 0), .018 - i * .0032, .0148 - i * .0032, carbon, 7)
        y = -.35 - i * .4
        tube("GuideSupport" + str(i), (0, y, 0), (0, y, -.045), .004, .004, steel)
        for j in range(8):
            a, b = math.tau * j / 8, math.tau * (j + 1) / 8
            tube(f"Guide{i}_{j}", (math.cos(a) * .015, y, -.045 + math.sin(a) * .015),
                (math.cos(b) * .015, y, -.045 + math.sin(b) * .015), .0025, .0025, steel, 4)
    tube("ReelFoot", (0, .03, 0), (0, .03, -.10), .012, .012, steel)
    ellipsoid("SpinningReel", (0, .04, -.13), (.05, .08, .05), carbon, 1)
    tube("Spool", (0, -.035, -.13), (0, -.09, -.13), .045, .045, steel, 10)
    tube("Crank", (.04, .04, -.13), (.11, .09, -.13), .008, .008, steel)
    tube("CrankHandle", (.11, .09, -.13), (.11, .13, -.13), .014, .014, cork)
    empty("RodTip", (0, -2.23, -.02))
    empty("Grip")


def beginner_lure():
    body = material("BeginnerLureGreen", (.23, .47, .12))
    metal = material("LureHookSteel", (.45, .47, .43))
    orange = material("LureTail", (.85, .30, .05))
    ellipsoid("LureBody", (0, -.035, .025), (.028, .07, .025), body, 1)
    tube("HookStem", (0, .03, .02), (0, .08, .02), .004, .004, metal)
    for side in (-1, 1):
        tube("Hook" + str(side), (0, .08, .02), (side * .025, .10, .02), .004, .003, metal)
        tube("HookTip" + str(side), (side * .025, .10, .02), (side * .03, .06, .02), .003, 0, metal)
        mesh("Feather" + str(side), [(0, .03, .02), (side * .035, .15, .035), (0, .12, .02)], [(0, 1, 2), (2, 1, 0)], orange)
    empty("Forward", (0, -.11, .025))
    empty("Grip")


def main():
    global ROOT
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    if not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        raise SystemExit("Not a Unity project")
    recipes = {
        "BrownCrab": lambda: shell("BrownCrab", (0.45, 0.23, 0.105)),
        "RockCrab": lambda: shell("RockCrab", (0.37, 0.40, 0.31), 1.2),
        "SpiderCrab": lambda: shell("SpiderCrab", (0.62, 0.23, 0.095), 7, True),
        "Shrimp": lambda: prawn(False), "Lobster": lambda: prawn(True),
        "Clam": clam, "CrabRod": rod, "Knife": knife, "CrabMeat": crab_meat,
        "FishingBoat": fishing_boat, "Lighthouse": lighthouse,
        "Keeper": keeper, "RightHand": right_hand,
        "Beer": lambda: supplies("Beer"), "HotDog": lambda: supplies("HotDog"), "Radar": lambda: supplies("Radar"),
        "Pine": pine, "Leech": leech, "ForestLady": forest_lady,
        "Piranha": piranha, "GiantPiranha": lambda: piranha(True),
        "PiranhaSkeleton": lambda: piranha(True, True),
        "Pistol": pistol, "Shotgun": shotgun, "ForestShop": forest_shop,
        "FishingRod": fishing_rod, "BeginnerLure": beginner_lure,
    }
    for species in ("Mackerel", "Gar", "Pike", "Cod", "Goldfish", "Perch", "Triggerfish", "Goby", "Salmon"):
        recipes[species] = lambda species=species: freshwater_fish(species)
    names = sys.argv[sys.argv.index("--") + 2:] or list(recipes)
    for name in names:
        build = recipes[name]
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        ROOT = None
        ROOT = empty(name)
        build()
        save_and_export(project, name)


if __name__ == "__main__":
    main()
