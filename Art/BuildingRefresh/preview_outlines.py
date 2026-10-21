from pathlib import Path
import json
import xml.etree.ElementTree as ET
from PIL import Image,ImageDraw,ImageFont
from normalize_outlines import CORE_STROKE_PER_CELL

HERE=Path(__file__).resolve().parent
items=json.loads((HERE/'outline-manifest.json').read_text(encoding='utf-8'))
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
canvas=Image.new('RGB',(1420,1490),'#303639')
d=ImageDraw.Draw(canvas)
d.text((24,16),'外围黑色描边 · 原粗细的 2/3',font=font,fill='#edf1f2')
d.text((24,54),'SVG 向外描边 · 普通机壳小切角 · 此图检查造型，游戏间距另见密排检查图',font=small,fill='#b8cbd6')
scale=90
for i,item in enumerate(items):
    x=16+(i%3)*470;y=98+(i//3)*345
    d.text((x+8,y),item['title'],font=small,fill='#edf1f2')
    d.rectangle((x,y+35,x+454,y+323),fill='#858a80')
    cx=x+227;cy=y+179
    for gx in range(cx-180,cx+181,scale):d.line((gx,y+35,gx,y+323),fill='#969b91')
    for gy in range(cy-90,cy+91,scale):d.line((x,gy,x+454,gy),fill='#969b91')
    im=Image.open(HERE/item['png']).convert('RGBA')
    assert im.getbbox() and im.getbbox()[0]>0 and im.getbbox()[1]>0
    assert im.getbbox()[2]<im.width and im.getbbox()[3]<im.height
    im=im.crop(im.getbbox())
    width=round(item['footprint'][0]*scale)
    height=round(im.height*width/im.width)
    im=im.resize((width,height),Image.Resampling.LANCZOS)
    canvas.paste(im,(cx-width//2,cy-height//2),im)
    root=ET.parse(HERE/item['svg']).getroot()
    outlines=[e for e in root.iter() if e.get('stroke')=='#000000']
    assert len(outlines)==1,item['name']
    source_stroke=float(outlines[0].get('stroke-width'))
    if item.get('outline_mode')=='outside':
        stroke=source_stroke/2
        actual=stroke/(item['path_width']+2*stroke)*item['footprint'][0]
        assert root.find('.//{http://www.w3.org/2000/svg}mask[@id="exterior-outside-mask"]') is not None
    else:
        stroke=source_stroke
        actual=stroke/(item['path_width']+stroke)*item['footprint'][0]
    assert abs(actual-CORE_STROKE_PER_CELL)<1e-10,(item['name'],actual)
canvas.save(HERE/'BuildingRefresh-outline-review.png')
print('Verified 12 SVG exterior strokes at the shared horizontal cell scale; exported outline review.')
