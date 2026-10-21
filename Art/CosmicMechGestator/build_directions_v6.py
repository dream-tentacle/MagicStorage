"""Build the north and east SVG studies around the approved south vessel."""
from pathlib import Path
from copy import deepcopy
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('', NS)
def tag(name):
    return f'{{{NS}}}{name}'

south = ET.parse(ROOT / 'CosmicMechGestator_south-v6.svg').getroot()
def find(identifier):
    return next(element for element in south.iter() if element.get('id') == identifier)

def fragment(text):
    return ET.fromstring(f'<g xmlns="{NS}">{text}</g>')

def document(direction, height, center):
    root = ET.Element(tag('svg'), dict(width='1536', height=str(height*2), viewBox=f'0 0 768 {height}'))
    ET.SubElement(root, tag('title')).text = f'魔法存储 — 宇宙培育器，{direction}，第六稿配套'
    ET.SubElement(root, tag('desc')).text = (
        f'Companion view to approved south v6. Lower vessel seat shares the platform top center {center}. '
        'Circular vessel dimensions and elevation match the south view. Pearl grey metal, '
        'RGB(197,229,245) accents, and a uniform 4-unit black exterior contour. Review artwork only.')
    root.append(deepcopy(south.find(tag('defs'))))
    return root

def vessel(root, dy=0, direction='north'):
    for name in ['vessel-seat', 'cylinder-glass', 'gestator-top']:
        layer = deepcopy(find(name))
        layer.set('transform', f'translate(0 {dy-27})')
        if name == 'gestator-top':
            # Surface markings turn with the device; highlights keep the scene lighting.
            markings = list(layer)[-2:]
            for mark in markings:
                layer.remove(mark)
            if direction == 'north':
                marks = fragment('''
                    <path d="M365 143H403M365 152H403" stroke="#94adbf" stroke-width="3" stroke-linecap="round"/>
                    <path d="M374 133H394" stroke="#c5e5f5" stroke-width="3" stroke-linecap="round"/>
                ''')
            else:
                marks = fragment('''
                    <path d="M396 128V155M383 128V155" stroke="#94adbf" stroke-width="3" stroke-linecap="round"/>
                    <path d="M369 135V149" stroke="#c5e5f5" stroke-width="3" stroke-linecap="round"/>
                ''')
            layer.extend(list(marks))
        root.append(layer)

# NORTH: keep the same platform, move the service face to its far edge.
north = document('北向', 512, [384, 275])
base = ET.Element(tag('g'), {'id':'gestator-base', 'data-platform-center':'384,275'})
old_base = find('gestator-base')
base.extend(deepcopy(list(old_base)[:6]))
manifolds = ET.SubElement(base, tag('g'), {'id':'side-manifolds', 'transform':'translate(0 21)'})
manifolds.extend(deepcopy(list(old_base)[6:-1]))
# Turn the small vent/indicator details around without inverting their thickness.
for element in manifolds:
    if element.get('d', '').startswith('M131 190'):
        element.set('d', 'M131 320V346M142 320V346M153 320V346M615 320V346M626 320V346M637 320V346')
    if element.tag == tag('rect') and element.get('fill') == '#c5e5f5':
        element.set('y', '171')
north.append(base)
rear = fragment('''
    <g id="rear-service" >
      <path d="M88 103H680V146H88Z" fill="#bacbd7"/>
      <path d="M94 107H674" stroke="#e9f1f6" stroke-width="2"/>
      <rect x="523" y="111" width="143" height="23" rx="4" fill="#93afc3"/>
      <rect x="527" y="114" width="135" height="16" rx="2" fill="#344e64"/>
      <path d="M538 122H561M591 122H613M625 122H650" stroke="#c5e5f5" stroke-width="2.5"/>
      <path d="M283 109H485V141H283Z" fill="#8ea9bd"/>
      <path d="M288 112H480V122H288ZM288 130H480V140H288Z" fill="#d3e1ea"/>
      <rect x="104" y="111" width="126" height="23" rx="4" fill="#d2e0e9"/>
      <path d="M117 122H149M160 122H174" stroke="#9ab3c6" stroke-width="2.5"/>
      <rect x="204" y="118" width="17" height="7" rx="2" fill="#c5e5f5"/>
    </g>
''')
north.extend(list(rear))
vessel(north)
apron = fragment('''
    <g id="back-casing">
      <path d="M88 406H680V449H88Z" fill="#bacbd7"/>
      <path d="M94 410H674" stroke="#e9f1f6" stroke-width="2"/>
      <path d="M282 416H486V440H282Z" fill="#a8bfce"/>
      <path d="M297 423H471M297 433H471" stroke="#7c99ad" stroke-width="3"/>
      <path d="M112 425H186M582 425H656" stroke="#8da7ba" stroke-width="3" stroke-linecap="round"/>
      <path d="M124 435H151M617 435H644" stroke="#c5e5f5" stroke-width="3" stroke-linecap="round"/>
      <rect x="343" y="461" width="82" height="6" rx="3" fill="#344e64"/>
    </g>
''')
north.extend(list(apron))
north.append(deepcopy(list(south)[-1]))

