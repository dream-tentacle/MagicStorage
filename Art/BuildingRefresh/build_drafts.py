"""Review-only SVG studies. Never writes game textures or definitions."""
from pathlib import Path
import json
import math
import xml.etree.ElementTree as ET
from casing_geometry import casing_faces, chamfered_outline

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
P = dict(shell='#d8dfe3', light='#e6e9e8', bevel='#acb8bf', metal='#93a3ad',
         side='#637782', dark='#304553', floor='#8498a4', blue='#c5e5f5', navy='#204570')
manifest = []

def path(d, fill, **attrs):
    a = ' '.join(f'{k.replace("_", "-")}="{v}"' for k, v in attrs.items())
    return f'<path d="{d}" fill="{P.get(fill, fill)}" {a}/>'

def rect(x,y,w,h,fill,rx=0):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{P.get(fill,fill)}"/>'

def ellipse(x,y,rx,ry,fill):
    return f'<ellipse cx="{x}" cy="{y}" rx="{rx}" ry="{ry}" fill="{P.get(fill,fill)}"/>'

def elliptic_band(outer, inner, start, end):
    """Follow both actual rim boundaries, including their projected centers."""
    def point(e, angle):
        cx,cy,rx,ry=e
        t=math.radians(angle)
        return f'{cx+rx*math.cos(t):.6f} {cy+ry*math.sin(t):.6f}'
    return (f'M{point(outer,start)}A{outer[2]} {outer[3]} 0 0 1 {point(outer,end)}'
            f'L{point(inner,end)}A{inner[2]} {inner[3]} 0 0 0 {point(inner,start)}Z')

def outline(d, weight=8):
    return path(d,'none',stroke='#000000',stroke_width=weight,stroke_linejoin='miter')

def casing(x,y,w,h,depth=26,cut=10):
    d,faces=casing_faces(x,y,w,h,depth,cut)
    return d,[path(geometry,fill) for geometry,fill in faces]

def save(name, title, old, size, footprint, shapes, note):
    body='\n'.join(shapes)
    svg=f'<svg xmlns="http://www.w3.org/2000/svg" width="{size[0]}" height="{size[1]}" viewBox="0 0 {size[0]} {size[1]}">\n<title>{title} · 优化样稿</title>\n<desc>{note} Review draft only; not installed.</desc>\n{body}\n</svg>\n'
    target=HERE/f'{name}-refresh-v1.svg'
    target.write_text(svg,encoding='utf-8')
    manifest.append(dict(name=name,title=title,source=old,svg=target.name,png=target.with_suffix('.png').name,footprint=footprint,note=note))

# Large removable lids occupy the original 1x2 chassis. Negative gaps distinguish
# the two cartridges; no fine highlight border or repeated miniature controller.
unit_outline,s=casing(132,104,248,520)
s += [
   rect(155,134,88,11,'dark',2),rect(158,137,48,5,'blue'),
   rect(321,135,28,8,'metal',1)]
for y in [182,365]:
    s += [rect(152,y,208,168,'dark',8),rect(157,y+4,198,150,'shell',6),
          path(f'M157 {y+4}H355V{y+12}H165V{y+150}H157Z','light'),
          path(f'M347 {y+12}H355V{y+154}H157V{y+147}H347Z','bevel'),
          rect(165,y+20,10,100,'blue',1),
          rect(226,y+123,60,19,'side',3),rect(230,y+123,52,8,'metal',1)]
s += [path('M323 555H330V575H323ZM335 555H342V575H335ZM347 555H354V575H347Z','side'),
      rect(204,607,104,8,'dark',2),rect(216,608,52,4,'blue'),outline(unit_outline,5)]
save('StorageUnit_south','存储单元','Art/StorageUnit/StorageUnit_south-v2.svg',(512,768),(1,2),s,'Two removable lids; original chassis bounds retained; flat metal planes and stronger cartridge gaps.')

