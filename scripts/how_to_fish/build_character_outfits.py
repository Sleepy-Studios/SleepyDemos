"""Self-authored clothing candidates. Export only to Library while Unity tests run."""
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, tube, box, ellipsoid


NAMES = ["Badman", "Bikini", "Fisherman", "Sailor", "LighthouseKeeper", "SwampMan", "SwampLady",
         "KioskLady", "Tourist", "GrillMaster", "Andrei", "Jacob", "GunstoreClerc", "ScaredGuyInShorts",
         "StoreGrandma", "Military", "Scientist", "Bean"]
COLORS = {"Skin": (.77, .56, .37), "Red": (.70, .035, .028), "White": (.92, .92, .83),
          "Navy": (.045, .060, .12), "Blue": (.12, .22, .55), "BlueGrey": (.23, .38, .47),
          "Orange": (.96, .33, .025), "Yellow": (.96, .79, .055), "Brown": (.30, .13, .065),
          "Apron": (.53, .28, .12), "Black": (.025, .030, .038), "Grey": (.36, .38, .39),
          "Silver": (.70, .72, .70), "Gold": (.78, .56, .10), "Pink": (.89, .20, .64),
          "Blonde": (.87, .69, .075), "Ginger": (.63, .29, .13), "Cream": (.78, .68, .43),
          "Rose": (.48, .25, .22), "Olive": (.23, .30, .15), "Helmet": (.36, .43, .24),
          "Cyan": (.16, .72, .77), "BeanGreen": (.055, .35, .16)}


def mat(name):
    return material("Outfit" + name, COLORS[name])


def front_patch(name, points, color, y=-.105):
    vertices = [(x, y, z) for x, z in points]
    face = list(range(len(vertices)))
    if sum(Vector(vertex).cross(Vector(vertices[(i + 1) % len(vertices)])).y
           for i, vertex in enumerate(vertices)) > 0:
        face.reverse()
    obj = mesh(name, vertices, [tuple(face)], mat(color))
    assert obj.data.polygons[0].normal.y < -.99, name
    return obj


def body(shirt, sleeves, trousers, leg_length, footwear):
    skin = mat("Skin")
    ellipsoid("Torso", (0, 0, 1.115), (.132, .084, .255), mat(shirt), 2)
    ellipsoid("Waist", (0, 0, .900), (.114, .076, .073), mat(trousers), 1)
    tube("Neck", (0, 0, 1.355), (0, 0, 1.48), .041, .043, skin, 8)
    head = empty("Head", (0, -.004, 1.575))
    ellipsoid("Face", (0, 0, 0), (.082, .075, .124), skin, 2, parent=head)
    tube("Nose", (0, -.062, -.004), (0, -.093, -.018), .018, .009, skin, 5, parent=head)
    for side, label in ((-1, "Right"), (1, "Left")):
        ellipsoid("Eye" + label, (side * .031, -.069, .024), (.013, .009, .014), mat("White"), 1, parent=head)
        ellipsoid("Pupil" + label, (side * .031, -.077, .024), (.005, .004, .007), mat("Black"), 1, parent=head)
        shoulder = Vector((side * .137, 0, 1.335))
        elbow = Vector((side * .195, -.007, 1.135))
        wrist = Vector((side * .215, -.027, .965))
        arm = empty(label + "Arm", shoulder)
        sleeve_color = mat(shirt) if sleeves != "bare" else skin
        ellipsoid("Shoulder" + label, (side * .109, 0, 1.315), (.055, .057, .067), sleeve_color, 1)
        if sleeves == "short":
            end = shoulder.lerp(elbow, .54)
            tube("Sleeve" + label, (0, 0, 0), end - shoulder, .047, .043, sleeve_color, 7, parent=arm)
            tube("UpperArmSkin" + label, end - shoulder, elbow - shoulder, .036, .029, skin, 7, parent=arm)
        else:
            tube(("Sleeve" if sleeves == "long" else "UpperArmSkin") + label,
                 (0, 0, 0), elbow - shoulder, .043, .032, sleeve_color, 7, parent=arm)
        lower = empty(label + "Forearm", elbow)
        tube(("ForearmSleeve" if sleeves == "long" else "ForearmSkin") + label, (0, 0, 0), wrist - elbow,
             .034, .022, sleeve_color if sleeves == "long" else skin, 7, parent=lower)
        hand = empty(label + "Hand", wrist)
        ellipsoid("Palm" + label, (0, -.005, -.025), (.026, .016, .038), skin, 1, parent=hand)
        for finger in range(4):
            x = (finger - 1.5) * .012
            tube("Finger" + label + str(finger), (x, -.008, -.046), (x, -.011, -.080 + abs(finger - 1.5) * .006),
                 .0055, .0045, skin, 5, parent=hand)
        tube("Thumb" + label, (-side * .019, -.012, -.013), (-side * .038, -.019, -.042), .009, .006, skin, 6, parent=hand)
        hip, knee, ankle = (side * .073, 0, .90), (side * .085, 0, .49), (side * .09, 0, .10)
        tube("Thigh" + label, hip, knee, .072, .050, mat(trousers), 8)
        tube("Calf" + label, knee, ankle, .047, .031, mat(trousers) if leg_length == "long" else skin, 8)
        if footwear != "bare":
            tube("BootTop" + label, (side * .09, 0, .09), (side * .09, 0, .21), .039, .037, mat(footwear), 8)
        ellipsoid("Foot" + label, (side * .09, -.045, .052), (.049, .104, .05),
                  skin if footwear == "bare" else mat(footwear), 1)
    empty("Grip")
    empty("FaceFront", (0, -.10, 1.58))