# EAST: quarter-turn the ground equipment, preserving the upright vessel.
# Approved south top plane: 640 × 350. Quarter turn: 426 2/3 × 525.
east = document('侧向（东向，西向可镜像）', 768, [384, 400])
base = fragment('''
  <g id="gestator-base" data-platform-center="384,400">
    <rect x="170.6667" y="157.5" width="426.6666" height="529" rx="20" fill="#7e939f"/>
    <rect x="170.6667" y="137.5" width="426.6666" height="525" rx="20" fill="#cdd8df"/>
    <path d="M170.6667 642.5Q170.6667 662.5 190.6667 662.5H577.3333Q597.3333 662.5 597.3333 642.5" fill="none" stroke="#536d7f" stroke-width="3.5"/>
    <path d="M184 250V165Q184 151 199 151H569" fill="none" stroke="#edf3f7" stroke-width="3" stroke-linecap="round"/>
    <path d="M584 171V631Q584 649 566 649H204" fill="none" stroke="#a0b5c3" stroke-width="3.5" stroke-linecap="round"/>
    <path d="M530 159V638" stroke="#acbfcd" stroke-width="2"/>
    <!-- The old left/right manifolds become the near/far crosswise blocks. -->
    <path d="M237 184L253 170H506V282H237Z" fill="#adbfcc"/>
    <path d="M246 188L257 179H497V273H246Z" fill="#8fa6b7"/>
    <path d="M253 190H486" stroke="#cbdbe6" stroke-width="2"/>
    <path d="M259 206H286M259 217H286M259 228H286" stroke="#d2e2ec" stroke-width="4"/>
    <path d="M326 260V285L303 321M441 260V285L465 321" fill="none" stroke="#6e8a9f" stroke-width="18" stroke-linecap="round" stroke-linejoin="round"/>
    <path d="M323 256V281L300 317M438 256V281L462 317" fill="none" stroke="#cddde7" stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/>
    <rect x="310" y="246" width="27" height="24" rx="6" fill="#9fb7c8"/>
    <ellipse cx="323.5" cy="246" rx="13.5" ry="9.45" fill="#dce8f0"/>
    <rect x="425" y="246" width="27" height="24" rx="6" fill="#9fb7c8"/>
    <ellipse cx="438.5" cy="246" rx="13.5" ry="9.45" fill="#dce8f0"/>
    <rect x="473" y="201" width="9" height="15" rx="2" fill="#c5e5f5"/>
  </g>
''')
east.extend(list(base))
vessel(east, 125, 'east')
near = fragment('''
  <g id="near-manifold">
    <path d="M326 540V511L303 480M441 540V511L465 480" fill="none" stroke="#6e8a9f" stroke-width="18" stroke-linecap="round" stroke-linejoin="round"/>
    <path d="M323 536V507L300 476M438 536V507L462 476" fill="none" stroke="#cddde7" stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/>
    <path d="M237 532L253 518H506V630H237Z" fill="#adbfcc"/>
    <path d="M246 536L257 527H497V621H246Z" fill="#8fa6b7"/>
    <path d="M253 538H486" stroke="#cbdbe6" stroke-width="2"/>
    <path d="M259 554H286M259 565H286M259 576H286" stroke="#d2e2ec" stroke-width="4"/>
    <rect x="310" y="532" width="27" height="24" rx="6" fill="#9fb7c8"/>
    <ellipse cx="323.5" cy="532" rx="13.5" ry="9.45" fill="#dce8f0"/>
    <rect x="425" y="532" width="27" height="24" rx="6" fill="#9fb7c8"/>
    <ellipse cx="438.5" cy="532" rx="13.5" ry="9.45" fill="#dce8f0"/>
    <rect x="473" y="590" width="9" height="15" rx="2" fill="#c5e5f5"/>
  </g>
  <g id="side-service">
    <path d="M542 157H596V643H542Z" fill="#bacbd7"/>
    <path d="M547 164V635" stroke="#e9f1f6" stroke-width="2"/>
    <rect x="555" y="512" width="29" height="117" rx="4" fill="#93afc3"/>
    <rect x="559" y="516" width="21" height="109" rx="2" fill="#344e64"/>
    <path d="M569 525V545M569 555V574M569 603V615" stroke="#c5e5f5" stroke-width="2.5"/>
    <path d="M551 317H596V483H551Z" fill="#8ea9bd"/>
    <path d="M552 322H565V478H552ZM575 322H588V478H575Z" fill="#d3e1ea"/>
    <path d="M555 328V472M578 328V472" stroke="#eaf3f8" stroke-width="2"/>
    <path d="M567 322V478M590 322V478" stroke="#7c99ad" stroke-width="2.5"/>
    <rect x="555" y="178" width="29" height="103" rx="4" fill="#d2e0e9"/>
    <path d="M569 191V216M569 226V240" stroke="#9ab3c6" stroke-width="2.5"/>
    <rect x="565" y="260" width="7" height="14" rx="2" fill="#c5e5f5"/>
    <path d="M198 642H512" stroke="#b7cbd8" stroke-width="2"/>
    <path d="M315 651H453" stroke="#8da7ba" stroke-width="3" stroke-linecap="round"/>
    <rect x="343" y="673" width="82" height="6" rx="3" fill="#344e64"/>
    <path d="M355 676H379M401 676H414" stroke="#c5e5f5" stroke-width="1.5" stroke-linecap="round"/>
  </g>
  <rect x="170.6667" y="137.5" width="426.6666" height="549" rx="20" fill="none" stroke="#000000" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
''')
east.extend(list(near))

