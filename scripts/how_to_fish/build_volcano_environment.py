"""Self-authored faceted volcanic island, crater and emissive lava surfaces."""
import math
import sys
from pathlib import Path
import bpy
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_island_models as art
from build_first_island_models import empty, material, mesh
from build_volcano_models import glow
from fbx_export import save_and_export

project = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
art.ROOT = None
art.ROOT = empty("VolcanoTerrain")
colors = [material("VolcanoBasalt", (.105,.10,.13)), material("VolcanoAsh", (.22,.21,.24)), material("VolcanoShore",(.16,.17,.19))]
profile = [(0,21.5),(12,21.5),(16,25),(22,27),(29,22),(40,14),(58,5),(74,2.4),(86,.2),(98,-4)]
segments=64
vertices=[]
for ring,(radius,height) in enumerate(profile):
 for i in range(segments):
  angle=i*math.tau/segments
  r=radius*(1+(.02*math.sin(i*2.7)+.012*math.sin(i*5.3)) if ring>1 else 1)
  z=height+(0 if ring<2 or ring>7 else .35*math.sin(angle*7+ring))
  vertices.append((r*math.cos(angle),r*math.sin(angle),z))
faces=[]
for ring in range(len(profile)-1):
 for i in range(segments):
  a=ring*segments+i;b=ring*segments+(i+1)%segments;c=(ring+1)*segments+i;d=(ring+1)*segments+(i+1)%segments
  if ring==0: faces.append((a,c,d))
  else: faces.extend(((a,c,b),(b,c,d)))
terrain=mesh("Terrain",vertices,faces,colors[0])
for value in colors[1:]: terrain.data.materials.append(value)
for polygon in terrain.data.polygons:
 polygon.material_index=2 if polygon.center.length>75 else (1 if polygon.index%7==0 else 0)
lava=glow("VolcanoLavaSurface",(1,.20,.015))
points=[(0,0,21.62)]+[(12.5*math.cos(i*math.tau/segments),12.5*math.sin(i*math.tau/segments),21.62) for i in range(segments)]
mesh("CraterLava",points,[(0,i+1,(i+1)%segments+1) for i in range(segments)],lava)
for stream,angle in enumerate([.3,2.1,4.5]):
 points=[]
 for j,(radius,height) in enumerate(profile[3:7]):
  direction=angle+.05*math.sin(j*2+stream)
  for side in (-1,1):
   a=direction+side*.012
   points.append((radius*math.cos(a),radius*math.sin(a),height+.55))
 mesh("LavaFlow"+str(stream),points,[(i,i+2,i+3,i+1) for i in range(0,len(points)-2,2)],lava)
empty("Grip")
empty("Forward",(0,-10,0))
empty("Crater",(0,0,21.62))
save_and_export(project,"VolcanoTerrain")
