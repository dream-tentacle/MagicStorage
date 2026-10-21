from copy import deepcopy
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree = ET.parse(ROOT/'StorageCore-v12.svg')
root = tree.getroot()
old = ET.parse(ROOT/'StorageCore-v11.svg').getroot()
find = lambda doc,identifier: next(e for e in doc.iter() if e.get('id')==identifier)
base = find(root,'base')
# Restore the previous swept corner shadow. Shared clipping retains the
# repaired exterior contour, so its edge cannot open a gap at the corners.
shadow = deepcopy(find(old,'base')[1])
shadow.set('id','rounded-base-shadow')
shadow.set('clip-path','url(#chassis-clip)')
base.remove(base[1])
base.insert(1,shadow)
root.find(f'{{{NS}}}title').text = '魔法存储 — 存储控制核心，第十三稿'
root.find(f'{{{NS}}}desc').text += ' Revision 13 restores the swept lower-corner shadow from v11, clipped to the shared chassis contour; the closed orbit and continuous front seam are retained.'
assert shadow.get('d')==find(old,'base')[1].get('d')
previous = ET.parse(ROOT/'StorageCore-v12.svg').getroot()
for identifier in ('orbit-loop-geometry','orbit-front-geometry','chassis-outline-geometry','front-seam'):
    assert find(root,identifier).attrib==find(previous,identifier).attrib
ET.indent(root,space='  ')
output = ROOT/'StorageCore-v13.svg'
tree.write(output,encoding='utf-8',xml_declaration=False)
print(output)
