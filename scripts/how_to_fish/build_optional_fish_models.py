"""Original optional encounters based on public silhouette references, no copied meshes."""
import math
import sys
from pathlib import Path
import bpy
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, ellipsoid, tube
from build_rocks_fish_models import fin, eyes
from fbx_export import save_and_export


def sunfish():
    gray = material("SunfishSilver", (.59,.61,.58))
    dark = material("SunfishEdge", (.35,.37,.34))
    ivory = material("OptionalFishIvory", (.83,.82,.72))
    ink = material("OptionalFishInk", (.035,.04,.037))
    ellipsoid("Body", (0,0,1.7), (.32,.88,1.0), gray, 3)
    for side in (-1,1):
        ellipsoid("Pectoral"+str(side), (side*.32,-.17,1.79), (.13,.20,.10), dark, 1)
    eyes(.21,-.69,2.08,.062,ink,ivory)
    ellipsoid("Mouth", (0,-.88,1.91), (.07,.035,.07), ink, 1)
    pivot = empty("Tail", (0,.6,1.7))
    fin("Clavus", [(0,0,-.65),(0,.29,-.5),(0,.32,.45),(0,.12,.72),(0,-.02,.5)], dark, pivot)
    for side in (-1,1):
        # Tall dorsal/anal fins, with volume so they remain readable head-on.
        verts=[(-.055,.1,1.7+side*.76),(.055,.1,1.7+side*.76),
               (0,.48,1.7+side*1.68),(0,.70,1.7+side*1.42),
               (-.045,.55,1.7+side*.58),(.045,.55,1.7+side*.58)]
        mesh("LongFin"+str(side),verts,[(0,1,2),(1,5,3,2),(5,4,3),(4,0,2,3),(0,4,5,1)],gray)
    empty("Grip")
    empty("Hook",(0,-.96,1.91))


def long_fish(goblin):
    name = "GoblinShark" if goblin else "OldPike"
    skin = material(name+"Skin", (.67,.42,.43) if goblin else (.33,.35,.22))
    pale = material(name+"Belly", (.83,.62,.60) if goblin else (.67,.64,.40))
    dark = material("OptionalFishInk", (.035,.04,.037))
    ivory = material("OptionalFishIvory", (.83,.82,.72))
    rings = [(-1.15,.11,.14),(-.8,.27,.29),(-.1,.33,.34),(.65,.22,.23),(1.25,.09,.11),(1.6,.04,.06)]
    z = .50
    verts=[(math.cos(i*math.tau/12)*w,y,z+math.sin(i*math.tau/12)*h) for y,w,h in rings for i in range(12)]
    faces=[tuple(reversed(range(12))),tuple(range(60,72))]
    for r in range(5):
        for i in range(12):faces.append((r*12+i,r*12+(i+1)%12,(r+1)*12+(i+1)%12,(r+1)*12+i))
    body=mesh("Body",verts,faces,skin)
    body.data.materials.append(pale)
    for polygon in body.data.polygons:
        if polygon.center.z < z-.08: polygon.material_index=1
    eyes(.14,-.94,.65,.048,dark,ivory)
    ellipsoid("LowerJaw",(0,-1.13,.36),(.14,.35,.08),pale,1)
    if goblin:
        mesh("LongSnout",[(-.10,-1.0,.63),(.10,-1.0,.63),(0,-2.12,.67),
             (-.075,-1.03,.52),(.075,-1.03,.52),(0,-2.12,.63)],
             [(0,2,1),(3,4,5),(0,3,5,2),(1,2,5,4),(0,1,4,3)],skin)
        ellipsoid("Gape",(0,-1.14,.445),(.12,.24,.047),dark,1)
        for side in (-1,1):
            for i in range(8):
                y=-.95-i*.065
                tube(f"Tooth{side}_{i}",(side*.10,y,.45),(side*.086,y-.012,.39),.015,0,ivory,4)
            for i in range(5):
                tube(f"GillSlit{side}_{i}",(side*.268,-.71+i*.055,.65),(side*.25,-.74+i*.055,.38),.009,.006,dark,4)
        fin("Dorsal",[(0,-.07,.80),(0,.23,1.3),(0,.49,.73)],skin)
        fin("RearDorsal",[(0,.8,.68),(0,1.08,.9),(0,1.21,.61)],skin)
    else:
        ellipsoid("UpperBill",(0,-1.18,.50),(.15,.38,.068),skin,1)
        scar=material("OldPikeScar",(.60,.18,.17))
        for side in (-1,1):
            for i in range(3):
                tube(f"Scar{side}_{i}",(side*.265,-.69+i*.14,.65),(side*.26,-.73+i*.14,.40),.013,.009,scar,4)
        fin("RearDorsal",[(0,.68,.70),(0,.88,1.07),(0,1.22,.65)],pale)
    for side in (-1,1):
        fin("Pectoral"+str(side),[(side*.25,-.57,.47),(side*(.85 if goblin else .49),-.07,.15),(side*.19,.14,.31)],pale)
        fin("Pelvic"+str(side),[(side*.16,.61,.35),(side*.42,.98,.14),(side*.08,1.07,.35)],pale)
    pivot=empty("Tail",(0,1.51,z))
    points=[(0,0,0),(0,.45,.62),(0,.59,.65),(0,.25,.07),(0,.43,-.27),(0,.29,-.30)] if goblin else [(0,0,0),(0,.54,.35),(0,.32,0),(0,.54,-.35)]
    fin("TailFin",points,skin if goblin else pale,pivot)
    empty("Grip")
    empty("Hook",(0,-1.58,.40))


def boss_lure(scientific):
    skin=material("BossLureDark",(.12,.16,.18))
    band=material("BossLureCyan" if scientific else "BossLureGold",(.08,.61,.67) if scientific else (.85,.59,.11))
    metal=material("BossLureSteel",(.52,.55,.53))
    tube("LureBody",(0,-.16,.07),(0,.1,.07),.055,.041,skin,8)
    for i in range(3):
        y=-.11+i*.065
        tube("Ring"+str(i),(0,y,.07),(0,y+.018,.07),.057,.054,band,8)
    for side in (-1,1):
        fin("Vane"+str(side),[(side*.03,.03,.08),(side*.095,.12,.08),(side*.02,.10,.11)],band)
        tube("Hook"+str(side),(0,.11,.04),(side*.05,.19,.03),.006,.005,metal,5)
        tube("HookTip"+str(side),(side*.05,.19,.03),(side*.055,.135,.03),.005,0,metal,5)
    empty("Grip")
    empty("Forward",(0,-.19,.07))


if __name__=="__main__":
    project=Path(sys.argv[sys.argv.index("--")+1]).resolve()
    assert (project/"ProjectSettings/ProjectVersion.txt").is_file()
    recipes=dict(Sunfish=sunfish,OldPike=lambda:long_fish(False),GoblinShark=lambda:long_fish(True),
                 BeginnerBossLure=lambda:boss_lure(False),ScientificBossLure=lambda:boss_lure(True))
    for name in sys.argv[sys.argv.index("--")+2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT=None
        art.ROOT=empty(name)
        recipes[name]()
        save_and_export(project,name)
