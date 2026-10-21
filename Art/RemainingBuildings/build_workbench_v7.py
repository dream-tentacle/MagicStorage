from pathlib import Path
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v6.svg')
svg=tree.getroot()
svg.find(f'{{{ns}}}title').text='魔法存储 — 宇宙工作台，第七稿'
svg.find(f'{{{ns}}}desc').text=(
    'North-facing 3×1 assembly workbench with a slotted work plate, integrated vise '
    'housing and a shallow trapezoid operator recess. A flat display is inset into '
    'the right side of the countertop, with no separate upright monitor or stand. '
    'Sky-blue accents RGB(197,229,245), flat grey metal and a uniform black exterior contour. '
    'Transparent draft, not installed.')
panel=ET.fromstring(f'''<g xmlns="{ns}" id="inset-display">
  <path d="M566 117H663V196H558V125Z" fill="#9baab2"/>
  <path d="M568 121H659V188H562V127Z" fill="#b9c4c8"/>
  <path d="M566 129H654V182H566Z" fill="#344a5b"/>
  <path d="M562 188H659V196H562Z" fill="#8197a4"/>
  <path d="M575 138H608M575 146H597" stroke="#91adbf" stroke-width="3"/>
  <path d="M629 137H645V152H629Z" fill="#c5e5f5"/>
  <path d="M575 166H645V172H575Z" fill="#68889d"/>
  <path d="M575 166H618V172H575Z" fill="#c5e5f5"/>
</g>''')
count=0
for parent in svg.iter():
    for index,element in enumerate(list(parent)):
        if element.get('id')=='tool-well':
            parent.remove(element)
            parent.insert(index,panel)
            count+=1
assert count==1
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v7.svg',encoding='utf-8')

export=(root/'export_workbench_v6.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v7.cjs').write_text(export.replace('north-v6','north-v7'),encoding='utf-8')
preview=(root/'preview_workbench_v6.py').read_text(encoding='utf-8')
preview=preview.replace('north-v6','north-v7').replace('review-v6','review-v7').replace('Workbench-v6.json','Workbench-v7.json')
preview=preview.replace('整合式台钳底座 · 带导槽的装配板 · 嵌入式工具位','保留台钳与导槽装配板 · 右侧改回嵌入式显示屏')
preview=preview.replace('mechanical vise and recessed hand tools, no monitor','mechanical vise and a flat inset display without an upright stand')
(root/'preview_workbench_v7.py').write_text(preview,encoding='utf-8')
print(root/'CosmicWorkbench_north-v7.svg')
