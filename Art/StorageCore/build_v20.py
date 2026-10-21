"""Balance the structural finish of the core without changing its orbit."""
from copy import deepcopy
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tag=lambda name:f'{{{NS}}}{name}'
find=lambda doc,identifier:next(e for e in doc.iter() if e.get('id')==identifier)
tree=ET.parse(ROOT/'StorageCore-v19.svg')
root=tree.getroot()

def replace_group(identifier,body):
    old=find(root,identifier)
    replacement=ET.fromstring(f'<g xmlns="{NS}" id="{identifier}">{body}</g>')
    index=list(root).index(old)
    root.remove(old)
    root.insert(index,replacement)
    return replacement

# Keep the node-panel design, with fewer very small marks and a more legible
# link. Its fine circuit graphic no longer contrasts with unfinished feet.
controller=find(root,'controller')
controller.remove(controller[-1])
for element in controller:
    if element.get('stroke')=='#849fb2':
        element.set('stroke-width','2.25')
        element.set('stroke-linejoin','round')
    if element.tag==tag('circle'):
        element.set('r','2.75')

# Recessed openings have a frame, an inner dark face and a broad lower lip.
# These are structural planes, with no new bolts, labels or ornament.
replace_group('cooling-intake','''
  <rect x="367" y="294" width="51" height="66" rx="3" fill="#97a8b2"/>
  <rect x="370" y="297" width="45" height="60" rx="2" fill="#b6c3c9"/>
  <path d="M375 305H410V314H375ZM375 323H410V332H375ZM375 341H410V350H375Z" fill="#405563"/>
  <path d="M375 312H410V314H375ZM375 330H410V332H375ZM375 348H410V350H375Z" fill="#8197a4"/>
''')

replace_group('core-cradle','''
  <!-- Solid beams with one broad upper face each. -->
  <path d="M243 178L171 292L181 302L255 186Z" fill="#697b87"/>
  <path d="M244 181L175 291L181 295L251 185Z" fill="#a4b3bb"/>
  <path d="M269 178L341 292L331 302L257 186Z" fill="#697b87"/>
  <path d="M268 181L337 291L331 295L261 185Z" fill="#a4b3bb"/>

  <!-- The mounting collar has a broad side face rather than a painted arc. -->
  <path d="M176 295V309C176 325 212 338 256 338C300 338 336 325 336 309V295Z" fill="#536b7a"/>
  <path d="M176 295V309C176 325 212 338 256 338V325C212 325 176 313 176 295Z" fill="#6c8290"/>
  <ellipse cx="256" cy="295" rx="80" ry="26" fill="#a6b4bc"/>
  <ellipse cx="256" cy="295" rx="66" ry="20" fill="#344d60"/>

  <!-- Three emitters share the same cap, lens inset, and side-face treatment. -->
  <g id="rear-emitter">
    <path d="M243 164H269V193Q269 201 261 201H251Q243 201 243 193Z" fill="#617783"/>
    <path d="M260 164H269V193Q269 201 261 201H260Z" fill="#4d626f"/>
    <rect x="243" y="158" width="26" height="18" rx="3" fill="#b6c3c9"/>
    <rect x="247" y="162" width="18" height="7" rx="1" fill="#c5e5f5"/>
  </g>
  <g id="left-emitter">
    <path d="M164 292H194V317Q194 325 186 325H172Q164 325 164 317Z" fill="#617783"/>
    <path d="M185 292H194V317Q194 325 186 325H185Z" fill="#4d626f"/>
    <rect x="164" y="286" width="30" height="18" rx="3" fill="#b6c3c9"/>
    <rect x="168" y="290" width="22" height="7" rx="1" fill="#c5e5f5"/>
  </g>
  <g id="right-emitter">
    <path d="M318 292H348V317Q348 325 340 325H326Q318 325 318 317Z" fill="#617783"/>
    <path d="M339 292H348V317Q348 325 340 325H339Z" fill="#4d626f"/>
    <rect x="318" y="286" width="30" height="18" rx="3" fill="#b6c3c9"/>
    <rect x="322" y="290" width="22" height="7" rx="1" fill="#c5e5f5"/>
  </g>
''')

# Keep a main light area, one broad shadow and one broad highlight. The small
# secondary sliver was more detailed than the surrounding material treatment.
orb=find(root,'suspended-core')
sliver=next(e for e in orb if e.get('fill')=='#6b8da5')
orb.remove(sliver)
root.find(tag('title')).text='魔法存储 — 存储控制核心，第二十稿'
root.find(tag('desc')).text=(
    'A consistent medium level of structural finish: emitter caps, recessed '
    'lights, beam faces, cooling lips and collar depth share a restrained '
    'material vocabulary. The node controller is retained with fewer tiny '
    'marks. The v18 orbit, its single tapered shadow, the chassis silhouette '
    'and swept lower-corner shading are unchanged. Review artwork only.')
previous=ET.parse(ROOT/'StorageCore-v19.svg').getroot()
for identifier in ('energy-orbit','base','outer-shell','power-feed','exterior-contour'):
    assert ET.tostring(find(root,identifier))==ET.tostring(find(previous,identifier))
assert sum(e.tag==tag('circle') for e in controller)==3
ET.indent(root,space='  ')
output=ROOT/'StorageCore-v20.svg'
tree.write(output,encoding='utf-8',xml_declaration=False)
print(output)