def hat(style, color, band=None):
    base = mat(color)
    if style in ("swim", "baseball", "flat", "helmet"):
        radius = (.099, .090, .056 if style != "helmet" else .080)
        ellipsoid("HatCrown", (0, .002, 1.680), radius, base, 2)
        if style in ("baseball", "flat"):
            ellipsoid("HatBill", (0, -.076, 1.657), (.100, .088, .011), base, 1)
        if style == "helmet":
            tube("HelmetRim", (0, 0, 1.644), (0, 0, 1.656), .102, .102, mat("Olive"), 12)
    elif style == "captain":
        tube("HatCrown", (0, 0, 1.689), (0, 0, 1.753), .089, .107, base, 10)
        ellipsoid("HatBill", (0, -.070, 1.676), (.105, .071, .009), base, 1)
    else:
        if style == "cowboy":
            points = [(math.cos(i * math.tau / 12) * .147, math.sin(i * math.tau / 12) * .106,
                       1.679 + abs(math.cos(i * math.tau / 12)) ** 2 * .026 + dz)
                      for dz in (0, .008) for i in range(12)]
            faces = [tuple(reversed(range(12))), tuple(range(12, 24))]
            faces += [(i, (i + 1) % 12, (i + 1) % 12 + 12, i + 12) for i in range(12)]
            mesh("CowboyBrim", points, faces, base)
        else:
            tube("HatBrim", (0, 0, 1.675), (0, 0, 1.689), .129, .129, base, 12)
        tube("HatCrown", (0, 0, 1.687), (0, 0, 1.767), .096, .078, base, 10)
    if band:
        tube("HatBand", (0, 0, 1.689), (0, 0, 1.703), .098, .096, mat(band), 12)


def hair(style, color):
    shade = mat(color)
    ellipsoid("HairTop", (0, .01, 1.680), (.092, .084, .052), shade, 1)
    if style == "curly":
        for i in range(9):
            a = i * math.tau / 9
            ellipsoid("HairCurl", (.067 * math.cos(a), .063 * math.sin(a), 1.675), (.032, .031, .034), shade, 1)
    elif style == "bob":
        for side in (-1, 1):
            ellipsoid("HairSide", (side * .076, .005, 1.57), (.033, .063, .125), shade, 1)
        ellipsoid("HairBack", (0, .060, 1.566), (.076, .036, .122), shade, 1)
    elif style == "braids":
        for side in (-1, 1):
            for index in range(5):
                ellipsoid("Braid", (side * (.080 + .006 * (index % 2)), -.003, 1.624 - index * .041),
                          (.014, .019, .028), shade, 1)
    else:
        for side in (-1, 1):
            ellipsoid("HairTemple", (side * .078, .023, 1.627), (.018, .052, .045), shade, 1)


