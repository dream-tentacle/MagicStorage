"""Recolor only the directional arrow in the three approved supplier SVGs."""
from pathlib import Path
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
namespace='http://www.w3.org/2000/svg'
ET.register_namespace('',namespace)
tag=lambda name:'{'+namespace+'}'+name
for direction in ['south','north','east']:
    svg=ET.parse(root/f'TransportSupplier_{direction}-v3.svg').getroot()
    changed=0
    for node in svg.iter(tag('path')):
        if node.get('stroke-width')=='13' and node.get('stroke')=='#c5e5f5':
            node.set('stroke','#204570')
            changed+=1
        elif node.get('d')=='M256 184V269' and node.get('stroke')=='#e4f3fb':
            node.set('stroke','#4a7299')
            changed+=1
    assert changed==2,(direction,changed)
    svg.find(tag('title')).text=svg.find(tag('title')).text.replace('第三稿','第四稿')
    svg.find(tag('desc')).text='Material supplier with a deep-blue recessed directional arrow. Original pearl-grey housing, sky-blue frame, geometry and uniform black exterior contour are preserved.'
    ET.indent(svg,space='  ')
    destination=root/f'TransportSupplier_{direction}-v4.svg'
    destination.write_text(ET.tostring(svg,encoding='unicode')+'\n',encoding='utf-8')
    print(destination)
