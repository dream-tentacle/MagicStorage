from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree=ET.parse(ROOT/'StorageCore-v22.svg')
root=tree.getroot()
shadow=next(e for e in root.iter() if e.get('id')=='rounded-base-shadow')
# Retain the curved corner portions; taper their inner ends beneath the
# contour, leaving the whole straight middle section in the base color.
shadow.set('d',
    'M66 397Q69 431 103 434Q115 437 128 440V446H100Q70 444 66 420Z '
    'M384 440Q397 437 409 434Q443 431 446 397V420Q441 446 412 446H384Z')
root.find(f'{{{NS}}}title').text='魔法存储 — 存储控制核心，第二十三稿'
root.find(f'{{{NS}}}desc').text+=' Bottom-face shading is confined to the two rounded corners, tapering away before the straight middle edge.'
tree.write(ROOT/'StorageCore-v23.svg',encoding='utf-8',xml_declaration=False)
print(ROOT/'StorageCore-v23.svg')
