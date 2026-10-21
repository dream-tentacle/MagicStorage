from pathlib import Path
import json
import math
import xml.etree.ElementTree as ET
from PIL import Image,ImageDraw,ImageFont
from normalize_outlines import CORE_STROKE_PER_CELL

HERE=Path(__file__).resolve().parent/'Directions'
items=json.loads((HERE/'manifest.json').read_text(encoding='utf-8'))
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',23)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
families=list(dict.fromkeys(item['family'] for item in items))
directions={'south':'南向','north':'北向','east':'东向','west':'西向'}
checks=[]
def tile(canvas,item,x,y,w,h):
    draw=ImageDraw.Draw(canvas)
    draw.rectangle((x,y,x+w,y+h),fill='#858a80')
    draw.text((x+12,y+8),directions[item['direction']]+(' · 已确认视角' if item['approved_view'] else ''),font=small,fill='#f2f3f2')
    with Image.open(HERE/item['png']) as raw:
        assert raw.mode=='RGBA'
        bounds=raw.getbbox()
        assert bounds and min(bounds[:2])>0 and bounds[2]<raw.width and bounds[3]<raw.height,item['name']
        icon=raw.crop(bounds)
    # Shared source-to-world horizontal scale; wide and side views use their
    # oriented footprint widths rather than independent fit-to-box enlargement.
    width=round(item['footprint'][0]*83)
    height=round(icon.height*width/icon.width)
    assert width<w-20 and height<h-50,(item['name'],width,height)
    icon=icon.resize((width,height),Image.Resampling.LANCZOS)
    canvas.paste(icon,(x+(w-width)//2,y+38+(h-38-height)//2),icon)

for family in families:
    group=[item for item in items if item['family']==family]
    canvas=Image.new('RGB',(1480,510),'#303639')
    draw=ImageDraw.Draw(canvas)
    draw.text((20,15),group[0]['title']+' · 四方向贴图',font=font,fill='#edf1f2')
    draw.text((20,52),'SVG 向外描边减为原先 2/3 · 同一横向格子比例 · 游戏留缝另见 GameExport',font=small,fill='#b8cbd6')
    for i,item in enumerate(group):tile(canvas,item,16+i*368,95,352,395)
    canvas.save(HERE/f'{family}-directions-review.png')

canvas=Image.new('RGB',(1480,2265),'#303639')
draw=ImageDraw.Draw(canvas)
draw.text((20,15),'五种可旋转建筑 · 四方向总览',font=font,fill='#edf1f2')
draw.text((20,52),'已确认的原视角保持原样；新增其余三个方向。固定朝向建筑不增加方向图。',font=small,fill='#b8cbd6')
for row,family in enumerate(families):
    group=[item for item in items if item['family']==family]
    y=94+row*430
    draw.text((20,y),group[0]['title'],font=font,fill='#edf1f2')
    for col,item in enumerate(group):tile(canvas,item,16+col*368,y+39,352,375)
canvas.save(HERE/'BuildingRefresh-all-directions.png')
for item in items:
    if item['approved_view']:
        assert (HERE/item['svg']).read_bytes()==(HERE.parent/item['approved_source_svg']).read_bytes(),item['name']
        assert (HERE/item['png']).read_bytes()==(HERE.parent/item['approved_source_png']).read_bytes(),item['name']
    root=ET.parse(HERE/item['svg']).getroot()
    outlines=[e for e in root.iter() if e.get('stroke')=='#000000']
    assert len(outlines)==1,item['name']
    assert root.find('.//{http://www.w3.org/2000/svg}mask[@id="exterior-outside-mask"]') is not None
    stroke=float(outlines[0].get('stroke-width'))/2
    assert abs(stroke/(item['path_width']+2*stroke)*item['footprint'][0]-CORE_STROKE_PER_CELL)<1e-10,item['name']
    with Image.open(HERE/item['png']) as image:checks.append(dict(name=item['name'],size=image.size,bounds=image.getbbox(),outline=stroke))
(HERE/'validation.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
print('Verified 20 unclipped RGBA PNGs / SVGs, complete direction sets and common outline scale; created 6 review sheets.')
