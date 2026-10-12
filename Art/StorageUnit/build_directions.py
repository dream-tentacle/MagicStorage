"""Complete the two missing views without changing the approved south study."""
from copy import deepcopy
from pathlib import Path
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('', NS)
tag = lambda value: '{' + NS + '}' + value
south = ET.parse(ROOT / 'StorageUnit_south-v2.svg').getroot()
manifest = [dict(stem='StorageUnit_south-v2', width=512, height=768)]


def node(parent, name, **attrs):
    return ET.SubElement(parent, tag(name), {key.replace('_', '-'): str(value) for key, value in attrs.items()})


def rect(parent, x, y, w, h, fill, radius=0):
    return node(parent, 'rect', x=x, y=y, width=w, height=h, fill=fill, rx=radius)


def line(parent, path, stroke, width=2):
    return node(parent, 'path', d=path, fill='none', stroke=stroke, stroke_width=width, stroke_linecap='round', stroke_linejoin='round')


def save(svg, stem, title):
    svg.find(tag('title')).text = title
    svg.find(tag('desc')).text = 'Original Magic Storage 1×2 storage unit. Two closed interchangeable cartridge lids, restrained RGB(197,229,245) sky-blue accents and a uniform pure-black exterior contour. Casing depth and highlights remain consistent between views.'
    ET.indent(svg, space='  ')
    text = ET.tostring(svg, encoding='unicode') + '\n'
    ET.fromstring(text)
    (ROOT / (stem+'.svg')).write_text(text, encoding='utf-8')
    manifest.append(dict(stem=stem, width=int(svg.get('width')), height=int(svg.get('height'))))
    print(stem)


# North: the same cabinet, handles towards the far edge, controls at the other end.
north = deepcopy(south)
old_defs = north.find(tag('defs'))
north.remove(old_defs)
defs = node(north, 'defs')
lid = node(defs, 'g', id='north-cartridge')
rect(lid, 154, 171, 204, 164, '#a9bbc6', 9)
panel = rect(lid, 157, 174, 198, 152, '#e1e7eb', 7)
panel.set('stroke', '#b3c1ca')
panel.set('stroke-width', '1.5')
line(lid, 'M168 177H343', '#f4f7f8', 2)
line(lid, 'M351 184V314Q351 323 341 323H166', '#c7d2d9', 2)
rect(lid, 337, 190, 10, 114, '#c5e5f5')
line(lid, 'M339 193V295', '#e7f4fb', 1.5)
rect(lid, 231, 190, 49, 8, '#aabdc9', 2)
rect(lid, 235, 193, 41, 3, '#728e9f', 1)
line(lid, 'M157 327H355', '#8fa6b4', 2)
cartridges = north.find(".//*[@id='storage-cartridges']")
cartridges.clear()
cartridges.set('id', 'storage-cartridges')
node(cartridges, 'use', href='#north-cartridge')
node(cartridges, 'use', href='#north-cartridge', transform='translate(0 183)')
rear = north.find(".//*[@id='rear-panel']")
rear.set('transform', 'translate(-28 421)')
apron = north.find(".//*[@id='service-apron']")
apron.set('transform', 'translate(-164 -428)')
save(north, 'StorageUnit_north-v2', '魔法存储 — 存储单元，北向精简版')


# East: two identical cartridges side by side, with a shallow foreground casing face.
east = ET.Element(tag('svg'), dict(width='768', height='512', viewBox='0 0 768 512'))
node(east, 'title')
node(east, 'desc')
east.append(deepcopy(south.find(tag('defs'))))
base = node(east, 'g', id='base')
rect(base, 124, 152, 520, 228, '#798893', 24)
line(base, 'M126 348Q126 377 152 378H616Q642 377 642 348', '#52616c', 6)
rect(base, 328, 363, 113, 8, '#344756', 4)
line(base, 'M340 367H354M361 367H375M382 367H396', '#a2bcca', 1.8)
line(base, 'M415 367H429', '#c5e5f5', 2.7)

housing = node(east, 'g', id='housing')
rect(housing, 124, 132, 520, 226, '#d8dfe3', 24)
line(housing, 'M124 334Q124 358 148 358H620Q644 358 644 334', '#25313c', 4)
line(housing, 'M134 264V160Q134 143 152 143H616Q631 143 633 158', '#f2f5f6', 2.5)
line(housing, 'M634 170V327Q634 348 614 348H151', '#a8b6bf', 4)

surface = node(east, 'g', id='operating-surface', transform='translate(20 507) rotate(-90)')
for name in ['rear-panel', 'storage-cartridges', 'service-apron']:
    surface.append(deepcopy(south.find(f".//*[@id='{name}']")))
line(east, 'M148 132H620Q644 132 644 156V356Q644 380 620 380H148Q124 380 124 356V156Q124 132 148 132Z', '#000000', 4)
save(east, 'StorageUnit_east-v2', '魔法存储 — 存储单元，东向精简版')

(ROOT / 'directions-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
