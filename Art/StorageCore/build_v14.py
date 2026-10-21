from copy import deepcopy
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('',NS)
tree = ET.parse(ROOT/'StorageCore-v13.svg')
root = tree.getroot()
reference = ET.parse(ROOT/'StorageCore-v10.svg').getroot()
find = lambda doc,identifier: next(e for e in doc.iter() if e.get('id')==identifier)
old_controller = find(root,'controller')
index = list(root).index(old_controller)
root.remove(old_controller)
root.insert(index,deepcopy(find(reference,'controller')))
root.find(f'{{{NS}}}title').text = '魔法存储 — 存储控制核心，第十四稿'
root.find(f'{{{NS}}}desc').text += ' Revision 14 restores the original rounded controller with three connected sky-blue network nodes.'
previous = ET.parse(ROOT/'StorageCore-v13.svg').getroot()
for group in previous:
    identifier = group.get('id')
    if identifier and identifier != 'controller':
        assert ET.tostring(group)==ET.tostring(find(root,identifier))
assert ET.tostring(find(root,'controller'))==ET.tostring(find(reference,'controller'))
ET.indent(root,space='  ')
output = ROOT/'StorageCore-v14.svg'
tree.write(output,encoding='utf-8',xml_declaration=False)
print(output)
