"""Stage approved artwork for existing game texture paths, at 256 pixels/cell."""
from copy import deepcopy
from pathlib import Path
import json
import xml.etree.ElementTree as ET
from normalize_outlines import CORE_STROKE_PER_CELL

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent.parent
OUT=HERE/'GameExport'
OUT.mkdir(exist_ok=True)
NS='http://www.w3.org/2000/svg';Q='{'+NS+'}'
ET.register_namespace('',NS)
PPU=256;PAD=16
GAP=12  # Real inset inside each occupied cell rectangle, beyond canvas padding.
STROKE=CORE_STROKE_PER_CELL*PPU
GESTATOR_VISUAL_SCALE=0.90
sources=json.loads((HERE/'outline-manifest.json').read_text(encoding='utf-8'))
directions=json.loads((HERE/'Directions/manifest.json').read_text(encoding='utf-8'))
definitions={}
for filename in ['StorageBuildings','StorageSupplyShelf','TransportSupplier','FuelSupplier','CaravanTeleporter','NivarianCannonExtractor','CosmicMechGestator']:
    for node in ET.parse(ROOT/f'Defs/ThingDefs/{filename}.xml').getroot():
        name=node.findtext('defName')
        if name:definitions[name]=(filename,node)
records=[];renders=[]

def game_document(source,width,height,sx,sy,tx,ty):
    root=ET.Element(Q+'svg',dict(width=str(width),height=str(height),viewBox=f'0 0 {width} {height}'))
    outer=ET.SubElement(root,Q+'g',dict(transform=f'matrix({sx:.12f} 0 0 {sy:.12f} {tx:.12f} {ty:.12f})'))
    for child in source:
        e=deepcopy(child)
        for part in e.iter():
            if part.get('stroke')=='#000000':
                # librsvg applies vector-effect reliably to paths, but not to
                # a <use> instance. Inline only this exterior geometry.
                if part.tag==Q+'use':
                    reference=source.find(f'.//*[@id="{part.get("href")[1:]}"]')
                    assert reference is not None and reference.tag==Q+'path'
                    part.tag=Q+'path';part.set('d',reference.get('d'));part.attrib.pop('href')
                part.set('stroke-width',str(2*STROKE))
                part.set('vector-effect','non-scaling-stroke')
        outer.append(e)
    return root

def save_svg(root,stem,png_path,**extra):
    path=OUT/(stem+'.svg')
    ET.ElementTree(root).write(path,encoding='utf-8',xml_declaration=True)
    renders.append(dict(svg=path.relative_to(OUT).as_posix(),png=png_path,**extra))

for family in ['StorageCore','StorageUnit','StorageReceiver','CosmicWorkbench','ConstructionSupplyUnit',
               'StorageApparelAdapter','StorageSupplyShelf','TransportSupplier','FuelSupplier',
               'NivarianCannonExtractor','CaravanTeleporter']:
    filename,definition=definitions['MS_'+family]
    graphic=definition.find('graphicData')
    multi=graphic.findtext('graphicClass')=='Graphic_Multi'
    views=[r for r in directions if r['family']==family] if multi else [next(r for r in sources if r['name']==family)]
    if multi:assert {r['direction'] for r in views}=={'south','north','east','west'},family
    for item in views:
        direction=item.get('direction','single')
        source_path=(HERE/'Directions' if multi else HERE)/item['svg']
        cells=item['footprint'];width,height=[int(c*PPU+2*PAD) for c in cells]
        suffix='_'+direction if multi else ''
        target='Textures/'+graphic.findtext('texPath')+suffix+'.png'
        record=dict(family=family,direction=direction,footprint=cells,world_size=[width/PPU,height/PPU],
                    size=[width,height],target=target,source=str(source_path.relative_to(ROOT)),xml=filename+'.xml')
        source=ET.parse(source_path).getroot()
        bounds=__import__('normalize_outlines').PATH_BOUNDS.get(item['name'])
        if bounds is None:
            # Read actual exterior path bounds measured by the direction builder.
            boundfile=HERE/'Directions/geometry-bounds.json'
            assert boundfile.is_file(),'Run updated build_directions.py first'
            bounds=json.loads(boundfile.read_text(encoding='utf-8'))[item['name']]
        left,top,right,bottom=bounds
        sx=(cells[0]*PPU-2*GAP-2*STROKE)/(right-left)
        sy=(cells[1]*PPU-2*GAP-2*STROKE)/(bottom-top)
        tx=PAD+GAP+STROKE-left*sx;ty=PAD+GAP+STROKE-top*sy
        doc=game_document(source,width,height,sx,sy,tx,ty)
        save_svg(doc,f'{family}{suffix}',target)
        records.append(record)

