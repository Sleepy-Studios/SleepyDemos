"""Original Jinx Casino visual sources. Run with Blender --background --factory-startup.
Source front=-Y, up=Z, character right=-X. Export gate settings are fixed, not guessed.
Only owned source blends and Library staging exports are written; never Unity Assets.
"""
import argparse
import hashlib
import json
import math
import os
import sys
from pathlib import Path
import bpy
import bmesh
from io_scene_fbx import fbx_utils
from mathutils import Matrix, Vector

ARGS = argparse.ArgumentParser()
ARGS.add_argument('--phase', choices=['first', 'all'], default='first')
ARGS.add_argument('--render', action='store_true')
ARGS.add_argument('--icons', action='store_true')
ARGS.add_argument('--preserve-existing', action='store_true')
ARGS.add_argument('--refresh-avatar', action='store_true')
ARGS.add_argument('--refresh-previews', action='store_true')
ARGS.add_argument('--refresh-model', nargs='*', default=[])
ARGS.add_argument('--icon-model', nargs='*', default=[])
OPTIONS = ARGS.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
WORKSPACE = Path(__file__).resolve().parents[2]
SOURCE = WORKSPACE / 'ArtSource/jinx_casino'
STAGING = WORKSPACE / 'Library/JinxCasino/Staging'
MODELS = STAGING / 'Models'
PREVIEWS = STAGING / 'Previews'
MODELS.mkdir(parents=True, exist_ok=True)
PREVIEWS.mkdir(parents=True, exist_ok=True)
if OPTIONS.refresh_previews:
    # Load only this task's generated source, never an interactive user's Blender session.
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE / 'jinx_casino_models.blend'))
    preview_scene = bpy.context.scene
    preview_camera = preview_scene.camera
    preview_manifest = json.loads((MODELS / 'manifest.json').read_text(encoding='utf-8'))
    for entry in preview_manifest['models']:
        for obj in bpy.data.collections[entry['name']].objects:
            obj.hide_render = entry['name'] != 'Avatar'
            obj.hide_set(entry['name'] != 'Avatar')
    bpy.ops.object.select_all(action='DESELECT')
    avatar_root = bpy.data.objects['Avatar']; avatar_root.select_set(True); bpy.context.view_layer.objects.active = avatar_root
    preview_camera.data.ortho_scale = 2.65
    preview_scene.render.filepath = str(PREVIEWS / 'Avatar.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'jinx_casino_models.blend'))
    bpy.ops.render.render(write_still=True)
    print('JINX_UPDATED_AVATAR_PREVIEW_READY', flush=True)
    for image_file in (STAGING / 'Icons').glob('*.png'):
        loaded = bpy.data.images.load(str(image_file), check_existing=False)
        assert tuple(loaded.size) == (512, 512) and loaded.channels == 4
        assert loaded.pixels[3] == 0, image_file.name
        bpy.data.images.remove(loaded)
        entry = next(value for value in preview_manifest['models'] if value.get('icon') == image_file.name)
        entry['iconSha256'] = hashlib.sha256(image_file.read_bytes()).hexdigest()
        entry['iconSize'] = [512, 512]; entry['iconTransparent'] = True
    (MODELS / 'manifest.json').write_text(json.dumps(preview_manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    for gallery_name, names, columns, spacing, scale in [
        ('P4StationsGallery', ['Slots','Roulette','CoinFlip','Blackjack','SicBo','Plinko','PassingBag','CooperativeVault','ChickenElevator'], 3, 4, 16),
        ('P4ItemsGallery', ['Item_' + item for item in ['bubble_gun','banana_peel','spring_glove','fake_jackpot','ink_balloon','magnet_hat','remote_horn','disguise_spray','relay_battery','rescue_whistle','duo_wrench','extra_time','teleport_bell','shared_shield','reroll_dice','redraw_card','lock_reel','xray','stop_loss','jackpot_coupon','map_radar','shortcut_key','event_remote','mystery_coupon']], 6, 1.25, 10),
        ('P4CosmeticsGallery', ['hat_party','hat_banana','hat_mechanic','hat_crown','hat_lucky','Face_emote_wave','Face_emote_clap','Face_emote_shrug','Face_emote_dance','Face_emote_salute','Face_emote_bow','Face_emote_crown','Face_emote_fireworks'], 5, .95, 6.5)]:
        rows = math.ceil(len(names) / columns)
        for entry in preview_manifest['models']:
            active = entry['name'] in names
            for obj in bpy.data.collections[entry['name']].objects:
                obj.hide_render = not active; obj.hide_set(not active)
        for i, name in enumerate(names): bpy.data.objects[name].location = ((i % columns - (columns - 1) / 2) * spacing, (i // columns - (rows - 1) / 2) * spacing, .26 if name.startswith('Face_') else 0)
        target = Vector((0, 0, .7 if gallery_name != 'P4StationsGallery' else 1.1))
        preview_camera.location = target + Vector((-scale * .45, -scale * .85, scale * .82))
        preview_camera.rotation_euler = (target - preview_camera.location).to_track_quat('-Z', 'Y').to_euler()
        preview_camera.data.ortho_scale = scale
        preview_scene.render.resolution_x = 1600; preview_scene.render.resolution_y = 1100
        for light_name, location, power in [('Key', (-scale * .45, -scale * .4, scale), 200 * scale), ('Fill', (scale * .5, 0, scale * .7), 150 * scale), ('Rim', (0, scale * .45, scale * .8), 250 * scale)]:
            obj = bpy.data.objects['Studio_' + light_name]; obj.location = location; obj.data.energy = power; obj.data.size = scale * .3
            obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()
        preview_scene.render.filepath = str(PREVIEWS / (gallery_name + '.png'))
        bpy.ops.render.render(write_still=True)
        for name in names: bpy.data.objects[name].location = (0, 0, 0)
        print('JINX_GALLERY_READY ' + gallery_name, flush=True)
    print('JINX_PREVIEW_REFRESH_COMPLETE', flush=True)
    sys.exit(0)
bpy.ops.wm.read_factory_settings(use_empty=True)
SCENE = bpy.context.scene
SCENE.unit_settings.system = 'METRIC'
SCENE.unit_settings.scale_length = 1.0

PALETTE = {
    'InkBlue': ('14213B', .15, .43), 'PlumRed': ('AD315D', .08, .43),
    'CreamYellow': ('F4DC9A', .02, .58), 'Mint': ('54C4AD', .08, .42),
    'CopperGold': ('BE8C50', .72, .31), 'FaceInk': ('101623', .05, .55),
    'PaperLight': ('FFF1CB', .0, .68),
    'color_blue': ('36A8FF', .12, .42), 'color_pink': ('FF6BA9', .12, .42),
    'color_mint': ('56DFB0', .12, .42), 'color_gold': ('F5CA57', .12, .42),
    'color_violet': ('A57AFF', .12, .42),
}
MATERIALS = {}
for material_name, (hex_color, metallic, roughness) in PALETTE.items():
    srgb = tuple(int(hex_color[i:i + 2], 16) / 255 for i in (0, 2, 4))
    rgb = tuple(value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4 for value in srgb)
    material = bpy.data.materials.new(material_name)
    material.diffuse_color = (*rgb, 1)
    material.use_nodes = True
    shader = material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*rgb, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    MATERIALS[material_name] = material


class Model:
    """Geometry coordinates are Blender source coordinates in metres, before parenting."""
    def __init__(self, name, category, rule_id=None):
        self.name, self.category, self.rule_id = name, category, rule_id
        self.collection = bpy.data.collections.new(name)
        SCENE.collection.children.link(self.collection)
        self.root = bpy.data.objects.new(name, None)
        self.collection.objects.link(self.root)
        self.root.empty_display_size = .15
        self.pivots = []

    def own(self, obj, name, material='InkBlue', parent=None):
        obj.name = self.name + '.' + name
        for old_collection in list(obj.users_collection):
            old_collection.objects.unlink(obj)
        self.collection.objects.link(obj)
        if obj.type == 'MESH':
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
            obj.data.materials.append(MATERIALS[material])
        world = obj.matrix_world.copy()
        obj.parent = parent or self.root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_world = world
        obj.select_set(False)
        return obj

    def pivot(self, name, position=(0, 0, 0), parent=None):
        obj = bpy.data.objects.new(self.name + '.' + name, None)
        self.collection.objects.link(obj)
        obj.location = position
        bpy.context.view_layer.update()
        world = obj.matrix_world.copy()
        obj.parent = parent or self.root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_world = world
        obj.empty_display_size = .12
        self.pivots.append(obj)
        return obj

    def bevel(self, obj, amount=.035, segments=2):
        if amount <= 0:
            return obj
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new('CraftedEdges', 'BEVEL')
        modifier.width = amount
        modifier.segments = segments
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        return obj

    def box(self, name, position, size, material='InkBlue', bevel=.035, parent=None, rotation=None):
        bpy.ops.mesh.primitive_cube_add(size=1, location=position)
        obj = bpy.context.object
        obj.dimensions = size
        if rotation:
            obj.rotation_euler = rotation
        return self.bevel(self.own(obj, name, material, parent), bevel)

    def cylinder(self, name, position, radius, depth, material='CopperGold', parent=None, axis='Z', vertices=24):
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=position)
        obj = bpy.context.object
        if axis == 'Y':
            obj.rotation_euler.x = math.pi / 2
        elif axis == 'X':
            obj.rotation_euler.y = math.pi / 2
        return self.bevel(self.own(obj, name, material, parent), min(.014, depth / 4, radius / 6), 2)

    def sphere(self, name, position, scale, material='CreamYellow', parent=None):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1, location=position)
        obj = bpy.context.object
        obj.scale = scale
        return self.own(obj, name, material, parent)

    def cone(self, name, position, radius1, radius2, depth, material='CopperGold', parent=None, axis='Z', vertices=16):
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=position)
        obj = bpy.context.object
        if axis == 'Y': obj.rotation_euler.x = math.pi / 2
        return self.own(obj, name, material, parent)

    def torus(self, name, position, radius, tube=.04, material='CopperGold', parent=None, axis='Z'):
        bpy.ops.mesh.primitive_torus_add(major_segments=32, minor_segments=8, major_radius=radius, minor_radius=tube, location=position)
        obj = bpy.context.object
        if axis == 'Y': obj.rotation_euler.x = math.pi / 2
        if axis == 'X': obj.rotation_euler.y = math.pi / 2
        return self.own(obj, name, material, parent)

    def rod(self, name, start, end, radius=.035, material='CopperGold', parent=None):
        start, end = Vector(start), Vector(end)
        direction = end - start
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=direction.length, location=(start + end) / 2)
        obj = bpy.context.object
        obj.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()
        return self.own(obj, name, material, parent)

    def mesh(self, name, vertices, faces, material='InkBlue', parent=None, bevel=.012):
        mesh = bpy.data.meshes.new(self.name + '.' + name)
        mesh.from_pydata(vertices, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(self.name + '.' + name, mesh)
        SCENE.collection.objects.link(obj)
        self.own(obj, name, material, parent)
        edges = {}
        for face in faces:
            for i, vertex in enumerate(face):
                edge = tuple(sorted((vertex, face[(i + 1) % len(face)])))
                edges[edge] = edges.get(edge, 0) + 1
        if any(count == 1 for count in edges.values()) and self.name not in ('Slots', 'CooperativeVault'):
            bpy.context.view_layer.objects.active = obj
            thickness = obj.modifiers.new('FoldedPaperThickness', 'SOLIDIFY'); thickness.thickness = .008
            bpy.ops.object.modifier_apply(modifier=thickness.name)
        return self.bevel(obj, bevel)

    def combine(self):
        # Shared static meshes become one renderer per semantic parent; moving pivots remain independent.
        parents = [self.root] + self.pivots
        for parent in parents:
            meshes = [obj for obj in parent.children if obj.type == 'MESH']
            if len(meshes) < 2:
                continue
            bpy.ops.object.select_all(action='DESELECT')
            for obj in meshes: obj.select_set(True)
            bpy.context.view_layer.objects.active = meshes[0]
            bpy.ops.object.join()
            meshes[0].name = parent.name + '.Mesh'
            # Join preserves the active object's origin; bake its translation into mesh vertices.
            offset = meshes[0].location.copy()
            meshes[0].data.transform(Matrix.Translation(offset))
            meshes[0].location = (0, 0, 0)
            meshes[0].select_set(False)
        bpy.context.view_layer.update()


def radial(model, parent, name, radius, height, count, material='CreamYellow', size=(.1, .1, .04)):
    for i in range(count):
        angle = 2 * math.pi * i / count
        model.box(name + str(i), (math.cos(angle) * radius, math.sin(angle) * radius, height), size, material, .008, parent)


def base(model, width=2.3, depth=1.4):
    # Separate tapered supports and a floating rim give even flat game surfaces a crafted silhouette.
    for i, x in enumerate((-width / 2 + .22, width / 2 - .22)):
        for j, y in enumerate((-.43, .43)):
            model.box('Foot' + str(i) + str(j), (x, y, .08), (.38, .46, .16), 'CopperGold', .04)
            model.box('Leg' + str(i) + str(j), (x, y, .57), (.2, .23, 1), 'InkBlue', .03)
    model.box('Apron', (0, 0, 1.02), (width - .16, depth - .14, .22), 'PlumRed', .04)
    model.box('Rim', (0, 0, 1.16), (width, depth, .13), 'CopperGold', .055)
    model.box('Surface', (0, 0, 1.215), (width - .1, depth - .1, .055), 'Mint', .025)


def avatar():
    model = Model('Avatar', 'Avatar')
    body = model.pivot('Body', (0, 0, .88))
    head = model.pivot('Head', (0, 0, 1.64))
    left = model.pivot('ArmLeftPivot', (.34, 0, 1.25))
    right = model.pivot('ArmRightPivot', (-.34, 0, 1.25))
    model.pivot('HatMount', (0, 0, 1.92), head)
    for i, x in enumerate((-.17, .17)):
        model.box('Boot' + str(i), (x, -.07, .095), (.25, .4, .19), 'InkBlue', .065)
        model.cylinder('Ankle' + str(i), (x, 0, .27), .08, .26, 'CopperGold')
    vertices = []
    for z, width, front, back in ((.35, .4, -.2, .17), (.96, .28, -.19, .15), (1.37, .32, -.16, .14)):
        vertices += [(-width, front, z), (width, front, z), (width, back, z), (-width, back, z)]
    faces = [(0, 3, 2, 1), (8, 9, 10, 11)] + [(ring * 4 + i, ring * 4 + (i + 1) % 4, (ring + 1) * 4 + (i + 1) % 4, (ring + 1) * 4 + i) for ring in range(2) for i in range(4)]
    coat = model.mesh('Coat', vertices, faces, 'color_blue', body, .035)
    for sign in (-1, 1):
        model.mesh('FoldedLapel' + str(sign), [(sign * .05, -.235, .9), (sign * .28, -.2, 1.36), (sign * .03, -.245, 1.18)], [(0, 1, 2)], 'CreamYellow', body, 0)
        pivot = left if sign == 1 else right
        model.rod('Sleeve' + str(sign), (sign * .36, 0, 1.22), (sign * .45, -.02, .83), .115, 'color_blue', pivot)
        model.cylinder('Cuff' + str(sign), (sign * .45, -.02, .83), .13, .08, 'CopperGold', pivot)
        model.sphere('Glove' + str(sign), (sign * .45, -.035, .72), (.115, .12, .13), 'CreamYellow', pivot)
    for i in range(3): model.sphere('Button' + str(i), (0, -.223, .69 + i * .16), (.035, .021, .035), 'CopperGold', body)
    model.box('Belt', (0, -.215, .6), (.68, .06, .1), 'InkBlue', .012, body)
    model.box('Buckle', (0, -.259, .6), (.12, .025, .095), 'CopperGold', .012, body)
    model.cylinder('NeckSpring', (0, 0, 1.42), .105, .16, 'CopperGold')
    mask_vertices = [(-.30, -.155, 1.40), (.30, -.155, 1.40), (.31, -.14, 1.85), (0, -.22, 1.925), (-.31, -.14, 1.85), (0, -.235, 1.62), (-.27, .11, 1.43), (.27, .11, 1.43), (.28, .1, 1.84), (-.28, .1, 1.84)]
    mask_faces = [(0, 1, 5), (1, 2, 5), (2, 3, 5), (3, 4, 5), (4, 0, 5), (6, 9, 8, 7), (0, 6, 7, 1), (1, 7, 8, 2), (2, 8, 9, 4, 3), (4, 9, 6, 0)]
    mask = model.mesh('FoldedMask', mask_vertices, mask_faces, 'CreamYellow', head, .01)
    mask.data.materials.append(MATERIALS['PaperLight'])
    for face in mask.data.polygons: face.material_index = face.index % 2
    for i, x in enumerate((-.125, .125)):
        model.sphere('Eye' + str(i), (x, -.225, 1.68), (.045, .018, .07), 'FaceInk', head)
        model.box('Brow' + str(i), (x, -.245, 1.79), (.13, .024, .03), 'FaceInk', .006, head, (0, (-1 if i == 0 else 1) * .18, 0))
    model.box('Mouth', (0, -.249, 1.51), (.14, .024, .028), 'PlumRed', .008, head)
    key = model.pivot('HeadKey', (0, .14, 1.72), head)
    model.rod('KeyShaft', (0, .12, 1.72), (0, .29, 1.72), .035, 'CopperGold', key)
    for sign in (-1, 1): model.torus('KeyLoop' + str(sign), (sign * .09, .3, 1.72), .085, .025, 'CopperGold', key, 'Y')
    return model


def slots():
    model = Model('Slots', 'Station', 'Slots')
    base(model, 2.45, 1.45)
    model.box('FruitCrate', (0, .16, 1.76), (1.96, .65, 1.08), 'PlumRed', .14)
    model.box('DisplayInset', (0, -.215, 1.75), (1.7, .12, .7), 'InkBlue', .075)
    model.box('Marquee', (0, .08, 2.39), (2.18, .56, .28), 'CopperGold', .08)
    for i, x in enumerate((-.55, 0, .55)):
        pivot = model.pivot('Reel' + str(i), (x, -.29, 1.77))
        model.cylinder('ReelDrum' + str(i), (x, -.29, 1.77), .29, .43, 'CreamYellow', pivot, 'X')
        for symbol in range(6):
            angle = symbol * math.pi / 3
            model.sphere('Fruit' + str(i) + str(symbol), (x, -.29 - math.cos(angle) * .29, 1.77 + math.sin(angle) * .29), (.095, .045, .095), 'PlumRed' if symbol % 2 else 'Mint', pivot)
            model.rod('FruitStem' + str(i) + str(symbol), (x, -.29 - math.cos(angle) * .33, 1.82 + math.sin(angle) * .29), (x + .035, -.29 - math.cos(angle) * .33, 1.86 + math.sin(angle) * .29), .009, 'CopperGold', pivot)
    model.box('CoinSlot', (0, -.58, 1.31), (.28, .05, .04), 'FaceInk', .008)
    model.box('PrizeTray', (0, -.55, 1.13), (.74, .3, .13), 'InkBlue', .035)
    lever = model.pivot('Lever', (-1.17, .02, 1.24))
    model.cylinder('LeverAxle', (-1.12, .02, 1.24), .1, .22, 'CopperGold', axis='X')
    model.rod('LeverStem', (-1.17, .02, 1.24), (-1.17, -.12, 1.97), .045, 'CopperGold', lever)
    model.sphere('LeverKnob', (-1.17, -.12, 1.97), (.12, .12, .12), 'Mint', lever)
    for i in range(7): model.sphere('Bulb' + str(i), (-.9 + i * .3, -.225, 2.41), (.065, .035, .065), 'CreamYellow')
    return model


def vault():
    model = Model('CooperativeVault', 'Station', 'CooperativeVault')
    for x in (-.86, .86): model.box('SplayedFoot' + str(x), (x, 0, .13), (.58, 1.28, .26), 'CopperGold', .07)
    model.box('VaultShell', (0, .1, 1.29), (2.35, 1.25, 2.16), 'InkBlue', .18)
    model.box('FrontFrame', (0, -.57, 1.28), (2.1, .17, 1.88), 'CopperGold', .14)
    door = model.pivot('Door', (1.02, -.72, 1.3))
    model.box('DoorPlate', (0, -.74, 1.29), (1.83, .15, 1.62), 'PlumRed', .105, door)
    wheel = model.pivot('Wheel', (0, -.9, 1.33), door)
    model.torus('SafeWheel', (0, -.91, 1.33), .37, .048, 'CopperGold', wheel, 'Y')
    model.cylinder('WheelHub', (0, -.92, 1.33), .105, .12, 'CreamYellow', wheel, 'Y')
    for i in range(5):
        angle = i * 2 * math.pi / 5
        model.rod('WheelSpoke' + str(i), (0, -.92, 1.33), (.34 * math.cos(angle), -.92, 1.33 + .34 * math.sin(angle)), .036, 'CopperGold', wheel)
    for z in (.78, 1.74): model.cylinder('Hinge' + str(z), (1.01, -.75, z), .09, .28, 'CopperGold')
    for i in range(3):
        model.box('CodeClue' + str(i), (-.44 + .44 * i, -.86, 2.12), (.31, .08, .21), 'Mint', .025)
        for dot in range(i + 1): model.sphere('ClueMark' + str(i) + str(dot), (-.5 + .44 * i + dot * .055, -.914, 2.12), (.018, .012, .025), 'FaceInk')
    for x in (-.79, .79):
        for z in (.58, 1.98): model.sphere('Rivet' + str(x) + str(z), (x, -.854, z), (.045, .025, .045), 'CopperGold', door)
    model.box('TeamCounter', (0, -.66, .35), (1.2, .45, .1), 'CreamYellow', .035)
    return model


def game_station(kind):
    model = Model(kind, 'Station', kind)
    if kind == 'Roulette':
        model.cylinder('OctagonalFoot', (0, 0, .12), .72, .24, 'InkBlue', vertices=8)
        model.cylinder('FlutedStem', (0, 0, .62), .32, .96, 'PlumRed', vertices=10)
        model.cylinder('RoundRim', (0, 0, 1.16), 1.18, .16, 'CopperGold', vertices=48)
        wheel = model.pivot('Wheel', (0, 0, 1.27))
        model.cylinder('WheelBowl', (0, 0, 1.28), 1.08, .16, 'InkBlue', wheel, vertices=48)
        model.torus('OuterTrack', (0, 0, 1.38), 1.035, .042, 'CopperGold', wheel)
        for i in range(24):
            angle = i * math.pi / 12
            model.box('Pocket' + str(i), (.78 * math.cos(angle), .78 * math.sin(angle), 1.39), (.15, .19, .07), 'PlumRed' if i % 2 else 'CreamYellow', .012, wheel, (0, 0, angle))
        model.cylinder('Spindle', (0, 0, 1.5), .13, .32, 'CopperGold', wheel)
        model.sphere('Ball', (.25, -.96, 1.46), (.055, .055, .055), 'Mint')
        radial(model, model.root, 'Flute', .35, .62, 10, 'CopperGold', (.05, .05, .7))
    elif kind == 'CoinFlip':
        base(model, 2.25, 1.25)
        for x in (-.78, .78):
            model.box('Upright' + str(x), (x, .04, 1.91), (.2, .32, 1.3), 'CopperGold', .06)
            model.sphere('PostCap' + str(x), (x, .04, 2.61), (.16, .16, .16), 'Mint')
        model.rod('Suspension', (-.79, .04, 2.45), (.79, .04, 2.45), .06, 'InkBlue')
        coin = model.pivot('Coin', (0, -.03, 1.91))
        model.cylinder('Medallion', (0, -.03, 1.91), .53, .14, 'CopperGold', coin, 'Y', 48)
        model.torus('MintedRim', (0, -.115, 1.91), .46, .032, 'CreamYellow', coin, 'Y')
        model.mesh('FoldedDiamond', [(-.19, -.125, 1.91), (0, -.15, 2.21), (.19, -.125, 1.91), (0, -.15, 1.61)], [(0, 1, 2), (0, 2, 3)], 'PlumRed', coin, 0)
        model.box('ChoiceTray', (0, -.38, 1.3), (.9, .32, .1), 'InkBlue', .03)
    elif kind == 'Blackjack':
        base(model, 2.7, 1.65)
        model.cylinder('CardFanPedestal', (.72, .18, 1.37), .22, .28, 'InkBlue')
        for i in range(5):
            angle = (i - 2) * .16
            card_y = .32 + abs(i - 2) * .042
            model.box('HandCard' + str(i), ((i - 2) * .15, card_y, 1.68 + abs(i - 2) * .025), (.36, .06, .6), 'CreamYellow', .02, rotation=(0, angle, 0))
            model.sphere('Suit' + str(i), ((i - 2) * .15, card_y - .046, 1.71), (.045, .012, .07), 'PlumRed')
        dealer = model.pivot('DealerArm', (.89, .32, 1.44))
        model.rod('MechanicalElbow', (.89, .32, 1.44), (.89, -.08, 1.87), .065, 'CopperGold', dealer)
        model.rod('DealerForearm', (.89, -.08, 1.87), (.48, -.27, 1.53), .05, 'CopperGold', dealer)
        model.box('DeckShoe', (-.82, .15, 1.37), (.48, .57, .25), 'PlumRed', .055)
        for i in range(4): model.box('StackCard' + str(i), (-.82, .15, 1.49 + i * .025), (.38, .45, .018), 'PaperLight', .008)
        for x in (-.66, 0, .66): model.torus('BettingRing' + str(x), (x, -.42, 1.26), .17, .012, 'CreamYellow')
    elif kind == 'SicBo':
        base(model, 2.6, 1.55)
        for i, x in enumerate((-.72, 0, .72)):
            model.cylinder('CupFoot' + str(i), (x, .05, 1.3), .3, .14, 'CopperGold')
            cup = model.pivot('DiceCup' + str(i), (x, .05, 1.38))
            model.torus('CupCrown' + str(i), (x, .05, 1.93), .27, .035, 'CopperGold', cup)
            for j in range(6):
                a = j * math.pi / 3
                model.rod('Cage' + str(i) + str(j), (x + .28 * math.cos(a), .05 + .28 * math.sin(a), 1.38), (x + .23 * math.cos(a), .05 + .23 * math.sin(a), 1.92), .017, 'InkBlue', cup)
            die = model.pivot('Die' + str(i), (x, .05, 1.59))
            model.box('DieBody' + str(i), (x, .05, 1.59), (.34, .34, .34), 'CreamYellow', .05, die)
            for j in range(i + 1): model.sphere('Pip' + str(i) + str(j), (x - .075 + j * .075, -.127, 1.59), (.023, .012, .023), 'PlumRed', die)
        model.box('BetRegion', (0, -.46, 1.27), (1.6, .27, .04), 'PlumRed', .02)
    elif kind == 'DragonTiger':
        base(model, 2.6, 1.5)
        for sign in (-1, 1):
            model.cylinder('AnimalPedestal' + str(sign), (sign * .76, .1, 1.4), .35, .36, 'CopperGold')
            model.rod('AnimalNeck' + str(sign), (sign * .76, .1, 1.5), (sign * .59, .06, 1.96), .12, 'Mint' if sign == -1 else 'PlumRed')
            model.sphere('AnimalHead' + str(sign), (sign * .65, -.02, 2.09), (.31, .23, .25), 'Mint' if sign == -1 else 'PlumRed')
            for x in (sign * .65 - .12, sign * .65 + .12): model.sphere('AnimalEye' + str(x), (x, -.226, 2.12), (.045, .025, .055), 'FaceInk')
            if sign == -1:
                for x in (-.86, -.5): model.rod('DragonHorn' + str(x), (x, .08, 2.24), (x, .17, 2.49), .04, 'CreamYellow')
                model.rod('DragonWhisker', (-.68, -.21, 2.01), (-1.08, -.27, 1.98), .022, 'CopperGold')
            else:
                for x in (.44, .86): model.sphere('TigerEar' + str(x), (x, .08, 2.29), (.11, .06, .12), 'CreamYellow')
                for i in range(3): model.box('TigerStripe' + str(i), (.55 + i * .1, -.23, 2.27), (.035, .025, .09), 'FaceInk', .008)
        model.box('VersusGlyph', (0, -.34, 1.42), (.14, .18, .42), 'InkBlue', .04, rotation=(0, .25, 0))
    elif kind == 'HighLow':
        base(model, 2.35, 1.5)
        for sign in (-1, 1):
            model.box('BookCover' + str(sign), (sign * .49, .09, 1.56), (.95, .79, .12), 'PlumRed', .04, rotation=(0, sign * .13, 0))
            for i in range(5): model.box('BookLeaf' + str(sign) + str(i), (sign * .49, .09, 1.64 + i * .021), (.88, .74, .018), 'CreamYellow', .008, rotation=(0, sign * .13, 0))
            model.mesh('Arrow' + str(sign), [(sign * .52, -.29, 1.86), (sign * .76, -.29, 1.62), (sign * .58, -.29, 1.62), (sign * .58, -.29, 1.42), (sign * .45, -.29, 1.42), (sign * .45, -.29, 1.62), (sign * .28, -.29, 1.62)], [(0, 1, 2, 3, 4, 5, 6)], 'Mint', bevel=0)
        page = model.pivot('TurnPage', (0, .1, 1.68))
        model.box('TurningLeaf', (.31, .1, 1.98), (.59, .72, .025), 'PaperLight', .009, page, (0, .62, 0))
        model.cylinder('Spine', (0, .1, 1.69), .075, .83, 'CopperGold', axis='Y')
    elif kind == 'LuckyDraw':
        model.cylinder('HexagonalFoot', (0, 0, .12), .71, .24, 'InkBlue', vertices=6)
        model.cylinder('Stem', (0, 0, .64), .23, 1.05, 'CopperGold', vertices=12)
        model.cylinder('Tray', (0, 0, 1.2), 1.02, .14, 'PlumRed', vertices=12)
        for i in range(6):
            a = i * math.pi / 3; x, y = .62 * math.cos(a), .62 * math.sin(a)
            tube = model.pivot('Tube' + str(i), (x, y, 1.28))
            model.cylinder('SignatureTube' + str(i), (x, y, 1.6), .22, .7, 'Mint' if i % 2 else 'CreamYellow', tube, vertices=8)
            model.torus('TubeRim' + str(i), (x, y, 1.97), .195, .024, 'CopperGold', tube)
            for j in range(3): model.box('Ticket' + str(i) + str(j), (x - .06 + j * .06, y, 2.09 + j * .035), (.045, .025, .4), 'PaperLight', .008, tube)
    elif kind == 'Bingo':
        base(model, 2.05, 1.45)
        globe = model.pivot('Globe', (0, .05, 1.89))
        for axis in ('X', 'Y', 'Z'): model.torus('GlobeRing' + axis, (0, .05, 1.89), .53, .029, 'CopperGold', globe, axis)
        for i in range(8):
            a = i * math.pi / 4
            model.sphere('NumberBall' + str(i), (.31 * math.cos(a), .05 + .24 * math.sin(a), 1.79 + .18 * math.sin(a * 2)), (.095, .095, .095), 'Mint' if i % 2 else 'PlumRed', globe)
        model.cylinder('GlobeSupport', (0, .05, 1.37), .23, .3, 'InkBlue')
        bell = model.pivot('Bell', (0, .05, 2.48))
        model.sphere('BellShell', (0, .05, 2.47), (.25, .25, .2), 'CopperGold', bell)
        model.cylinder('NumberPlate', (0, -.58, 1.31), .22, .09, 'CreamYellow', axis='Y')
        model.box('BingoGrid', (0, -.56, 1.17), (1.3, .31, .08), 'CreamYellow', .025)
        for i in range(12): model.sphere('GridMark' + str(i), ((i % 4 - 1.5) * .22, -.65 + i // 4 * .09, 1.225), (.025, .025, .015), 'PlumRed')
    elif kind == 'Plinko':
        base(model, 2.1, 1.45)
        model.box('PegBoard', (0, .29, 2.14), (1.85, .2, 1.76), 'InkBlue', .07)
        # Seven initial positions branch through eight rows into fifteen actual pockets.
        # Source slot index runs -X to +X; the proven Unity mapping is its negative.
        # Unity X=(3+Level*.5-Cursor)*.114, Y=2.91-Level*.19875.
        for row in range(8):
            for column in range(7 + row):
                model.cylinder('Peg' + str(row) + str(column), ((column - (6 + row) / 2) * .114, .145, 2.810625 - row * .19875), .016, .1, 'CopperGold', axis='Y', vertices=12)
        for x in (-.95, .95): model.box('GuideRail' + str(x), (x, .1, 2.16), (.08, .16, 1.85), 'Mint', .025)
        for i in range(16): model.box('PocketDivider' + str(i), (-.855 + i * .114, -.12, 1.34), (.018, .49, .29), 'CopperGold', .005)
        ball = model.pivot('Ball', (0, .13, 2.91)); model.sphere('DropBall', (0, .13, 2.91), (.042, .042, .042), 'CreamYellow', ball)
        gate = model.pivot('DropGate', (0, .16, 3.0)); model.box('DropFunnel', (0, .12, 3.03), (.52, .33, .18), 'PlumRed', .04, gate)
    elif kind == 'CooperativeLevers':
        base(model, 2.7, 1.5)
        for i in range(6):
            x = -1.0 + i * .4
            model.box('TimingColumn' + str(i), (x, .15, 1.7), (.31, .45, .87), 'InkBlue', .045)
            timing = model.pivot('TimingFace' + str(i), (x, -.12, 1.95))
            # Face is an independent semantic renderer; shared URP palette can light it
            # without recolouring the cabinet, lever or other players' timing windows.
            model.cylinder('TimingDisk' + str(i), (x, -.12, 1.95), .135, .06, 'CreamYellow', timing, axis='Y')
            model.rod('TimingNeedle' + str(i), (x, -.16, 1.95), (x + .075, -.16, 2.01), .014, 'PlumRed', timing)
            lever = model.pivot('Lever' + str(i), (x, -.32, 1.27))
            model.rod('Handle' + str(i), (x, -.32, 1.27), (x, -.53, 1.67), .04, 'CopperGold', lever)
            model.sphere('Grip' + str(i), (x, -.53, 1.67), (.1, .1, .1), 'Mint' if i % 2 else 'PlumRed', lever)
            model.rod('SignalWire' + str(i), (x, .17, 2.16), (0, .17, 2.37), .025, 'CopperGold')
        model.sphere('SyncLamp', (0, .17, 2.4), (.12, .12, .12), 'Mint')
    elif kind == 'PushYourLuckDice':
        base(model, 2.2, 1.4)
        tower = model.pivot('DiceTower', (0, .1, 1.32))
        for i in range(3):
            model.box('DiceStack' + str(i), ((i % 2) * .13 - .065, .12, 1.48 + i * .38), (.45, .45, .4), 'CreamYellow' if i % 2 else 'PlumRed', .065, tower, (0, 0, i * .13))
            for j in range(i + 2): model.sphere('StackPip' + str(i) + str(j), ((j % 2 - .5) * .16, -.117, 1.41 + i * .38 + j // 2 * .13), (.026, .018, .026), 'FaceInk', tower)
        model.box('CashTray', (0, -.42, 1.28), (1.2, .47, .18), 'Mint', .04)
        for i in range(5): model.cylinder('BankChip' + str(i), (-.45 + i * .22, -.42, 1.4 + i % 2 * .05), .085, .035, 'CopperGold')
        model.rod('RiskGauge', (.77, .25, 1.25), (.77, .25, 2.41), .035, 'CopperGold')
        for i in range(5): model.box('GaugeMark' + str(i), (.77, .18, 1.45 + i * .19), (.16, .06, .045), 'Mint' if i < 3 else 'PlumRed', .008)
    elif kind == 'PassingBag':
        base(model, 2.65, 1.5)
        for x in (-1.0, 1.0): model.rod('RelayArm' + str(x), (x, .35, 1.27), (x * .8, .35, 2.22), .07, 'CopperGold')
        model.rod('Crossbar', (-.8, .35, 2.22), (.8, .35, 2.22), .055, 'InkBlue')
        bag = model.pivot('Bag', (0, .16, 2.15))
        model.sphere('PaperBag', (0, .12, 1.73), (.42, .26, .43), 'PlumRed', bag)
        model.box('FoldedBagNeck', (0, .12, 2.04), (.35, .19, .17), 'CreamYellow', .035, bag)
        model.torus('SuspensionLoop', (0, .14, 2.14), .14, .03, 'CopperGold', bag, 'Y')
        for x in (-.23, .23): model.box('BagFold' + str(x), (x, -.12, 1.75), (.045, .035, .39), 'CreamYellow', .01, bag, (0, x, 0))
        model.rod('Fuse', (0, .12, 2.08), (.38, .12, 2.42), .02, 'InkBlue', bag)
        model.sphere('FuseSpark', (.38, .12, 2.42), (.095, .095, .095), 'CopperGold', bag)
        for x in (-.74, .74): model.sphere('ReceivingPalm' + str(x), (x, -.39, 1.31), (.22, .19, .075), 'Mint')
    elif kind == 'BlindAuction':
        base(model, 2.5, 1.55)
        for y in (-.35, .3):
            for i in range(4): model.box('CratePlank' + str(y) + str(i), (0, y, 1.37 + i * .2), (1.52, .07, .16), 'CreamYellow', .015)
        for x in (-.72, .72): model.box('CrateSide' + str(x), (x, 0, 1.67), (.09, .7, .8), 'CopperGold', .025)
        lid = model.pivot('Lid', (0, .33, 2.07))
        model.box('MysteryLid', (0, 0, 2.1), (1.6, .79, .1), 'PlumRed', .035, lid)
        model.torus('QuestionLoop', (0, -.095, 2.33), .15, .035, 'CopperGold', lid, 'Y')
        model.sphere('QuestionDot', (0, -.10, 2.11), (.04, .025, .04), 'CopperGold', lid)
        hammer = model.pivot('Gavel', (-.98, -.3, 1.26))
        model.rod('GavelHandle', (-.98, -.3, 1.26), (-.98, -.3, 1.93), .045, 'InkBlue', hammer)
        model.cylinder('GavelHead', (-.98, -.3, 1.93), .12, .44, 'CopperGold', hammer, 'X')
        model.box('CluePocket', (.91, -.23, 1.48), (.28, .27, .45), 'Mint', .03)
    elif kind == 'MechanicalRace':
        base(model, 2.85, 1.75)
        model.box('RaceBed', (0, 0, 1.28), (2.6, 1.47, .12), 'InkBlue', .14)
        for lane in range(4):
            y = -.48 + lane * .32
            model.rod('Lane' + str(lane), (-1.1, y, 1.35), (1.1, y, 1.35), .012, 'CreamYellow')
            racer = model.pivot('Racer' + str(lane), (-.7 + lane * .17, y, 1.45))
            model.sphere('RacerShell' + str(lane), (-.7 + lane * .17, y, 1.45), (.22, .1, .12), ['PlumRed', 'Mint', 'CopperGold', 'CreamYellow'][lane], racer)
            for x in (-.85 + lane * .17, -.58 + lane * .17): model.cylinder('RacerWheel' + str(lane) + str(x), (x, y, 1.39), .065, .27, 'CopperGold', racer, 'Y', 12)
            model.rod('RacerFlag' + str(lane), (-.75 + lane * .17, y, 1.53), (-.75 + lane * .17, y, 1.75), .013, 'CopperGold', racer)
        for x in (-1.05, 1.05): model.box('FinishPost' + str(x), (x, .63, 1.82), (.09, .11, 1), 'PlumRed', .025)
        model.box('FinishArch', (0, .63, 2.3), (2.2, .13, .23), 'CreamYellow', .03)
        for i in range(10): model.box('Checker' + str(i), (-.9 + i * .2, .552, 2.3), (.095, .025, .13), 'FaceInk', .006)
    elif kind == 'ChickenElevator':
        base(model, 2.4, 1.55)
        for x in (-.62, .62):
            model.box('LiftShaft' + str(x), (x, .22, 2.03), (.55, .51, 1.61), 'InkBlue', .055)
            for i in range(5): model.box('FloorLine' + str(x) + str(i), (x, -.054, 1.37 + i * .28), (.52, .045, .027), 'CopperGold', .008)
            cage = model.pivot('Elevator' + str(x), (x, -.10, 1.42))
            model.box('LiftCar' + str(x), (x, -.14, 1.46), (.47, .37, .35), 'Mint' if x < 0 else 'PlumRed', .04, cage)
            for delta in (-.14, .14): model.rod('LiftBar' + str(x) + str(delta), (x + delta, -.342, 1.33), (x + delta, -.342, 1.6), .016, 'CreamYellow', cage)
        model.sphere('ChickenHead', (0, .22, 2.72), (.31, .28, .26), 'CreamYellow')
        model.mesh('ChickenBeak', [(-.13, -.01, 2.7), (.13, -.01, 2.7), (0, -.3, 2.6), (0, -.01, 2.53)], [(0, 1, 2), (0, 2, 3), (1, 3, 2)], 'CopperGold', bevel=.008)
        for x in (-.12, .12): model.sphere('ChickenEye' + str(x), (x, -.015, 2.8), (.025, .014, .035), 'FaceInk')
        for i in range(3): model.sphere('Comb' + str(i), ((i - 1) * .09, .22, 2.98), (.08, .055, .08), 'PlumRed')
    return model


ITEM_IDS = ['bubble_gun', 'banana_peel', 'spring_glove', 'fake_jackpot', 'ink_balloon', 'magnet_hat',
    'remote_horn', 'disguise_spray', 'relay_battery', 'rescue_whistle', 'duo_wrench', 'extra_time',
    'teleport_bell', 'shared_shield', 'reroll_dice', 'redraw_card', 'lock_reel', 'xray', 'stop_loss',
    'jackpot_coupon', 'map_radar', 'shortcut_key', 'event_remote', 'mystery_coupon']


def gear(model, name, centre, radius, material='CopperGold', parent=None):
    x, y, z = centre
    model.cylinder(name + 'Hub', centre, radius * .82, .09, material, parent, 'Y')
    for i in range(12):
        angle = i * math.pi / 6
        model.box(name + 'Tooth' + str(i), (x + radius * math.cos(angle), y, z + radius * math.sin(angle)), (radius * .32, .11, radius * .2), material, .012, parent, (0, -angle, 0))


def banana(model, name, size=1, centre=(0, 0, 0), material='CreamYellow'):
    vertices, faces = [], []
    for i in range(9):
        angle = -.9 + i * .22
        point = Vector((math.sin(angle) * .42 * size, 0, (.48 - math.cos(angle) * .32) * size)) + Vector(centre)
        radius = .065 * size * (.45 + .55 * math.sin(math.pi * i / 8))
        for j in range(6):
            theta = j * math.pi / 3
            vertices.append(tuple(point + Vector((math.cos(theta) * radius, math.sin(theta) * radius, 0))))
    for i in range(8):
        for j in range(6): faces.append((i * 6 + j, i * 6 + (j + 1) % 6, (i + 1) * 6 + (j + 1) % 6, (i + 1) * 6 + j))
    faces += [tuple(range(5, -1, -1)), tuple(48 + j for j in range(6))]
    model.mesh(name, vertices, faces, material, bevel=.008)


def star(model, name, centre, radius, material='CopperGold', parent=None):
    x, y, z = centre
    vertices = [(x, y - .015, z)]
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5; r = radius if i % 2 == 0 else radius * .45
        vertices.append((x + r * math.cos(a), y, z + r * math.sin(a)))
    model.mesh(name, vertices, [(0, i + 1, (i + 1) % 10 + 1) for i in range(10)], material, parent, 0)


def prop(item_id):
    model = Model('Item_' + item_id, 'Item', item_id)
    if item_id == 'bubble_gun':
        model.box('ToyBody', (0, .03, .29), (.25, .45, .2), 'PlumRed', .055)
        model.box('Grip', (0, .13, .14), (.15, .18, .29), 'InkBlue', .04, rotation=(.2, 0, 0))
        model.cylinder('BubbleNozzle', (0, -.28, .31), .12, .22, 'CopperGold', axis='Y')
        model.torus('Muzzle', (0, -.402, .31), .11, .022, 'Mint', axis='Y')
        model.torus('TriggerGuard', (0, -.06, .17), .065, .015, 'CopperGold', axis='X')
        model.sphere('BubbleTank', (0, .14, .48), (.1, .14, .11), 'Mint')
    elif item_id == 'banana_peel':
        for i in range(3):
            angle = i * 2 * math.pi / 3
            c, s = math.cos(angle), math.sin(angle)
            vertices = []
            for j in range(5):
                r, height, width = j * .09, .28 * (1 - j / 4) ** 2 + .015, .04 + .035 * math.sin(j * math.pi / 4)
                vertices += [(r * c - width * s, r * s + width * c, height), (r * c + width * s, r * s - width * c, height)]
            model.mesh('Peel' + str(i), vertices, [(j * 2, j * 2 + 1, j * 2 + 3, j * 2 + 2) for j in range(4)], 'CreamYellow', bevel=0)
        model.cylinder('BananaStalk', (0, 0, .31), .036, .12, 'CopperGold')
    elif item_id == 'spring_glove':
        for i in range(6): model.torus('SpringCoil' + str(i), (0, 0, .09 + i * .053), .1, .018, 'CopperGold')
        model.box('Cuff', (0, 0, .06), (.27, .24, .12), 'InkBlue', .025)
        model.sphere('BoxingGlove', (0, -.035, .48), (.21, .19, .23), 'PlumRed')
        model.sphere('Thumb', (-.18, -.02, .43), (.085, .12, .09), 'PlumRed')
        model.box('GloveStitch', (0, -.211, .51), (.21, .025, .04), 'CreamYellow', .008)
    elif item_id == 'fake_jackpot':
        model.box('RadioCase', (0, 0, .21), (.62, .24, .4), 'PlumRed', .05)
        for x in (-.19, -.07, .05): model.box('SpeakerSlot' + str(x), (x, -.136, .22), (.035, .025, .18), 'FaceInk', .008)
        model.cylinder('RadioDial', (.21, -.14, .21), .075, .035, 'CopperGold', axis='Y')
        model.rod('Antenna', (.2, .02, .41), (.31, .02, .74), .018, 'CopperGold')
        star(model, 'FakePrize', (0, -.139, .53), .16, 'CreamYellow')
        for x in (-.24, .24): model.sphere('RubberFoot' + str(x), (x, 0, .04), (.07, .1, .04), 'InkBlue')
    elif item_id == 'ink_balloon':
        model.sphere('InkBalloon', (0, 0, .51), (.24, .21, .29), 'PlumRed')
        model.cone('TiedNeck', (0, 0, .24), .06, .025, .08, 'InkBlue')
        for i in range(4): model.rod('String' + str(i), (.04 * math.sin(i), 0, i * .055), (.04 * math.sin(i + 1), 0, (i + 1) * .055), .008, 'CreamYellow')
        model.sphere('InkSpot', (0, -.212, .51), (.09, .01, .105), 'FaceInk')
    elif item_id == 'magnet_hat':
        model.cylinder('HatBrim', (0, 0, .07), .34, .1, 'InkBlue')
        model.box('MagnetBase', (0, 0, .15), (.37, .2, .14), 'CopperGold', .04)
        for x in (-.19, .19):
            model.box('MagnetPole' + str(x), (x, 0, .34), (.15, .17, .32), 'PlumRed' if x < 0 else 'Mint', .04)
            model.box('PoleCap' + str(x), (x, 0, .52), (.16, .18, .09), 'CreamYellow', .018)
    elif item_id == 'remote_horn':
        model.box('HornGrip', (0, .06, .14), (.12, .18, .28), 'PlumRed', .04)
        model.cylinder('HornNeck', (0, 0, .35), .075, .3, 'CopperGold', axis='Y')
        model.cone('TrumpetBell', (0, -.24, .35), .08, .23, .31, 'CopperGold', axis='Y')
        model.cylinder('BellInterior', (0, -.402, .35), .18, .017, 'FaceInk', axis='Y')
        model.torus('BellLip', (0, -.415, .35), .215, .022, 'CreamYellow', axis='Y')
        model.sphere('SqueezeBulb', (0, .23, .35), (.13, .13, .13), 'Mint')
    elif item_id == 'disguise_spray':
        model.cylinder('SprayCan', (0, 0, .25), .16, .5, 'Mint')
        model.cylinder('TopCap', (0, 0, .53), .13, .08, 'CopperGold')
        model.box('SprayNozzle', (0, -.02, .6), (.14, .15, .08), 'InkBlue', .02)
        model.box('MaskLabel', (0, -.16, .28), (.21, .025, .19), 'CreamYellow', .025)
        for x in (-.053, .053): model.sphere('LabelEye' + str(x), (x, -.18, .3), (.021, .012, .028), 'FaceInk')
    elif item_id == 'relay_battery':
        model.box('Battery', (0, 0, .29), (.39, .25, .53), 'InkBlue', .06)
        for x in (-.11, .11): model.cylinder('Terminal' + str(x), (x, 0, .58), .055, .08, 'CopperGold')
        model.mesh('Lightning', [(-.04, -.139, .48), (.1, -.139, .48), (.01, -.139, .31), (.1, -.139, .31), (-.08, -.139, .11), (-.01, -.139, .3), (-.1, -.139, .3)], [(0, 1, 2, 3, 4, 5, 6)], 'Mint', bevel=0)
    elif item_id == 'rescue_whistle':
        model.box('WhistleBody', (0, 0, .12), (.34, .4, .23), 'CopperGold', .06)
        model.box('AirMouth', (0, -.25, .15), (.2, .15, .14), 'CreamYellow', .025)
        model.box('AirSlot', (0, -.334, .17), (.14, .02, .03), 'FaceInk', .005)
        model.torus('WhistleLoop', (0, .27, .14), .105, .025, 'Mint', axis='Y')
        model.box('Flag', (.18, .12, .42), (.06, .12, .48), 'PlumRed', .02)
    elif item_id == 'duo_wrench':
        for sign in (-1, 1):
            model.rod('WrenchStem' + str(sign), (sign * .13, 0, .08), (-sign * .13, 0, .6), .048, 'Mint' if sign < 0 else 'CopperGold')
            model.torus('WrenchRing' + str(sign), (sign * .13, 0, .09), .075, .023, 'Mint' if sign < 0 else 'CopperGold', axis='Y')
            for delta in (-.07, .07): model.box('WrenchJaw' + str(sign) + str(delta), (-sign * .13 + delta, 0, .62), (.065, .09, .16), 'CreamYellow', .018)
    elif item_id == 'extra_time':
        model.cylinder('Clock', (0, 0, .39), .31, .15, 'CopperGold', axis='Y')
        model.cylinder('ClockFace', (0, -.086, .39), .26, .025, 'CreamYellow', axis='Y')
        model.torus('ClockRim', (0, -.107, .39), .277, .023, 'InkBlue', axis='Y')
        model.rod('HourHand', (0, -.11, .39), (.12, -.11, .45), .014, 'PlumRed')
        model.rod('MinuteHand', (0, -.11, .39), (0, -.11, .58), .012, 'InkBlue')
        for x in (-.14, .14): model.box('ClockFoot' + str(x), (x, 0, .06), (.12, .2, .12), 'Mint', .025)
        model.cylinder('ClockCrown', (0, 0, .76), .06, .13, 'CopperGold')
    elif item_id == 'teleport_bell':
        model.cone('BellSkirt', (0, 0, .25), .25, .13, .42, 'CopperGold')
        model.torus('BellLip', (0, 0, .05), .23, .025, 'CreamYellow')
        model.sphere('Clapper', (0, 0, .06), (.07, .07, .06), 'PlumRed')
        model.torus('TopHandle', (0, 0, .52), .11, .026, 'Mint', axis='Y')
        for i in range(3): model.box('TeleportMark' + str(i), ((i - 1) * .12, -.193, .25), (.04, .025, .11), 'InkBlue', .008)
    elif item_id == 'shared_shield':
        verts = [(-.3, -.055, .63), (.3, -.055, .63), (.31, -.055, .28), (0, -.09, .02), (-.31, -.055, .28), (0, -.14, .38)]
        model.mesh('FoldedShield', verts, [(i, (i + 1) % 5, 5) for i in range(5)], 'Mint', bevel=0)
        for i in range(5): model.rod('ShieldEdge' + str(i), verts[i], verts[(i + 1) % 5], .026, 'CopperGold')
        star(model, 'ShieldStar', (0, -.17, .39), .13, 'CreamYellow')
        model.torus('ShieldGrip', (0, .02, .38), .1, .02, 'InkBlue', axis='Y')
    elif item_id == 'reroll_dice':
        model.box('Die', (0, 0, .25), (.46, .46, .46), 'Mint', .065)
        for x, z in [(-.1, .15), (.1, .15), (0, .25), (-.1, .35), (.1, .35)]: model.sphere('DiePip' + str(x) + str(z), (x, -.242, z), (.025, .012, .025), 'FaceInk')
        model.torus('RerollHalo', (0, .03, .25), .34, .025, 'CopperGold', axis='Y')
    elif item_id in ('redraw_card', 'stop_loss', 'jackpot_coupon', 'mystery_coupon'):
        colors = {'redraw_card': 'CreamYellow', 'stop_loss': 'Mint', 'jackpot_coupon': 'PlumRed', 'mystery_coupon': 'PaperLight'}
        for i in range(3 if item_id == 'redraw_card' else 1): model.box('Card' + str(i), ((i - 1) * .08 if item_id == 'redraw_card' else 0, i * .028, .31), (.38, .035, .59), colors[item_id], .025, rotation=(0, (i - 1) * .13 if item_id == 'redraw_card' else 0, 0))
        if item_id == 'stop_loss':
            model.box('StopBar', (0, -.037, .33), (.27, .022, .07), 'FaceInk', .015)
            model.torus('StopCircle', (0, -.037, .33), .18, .025, 'CopperGold', axis='Y')
        elif item_id == 'mystery_coupon':
            model.torus('MysteryLoop', (0, -.04, .4), .1, .025, 'PlumRed', axis='Y')
            model.sphere('MysteryDot', (0, -.04, .19), (.03, .015, .03), 'PlumRed')
        else: star(model, 'PrizeStar', (0, -.05, .34), .15, 'CopperGold')
        for i in range(3): model.sphere('CouponPerforation' + str(i), (-.105 + i * .105, -.027, .073), (.018, .012, .018), 'InkBlue')
    elif item_id == 'lock_reel':
        gear(model, 'Cog', (0, 0, .32), .26)
        model.box('LockBody', (0, -.085, .32), (.26, .13, .22), 'PlumRed', .035)
        model.torus('LockShackle', (0, -.08, .51), .09, .025, 'InkBlue', axis='Y')
        model.box('KeySlot', (0, -.161, .31), (.035, .021, .07), 'FaceInk', .006)
    elif item_id == 'xray':
        for x in (-.18, .18):
            model.torus('LensFrame' + str(x), (x, 0, .2), .15, .028, 'CopperGold', axis='Y')
            model.cylinder('Lens' + str(x), (x, 0, .2), .125, .025, 'Mint', axis='Y')
        model.rod('NoseBridge', (-.04, 0, .2), (.04, 0, .2), .02, 'CopperGold')
        for x in (-.34, .34): model.rod('GlassesArm' + str(x), (x, 0, .2), (x, .27, .15), .018, 'InkBlue')
    elif item_id == 'map_radar':
        model.cylinder('RadarCase', (0, 0, .35), .29, .17, 'InkBlue', axis='Y')
        model.cylinder('RadarFace', (0, -.1, .35), .24, .025, 'Mint', axis='Y')
        for r in (.09, .17, .25): model.torus('RadarRing' + str(r), (0, -.12, .35), r, .008, 'CopperGold', axis='Y')
        model.rod('RadarSweep', (0, -.13, .35), (.16, -.13, .5), .012, 'CreamYellow')
        for x, z in [(-.1, .4), (.1, .22)]: model.sphere('RadarDot' + str(x), (x, -.137, z), (.025, .012, .025), 'PlumRed')
        model.box('RadarFoot', (0, 0, .045), (.31, .26, .09), 'CopperGold', .025)
    elif item_id == 'shortcut_key':
        model.torus('KeyRing', (0, 0, .58), .13, .035, 'CopperGold', axis='Y')
        model.rod('KeyStem', (0, 0, .12), (0, 0, .48), .035, 'CopperGold')
        for z in (.13, .22): model.box('KeyTooth' + str(z), (.085, 0, z), (.18, .09, .052), 'CopperGold', .009)
        model.box('KeyRibbon', (0, -.036, .58), (.11, .035, .1), 'Mint', .02)
    elif item_id == 'event_remote':
        model.box('RemoteCase', (0, 0, .32), (.28, .16, .62), 'PlumRed', .055)
        for i in range(3): model.sphere('RemoteButton' + str(i), (0, -.094, .22 + i * .12), (.065, .022, .042), 'Mint' if i == 2 else 'CreamYellow')
        model.rod('RemoteAntenna', (0, 0, .64), (.045, 0, .85), .018, 'CopperGold')
        model.sphere('AntennaTip', (.045, 0, .85), (.035, .035, .035), 'CopperGold')
    return model


def hat(hat_id):
    model = Model(hat_id, 'Hat', hat_id)
    model.cylinder('Brim', (0, 0, .04), .35, .08, 'InkBlue', vertices=32)
    if hat_id == 'hat_party':
        model.cone('PaperCone', (0, 0, .25), .29, .018, .43, 'PlumRed', vertices=6)
        for i in range(6):
            a = i * math.pi / 3
            model.rod('FoldSeam' + str(i), (.285 * math.cos(a), .285 * math.sin(a), .05), (0, 0, .465), .009, 'CreamYellow')
        model.sphere('Pom', (0, 0, .5), (.065, .065, .065), 'Mint')
    elif hat_id == 'hat_banana':
        banana(model, 'BananaCrown', 1, (0, 0, .07))
        banana(model, 'SecondBanana', .7, (0, .13, .1))
        model.box('FruitSticker', (0, -.09, .23), (.13, .035, .09), 'Mint', .02)
    elif hat_id == 'hat_mechanic':
        model.sphere('Cap', (0, .035, .15), (.3, .27, .17), 'Mint')
        model.box('Visor', (0, -.27, .06), (.48, .25, .07), 'InkBlue', .04)
        for x in (-.12, .12): model.torus('Goggle' + str(x), (x, -.235, .17), .083, .025, 'CopperGold', axis='Y')
        model.rod('GoggleStrap', (-.25, -.2, .17), (.25, -.2, .17), .022, 'PlumRed')
    elif hat_id == 'hat_crown':
        model.cylinder('CrownBand', (0, 0, .14), .28, .2, 'CopperGold', vertices=10)
        for i in range(5):
            a = i * 2 * math.pi / 5
            model.cone('CrownPoint' + str(i), (.23 * math.cos(a), .23 * math.sin(a), .31), .072, .018, .3, 'CopperGold', vertices=4)
            model.sphere('Jewel' + str(i), (.25 * math.cos(a), .25 * math.sin(a), .2), (.055, .055, .065), 'PlumRed' if i % 2 else 'Mint')
    elif hat_id == 'hat_lucky':
        model.cylinder('TallHat', (0, .025, .25), .24, .39, 'PlumRed', vertices=12)
        model.cylinder('HatBand', (0, .025, .13), .25, .095, 'CopperGold', vertices=12)
        model.rod('WindingStem', (0, .23, .3), (0, .39, .3), .025, 'CopperGold')
        for x in (-.075, .075): model.torus('WindingLoop' + str(x), (x, .39, .3), .066, .02, 'Mint', axis='Y')
        star(model, 'LuckyBadge', (0, -.226, .26), .095, 'CreamYellow')
    return model


EMOTE_IDS = ['emote_wave', 'emote_clap', 'emote_shrug', 'emote_dance', 'emote_salute', 'emote_bow', 'emote_crown', 'emote_fireworks']


def face(emote_id):
    model = Model('Face_' + emote_id, 'Face', emote_id)
    model.mesh('FoldedExpression', [(-.29, -.27, -.24), (.29, -.27, -.24), (.29, -.27, .24), (-.29, -.27, .24), (0, -.315, 0)], [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)], 'PaperLight', bevel=0)
    for x in (-.12, .12):
        if emote_id in ('emote_clap', 'emote_bow', 'emote_salute'):
            model.rod('HappyEye' + str(x), (x - .046, -.326, .045), (x, -.326, .07), .011, 'FaceInk')
            model.rod('HappyEyeOther' + str(x), (x, -.326, .07), (x + .046, -.326, .045), .011, 'FaceInk')
        elif emote_id in ('emote_crown', 'emote_fireworks'): star(model, 'StarEye' + str(x), (x, -.326, .06), .062, 'CopperGold')
        else: model.sphere('Eye' + str(x), (x, -.326, .055), (.035, .012, .051), 'FaceInk')
    if emote_id == 'emote_shrug':
        model.box('ShrugMouth', (0, -.332, -.12), (.14, .023, .02), 'PlumRed', .008, rotation=(0, .15, 0))
        for x in (-.12, .12): model.box('UnevenBrow' + str(x), (x, -.332, .15 if x < 0 else .12), (.12, .022, .02), 'FaceInk', .008)
    elif emote_id == 'emote_fireworks': model.sphere('SurpriseMouth', (0, -.332, -.12), (.044, .015, .06), 'PlumRed')
    else:
        for i in range(5):
            x1, x2 = -.085 + i * .034, -.085 + (i + 1) * .034
            z1, z2 = -.09 - .04 * math.sin(i * math.pi / 5), -.09 - .04 * math.sin((i + 1) * math.pi / 5)
            model.rod('Smile' + str(i), (x1, -.332, z1), (x2, -.332, z2), .012, 'PlumRed')
    if emote_id == 'emote_dance':
        for x in (-.21, .21): model.sphere('Blush' + str(x), (x, -.315, -.04), (.042, .012, .026), 'PlumRed')
    if emote_id == 'emote_clap':
        for x in (-.21, .21): star(model, 'CheekSpark' + str(x), (x, -.323, -.04), .035, 'CopperGold')
    if emote_id == 'emote_salute':
        model.box('BrowBadge', (0, -.324, .17), (.07, .022, .045), 'Mint', .012)
    if emote_id == 'emote_bow':
        model.box('CalmBrow', (0, -.325, .14), (.19, .022, .013), 'FaceInk', .005)
    return model


def mission(kind):
    model = Model('Mission_' + kind, 'Mission', kind)
    if kind == 'chip_rain':
        model.cylinder('CollectibleCoin', (0, 0, .25), .24, .075, 'CopperGold', axis='Y')
        model.torus('MintedEdge', (0, -.051, .25), .195, .018, 'CreamYellow', axis='Y')
        star(model, 'CollectStar', (0, -.067, .25), .13, 'PlumRed')
    elif kind == 'gold_delivery':
        model.box('CargoBox', (0, 0, .28), (.72, .56, .56), 'CreamYellow', .055)
        for x in (-.24, .24): model.box('CargoStrap' + str(x), (x, 0, .285), (.065, .585, .58), 'CopperGold', .012)
        for x in (-.4, .4): model.torus('CargoHandle' + str(x), (x, 0, .34), .1, .025, 'InkBlue', axis='X')
        star(model, 'CargoSeal', (0, -.296, .32), .13, 'PlumRed')
    elif kind == 'mascot_chase':
        model.cylinder('MascotFoot', (0, 0, .07), .34, .14, 'CopperGold', vertices=8)
        model.cone('OrigamiBody', (0, 0, .39), .26, .09, .56, 'PlumRed', vertices=6)
        model.box('MascotMask', (0, -.015, .83), (.46, .21, .4), 'CreamYellow', .025)
        for x in (-.1, .1): model.sphere('MascotEye' + str(x), (x, -.135, .85), (.033, .015, .045), 'FaceInk')
        for x in (-.23, .23): model.rod('RaisedWing' + str(x), (x, 0, .52), (x * 1.55, 0, .7), .045, 'Mint')
        model.torus('WindupHalo', (0, .14, .86), .18, .025, 'CopperGold', axis='Y')
    elif kind == 'power_relay':
        model.cylinder('RelayFoot', (0, 0, .08), .31, .16, 'InkBlue', vertices=8)
        model.box('RelayDock', (0, 0, .36), (.4, .36, .56), 'CopperGold', .05)
        for z in (.22, .38, .54): model.box('ChargeBar' + str(z), (0, -.2, z), (.29, .05, .068), 'Mint', .014)
        for x in (-.11, .11): model.cylinder('EnergyContact' + str(x), (x, 0, .69), .045, .15, 'CreamYellow')
    elif kind == 'sync_buttons':
        model.cylinder('ButtonBase', (0, 0, .12), .33, .24, 'InkBlue', vertices=12)
        button = model.pivot('Button', (0, 0, .27))
        model.cylinder('SyncButton', (0, 0, .3), .23, .14, 'PlumRed', button)
        model.torus('ButtonGuard', (0, 0, .25), .29, .033, 'CopperGold')
        model.box('SignalMast', (0, .25, .56), (.07, .08, .77), 'CopperGold', .02)
        model.sphere('SignalLamp', (0, .25, .96), (.1, .1, .1), 'Mint')
    elif kind == 'power_repair':
        model.box('RepairHousing', (0, 0, .41), (.63, .42, .8), 'InkBlue', .055)
        panel = model.pivot('Door', (.29, -.23, .4))
        model.box('RepairPanel', (0, -.245, .42), (.53, .055, .66), 'Mint', .025, panel)
        for i in range(3):
            model.cylinder('Fuse' + str(i), (-.16 + .16 * i, -.295, .44), .048, .24, 'CreamYellow')
            model.sphere('Warning' + str(i), (-.16 + .16 * i, -.299, .67), (.026, .012, .026), 'PlumRed')
        model.rod('Cable', (-.3, 0, .18), (-.38, -.15, .025), .025, 'CopperGold')
    return model


def region(kind):
    model = Model('Region_' + kind, 'Region', kind)
    if kind == 'lobby':
        for x in (-1.31, 1.31):
            model.box('DoorPost' + str(x), (x, 0, 1.52), (.22, .32, 3.04), 'InkBlue', .055)
            model.box('GoldPost' + str(x), (x, -.18, 1.52), (.06, .045, 2.87), 'CopperGold', .015)
        model.box('WelcomeMarquee', (0, 0, 3.1), (2.96, .41, .53), 'PlumRed', .09)
        for i in range(9): model.sphere('MarqueeLamp' + str(i), (-1.13 + i * .28, -.232, 3.09), (.075, .038, .075), 'CreamYellow')
        model.cylinder('WelcomeCoin', (0, 0, 3.66), .32, .16, 'CopperGold', axis='Y')
        star(model, 'ClubLogo', (0, -.1, 3.67), .23, 'Mint')
    elif kind == 'arcade':
        for x in (-1.15, 1.15):
            model.box('LanternPole' + str(x), (x, .1, 1.6), (.12, .17, 3.2), 'InkBlue', .035)
            model.rod('LanternArm' + str(x), (x, .1, 3.06), (x * .68, .1, 3.32), .045, 'CopperGold')
            model.sphere('PaperLantern' + str(x), (x * .68, .1, 2.88), (.28, .25, .35), 'Mint' if x < 0 else 'PlumRed')
            model.cylinder('LanternCollar' + str(x), (x * .68, .1, 3.21), .12, .08, 'CopperGold')
            model.rod('LanternTassel' + str(x), (x * .68, .1, 2.53), (x * .68, .1, 2.31), .025, 'CopperGold')
        model.box('NightMarketSign', (0, .14, 1.15), (1.07, .23, 1.31), 'InkBlue', .07)
        gear(model, 'NeonFlower', (0, -.017, 1.27), .37, 'Mint')
        star(model, 'FlowerCore', (0, -.09, 1.27), .17, 'PlumRed')
    elif kind == 'backroom':
        model.box('MachineryFoot', (0, 0, .14), (2.4, 1.03, .28), 'InkBlue', .06)
        model.cylinder('Boiler', (.66, .15, .92), .4, 1.63, 'CopperGold', vertices=16)
        for z in (.34, 1.46): model.torus('BoilerRing' + str(z), (.66, .15, z), .42, .045, 'InkBlue')
        model.rod('SteamStack', (.66, .15, 1.64), (.66, .15, 3.05), .12, 'InkBlue')
        model.cone('StackLip', (.66, .15, 3.16), .13, .24, .25, 'CopperGold')
        wheel = model.pivot('Gear', (-.45, 0, 1.57)); gear(model, 'GreatCog', (-.45, -.05, 1.57), .72, 'CopperGold', wheel)
        model.cylinder('PressureGauge', (.66, -.28, 1.0), .2, .06, 'CreamYellow', axis='Y')
        model.rod('GaugeNeedle', (.66, -.321, 1.0), (.78, -.321, 1.08), .013, 'PlumRed')
    elif kind == 'penthouse':
        model.box('DecoPedestal', (0, 0, .14), (2.6, .85, .28), 'InkBlue', .055)
        for i in range(7):
            x = (i - 3) * .29; height = 2.91 - abs(i - 3) * .35
            model.box('ArtDecoFin' + str(i), (x, .05, height / 2 + .25), (.17, .21, height), 'CopperGold' if i % 2 == 0 else 'PlumRed', .035)
        for x in (-1.04, 1.04): model.rod('HangingChain' + str(x), (x, .06, .3), (x, .06, 2.38), .018, 'CopperGold')
        model.cylinder('VaultHalo', (0, -.09, 1.62), .68, .08, 'InkBlue', axis='Y')
        model.torus('DecoHalo', (0, -.151, 1.62), .61, .044, 'Mint', axis='Y')
        star(model, 'PenthouseStar', (0, -.214, 1.62), .37, 'CopperGold')
    return model


GAME_IDS = ['Slots', 'Roulette', 'CoinFlip', 'Blackjack', 'SicBo', 'DragonTiger', 'HighLow', 'LuckyDraw', 'Bingo',
    'Plinko', 'CooperativeLevers', 'PushYourLuckDice', 'PassingBag', 'BlindAuction', 'MechanicalRace', 'CooperativeVault', 'ChickenElevator']
HAT_IDS = ['hat_party', 'hat_banana', 'hat_mechanic', 'hat_crown', 'hat_lucky']
MISSION_IDS = ['chip_rain', 'mascot_chase', 'gold_delivery', 'power_relay', 'sync_buttons', 'power_repair']
REGION_IDS = ['lobby', 'arcade', 'backroom', 'penthouse']


def normalize_foot(model):
    if model.category in ('Face', 'Avatar') or model.name in ('Slots', 'CooperativeVault'): return
    bpy.context.view_layer.update()
    vertices = [obj.matrix_world @ vertex.co for obj in model.collection.objects if obj.type == 'MESH' for vertex in obj.data.vertices]
    if not vertices: return
    minimum = min(vertex.z for vertex in vertices)
    for obj in model.root.children: obj.location.z -= minimum
    bpy.context.view_layer.update()


def hierarchy_baked_matrix(wrapper, scene_data, rest=False, local_space=False, global_space=False):
    """Fix Blender 5.2's repeated inverse bake in nested Mesh/Empty export, only for this source.

    All selected source nodes are static Mesh/Empty with applied rotation/positive scale.
    Geometry is baked by Blender's exporter using G. Each FBX local basis must therefore
    be G * sourceLocal * inverse(G), not recursively apply inverse(G) again per depth.
    Source -Y/Z, exporter -Z/Y, original semantic hierarchy and pivots remain unchanged.
    This hook exists only in our factory-startup exporter process, never an installed addon.
    """
    if wrapper._tag == 'OB' and wrapper.bdata.type in {'MESH', 'EMPTY'} and scene_data.settings.bake_space_transform:
        parent_valid = wrapper.has_valid_parent(scene_data.objects)
        world = not local_space and (global_space or not parent_valid)
        source = (wrapper.matrix_rest_global if rest else wrapper.matrix_global) if world else (wrapper.matrix_rest_local if rest else wrapper.matrix_local)
        return scene_data.settings.global_matrix @ source @ scene_data.settings.global_matrix_inv
    return ORIGINAL_FBX_MATRIX(wrapper, scene_data, rest=rest, local_space=local_space, global_space=global_space)


ORIGINAL_FBX_MATRIX = fbx_utils.ObjectWrapper.fbx_object_matrix
fbx_utils.ObjectWrapper.fbx_object_matrix = hierarchy_baked_matrix


def triangulate_export_meshes(model):
    # Explicit triangles remove exporter/importer n-gon ambiguity; only mathematically
    # zero-area triangles are discarded, not valid tiny paper/eye geometry.
    removed = 0
    for obj in model.collection.objects:
        if obj.type != 'MESH': continue
        mesh = bmesh.new(); mesh.from_mesh(obj.data)
        if model.name == 'Item_redraw_card':
            # This fan's bevel clamp produces coincident seams separated by ~1.86e-9m.
            # Weld only sub-micrometre duplicate positions in the DCC source, before
            # explicit triangles, so float32 FBX import cannot delete extra slivers.
            bmesh.ops.remove_doubles(mesh, verts=list(mesh.verts), dist=1e-7)
        bmesh.ops.triangulate(mesh, faces=list(mesh.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
        zero = [face for face in mesh.faces if face.calc_area() < 1e-12]
        removed += len(zero)
        if zero: bmesh.ops.delete(mesh, geom=zero, context='FACES_ONLY')
        orphan = [vertex for vertex in mesh.verts if not vertex.link_faces]
        if orphan: bmesh.ops.delete(mesh, geom=orphan, context='VERTS')
        mesh.to_mesh(obj.data); mesh.free(); obj.data.update()
    print('JINX_ZERO_AREA_TRIANGLES_REMOVED ' + model.name + ' count=' + str(removed), flush=True)


def export(model):
    normalize_foot(model)
    model.combine()
    objects = list(model.collection.objects)
    for obj in objects:
        assert min(obj.scale) > 0, obj.name
        assert all(abs(value) < 1e-5 for value in obj.rotation_euler), (obj.name, tuple(obj.rotation_euler))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = model.root
    path = MODELS / (model.name + '.fbx')
    preserve = OPTIONS.phase == 'all' and model.name in ('Slots', 'CooperativeVault') and model.name not in OPTIONS.refresh_model
    preserve |= OPTIONS.preserve_existing and not (model.name in OPTIONS.refresh_model or model.name == 'Avatar' and OPTIONS.refresh_avatar)
    if preserve and path.exists():
        print('JINX_LOCKED_MODEL_PRESERVED ' + model.name, flush=True)
        if model.name in old_entries: return dict(old_entries[model.name])
    else:
        triangulate_export_meshes(model)
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH', 'EMPTY'},
            axis_forward='-Z', axis_up='Y', global_scale=1, apply_unit_scale=True,
            bake_space_transform=True, bake_anim=False, add_leaf_bones=False, path_mode='AUTO', use_triangles=True)
    points, triangles, materials = [], 0, set()
    for obj in objects:
        if obj.type != 'MESH': continue
        obj.data.calc_loop_triangles(); triangles += len(obj.data.loop_triangles)
        points.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
        # Unity discards unused slots; permanent costume palette is independent of the
        # materials actually used by model faces and is provided by the scene builder.
        materials.update(obj.data.materials[face.material_index].name for face in obj.data.polygons)
    minimum = [min(point[i] for point in points) for i in range(3)] if points else [0, 0, 0]
    maximum = [max(point[i] for point in points) for i in range(3)] if points else [0, 0, 0]
    if model.category == 'Station':
        assert maximum[0] - minimum[0] <= 3.001 and maximum[1] - minimum[1] <= 3.001, model.name
    entry = dict(name=model.name, category=model.category, ruleId=model.rule_id, file=path.name,
        triangles=triangles, materialNames=sorted(materials), meshCount=sum(obj.type == 'MESH' for obj in objects),
        boundsSource=dict(min=minimum, max=maximum),
        boundsUnity=dict(min=[-maximum[0], minimum[2], -maximum[1]], max=[-minimum[0], maximum[2], -minimum[1]]),
        pivots=[dict(name=obj.name, sourcePosition=list(obj.matrix_world.translation), localPosition=list(obj.location)) for obj in model.pivots],
        nodeNames=[obj.name for obj in objects], sha256=hashlib.sha256(path.read_bytes()).hexdigest(), hierarchyBake='G*sourceLocal*inverse(G)', zeroAreaThreshold=1e-12)
    if model.name == 'Plinko':
        entry['slots'] = dict(count=15, dividers=16, pitchMetres=.114, indexDirectionSource='-X to +X', indexDirectionUnity='+X to -X',
            unityBallInitial=[0,2.91,-.13], unityBallOffsetX='(3+Level*0.5-Cursor)*0.114', unityBallOffsetY='-Level*0.19875', unityBallOffsetZ='Level/8*0.25')
    print('JINX_MODEL_READY ' + model.name + ' triangles=' + str(triangles), flush=True)
    return entry


def studio():
    collection = bpy.data.collections.new('PreviewStudio_NotExported'); SCENE.collection.children.link(collection)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.018))
    floor = bpy.context.object
    for old in list(floor.users_collection): old.objects.unlink(floor)
    collection.objects.link(floor)
    mat = bpy.data.materials.new('Studio_Backdrop'); mat.diffuse_color = (.025, .034, .055, 1); mat.use_nodes = True
    mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (.025, .034, .055, 1)
    mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value = .78
    floor.data.materials.append(mat)
    for name, location, power, size in [('Key', (-3, -4, 6), 700, 5), ('Fill', (4, -2, 4), 500, 4), ('Rim', (1, 4, 5), 900, 3)]:
        data = bpy.data.lights.new('Studio_' + name, 'AREA'); data.energy = power; data.shape = 'DISK'; data.size = size
        obj = bpy.data.objects.new('Studio_' + name, data); collection.objects.link(obj); obj.location = location
        obj.rotation_euler = (Vector((0, 0, 1)) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    data = bpy.data.cameras.new('StudioCamera'); camera = bpy.data.objects.new('StudioCamera', data); collection.objects.link(camera)
    SCENE.camera = camera; data.type = 'ORTHO'; data.ortho_scale = 3.5
    camera.location = (-3.6, -6, 3.4); camera.rotation_euler = (Vector((0, 0, 1.1)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    SCENE.render.engine = 'CYCLES'; SCENE.cycles.device = 'CPU'; SCENE.cycles.samples = 24
    SCENE.cycles.use_denoising = True
    SCENE.render.resolution_x = 1100; SCENE.render.resolution_y = 1100; SCENE.render.resolution_percentage = 100
    SCENE.render.image_settings.file_format = 'PNG'; SCENE.render.film_transparent = False
    if SCENE.world is None: SCENE.world = bpy.data.worlds.new('PreviewWorld')
    SCENE.world.color = (.12, .14, .19)
    SCENE.view_settings.view_transform = 'AgX'
    return camera


models = [avatar(), slots(), vault()]
if OPTIONS.phase == 'all':
    models += [game_station(kind) for kind in GAME_IDS if kind not in ('Slots', 'CooperativeVault')]
    models += [prop(item) for item in ITEM_IDS]
    models += [hat(item) for item in HAT_IDS]
    models += [face(item) for item in EMOTE_IDS]
    models += [mission(item) for item in MISSION_IDS]
    models += [region(item) for item in REGION_IDS]
old_manifest_path = MODELS / 'manifest.json'
old_entries = {value['name']: value for value in json.loads(old_manifest_path.read_text(encoding='utf-8'))['models']} if old_manifest_path.exists() else {}
entries = [export(model) for model in models]
for entry in entries:
    previous = old_entries.get(entry['name'], {})
    for key in ('icon', 'iconSha256', 'iconSize', 'iconTransparent'):
        if key in previous: entry[key] = previous[key]
manifest = dict(blender=bpy.app.version_string, units='metres', sourceFront='-Y', sourceUp='+Z', sourceRight='-X',
    unityFront='+Z', unityUp='+Y', unityRight='+X', axisGate='526c02d9',
    export=dict(axisForward='-Z', axisUp='Y', bakeSpaceTransform=True, applyUnitScale=True, cameras=False, lights=False, animations=False),
    palette={name: values[0] for name, values in PALETTE.items()}, cosmeticAbsence=[dict(id='hat_none', model=None)], models=entries)
(MODELS / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
camera = studio()
for model in models:
    for obj in model.collection.objects:
        obj.hide_render = model != models[0]
        obj.hide_set(model != models[0])
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / ('jinx_first_models.blend' if OPTIONS.phase == 'first' else 'jinx_casino_models.blend')))
if OPTIONS.render:
    for model in models[:3]:
        for other in models:
            for obj in other.collection.objects:
                obj.hide_render = other != model
                obj.hide_set(other != model)
        camera.data.ortho_scale = 2.65 if model.category == 'Avatar' else 3.55
        SCENE.render.filepath = str(PREVIEWS / (model.name + '.png'))
        bpy.ops.render.render(write_still=True)
        print('JINX_PREVIEW_READY ' + SCENE.render.filepath, flush=True)
if OPTIONS.icons:
    icons = STAGING / 'Icons'; icons.mkdir(parents=True, exist_ok=True)
    SCENE.render.resolution_x = SCENE.render.resolution_y = 512
    SCENE.render.film_transparent = True
    SCENE.render.image_settings.color_mode = 'RGBA'
    SCENE.cycles.samples = 24
    bpy.data.objects.get('Plane').hide_render = True
    for model, entry in zip(models, entries):
        if model.category not in ('Item', 'Station'): continue
        if OPTIONS.icon_model and model.name not in OPTIONS.icon_model: continue
        for other in models:
            for obj in other.collection.objects:
                obj.hide_render = other != model
                obj.hide_set(other != model)
        minimum, maximum = Vector(entry['boundsSource']['min']), Vector(entry['boundsSource']['max'])
        centre = (minimum + maximum) / 2; extent = max(maximum - minimum)
        camera.location = centre + Vector((-1.4, -2.5, 1.45)) * extent
        camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
        camera.data.ortho_scale = extent * 1.42
        SCENE.render.filepath = str(icons / (model.name + '.png'))
        bpy.ops.render.render(write_still=True)
        entry['icon'] = model.name + '.png'
        entry['iconSha256'] = hashlib.sha256(Path(SCENE.render.filepath).read_bytes()).hexdigest()
        entry['iconSize'] = [512, 512]; entry['iconTransparent'] = True
        print('JINX_ICON_READY ' + model.name, flush=True)
    (MODELS / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print('JINX_MODEL_BATCH_COMPLETE phase=' + OPTIONS.phase, flush=True)
