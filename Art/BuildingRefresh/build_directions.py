"""Complete four views of the five rotatable buildings; review files only."""
from pathlib import Path
from copy import deepcopy
import xml.etree.ElementTree as ET
import math
import json
import shutil
import hashlib
from normalize_outlines import outward_contour, PATH_WIDTHS, PATH_BOUNDS
from casing_geometry import casing_faces

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent.parent
OUT=HERE/'Directions'
OUT.mkdir(exist_ok=True)
NS='http://www.w3.org/2000/svg'
Q='{'+NS+'}'
ET.register_namespace('',NS)
P={'shell':'#d8dfe3','light':'#e6e9e8','bevel':'#acb8bf','metal':'#93a3ad',
   'side':'#637782','dark':'#304553','floor':'#8498a4','blue':'#c5e5f5'}
LABELS={'StorageUnit':'存储单元','CosmicWorkbench':'宇宙工作台',
        'StorageApparelAdapter':'衣物适配器','TransportSupplier':'物资补给器',
        'CosmicMechGestator':'宇宙培育器'}
DIRS={'south':'南','north':'北','east':'东','west':'西'}
records=[]
existing=json.loads((HERE/'outline-manifest.json').read_text(encoding='utf-8'))
hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in HERE.glob('*-v1.svg')}

def elem(tag,**attrs):
    return ET.Element(Q+tag,{k.replace('_','-'):str(P.get(v,v)) for k,v in attrs.items()})
def path(d,fill,**attrs):return elem('path',d=d,fill=fill,**attrs)
def rect(x,y,w,h,fill,rx=0):return elem('rect',x=x,y=y,width=w,height=h,rx=rx,fill=fill)
def group(children,**attrs):
    g=elem('g',**attrs);g.extend(children);return g
def outline(d):return path(d,'none',stroke='#000000',stroke_width=8,stroke_linejoin='miter')
def document(w,h,title):
    svg=elem('svg',width=w,height=h,viewBox=f'0 0 {w} {h}')
    ET.SubElement(svg,Q+'title').text=title
    ET.SubElement(svg,Q+'desc').text='Directional companion to latest approved SVG study; outside-only black contour; not installed.'
    return svg
def serialize(root):return ET.tostring(root,encoding='unicode')
def copy_existing(family,direction):
    item=next(r for r in existing if r['name']==f'{family}_{direction}')
    record=deepcopy(item)
    record.update(family=family,direction=direction,title=LABELS[family],
                  svg=f'{family}_{direction}.svg',png=f'{family}_{direction}.png',approved_view=True,
                  approved_source_svg=item['svg'],approved_source_png=item['png'])
    shutil.copyfile(HERE/item['svg'],OUT/record['svg'])
    shutil.copyfile(HERE/item['png'],OUT/record['png'])
    records.append(record)
def save(family,direction,root,cells,bounds):
    name=f'{family}_{direction}'
    record=dict(name=name,family=family,direction=direction,title=LABELS[family],
                svg=name+'.svg',png=name+'.png',footprint=cells,approved_view=False)
    PATH_WIDTHS[name]=bounds[2]-bounds[0]
    PATH_BOUNDS[name]=bounds
    text=outward_contour(serialize(root),record)
    ET.fromstring(text)
    (OUT/record['svg']).write_text(text,encoding='utf-8')
    records.append(record)
def rounded_outline(x,y,w,h,r):
    return (f'M{x+r} {y}H{x+w-r}Q{x+w} {y} {x+w} {y+r}V{y+h-r}'
            f'Q{x+w} {y+h} {x+w-r} {y+h}H{x+r}Q{x} {y+h} {x} {y+h-r}'
            f'V{y+r}Q{x} {y} {x+r} {y}Z')
def casing(x,y,w,h,depth=26,cut=10):
    d,faces=casing_faces(x,y,w,h,depth,cut)
    return d,[path(geometry,fill) for geometry,fill in faces]

# Storage unit: global light and near-side plinth stay upright. Only lid pulls,
# identifier strips and end equipment change sides with the access direction.
copy_existing('StorageUnit','south')
def cartridge(x,y,w,h,orientation):
    pieces=[rect(x,y,w,h,'dark',8),rect(x+5,y+4,w-10,h-18,'shell',6),
            path(f'M{x+5} {y+4}H{x+w-5}V{y+12}H{x+13}V{y+h-18}H{x+5}Z','light'),
            path(f'M{x+w-13} {y+12}H{x+w-5}V{y+h-14}H{x+5}V{y+h-21}H{x+w-13}Z','bevel')]
    if orientation=='north':
        pieces += [rect(x+w-23,y+25,10,100,'blue',1),rect(x+w/2-30,y+19,60,19,'side',3),rect(x+w/2-26,y+19,52,8,'metal',1)]
    else:
        right=orientation=='east'
        pieces += [rect(x+20,y+13 if right else y+h-30,100,10,'blue',1),
                   rect(x+w-34 if right else x+15,y+h/2-30,19,60,'side',3),
                   rect(x+w-34 if right else x+15,y+h/2-26,8,52,'metal',1)]
    return pieces
