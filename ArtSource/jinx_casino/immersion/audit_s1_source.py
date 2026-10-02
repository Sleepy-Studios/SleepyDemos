"""Read only the generated S1 source, writing a Library report; no Unity or source mutation."""
import hashlib
import json
from pathlib import Path
import bpy
from mathutils import Vector

source=Path(__file__).parent;workspace=source.parents[2]
staging=workspace/'Library/JinxCasino/ImmersionStaging/Art'
blueprint=json.loads((source/'S1Layout.json').read_text(encoding='utf-8'))
manifest=json.loads((staging/'Models/manifest.json').read_text(encoding='utf-8'))
bpy.ops.wm.open_mainfile(filepath=str(source/'S1Models.blend'))
def U(point):return [-point.x,point.z,-point.y]
report={'kind':'DCC_source_audit_not_Unity_import_validation','models':[],'targets':[]}
for entry in manifest['models']:
    collection=bpy.data.collections[entry['name']];root=bpy.data.objects[entry['name']]
    assert root.location.length<1e-6 and (root.scale-Vector((1,1,1))).length<1e-6
    meshes=[obj for obj in collection.objects if obj.type=='MESH']
    triangles=0
    for obj in collection.objects:
        assert obj.type in {'MESH','EMPTY'},(entry['name'],obj.name,obj.type)
        assert min(obj.scale)>0 and all(abs(value)<1e-5 for value in obj.rotation_euler),(obj.name,tuple(obj.rotation_euler),tuple(obj.scale))
        if obj.type=='MESH':obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
    assert triangles==entry['triangles'] and len(meshes)==entry['meshCount'],entry['name']
    assert hashlib.sha256((staging/'Models'/entry['file']).read_bytes()).hexdigest()==entry['sha256']
    report['models'].append({'name':entry['name'],'triangles':triangles,'meshCount':len(meshes),'rootIdentity':True,'onlyMeshAndEmpty':True,'sha256':entry['sha256']})
for station in blueprint['stations']:
    for target in station['targets']:
        actual=U(bpy.data.objects[target['node']].matrix_world.translation)
        error=sum((a-b)**2 for a,b in zip(actual,target['position']))**.5
        assert error<.0001,(target['node'],actual,target['position'],error)
        report['targets'].append({'node':target['node'],'unityPosition':actual,'positionErrorMetres':error})
assert all('Blackjack.PlayerCard'+str(index) in bpy.data.objects and 'Blackjack.DealerCard'+str(index) in bpy.data.objects for index in range(12))
assert all('Blackjack.Rank'+str(index) in bpy.data.objects for index in range(1,14))
assert all('Slots.Reel'+str(index) in bpy.data.objects for index in range(3))
assert all('Slots.PayoutChip'+str(index) in bpy.data.objects for index in range(8))
assert all('CooperativeLevers.Lever'+str(index) in bpy.data.objects and 'CooperativeLevers.TimingNeedle'+str(index) in bpy.data.objects for index in range(2))
report['cardPool']={'player':12,'dealer':12,'rankFaceTemplates':13,'drawCard':'Blackjack.DrawCard','dealOrigin':'Blackjack.DealOrigin'}
report['totalSourceTriangles']=sum(model['triangles'] for model in report['models'])
(staging/'SourceAudit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('S1_SOURCE_AUDIT_READY targets='+str(len(report['targets']))+' triangles='+str(report['totalSourceTriangles']),flush=True)
