"""Shared, Unity-verified Blender 5.2 static hierarchy export for HowToFish."""
import hashlib
import json
import math
from pathlib import Path
import bpy
from mathutils import Matrix


def save_and_export(project, name):
    project = Path(project).resolve()
    source = project / "ArtSource~/how_to_fish" / (name + ".blend")
    target = project / "Assets/LoadResources/Demos/how_to_fish/Art/Models" / (name + ".fbx")
    source.parent.mkdir(parents=True, exist_ok=True)
    target.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    # Keep the saved source native. Transform this process's unsaved export state
    # explicitly to avoid Blender 5.2's nested Apply Transform double conversion.
    basis = Matrix.Rotation(-math.pi / 2, 4, "X")
    local_matrices = {obj: obj.matrix_local.copy() for obj in bpy.context.scene.objects if obj.type in {"MESH", "EMPTY"}}
    converted_meshes = set()
    for obj, local in local_matrices.items():
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_basis = basis @ local @ basis.inverted()
        if obj.type == "MESH" and obj.data not in converted_meshes:
            obj.data.transform(basis)
            converted_meshes.add(obj.data)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    for obj in local_matrices:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=str(target), use_selection=True, object_types={"MESH", "EMPTY"},
        axis_forward="Z", axis_up="Y", global_scale=1,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True, use_space_transform=False,
        add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=True, path_mode="AUTO",
    )
    report = {
        "blender": bpy.app.version_string,
        "source_forward": "-Y", "source_up": "+Z", "source_right": "-X",
        "target_forward": "+Z", "target_up": "+Y", "target_right": "+X",
        "export_axis_forward": "Z", "export_axis_up": "Y",
        "bake_space_transform": True, "use_space_transform": False,
        "unsaved_export_basis": "RotationX(-90 degrees)",
        "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
        "unity_import_verified": False,
        "objects": sorted(obj.name for obj in local_matrices),
    }
    (source.parent / (name + ".json")).write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report))
    return report
