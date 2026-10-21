"""Place the approved standalone v18 ring into the complete core study."""
from copy import deepcopy
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
find=lambda doc,identifier: next(e for e in doc.iter() if e.get('id')==identifier)
tree=ET.parse(ROOT/'StorageCore-v16.svg')
root=tree.getroot()
ring_source=ET.parse(ROOT/'StorageCore-ring-v18.svg').getroot()
current=find(root,'energy-orbit')
replacement=deepcopy(find(ring_source,'energy-orbit'))
# The standalone ring's local coordinates are unchanged. Reuse the existing
# assembly transform and sphere-occlusion mask for correct front/rear order.
replacement.set('transform',current.get('transform'))
replacement.set('mask',current.get('mask'))
orb=find(root,'suspended-core')
index=list(orb).index(current)
orb.remove(current)
orb.insert(index,replacement)
assert find(root,'orbit-loop-geometry').get('d')==find(ring_source,'orbit-loop-geometry').get('d')
assert find(root,'orbit-front-shadow').attrib==find(ring_source,'orbit-front-shadow').attrib
previous=ET.parse(ROOT/'StorageCore-v16.svg').getroot()
for element in previous:
    identifier=element.get('id')
    if identifier and identifier!='suspended-core':
        assert ET.tostring(element)==ET.tostring(find(root,identifier))
assert [ET.tostring(e) for e in orb[:-1]]==[ET.tostring(e) for e in find(previous,'suspended-core')[:-1]]
root.find(f'{{{NS}}}title').text='魔法存储 — 存储控制核心，第十九稿'
root.find(f'{{{NS}}}desc').text=(
    'Complete core with the standalone v18 sky-blue annulus and a single '
    'foreground shadow extending farther around both ends. Shadow width '
    'is capped at 45 percent of the local ring band. Sphere occlusion, '
    'controller, cradle and swept chassis shading remain unchanged.')
ET.indent(root,space='  ')
output=ROOT/'StorageCore-v19.svg'
tree.write(output,encoding='utf-8',xml_declaration=False)
print(output)