def glasses(rim, lenses="Black"):
    for side in (-1, 1):
        center = Vector((side * .037, -.079, 1.599))
        box("Lens", center, (.051, .012, .029), mat(lenses))
        points = [center + Vector((math.cos(i * math.tau / 8) * .030, -.007,
                                  math.sin(i * math.tau / 8) * .020)) for i in range(8)]
        for i in range(8): tube("GlassesRim", points[i], points[(i + 1) % 8], .003, .003, mat(rim), 4)
        tube("GlassesArm", (side * .066, -.08, 1.605), (side * .084, .024, 1.608), .003, .003, mat(rim), 4)
    tube("GlassesBridge", (-.01, -.085, 1.604), (.01, -.085, 1.604), .004, .004, mat(rim), 5)


def beard(color="Silver"):
    for side in (-1, 1):
        ellipsoid("SideBeard", (side * .063, -.028, 1.511), (.024, .047, .060), mat(color), 1)
        tube("Moustache", (side * .008, -.080, 1.541), (side * .046, -.073, 1.526), .011, .007, mat(color), 6)
    front_patch("BeardTip", [(-.039, 1.505), (0, 1.463), (.039, 1.505)], color, -.052)


def bib(color):
    box("BibPanel", (0, -.083, 1.143), (.190, .025, .34), mat(color))
    box("BibPocket", (0, -.100, 1.15), (.100, .014, .075), mat(color))
    for side in (-1, 1):
        tube("BibStrap", (side * .079, -.091, 1.28), (side * .091, -.037, 1.373), .015, .015, mat(color), 4)
        tube("RearStrap", (side * .091, -.037, 1.373), (side * .078, .065, 1.12), .015, .015, mat(color), 4)


def vest(color):
    for side in (-1, 1):
        points = [(side * .018, 1.29), (side * .094, 1.375), (side * .125, 1.26),
                  (side * .106, .93), (side * .025, .885)]
        front_patch("VestFront", points, color, -.096)
    box("VestBack", (0, .076, 1.13), (.18, .016, .39), mat(color))