for direction in ['north','east','west']:
    horizontal=direction in ['east','west']
    bounds=(124,132,644,380) if horizontal else (132,104,380,624)
    x,y,r,b=bounds
    svg=document(768 if horizontal else 512,512 if horizontal else 768,LABELS['StorageUnit']+' · '+DIRS[direction]+'向')
    d,parts=casing(x,y,r-x,b-y,22 if horizontal else 26)
    svg.extend(parts)
    if horizontal:
        for lx in [202,385]:svg.extend(cartridge(lx,147,168,208,direction))
        sx=154 if direction=='east' else 593
        svg.extend([rect(sx,180,11,88,'dark',2),rect(sx+3,183,5,48,'blue'),
                    rect(328,363,104,8,'dark',2),rect(340,364,52,4,'blue')])
    else:
        for ly in [182,365]:svg.extend(cartridge(152,ly,208,168,direction))
        svg.extend([rect(269,555,88,11,'dark',2),rect(309,558,45,5,'blue'),
                    path('M158 142H165V162H158ZM170 142H177V162H170ZM182 142H189V162H182Z','side'),
                    rect(204,607,104,8,'dark',2),rect(216,608,52,4,'blue')])
    svg.append(outline(d))
    save('StorageUnit',direction,svg,[2,1] if horizontal else [1,2],bounds)

# Material supplier: rotate the functional deck and its arrow; the outer
# casing, its lighting and downward extrusion remain fixed in every view.
copy_existing('TransportSupplier','south')
supplier=ET.parse(HERE/'TransportSupplier_south-refresh-v1.svg').getroot()
body=[deepcopy(e) for e in supplier if e.tag not in [Q+'defs',Q+'title',Q+'desc'] and e.get('id')!='outer-only-outline']
supplier_d='M58 50H454L482 78V450L456 478H56L30 450V78Z'
for direction,angle in [('north',180),('east',-90),('west',90)]:
    svg=document(512,512,LABELS['TransportSupplier']+' · '+DIRS[direction]+'向')
    svg.extend(deepcopy(body[:4]))
    svg.append(group(deepcopy(body[4:]),transform=f'rotate({angle} 256 256)',id='directional-output-deck'))
    svg.append(outline(supplier_d))
    save('TransportSupplier',direction,svg,[1,1],(30,50,482,478))

# Apparel remains upright from every side. The bay and service compartment are
# rearranged; the shirt is never rotated along with a rectangular floor plan.
copy_existing('StorageApparelAdapter','south')
garment=[path('M370 140V158L299 189H441L370 158','none',stroke='#b8c5cc',stroke_width=6,stroke_linejoin='round'),
 path('M338 180L317 184L282 229L306 247L327 225V314Q370 326 414 314V225L435 247L459 229L424 184L402 180L383 201H358Z','blue'),
 path('M402 180L424 184L459 229L435 247L414 225V314Q395 321 370 322V201H383Z','#9ab6c8'),
 path('M338 180L358 201H383L402 180L389 178Q370 192 350 178Z','side')]
def apparel_bay(x,y,w,h):
    rod=y+max(40,(h-184)/2)
    return [rect(x,y,w,h,'side'),rect(x+12,y+12,w-24,h-28,'dark'),
            rect(x+12,y+55,w-24,h-71,'floor'),rect(x+29,rod-7,w-58,10,'bevel',2),
            group(deepcopy(garment),transform=f'translate({x+w/2-370} {rod-140})',id='upright-garment'),
            rect(x+12,y+h-16,w-24,22,'bevel'),rect(x+12,y+h-16,w-24,7,'light'),
            rect(x-10,y+31,12,h-95,'blue',1),rect(x+w-2,y+31,12,h-95,'blue',1)]
