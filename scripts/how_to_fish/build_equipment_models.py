"""Self-authored melee and upgrade-shop models in the shared metre/axis contract."""
import math
import sys
from pathlib import Path
import bpy
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, tube, box, ellipsoid
from fbx_export import save_and_export


def knuckles():
    brass = material("KnuckleBrass", (.72, .49, .13))
    edge = material("KnuckleEdges", (.95, .73, .27))
    for finger in range(4):
        center = (finger - 1.5) * .035
        vertices = [(center + math.cos(i*math.tau/8)*radius, y, .02 + math.sin(i*math.tau/8)*radius)
                    for y in (-.064, -.050) for radius in (.019, .013) for i in range(8)]
        faces = []
        for i in range(8):
            j=(i+1)%8
            faces.extend([(i,j,j+8,i+8),(i+16,i+24,j+24,j+16),
                          (i,i+16,j+16,j),(i+8,j+8,j+24,i+24)])
        mesh("FingerRing"+str(finger), vertices, faces, edge)
    for side in (-1, 1):
        tube("PalmBridge"+str(side), (side*.065,-.057,.009), (side*.050,-.057,-.026), .009,.009,brass,6)
    tube("PalmBrace",(-.050,-.057,-.026),(.050,-.057,-.026),.011,.011,brass,6)
    empty("Grip")
    empty("Strike", (0,-.080,.025))


def anvil():
    wood=material("UpgradeStump",(.25,.15,.08))
    iron=material("UpgradeIron",(.19,.21,.23))
    edge=material("UpgradeSteel",(.43,.46,.48))
    tube("Stump",(0,0,0),(0,0,.58),.27,.22,wood,9)
    for z in (.08,.48):
        tube("IronBand",(0,0,z),(0,0,z+.04),.276 if z<.2 else .239,.274 if z<.2 else .236,iron,9)
    box("AnvilBase",(0,0,.62),(.48,.31,.08),iron)
    box("AnvilWaist",(.02,0,.71),(.24,.20,.14),iron)
    box("AnvilFace",(.06,0,.815),(.46,.24,.08),edge)
    tube("AnvilHorn",(-.16,0,.815),(-.46,0,.815),.10,.015,iron,6)
    box("Heel",(.30,0,.807),(.14,.20,.065),iron)
    tube("HammerHandle",(.06,-.12,.86),(.22,-.12,.87),.019,.016,wood,6)
    box("HammerHead",(.035,-.12,.88),(.09,.065,.065),edge)
    empty("Grip")
    empty("Forward",(0,-.32,.80))


def backpack():
    canvas=material("BackpackCanvas",(.35,.40,.20))
    trim=material("BackpackStraps",(.17,.19,.10))
    buckle=material("BackpackBuckles",(.60,.54,.34))
    ellipsoid("BackpackBody",(0,0,.26),(.20,.11,.25),canvas,2)
    box("FrontPocket",(0,-.113,.19),(.31,.06,.18),canvas)
    box("PocketFlap",(0,-.15,.28),(.33,.022,.035),trim)
    for side in (-1,1):
        box("FrontStrap",(side*.105,-.15,.39),(.032,.022,.18),trim)
        box("Buckle",(side*.105,-.17,.315),(.045,.025,.04),buckle)
        tube("ShoulderTop",(side*.11,.065,.44),(side*.18,.15,.36),.024,.024,trim,5)
        tube("ShoulderLoop",(side*.18,.15,.36),(side*.13,.13,.08),.024,.018,trim,5)
        box("SidePocket",(side*.20,0,.20),(.055,.15,.17),canvas)
    tube("Handle",(-.06,0,.47),(.06,0,.47),.016,.016,trim,6)
    empty("Grip")
    empty("Forward",(0,-.20,.28))


def ammo_upgrade():
    wood=material("AmmoBenchWood",(.30,.18,.10))
    olive=material("AmmoBoxOlive",(.24,.28,.12))
    dark=material("AmmoBoxLatch",(.08,.09,.07))
    brass=material("AmmoCases",(.72,.49,.13))
    copper=material("AmmoTips",(.55,.26,.12))
    box("BenchTop",(0,0,.77),(.76,.50,.07),wood)
    for x in (-.29,.29):
        for y in (-.18,.18): box("BenchLeg",(x,y,.36),(.065,.065,.72),wood)
    box("AmmoCrate",(0,.02,.95),(.48,.31,.27),olive)
    box("AmmoLid",(0,.02,1.095),(.50,.33,.035),olive)
    for side in (-1,1): box("CrateLatch",(side*.17,-.147,1.02),(.035,.025,.11),dark)
    for i in range(5):
        x=(i-2)*.053
        tube("Cartridge"+str(i),(x,-.198,.81),(x,-.198,.90),.016,.016,brass,7)
        tube("Bullet"+str(i),(x,-.198,.90),(x,-.198,.94),.016,.002,copper,7)
    empty("Grip")
    empty("Forward",(0,-.3,1))


if __name__ == "__main__":
    project=Path(sys.argv[sys.argv.index("--")+1]).resolve()
    assert (project/"ProjectSettings/ProjectVersion.txt").is_file()
    recipes=dict(BrassKnuckles=knuckles,UpgradeAnvil=anvil,AmmoUpgrade=ammo_upgrade,Backpack=backpack)
    for name in sys.argv[sys.argv.index("--")+2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT=None
        art.ROOT=empty(name)
        recipes[name]()
        save_and_export(project,name)
