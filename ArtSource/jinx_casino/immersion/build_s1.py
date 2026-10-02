"""Independent S1 immersion sources. Blender --background --factory-startup only.
Reuse validated geometry/export functions by AST, never execute the legacy build entry.
All physical coordinates authored through the approved Unity-to-source mapping.
"""
import ast
import hashlib
import json
import math
from pathlib import Path
from types import SimpleNamespace
import bpy
import bmesh
from mathutils import Matrix, Vector
from io_scene_fbx import fbx_utils

WORKSPACE = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent
STAGING = WORKSPACE / 'Library/JinxCasino/ImmersionStaging/Art'
MODELS = STAGING / 'Models'; PREVIEWS = STAGING / 'Previews'
MODELS.mkdir(parents=True, exist_ok=True); PREVIEWS.mkdir(parents=True, exist_ok=True)
BLUEPRINT = json.loads((SOURCE / 'S1Layout.json').read_text(encoding='utf-8'))
LEGACY = WORKSPACE / 'ArtSource/jinx_casino/build_models.py'
legacy_tree = ast.parse(LEGACY.read_text(encoding='utf-8'))
selected = [node for node in legacy_tree.body if
    isinstance(node, ast.ClassDef) and node.name == 'Model' or
    isinstance(node, ast.FunctionDef) and node.name in {'hierarchy_baked_matrix','triangulate_export_meshes','export'}]
palette_node = next(node for node in legacy_tree.body if isinstance(node, ast.Assign) and any(isinstance(target, ast.Name) and target.id == 'PALETTE' for target in node.targets))
PALETTE = ast.literal_eval(palette_node.value)
bpy.ops.wm.read_factory_settings(use_empty=True)
SCENE = bpy.context.scene; SCENE.unit_settings.system = 'METRIC'; SCENE.unit_settings.scale_length = 1
MATERIALS = {}
for name, (hex_value, metallic, roughness) in PALETTE.items():
    srgb = [int(hex_value[index:index+2],16)/255 for index in (0,2,4)]
    rgb = [value/12.92 if value<=.04045 else ((value+.055)/1.055)**2.4 for value in srgb]
    mat = bpy.data.materials.new(name); mat.diffuse_color = (*rgb,1); mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF'); shader.inputs['Base Color'].default_value = (*rgb,1)
    shader.inputs['Metallic'].default_value = .88 if name=='CopperGold' else .04 if name in ('InkBlue','PaperLight','CreamYellow') else metallic
    shader.inputs['Roughness'].default_value = .27 if name=='CopperGold' else .73 if name=='InkBlue' else .62 if name in ('PaperLight','CreamYellow') else .46
    MATERIALS[name] = mat
exec(compile(ast.Module(body=selected,type_ignores=[]),str(LEGACY),'exec'),globals())
ORIGINAL_FBX_MATRIX = fbx_utils.ObjectWrapper.fbx_object_matrix
fbx_utils.ObjectWrapper.fbx_object_matrix = hierarchy_baked_matrix
OPTIONS = SimpleNamespace(phase='first',preserve_existing=False,refresh_model=[],refresh_avatar=False)
old_entries = {}
def normalize_foot(model):
    # Exact approved target heights stay unchanged; floor hull intentionally extends below Y=0.
    return
FONT = bpy.data.fonts.load(str(WORKSPACE / 'Assets/LoadResources/Fonts/Source/CN/HarmonyOS_CN.ttf'))

def S(point): return (-point[0],-point[2],point[1])
def sizeS(size): return (size[0],size[2],size[1])
def pivot(model,name,point,parent=None):
    node = model.pivot(name,S(point),parent); node.name = name; return node
def box(model,name,point,size,material='InkBlue',bevel=.025,parent=None):
    return model.box(name,S(point),sizeS(size),material,bevel,parent)
def sphere(model,name,point,size,material='Mint',parent=None):
    return model.sphere(name,S(point),sizeS(size),material,parent)
def rod(model,name,start,end,radius=.02,material='CopperGold',parent=None):
    return model.rod(name,S(start),S(end),radius,material,parent)
def cylinder(model,name,point,radius,depth,material='CopperGold',parent=None,axis='Y',vertices=24):
    return model.cylinder(name,S(point),radius,depth,material,parent,{'Y':'Z','Z':'Y','X':'X'}[axis],vertices)
def torus(model,name,point,radius,tube=.025,material='CopperGold',parent=None,axis='Y'):
    return model.torus(name,S(point),radius,tube,material,parent,{'Y':'Z','Z':'Y','X':'X'}[axis])
