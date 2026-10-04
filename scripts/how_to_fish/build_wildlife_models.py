"""Self-authored wildlife, coconut bait and the local player's recoverable remains."""
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, ellipsoid, tube, box
from build_rocks_fish_models import fin, eyes
from fbx_export import save_and_export


def bing_bong():
    green=material("BingLime",(.60,.77,.10))
    dark_green=material("BingBelly",(.29,.43,.07))
    white=material("BingEyes",(.91,.94,.82))
    dark=material("WildlifeInk",(.025,.029,.025))
    orange=material("BingBeak",(.94,.60,.05))
    red=material("BingHatRed",(.86,.08,.10))
    blue=material("BingHatBlue",(.08,.17,.69))
    ellipsoid("Body",(0,0,.59),(.27,.23,.245),green,2)
    ellipsoid("Belly",(0,-.01,.49),(.25,.20,.13),dark_green,1)
    for side in (-1,1):
        ellipsoid("Eye"+str(side),(side*.18,-.18,.68),(.13,.095,.13),white,1)
        ellipsoid("Pupil"+str(side),(side*.20,-.259,.69),(.051,.025,.057),dark,1)
        hip=(side*.11,.025,.41)
        leg=empty("LegLeft" if side<0 else "LegRight",hip)
        knee=Vector((side*.145,-.02,.21))-Vector(hip)
        ankle=Vector((side*.13,-.11,.055))-Vector(hip)
        tube("Thigh"+str(side),(0,0,0),knee,.035,.023,green,6,leg)
        tube("Shin"+str(side),knee,ankle,.022,.018,green,5,leg)
        ellipsoid("Foot"+str(side),ankle+Vector((0,-.035,-.015)),(.045,.075,.035),green,1,leg)
        shoulder=(side*.235,0,.68)
        arm=empty("ArmLeft" if side<0 else "ArmRight",shoulder)
        elbow=Vector((side*.36,-.01,.91 if side<0 else .44))-Vector(shoulder)
        hand=Vector((side*.48,-.03,.95 if side<0 else .30))-Vector(shoulder)
        tube("UpperArm"+str(side),(0,0,0),elbow,.028,.02,green,5,arm)
        tube("Forearm"+str(side),elbow,hand,.021,.016,green,5,arm)
        ellipsoid("Palm"+str(side),hand,(.038,.035,.025),green,1,arm)
        for i in range(3):
            tip=hand+Vector((side*(.055+i*.012),(i-1)*.026,-.016))
            tube(f"Finger{side}_{i}",hand,tip,.012,.008,green,5,arm)
    fin("UpperBeak",[(-.07,-.224,.595),(0,-.355,.57),(.07,-.224,.595)],orange)
    fin("LowerBeak",[(-.055,-.23,.56),(.055,-.23,.56),(0,-.325,.52)],orange)
    tube("Hat",(0,.04,.80),(0,.04,.90),.11,.055,red,8)
    box("HatBluePanel",(.045,.04,.857),(.055,.105,.055),blue)
    propeller=empty("Propeller",(0,.04,.925))
    tube("PropellerAxle",(0,0,-.025),(0,0,.018),.01,.008,dark,5,propeller)
    for side in (-1,1):
        fin("PropellerBlade"+str(side),[(0,0,0),(side*.14,-.028,.012),(side*.18,.015,.012),(side*.04,.035,0)],dark,propeller)
    empty("Grip")
    empty("Hook",(0,-.37,.565))


