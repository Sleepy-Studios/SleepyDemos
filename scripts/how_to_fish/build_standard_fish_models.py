"""Five missing standard-lure species; native Blender sources and shared FBX contract."""
import math
import sys
from pathlib import Path
import bpy
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, ellipsoid, tube
from build_rocks_fish_models import fin, eyes, tail
from fbx_export import save_and_export


def angelfish():
    silver = material("AngelfishSilver", (.70, .72, .63))
    ink = material("StandardFishInk", (.035, .044, .046))
    gold = material("AngelfishGold", (.79, .62, .26))
    ellipsoid("Body", (0, 0, .40), (.075, .28, .24), silver, 2)
    for side in (-1, 1):
        for i, y in enumerate((-.16, -.025, .13)):
            points = []
            for step in range(9):
                z = .22 + step * .043
                for dy in (-.015, .015):
                    x = .075 * math.sqrt(max(0, 1-((y+dy)/.28)**2-((z-.40)/.24)**2))
                    points.append((side*(x+.002), y+dy, z))
            faces = [(j*2,j*2+1,j*2+3,j*2+2) for j in range(8)]
            mesh(f"Stripe{side}_{i}", points, faces+list(tuple(reversed(f)) for f in faces), ink)
        tube("VentralThread" + str(side), (side * .035, -.14, .27), (side * .042, .16, .015), .007, .002, gold, 5)
        fin("Pectoral" + str(side), [(side * .065, -.10, .4), (side * .20, .08, .35), (side * .065, .09, .38)], silver)
    fin("TallDorsal", [(0, -.13, .55), (0, .14, .91), (0, .19, .52)], gold)
    fin("TallAnal", [(0, -.08, .23), (0, .18, .04), (0, .20, .29)], gold)
    tail(.25, .40, .15, silver)
    eyes(.06, -.20, .46, .023, ink, gold)
    empty("Grip")
    empty("Hook", (0, -.31, .4))


def catfish():
    gray = material("CatfishSkin", (.22, .26, .23))
    cream = material("CatfishBelly", (.63, .61, .46))
    ink = material("StandardFishInk", (.035, .044, .046))
    ellipsoid("Body", (0, .07, .19), (.17, .48, .17), gray, 2)
    ellipsoid("Head", (0, -.34, .17), (.21, .22, .12), gray, 2)
    ellipsoid("Belly", (0, -.02, .09), (.145, .40, .06), cream, 1)
    eyes(.17, -.42, .24, .022, ink, cream)
    tube("Mouth", (-.13, -.529, .14), (.13, -.529, .14), .01, .01, ink, 5)
    for side in (-1, 1):
        for i in range(3):
            start = (side * (.09 + i * .04), -.51 + i * .025, .13)
            bend = (side * (.25 + i * .025), -.55 + i * .07, .11)
            end = (side * (.34 + i * .025), -.43 + i * .11, .08)
            tube(f"BarbelBase{side}_{i}", start, bend, .007, .005, cream, 5)
            tube(f"BarbelTip{side}_{i}", bend, end, .005, .001, cream, 5)
        fin("Pectoral" + str(side), [(side * .15, -.2, .17), (side * .39, .02, .06), (side * .12, .17, .09)], gray)
    fin("Dorsal", [(0, -.13, .31), (0, .04, .50), (0, .16, .32)], gray)
    fin("Adipose", [(0, .25, .29), (0, .38, .34), (0, .44, .24)], gray)
    tail(.48, .18, .18, gray)
    empty("Grip")
    empty("Hook", (0, -.58, .15))


def sea_urchin():
    shell = material("UrchinShell", (.16, .075, .20))
    tip = material("UrchinSpines", (.075, .038, .10))
    ellipsoid("Body", (0, 0, .16), (.15, .15, .12), shell, 2)
    for i in range(90):
        z = -1 + 2 * (i + .5) / 90
        angle = i * math.pi * (3 - math.sqrt(5))
        x, y = math.sqrt(1 - z*z) * math.cos(angle), math.sqrt(1-z*z) * math.sin(angle)
        start = (x * .14, y * .14, .16 + z * .11)
        reach = .24 + .035 * math.sin(i * 2.4)
        end = (x * reach, y * reach, .16 + z * .15)
        tube("Spine" + str(i), start, end, .009, .001, tip, 5)
    empty("Grip")
    empty("Hook", (0, -.29, .16))


