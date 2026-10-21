"""Standalone orbit study only; leave the complete core unchanged."""
from pathlib import Path
import math
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree = ET.parse(ROOT/'StorageCore-ring-v16.svg')
root = tree.getroot()
shadow = next(e for e in root.iter() if e.get('id')=='orbit-front-shadow')
shadow.set('d','M82 0A82 34 0 0 1 -82 0A82 31.3 0 0 0 82 0Z')

def inward_distance(px,py,nx,ny,a,b):
    aa=(nx/a)**2+(ny/b)**2
    bb=-2*(px*nx/(a*a)+py*ny/(b*b))
    cc=(px/a)**2+(py/b)**2-1
    return max(0,(-bb-math.sqrt(max(0,bb*bb-4*aa*cc)))/(2*aa))

# Measure along the ellipse normal, rather than comparing vertical thickness
# only. The thickest shadow stays below half of the local band width.
ratios=[]
for index in range(1001):
    theta=math.pi*index/1000
    px,py=82*math.cos(theta),34*math.sin(theta)
    nx,ny=math.cos(theta)/82,math.sin(theta)/34
    length=math.hypot(nx,ny)
    nx,ny=nx/length,ny/length
    band=inward_distance(px,py,nx,ny,76,28)
    shade=inward_distance(px,py,nx,ny,82,31.3)
    ratios.append(shade/band)
assert max(ratios)<=.45+1e-8
assert ratios[0]<1e-8 and ratios[-1]<1e-8
ET.indent(root,space='  ')
output=ROOT/'StorageCore-ring-v17.svg'
tree.write(output,encoding='utf-8',xml_declaration=False)
print(f'Standalone ring: maximum shadow/band width = {max(ratios):.1%}; complete core unchanged.')
print(output)
