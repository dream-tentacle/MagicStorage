"""Three similar open clothing bays, with upright garments in every view."""
from copy import deepcopy
from pathlib import Path
import json
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
namespace = 'http://www.w3.org/2000/svg'
ET.register_namespace('', namespace)
tag = lambda name: '{' + namespace + '}' + name
south = ET.parse(root / 'StorageApparelAdapter_south-v1.svg').getroot()
elements = list(south)
start = next(i for i, node in enumerate(elements) if node.tag == tag('rect') and node.get('x') == '71')
end = next(i for i, node in enumerate(elements) if node.tag == tag('rect') and node.get('x') == '460.0')
surface = elements[start:end]
garment_start = next(i for i, node in enumerate(surface) if node.get('d', '').startswith('M370 140'))
garment = surface[garment_start:garment_start+6]
manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))


def element(parent, name, **attrs):
    return ET.SubElement(parent, tag(name), {key: str(value) for key, value in attrs.items()})


def rect(parent, x, y, width, height, fill, rx):
    return element(parent, 'rect', x=x, y=y, width=width, height=height, fill=fill, rx=rx)


def line(parent, data, color, width=2):
    node = element(parent, 'path', d=data, fill='none', stroke=color)
    node.set('stroke-width', str(width))
    node.set('stroke-linecap', 'round')
    node.set('stroke-linejoin', 'round')


def east_surface():
    group = ET.Element(tag('g'))
    rect(group, 98, 70, 296, 592, '#a4b7c5', 17)
    rect(group, 105, 76, 282, 580, '#c4d3de', 12)
    rect(group, 129, 103, 234, 526, '#496579', 8)
    rect(group, 135, 136, 222, 487, '#8da7b9', 5)
    line(group, 'M143 620H349', '#b7cddc', 3)
    line(group, 'M143 258H349', '#bed1df', 9)
    line(group, 'M147 254H345', '#edf4f9', 2)
    clothes = element(group, 'g', transform='translate(-161 100) scale(1.1)')
    for node in garment:
        clothes.append(deepcopy(node))
    rect(group, 115, 127, 8, 463, '#c5e5f5', 2)
    rect(group, 369, 127, 8, 463, '#c5e5f5', 2)
    rect(group, 176, 645, 141, 15, '#c8d6df', 4)
    line(group, 'M188 648H305', '#f1f6f9', 2)

    # Same status strip, access panel and small lower service strip as the broad view.
    rect(group, 98, 708, 296, 228, '#aebfcb', 14)
    rect(group, 105, 702, 282, 224, '#e2e9ee', 12)
    rect(group, 170, 727, 146, 29, '#aebdc6', 5)
    rect(group, 174, 731, 138, 21, '#344a5b', 3)
    line(group, 'M186 741H207M217 741H238', '#c5e5f5', 3)
    line(group, 'M253 741H301', '#91afc3', 2)
    rect(group, 146, 789, 199, 95, '#c3d2dc', 7)
    rect(group, 152, 795, 187, 83, '#e7edf1', 5)
    rect(group, 197, 861, 99, 7, '#a1b7c7', 3)
    rect(group, 146, 894, 199, 23, '#c3d2dc', 5)
    line(group, 'M172 906H218', '#91aabb', 4)
    rect(group, 290, 901, 24, 10, '#c5e5f5', 2)
    return group


for direction, label in [('south', '南'), ('north', '北'), ('east', '东')]:
    stem = f'StorageApparelAdapter_{direction}-v2'
    if direction == 'east':
        result = ET.parse(root / 'StorageApparelAdapter_east-v1.svg').getroot()
        old = list(result)
        first = next(i for i, node in enumerate(old) if node.tag == tag('rect') and node.get('x') == '98')
        last = next(i for i, node in enumerate(old) if node.tag == tag('rect') and node.get('x') == '204.0')
        for node in old[first:last]:
            result.remove(node)
        result.insert(first, east_surface())
    else:
        result = deepcopy(south)
        if direction == 'north':
            old = list(result)
            for node in old[start:end]:
                result.remove(node)
            # Mirror positions left/right, preserving the upright bay, rail and garment.
            mirrored = ET.Element(tag('g'), {'transform': 'translate(1024 0) scale(-1 1)'})
            for node in surface:
                mirrored.append(deepcopy(node))
            result.insert(start, mirrored)
    result.find(tag('title')).text = f'衣物适配器 — {label}向第二稿'
    result.find(tag('desc')).text = 'Original Magic Storage apparel adapter. All three views share an open clothing bay, an upright hanging garment, sky-blue side inserts and a compact service module.'
    ET.indent(result, space='  ')
    text = ET.tostring(result, encoding='unicode') + '\n'
    ET.fromstring(text)
    (root / (stem + '.svg')).write_text(text, encoding='utf-8')
    entry = dict(stem=stem, label=f'衣物适配器 — {label}向第二稿', width=int(result.get('width')), height=int(result.get('height')), category='building')
    manifest = [item for item in manifest if item['stem'] != stem]
    manifest.append(entry)
    print(stem)

(root / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
