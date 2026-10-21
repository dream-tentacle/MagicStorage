from pathlib import Path
import math
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v13.svg')
svg=tree.getroot()
svg.find(f'{{{ns}}}title').text='魔法存储 — 宇宙工作台，第十四稿'
table=next(e for e in svg if e.get('id')=='tabletop')
shift=4*(math.sqrt(2)-1)
left_outer,left_inner=316-shift,338-shift
right_inner,right_outer=768-left_inner,768-left_outer
top_y=210.75
seam=next(e for e in table if e.get('stroke-width')=='2.5')
right=next(e for e in table if e.get('d')=='M430 212L452 234V250L430 228Z')
right.set('d',f'M{right_inner:.7f} {top_y:g}L{right_outer:.7f} {top_y+22:g}V250L{right_inner:.7f} 228Z')
table.remove(right)
seam.set('d',f'M64 220L78 234H{left_outer:.7f}M{left_inner:.7f} 212H{right_inner:.7f}M{right_outer:.7f} 234H690L704 220')
joint=ET.Element(f'{{{ns}}}path',{
    'id':'notch-right-back-joint-fill',
    'd':f'M430 {top_y:g}H{right_inner:.7f}V228H430Z',
    'fill':'#657982',
})
table.insert(list(table).index(seam),joint)
table.insert(list(table).index(seam)+1,right)
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v14.svg',encoding='utf-8')

export=(root/'export_workbench_v13.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v14.cjs').write_text(export.replace('north-v13','north-v14'),encoding='utf-8')
preview=(root/'preview_workbench_v13.py').read_text(encoding='utf-8')
preview=preview.replace('north-v13','north-v14').replace('review-v13','review-v14').replace('Workbench-v13.json','Workbench-v14.json')
preview=preview.replace('north-v12.png','north-v13.png')
preview=preview.replace('宇宙工作台 · 接点修复与底色恢复','宇宙工作台 · 右侧斜面同步修复')
preview=preview.replace('接好斜面与横向边线；台面底色恢复为最初的浅灰蓝 #D8DFE3','右侧按左侧相同的几何关系补齐，接好上下接点；保留浅灰蓝底色')
preview=preview.replace('crop((650,405,704,467)).resize((240,276)','crop((848,405,924,505)).resize((216,284)')
preview=preview.replace('(x+142,720)','(x+154,720)')
check='''
from PIL import ImageChops
old=Image.open(ROOT/'CosmicWorkbench_north-v13.png').convert('RGBA')
assert ImageChops.difference(sprite.getchannel('A'),old.getchannel('A')).getbbox() is None
changed=ImageChops.difference(sprite.convert('RGB'),old.convert('RGB')).getbbox()
assert changed and 856<=changed[0] and changed[2]<=913 and 416<=changed[1] and changed[3]<=505,changed
report['changed_pixel_bounds']=changed
report['right_junction']=[431.6568542,210.75]
'''
preview=preview.replace("(ROOT/'CosmicWorkbench-v14.json').write_text",check+"\n(ROOT/'CosmicWorkbench-v14.json').write_text")
(root/'preview_workbench_v14.py').write_text(preview,encoding='utf-8')
print('Right face now mirrors the corrected left geometry; its upper and lower junctions share the matching contour coordinates.')