# Receiver: a broad, empty landing tray and recessed back wall. Its continuous
# walls share their tray-floor vertices, rather than floating corner brackets.
d='M64 48H448L482 82V448L456 478H56L30 448V82Z'
s=[path(d,'side'),path('M64 48H448L482 82V422L456 450H56L30 422V82Z','shell'),
   path('M64 48H448L465 65H58L45 83H30V82Z','light'),
   path('M465 83H482V422L456 450H56L65 434H448L465 415Z','bevel'),
   path('M95 125H417L437 145V384L409 412H103L75 384V145Z','side'),
   path('M106 141H406L420 155V355L398 377H114L92 355V155Z','dark'),
   path('M92 192H420V355L398 377H114L92 355Z','floor'),
   path('M92 155L114 175V355L92 355Z','metal'),
   path('M398 175L420 155V355L398 377Z','side'),
   path('M92 355L114 377H398L420 355V379L402 398H110L92 379Z','bevel'),
   path('M114 377H398V385H114Z','light'),
   rect(190,90,132,11,'blue',1),rect(220,427,72,9,'dark',2),outline(d)]
save('StorageReceiver','存储接收器','Art/StorageEndpoints/StorageReceiver-v1.svg',(512,512),(1,1),s,'Receiving tray with a dark back wall, open landing floor and one loading lip; fixed orientation.')

# Restocking shelf: open depth and two substantial shelves. Sparse labeling
# replaces the ornamental pseudo-electronic strips.
d='M38 42H474L484 52V476L478 482H432V446H80V482H34L28 476V52Z'
s=[path(d,'side'),rect(76,117,360,319,'dark'),
   path(chamfered_outline(28,42,456,90,10),'shell'),
   path('M38 42H474L484 52H38V117H28V52Z','light'),
   rect(77,75,83,13,'blue',1),rect(361,76,68,11,'side',1)]
for y,h in [(173,99),(311,111)]:
    s += [rect(78,y,356,h,'floor'),rect(78,y,356,24,'side'),
          rect(78,y+h-22,356,22,'shell'),rect(78,y+h-22,356,7,'light'),
          rect(78,y+h-5,356,5,'metal')]
s += [path('M28 117H78V453H80V482H34L28 476Z','shell'),
      path('M434 117H484V476L478 482H432V453H434Z','bevel'),
      rect(434,117,32,336,'shell'),rect(36,132,9,310,'light'),
      rect(51,347,12,58,'blue',1),rect(446,347,12,58,'blue',1),
      path('M28 453H80V482H34L28 476ZM432 453H484V476L478 482H432Z','side'),outline(d)]
save('StorageSupplyShelf','自动补货架','Art/StorageEndpoints/StorageSupplyShelf_south-v1.svg',(512,512),(1,1),s,'Two empty open shelves; fewer trim lines; original frame and fixed orientation retained.')

# Supplier is an output deck with a low back housing, not a receiving basin.
d='M58 50H454L482 78V450L456 478H56L30 450V78Z'
s=[path(d,'side'),path('M58 50H454L482 78V422L456 450H56L30 422V78Z','shell'),
   path('M58 50H454L469 66H51L43 78H30Z','light'),
   path('M468 78H482V422L456 450H56L65 436H448L468 416Z','bevel'),
   path('M87 112H425V178H87Z','side'),rect(96,112,320,47,'metal',4),
   rect(116,124,88,10,'blue',1),rect(339,124,56,10,'dark',1),
   path('M94 184H418V373L396 397H116L94 373Z','bevel'),
   path('M108 184H404V365L387 383H125L108 365Z','shell'),
   path('M108 184H404V196H108Z','side'),
   path('M256 211V292M202 283L256 337L310 283','none',stroke=P['navy'],stroke_width=15,stroke_linecap='round',stroke_linejoin='round'),
   path('M91 383H421V409L405 424H107L91 409Z','side'),
   path('M91 383H421V394H91Z','metal'),rect(191,404,130,8,'dark',2),outline(d)]
save('TransportSupplier_south','物资补给器','Art/StorageEndpoints/TransportSupplier_south-v4.svg',(512,512),(1,1),s,'Low output deck and physical discharge lip; dark-blue directional arrow retained.')

# Construction supply: preserve the original raised hatch and recessed shutter.
d='M61 54H451L480 83V449L453 478H59L32 449V83Z'
# Both hatch sides and the inlay derive their corners from these shared edges.
hatch_left_slope=((119,341),(136,362))
hatch_right_slope=((393,341),(376,362))
def hatch_edge_x(edge,y):
    (x0,y0),(x1,y1)=edge
    return x0+(x1-x0)*(y-y0)/(y1-y0)
hatch_plate=(f'M132 132H378L393 149L{hatch_right_slope[0][0]} {hatch_right_slope[0][1]}'
             f'L{hatch_right_slope[1][0]} {hatch_right_slope[1][1]}'
             f'L{hatch_left_slope[1][0]} {hatch_left_slope[1][1]}'
             f'L{hatch_left_slope[0][0]} {hatch_left_slope[0][1]}V149Z')
