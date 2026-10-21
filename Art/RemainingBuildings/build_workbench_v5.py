from pathlib import Path
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v4.svg')
svg=tree.getroot()
svg.find(f'{{{ns}}}title').text='魔法存储 — 宇宙工作台，第五稿'
svg.find(f'{{{ns}}}desc').text=(
    'North-facing 3×1 workbench with concealed supports and a centered shallow trapezoid recess. '
    'An open assembly mat, a small screw vise and two flush mechanical sliders define the worktop. '
    'No standing monitor, screen or monitor base. RGB(197,229,245) accents and uniform black exterior contour. '
    'Transparent review artwork.')
terminal=next(element for element in svg if element.get('id')=='terminal')
svg.remove(terminal)
controls=ET.fromstring(f'''<g xmlns="{ns}" id="flush-controls">
  <path d="M571 120H645L653 128V178H563V128Z" fill="#a6b3ba"/>
  <path d="M571 120H645L653 128H563Z" fill="#d8dddc"/>
  <path d="M563 174H653V178H563Z" fill="#8fa2ad"/>
  <path d="M578 132H588V165H578ZM610 132H620V165H610Z" fill="#536b7a"/>
  <rect x="575" y="136" width="16" height="9" rx="1" fill="#c5e5f5"/>
  <path d="M575 145H591V148H575Z" fill="#7c919d"/>
  <rect x="607" y="151" width="16" height="9" rx="1" fill="#becbd2"/>
  <path d="M607 160H623V163H607Z" fill="#7c919d"/>
</g>''')
svg.append(controls)
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v5.svg',encoding='utf-8')
assert not any(element.get('id')=='terminal' for element in svg)

export=(root/'export_workbench_v4.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v5.cjs').write_text(export.replace('north-v4','north-v5'),encoding='utf-8')
preview=(root/'preview_workbench_v4.py').read_text(encoding='utf-8')
preview=preview.replace('north-v4','north-v5').replace('review-v4','review-v5').replace('Workbench-v4.json','Workbench-v5.json')
preview=preview.replace('宇宙工作台 · 重新设计','宇宙工作台 · 操作区调整')
preview=preview.replace('收起凸出支脚 · 保留中央梯形凹口 · 台钳与倾斜终端','取消立式显示器 · 嵌入式机械拨杆 · 保留中央梯形凹口')
preview=preview.replace("'review_only':True", "'controls':'flush mechanical sliders, no monitor','review_only':True")
(root/'preview_workbench_v5.py').write_text(preview,encoding='utf-8')
print(root/'CosmicWorkbench_north-v5.svg')
