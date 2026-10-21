from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree=ET.parse(ROOT/'StorageCore-v20.svg')
root=tree.getroot()
contour=next(e for e in root.iter() if e.get('id')=='exterior-contour')
assert contour.get('stroke')=='#000000' and contour.get('stroke-width')=='4'
contour.set('stroke-width','12')
root.find(f'{{{NS}}}title').text='魔法存储 — 存储控制核心，第二十一稿'
root.find(f'{{{NS}}}desc').text+=' The exterior black contour is 12 units wide, three times the former 4 units; all internal artwork is retained.'
tree.write(ROOT/'StorageCore-v21.svg',encoding='utf-8',xml_declaration=False)
print(ROOT/'StorageCore-v21.svg')