def service_compartment(x,y,w,h):
    hh=min(136,h-130)
    return [rect(x,y,w,h,'bevel',6),rect(x,y,w-8,h-12,'shell',5),rect(x+29,y+25,88,10,'blue',1),
            rect(x+31,y+73,w-66,hh,'metal',3),rect(x+35,y+77,w-74,hh-12,'shell',2),
            rect(x+w-93,y+73+hh-41,37,11,'side',2),
            path(f'M{x+31} {y+h-51}h17v12h-17Z M{x+56} {y+h-51}h17v12h-17Z M{x+81} {y+h-51}h17v12h-17Z','side')]
for direction in ['north','east','west']:
    vertical=direction!='north'
    bounds=(26,42,486,998) if vertical else (26,42,998,486)
    svg=document(512 if vertical else 1024,1024 if vertical else 512,LABELS['StorageApparelAdapter']+' · '+DIRS[direction]+'向')
    d,parts=casing(26,42,bounds[2]-26,bounds[3]-42,28,10)
    svg.extend(parts)
    if direction=='north':
        svg.extend(apparel_bay(375,94,572,287));svg.extend(service_compartment(85,97,216,292))
    elif direction=='east':
        svg.extend(apparel_bay(98,94,316,570));svg.extend(service_compartment(98,711,316,218))
    else:
        svg.extend(service_compartment(98,94,316,218));svg.extend(apparel_bay(98,359,316,570))
    svg.append(outline(d))
    save('StorageApparelAdapter',direction,svg,[1,2] if vertical else [2,1],bounds)

# Workbench: transform only the worktop plan and mounted equipment. Rebuild
# the vertical faces, global bevel lighting and outside silhouette afterwards.
copy_existing('CosmicWorkbench','north')
bench=ET.parse(ROOT/'Art/RemainingBuildings/CosmicWorkbench_north-v15.svg').getroot()
top_points=[(80,68),(688,68),(704,84),(704,220),(690,232.75),
            (453.6568542,232.75),(431.6568542,210.75),(336.3431458,210.75),
            (314.3431458,232.75),(78,232.75),(64,220),(64,84)]
k=164.75/(640/3)
def poly(points):return 'M'+'L'.join(f'{x:.9f} {y:.9f}' for x,y in points)+'Z'
def inset(points,amount):
    shifted=[]
    for a,b in zip(points,points[1:]+points[:1]):
        dx,dy=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dy)
        n=(-dy/length*amount,dx/length*amount)
        shifted.append(((a[0]+n[0],a[1]+n[1]),(dx,dy)))
    inner=[]
    for prev,current in zip(shifted[-1:]+shifted[:-1],shifted):
        p,v=prev;q,u=current
        cross=v[0]*u[1]-v[1]*u[0]
        if abs(cross)<1e-9:inner.append(q);continue
        t=((q[0]-p[0])*u[1]-(q[1]-p[1])*u[0])/cross
        inner.append((p[0]+t*v[0],p[1]+t*v[1]))
    return inner
for direction,angle in [('south',180),('east',90),('west',-90)]:
    vertical=direction in ['east','west']
    cx,cy=(176,352) if vertical else (384,150.375)
    radians=math.radians(angle);c=round(math.cos(radians));s=round(math.sin(radians))
    a,b,cc,dd=c,k*s,-s/k,c
    tx=cx-a*384-cc*150.375;ty=cy-b*384-dd*150.375
    points=[(a*x+cc*y+tx,b*x+dd*y+ty) for x,y in top_points]
    depth=17.25
    edges=list(zip(points,points[1:]+points[:1]))
    boundary=[];faces=[]
    for p,q in edges:
        front=q[0]<p[0]-1e-6
        edge=[(p[0],p[1]+depth),(q[0],q[1]+depth)] if front else [p,q]
        for v in edge:
            if not boundary or boundary[-1]!=v:boundary.append(v)
        if front:
            color='#91a1a9' if q[1]>p[1]+1e-6 else '#465d6b' if q[1]<p[1]-1e-6 else '#657982'
            faces.append(path(poly([p,q,(q[0],q[1]+depth),(p[0],p[1]+depth)]),color))
    body_d=poly(boundary)
    svg=document(352 if vertical else 768,768 if vertical else 352,LABELS['CosmicWorkbench']+' · '+DIRS[direction]+'向')
    svg.append(path(body_d,'#7d8c93'));svg.extend(faces)
    svg.append(path(poly(points),'shell'))
    inside=inset(points,7)
    for i,(p,q) in enumerate(edges):
        color='light' if q[0]>p[0]+1e-6 or (abs(q[0]-p[0])<1e-6 and q[1]<p[1]) else 'bevel'
        svg.append(path(poly([p,q,inside[(i+1)%len(points)],inside[i]]),color))
    equipment=[deepcopy(e) for e in bench if e.tag==Q+'g' and e.get('id')!='tabletop']
    svg.append(group(equipment,transform=f'matrix({a} {b} {cc} {dd} {tx} {ty})',id='worktop-equipment'))
    # One unobtrusive identification inlay stays on the visible near fascia.
    max_y=max(y for x,y in boundary)
    if vertical:svg.append(rect(cx-25,max_y-10,50,5,'blue'))
    else:svg.append(rect(cx+184,max_y-10,87,5,'blue'))
    svg.append(outline(body_d))
    bounds=(min(x for x,y in boundary),min(y for x,y in boundary),max(x for x,y in boundary),max(y for x,y in boundary))
    save('CosmicWorkbench',direction,svg,[1,3] if vertical else [3,1],bounds)

