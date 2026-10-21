"""Keep the swept bottom shadow visible inside the thicker black outline."""
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree=ET.parse(ROOT/'StorageCore-v21.svg')
root=tree.getroot()
find=lambda identifier:next(e for e in root.iter() if e.get('id')==identifier)
shadow=find('rounded-base-shadow')
# The old inner shadow edge at y442 was underneath the 12-unit outline,
# whose inner edge is at y440. Lift the swept inner edge, keeping its
# rounded transition and original outer boundary attached to the chassis.
shadow.set('d','M66 397Q69 431 103 434H409Q443 431 446 397V420Q441 446 412 446H100Q70 444 66 420Z')
assert shadow.get('fill')=='#414e58'
assert shadow.get('clip-path')=='url(#chassis-clip)'
assert find('exterior-contour').get('stroke-width')=='12'
root.find(f'{{{NS}}}title').text='魔法存储 — 存储控制核心，第二十二稿'
root.find(f'{{{NS}}}desc').text+=' The swept bottom shadow extends farther into the visible front face so it remains visible inside the 12-unit contour, retaining its curved corners and original color.'
tree.write(ROOT/'StorageCore-v22.svg',encoding='utf-8',xml_declaration=False)
print(ROOT/'StorageCore-v22.svg')