gestator=[]
for item in [r for r in directions if r['family']=='CosmicMechGestator']:
    direction=item['direction'];side=direction in ['east','west']
    source=ET.parse(HERE/'Directions'/item['svg']).getroot()
    world=[3.125,3.625] if side else [3.625,3.125]
    width,height=[round(v*PPU) for v in world]
    center=[384,400 if side else 275]
    sx,sy=3/640*PPU*GESTATOR_VISUAL_SCALE,2/350*PPU*GESTATOR_VISUAL_SCALE
    tx,ty=width/2-center[0]*sx,height/2-center[1]*sy
    full=game_document(source,width,height,sx,sy,tx,ty)
    save_svg(full,f'CosmicMechGestator_Full_{direction}',f'References/CosmicMechGestator_{direction}.png')
    layers={key:ET.Element(source.tag,source.attrib) for key in ['Base','Glass','Top']}
    for layer in layers.values():
        for child in source:
            if child.tag==Q+'defs':layer.append(deepcopy(child))
    for child in source:
        if child.tag in [Q+'defs',Q+'title',Q+'desc']:continue
        identifier=child.get('id')
        if identifier=='outer-only-outline':
            if side:layers['Base'].append(deepcopy(child))
            else:
                # Partition the single outside contour at the platform's rear
                # edge: the cap's overhanging arc must be above forming mechs.
                for name,rect in [('Top',(-1000,-1000,3000,1100)),('Base',(-1000,100,3000,2000))]:
                    defs=ET.SubElement(layers[name],Q+'defs')
                    clip=ET.SubElement(defs,Q+'clipPath',dict(id='exterior-layer-partition'))
                    x,y,w,h=rect
                    ET.SubElement(clip,Q+'rect',dict(x=str(x),y=str(y),width=str(w),height=str(h)))
                    wrapper=ET.SubElement(layers[name],Q+'g',{'clip-path':'url(#exterior-layer-partition)'})
                    wrapper.append(deepcopy(child))
        elif identifier=='cylinder-glass':layers['Glass'].append(deepcopy(child))
        elif identifier in ['gestator-top','near-manifold'] or child.find('.//*[@id="near-manifold"]') is not None:
            layers['Top'].append(deepcopy(child))
        else:layers['Base'].append(deepcopy(child))
    paths={}
    prefix='Textures/Things/Building/CosmicMechGestator/CosmicMechGestator'
    for key,layer in layers.items():
        suffix={'Base':'','Glass':'Glass','Top':'Top'}[key]
        target=prefix+suffix+'_'+direction+'.png'
        save_svg(game_document(layer,width,height,sx,sy,tx,ty),f'CosmicMechGestator_{key}_{direction}',target)
        paths[key]=target
    # The user allows this building to fill its cells. Fit the platform's top
    # plane; its 24-source-unit front extrusion is natural vertical projection.
    floor=(170.6667,137.5,597.3333,686.5) if side else (64,100,704,474)
    bx0,by0,bx1,by1=floor
    floor_bbox=[bx0*sx+tx-STROKE,by0*sy+ty-STROKE,
                bx1*sx+tx+STROKE,by1*sy+ty+STROKE]
    cells=[2,3] if side else [3,2]
    occupied=[width/2-cells[0]*PPU/2,height/2-cells[1]*PPU/2,
              width/2+cells[0]*PPU/2,height/2+cells[1]*PPU/2]
    clearance=[floor_bbox[0]-occupied[0],floor_bbox[1]-occupied[1],
               occupied[2]-floor_bbox[2],occupied[3]-floor_bbox[3]]
    plane_clearance=clearance.copy()
    plane_clearance[3]+=24*sy
    assert min(plane_clearance)>=0,(direction,plane_clearance)
    assert min(clearance)>=-24*sy,(direction,clearance)
    gestator.append(dict(direction=direction,world_size=world,size=[width,height],center_svg=center,
                         visual_scale=GESTATOR_VISUAL_SCALE,
                         floor_clearance_pixels=clearance,
                         platform_plane_clearance_pixels=plane_clearance,
                         layers=paths,reference=f'References/CosmicMechGestator_{direction}.png'))

(OUT/'manifest.json').write_text(json.dumps(dict(ppu=PPU,padding=PAD,inset_pixels=GAP,outline_pixels=STROKE,
    ordinary=records,gestator=gestator,renders=renders),ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Prepared {len(records)} ordinary textures and 4 gestator directions; {len(renders)} SVG exports staged.')
