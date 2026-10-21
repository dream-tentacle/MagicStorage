from pathlib import Path
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v3.svg')
svg=tree.getroot()
svg.find(f'{{{ns}}}title').text='魔法存储 — 宇宙工作台，第四稿'
svg.find(f'{{{ns}}}desc').text=(
    'North-facing 3×1 workbench with concealed supports and a clean chamfered silhouette. '
    'The operator edge has a shallow centered trapezoid recess. An open assembly mat, '
    'a small screw vise and a tilted terminal define the worktop. '
    'RGB(197,229,245) accents and uniform black exterior contour. Transparent review artwork.')
for element in list(svg):
    if element.get('id')=='pedestals':
        svg.remove(element)
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v4.svg',encoding='utf-8')

export=(root/'export_workbench_v3.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v4.cjs').write_text(export.replace('north-v3','north-v4'),encoding='utf-8')
preview=(root/'preview_workbench_v3.py').read_text(encoding='utf-8')
preview=preview.replace('north-v3','north-v4').replace('review-v3','review-v4').replace('Workbench-v3.json','Workbench-v4.json')
preview=preview.replace("assert len(black)==3", "assert len(black)==1")
preview=preview.replace("assert sprite.getpixel((150*2,265*2))[3]==255", "assert sprite.getpixel((150*2,265*2))[3]==0")
preview=preview.replace('薄台面与独立支撑 · 中央梯形凹口 · 台钳与倾斜终端','收起凸出支脚 · 保留中央梯形凹口 · 台钳与倾斜终端')
preview=preview.replace('chamfered worktop on two separate pedestals with open knee space','chamfered worktop with concealed supports and a shallow central recess')
(root/'preview_workbench_v4.py').write_text(preview,encoding='utf-8')
print(root/'CosmicWorkbench_north-v4.svg')
