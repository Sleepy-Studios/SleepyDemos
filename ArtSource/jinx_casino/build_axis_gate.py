import bpy, json, os
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
root = bpy.data.objects.new('AxisGate', None)
scene.collection.objects.link(root)
for name, location in [('Front', (0,-1,0)), ('Right',(-1,0,0)), ('Up',(0,0,1))]:
    bpy.ops.mesh.primitive_cube_add(size=.1, location=location)
    obj=bpy.context.object; obj.name=name; obj.parent=root
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
props=bpy.ops.export_scene.fbx.get_rna_type().properties
for key,value in [('axis_forward','-Z'),('axis_up','Y')]:
    assert value in [i.identifier for i in props[key].enum_items], key
path=os.path.abspath('Library/JinxCasino/ArtGate/AxisGate.fbx')
bpy.ops.export_scene.fbx(filepath=path, use_selection=False, object_types={'MESH','EMPTY'}, axis_forward='-Z', axis_up='Y', global_scale=1.0, apply_unit_scale=True, bake_space_transform=True, bake_anim=False, add_leaf_bones=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath('Library/JinxCasino/ArtGate/AxisGate.blend'))
print('JINX_AXIS_GATE_READY '+path)