def seagull():
    white=material("SeagullWhite",(.87,.89,.84))
    gray=material("SeagullWingGray",(.64,.68,.66))
    dark=material("WildlifeInk",(.025,.029,.025))
    yellow=material("SeagullYellow",(.91,.67,.05))
    ellipsoid("Body",(0,.06,.31),(.12,.31,.17),white,2)
    tube("Neck",(0,-.13,.38),(0,-.24,.68),.087,.055,white,7)
    ellipsoid("Head",(0,-.26,.73),(.085,.11,.095),white,1)
    tube("Beak",(0,-.35,.73),(0,-.53,.72),.035,.002,yellow,5)
    eyes(.071,-.30,.764,.015,dark,white)
    for side in (-1,1):
        wing=empty("WingLeft" if side<0 else "WingRight",(side*.09,.015,.40))
        vertices=[(0,-.15,0),(side*.28,-.05,.045),(side*.62,.14,.01),(side*.70,.31,-.025),
                  (side*.46,.34,-.04),(side*.20,.23,-.045),(0,.20,-.025)]
        fin("WingSurface"+str(side),vertices,gray,wing)
        fin("WingTip"+str(side),[(side*.45,.13,.014),(side*.62,.14,.011),(side*.70,.31,-.025),(side*.46,.34,-.039)],dark,wing)
        hip=(side*.055,.035,.22)
        knee=(side*.06,.06,.11)
        ankle=(side*.06,-.015,.035)
        tube("Leg"+str(side),hip,knee,.012,.010,yellow,5)
        tube("Shin"+str(side),knee,ankle,.01,.008,yellow,5)
        fin("WebFoot"+str(side),[ankle,(side*.06-.035,-.13,.017),(side*.06+.035,-.13,.017)],yellow)
    fin("Tail",[(-.07,.28,.32),(-.105,.56,.30),(.105,.56,.30),(.07,.28,.32)],white)
    empty("CarryPoint",(0,-.15,-.20))
    empty("Grip")
    empty("Hook",(0,-.55,.72))


def coconut():
    brown=material("CoconutHusk",(.29,.16,.065))
    fiber=material("CoconutFiber",(.49,.30,.13))
    dark=material("WildlifeInk",(.025,.029,.025))
    ellipsoid("Body",(0,0,.18),(.16,.18,.18),brown,2)
    for i in range(12):
        angle=i*math.tau/12
        points=[(math.cos(angle)*.15*math.sin(j*math.pi/8),math.sin(angle)*.17*math.sin(j*math.pi/8),.18+math.cos(j*math.pi/8)*.175) for j in range(1,8)]
        for j,(a,b) in enumerate(zip(points,points[1:])):tube(f"Fiber{i}_{j}",a,b,.002,.002,fiber,4)
    for x,y in ((-.035,-.04),(.035,-.04),(0,.018)):
        ellipsoid("Eye",(x,y,.350),(.013,.018,.006),dark,1)
    empty("Grip")
    empty("Forward",(0,-.20,.18))


def player_remains():
    coat=material("PlayerJacketDark",(.045,.049,.055))
    trousers=material("PlayerTrousers",(.11,.13,.15))
    skin=material("PlayerSkin",(.76,.54,.32))
    white=material("PlayerEye",(.89,.86,.73))
    dark=material("WildlifeInk",(.025,.029,.025))
    tube("Body",(0,0,.88),(0,0,1.42),.13,.17,coat,7)
    tube("Neck",(0,0,1.42),(0,0,1.59),.046,.045,skin,6)
    ellipsoid("Head",(0,0,1.69),(.115,.11,.17),skin,1)
    ellipsoid("Hair",(0,.03,1.79),(.118,.10,.085),coat,1)
    for side in (-1,1):
        tube("Leg"+str(side),(side*.08,0,.92),(side*.14,-.03,.10),.064,.038,trousers,6)
        ellipsoid("Boot"+str(side),(side*.14,-.08,.065),(.060,.13,.056),dark,1)
        tube("Sleeve"+str(side),(side*.14,0,1.40),(side*.27,-.02,.85),.059,.036,coat,6)
        ellipsoid("Hand"+str(side),(side*.27,-.03,.79),(.037,.035,.073),skin,1)
        tube("ClosedEye"+str(side),(side*.055-.023,-.104,1.72),(side*.055+.023,-.104,1.72),.007,.007,dark,4)
    empty("Grip")
    empty("Forward",(0,-.2,1.7))


if __name__=="__main__":
    project=Path(sys.argv[sys.argv.index("--")+1]).resolve()
    assert (project/"ProjectSettings/ProjectVersion.txt").is_file()
    recipes=dict(BingBong=bing_bong,Seagull=seagull,Coconut=coconut,PlayerRemains=player_remains)
    for name in sys.argv[sys.argv.index("--")+2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT=None
        art.ROOT=empty(name)
        recipes[name]()
        save_and_export(project,name)
