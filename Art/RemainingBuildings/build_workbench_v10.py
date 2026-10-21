"""Restore v8; change only the upper height of the existing diagonal faces."""
from pathlib import Path
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v8.svg')
svg=tree.getroot()
RAISE=8
table=next(e for e in svg if e.get('id')=='tabletop')
seam=next(e for e in table if e.get('stroke-width')=='2.5')
changes={
    'M316 234L338 212V228L316 250Z':f'M316 {234-RAISE}L338 {212-RAISE}V228L316 250Z',
    'M430 212L452 234V250L430 228Z':f'M430 {212-RAISE}L452 {234-RAISE}V250L430 228Z',
}
faces=[]
for e in list(table):
    if e.get('d') in changes:
        e.set('d',changes[e.get('d')])
        table.remove(e)
        faces.append(e)
assert len(faces)==2
# Place the raised parts above the top-plate fill so the extra height is visible.
# The existing seam and exterior contour remain above these faces, unchanged.
index=list(table).index(seam)
for face in faces:
    table.insert(index,face)
    index+=1
assert seam.get('d')=='M64 220L78 234H316L338 212H430L452 234H690L704 220'
assert not any(e.get('id')=='continuous-notch-faces' for e in svg.iter())
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v10.svg',encoding='utf-8')

export=(root/'export_workbench_v8.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v10.cjs').write_text(export.replace('north-v8','north-v10'),encoding='utf-8')
preview=(root/'preview_workbench_v9.py').read_text(encoding='utf-8')
preview=preview.replace('north-v9','north-v10').replace('review-v9','review-v10').replace('Workbench-v9.json','Workbench-v10.json')
preview=preview.replace('宇宙工作台 · 缺口斜面修正','宇宙工作台 · 恢复原形，仅提高斜面')
preview=preview.replace('缺口斜面向上接至台面倒角边缘，去掉中间截断斜面的接缝','以第八稿为底稿；斜面上沿提高 8 单位，保留横向坐标、下沿和原接缝')
(root/'preview_workbench_v10.py').write_text(preview,encoding='utf-8')
print('Restored v8 geometry and seam; changed only the two upper Y coordinates of each side face by -8.')