# Gestator: retain the approved older direction-specific platforms/manifolds,
# replacing all three vessel passes with the latest simplified south passes.
copy_existing('CosmicMechGestator','south')
latest=ET.parse(HERE/'CosmicMechGestator_south-refresh-v1.svg').getroot()
palette={'#cdd8df':'#d8dfe3','#7e939f':'#637782','#adbfcc':'#acb8bf','#8fa6b7':'#8498a4',
         '#bacbd7':'#acb8bf','#d3e1ea':'#d8dfe3','#a8bfce':'#93a3ad'}
removed_strokes={'#edf3f7','#a0b5c3','#cbdbe6','#d2e2ec','#e2edf4','#eaf2f7','#e9f1f6','#eaf3f8','#9ab3c6'}
for direction in ['north','east','west']:
    original_direction='east' if direction=='west' else direction
    svg=ET.parse(ROOT/f'Art/CosmicMechGestator/CosmicMechGestator_{original_direction}-v6.svg').getroot()
    for parent in list(svg.iter()):
        for child in list(parent):
            if child.tag==Q+'path' and child.get('stroke') in removed_strokes:parent.remove(child)
    for e in svg.iter():
        if e.get('fill') in palette:e.set('fill',palette[e.get('fill')])
    for e in list(svg):
        if e.tag==Q+'defs':svg.remove(e)
    latest_defs=next(e for e in latest if e.tag==Q+'defs' and e.get('id')!='exterior-outline-mask-defs')
    svg.insert(0,deepcopy(latest_defs))
    for identifier in ['vessel-seat','cylinder-glass','gestator-top']:
        old=svg.find(f'.//*[@id="{identifier}"]')
        new=deepcopy(latest.find(f'.//*[@id="{identifier}"]'))
        new.set('transform',old.get('transform'))
        for parent in svg.iter():
            if old in list(parent):
                index=list(parent).index(old);parent.remove(old);parent.insert(index,new);break
    contour=next(e for e in svg if e.get('stroke')=='#000000')
    svg.remove(contour)
    if original_direction=='east':
        bounds=(170.6667,137.5,597.3333,686.5)
        d=rounded_outline(bounds[0],bounds[1],bounds[2]-bounds[0],bounds[3]-bounds[1],20)
        # Apply the mirror to mechanical housings only; the vessel and lighting
        # stay upright and share the exact platform-center anchor.
        if direction=='west':
            for e in list(svg):
                if e.tag==Q+'g' and e.get('id') not in ['vessel-seat','cylinder-glass','gestator-top']:
                    index=list(svg).index(e);svg.remove(e)
                    svg.insert(index,group([e],transform='translate(768 0) scale(-1 1)'))
        cells=[2,3]
    else:
        bounds=(64,22.6,704,474);d=contour.get('d');cells=[3,2]
    svg.find(Q+'title').text=LABELS['CosmicMechGestator']+' · '+DIRS[direction]+'向'
    svg.append(outline(d))
    save('CosmicMechGestator',direction,svg,cells,bounds)

order=list(LABELS)
records.sort(key=lambda r:(order.index(r['family']),list(DIRS).index(r['direction'])))
(OUT/'manifest.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
all_bounds={}
for record in records:
    name=record['name']
    if name in PATH_BOUNDS:all_bounds[name]=PATH_BOUNDS[name]
(OUT/'geometry-bounds.json').write_text(json.dumps(all_bounds,indent=2),encoding='utf-8')
assert len(records)==20
assert {r['family'] for r in records}==set(LABELS)
for family in LABELS:
    assert {r['direction'] for r in records if r['family']==family}==set(DIRS)
assert hashes=={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in HERE.glob('*-v1.svg')},'Approved base view changed'
print('Prepared 20 directional SVGs: 5 approved views preserved and 15 companion views added. No game files changed.')