inlay_top,inlay_bottom=344,359
hatch_inlay=(f'M{hatch_edge_x(hatch_left_slope,inlay_top):.9f} {inlay_top}'
             f'H{hatch_edge_x(hatch_right_slope,inlay_top):.9f}'
             f'L{hatch_edge_x(hatch_right_slope,inlay_bottom):.9f} {inlay_bottom}'
             f'H{hatch_edge_x(hatch_left_slope,inlay_bottom):.9f}Z')
s=[path(d,'side'),path('M61 54H451L480 83V422L453 450H59L32 422V83Z','shell'),
   path('M61 54H451L480 83H465L452 70H53L45 83H32Z','light'),
   path('M465 83H480V422L453 450H59L68 436H446L465 416Z','bevel'),
   path('M130 136H382L405 160V364L380 392H132L107 364V160Z','side'),
   path('M130 125H382L405 149V346L380 374H132L107 346V149Z','bevel'),
   path(hatch_plate,'shell'),
   path('M130 125H382L405 149H393L378 132H132L119 149H107Z','light'),
   path('M153 173H359L375 192V301L355 327H157L137 301V192Z','side'),
   path('M157 182H355L363 195V296L349 313H163L149 296V195Z','bevel'),
   path('M149 195H363V212H149Z','dark'),
   path('M155 247H357V254H155Z','floor'),
   path(hatch_inlay,'blue'),
   outline(d)]
save('ConstructionSupplyUnit','建筑供给单元','Art/RemainingBuildings/ConstructionSupplyUnit-v1.svg',(512,512),(1,1),s,'Simplified raised hatch with one shutter seam and one sky-blue inlay; blank surrounding casing; no side trims, status marks or service panel.')

# Apparel bay: retain the hanging garment and broad 2x1 footprint. A single
# recessed service door replaces the stack of rounded interface panels.
d,s=casing(26,42,972,444,28)
s += [
   path('M89 94H661V381H89Z','side'),rect(101,106,548,259,'dark'),rect(101,149,548,216,'floor'),
   rect(118,133,514,10,'bevel',2),
   path('M370 140V158L299 189H441L370 158','none',stroke='#b8c5cc',stroke_width=6,stroke_linejoin='round'),
   path('M338 180L317 184L282 229L306 247L327 225V314Q370 326 414 314V225L435 247L459 229L424 184L402 180L383 201H358Z','blue'),
   path('M402 180L424 184L459 229L435 247L414 225V314Q395 321 370 322V201H383Z','#9ab6c8'),
   path('M338 180L358 201H383L402 180L389 178Q370 192 350 178Z','side'),
   rect(101,365,548,22,'bevel'),rect(101,365,548,7,'light'),
   rect(79,125,12,192,'blue',1),rect(659,125,12,192,'blue',1),
   rect(723,97,216,292,'bevel',6),rect(723,97,208,280,'shell',5),
   rect(752,122,88,10,'blue',1),rect(754,170,150,136,'metal',3),
   rect(758,174,142,124,'shell',2),rect(846,265,37,11,'side',2),
   path('M754 338H771V350H754ZM779 338H796V350H779ZM804 338H821V350H804Z','side'),outline(d)]
save('StorageApparelAdapter_south','衣物适配器','Art/RemainingBuildings/StorageApparelAdapter_south-v2.svg',(1024,512),(2,1),s,'Open garment bay and one plain service compartment; larger value groups, fewer interface frames.')

