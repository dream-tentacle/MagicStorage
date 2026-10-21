from pathlib import Path
import math
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v12.svg')
svg=tree.getroot()
svg.find(f'{{{ns}}}title').text='魔法存储 — 宇宙工作台，第十三稿'
table=next(e for e in svg if e.get('id')=='tabletop')
shift=4*(math.sqrt(2)-1)
x0,x1=316-shift,338-shift
seam=next(e for e in table if e.get('stroke-width')=='2.5')
face=next(e for e in table if e.get('fill')=='#91a1a9')
seam_width=float(seam.get('stroke-width'))
back_y=212
# The seam's visible top and the face's upper-right vertex are one shared point.
top_y=back_y-seam_width/2
face.set('d',f'M{x0:.7f} {top_y+22:g}L{x1:.7f} {top_y:g}V228L{x0:.7f} 250Z')
seam.set('d',f'M64 220L78 234H{x0:.7f}M{x1:.7f} {back_y:g}H430L452 234H690L704 220')
# The former back-face fill starts at x338. Continue that fill to the shared
# vertical edge as well, eliminating the tiny exposed top-plate triangle.
joint_fill=ET.Element(f'{{{ns}}}path',{
    'id':'notch-back-joint-fill',
    'd':f'M{x1:.7f} {top_y:g}H338V228H{x1:.7f}Z',
    'fill':'#657982',
})
table.insert(list(table).index(seam),joint_fill)
assert top_y==210.75
assert abs((top_y+22)-top_y-(x1-x0))<1e-10
surface=next(e for e in table if e.get('href')=='#worktop-surface')
surface.set('fill','#d8dfe3')
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v13.svg',encoding='utf-8')

export=(root/'export_workbench_v12.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v13.cjs').write_text(export.replace('north-v12','north-v13'),encoding='utf-8')
# Use the standard artifact checks; the intentional full worktop recolor makes
# the earlier corner-only pixel-difference assertion inapplicable.
preview=(root/'preview_workbench_v10.py').read_text(encoding='utf-8')
preview=preview.replace('north-v10','north-v13').replace('review-v10','review-v13').replace('Workbench-v10.json','Workbench-v13.json')
preview=preview.replace('north-v8.png','north-v12.png')
preview=preview.replace('宇宙工作台 · 恢复原形，仅提高斜面','宇宙工作台 · 接点修复与底色恢复')
preview=preview.replace('以第八稿为底稿；斜面上沿提高 8 单位，保留横向坐标、下沿和原接缝','接好斜面与横向边线；台面底色恢复为最初的浅灰蓝 #D8DFE3')
preview=preview.replace("detail=image.crop((600,390,700,510)).resize((240,288),Image.Resampling.LANCZOS)","detail=image.crop((650,405,704,467)).resize((240,276),Image.Resampling.LANCZOS)")
preview=preview.replace("'review_only':True", "'worktop_base_color':'#D8DFE3','left_junction':[336.3431458,210.75],'review_only':True")
(root/'preview_workbench_v13.py').write_text(preview,encoding='utf-8')
print(f'Joint point: ({x1:.7f},{top_y:g}); face vertex and visible horizontal seam use identical coordinates.')
print('Worktop base color restored to #D8DFE3.')
