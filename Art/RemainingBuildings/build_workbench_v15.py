"""Build the notch surfaces from shared vertices instead of joint patches."""
from pathlib import Path
import math
import json
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v14.svg')
svg=tree.getroot()
svg.find(f'{{{ns}}}title').text='魔法存储 — 宇宙工作台，第十五稿'
svg.find(f'{{{ns}}}desc').text=(
    'North-facing 3×1 assembly workbench. The top-plate boundary, front bevel, '
    'front fascia and both notch faces use one shared set of junction vertices. '
    'No joint-fill patches or detached seam segments. Original exterior contour, '
    'vise, working plate, display and light blue-grey worktop color retained. '
    'Transparent draft, not installed.')
defs=svg.find(f'{{{ns}}}defs')
table=next(e for e in svg if e.get('id')=='tabletop')
stroke=8
shift=stroke/2*(math.sqrt(2)-1)
xa,xb=316-shift,338-shift
xc,xd=768-xb,768-xa
front_y,back_y=232.75,210.75
A=(xa,front_y)
B=(xb,back_y)
C=(xc,back_y)
D=(xd,front_y)
a=(xa,250)
b=(xb,228)
c=(xc,228)
d=(xd,250)

def number(value):
    return f'{value:.7f}'.rstrip('0').rstrip('.')

def polygon(points):
    return 'M'+'L'.join(' '.join(number(v) for v in p) for p in points)+'Z'

def element(name,**attrs):
    return ET.Element(f'{{{ns}}}{name}',{k.replace('_','-'):str(v) for k,v in attrs.items()})

# Exact same top edge is used by the top plate, fascia, bevel and side faces.
top_front=[(64,220),(78,front_y),A,B,C,D,(690,front_y),(704,220)]
top_boundary=[(80,68),(688,68),(704,84),*list(reversed(top_front)),(64,84)]
surface=next(e for e in defs if e.get('id')=='worktop-surface')
surface.set('d',polygon(top_boundary))

# Lower points remain behind the original black silhouette; the outline itself
# is untouched. Each side face shares its full vertical edge with the fascia.
fascia_boundary=[*top_front,(704,236),(690,250),d,c,b,a,(78,250),(64,236)]
left_boundary=[A,B,b,a]
right_boundary=[C,D,d,c]

# A constant-width mitered bevel connects to those same four top vertices.
inset=front_y-226
diagonal_constant=xa+front_y-math.sqrt(2)*inset
inner_a=(diagonal_constant-226,226)
inner_b=(diagonal_constant-204,204)
inner_c=(768-inner_b[0],204)
inner_d=(768-inner_a[0],226)
bevel_boundary=[(695,84),(704,84),*list(reversed(top_front)),
                (84,226),inner_a,inner_b,inner_c,inner_d,(686,226),(695,217)]

for e in list(table):
    table.remove(e)
table.append(element('use',href='#worktop-body',fill='#7d8c93'))
table.append(element('path',id='front-fascia',d=polygon(fascia_boundary),fill='#657982'))
table.append(element('path',id='left-notch-face',d=polygon(left_boundary),fill='#91a1a9'))
table.append(element('path',id='right-notch-face',d=polygon(right_boundary),fill='#465d6b'))
table.append(element('use',href='#worktop-surface',fill='#d8dfe3'))
table.append(element('path',id='top-bevel',d='M80 68H688L704 84H64Z',fill='#e0e4e1'))
table.append(element('path',id='left-bevel',d=polygon([(64,84),(73,84),(73,218),(84,228),(78,front_y),(64,220)]),fill='#dce1de'))
table.append(element('path',id='front-right-bevel',d=polygon(bevel_boundary),fill='#aab6ba'))
table.append(element('path',d='M570 238H657V244H570Z',fill='#c5e5f5'))
table.append(element('use',href='#worktop-body',fill='none',stroke='#000000',stroke_width='8',stroke_linejoin='round'))

for point in (A,B):
    assert point in top_boundary and point in fascia_boundary and point in left_boundary and point in bevel_boundary
for point in (C,D):
    assert point in top_boundary and point in fascia_boundary and point in right_boundary and point in bevel_boundary
assert not any('joint-fill' in e.get('id','') for e in svg.iter())
assert not any(e.get('stroke-width')=='2.5' for e in table)
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v15.svg',encoding='utf-8')
(root/'CosmicWorkbench-v15-shared-vertices.json').write_text(json.dumps({
    'top_junctions':[A,B,C,D],'bottom_junctions':[a,b,c,d],
    'shared_by':['top plate','front fascia','side faces','bevel'],
    'joint_patches':0,'worktop_color':'#D8DFE3'},indent=2),encoding='utf-8')

export=(root/'export_workbench_v14.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v15.cjs').write_text(export.replace('north-v14','north-v15'),encoding='utf-8')
preview=(root/'preview_workbench_v13.py').read_text(encoding='utf-8')
preview=preview.replace('north-v13','north-v15').replace('review-v13','review-v15').replace('Workbench-v13.json','Workbench-v15.json')
preview=preview.replace('north-v12.png','north-v14.png')
preview=preview.replace('宇宙工作台 · 接点修复与底色恢复','宇宙工作台 · 统一缺口连接点')
preview=preview.replace('接好斜面与横向边线；台面底色恢复为最初的浅灰蓝 #D8DFE3','取消补片；斜面、横向面和倒角使用同一组连接点')
preview=preview.replace('crop((650,405,704,467)).resize((240,276)','crop((608,400,696,508)).resize((248,304)')
preview=preview.replace('(x+142,720)','(x+138,710)')
preview=preview.replace("report={'source'", "report={'source'")
preview=preview.replace("'left_junction':[336.3431458,210.75]", "'left_junction':[336.3431458,210.75],'shared_vertices':True,'joint_patches':0")
(root/'preview_workbench_v15.py').write_text(preview,encoding='utf-8')
print('Four shared upper vertices and four shared lower vertices generated; joint patches removed.')
