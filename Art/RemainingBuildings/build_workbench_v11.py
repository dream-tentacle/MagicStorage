"""Fill only the left face up to the upper edge shown in the user's red outline."""
from pathlib import Path
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v8.svg')
svg=tree.getroot()
table=next(e for e in svg if e.get('id')=='tabletop')
left=next(e for e in table if e.get('d')=='M316 234L338 212V228L316 250Z')
# Preserve both X coordinates, both lower Y coordinates and the vertical ends.
# The red upper edge follows the upper edge of the old 2.5-unit seam.
left.set('d','M316 232L338 210V228L316 250Z')
table.remove(left)
seam=next(e for e in table if e.get('stroke-width')=='2.5')
# The restored seam remains everywhere else. Only its part under the marked
# face is covered, so it does not split the requested continuous light face.
table.insert(list(table).index(seam)+1,left)
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v11.svg',encoding='utf-8')

export=(root/'export_workbench_v8.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v11.cjs').write_text(export.replace('north-v8','north-v11'),encoding='utf-8')
preview=(root/'preview_workbench_v10.py').read_text(encoding='utf-8')
preview=preview.replace('north-v10','north-v11').replace('review-v10','review-v11').replace('Workbench-v10.json','Workbench-v11.json')
preview=preview.replace('宇宙工作台 · 恢复原形，仅提高斜面','宇宙工作台 · 按红框补齐左侧斜面')
preview=preview.replace('以第八稿为底稿；斜面上沿提高 8 单位，保留横向坐标、下沿和原接缝','左侧浅色面补至原深色斜边上沿；其余部分恢复第八稿')
check='''
from PIL import ImageChops
old=Image.open(ROOT/'CosmicWorkbench_north-v8.png').convert('RGBA')
assert ImageChops.difference(sprite.getchannel('A'),old.getchannel('A')).getbbox() is None
delta=ImageChops.difference(sprite.convert('RGB'),old.convert('RGB'))
changed=delta.getbbox()
assert changed is not None
assert 628<=changed[0] and changed[2]<=682 and 412<=changed[1] and changed[3]<=506,changed
report['changed_pixel_bounds']=changed
report['correction']='left face only, following the user red outline'
'''
preview=preview.replace("(ROOT/'CosmicWorkbench-v11.json').write_text",check+"\n(ROOT/'CosmicWorkbench-v11.json').write_text")
(root/'preview_workbench_v11.py').write_text(preview,encoding='utf-8')
print('Restored v8. Changed only the upper height of the marked left face, and made that face continuous over the old seam.')
