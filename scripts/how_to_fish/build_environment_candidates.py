"""Original HowToFish G5 candidates. Never writes production Assets.
Use the existing verified exporter through a Library-only staging project.
"""
import sys, math, random, json, shutil, hashlib
from pathlib import Path
import bpy, bmesh
from mathutils import Vector
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, tube
from fbx_export import save_and_export

PROJECT = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
STAGE = PROJECT / 'Library/HowToFish/Staging/EnvironmentModels'
SOURCE = PROJECT / 'ArtSource~/how_to_fish'
RNG = random.Random(4104)

def rock(name, center, size, mats, lean=(.1,-.1), corners=7):
    vertices=[]
    phases=[RNG.uniform(-.13,.13) for _ in range(corners)]
    radii=[RNG.uniform(.78,1.1) for _ in range(corners)]
    for level,(height,scale) in enumerate(((0,.72),(.28,1),(.72,.91),(1,.42))):
        for i in range(corners):
            a=i*math.tau/corners+phases[i]
            vertices.append((center[0]+size[0]*(math.cos(a)*radii[i]*scale+lean[0]*height),
                             center[1]+size[1]*(math.sin(a)*radii[i]*scale+lean[1]*height),
                             center[2]+size[2]*height*(1+(.1*math.sin(i*3.1) if level else 0))))
    faces=[tuple(reversed(range(corners)))]
    for level in range(3):
        for i in range(corners):
            a=level*corners+i;b=level*corners+(i+1)%corners;c=b+corners;d=a+corners
            if (i+level)%3==0:faces.extend(((a,b,d),(b,c,d)))
            else:faces.append((a,b,c,d))
    faces.append(tuple(range(corners*3,corners*4)))
    obj=mesh(name,vertices,faces,mats[0])
    for mat in mats[1:]:obj.data.materials.append(mat)
    for polygon in obj.data.polygons:polygon.material_index=RNG.choices(range(len(mats)),[6,2,1])[0]

def stones(basalt=False,pebbles=False):
    mats=[material('VolcanoBasalt',(.105,.10,.13)),material('VolcanoAsh',(.22,.21,.24)),material('BasaltFracture',(.30,.27,.29))] if basalt else [material('ShoreStone',(.39,.41,.40)),material('ShoreStoneLight',(.54,.53,.47)),material('ShoreStoneWet',(.23,.28,.30))]
    if pebbles:
        for i in range(17):
            a=RNG.random()*math.tau;r=RNG.uniform(.1,1.25);scale=RNG.uniform(.07,.22)
            rock('Pebble'+str(i),(math.cos(a)*r,math.sin(a)*r*.6,0),(scale,scale*RNG.uniform(.7,1.6),scale*.75),mats,corners=5)
    else:
        layouts=[(-.52,.0,1,.68,1.08),(.40,.3,.65,.65,.74),(.73,-.55,.53,.4,.50),(-.82,-.64,.36,.41,.34),(.1,-.62,.48,.37,.35),(-.05,.72,.33,.35,.39)]
        for i,(x,y,sx,sy,sz) in enumerate(layouts):
            rock('FracturedStone'+str(i),(x,y,0),(sx,sy,sz*(1.25 if basalt else 1)),mats,lean=(.28 if basalt else -.14,.07),corners=6 if basalt else 7)


def needle_spray(name,a,b,width,height,mats):
    a,b=Vector(a),Vector(b);axis=b-a;side=Vector((-axis.y,axis.x,0)).normalized()
    vertices=[tuple(a+Vector((0,0,height*.4)))]
    # Alternating narrow facets produce individual branch silhouettes instead of stacked cones.
    outline=[]
    for sign in (1,-1):
        indexes=range(7) if sign==1 else reversed(range(7))
        for i in indexes:
            t=i/6;w=width*(.2+.8*math.sin(math.pi*t))*(.63 if i%2 else 1)
            outline.append(a+axis*t+side*w*sign+Vector((0,0,-height*.20)))
    vertices+=list(map(tuple,outline));vertices.append(tuple(a+axis*.52+Vector((0,0,height))))
    peak=len(vertices)-1
    faces=[(peak,1+(i+1)%len(outline),1+i) for i in range(len(outline))]
    faces.append(tuple(range(1,1+len(outline))))
    obj=mesh(name,vertices,faces,mats[0])
    for mat in mats[1:]:obj.data.materials.append(mat)
    for face in obj.data.polygons:face.material_index=RNG.randrange(len(mats)) if face.index%4==0 else 0


def pine(variant):
    bark=material('PineBark',(.19,.115,.075))
    foliage=[material('PineNeedles',(.12,.23,.085)),material('PineTips',(.19,.30,.12)),material('PineShade',(.075,.16,.085))]
    h=5.4 if variant==0 else 6.1
    lean=.07 if variant==0 else -.12
    for k in range(6):
        z=k*h/6;nextz=(k+1)*h/6
        tube('Trunk'+str(k),(lean*z,.02*math.sin(k),z),(lean*nextz,.02*math.sin(k+1),nextz),.17*(1-k/7),.17*(1-(k+1)/7),bark,7)
    for i in range(6):
        a=i*math.tau/6;tube('Root'+str(i),(0,0,.12),(.54*math.cos(a),.54*math.sin(a),.025),.10,.016,bark,5)
    for tier in range(6):
        z=.85+tier*.75;length=(1.8-tier*.23)*(1 if variant==0 else .9)
        for branch in range(5):
            angle=branch*math.tau/5+tier*.71+variant*.31
            base=Vector((lean*z,0,z));v=Vector((math.cos(angle),math.sin(angle),0))
            end=base+v*length+Vector((0,0,-.12 if tier<3 else .1))
            tube(f'Branch{tier}_{branch}',base,end,.045*(1-tier*.10),.008,bark,5)
            for twig in range(3):
                a=base+v*length*(.12+twig*.22)
                b=a+v*length*.56+Vector((0,0,.28))
                needle_spray(f'Needles{tier}_{branch}_{twig}',a,b,length*.25,.27,foliage)
    needle_spray('Leader',(lean*(h-.55),0,h-.6),(lean*h,-.03,h),.28,.20,foliage)


