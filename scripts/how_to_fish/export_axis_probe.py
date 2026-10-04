"""Blender-only FBX axis probe. Run in a separate --background --factory-startup process."""
import bpy
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from fbx_export import save_and_export


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(arguments) != 1:
        raise SystemExit("Expected the SleepyDemos checkout directory after --")
    project = Path(arguments[0]).resolve()
    if not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        raise SystemExit("Target is not a Unity project")
    # Isolated factory scene only; this script must never run in the user's live Blender document.
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    collection = bpy.context.scene.collection
    root = bpy.data.objects.new("AxisProbe", None)
    collection.objects.link(root)
    for name, point, color in [
        ("Forward", (0, -1, 0), (0.08, 0.35, 0.9, 1)),
        ("Right", (-1, 0, 0), (0.9, 0.1, 0.08, 1)),
        ("Up", (0, 0, 1), (0.1, 0.85, 0.2, 1)),
    ]:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.12, location=point)
        obj = bpy.context.object
        obj.name = name
        obj.parent = root
        material = bpy.data.materials.new(name)
        material.diffuse_color = color
        material.use_nodes = True
        node = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
        node.inputs["Base Color"].default_value = color
        obj.data.materials.append(material)
    hinge = bpy.data.objects.new("Hinge", None)
    collection.objects.link(hinge)
    hinge.parent = root
    hinge.location = (0, -0.5, 0.5)
    tip = bpy.data.objects.new("HingeForward", None)
    collection.objects.link(tip)
    tip.parent = hinge
    tip.location = (0, -0.4, 0)
    save_and_export(project, "AxisProbe")


if __name__ == "__main__":
    main()
