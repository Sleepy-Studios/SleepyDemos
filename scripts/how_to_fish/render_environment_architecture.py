"""Candidate-only architectural views at metre scale with an unexported 1.8m marker."""
import bpy,sys,math
from pathlib import Path
from mathutils import Vector
project=Path(sys.argv[sys.argv.index('--')+1]).resolve()
out=project/'Library/HowToFish/Staging/EnvironmentModels/Previews';out.mkdir(parents=True,exist_ok=True)
def mat(name,color):
    result=bpy.data.materials.new(name);result.diffuse_color=(*color,1);result.use_nodes=True
    node=next(n for n in result.node_tree.nodes if n.type=='BSDF_PRINCIPLED');node.inputs['Base Color'].default_value=(*color,1);node.inputs['Roughness'].default_value=.8
    return result
for name in ('RocksPierModule','LuckyBaitCasinoShell'):
    for view in ('Front','ThreeQuarter','Interior') if name=='LuckyBaitCasinoShell' else ('Front','ThreeQuarter'):
        bpy.ops.wm.open_mainfile(filepath=str(project/'ArtSource~/how_to_fish'/f'{name}.blend'))
        if view=='Interior':
            for obj in bpy.context.scene.objects:
                if obj.name in {'RoofLeft','RoofRight','GableWalls','Marquee'}:obj.hide_render=True
        floorz=-2.25 if name=='RocksPierModule' else -.16
        bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,floorz));bpy.context.object.data.materials.append(mat('PreviewGround',(.50,.55,.57)))
        marker=mat('ScaleMarker',(.05,.52,.57));human_y=-3.7 if name=='RocksPierModule' else -5.7
        for z,sz,rad in ((.42,.76,.10),(1.02,.70,.22),(1.65,.3,.14)):
            bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=4,radius=1,location=(.60,human_y,z))
            obj=bpy.context.object;obj.scale=(rad,rad,sz/2);obj.data.materials.append(marker)
        bpy.ops.object.text_add(location=(-1.6,human_y-.8,.04));bpy.context.object.data.body='1.8 m';bpy.context.object.data.size=.30;bpy.context.object.data.materials.append(marker)
        bpy.ops.object.light_add(type='AREA',location=(-7,-9,16));bpy.context.object.data.energy=2600;bpy.context.object.data.size=10
        bpy.ops.object.light_add(type='AREA',location=(8,4,10));bpy.context.object.data.energy=1500;bpy.context.object.data.size=8
        loc=(0,-28,5.5) if view=='Front' else (18,-23,15) if view=='ThreeQuarter' else (7,-10,25)
        if name=='RocksPierModule':loc=(0,-17,5) if view=='Front' else (10,-14,10)
        bpy.ops.object.camera_add(location=loc);cam=bpy.context.object;target=Vector((0,-.6,1.4 if name=='LuckyBaitCasinoShell' else -.25))
        cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=23 if name=='LuckyBaitCasinoShell' else 15
        scene=bpy.context.scene;scene.camera=cam;scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
        scene.render.image_settings.file_format='PNG';scene.render.filepath=str(out/f'{name}-{view}.png');bpy.ops.render.render(write_still=True)
