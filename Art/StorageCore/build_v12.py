"""Repair the orbit and chassis joins without redesigning the v11 study."""
from copy import deepcopy
from pathlib import Path
import math
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('', NS)
def tag(name):
    return f'{{{NS}}}{name}'
def use(identifier, **attributes):
    return ET.Element(tag('use'), {'href':'#'+identifier, **attributes})

tree = ET.parse(ROOT/'StorageCore-v11.svg')
root = tree.getroot()
root.find(tag('title')).text = '魔法存储 — 存储控制核心，第十二稿'
root.find(tag('desc')).text += ' Revision 12 shares one closed orbit between its rear and front passes; the chassis fill, clipping and exterior use the same geometry. The front seam is a single tangent-continuous curve.'
groups = {element.get('id'):element for element in root if element.get('id')}
outline = groups['exterior-contour'].get('d')
plate = 'M99 60H413Q448 60 448 95V385Q448 420 413 420H99Q64 420 64 385V95Q64 60 99 60Z'

defs = ET.Element(tag('defs'))
ET.SubElement(defs, tag('path'), {'id':'chassis-outline-geometry','d':outline})
ET.SubElement(defs, tag('path'), {'id':'top-plate-geometry','d':plate})
for name,geometry in [('chassis-clip','chassis-outline-geometry'),('plate-clip','top-plate-geometry')]:
    clip = ET.SubElement(defs,tag('clipPath'),{'id':name})
    clip.append(use(geometry))

# Both passes use exactly the same projected ellipse and the same stroke
# centerline, rather than individually offsetting the colored highlight.
angle = math.radians(-22)
rx,ry,cx,cy = 80,31,256,245
right = (cx+rx*math.cos(angle), cy+rx*math.sin(angle))
left = (cx-rx*math.cos(angle), cy-rx*math.sin(angle))
r = f'{right[0]:.8f} {right[1]:.8f}'
l = f'{left[0]:.8f} {left[1]:.8f}'
front = f'M{r}A80 31 -22 0 1 {l}'
closed = front+f'A80 31 -22 0 1 {r}Z'
ET.SubElement(defs,tag('path'),{'id':'orbit-loop-geometry','d':closed})
ET.SubElement(defs,tag('path'),{'id':'orbit-front-geometry','d':front})
root.insert(2,defs)

base = groups['base']
base.remove(base[0])
base.insert(0,use('chassis-outline-geometry',fill='#53616b'))
base.remove(base[1])
base.insert(1,ET.Element(tag('rect'),{
    'x':'64','y':'432','width':'384','height':'14','fill':'#414e58',
    'clip-path':'url(#chassis-clip)'}))

shell = groups['outer-shell']
original = list(shell)
shell.clear()
shell.set('id','outer-shell')
shell.append(use('top-plate-geometry',fill='#cfd4d5'))
bevels = ET.SubElement(shell,tag('g'),{'clip-path':'url(#plate-clip)'})
bevels.extend(deepcopy(original[1:4]))
ET.SubElement(shell,tag('path'),{
    'id':'front-seam', 'd':'M64 385Q64 420 99 420H413Q448 420 448 385',
    'fill':'none','stroke':'#52616b','stroke-width':'3'})

sphere = groups['suspended-core']
original = list(sphere)
sphere.clear()
sphere.set('id','suspended-core')
sphere.append(deepcopy(original[0]))
for geometry in ('orbit-loop-geometry','orbit-front-geometry'):
    sphere.append(use(geometry,fill='none',stroke='#3d5a70',**{'stroke-width':'8'}))
    sphere.append(use(geometry,fill='none',stroke='#c5e5f5',**{'stroke-width':'4'}))
    if geometry == 'orbit-loop-geometry':
        sphere.extend(deepcopy(original[3:7]))

root.remove(groups['exterior-contour'])
root.append(use('chassis-outline-geometry',id='exterior-contour',fill='none',
                stroke='#000000',**{'stroke-width':'4'}))
ET.indent(root,space='  ')
output = ROOT/'StorageCore-v12.svg'
tree.write(output,encoding='utf-8',xml_declaration=False)
assert closed.startswith(front) and closed.endswith(r+'Z')
assert root[-1].get('href')==base[0].get('href')=='#chassis-outline-geometry'
print(output)
