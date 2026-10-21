"""Carry the single tapered shadow farther around both ends of the ring."""
from pathlib import Path
import math
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree=ET.parse(ROOT/'StorageCore-ring-v17.svg')
root=tree.getroot()
shadow=next(e for e in root.iter() if e.get('id')=='orbit-front-shadow')
start,end=math.radians(-18),math.radians(198)

def outer(t):
    return 82*math.cos(t),34*math.sin(t)

def inner_edge(t):
    x,y=outer(t)
    nx,ny=math.cos(t)/82,math.sin(t)/34
    length=math.hypot(nx,ny)
    nx,ny=nx/length,ny/length
    a=(nx/76)**2+(ny/28)**2
    b=-2*(x*nx/76**2+y*ny/28**2)
    c=(x/76)**2+(y/28)**2-1
    band=(-b-math.sqrt(max(0,b*b-4*a*c)))/(2*a)
    u=(t-start)/(end-start)
    s2=math.sin(math.pi*u)**2
    # A broader taper holds visible shading near both major-axis ends;
    # the 45% maximum stays identical to the preceding study.
    fraction=.45*s2/(s2+.12*(1-s2))
    depth=band*fraction
    return (x-nx*depth,y-ny*depth),fraction

def derivative(t):
    h=1e-5
    p,_=inner_edge(t-h)
    q,_=inner_edge(t+h)
    return (q[0]-p[0])/(2*h),(q[1]-p[1])/(2*h)

def point(p):
    return f'{p[0]:.7f} {p[1]:.7f}'

first,last=outer(start),outer(end)
parts=[f'M{point(first)}A82 34 0 1 1 {point(last)}']
count=128
fractions=[]
# Return along one smooth inner boundary, forming ONE closed filled shape.
for index in range(count):
    ta=end-(end-start)*index/count
    tb=end-(end-start)*(index+1)/count
    pa,fa=inner_edge(ta)
    pb,fb=inner_edge(tb)
    da,db=derivative(ta),derivative(tb)
    delta=(tb-ta)/3
    ca=(pa[0]+da[0]*delta,pa[1]+da[1]*delta)
    cb=(pb[0]-db[0]*delta,pb[1]-db[1]*delta)
    if index==count-1:
        pb=first
    parts.append(f'C{point(ca)} {point(cb)} {point(pb)}')
    fractions.extend((fa,fb))
parts.append('Z')
shadow.set('d',''.join(parts))
assert max(fractions)<=.45+1e-10
assert abs(fractions[0])<1e-10 and abs(fractions[-1])<1e-10
assert shadow.get('d').count('M')==1 and shadow.get('d').count('Z')==1

# The standalone file needs only the ring definition and the two color shapes.
defs=root.find(f'{{{NS}}}defs')
for element in list(defs):
    if element.get('id')!='orbit-loop-geometry':
        defs.remove(element)
ET.indent(root,space='  ')
output=ROOT/'StorageCore-ring-v18.svg'
tree.write(output,encoding='utf-8',xml_declaration=False)
print('Standalone ring: shadow extended 18 degrees around each end; maximum width 45%.')
print(output)
