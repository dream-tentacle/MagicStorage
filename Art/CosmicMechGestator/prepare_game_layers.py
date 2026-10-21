"""Separate approved SVG views for the native gestator's three render passes."""
from copy import deepcopy
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / 'GameLayers'
OUTPUT.mkdir(exist_ok=True)
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('', NS)

for direction in ('south', 'north', 'east'):
    source = ET.parse(ROOT / f'CosmicMechGestator_{direction}-v6.svg').getroot()
    layers = {}
    for name in ('Base', 'Glass', 'Top'):
        layer = ET.Element(source.tag, source.attrib)
        layer.append(deepcopy(source.find(f'{{{NS}}}defs')))
        layers[name] = layer
    for element in source:
        if element.tag.rsplit('}',1)[-1] in ('defs','title','desc'):
            continue
        identifier = element.get('id')
        if identifier == 'cylinder-glass':
            target = 'Glass'
        elif identifier in ('gestator-top', 'near-manifold'):
            target = 'Top'
        else:
            target = 'Base'
        if element is list(source)[-1] and direction != 'east':
            # The rear platform edge is hidden by the lid. Its exposed arc is
            # part of the upper pass, so forming mechs remain behind the lid.
            ET.SubElement(layers['Base'], f'{{{NS}}}rect', dict(
                x='64',y='100',width='640',height='374',rx='20',fill='none',
                stroke='#000000',**{'stroke-width':'4','stroke-linejoin':'round'}))
            ET.SubElement(layers['Top'], f'{{{NS}}}path', dict(
                d='M253.7509 100A132 92.4 0 0 1 514.2491 100',fill='none',
                stroke='#000000',**{'stroke-width':'4','stroke-linecap':'round'}))
        else:
            layers[target].append(deepcopy(element))
    for name, layer in layers.items():
        ET.indent(layer,space='  ')
        ET.ElementTree(layer).write(OUTPUT / f'{name}_{direction}.svg',encoding='utf-8')
print('Prepared 9 SVG layers from the approved south/north/east views.')
