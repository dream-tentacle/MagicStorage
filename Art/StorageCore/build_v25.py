from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent
NS='http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree=ET.parse(ROOT/'StorageCore-v24.svg')
root=tree.getroot()
find=lambda identifier:next(e for e in root.iter() if e.get('id')==identifier)
# A full 50-unit quarter-circle gives the shade a longer, rounder inside
# turn, with vertical and horizontal tangents at its ends. The outside
# boundary still follows the existing chassis and its 12-unit contour.
find('rounded-base-shadow').set('d',
    'M64 390V410Q64 446 100 446H120V440'
    'C92.3857625 440 70 417.6142375 70 390Z '
    'M448 390V410Q448 446 412 446H392V440'
    'C419.6142375 440 442 417.6142375 442 390Z')
assert find('exterior-contour').get('stroke-width')=='12'
root.find(f'{{{NS}}}title').text='魔法存储 — 存储控制核心，第二十五稿'
root.find(f'{{{NS}}}desc').text=(
    'The two lower corner shades use fuller circular inner arcs, sweeping '
    'from the vertical sides onto the bottom face. The straight middle '
    'edge stays unshaded. The chassis contour and remaining art are retained.')
tree.write(ROOT/'StorageCore-v25.svg',encoding='utf-8',xml_declaration=False)
print(ROOT/'StorageCore-v25.svg')
