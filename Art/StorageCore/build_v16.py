"""One sky-blue annulus and one continuous tapered foreground shadow."""
from copy import deepcopy
from pathlib import Path
import math
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tag = lambda name: f'{{{NS}}}{name}'
find = lambda doc,identifier: next(e for e in doc.iter() if e.get('id')==identifier)
tree = ET.parse(ROOT/'StorageCore-v15.svg')
root = tree.getroot()
defs = root.find(tag('defs'))
for identifier in ('orbit-loop-geometry','orbit-front-geometry'):
    defs.remove(find(root,identifier))

# The entire ring is a single flat-color annulus. Its shadow is ONE filled
# crescent bounded by two half-ellipses with shared endpoints. It vanishes
# at (+/-82,0) and grows to 4.5 units deep at the middle of the front half.
annulus = ('M82 0A82 34 0 1 1 -82 0A82 34 0 1 1 82 0Z '
           'M76 0A76 28 0 1 1 -76 0A76 28 0 1 1 76 0Z')
shadow = 'M82 0A82 34 0 0 1 -82 0A82 29.5 0 0 0 82 0Z'
ET.SubElement(defs,tag('path'),{'id':'orbit-loop-geometry','d':annulus,'fill-rule':'evenodd'})

# Draw the ring only once. A visibility mask places the rear half behind
# the sphere while the front half stays in front; the shadow is not split.
rear = ET.SubElement(defs,tag('clipPath'),{'id':'orbit-rear-half'})
ET.SubElement(rear,tag('rect'),{'x':'-100','y':'-100','width':'200','height':'100'})
visibility = ET.SubElement(defs,tag('mask'),{
    'id':'orbit-visibility','maskUnits':'userSpaceOnUse',
    'x':'-100','y':'-100','width':'200','height':'200'})
ET.SubElement(visibility,tag('rect'),{'x':'-100','y':'-100','width':'200','height':'200','fill':'#ffffff'})
angle = math.radians(17)
ET.SubElement(visibility,tag('circle'),{
    'cx':f'{3*math.sin(angle):.8f}','cy':f'{-3*math.cos(angle):.8f}',
    'r':'61.25','fill':'#000000','clip-path':'url(#orbit-rear-half)'})

orb = find(root,'suspended-core')
original = list(orb)
orb.clear()
orb.set('id','suspended-core')
orb.append(deepcopy(original[0]))
orb.extend(deepcopy(original[3:7]))
ring = ET.SubElement(orb,tag('g'),{
    'id':'energy-orbit','transform':'translate(256 245) rotate(-17)',
    'mask':'url(#orbit-visibility)'})
ET.SubElement(ring,tag('use'),{'id':'orbit-blue-band','href':'#orbit-loop-geometry','fill':'#c5e5f5'})
ET.SubElement(ring,tag('path'),{'id':'orbit-front-shadow','d':shadow,'fill':'#3d5a70'})

root.find(tag('title')).text = '魔法存储 — 存储控制核心，第十六稿'
root.find(tag('desc')).text = (
    'The orbit is one solid sky-blue annulus with one filled crescent shadow '
    'only on the foreground half. The shadow tapers continuously to zero at '
    'both ends. A rear-half visibility mask handles sphere occlusion without '
    'splitting either shape. Controller and chassis remain as approved.')
previous = ET.parse(ROOT/'StorageCore-v15.svg').getroot()
for group in previous:
    identifier = group.get('id')
    if identifier and identifier!='suspended-core':
        assert ET.tostring(group)==ET.tostring(find(root,identifier))
assert len(ring)==2 and all(e.get('stroke') is None for e in ring)
assert shadow.count('M')==1 and shadow.count('Z')==1
assert 28 < 29.5 < 34
ET.indent(root,space='  ')
tree.write(ROOT/'StorageCore-v16.svg',encoding='utf-8',xml_declaration=False)

# Unoccluded construction study: exactly the same blue band and shadow.
study = ET.Element(tag('svg'),{'width':'512','height':'282','viewBox':'-100 -55 200 110'})
study.append(deepcopy(defs))
standalone = deepcopy(ring)
standalone.set('transform','rotate(-17)')
standalone.attrib.pop('mask')
study.append(standalone)
ET.indent(study,space='  ')
ET.ElementTree(study).write(ROOT/'StorageCore-ring-v16.svg',encoding='utf-8',xml_declaration=False)
print('Prepared v16: one blue annulus, one foreground-only filled shadow, tapered ends.')
