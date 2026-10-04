"""Self-authored motor silhouettes; mount dimensions match the existing boat."""
import math
import sys
from pathlib import Path
import bpy
from mathutils import Matrix
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import material, empty, tube, box, ellipsoid
from fbx_export import save_and_export


def motor(medium=False, x=0):
    steel = material("MotorSteel", (.34, .38, .41))
    hood = material("MotorHood", (.30, .39, .49))
    dark = material("MotorRubber", (.07, .08, .09))
    if medium:
        ellipsoid("EngineCover", (x, .03, .27), (.22, .30, .24), hood, 2)
        box("CoverBand", (x, .03, .15), (.37, .49, .06), dark)
    else:
        box("EngineCover", (x, 0, .23), (.29, .36, .26), steel)
        box("EngineTop", (x, 0, .37), (.30, .37, .045), material("MotorTop", (.64, .65, .63)))
    box("TransomClamp", (x, -.18, .03), (.22, .20, .15), steel)
    tube("Shaft", (x, .04, .13), (x, .10, -.65), .065 if medium else .04, .05, steel, 8)
    tube("GearHousing", (x, -.035, -.64), (x, .19, -.64), .07, .045, steel, 8)
    propeller = empty("Propeller", (x, .20, -.64))
    tube("PropHub", (0, -.025, 0), (0, .025, 0), .04, .03, dark, 8, propeller)
    for i in range(3):
        angle = i * math.tau / 3
        obj = box("Blade", (.10 * math.cos(angle), 0, .10 * math.sin(angle)), (.15, .022, .055), steel, propeller)
        obj.data.transform(Matrix.Rotation(-angle, 4, "Y"))
    box("Skeg", (x, .09, -.75), (.025, .18, .14), steel)
    if not medium:
        tube("Tiller", (x, -.15, .15), (x, -.44, .15), .022, .022, steel, 7)
        tube("TillerGrip", (x, -.44, .15), (x, -.55, .15), .029, .029, dark, 7)


def radar():
    shell = material("BoatRadarShell", (.22, .26, .25))
    dark = material("BoatRadarFrame", (.055, .07, .06))
    box("ScreenHousing", (0, 0, .19), (.46, .13, .38), shell)
    box("ScreenInset", (0, -.072, .21), (.38, .018, .30), dark)
    box("Stand", (0, .01, -.025), (.12, .12, .11), shell)
    box("Foot", (0, 0, -.09), (.31, .24, .025), shell)
    for x in (-.15, -.10, .10, .15):
        tube("Key", (x, -.07, .035), (x, -.09, .035), .014, .014, dark, 7)
    tube("Antenna", (.19, .035, .36), (.19, .035, .66), .009, .009, dark, 6)


if __name__ == "__main__":
    project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    assert (project / "ProjectSettings/ProjectVersion.txt").is_file()
    for name in ("SmallMotor", "MediumMotor", "BigMotor", "BoatRadar"):
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        art.ROOT = None
        art.ROOT = empty(name)
        if name == "BoatRadar": radar()
        elif name == "BigMotor":
            for x in (-.26, .26): motor(True, x)
        else: motor(name == "MediumMotor")
        empty("Grip")
        empty("Forward", (0, -.4, .2))
        save_and_export(project, name)
