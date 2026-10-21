"""Match current review assets to the core's outline at a shared cell scale."""
from pathlib import Path
import json
import re
import shutil
import math
import xml.etree.ElementTree as ET

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent.parent
# Core v27: exterior path spans x=60..452; 12-unit centered stroke;
# visible outer width=404 source units, displayed across two cells.
CORE_STROKE_PER_CELL=12*2/404 * (2/3)
PATH_WIDTHS={
    'StorageCore':384,
    'StorageUnit_south':248,
    'StorageReceiver':452,
    'StorageSupplyShelf':456,
    'TransportSupplier_south':452,
    'ConstructionSupplyUnit':448,
    'StorageApparelAdapter_south':972,
    'FuelSupplier':376,
    'NivarianCannonExtractor':444,
    'CaravanTeleporter':460,
    'CosmicMechGestator_south':640,
    'CosmicWorkbench_north':640,
}
PATH_BOUNDS={
    'StorageCore':(64,60,448,446),
    'StorageUnit_south':(132,104,380,624),
    'StorageReceiver':(30,48,482,478),
    'StorageSupplyShelf':(28,42,484,482),
    'TransportSupplier_south':(30,50,482,478),
    'ConstructionSupplyUnit':(32,54,480,478),
    'StorageApparelAdapter_south':(26,42,998,486),
    'FuelSupplier':(66,68,442,465),
    'NivarianCannonExtractor':(34,46,478,474),
    'CaravanTeleporter':(26,42,486,486),
    'CosmicMechGestator_south':(64,22.6,704,474),
    'CosmicWorkbench_north':(64,68,704,250),
}

def attr(element,name,value):
    pattern=rf'\b{re.escape(name)}="[^"]*"'
    if re.search(pattern,element):
        return re.sub(pattern,f'{name}="{value}"',element,count=1)
    ending='/>' if element.endswith('/>') else '>'
    return element[:-len(ending)]+f' {name}="{value}"'+ending

def outward_contour(text,item):
    # Strip our previous wrapper before updating; repeated runs are idempotent.
    text=re.sub(r'<defs id="exterior-outline-mask-defs">.*?</defs>\s*','',text,flags=re.S)
    text=re.sub(r'<g id="outer-only-outline"[^>]*>(.*?)</g>',r'\1',text,flags=re.S)
    pattern=r'<(?:path|use)\b[^>]*\bstroke="#000000"[^>]*>'
    matches=list(re.finditer(pattern,text))
    assert len(matches)==1,(item['name'],len(matches))
    contour=matches[0].group()
    span=PATH_WIDTHS[item['name']]
    cells=item['footprint'][0]
    # The outside-only stroke adds t to BOTH sides of the original body.
    # t/(path_width+2*t)*footprint_x = unchanged core's visible stroke per cell.
    thickness=CORE_STROKE_PER_CELL*span/(cells-2*CORE_STROKE_PER_CELL)
    outer=re.sub(r'\bstroke-width="[^"]+"',f'stroke-width="{2*thickness:.9f}"',contour)
    # Small cut corners remain straight instead of acquiring rounded joins.
    outer=attr(outer,'stroke-linejoin','miter')
    outer=attr(outer,'stroke-miterlimit','2')
    geometry=re.sub(r'\s+(?:id|stroke|stroke-width|stroke-linejoin|stroke-linecap)="[^"]*"','',contour)
    geometry=re.sub(r'\bfill="[^"]*"','fill="#000000"',geometry)
    geometry=geometry.replace('/>',' stroke="none"/>')
    wrapped=f'<g id="outer-only-outline" mask="url(#exterior-outside-mask)">{outer}</g>'
    text=text[:matches[0].start()]+wrapped+text[matches[0].end():]

    header=re.search(r'<svg\b[^>]*>',text).group()
    root=ET.fromstring(text)
    original_vb=root.get('data-outline-original-viewbox',root.get('viewBox'))
    original_w=root.get('data-outline-original-width',root.get('width'))
    original_h=root.get('data-outline-original-height',root.get('height'))
    x,y,w,h=map(float,original_vb.split())
    left,top,right,bottom=PATH_BOUNDS[item['name']]
    pad=max(0,math.ceil(max(x+2-(left-thickness),y+2-(top-thickness),
                            right+thickness+2-(x+w),bottom+thickness+2-(y+h))))
    vx,vy,vw,vh=x-pad,y-pad,w+2*pad,h+2*pad
    for key,value in [('viewBox',f'{vx:g} {vy:g} {vw:g} {vh:g}'),
                      ('width',f'{float(original_w)*vw/w:g}'),
                      ('height',f'{float(original_h)*vh/h:g}'),
                      ('data-outline-original-viewbox',original_vb),
                      ('data-outline-original-width',original_w),
                      ('data-outline-original-height',original_h)]:
        header=attr(header,key,value)
    text=re.sub(r'<svg\b[^>]*>',lambda _:header,text,count=1)
    mask=(f'<defs id="exterior-outline-mask-defs"><mask id="exterior-outside-mask" '
          f'maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" '
          f'x="{vx:g}" y="{vy:g}" width="{vw:g}" height="{vh:g}" style="mask-type:luminance">'
          f'<rect x="{vx:g}" y="{vy:g}" width="{vw:g}" height="{vh:g}" fill="#ffffff"/>'
          f'{geometry}</mask></defs>')
    text=text.replace(header,header+'\n'+mask,1)
    item.update(outline_width=round(thickness,9),outline_cells=CORE_STROKE_PER_CELL,
                path_width=span,outline_mode='outside',transparent_padding=pad)
    return text

def normalize():
    records=json.loads((HERE/'manifest.json').read_text(encoding='utf-8'))
    bench_name='CosmicWorkbench_north-refresh-v1.svg'
    shutil.copyfile(ROOT/'Art/RemainingBuildings/CosmicWorkbench_north-v15.svg',HERE/bench_name)
    records.append(dict(name='CosmicWorkbench_north',title='宇宙工作台',svg=bench_name,
                        png=bench_name.replace('.svg','.png'),footprint=[3,1]))
    for item in records:
        source=HERE/item['svg']
        text=source.read_text(encoding='utf-8')
        updated=outward_contour(text,item)
        source.write_text(updated,encoding='utf-8')
    # Keep the original v27 immutable. The current core gets the same thinner,
    # outside-only outline as all other buildings, following its actual casing.
    core_svg='StorageCore-refresh-v1.svg'
    text=(ROOT/'Art/StorageCore/StorageCore-v27.svg').read_text(encoding='utf-8')
    text=text.replace('href="#expanded-exterior-geometry" id="exterior-contour"',
                      'href="#chassis-outline-geometry" id="exterior-contour"')
    core=dict(name='StorageCore',title='存储控制核心',svg=core_svg,
              png=core_svg.replace('.svg','.png'),footprint=[2,2])
    (HERE/core_svg).write_text(outward_contour(text,core),encoding='utf-8')
    records.insert(0,core)
    (HERE/'outline-manifest.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Normalized 12 SVG outlines: outside-only, 2/3 original width, straight joins; core shape retained.')

if __name__=='__main__':
    normalize()