def outfit(name):
    basic = {
        "Badman": ("Skin", "bare", "Red", "short", "bare"),
        "Bikini": ("Skin", "bare", "Skin", "short", "bare"),
        "Fisherman": ("BlueGrey", "long", "Orange", "long", "Black"),
        "Sailor": ("White", "long", "White", "long", "White"),
        "LighthouseKeeper": ("Navy", "long", "Navy", "long", "Grey"),
        "SwampMan": ("BlueGrey", "long", "Rose", "long", "Grey"),
        "SwampLady": ("Red", "short", "Blue", "long", "Brown"),
        "KioskLady": ("Red", "short", "Skin", "short", "Red"),
        "Tourist": ("Skin", "bare", "Skin", "short", "bare"),
        "GrillMaster": ("White", "short", "Cream", "short", "Black"),
        "Andrei": ("White", "long", "Navy", "long", "Grey"),
        "Jacob": ("White", "long", "Navy", "long", "Grey"),
        "GunstoreClerc": ("Red", "short", "Brown", "long", "Black"),
        "ScaredGuyInShorts": ("Orange", "short", "Blue", "short", "bare"),
        "StoreGrandma": ("White", "short", "Blue", "long", "Grey"),
        "Military": ("Olive", "long", "Olive", "long", "Brown"),
        "Scientist": ("Yellow", "long", "Yellow", "long", "Yellow"),
        "Bean": ("BeanGreen", "long", "BeanGreen", "long", "Black")}
    body(*basic[name])
    if name == "Badman":
        hat("swim", "Grey"); glasses("Cyan")
    elif name == "Bikini":
        hair("bob", "Brown")
        for side in (-1, 1):
            front_patch("BikiniCup", [(side * .008, 1.20), (side * .105, 1.21), (side * .067, 1.305)], "Red", -.087)
            tube("BikiniStrap", (side * .065, -.074, 1.28), (side * .090, -.025, 1.363), .006, .006, mat("Red"), 5)
        front_patch("BikiniBrief", [(-.106, .948), (.106, .948), (0, .824)], "Red", -.076)
        tube("BikiniWaist", (-.105, -.075, .943), (.105, -.075, .943), .009, .009, mat("Red"), 5)
        for side in (-1, 1):
            ellipsoid("HairBow", (.084 + side * .019, -.031, 1.698), (.022, .011, .016), mat("Pink"), 1)
    elif name == "Fisherman":
        bib("Orange"); hat("bucket", "Orange", "Yellow")
    elif name == "Bean":
        bib("BeanGreen"); hat("bucket", "BeanGreen", "Cream")
        points = [(.047 * math.cos(i * math.tau / 24), 1.17 + .067 * math.sin(i * math.tau / 24)) for i in range(24)]
        front_patch("BeanBadge", points, "White", -.111)
        seam = [(-.012, -.114, 1.229), (.012, -.114, 1.200), (-.009, -.114, 1.151), (.010, -.114, 1.112)]
        for index in range(len(seam) - 1):
            tube("BeanBadgeSeam", seam[index], seam[index + 1], .0025, .0025, mat("BeanGreen"), 5)
    elif name == "Sailor":
        for side in (-1, 1):
            front_patch("SailorCollar", [(0, 1.25), (side * .119, 1.34), (side * .041, 1.379)], "Blue", -.092)
        front_patch("SailorTie", [(-.012, 1.295), (0, 1.158), (.012, 1.295)], "Navy", -.110)
        box("SleeveBadge", (.165, -.040, 1.274), (.045, .010, .057), mat("Navy"))
        box("BadgeStripe", (.165, -.047, 1.263), (.038, .003, .005), mat("Gold"))
    elif name == "LighthouseKeeper":
        hat("captain", "Navy", "Gold"); beard()
        for side in (-1, 1):
            front_patch("CoatLapel", [(side * .115, 1.329), (side * .020, 1.231), (side * .038, 1.374)], "BlueGrey", -.10)
        for z in (1.15, 1.025): ellipsoid("CoatButton", (0, -.092, z), (.011, .006, .011), mat("Gold"), 1)
        tube("PipeStem", (-.030, -.077, 1.526), (-.094, -.125, 1.511), .004, .004, mat("Brown"), 6)
        tube("PipeBowl", (-.094, -.125, 1.503), (-.094, -.125, 1.539), .012, .017, mat("Brown"), 8)
    elif name == "SwampMan":
        hat("captain", "White", "Cyan"); beard("White"); vest("Rose")
    elif name == "SwampLady":
        bib("Blue"); hair("braids", "Brown"); hat("cowboy", "White", "Red")
    elif name == "KioskLady":
        hair("bob", "Blonde"); hat("flat", "White", "Red")
        for x in (-.08, -.025, .025, .08): box("BlouseStripe", (x, -.085, 1.15), (.018, .02, .35), mat("White"))
        count = 12
        vertices = [(math.cos(i * math.tau / count) * rx, math.sin(i * math.tau / count) * ry, z)
                    for rx, ry, z in ((.105, .078, .94), (.180, .113, .59)) for i in range(count)]
        for i in range(count):
            j = (i + 1) % count
            mesh("SkirtPanel", [vertices[i], vertices[j], vertices[j + count], vertices[i + count]],
                 [(3, 2, 1, 0)], mat("White" if i % 3 == 0 else "Red"))
        box("WaistApron", (0, -.099, .93), (.176, .027, .135), mat("White"))
        for side in (-1, 1): tube("Sock", (side * .09, 0, .16), (side * .09, 0, .30), .038, .040, mat("White"), 8)
    elif name == "Tourist":
        hat("bucket", "Cream", "Brown")
        for side in (-1, 1):
            box("LifeVestPad", (side * .064, -.087, 1.15), (.118, .093, .38), mat("Yellow"))
            tube("ArmFloat", (side * .152, 0, 1.325), (side * .181, -.004, 1.228), .066, .066, mat("Yellow"), 9)
        box("VestBelt", (0, -.138, 1.03), (.252, .016, .027), mat("Black"))
        box("VestZip", (0, -.139, 1.16), (.010, .014, .35), mat("Black"))
        front_patch("SwimBrief", [(-.11, .947), (.11, .947), (0, .808)], "Black", -.08)
    elif name == "GrillMaster":
        hat("baseball", "Grey"); glasses("Black")
        box("ApronBib", (0, -.085, 1.172), (.192, .025, .34), mat("Apron"))
        box("ApronSkirt", (0, -.095, .822), (.225, .027, .40), mat("Apron"))
        for side in (-1, 1): tube("ApronStrap", (side * .078, -.095, 1.32), (side * .075, -.012, 1.38), .009, .009, mat("Apron"), 4)
    elif name in ("Andrei", "Jacob"):
        hair("curly" if name == "Andrei" else "short", "Grey" if name == "Andrei" else "Ginger")
        vest("Navy")
        for side in (-1, 1):
            front_patch("BowTie", [(0, 1.331), (side * .033, 1.350), (side * .033, 1.316)], "Black", -.105)
    elif name == "GunstoreClerc":
        hat("flat", "Rose"); vest("Brown")
        box("LeatherApron", (0, -.094, 1.098), (.20, .022, .33), mat("Brown"))
    elif name == "ScaredGuyInShorts":
        for side in (-1, 1):
            front_patch("PoloCollar", [(0, 1.305), (side * .071, 1.348), (side * .040, 1.377)], "White", -.092)
            tube("ShortsHem", (side * .085, 0, .488), (side * .085, 0, .514), .052, .053, mat("BlueGrey"), 8)
    elif name == "StoreGrandma":
        hair("curly", "Silver")
        box("StoreBadge", (-.067, -.082, 1.268), (.042, .013, .027), mat("Red"))
        for x in (-.079, -.068, -.057): box("BadgeMark", (x, -.090, 1.268), (.003, .003, .011), mat("White"))
    elif name == "Military":
        hat("helmet", "Helmet")
        for side in (-1, 1):
            box("ChestPocket", (side * .067, -.081, 1.24), (.074, .022, .086), mat("Helmet"))
            box("CargoPocket", (side * .107, -.039, .70), (.083, .040, .106), mat("Olive"))
        box("Belt", (0, -.077, .955), (.211, .019, .027), mat("Helmet"))
        box("Buckle", (0, -.09, .955), (.027, .009, .024), mat("Black"))
    elif name == "Scientist":
        hair("short", "Black"); hat("swim", "Grey"); glasses("White")
        box("Mask", (0, -.083, 1.541), (.103, .022, .044), mat("Cyan"))
        box("SuitZip", (0, -.085, 1.143), (.020, .015, .41), mat("White"))