def shrub():
    bark=material('PineBark',(.19,.115,.075));foliage=[material('ShrubLeaf',(.23,.34,.11)),material('ShrubNewLeaf',(.40,.47,.18)),material('ShrubShade',(.12,.24,.105))]
    for i in range(9):
        a=i*2.399;length=RNG.uniform(.45,.85);base=Vector((.05*math.cos(a),.05*math.sin(a),0))
        tip=Vector((math.cos(a)*length*.75,math.sin(a)*length*.75,length))
        tube('Stem'+str(i),base,tip,.026,.007,bark,5)
        for j in range(4):
            start=base+(tip-base)*(.26+j*.16);v=Vector((math.cos(a+j*1.7),math.sin(a+j*1.7),.26))
            needle_spray(f'LeafSprig{i}_{j}',start,start+v*.4,.17,.09,foliage)


def grass():
    mats=[material('GrassBlade',(.28,.39,.13)),material('GrassTip',(.48,.53,.20)),material('GrassShade',(.16,.28,.10))]
    for i in range(33):
        angle=RNG.random()*math.tau;r=RNG.uniform(.0,.45);height=RNG.uniform(.22,.65);width=RNG.uniform(.018,.045)
        base=Vector((math.cos(angle)*r,math.sin(angle)*r,0));side=Vector((-math.sin(angle),math.cos(angle),0))*width
        bend=Vector((math.cos(angle),math.sin(angle),0))*height*.32
        vertices=[base-side,base+side,base+bend*.45+Vector((0,0,height*.60))+side*.5,base+bend*.45+Vector((0,0,height*.60))-side*.5,base+bend+Vector((0,0,height))]
        # A thin closed blade avoids coplanar front/back faces flickering in two-sided previews.
        thickness=Vector((math.cos(angle),math.sin(angle),0))*.004
        vertices=[v-thickness for v in vertices]+[v+thickness for v in vertices]
        front=[(0,1,2),(0,2,3),(3,2,4)]
        faces=front+[tuple(index+5 for index in reversed(face)) for face in front]
        boundary=(0,1,2,4,3)
        faces += [(a,b,b+5,a+5) for a,b in zip(boundary,boundary[1:]+boundary[:1])]
        obj=mesh('Blade'+str(i),vertices,faces,mats[i%3])
        bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()


def merge_meshes(name):
    bpy.ops.object.select_all(action='DESELECT');objects=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=name+'Visual'
    return obj


def build(name,fn,combine=True):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for existing in list(bpy.data.materials):bpy.data.materials.remove(existing)
    RNG.seed(name)
    art.ROOT=None;art.MATERIALS.clear();art.ROOT=empty(name);fn()
    if combine:merge_meshes(name)
    objects=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    empty('GroundAnchor');empty('Forward',(0,-1,0));empty('Right',(-1,0,0));empty('Up',(0,0,1))
    bpy.context.view_layer.update()
    corners=[obj.matrix_world@Vector(corner) for obj in objects for corner in obj.bound_box]
    low=[min(v[i] for v in corners) for i in range(3)];high=[max(v[i] for v in corners) for i in range(3)]
    triangles=sum(len(face.vertices)-2 for obj in objects for face in obj.data.polygons)
    stage_project=STAGE/'ExportWorkspace'
    report=save_and_export(stage_project,name)
    SOURCE.mkdir(parents=True,exist_ok=True);(STAGE/'FBX').mkdir(parents=True,exist_ok=True)
    staged_source=stage_project/'ArtSource~/how_to_fish'/f'{name}.blend'
    source=SOURCE/f'{name}.blend'
    if source.exists() and '--refresh-candidates' not in sys.argv:raise RuntimeError('Existing source protected: '+str(source))
    shutil.copy2(staged_source,source)
    fbx=STAGE/'FBX'/f'{name}.fbx';shutil.copy2(stage_project/'Assets/LoadResources/Demos/how_to_fish/Art/Models'/f'{name}.fbx',fbx)
    report.update({'bounds_blender_min_m':low,'bounds_blender_max_m':high,'size_blender_m':[high[i]-low[i] for i in range(3)],'triangles':triangles,'source':str(source),'candidate_fbx':str(fbx),'authorship':'Original procedural meshes; no source-game geometry copied.','placement':'Visual only. GroundAnchor at origin. Keep paths and interaction positions clear. Unity import pending.'})
    (SOURCE/f'{name}.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    (STAGE/f'{name}.json').write_text(json.dumps(report,indent=2),encoding='utf-8')

assets=[('ShoreRockCluster',lambda:stones()),('BasaltRockCluster',lambda:stones(True)),('ShorePebbleScatter',lambda:stones(pebbles=True)),('PineBranchedA',lambda:pine(0)),('PineBranchedB',lambda:pine(1)),('ForestShrub',shrub),('ForestGrass',grass)]
if __name__ == '__main__':
    requested=[arg for arg in sys.argv[sys.argv.index('--')+2:] if arg != '--refresh-candidates']
    for name,fn in assets:
        if not requested or name in requested:build(name,fn)