def clownfish():
    orange = material("ClownfishOrange", (.90, .24, .035))
    white = material("ClownfishWhite", (.87, .86, .77))
    ink = material("StandardFishInk", (.035, .044, .046))
    # Ring topology makes the three curved white bands part of the body surface.
    rings = [(-.30, .02, .04), (-.23, .060, .10), (-.19, .072, .12), (-.15, .08, .135),
             (-.10, .087, .14), (-.04, .09, .14), (.025, .083, .13), (.10, .067, .105),
             (.16, .046, .076), (.20, .031, .045), (.24, .018, .03)]
    verts = [(math.cos(i * math.tau/12)*w, y, .17 + math.sin(i * math.tau/12)*h) for y,w,h in rings for i in range(12)]
    faces = [tuple(reversed(range(12))), tuple(range(120,132))]
    for r in range(10):
        for i in range(12): faces.append((r*12+i, r*12+(i+1)%12, (r+1)*12+(i+1)%12, (r+1)*12+i))
    body = mesh("Body", verts, faces, orange)
    body.data.materials.append(white)
    for poly in body.data.polygons:
        if poly.index >= 2 and (poly.index-2)//12 in (2,5,8): poly.material_index = 1
    eyes(.056, -.234, .211, .020, ink, orange)
    for side in (-1,1):
        fin("Pectoral"+str(side), [(side*.075,-.12,.18),(side*.19,.03,.10),(side*.06,.08,.15)], orange)
    fin("Dorsal", [(0,-.13,.27),(0,-.08,.36),(0,.15,.29),(0,.19,.22)], ink)
    pivot = empty("Tail", (0,.23,.17))
    fin("TailBorder", [(0,0,0),(0,.13,.10),(0,.16,0),(0,.13,-.10)], ink, pivot)
    fin("TailColor", [(.001,.01,0),(.001,.105,.076),(.001,.125,0),(.001,.105,-.076)], orange, pivot)
    empty("Grip")
    empty("Hook", (0,-.33,.17))


def bluegill():
    green = material("BluegillSkin", (.25,.37,.22))
    orange = material("BluegillBreast", (.72,.36,.10))
    blue = material("BluegillCheek", (.18,.35,.46))
    ink = material("StandardFishInk", (.035,.044,.046))
    ellipsoid("Body", (0,0,.26), (.10,.31,.24), green, 2)
    ellipsoid("Breast", (0,-.08,.15), (.083,.23,.12), orange, 2)
    for side in (-1,1):
        ellipsoid("GillCover"+str(side), (side*.077,-.15,.28), (.022,.095,.097), blue, 1)
        ellipsoid("BlackEar"+str(side), (side*.100,-.073,.315), (.010,.038,.034), ink, 1)
        fin("Pectoral"+str(side), [(side*.08,-.05,.25),(side*.23,.14,.17),(side*.08,.14,.24)], blue)
    eyes(.057,-.235,.32,.023,ink,orange)
    points=[(0,-.16,.40)]+[(0,-.13+i*.045,.60 if i%2==0 else .49) for i in range(8)]+[(0,.23,.40)]
    fin("DorsalSpines", points, blue)
    fin("Anal", [(0,.01,.08),(0,.16,.015),(0,.24,.16)], blue)
    tail(.28,.26,.17,blue)
    empty("Grip")
    empty("Hook", (0,-.35,.26))


if __name__ == "__main__":
    project = Path(sys.argv[sys.argv.index("--")+1]).resolve()
    assert (project/"ProjectSettings/ProjectVersion.txt").is_file()
    recipes = dict(Angelfish=angelfish,Catfish=catfish,SeaUrchin=sea_urchin,Clownfish=clownfish,Bluegill=bluegill)
    for name in sys.argv[sys.argv.index("--")+2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT = None
        art.ROOT = empty(name)
        recipes[name]()
        save_and_export(project,name)
