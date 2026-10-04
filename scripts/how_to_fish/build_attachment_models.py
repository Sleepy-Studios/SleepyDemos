"""Original accessory geometry; sizes are fitted to this demo's own weapon models."""
import math
import sys
from pathlib import Path
import bpy
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, mesh, tube, box, ellipsoid
from fbx_export import save_and_export


def sleeve(name, start, end, radius, bore, z, mat):
    vertices=[(math.cos(i*math.tau/12)*r,y,z+math.sin(i*math.tau/12)*r)
              for y in (start,end) for r in (radius,bore) for i in range(12)]
    faces=[]
    for i in range(12):
        j=(i+1)%12
        faces.extend([(i,j,j+12,i+12),(i+24,i+36,j+36,j+24),
                      (i,i+24,j+24,j),(i+12,j+12,j+36,i+36)])
    if end < start: faces=[tuple(reversed(face)) for face in faces]
    result=mesh(name,vertices,faces,mat)
    assert result.data.polygons[2].normal.x > 0, "The outer sleeve must face outwards."
    return result


def red_dot():
    metal=material("AttachmentMetal",(.12,.14,.15))
    red=material("SightReticle",(.95,.035,.02))
    box("RailClamp",(0,0,.008),(.060,.075,.016),metal)
    for side in (-1,1): box("SightPost",(side*.026,-.01,.040),(.009,.025,.06),metal)
    box("SightTop",(0,-.01,.070),(.061,.025,.009),metal)
    ellipsoid("RedDot",(0,-.025,.044),(.0015,.0015,.0015),red,1)
    empty("Aim",(0,0,.044))
    empty("Forward",(0,-.08,.044))
    empty("Grip")


def scope():
    metal=material("AttachmentMetal",(.12,.14,.15))
    rim=material("ScopeRing",(.23,.25,.26))
    ink=material("ScopeCrosshair",(.015,.016,.018))
    for y in (-.08,.06): box("ScopeFoot",(0,y,.021),(.04,.025,.042),metal)
    sleeve("ScopeTube",.10,-.18,.030,.027,.065,metal)
    sleeve("Eyepiece",.11,.075,.036,.026,.065,rim)
    sleeve("Objective",-.155,-.23,.043,.032,.065,rim)
    tube("ElevationDial",(0,-.02,.090),(0,-.02,.112),.020,.020,metal,10)
    tube("WindageDial",(-.025,-.02,.065),(-.043,-.02,.065),.018,.018,metal,10)
    tube("ReticleH",(-.032,-.221,.065),(.032,-.221,.065),.0007,.0007,ink,4)
    tube("ReticleV",(0,-.221,.033),(0,-.221,.097),.0007,.0007,ink,4)
    empty("Aim",(0,.10,.065))
    empty("Forward",(0,-.235,.065))
    empty("Grip")


def muzzle(suppressed):
    metal=material("AttachmentMetal",(.12,.14,.15))
    ring=material("MuzzleRing",(.24,.26,.28))
    length=.20 if suppressed else .07
    sleeve("SuppressorTube" if suppressed else "CompensatorBody",0,-length,.025,.010,0,metal)
    sleeve("ThreadedRing",.005,-.018,.027,.010,0,ring)
    if suppressed:
        for i in range(5): sleeve("CoolingRing"+str(i),-.032-i*.031,-.038-i*.031,.026,.024,0,ring)
    else:
        for side in (-1,1):
            for y in (-.028,-.049): box("GasVent",(side*.024,y,.004),(.002,.012,.012),ring)
    empty("Forward",(0,-length-.005,0))
    empty("Grip")


def laser():
    metal=material("AttachmentMetal",(.12,.14,.15))
    red=material("SightReticle",(.95,.035,.02))
    box("LaserClamp",(-.007,0,.008),(.040,.048,.017),metal)
    tube("LaserBody",(0,.022,-.015),(0,-.045,-.015),.012,.012,metal,10)
    tube("LaserLens",(0,-.045,-.015),(0,-.047,-.015),.008,.008,red,10)
    empty("Emitter",(0,-.05,-.015))
    empty("Forward",(0,-.055,-.015))
    empty("Grip")


def magazine():
    metal=material("AttachmentMetal",(.12,.14,.15))
    edge=material("MagazineExtensionRib",(.22,.24,.25))
    box("ExtensionBody",(0,0,-.05),(.050,.065,.10),metal)
    box("MagazineFloorPlate",(0,0,-.105),(.056,.074,.016),edge)
    for z in (-.03,-.06,-.09):
        for side in (-1,1): box("MagazineRib",(side*.026,0,z),(.003,.056,.007),edge)
    empty("Forward",(0,-.05,-.04))
    empty("Grip")


def crate():
    olive=material("AttachmentCrate",(.34,.42,.17))
    lining=material("AttachmentCrateLining",(.64,.66,.58))
    dark=material("AttachmentMetal",(.12,.14,.15))
    box("CrateBase",(0,0,.03),(.55,.42,.06),olive)
    for side in (-1,1):
        box("CrateSide",(side*.258,0,.15),(.034,.42,.30),olive)
        box("CrateEnd",(0,side*.193,.15),(.52,.034,.30),olive)
        for x in (-.17,.17): box("CrateRib",(x,side*.214,.15),(.038,.018,.29),olive)
    box("Lining",(0,0,.24),(.47,.34,.028),lining)
    box("OpenLid",(0,.215,.50),(.55,.045,.42),olive)
    box("LidLining",(0,.187,.50),(.47,.014,.34),lining)
    for x in (-.18,.18): tube("Hinge",(x-.04,.21,.29),(x+.04,.21,.29),.018,.018,dark,6)
    empty("Display",(0,-.015,.267))
    empty("Forward",(0,-.26,.15))
    empty("Grip")


if __name__ == "__main__":
    project=Path(sys.argv[sys.argv.index("--")+1]).resolve()
    assert (project/"ProjectSettings/ProjectVersion.txt").is_file()
    recipes=dict(RedDotSight=red_dot,SniperScope=scope,Compensator=lambda:muzzle(False),
                 Suppressor=lambda:muzzle(True),LaserSight=laser,ExtendedMag=magazine,AttachmentCrate=crate)
    for name in sys.argv[sys.argv.index("--")+2:] or recipes:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT=None
        art.ROOT=empty(name)
        recipes[name]()
        save_and_export(project,name)
