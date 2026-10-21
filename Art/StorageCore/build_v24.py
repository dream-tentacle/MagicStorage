"""Restore the curved corner-side shade beside the inner black edge."""
from pathlib import Path
import math
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree=ET.parse(ROOT/'StorageCore-v23.svg')
root=tree.getroot()
find=lambda identifier:next(e for e in root.iter() if e.get('id')==identifier)

def edge(t,inside):
    # Exactly the quadratic corner used by the chassis outline. Offset from
    # it by half the 12-unit contour so the visible shade hugs that contour.
    x=64+36*t*t
    y=410+72*t-36*t*t
    nx,ny=1-t,-t
    length=math.hypot(nx,ny)
    depth=6+(6*math.sin(math.pi*t) if inside else 0)
    return x+depth*nx/length,y+depth*ny/length

def derivative(t,inside):
    h=1e-5
    a,b=edge(t-h,inside),edge(t+h,inside)
    return (b[0]-a[0])/(2*h),(b[1]-a[1])/(2*h)

def corner(mirror=False):
    def point(p):
        x,y=p
        return f'{512-x if mirror else x:.7f} {y:.7f}'
    parts=['M'+point(edge(0,False))]
    for inside,direction in [(False,1),(True,-1)]:
        count=48
        for index in range(count):
            ta=index/count if direction==1 else 1-index/count
            tb=(index+1)/count if direction==1 else 1-(index+1)/count
            pa,pb=edge(ta,inside),edge(tb,inside)
            da,db=derivative(ta,inside),derivative(tb,inside)
            delta=(tb-ta)/3
            ca=(pa[0]+da[0]*delta,pa[1]+da[1]*delta)
            cb=(pb[0]-db[0]*delta,pb[1]-db[1]*delta)
            parts.append('C'+point(ca)+' '+point(cb)+' '+point(pb))
    parts.append('Z')
    return ''.join(parts)

shadow=find('rounded-base-shadow')
shadow.set('d',corner()+' '+corner(True))
assert shadow.get('fill')=='#414e58'
assert find('exterior-contour').get('stroke-width')=='12'
root.find(f'{{{NS}}}title').text='魔法存储 — 存储控制核心，第二十四稿'
root.find(f'{{{NS}}}desc').text=(
    'The lower corner shading follows the chassis curvature immediately '
    'inside the thick black outline. It is confined to the curved side '
    'faces, with no shade band on the straight bottom edge. The current '
    'sphere, orbit, node controller and structural finish are retained.')
tree.write(ROOT/'StorageCore-v24.svg',encoding='utf-8',xml_declaration=False)
print(ROOT/'StorageCore-v24.svg')
