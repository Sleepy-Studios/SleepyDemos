"""Original dock module and inferred casino shell; candidate export only."""
import sys,math
from pathlib import Path
import bpy
from mathutils import Vector
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_environment_candidates as candidate
import build_first_island_models as art
from build_first_island_models import box,tube,mesh,empty,material

def join_part(name,fn):
    before=set(bpy.context.scene.objects);fn();objects=[o for o in bpy.context.scene.objects if o not in before and o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();bpy.context.object.name=name


def pier():
    wood=material('RocksWood',(.33,.24,.17));light=material('DockWeatheredWood',(.48,.39,.27));dark=material('DockEndGrain',(.22,.17,.13));iron=material('DockIron',(.13,.18,.20))
    def deck():
        for i in range(34):
            y=-5+i*.30
            box('DeckBoard'+str(i),(0,y,-.075),(3.8,.285,.15),light if i%4 else wood)
    join_part('DeckSurface',deck)
    def structure():
        for x in (-1.65,1.65):
            box('DeckStringer',(x,0,-.3),(.22,10.2,.4),wood)
            for y in (-4.8,-1.6,1.6,4.8):
                tube('WeatheredPile',(x,y,-2.2),(x,y,1.0),.14,.13,wood,7)
                tube('PileCap',(x,y,1.0),(x,y,1.10),.17,.17,dark,7)
                for z in (-.52,.65):tube('IronBand',(x,y,z),(x,y,z+.045),.147,.147,iron,8)
        for y in (-4.8,-1.6,1.6,4.8):
            tube('CrossBrace',(-1.65,y,-1.55),(1.65,y,-.32),.085,.085,dark,5)
            tube('CrossBrace',(1.65,y,-1.55),(-1.65,y,-.32),.085,.085,dark,5)
    join_part('PileStructure',structure)
    def rail():
        for x in (-1.65,1.65):
            for start,end in ((-4.8,-1.6),(-1.6,1.6),(1.6,4.8)):
                for segment in range(8):
                    t=segment/8;u=(segment+1)/8
                    tube('Rope',(x,start+(end-start)*t,.87-.19*math.sin(t*math.pi)),(x,start+(end-start)*u,.87-.19*math.sin(u*math.pi)),.025,.025,dark,5)
    join_part('RopeRails',rail)
    empty('EntryFront',(0,-5.14,0));empty('ExitBack',(0,5.14,0));empty('WalkwayCenter',(0,0,0));empty('Waterline',(0,0,-.8))


def sign_text(body,position,size,mat,name):
    bpy.ops.object.text_add(location=position,rotation=(math.pi/2,0,0))
    obj=bpy.context.object;obj.name=name;obj.parent=art.ROOT;obj.data.body=body;obj.data.align_x='CENTER';obj.data.size=size;obj.data.extrude=.014;obj.data.materials.append(mat)
    bpy.ops.object.convert(target='MESH')


def casino():
    red=material('RocksRedSiding',(.43,.09,.075));wood=material('RocksWood',(.33,.24,.17));trim=material('RocksWindowTrim',(.78,.70,.49));roof=material('RocksRoofTile',(.36,.20,.105));dark=material('CasinoRoofShadow',(.18,.13,.12));light=material('CasinoSignWarm',(.95,.60,.18))
    shader=next(n for n in light.node_tree.nodes if n.type=='BSDF_PRINCIPLED');shader.inputs['Emission Color'].default_value=(1,.42,.08,1);shader.inputs['Emission Strength'].default_value=.7
    def floor():
        for i in range(47):box('FloorBoard'+str(i),(-6.9+i*.3,0,-.07),(.287,9.0,.14),wood if i%5 else trim)
    join_part('InteriorFloor',floor)
    for side in (-1,1):
        def sidewall(side=side):
            for i in range(30):
                y=-4.35+i*.3
                if abs(y)<1.55:
                    box('LowerSiding',(side*7,y,.53),(.17,.287,1.06),red)
                    box('UpperSiding',(side*7,y,3.05),(.17,.287,1.1),red)
                else:box('Siding',(side*7,y,1.8),(.17,.287,3.6),red)
        join_part('SideWallLeft' if side<0 else 'SideWallRight',sidewall)
        def frontwall(side=side):
            for i in range(18):box('FrontSiding',(side*(1.7+i*.3),-4.5,1.8),(.287,.17,3.6),red)
        join_part('FrontWallLeft' if side<0 else 'FrontWallRight',frontwall)
    def rear():
        for i in range(47):box('BackSiding',(-6.9+i*.3,4.5,1.8),(.287,.17,3.6),red)
    join_part('BackWall',rear)
    def trimwork():
        for x in (-7,7):
            for y in (-4.5,4.5):box('CornerPost',(x,y,1.8),(.24,.24,3.65),trim)
            for z in (1.06,2.5):box('WindowRail',(x,0,z),(.23,3.25,.12),trim)
            for y in (-1.58,0,1.58):box('WindowMullion',(x,y,1.78),(.22,.11,1.55),trim)
        for x in (-1.53,1.53):box('DoorPost',(x,-4.6,1.44),(.16,.28,2.88),trim)
        box('DoorHeader',(0,-4.6,2.96),(3.23,.28,.18),trim)
        box('Transom',(0,-4.5,3.30),(3.1,.17,.6),red)
    join_part('TrimAndWindowFrames',trimwork)
    for side in (-1,1):
        def tiles(side=side):
            for i in range(20):
                y=-4.8+i*.49
                points=[(0,y,5.35),(side*7.45,y,3.6),(side*7.45,y+.475,3.6),(0,y+.475,5.35),(0,y,5.23),(side*7.45,y,3.48),(side*7.45,y+.475,3.48),(0,y+.475,5.23)]
                faces=[(0,1,2,3),(7,6,5,4),(0,4,5,1),(3,2,6,7),(1,5,6,2),(0,3,7,4)]
                mesh('RoofTile',points,[tuple(reversed(f)) for f in faces] if side<0 else faces,roof)
        join_part('RoofLeft' if side<0 else 'RoofRight',tiles)
    def gables():
        for y in (-4.52,4.52):
            mesh('Gable',[(-7,y,3.6),(7,y,3.6),(0,y,5.3),(-7,y+.15,3.6),(7,y+.15,3.6),(0,y+.15,5.3)],[(0,1,2),(3,5,4),(3,4,1,0),(4,5,2,1),(5,3,0,2)],red)
    join_part('GableWalls',gables)
    def porch():
        for i in range(47):box('PorchBoard',(-6.9+i*.3,-5.47,-.07),(.287,1.8,.14),wood)
        for x in (-6.5,-3.8,3.8,6.5):
            box('PorchPost',(x,-6.15,1.3),(.17,.17,2.6),wood)
            tube('PorchBrace',(x,-6.15,2.0),(x+.5,-6.15,2.6),.08,.08,trim,4)
        box('PorchCanopy',(0,-5.4,2.72),(14.5,2.15,.15),dark)
    join_part('Porch',porch)
    def marquee():
        box('MarqueeBoard',(0,-4.86,4.18),(7.2,.25,1.34),dark)
        for x in (-3.7,3.7):box('MarqueeSide',(x,-4.98,4.18),(.12,.12,1.5),trim)
        for z in (3.43,4.93):box('MarqueeEdge',(0,-4.98,z),(7.52,.12,.12),trim)
        sign_text('LUCKY BAIT',(0,-5.015,4.22),.53,light,'CasinoName')
        sign_text('CASINO',(0,-5.02,3.66),.55,light,'CasinoLabel')
        for i in range(17):
            for z in (3.48,4.89):
                tube('Bulb',(-3.56+i*.445,-5.06,z),(-3.56+i*.445,-5.10,z),.034,.034,light,6)
    join_part('Marquee',marquee)
    def foundation():
        # Supports cover the existing plateau edge; floor and functional stations remain at the original height.
        for x in (-6.6,-3.3,0,3.3,6.6):
            for y in (-6.1,4.1):
                tube('FoundationPile',(x,y,-1.4),(x,y,-.12),.14,.14,wood,6)
        box('FoundationFrontBeam',(0,-6.1,-.23),(13.5,.22,.26),wood)
        box('FoundationBackBeam',(0,4.1,-.23),(13.5,.22,.26),wood)
    join_part('FoundationPiles',foundation)
    empty('DoorFront',(0,-6.45,0));empty('DoorThreshold',(0,-4.5,0));empty('InteriorCenter',(0,0,0));empty('RoofPeak',(0,0,5.35))

requested=[arg for arg in sys.argv[sys.argv.index('--')+2:] if arg != '--refresh-candidates']
for name,fn in [('RocksPierModule',pier),('LuckyBaitCasinoShell',casino)]:
    if not requested or name in requested:candidate.build(name,fn,combine=False)