for name, root in [('north', north), ('east', east)]:
    ET.indent(root, space='  ')
    output = ROOT / f'CosmicMechGestator_{name}-v6.svg'
    ET.ElementTree(root).write(output, encoding='utf-8', xml_declaration=False)
    # Upright vessel radius, elevation and lower-seat anchor are invariant.
    seat = next(e for e in root.iter() if e.get('id') == 'vessel-seat')
    expected_y = 275 if name == 'north' else 400
    shift_y = float(seat.get('transform').split()[-1].rstrip(')'))
    assert 302 + shift_y == expected_y
    assert next(e for e in seat if e.tag == tag('ellipse')).get('rx') == '140'
    for e in root.iter():
        if e.get('stroke') == '#000000':
            assert e.get('stroke-width') == '4'
    print(output)

manifest = {
    'version': 6, 'review_only': True, 'game_files_changed': False,
    'approved_source': 'CosmicMechGestator_south-v6.svg',
    'accent': '#C5E5F5', 'exterior_stroke': {'color':'#000000', 'width':4},
    'views': {
        'south': {'svg':'CosmicMechGestator_south-v6.svg', 'viewBox':[0,0,768,512], 'platform_center':[384,275], 'footprint':[3,2]},
        'north': {'svg':'CosmicMechGestator_north-v6.svg', 'viewBox':[0,0,768,512], 'platform_center':[384,275], 'footprint':[3,2]},
        'east': {'svg':'CosmicMechGestator_east-v6.svg', 'viewBox':[0,0,768,768], 'platform_center':[384,400], 'footprint':[2,3], 'west':'mirror east'},
    },
    'vessel': {'seat_rx':140, 'seat_ry':98, 'lid_rx':132, 'lid_ry':92.4, 'lid_elevation_in_image':160},
}
(ROOT / 'directions-v6.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
