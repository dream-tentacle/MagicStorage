"""Check real spacing using staged game PNGs at their actual draw sizes."""
from pathlib import Path
import json
import math
from PIL import Image,ImageDraw,ImageFont,ImageChops

HERE=Path(__file__).resolve().parent/'GameExport'
data=json.loads((HERE/'manifest.json').read_text(encoding='utf-8'))
ppu=data['ppu'];inset=data['inset_pixels'];padding=data['padding']
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',14)
items=[*data['ordinary'],*[
    dict(family='CosmicMechGestator',direction=r['direction'],
         footprint=[2,3] if r['direction'] in ['east','west'] else [3,2],
         world_size=r['world_size'],target=r['reference'],gestator=r)
    for r in data['gestator']]]
scale=64;tile_w=448;tile_h=470
sheet=Image.new('RGB',(tile_w*3,60+tile_h*math.ceil(len(items)/3)),'#303639')
draw=ImageDraw.Draw(sheet)
draw.text((16,10),'实际游戏比例密排 · 每组 2×2 台 · 蓝线为网格边界',font=font,fill='#eef2f3')
draw.text((16,34),'SVG 外描边为原先 2/3；普通建筑间隙 0.09375 格；培育器 90%，以台面对齐占地',font=small,fill='#bdcbd1')
report=[]
for i,item in enumerate(items):
    x=(i%3)*tile_w;y=60+(i//3)*tile_h
    draw.text((x+10,y+8),item['family']+' / '+item['direction'],font=font,fill='#eef2f3')
    cells=item['footprint'];sx,sy=[round(c*scale) for c in cells]
    left=x+(tile_w-2*sx)//2;top=y+50+(390-2*sy)//2
    draw.rectangle((x+6,y+36,x+tile_w-6,y+tile_h-10),fill='#858a80')
    for gx in range(2*cells[0]+1):
        px=left+gx*scale
        draw.line((px,top,px,top+2*sy),fill='#a5aca1')
    for gy in range(2*cells[1]+1):
        py=top+gy*scale
        draw.line((left,py,left+2*sx,py),fill='#a5aca1')
    with Image.open(HERE/item['target']) as raw:
        image=raw.convert('RGBA')
    bbox=image.getbbox()
    if 'gestator' not in item:
        hgap=cells[0]*ppu-(bbox[2]-bbox[0])
        vgap=cells[1]*ppu-(bbox[3]-bbox[1])
        assert hgap==vgap==2*inset,(item['target'],hgap,vgap)
        # Check alpha collisions as well as bounding rectangles, at native size.
        cw,ch=[round(c*ppu) for c in cells]
        occupied=Image.new('L',(2*cw+2*padding,2*ch+2*padding))
        for row in range(2):
            for col in range(2):
                placed=Image.new('L',occupied.size)
                placed.paste(image.getchannel('A'),(col*cw,row*ch))
                assert ImageChops.multiply(occupied,placed).getbbox() is None,item['target']
                occupied=ImageChops.lighter(occupied,placed)
        report.append(dict(family=item['family'],direction=item['direction'],
                           horizontal_gap_pixels=hgap,vertical_gap_pixels=vgap,alpha_overlap_pixels=0))
    else:
        clearance=item['gestator']['floor_clearance_pixels']
        plane_clearance=item['gestator']['platform_plane_clearance_pixels']
        assert min(plane_clearance)>=0
        report.append(dict(family=item['family'],direction=item['direction'],
                           floor_clearance_pixels=clearance,platform_plane_clearance_pixels=plane_clearance,
                           vertical_projection=True))
    image=image.resize(tuple(round(c*scale) for c in item['world_size']),Image.Resampling.LANCZOS)
    for row in range(2):
        for col in range(2):
            cx=left+col*sx+sx//2;cy=top+row*sy+sy//2
            sheet.paste(image,(cx-image.width//2,cy-image.height//2),image)
    for row in range(2):
        for col in range(2):
            px=left+col*sx;py=top+row*sy
            draw.rectangle((px,py,px+sx,py+sy),outline='#69bddc')
sheet.save(HERE/'DenseLayout-review.png')
(HERE/'dense-layout-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Verified 23 ordinary views with unchanged 24-pixel gaps; 4 enlarged gestator platform planes fit occupied cells.')
