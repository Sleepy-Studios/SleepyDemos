"""Render three original endings from this demo's own DCC models.

blender --background --factory-startup --python ArtSource/jinx_casino/build_endings.py
Only Library staging PNGs and a separate owned ending .blend are written.
The authoritative model blend, FBXs, Unity assets and user's interactive Blender are untouched.
"""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

WORKSPACE = Path(__file__).resolve().parents[2]
SOURCE = WORKSPACE / 'ArtSource/jinx_casino'
OUTPUT = WORKSPACE / 'Library/JinxCasino/Staging/Endings'
OUTPUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE / 'jinx_casino_models.blend'))
scene = bpy.context.scene
for obj in bpy.data.objects:
    if obj.type not in {'LIGHT', 'CAMERA'} and obj.name != 'Plane': obj.hide_render = True
camera = scene.camera
camera.location = (-5.2, -10.4, 4.8)
camera.rotation_euler = (Vector((0, 0, 1.15)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.ortho_scale = 5.65
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.film_transparent = False
scene.render.image_settings.color_mode = 'RGB'
scene.cycles.samples = 16
scene.cycles.device = 'CPU'
scene.cycles.use_denoising = True
created = []
palette = json.loads((WORKSPACE / 'Library/JinxCasino/Staging/Models/manifest.json').read_text(encoding='utf-8'))['palette']
for color_id in ('color_gold','color_violet'):
    if bpy.data.materials.get(color_id) is not None: continue
    color = tuple(int(palette[color_id][offset:offset+2],16)/255 for offset in (0,2,4))
    linear = tuple(value/12.92 if value <= .04045 else ((value+.055)/1.055)**2.4 for value in color)
    material = bpy.data.materials.new(color_id); material.use_nodes = True
    shader = material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*linear,1)
    shader.inputs['Roughness'].default_value = .42
    shader.inputs['Metallic'].default_value = .12


def clone_model(name, position=(0,0,0), scale=1, parent=None):
    source = bpy.data.collections[name]
    copies = {}
    for original in source.objects:
        obj = original.copy()
        if original.type == 'MESH': obj.data = original.data.copy()
        scene.collection.objects.link(obj)
        obj.name = 'Ending_' + original.name
        obj.hide_render = False; obj.hide_set(False)
        copies[original] = obj; created.append(obj)
    for original, obj in copies.items():
        obj.parent = copies.get(original.parent)
        obj.matrix_parent_inverse = original.matrix_parent_inverse.copy()
        obj.matrix_basis = original.matrix_basis.copy()
    root = copies[bpy.data.objects[name]]
    root.parent = parent; root.location = position; root.scale = (scale,scale,scale)
    return root, {original.name: copy for original, copy in copies.items()}


def hero(color, face):
    root, bones = clone_model('Avatar', (-1.1,0,0), 1.18)
    for obj in bones.values():
        if obj.type == 'MESH':
            for index, mat in enumerate(obj.data.materials):
                if mat.name.startswith('color_'): obj.data.materials[index] = bpy.data.materials[color]
    clone_model('Face_' + face, parent=bones['Avatar.Head'])
    return root, bones


def paper(name, position, size, rotation, material):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object; obj.name = 'Ending_' + name
    obj.scale = size; obj.rotation_euler = rotation
    obj.data.materials.append(bpy.data.materials[material]); created.append(obj)
    bevel = obj.modifiers.new('PaperEdges', 'BEVEL'); bevel.width = .025; bevel.segments = 2
    return obj


for ending in ('EndingDignity', 'EndingTakeover', 'EndingWithdraw'):
    for obj in created: bpy.data.objects.remove(obj, do_unlink=True)
    created.clear()
    if ending == 'EndingDignity':
        root, bones = hero('color_gold', 'emote_wave')
        bones['Avatar.ArmRightPivot'].rotation_euler.y = math.radians(115)
        bones['Avatar.Head'].rotation_euler.y = math.radians(8)
        ticket, _ = clone_model('Item_jackpot_coupon', (1.05,-.4,.6), 2.55)
        ticket.rotation_euler = (math.radians(-8),math.radians(-12),math.radians(-13))
        clone_model('Region_lobby', (1.0,1.7,0), .68)
        for index in range(5):
            chip, _ = clone_model('Mission_chip_rain', (-.25+index*.24,-.7,.08), .34)
            chip.rotation_euler.x = math.radians(90)
    elif ending == 'EndingTakeover':
        root, bones = hero('color_violet', 'emote_crown')
        bones['Avatar.ArmLeftPivot'].rotation_euler.y = math.radians(-145)
        bones['Avatar.ArmRightPivot'].rotation_euler.y = math.radians(145)
        clone_model('hat_crown', parent=bones['Avatar.HatMount'])
        vault, _ = clone_model('CooperativeVault', (1.0,.9,0), .68)
        vault.rotation_euler.z = math.radians(-8)
        clone_model('Region_penthouse', (1.8,2.4,0), .65)
        for index in range(28):
            angle = index * 2.399963
            paper('Confetti' + str(index), (math.sin(angle)*2.5,-.8+(index%4)*.16,.65+(index%8)*.32),
                (.055,.008,.13), (index*.2,index*.17,index*.11), ('Mint','PlumRed','CreamYellow','CopperGold')[index%4])
    else:
        root, bones = hero('color_blue', 'emote_shrug')
        root.rotation_euler.y = math.radians(-12)
        bones['Avatar.ArmLeftPivot'].rotation_euler.y = math.radians(-72)
        bones['Avatar.ArmRightPivot'].rotation_euler.y = math.radians(72)
        bones['Avatar.Head'].rotation_euler.y = math.radians(-17)
        banana, _ = clone_model('Item_banana_peel', (.3,-.8,.01), 1.75)
        banana.rotation_euler.z = math.radians(-18)
        for index in range(13):
            angle = index * 2.399963; radius = .32+(index%4)*.19
            chip, _ = clone_model('Mission_chip_rain', (.6+math.cos(angle)*radius,-.5+math.sin(angle)*radius,.08+(index%3)*.03), .34)
            chip.rotation_euler = (math.radians(90),0,angle)
        clone_model('Region_backroom', (1.4,1.3,0), .68)
    bpy.context.view_layer.update()
    scene.render.filepath = str(OUTPUT / (ending + '.png'))
    bpy.ops.render.render(write_still=True)
    print('JINX_ENDING_READY ' + scene.render.filepath, flush=True)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'jinx_casino_endings.blend'))
print('JINX_ENDINGS_COMPLETE 3', flush=True)
