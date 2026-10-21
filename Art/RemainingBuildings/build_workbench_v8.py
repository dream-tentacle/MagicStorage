from pathlib import Path
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v7.svg')
svg=tree.getroot()
svg.find(f'{{{ns}}}title').text='魔法存储 — 宇宙工作台，第八稿'
svg.find(f'{{{ns}}}desc').text=(
    'North-facing 3×1 assembly workbench with a plain uninterrupted working plate, '
    'integrated vise housing, an inset display and a shallow trapezoid operator recess. '
    'Sky-blue accents RGB(197,229,245), flat grey metal and a uniform black exterior contour. '
    'Transparent draft, not installed.')
plate=next(element for element in svg.iter() if element.get('id')=='slotted-assembly-plate')
plate.set('id','assembly-plate')
for element in list(plate):
    if element.get('d','').startswith('M265 '):
        plate.remove(element)
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v8.svg',encoding='utf-8')

export=(root/'export_workbench_v7.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v8.cjs').write_text(export.replace('north-v7','north-v8'),encoding='utf-8')
preview=(root/'preview_workbench_v7.py').read_text(encoding='utf-8')
preview=preview.replace('north-v7','north-v8').replace('review-v7','review-v8').replace('Workbench-v7.json','Workbench-v8.json')
preview=preview.replace('保留台钳与导槽装配板 · 右侧改回嵌入式显示屏','中央改为完整工作面 · 保留台钳、嵌入式显示屏与梯形凹口')
(root/'preview_workbench_v8.py').write_text(preview,encoding='utf-8')
print(root/'CosmicWorkbench_north-v8.svg')
