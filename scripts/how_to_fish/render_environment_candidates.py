"""Render candidate model silhouettes from saved Blender sources without changing them."""
import bpy
import sys
import json
from pathlib import Path
from mathutils import Vector

project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
label_material = bpy.data.materials.new("PreviewLabel")
label_material.diffuse_color = (0.03, 0.045, 0.06, 1)
label_material.use_nodes = True
next(node for node in label_material.node_tree.nodes if node.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.03, 0.045, 0.06, 1)
requested = sys.argv[sys.argv.index("--") + 2:]
front_view = "--front" in requested
requested = [name for name in requested if name != "--front"]
names = requested or ["BrownCrab", "RockCrab", "Shrimp", "Lobster", "Clam", "SpiderCrab", "CrabRod", "Knife"]
for index, name in enumerate(names):
    with bpy.data.libraries.load(str(project / "ArtSource~/how_to_fish" / (name + ".blend"))) as (available, loaded):
        loaded.objects = available.objects
    for obj in loaded.objects:
        if obj is not None:
            bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.update()
    root = next(obj for obj in loaded.objects if obj is not None and obj.parent is None and obj.type == "EMPTY")
    corners = [obj.matrix_world @ Vector(corner) for obj in loaded.objects if obj is not None and obj.type == "MESH" for corner in obj.bound_box]
    low = Vector(tuple(min(v[axis] for v in corners) for axis in range(3)))
    high = Vector(tuple(max(v[axis] for v in corners) for axis in range(3)))
    scale = 1.65 / max(high - low)
    tile = Vector(((index % 4 - 1.5) * 2.4, (index // 4 - 0.5) * 2.8, 0))
    root.scale = (scale,) * 3
    root.location = tile - Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z)) * scale
    bpy.ops.object.text_add(location=tile + Vector((-0.8, -1.2, 0.012)))
    label = bpy.context.object
    report = json.loads((project / "ArtSource~/how_to_fish" / (name + ".json")).read_text())
    dims = report.get("size_blender_m", [0, 0, 0])
    label.data.body = name + "\n" + " x ".join(f"{v:.2f}" for v in dims) + " m"
    label.data.size = 0.15
    label.data.extrude = 0.001
    label.data.materials.append(label_material)
    label.name = "Label" + name

bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.01))
floor = bpy.context.object
mat = bpy.data.materials.new("PreviewPaper")
mat.diffuse_color = (0.64, 0.69, 0.72, 1)
mat.use_nodes = True
next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.64, 0.69, 0.72, 1)
floor.data.materials.append(mat)
bpy.ops.object.light_add(type="AREA", location=(-3, -4, 9))
bpy.context.object.data.energy = 1500
bpy.context.object.data.shape = "DISK"
bpy.context.object.data.size = 7
bpy.ops.object.camera_add(location=(0, -14, 5) if front_view else (0, -7, 11))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 0, 0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 10.3
scene = bpy.context.scene
scene.camera = camera
try:
    scene.render.engine = "BLENDER_EEVEE"
except TypeError as error:
    raise RuntimeError("This preview requires the installed Blender Eevee engine") from error
scene.render.resolution_x = 1600
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
assert "PNG" in [item.identifier for item in scene.render.image_settings.bl_rna.properties["file_format"].enum_items]
scene.render.image_settings.file_format = "PNG"
output = project / "Library/HowToFish/Staging/EnvironmentModels/Previews" / (("Front-" if front_view else "Review-") + "-".join(names) + ".png")
output.parent.mkdir(parents=True, exist_ok=True)
scene.render.filepath = str(output)
bpy.ops.render.render(write_still=True)
print(output)