def export_candidate(project, name):
    """Keep the shared export basis/settings, but never touch production Assets here."""
    source = project / "ArtSource~/how_to_fish" / (name + ".blend")
    target = project / "Library/HowToFish/OutfitExports" / (name + ".fbx")
    source.parent.mkdir(parents=True, exist_ok=True)
    target.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.view_layer.update()
    objects = [obj for obj in bpy.context.scene.objects if obj.type in {"MESH", "EMPTY"}]
    def bounds():
        points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
        return [min(point[i] for point in points) for i in range(3)], [max(point[i] for point in points) for i in range(3)]
    low, high = bounds()
    scale = 1.75 / (high[2] - low[2])
    scaled_meshes = set()
    for obj in objects:
        obj.location *= scale
        if obj.type == "MESH" and obj.data not in scaled_meshes:
            obj.data.transform(Matrix.Scale(scale, 4))
            scaled_meshes.add(obj.data)
    for obj in art.ROOT.children:
        if obj.name != "Grip": obj.location.z -= low[2] * scale
    bpy.context.view_layer.update()
    low, high = bounds()
    assert abs(high[2] - 1.75) < .001 and abs(low[2]) < .001
    assert all(all(abs(v - 1) < .00001 for v in obj.scale) for obj in objects)
    assert all(all(abs(v) < .00001 for v in obj.rotation_euler) for obj in objects)
    assert bpy.data.objects["RightHand"].matrix_world.translation.x < 0 < bpy.data.objects["LeftHand"].matrix_world.translation.x
    assert bpy.data.objects["Grip"].location.length < .00001
    mounts = {key: list(bpy.data.objects[key].matrix_world.translation) for key in ("RightHand", "LeftHand", "Grip", "FaceFront")}
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    basis = Matrix.Rotation(-math.pi / 2, 4, "X")
    local_matrices = {obj: obj.matrix_local.copy() for obj in objects}
    converted = set()
    for obj, local in local_matrices.items():
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_basis = basis @ local @ basis.inverted()
        if obj.type == "MESH" and obj.data not in converted:
            obj.data.transform(basis)
            converted.add(obj.data)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True, object_types={"MESH", "EMPTY"},
                           axis_forward="Z", axis_up="Y", global_scale=1, apply_unit_scale=True,
                           apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, use_space_transform=False,
                           add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=True, path_mode="AUTO")
    report = {"blender": bpy.app.version_string, "source_forward": "-Y", "source_up": "+Z", "source_right": "-X",
              "target_forward": "+Z", "target_up": "+Y", "target_right": "+X", "export_axis_forward": "Z", "export_axis_up": "Y",
              "bake_space_transform": True, "use_space_transform": False, "unsaved_export_basis": "RotationX(-90 degrees)",
              "source_bounds_m": {"min": low, "max": high}, "source_mounts_m": mounts,
              "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "unity_import_verified": False,
              "candidate_fbx": str(target), "objects": sorted(obj.name for obj in objects),
              "design_note": "Self-authored from visible front reference; unseen back, neutral face and standing pose inferred. No animation rig."}
    if name == "OutfitBean":
        report["design_note"] = "Original Bean reward appearance is unverified. This is an independent inferred bean-themed design: green waterproof clothing, ivory bean badge and dark boots; not a reconstruction of the original or Mr Bean. Same hand/pivot contract; no animation rig."
    source.with_suffix(".json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(name, "height=1.75", "objects=" + str(len(objects)), report["sha256"])


def render_icons(project, names):
    """Render saved sources only; cameras/lights stay in the unsaved preview scene."""
    import numpy as np
    from bpy_extras.object_utils import world_to_camera_view
    output = project / "Library/HowToFish/OutfitExports/Icons"
    output.mkdir(parents=True, exist_ok=True)
    for name in names:
        bpy.ops.wm.open_mainfile(filepath=str(project / "ArtSource~/how_to_fish" / ("Outfit" + name + ".blend")))
        scene = bpy.context.scene
        corners = [obj.matrix_world @ Vector(corner) for obj in scene.objects if obj.type == "MESH" for corner in obj.bound_box]
        low = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
        high = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
        center = (low + high) / 2
        bpy.ops.object.camera_add(location=center + Vector((.55, -4, .25)))
        camera = bpy.context.object
        camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 2
        scene.camera = camera
        scene.render.engine = "BLENDER_EEVEE"
        scene.render.resolution_x = 384
        scene.render.resolution_y = 512
        scene.render.resolution_percentage = 100
        scene.render.film_transparent = True
        scene.render.image_settings.file_format = "PNG"
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.image_settings.color_depth = "8"
        bpy.context.view_layer.update()
        projected = [world_to_camera_view(scene, camera, point) for point in corners]
        width = max(point.x for point in projected) - min(point.x for point in projected)
        height = max(point.y for point in projected) - min(point.y for point in projected)
        camera.data.ortho_scale *= max(width / .88, height / .90)
        for location, energy, size in (((-3, -4, 5), 450, 4), ((3, -2, 2), 180, 3)):
            bpy.ops.object.light_add(type="AREA", location=center + Vector(location))
            light = bpy.context.object
            light.data.energy = energy
            light.data.size = size
            light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(output / ("Outfit" + name + ".png"))
        bpy.ops.render.render(write_still=True)
        icon = bpy.data.images.load(scene.render.filepath, check_existing=False)
        pixels = np.empty(384 * 512 * 4, dtype=np.float32)
        icon.pixels.foreach_get(pixels)
        alpha = pixels.reshape(512, 384, 4)[:, :, 3]
        rows, columns = np.nonzero(alpha > .01)
        assert len(rows) and rows.min() > 4 and rows.max() < 507 and columns.min() > 4 and columns.max() < 379, name + " cropped"
        assert rows.max() - rows.min() > 512 * .80, name + " too small"
        assert alpha[0, 0] == 0 and alpha[-1, -1] == 0, name + " missing transparency"
        print("ICON_OK", name, "alpha bounds", (int(columns.min()), int(rows.min()), int(columns.max()), int(rows.max())))
        bpy.data.images.remove(icon)


if __name__ == "__main__":
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    arguments = sys.argv[sys.argv.index("--") + 2:]
    requested = [name for name in arguments if name != "--icons"] or NAMES
    assert all(name in NAMES for name in requested)
    if "--icons" in arguments:
        render_icons(project, requested)
    else:
        for name in requested:
            bpy.ops.object.select_all(action="SELECT")
            bpy.ops.object.delete(use_global=False)
            art.ROOT = None
            art.ROOT = empty("Outfit" + name)
            outfit(name)
            export_candidate(project, "Outfit" + name)
