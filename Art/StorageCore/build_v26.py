"""Restore the supplied v13 shadow instead of inventing another curve."""
from copy import deepcopy
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
REFERENCE=Path(r'C:/Users/x1353/.codex/attachments/4e9322ef-dff1-41a4-b21b-48e74d092971/已粘贴的文本.txt')
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tag=lambda name:f'{{{NS}}}{name}'
find=lambda doc,identifier:next(e for e in doc.iter() if e.get('id')==identifier)
tree=ET.parse(ROOT/'StorageCore-v25.svg')
root=tree.getroot()
reference=ET.parse(REFERENCE).getroot()
base=find(root,'base')
old=find(root,'rounded-base-shadow')
index=list(base).index(old)
base.remove(old)
restored=deepcopy(find(reference,'rounded-base-shadow'))
wrapper=ET.Element(tag('g'),{'id':'corner-shading','clip-path':'url(#corner-shadow-only)'})
wrapper.append(restored)
base.insert(index,wrapper)

defs=root.find(tag('defs'))
# Preserve the supplied curved shape. Only suppress its short horizontal
# continuation toward the middle of the face, as requested previously.
corners=ET.SubElement(defs,tag('clipPath'),{'id':'corner-shadow-only'})
ET.SubElement(corners,tag('path'),{
    'd':'M0 0H104V440L112 446V512H0Z M408 0H512V512H400V446L408 440Z'})

# The previous centered 12-unit stroke covered the old shadow entirely.
# Expand its centerline outward by four units, keeping a 12-unit stroke.
# Its visible inside edge now returns to roughly the old 4-unit-stroke
# position; the chassis fill and original shade themselves are unchanged.
ET.SubElement(defs,tag('path'),{
    'id':'expanded-exterior-geometry',
    'd':'M99 56H413Q452 56 452 95V410Q452 450 412 450H100Q60 450 60 410V95Q60 56 99 56Z'})
contour=find(root,'exterior-contour')
contour.set('href','#expanded-exterior-geometry')
assert contour.get('stroke-width')=='12'
assert restored.get('d')==find(reference,'rounded-base-shadow').get('d')
assert restored.get('fill')==find(reference,'rounded-base-shadow').get('fill')
previous=ET.parse(ROOT/'StorageCore-v25.svg').getroot()
for identifier in ('outer-shell','controller','core-cradle','suspended-core','power-feed','cooling-intake'):
    assert ET.tostring(find(root,identifier))==ET.tostring(find(previous,identifier))
root.find(tag('title')).text='魔法存储 — 存储控制核心，第二十六稿'
root.find(tag('desc')).text=(
    'The exact lower shadow path supplied in the v13 reference is restored. '
    'It is visible only near the corners. The 12-unit black exterior stroke '
    'has an outward-expanded centerline so it no longer hides that shadow. '
    'The chassis surface, orbit, node panel and other current components are retained.')
ET.indent(root,space='  ')
tree.write(ROOT/'StorageCore-v26.svg',encoding='utf-8',xml_declaration=False)
print('Restored exact shadow path from the user-supplied SVG; black contour remains 12 units.')
print(ROOT/'StorageCore-v26.svg')
