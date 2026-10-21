from pathlib import Path
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v8.svg')
svg=tree.getroot()
svg.find(f'{{{ns}}}title').text='魔法存储 — 宇宙工作台，第九稿'
svg.find(f'{{{ns}}}desc').text=(
    'North-facing 3×1 assembly workbench. The two diagonal faces and the rear face '
    'of the shallow trapezoid recess continue to the upper countertop bevel edge, '
    'with no seam interrupting the sloping faces. Working plate, vise, inset display '
    'and exterior silhouette retain their previous geometry. Transparent draft, not installed.')
table=next(element for element in svg if element.get('id')=='tabletop')
for element in list(table):
    if element.get('d') in ('M430 212L452 234V250L430 228Z','M316 234L338 212V228L316 250Z'):
        table.remove(element)
seam=next(element for element in table if element.get('stroke-width')=='2.5')
# Stop the front seam at the side-face junctions. Do not cut through the faces.
seam.set('d','M64 220L78 234H313.3333M454.6667 234H690L704 220')
faces=ET.fromstring(f'''<g xmlns="{ns}" id="continuous-notch-faces">
  <path id="notch-back-face" d="M334 204H434L430 228H338Z" fill="#657982"/>
  <path id="notch-left-face" d="M312 226L334 204L338 228L316 250Z" fill="#91a1a9"/>
  <path id="notch-right-face" d="M434 204L456 226L452 250L430 228Z" fill="#465d6b"/>
</g>''')
table.insert(list(table).index(seam),faces)
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v9.svg',encoding='utf-8')

export=(root/'export_workbench_v8.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v9.cjs').write_text(export.replace('north-v8','north-v9'),encoding='utf-8')
preview=(root/'preview_workbench_v8.py').read_text(encoding='utf-8')
preview=preview.replace('north-v8','north-v9').replace('review-v8','review-v9').replace('Workbench-v8.json','Workbench-v9.json')
preview=preview.replace('(1120,820)','(1120,1040)')
preview=preview.replace('宇宙工作台 · 装配台面方案','宇宙工作台 · 缺口斜面修正')
preview=preview.replace('中央改为完整工作面 · 保留台钳、嵌入式显示屏与梯形凹口','缺口斜面向上接至台面倒角边缘，去掉中间截断斜面的接缝')
start=preview.index("draw.text((28,624),'缩小显示'")
end=preview.index("output=ROOT/",start)
preview=preview[:start]+'''draw.text((28,624),'缺口局部对比',font=label,fill='#e5edf1')
for x,image,name in [(24,Image.open(ROOT/'CosmicWorkbench_north-v8.png').convert('RGBA'),'修改前'),(572,sprite,'修改后')]:
    draw.rectangle((x,670,x+523,1015),fill='#d7ddda')
    draw.text((x+18,680),name,font=label,fill='#344a5b')
    detail=image.crop((600,390,700,510)).resize((240,288),Image.Resampling.LANCZOS)
    sheet.alpha_composite(detail,(x+142,720))
'''+preview[end:]
(root/'preview_workbench_v9.py').write_text(preview,encoding='utf-8')
print(root/'CosmicWorkbench_north-v9.svg')