# Fuel supplier: the cylindrical reservoir is the body, with a small attached
# pump. It no longer sits on the generic full-size square terminal chassis.
d='M74 96H190L219 119C237 86 274 68 316 68C389 68 442 110 442 163V371C442 426 389 465 316 465H74L66 457V104Z'
_,pump=casing(66,96,132,369,24,8)
s=[path(d,'side'),*pump,
   rect(95,137,70,89,'metal',4),rect(108,150,44,59,'dark',2),rect(113,177,34,27,'blue',1),
   path('M96 286H111V363H96ZM121 286H136V363H121ZM146 286H161V363H146Z','side'),
   path('M213 160H442V371C442 426 389 465 316 465C250 465 213 425 213 371Z','metal'),
   path('M213 160H442V348C442 400 389 437 316 437C250 437 213 400 213 348Z','shell'),
   path('M395 169H442V348C442 395 400 431 337 436C382 414 402 390 402 343Z','bevel'),
   ellipse(327.5,162,114.5,77,'bevel'),ellipse(327.5,150,114.5,77,'shell'),
   ellipse(327.5,150,79,49,'side'),ellipse(327.5,144,65,37,'blue'),
   rect(311,124,33,35,'metal',4),rect(311,124,33,12,'light',2),
   path('M216 264C257 304 400 304 439 264V285C400 325 257 325 216 285Z','blue'),
   path('M178 380H194V393Q194 408 213 408H234','none',stroke=P['dark'],stroke_width=20,stroke_linejoin='round'),
   path('M178 377H194V390Q194 402 213 402H234','none',stroke=P['metal'],stroke_width=11,stroke_linejoin='round'),outline(d)]
save('FuelSupplier','燃料供给器','Art/RemainingBuildings/FuelSupplier-v1.svg',(512,512),(1,1),s,'A sealed cylindrical fuel reservoir with a compact pump and one feed connection; flat metal shading.')

# Extractor: heavy octagonal collar, dark shaft and two clamps. The shaft is
# visually different from the teleporter's flowing energy field.
d='M105 46H407L478 117V403L407 474H105L34 403V117Z'
rim_outer=(256,249,153,137)
rim_inner=(256,243,132,117)
s=[path(d,'side'),path('M105 46H407L478 117V375L407 446H105L34 375V117Z','shell'),
   path('M105 46H407L422 62H112L50 124H34V117Z','light'),
   path('M460 117H478V375L407 446H105L120 430H400L460 370Z','bevel'),
   ellipse(256,263,153,137,'side'),ellipse(*rim_outer,'metal'),
   ellipse(*rim_inner,'light'),ellipse(256,249,110,97,'dark'),
   path(elliptic_band(rim_outer,rim_inner,210,245),'blue'),
   path(elliptic_band(rim_outer,rim_inner,295,330),'blue')]
for x in [81,389]:
    s += [rect(x,213,42,83,'side',4),rect(x,207,35,73,'shell',3),rect(x+8,226,18,28,'blue',1)]
s += [path('M219 367H293V404H219Z','side'),rect(226,367,60,23,'shell',2),rect(235,373,42,8,'blue',1),outline(d)]
save('NivarianCannonExtractor','涅瓦莲质量驱动器提取口','Art/RemainingBuildings/NivarianCannonExtractor-v1.svg',(512,512),(1,1),s,'Heavy octagonal extraction collar, dark shaft and two locking clamps; fixed orientation.')

# Keep the exact spiral geometry and clipping from the approved teleporter.
# Remove fine casing highlights and duplicated status ornaments only.
NS='{http://www.w3.org/2000/svg}'
ET.register_namespace('',NS[1:-1])
tree=ET.parse(ROOT/'Art/CaravanTeleporter/CaravanTeleporter-v2.svg')
root=tree.getroot()
for e in list(root):
    if (e.tag==NS+'rect' and e.get('x')=='26') or (e.tag==NS+'path' and e.get('d','').startswith('M26 430')):
        root.remove(e)
    elif e.tag==NS+'path' and e.get('stroke') in ['#52616c','#f2f5f6','#a8b6bf']:
        root.remove(e)
    elif e.tag==NS+'path' and e.get('d','').startswith(('M209 78','M284 78','M217 471','M118 192')):
        root.remove(e)
    elif e.tag==NS+'rect' and (e.get('x'),e.get('y')) in [('193','64'),('197','68'),('204','468')]:
        root.remove(e)
teleporter_d,faces=casing_faces(26,42,460,444,28,10)
position=next(i for i,e in enumerate(root) if e.tag not in [NS+'title',NS+'desc',NS+'defs'])
for geometry,fill in faces:
    root.insert(position,ET.fromstring(f'<path xmlns="{NS[1:-1]}" d="{geometry}" fill="{P[fill]}"/>'))
    position+=1
for e in root:
    if e.get('stroke')=='#000000':e.set('d',teleporter_d)
for e in root.iter():
    if e.get('fill')=='#798893': e.set('fill',P['side'])
    if e.get('fill')=='#8ca2b2': e.set('fill',P['side'])
    if e.get('fill')=='#bfced9': e.set('fill',P['bevel'])
    if e.get('fill')=='#e4ebef': e.set('fill',P['shell'])
    if e.get('stroke')=='#6994b9': e.set('stroke-opacity','.22')
