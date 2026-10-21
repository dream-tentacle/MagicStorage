"""Restore the v11 orbit profile and shade while closing its two joins."""
from copy import deepcopy
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tag = lambda name: f'{{{NS}}}{name}'
find = lambda doc,identifier: next(e for e in doc.iter() if e.get('id')==identifier)
tree = ET.parse(ROOT/'StorageCore-v14.svg')
root = tree.getroot()
old = ET.parse(ROOT/'StorageCore-v11.svg').getroot()
original_orb = find(root,'suspended-core')
orb = deepcopy(find(old,'suspended-core'))

# Keep the old shallow orbit. Its rear endpoint now meets the foreground
# endpoint at (329,226), with the same tangent (3,17) on both sides.
rear = 'M185 267C171 246 194 215 233 203C272 191 326 209 329 226'
front = 'M185 267C199 288 251 293 292 274C316 263 332 243 329 226'
loop = rear+'C332 243 316 263 292 274C251 293 199 288 185 267Z'
# The rear and front illuminated faces share both endpoints as well. Retain
# the original upper-edge offset, which exposes the dark underside.
rear_light = 'M185 264C171 243 196 216 233 205C271 193 324 210 327 227'
front_light = 'M185 264C199 285 251 290 292 271C315 260 330 244 327 227'
orb[1].set('d',loop)
orb[2].set('d',rear_light)
orb[-2].set('d',front)
orb[-1].set('d',front_light)
root.insert(list(root).index(original_orb),orb)
root.remove(original_orb)

defs = root.find(tag('defs'))
find(root,'orbit-loop-geometry').set('d',loop)
find(root,'orbit-front-geometry').set('d',front)
root.find(tag('title')).text = '魔法存储 — 存储控制核心，第十五稿'
root.find(tag('desc')).text = (
    'Rounded-square storage core. The original shallow v11 orbit and its '
    'offset illuminated face are restored, preserving the dark rear/underside. '
    'The rear and front curves share endpoints and tangent directions; a '
    'closed underlying band prevents gaps. The node controller and swept '
    'chassis corner shadow are retained from v14. Accent RGB(197,229,245).')

previous = ET.parse(ROOT/'StorageCore-v14.svg').getroot()
for group in previous:
    identifier = group.get('id')
    if identifier and identifier!='suspended-core':
        assert ET.tostring(group)==ET.tostring(find(root,identifier))
assert (329-326,226-209)==(332-329,243-226)
assert (185-199,267-288)==(171-185,246-267)
assert (327-324,227-210)==(330-327,244-227)
assert loop.endswith('185 267Z')
ET.indent(root,space='  ')
output = ROOT/'StorageCore-v15.svg'
tree.write(output,encoding='utf-8',xml_declaration=False)
print(output)
