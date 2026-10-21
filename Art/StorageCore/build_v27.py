from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree=ET.parse(ROOT/'StorageCore-v26.svg')
root=tree.getroot()
find=lambda identifier:next(e for e in root.iter() if e.get('id')==identifier)
base=find('base')
wrapper=find('corner-shading')
shadow=wrapper[0]
index=list(base).index(wrapper)
base.remove(wrapper)
base.insert(index,shadow)
defs=root.find(f'{{{NS}}}defs')
defs.remove(find('corner-shadow-only'))
reference=ET.parse(ROOT/'StorageCore-v13.svg').getroot()
original=next(e for e in reference.iter() if e.get('id')=='rounded-base-shadow')
assert shadow.attrib==original.attrib
assert find('exterior-contour').get('stroke-width')=='12'
root.find(f'{{{NS}}}title').text='魔法存储 — 存储控制核心，第二十七稿'
root.find(f'{{{NS}}}desc').text=(
    'The complete v13 bottom shadow is retained, including its very thin '
    'horizontal continuation along the lower edge and its curved corners. '
    'The outward-expanded 12-unit black contour keeps that thin shade visible. '
    'All remaining current components are unchanged.')
ET.indent(root,space='  ')
tree.write(ROOT/'StorageCore-v27.svg',encoding='utf-8',xml_declaration=False)
print(ROOT/'StorageCore-v27.svg')