# A plain identification inlay, not another controller.
root.insert(len(root)-1,ET.fromstring(f'<rect xmlns="{NS[1:-1]}" x="204" y="75" width="104" height="11" rx="2" fill="#c5e5f5"/>'))
ET.SubElement(root,NS+'desc').text='Approved continuous spiral geometry and aperture clipping retained; flatter collar and simplified casing. Review only.'
target=HERE/'CaravanTeleporter-refresh-v1.svg'
tree.write(target,encoding='unicode',xml_declaration=False)
manifest.append(dict(name='CaravanTeleporter',title='远行队传送器',source='Art/CaravanTeleporter/CaravanTeleporter-v2.svg',svg=target.name,png=target.with_suffix('.png').name,footprint=[1,1],note='Exact approved spiral and clip preserved; simplify only the casing and collar.'))

# Gestator: reuse all tank, lid, platform coordinates, including the -27 anchor
# translation. Only large material planes and nonfunctional trim change.
tree=ET.parse(ROOT/'Art/CosmicMechGestator/CosmicMechGestator_south-v6.svg')
root=tree.getroot()
for parent in root.iter():
    for e in list(parent):
        if e.tag==NS+'path' and e.get('stroke') in ['#edf3f7','#a0b5c3','#cbdbe6','#d2e2ec','#e2edf4','#eaf2f7','#e9f1f6','#eaf3f8']:
            parent.remove(e)
        elif parent.get('id')=='gestator-top' and e.tag==NS+'path' and e.get('d','').startswith(('M365','M374')):
            parent.remove(e)
        elif parent.get('id')=='front-service' and e.tag==NS+'path' and e.get('d','').startswith(('M550','M355')):
            parent.remove(e)
for e in root.iter():
    fill=e.get('fill')
    replacements={'url(#lid-metal)':'#d8dfe3','#cdd8df':'#d8dfe3','#7e939f':'#637782','#adbfcc':'#acb8bf','#8fa6b7':'#8498a4','#bacbd7':'#acb8bf','#d3e1ea':'#d8dfe3','#88a2b6':'#637782','#8fa9bb':'#637782','#c6d8e4':'#acb8bf','#88a5ba':'#637782','#7395ae':'#405c70','#a9c4d7':'#8498a4','#c2d8e6':'#acb8bf'}
    if fill in replacements: e.set('fill',replacements[fill])
    if e.get('stroke')=='#000000': e.set('stroke-width','6')
    if e.get('id')=='tank-glass':
        # Glass is the sole translucent material; use a few broad values.
        for child in list(e): e.remove(child)
        for off,col,alpha in [('0','#4d7188','.90'),('.20','#b9d9e7','.65'),('.74','#94b7ca','.55'),('1','#4a7189','.88')]:
            ET.SubElement(e,NS+'stop',{'offset':off,'stop-color':col,'stop-opacity':alpha})
lid=root.find(f".//{NS}g[@id='gestator-top']")
ET.SubElement(lid,NS+'path',{'d':'M267 151C281 190 328 219 384 219C440 219 487 190 501 151C490 203 441 234 384 234C327 234 278 203 267 151Z','fill':'#acb8bf'})
ET.SubElement(lid,NS+'path',{'d':'M326 121H442V135H326Z','fill':'#93a3ad'})
# Gray stripe center is (384,128); keep the 76x7 blue inlay centered on it.
ET.SubElement(lid,NS+'path',{'d':'M346 124.5H422V131.5H346Z','fill':'#c5e5f5'})
ET.SubElement(root,NS+'desc').text='Original 3x2 platform and centered vessel seat retained exactly; flatter lid metal, simpler side manifolds and service apron. Review only.'
target=HERE/'CosmicMechGestator_south-refresh-v1.svg'
tree.write(target,encoding='unicode',xml_declaration=False)
manifest.append(dict(name='CosmicMechGestator_south',title='宇宙培育器',source='Art/CosmicMechGestator/CosmicMechGestator_south-v6.svg',svg=target.name,png=target.with_suffix('.png').name,footprint=[3,2],note='Exact original platform, cylinder and seat coordinates; simplified materials only.'))

(HERE/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Wrote {len(manifest)} review SVGs to {HERE}')
from normalize_outlines import normalize
normalize()