def text(model,name,body,point,height=.10,material='FaceInk',parent=None,flat=False,reverse=False):
    data = bpy.data.curves.new(name,'FONT'); data.body=body; data.font=FONT; data.size=height
    data.align_x='CENTER'; data.align_y='CENTER'; data.extrude=.0006; data.bevel_depth=0; data.bevel_resolution=0; data.resolution_u=2
    obj=bpy.data.objects.new(name,data); SCENE.collection.objects.link(obj); obj.location=S(point)
    if not flat: obj.rotation_euler.x=math.pi/2
    if reverse: obj.rotation_euler.z=math.pi
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH'); obj=bpy.context.object; return model.own(obj,name,material,parent)
def extrusion(model,name,polygon,y0,y1,material='InkBlue',parent=None,bevel=.015):
    vertices=[S((x,y,z)) for y in (y0,y1) for x,z in polygon]; count=len(polygon)
    faces=[tuple(range(count-1,-1,-1)),tuple(range(count,count*2))]
    faces += [(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
    return model.mesh(name,vertices,faces,material,parent,bevel)

def arch(model,name,x,z,width,base,height,material='CreamYellow',parent=None):
    # Curved rib built as a closed extruded polygon, not a box with a sign pasted over it.
    outer=[]; inner=[]; radius=width/2; crown=height-base-radius
    for step in range(25):
        angle=math.pi*step/24
        outer.append((x+radius*math.cos(angle),base+crown+radius*math.sin(angle)))
        inner.append((x+(radius-.15)*math.cos(angle),base+crown+(radius-.15)*math.sin(angle)))
    polygon=outer+list(reversed(inner)); verts=[S((px,py,z+depth)) for depth in (-.10,.10) for px,py in polygon]
    count=len(polygon); faces=[tuple(range(count-1,-1,-1)),tuple(range(count,count*2))]
    faces += [(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
    model.mesh(name,verts,faces,material,parent,.02)
    if crown>0:
        for side in (-1,1): box(model,name+'Leg'+str(side),(x+side*(radius-.075),base+crown/2,z),(.15,crown,.20),material,.025,parent)

def target_props(model,definition):
    for target in definition['targets']:
        p=target['position']; node=pivot(model,target['node'],p)
        action=target['action']; value=target['value']
        if action=='ChipAdd':
            base_y=1.171 if definition['game']==3 else p[1]-.005
            for layer in range(4):
                cylinder(model,'Chip'+target['id']+str(layer),(p[0],base_y+layer*.018,p[2]),.095,.018,
                    'PlumRed' if value==10 else 'Mint' if value==50 else 'CopperGold',node)
            torus(model,'ChipRim'+target['id'],(p[0],base_y+.066,p[2]),.070,.007,'PaperLight',node)
            text(model,'ChipNumber'+target['id'],'+'+str(value),(p[0],base_y+.068,p[2]),.062,'FaceInk',node,True)
        elif target['id']=='primary' and definition['game'] in (0,10):
            sphere(model,'Grip'+target['id'],p,(.13,.12,.11),'Mint' if definition['game']==0 else 'PlumRed',node)
        else:
            flat=definition['game']==3 or action=='ChipClear'
            size=(target['size'][0]*.87,.065,target['size'][2]*.85) if flat else (target['size'][0]*.87,target['size'][1]*.8,.075)
            box(model,'Control'+target['id'],p,size,'CopperGold',.02,node)
            if flat:
                box(model,'ControlInset'+target['id'],(p[0],p[1]+.034,p[2]),(size[0]-.035,.01,size[2]-.035),'PlumRed' if action=='Secondary' else 'PaperLight',.009,node)
                label={'ChipClear':'退','Commit':'下注','Primary':'要牌','Secondary':'停牌','Help':'?'}[action]
                text(model,'ControlLabel'+target['id'],label,(p[0],p[1]+.043,p[2]),.065,'FaceInk',node,True)
            else:
                label={'Commit':'投币' if definition['game']==0 else '就绪','Help':'?'}[action]
                text(model,'ControlLabel'+target['id'],label,(p[0],p[1],p[2]+.043),.075,'FaceInk',node)
    plaque=pivot(model,'StatePlaque',(0,1.35,.54) if definition['game']==0 else (0,1.24,-.66) if definition['game']==3 else (0,1.42,.47))
    # Empty plaque leaves room for the saved small state text, not a UGUI interaction panel.
    p=tuple((-plaque.matrix_world.translation.x,plaque.matrix_world.translation.z,-plaque.matrix_world.translation.y))
    box(model,'StatePlaqueFrame',p,(.62,.13,.065),'CopperGold',.015,plaque)
    box(model,'StatePlaqueFace',(p[0],p[1],p[2]+.035),(.56,.095,.016),'InkBlue',.006,plaque)

def fruit(model,name,kind,center,parent):
    x,y,z=center
    if kind==0:
        for side in (-1,1):
            sphere(model,name+'Cherry'+str(side),(x+side*.045,y-.025,z),(.056,.056,.025),'PlumRed',parent)
            rod(model,name+'Stem'+str(side),(x+side*.045,y+.018,z),(x,y+.083,z),.007,'CopperGold',parent)
        sphere(model,name+'Leaf',(x+.024,y+.072,z),(.040,.015,.010),'Mint',parent)
    elif kind==1:
        sphere(model,name+'Lemon',(x,y,z),(.093,.058,.028),'color_gold',parent)
        for side in (-1,1): sphere(model,name+'Nub'+str(side),(x+side*.088,y,z),(.017,.017,.020),'CopperGold',parent)
        rod(model,name+'Slice',(x-.060,y+.012,z+.025),(x+.060,y+.012,z+.025),.004,'PaperLight',parent)
    elif kind==2:
        verts=[]
        for layer in (z-.012,z+.012):
            for corner in range(10):
                angle=math.pi/2+corner*math.pi/5; radius=.092 if corner%2==0 else .042
                verts.append(S((x+radius*math.cos(angle),y+radius*math.sin(angle),layer)))
        faces=[tuple(range(9,-1,-1)),tuple(range(10,20))]+[(i,(i+1)%10,(i+1)%10+10,i+10) for i in range(10)]
        model.mesh(name+'Star',verts,faces,'CopperGold',parent,.004)
    elif kind==3:
        # Bell: splayed silhouette, separate lip and clapper, not a coloured disc.
        for level in range(4):
            box(model,name+'BellTier'+str(level),(x,y+.047-level*.029,z),(.075+level*.025,.028,.04),'CopperGold',.012,parent)
        sphere(model,name+'Clapper',(x,y-.069,z),(.025,.021,.025),'PlumRed',parent)
    elif kind==4: text(model,name+'Seven','7',(x,y,z+.023),.20,'PlumRed',parent)
    else:
        verts=[];steps=10;rings=8
        for piece in range(steps+1):
            t=piece/steps;angle=-1.15+t*2.15;radius=.004+.018*(math.sin(math.pi*t)**.4)
            for ring in range(rings):
                around=ring*2*math.pi/rings
                verts.append(S((x+math.sin(angle)*.090-math.sin(angle)*radius*math.cos(around),
                    y-math.cos(angle)*.105+.045+math.cos(angle)*radius*math.cos(around),z+radius*math.sin(around))))
        faces=[]
        for piece in range(steps):
            for ring in range(rings):faces.append((piece*rings+ring,piece*rings+(ring+1)%rings,(piece+1)*rings+(ring+1)%rings,(piece+1)*rings+ring))
        faces.extend([tuple(range(rings-1,-1,-1)),tuple(range(steps*rings,(steps+1)*rings))])
        model.mesh(name+'Banana',verts,faces,'color_gold',parent,.002)
        rod(model,name+'Tip',(x-.077,y+.002,z),(x-.086,y+.023,z),.008,'CopperGold',parent)

def make_slots(definition):
    model=Model('S1Slots','Station','Slots')
    for x in (-.73,.73): box(model,'SplayedFoot'+str(x),(x,.075,-.02),(.40,.15,1.04),'CopperGold',.055)
    box(model,'Plinth',(0,.22,0),(1.92,.30,1.15),'InkBlue',.12)
    box(model,'LowerCabinet',(0,.71,-.12),(1.89,.75,.95),'PlumRed',.13)
    box(model,'Apron',(0,1.07,.28),(1.90,.13,1.38),'CopperGold',.06)
    box(model,'ControlShelf',(0,1.12,.56),(1.8,.05,.74),'InkBlue',.025)
    for side in (-1,1):
        box(model,'Shoulder'+str(side),(side*.92,1.72,-.04),(.19,1.13,.81),'PlumRed',.085)
        box(model,'Moulding'+str(side),(side*.86,1.72,.385),(.09,.79,.09),'CopperGold',.025)
    box(model,'BackCase',(0,1.72,-.36),(1.72,1.03,.21),'PlumRed',.09)
    for y in (1.43,1.97): box(model,'WindowRail'+str(y),(0,y,.385),(1.75,.18,.14),'CopperGold',.028)
    for x in (-.24,.24): box(model,'WindowDivider'+str(x),(x,1.70,.39),(.09,.40,.15),'InkBlue',.016)
    for reel_index,x in enumerate((.48,0,-.48)):
        reel=pivot(model,'Slots.Reel'+str(reel_index),(x,1.70,.05))
        cylinder(model,'Drum'+str(reel_index),(x,1.70,.05),.26,.40,'PaperLight',reel,'X',36)
        for symbol in range(6):
            before=set(model.collection.objects)
            fruit(model,'Symbol'+str(reel_index)+'_'+str(symbol),symbol,(x,1.70,.323),reel)
            rotation=Matrix.Translation(Vector(S((x,1.70,.05)))) @ Matrix.Rotation(-symbol*math.pi/3,4,'X') @ Matrix.Translation(-Vector(S((x,1.70,.05))))
            for obj in set(model.collection.objects)-before:
                obj.matrix_world=rotation @ obj.matrix_world
                bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
                bpy.ops.object.transform_apply(location=False,rotation=True,scale=True); obj.select_set(False)
    arch(model,'FruitCrown',0,-.08,1.92,1.96,2.60,'CopperGold')
    box(model,'Title',(0,2.37,.10),(1.36,.30,.15),'InkBlue',.075)
    text(model,'TitleText','水果维修',(0,2.38,.19),.20,'CreamYellow')
    for i in range(9): sphere(model,'Bulb'+str(i),(-.83+i*.2075,2.33+(.14 if 1<i<7 else 0),.12),(.045,.045,.03),'PaperLight')
    lever=pivot(model,'Slots.LeverPivot',(-1.03,1.15,.12))
    rod(model,'LeverStem',(-1.03,1.15,.12),(-1.08,1.74,.61),.035,'CopperGold',lever)
    target_props(model,definition)
    grip=bpy.data.objects['Slots.HandleGrip'];world=grip.matrix_world.copy();grip.parent=lever;grip.matrix_parent_inverse=Matrix.Identity(4);grip.matrix_world=world
    tray=pivot(model,'Slots.PrizeTray',(0,.92,.63))
    box(model,'PrizeTray',(0,.92,.63),(.84,.15,.40),'InkBlue',.035,tray)
    for i in range(8):
        point=(-.31+i*.088,1.01,.64);chip=pivot(model,'Slots.PayoutChip'+str(i),point,tray)
        cylinder(model,'PrizeChip'+str(i),point,.045,.022,'CopperGold',chip)
    return model

def make_cards(definition):
    model=Model('S1Blackjack','Station','Blackjack')
    polygon=[(-1.35,-.79),(1.35,-.79)]+[(1.35*math.cos(math.pi*i/24),.30+.60*math.sin(math.pi*i/24)) for i in range(25)]
    extrusion(model,'TableLip',polygon,1.045,1.145,'CopperGold',bevel=.035)
    extrusion(model,'Felt',[(x*.972,z*.972) for x,z in polygon],1.145,1.16,'Mint',bevel=.006)
    for x in (-.85,.85):
        box(model,'Foot'+str(x),(x,.09,-.03),(.55,.18,1.10),'InkBlue',.07)
        cylinder(model,'FlutedLeg'+str(x),(x,.60,-.05),.14,.95,'CopperGold',vertices=12)
        cylinder(model,'Collar'+str(x),(x,.27,-.05),.19,.10,'PlumRed',vertices=16)
    box(model,'UnderApron',(0,.88,-.08),(2.42,.29,1.20),'InkBlue',.08)
    shoe=pivot(model,'Blackjack.CardShoe',(-1.06,1.32,-.42))
    box(model,'DeckShoe',(-1.06,1.32,-.42),(.39,.29,.52),'PlumRed',.045,shoe)
    for i in range(4): box(model,'DeckEdge'+str(i),(-1.06,1.455+i*.015,-.40),(.28,.015,.38),'PaperLight',.004,shoe)
    pivot(model,'Blackjack.DealOrigin',(-1.06,1.52,-.32))
    draw=pivot(model,'Blackjack.DrawCard',(-1.06,1.52,-.32))
    box(model,'DrawPaper',(-1.06,1.52,-.32),(.23,.006,.33),'PaperLight',.006,draw)
    box(model,'DrawBack',(-1.06,1.524,-.32),(.21,.001,.31),'InkBlue',.003,draw)
    for side,x,z in [('Player',-.32,.10),('Dealer',0,-.66)]:
        total=pivot(model,'Blackjack.'+side+'Total',(x,1.24,z))
        box(model,side+'CounterFrame',(x,1.24,z),(.30,.13,.09),'CopperGold',.015,total)
        box(model,side+'CounterFace',(x,1.24,z+.05),(.25,.092,.012),'InkBlue',.004,total)
    arm=pivot(model,'Blackjack.DealerArmPivot',(.93,1.22,-.43))
    for a,b in [((.93,1.22,-.43),(.93,1.70,-.43)),((.93,1.70,-.43),(.50,1.78,-.25)),((.50,1.78,-.25),(.17,1.35,-.24))]:
        rod(model,'DealerLink'+str(a),a,b,.045,'CopperGold',arm)
        sphere(model,'DealerJoint'+str(a),a,(.085,.085,.085),'InkBlue',arm)
    box(model,'DealerPalm',(.17,1.35,-.24),(.24,.06,.16),'PlumRed',.025,arm)
    # Separate saved hand slots and rank prototypes; default face will be assigned by the safe public projection.
    for side,z in [('Player',.20),('Dealer',-.38)]:
        for i in range(12):
            card=pivot(model,'Blackjack.'+side+'Card'+str(i),((i-5.5)*.17,1.174+i*.0004,z))
            box(model,side+'Paper'+str(i),((i-5.5)*.17,1.174+i*.0004,z),(.23,.006,.33),'PaperLight',.008,card)
            box(model,side+'Back'+str(i),((i-5.5)*.17,1.178+i*.0004,z),(.209,.001,.309),'InkBlue',.004,card)
            text(model,side+'Seal'+str(i),'☆',((i-5.5)*.17,1.180+i*.0004,z),.10,'CopperGold',card,True)
    library=pivot(model,'Blackjack.CardFaceLibrary',(0,1.176,.20))
    for rank in range(1,14):
        card=pivot(model,'Blackjack.Rank'+str(rank),(0,1.176,.20),library)
        box(model,'FacePaper'+str(rank),(0,1.176,.20),(.23,.006,.33),'PaperLight',.008,card)
        body='A' if rank==1 else 'J' if rank==11 else 'Q' if rank==12 else 'K' if rank==13 else str(rank)
        text(model,'FaceIndex'+str(rank),body,(-.075,1.181,.305),.046,'FaceInk',card,True)
        text(model,'FaceMiddle'+str(rank),body,(0,1.181,.195),.118,'FaceInk',card,True)
        text(model,'ClubMark'+str(rank),'☆',(.070,1.181,.09),.047,'PlumRed',card,True)
    target_props(model,definition)
    torus(model,'BettingRing',(-.52,1.206,.64),.155,.007,'CreamYellow')
    text(model,'RuleOnFelt','好运不会自动停牌',(0,1.169,-.68),.070,'InkBlue',flat=True)
    return model

def dial_arc(model,name,point,start,end,parent,material='Mint'):
    x,y,z=point; verts=[]; steps=12
    for depth in (-.004,.004):
        for radius in (.205,.239):
            for i in range(steps+1):
                theta=math.radians(start+(end-start)*i/steps)
                verts.append(S((x+radius*math.sin(theta),y+radius*math.cos(theta),z+depth)))
    width=steps+1; faces=[]
    for i in range(steps):
        faces += [(i,i+1,width+i+1,width+i),(2*width+i,3*width+i,3*width+i+1,2*width+i+1),
            (i,2*width+i,2*width+i+1,i+1),(width+i,width+i+1,3*width+i+1,3*width+i)]
    faces += [(0,width,3*width,2*width),(steps,2*width+steps,3*width+steps,width+steps)]
    model.mesh(name,verts,faces,material,parent,.003)

def make_levers(definition):
    model=Model('S1Levers','Station','CooperativeLevers')
    for x in (-.82,.82): box(model,'Skid'+str(x),(x,.075,-.10),(.48,.15,1.06),'CopperGold',.045)
    box(model,'LowerCase',(0,.71,-.16),(2.32,1.12,.95),'InkBlue',.13)
    box(model,'ControlBench',(0,1.07,.42),(2.34,.13,.97),'CopperGold',.045)
    box(model,'ControlSurface',(0,1.13,.55),(2.21,.035,.78),'InkBlue',.025)
    box(model,'GaugeCase',(0,1.80,-.19),(2.05,.93,.39),'PlumRed',.13)
    for i,x in enumerate((.55,-.55)):
        face=pivot(model,'CooperativeLevers.TimingFace'+str(i),(x,1.87,.10))
        cylinder(model,'GaugeBacking'+str(i),(x,1.87,.10),.30,.105,'CopperGold',face,'Z',40)
        cylinder(model,'GaugeFace'+str(i),(x,1.87,.17),.265,.025,'PaperLight',face,'Z',40)
        for mark in range(20):
            theta=mark*math.pi/10
            rod(model,'Tick'+str(i)+'_'+str(mark),(x+math.sin(theta)*.232,1.87+math.cos(theta)*.232,.19),
                (x+math.sin(theta)*.255,1.87+math.cos(theta)*.255,.19),.004,'InkBlue',face)
        needle=pivot(model,'CooperativeLevers.TimingNeedle'+str(i),(x,1.87,.21))
        rod(model,'Needle'+str(i),(x,1.87,.21),(x,2.10,.21),.009,'PlumRed',needle)
        sphere(model,'NeedleHub'+str(i),(x,1.87,.22),(.026,.026,.018),'CopperGold',needle)
        arc=pivot(model,'CooperativeLevers.Window'+str(i),(x,1.87,.205))
        dial_arc(model,'ActiveArc'+str(i),(x,1.87,.205),36+i*45,72+i*45,arc)
        helped=pivot(model,'CooperativeLevers.HelpWindow'+str(i),(x,1.87,.207))
        dial_arc(model,'HelpArc'+str(i),(x,1.87,.207),18+i*45,90+i*45,helped,'color_blue')
        lever=pivot(model,'CooperativeLevers.Lever'+str(i),(x,1.16,.28))
        rod(model,'Lever'+str(i),(x,1.16,.28),(x,1.60,.58),.036,'CopperGold',lever)
        if i==1:
            npc=pivot(model,'CooperativeLevers.NpcHandle',(x,1.60,.58),lever)
            sphere(model,'NpcGrip',(x,1.60,.58),(.13,.12,.11),'Mint',npc)
    for side in (-1,1):
        rod(model,'SideConduit'+str(side),(side*.83,2.18,-.05),(0,2.28,-.05),.028,'CopperGold')
    sync=pivot(model,'CooperativeLevers.SyncLamp',(0,2.22,.06))
    sphere(model,'SyncBulb',(0,2.22,.06),(.11,.11,.07),'Mint',sync)
    target_props(model,definition)
    grip=bpy.data.objects['CooperativeLevers.PlayerHandle'];world=grip.matrix_world.copy()
    grip.parent=bpy.data.objects['CooperativeLevers.Lever0'];grip.matrix_parent_inverse=Matrix.Identity(4);grip.matrix_world=world
    text(model,'PlayerGripMark','你',(.55,1.60,.697),.10,'FaceInk',grip)
    text(model,'NpcGripMark','伙伴',(-.55,1.60,.697),.075,'FaceInk',bpy.data.objects['CooperativeLevers.NpcHandle'])
    wrench=pivot(model,'CooperativeLevers.HelpWrench',(1.02,1.34,.22))
    rod(model,'WrenchHookedStem',(1.02,1.21,.22),(1.02,1.48,.22),.022,'CopperGold',wrench)
    for side in (-1,1):rod(model,'WrenchHookedJaw'+str(side),(1.02,1.48,.22),(1.02+side*.058,1.57,.22),.019,'CopperGold',wrench)
    return model

def make_hall():
    model=Model('S1Hall','Architecture','ImmersionS1')
    box(model,'Floor',(0,-.08,.5),(13.2,.16,12.6),'CreamYellow',.01)
    # Large, restrained tiles with broad join lines, not a single colour field or a dense chessboard.
    for x in (-5.5,-3.3,-1.1,1.1,3.3,5.5):
        for z in (-4.75,-2.65,-.55,1.55,3.65,5.75):
            box(model,'Tile'+str(x)+'_'+str(z),(x,.008,z),(2.15,.012,2.05),'PaperLight' if (round(x*10)+round(z*10))%4 else 'CreamYellow',.014)
    for x in (-6.6,6.6):
        box(model,'SideWall'+str(x),(x,2.3,.5),(.24,4.6,12.6),'InkBlue',.025)
        box(model,'SideWainscot'+str(x),(x-math.copysign(.14,x),.65,.5),(.07,1.0,12.3),'PlumRed',.01)
        for z in (-4,-.8,2.4,5.6):
            box(model,'Pilaster'+str(x)+str(z),(x-math.copysign(.19,x),2.05,z),(.15,4.10,.14),'CopperGold',.022)
    box(model,'BackWall',(0,2.3,6.8),(13.2,4.6,.24),'InkBlue',.025)
    for x in (-4.1,4.1): box(model,'EntranceWall'+str(x),(x,2.30,-5.8),(4.95,4.6,.24),'CreamYellow',.025)
    arch(model,'EntryArch',0,-5.64,3.25,0,4.22,'CopperGold')
    for x in (-3.7,0,3.7):
        arch(model,'BayArch'+str(x),x,6.54,3.45,.0,4.33,'CreamYellow')
        box(model,'BayBacking'+str(x),(x,2.0,6.57),(2.92,3.95,.06),'PlumRed' if x<0 else 'InkBlue' if x==0 else 'Mint',.10)
        for y in (.36,.52): box(model,'BayPlinth'+str(x)+str(y),(x,y,6.43),(3.22,.12,.27),'CopperGold',.035)
    # Shallow canopy lobes keep the ceiling visible and layered in first person.
    ceiling=pivot(model,'Hall.Ceiling',(0,0,0))
    box(model,'Roof',(0,4.70,.5),(13.2,.15,12.6),'PaperLight',.012,ceiling)
    for x in (-3.65,0,3.65):
        arch(model,'CanopyFront'+str(x),x,-1.40,3.56,3.18,4.68,'PlumRed' if x<0 else 'CopperGold' if x==0 else 'Mint',ceiling)
        for z in (-1.4,2.3,6.1):
            box(model,'CanopyBeam'+str(x)+str(z),(x,4.48,z),(3.3,.16,.14),'CreamYellow',.025,ceiling)
    prize=pivot(model,'Hall.PrizeWheel',(0,3.02,6.22))
    cylinder(model,'PrizeHousing',(0,3.02,6.22),.86,.20,'CopperGold',prize,'Z',36)
    cylinder(model,'PrizeBacking',(0,3.02,6.09),.77,.06,'InkBlue',prize,'Z',36)
    for i in range(8):
        theta=i*math.pi/4;x=math.sin(theta)*.62;y=3.02+math.cos(theta)*.62
        rod(model,'PrizeSpoke'+str(i),(0,3.02,6.01),(x,y,6.01),.026,'CopperGold',prize)
        box(model,'PrizeTicket'+str(i),(x,y,5.97),(.24,.31,.055),'PlumRed' if i%2 else 'Mint',.045,prize)
        sphere(model,'PrizeBulb'+str(i),(math.sin(theta)*.87,3.02+math.cos(theta)*.87,6.10),(.047,.047,.029),'CreamYellow',prize)
    sphere(model,'PrizeHub',(0,3.02,5.93),(.16,.16,.08),'CopperGold',prize)
    claw=pivot(model,'Hall.PrizeClaw',(0,3.98,5.84))
    for side in (-1,1):
        rod(model,'ClawRod'+str(side),(0,3.98,5.84),(side*.19,3.73,5.84),.03,'InkBlue',claw)
        rod(model,'ClawFinger'+str(side),(side*.19,3.73,5.84),(side*.11,3.56,5.84),.02,'CopperGold',claw)
    box(model,'PrizeChute',(0,1.75,6.32),(.78,1.40,.28),'PlumRed',.09)
    for i in range(6):box(model,'SpilledTicket'+str(i),(.08*math.sin(i),1.25-i*.05,6.03-i*.08),(.34,.025,.26),'CreamYellow',.018)
    text(model,'HallTitle','好运维修站',(0,4.19,6.03),.29,'CreamYellow',reverse=True)
    cylinder(model,'EntryRug',(0,.016,-.50),1.75,.016,'InkBlue',vertices=64)
    torus(model,'RugBorder',(0,.032,-.50),1.63,.013,'CopperGold')
    for i in range(12):
        theta=i*math.pi/6
        box(model,'RugTicket'+str(i),(math.sin(theta)*1.43,.036,-.50+math.cos(theta)*1.43),(.18,.006,.28),'PlumRed' if i%2 else 'Mint',.005)
    # Asymmetric wall-side vendor and maintenance rack add a lived-in layer outside the three station envelopes.
    box(model,'VendorCase',(-5.70,1.0,-.65),(.95,2.0,1.05),'PlumRed',.10)
    box(model,'VendorWindow',(-5.20,1.30,-.65),(.065,1.04,.87),'InkBlue',.025)
    for row in range(3):
        box(model,'VendorShelf'+str(row),(-5.14,.91+row*.28,-.65),(.08,.035,.78),'CopperGold',.006)
        for item in range(3):sphere(model,'VendorToy'+str(row)+str(item),(-5.10,1.02+row*.28,-.92+item*.27),(.065,.095,.06),'Mint' if row%2 else 'CreamYellow')
    box(model,'VendorAwning',(-5.70,2.09,-.65),(1.13,.14,1.25),'CreamYellow',.045)
    box(model,'ToolRack',(-3.7,1.75,6.41),(1.58,1.04,.08),'InkBlue',.045)
    for i in range(3):
        x=-4.12+i*.42
        rod(model,'WrenchStem'+str(i),(x,1.40,6.29),(x,1.91,6.29),.035,'CopperGold')
        for side in (-1,1):rod(model,'WrenchJaw'+str(i)+str(side),(x,1.91,6.29),(x+side*.09,2.08,6.29),.027,'CopperGold')
    for side in (-1,1):
        points=[(side*6.33,2.95,2.2),(side*6.33,3.35,2.2),(side*6.33,3.35,5.85),(side*3.0,3.35,6.27),(side*1.0,2.90,6.27)]
        for i in range(len(points)-1):rod(model,'CoinPipe'+str(side)+str(i),points[i],points[i+1],.075,'CopperGold')
        for z in (2.7,3.8,4.9):torus(model,'PipeClamp'+str(side)+str(z),(side*6.33,3.35,z),.087,.014,'InkBlue',axis='Z')
    # Entry ticket booth and side details occupy the outer margin, not the central walk lane.
    box(model,'TicketBooth',(-4.85,.60,-3.60),(1.85,1.20,.75),'PlumRed',.13)
    box(model,'TicketCounter',(-4.85,1.235,-3.60),(2.0,.10,.92),'CopperGold',.045)
    arch(model,'TicketAwning',-4.85,-3.88,2.05,1.05,2.38,'CreamYellow')
    text(model,'TicketSign','最后一张好运券',(-4.85,2.19,-3.72),.18,'InkBlue',reverse=True)
    for i in range(4): box(model,'TicketStub'+str(i),(-5.36+i*.28,1.32,-3.26),(.19,.008,.31),'CreamYellow',.01)
    for x in (-5.55,5.55):
        bin_z=-2.1 if x<0 else -.4
        cylinder(model,'CopperBin'+str(x),(x,.44,bin_z),.30,.88,'InkBlue',vertices=16)
        torus(model,'BinRim'+str(x),(x,.86,bin_z),.30,.025,'CopperGold')
        for y in (1.5,2.6):
            box(model,'ServiceWindow'+str(x)+str(y),(x,y,6.39),(.76,.45,.07),'CopperGold',.035)
            box(model,'ServiceInset'+str(x)+str(y),(x,y,6.44),(.62,.31,.03),'PlumRed',.018)
    return model

models=[make_slots(BLUEPRINT['stations'][0]),make_cards(BLUEPRINT['stations'][1]),make_levers(BLUEPRINT['stations'][2]),make_hall()]
for model in models:
    for obj in model.collection.objects:
        if obj.type=='MESH':
            mesh=bmesh.new();mesh.from_mesh(obj.data);bmesh.ops.recalc_face_normals(mesh,faces=list(mesh.faces));mesh.to_mesh(obj.data);mesh.free()
entries=[export(model) for model in models]
manifest={'stage':'S1 immersion candidate','blender':bpy.app.version_string,'units':'metres','sourceFront':'-Y','sourceUp':'+Z','sourceRight':'-X',
    'unityFront':'+Z','unityUp':'+Y','unityRight':'+X','hierarchyBake':'G*sourceLocal*inverse(G)',
    'export':{'axisForward':'-Z','axisUp':'Y','applyUnitScale':True,'bakeSpaceTransform':True,'cameras':False,'lights':False,'animations':False},
    'palette':{name:values[0] for name,values in PALETTE.items()},'blueprint':BLUEPRINT,'models':entries,
    'legacyPipelineSha256':hashlib.sha256(LEGACY.read_bytes()).hexdigest(),
    'materialPhysical':{name:{'metallic':mat.node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value,
        'roughness':mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value} for name,mat in MATERIALS.items()}}
(MODELS/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'S1Models.blend'))
print('S1_SOURCE_AND_FBX_READY',flush=True)

# Render cameras/lights live in a separate non-exported collection. All sources keep their root identity.
studio=bpy.data.collections.new('PreviewStudio_NotExported');SCENE.collection.children.link(studio)
for name,point,power,size in [('Key',(-3,4,4),420,4),('Fill',(3,3,3),170,3),('Rim',(0,4,-3),500,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    data.color=(1,.78,.57) if name=='Key' else (.35,.56,1) if name=='Fill' else (.72,.84,1)
    obj=bpy.data.objects.new(name,data);studio.objects.link(obj);obj.location=S(point)
    obj.rotation_euler=(Vector(S((0,1.30,0)))-obj.location).to_track_quat('-Z','Y').to_euler()
camera_data=bpy.data.cameras.new('PreviewCamera');camera=bpy.data.objects.new('PreviewCamera',camera_data);studio.objects.link(camera);SCENE.camera=camera
SCENE.render.engine='CYCLES';SCENE.cycles.device='CPU';SCENE.cycles.samples=16;SCENE.cycles.use_denoising=True
SCENE.render.resolution_x=1280;SCENE.render.resolution_y=720;SCENE.render.resolution_percentage=100
SCENE.render.image_settings.file_format='PNG';SCENE.view_settings.view_transform='AgX'
if SCENE.world is None: SCENE.world=bpy.data.worlds.new('PreviewWorld')
SCENE.world.color=(.035,.055,.10)

def set_camera(point,look,fov):
    camera.location=S(point);camera.rotation_euler=(Vector(S(look))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.type='PERSP';camera_data.sensor_fit='VERTICAL';camera_data.sensor_height=24
    camera_data.lens=12/math.tan(math.radians(fov/2));camera_data.clip_start=.07

def render(name):
    SCENE.render.filepath=str(PREVIEWS/(name+'.png'));bpy.ops.render.render(write_still=True);print('S1_PREVIEW_READY '+name,flush=True)

for model,definition in zip(models[:3],BLUEPRINT['stations']):
    for other in models:
        for obj in other.collection.objects: obj.hide_render=other!=model
    if model.name=='S1Blackjack':
        # Staged example A+6 and a hidden dealer card only for DCC framing, not domain outcome.
        for obj in model.collection.objects:
            if '.PlayerCard' in obj.name or '.DealerCard' in obj.name or '.Rank' in obj.name: obj.hide_render=True
        for name in ('Blackjack.Rank1.Mesh','Blackjack.Rank6.Mesh','Blackjack.Rank7.Mesh','Blackjack.DealerCard0.Mesh'):
            if name in bpy.data.objects: bpy.data.objects[name].hide_render=False
        bpy.data.objects['Blackjack.DealerCard0'].location.x=.18
        bpy.data.objects['Blackjack.Rank7'].location=(-.20,.60,0)
        bpy.data.objects['Blackjack.Rank1'].location.x=.14
        bpy.data.objects['Blackjack.Rank6'].location.x=-.14
        for obj in model.collection.objects:
            if 'DrawCard' in obj.name:obj.hide_render=True
    if model.name=='S1Slots':
        for obj in model.collection.objects:
            if 'PayoutChip' in obj.name:obj.hide_render=True
    if model.name=='S1Levers':
        for obj in model.collection.objects:
            if 'HelpWindow' in obj.name or 'HelpWrench' in obj.name: obj.hide_render=True
    set_camera(definition['focus'],definition['lookAt'],definition['fieldOfView']);render(model.name+'Focus')
    if model.name=='S1Slots':
        for offset in (0,3):
            for index in range(3):bpy.data.objects['Slots.Reel'+str(index)].rotation_euler.x=(offset+index)*math.pi/3
            render('S1SlotsSymbols'+str(offset)+str(offset+1)+str(offset+2))
        for index in range(3):bpy.data.objects['Slots.Reel'+str(index)].rotation_euler.x=0

for model in models:
    for obj in model.collection.objects:obj.hide_render=False
for model,definition in zip(models[:3],BLUEPRINT['stations']):
    model.root.location=S(definition['position']);model.root.rotation_euler.z=-math.radians(definition['yaw'])
    if model.name=='S1Blackjack':
        for obj in model.collection.objects:
            if '.PlayerCard' in obj.name or '.DealerCard' in obj.name or '.Rank' in obj.name:obj.hide_render=True
        for name in ('Blackjack.Rank1.Mesh','Blackjack.Rank6.Mesh','Blackjack.Rank7.Mesh','Blackjack.DealerCard0.Mesh'):
            if name in bpy.data.objects:bpy.data.objects[name].hide_render=False
        for obj in model.collection.objects:
            if 'DrawCard' in obj.name:obj.hide_render=True
    if model.name=='S1Slots':
        for obj in model.collection.objects:
            if 'PayoutChip' in obj.name:obj.hide_render=True
    if model.name=='S1Levers':
        for obj in model.collection.objects:
            if 'HelpWindow' in obj.name or 'HelpWrench' in obj.name:obj.hide_render=True
set_camera((0,1.6,-4.6),(0,1.75,3.3),62);render('S1HallEntry')
# Preview-only companion is appended from the owned existing source, never exported or changed on disk.
with bpy.data.libraries.load(str(WORKSPACE/'ArtSource/jinx_casino/jinx_casino_models.blend'),link=False) as (available,loaded):
    loaded.collections=['Avatar'] if 'Avatar' in available.collections else []
for collection in loaded.collections:
    if collection is not None:
        SCENE.collection.children.link(collection)
        roots=[obj for obj in collection.objects if obj.parent is None and obj.type=='EMPTY']
        if roots:
            actor=roots[0];actor.location=S((5.02,0,1.12));actor.rotation_euler.z=math.radians(110)
        for obj in collection.objects:obj.hide_render=False
render('S1HallEntryWithCompanion')
set_camera((4.4,1.65,-2.1),(-2.6,1.55,.2),67);render('S1HallVendorLayer')
print('S1_BUILD_COMPLETE',flush=True)
