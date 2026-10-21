from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parents[1]
paths=[PROJECT/'Defs/ThingDefs/StorageBuildings.xml',*list((PROJECT/'Textures/Things/Building/CosmicWorkbench').glob('*.png'))]
before={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
source=ROOT/'CosmicWorkbench_north-v2.svg'
svg=ET.parse(source).getroot()
assert svg.get('viewBox')=='0 0 704 320'
contours=[e for e in svg.iter() if e.get('stroke')=='#000000']
assert len(contours)==1 and contours[0].get('stroke-width')=='12'
assert not any(e.tag.endswith('Gradient') for e in svg.iter())
sprite=Image.open(source.with_suffix('.png')).convert('RGBA')
assert sprite.size==(1408,640)
bounds=sprite.getchannel('A').getbbox()
assert bounds[0]>0 and bounds[1]>0 and bounds[2]<sprite.width and bounds[3]<sprite.height
# Source-space worker opening: central inner edge y243 versus outer front y266.
assert sprite.getpixel((352*2,258*2))[3]==0
assert sprite.getpixel((250*2,258*2))[3]==255

sheet=Image.new('RGBA',(1280,1000),'#272d31')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',28)
label=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
draw.text((28,18),'宇宙工作台 · 操作侧样稿',font=font,fill='#e5edf1')
draw.text((28,62),'中央浅梯形缺口 · 开放工作面与夹持件 · 沿用核心成图的材质和配色',font=small,fill='#b3c1c8')
draw.text((956,118),'风格参照：核心 v27',font=small,fill='#b3c1c8')
for y in range(112,528,16):
    for x in range(24,936,16):
        color='#e3e5e2' if ((x-24)//16+(y-112)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(sprite.resize((880,400),Image.Resampling.LANCZOS),(40,120))
core=Image.open(PROJECT/'Art/StorageCore/StorageCore-v27.png').convert('RGBA')
sheet.alpha_composite(core.resize((288,288),Image.Resampling.LANCZOS),(960,168))
draw.text((28,558),'原工作台',font=label,fill='#e5edf1')
draw.text((660,558),'修改样稿',font=label,fill='#e5edf1')
old=Image.open(ROOT/'CosmicWorkbench_north-v1.png').convert('RGBA')
for image,x in [(old,24),(sprite,656)]:
    draw.rectangle((x,602,x+599,827),fill='#7c8273')
    body=image.crop(image.getchannel('A').getbbox())
    fit=ImageOps.contain(body,(552,202),Image.Resampling.LANCZOS)
    sheet.alpha_composite(fit,(x+(600-fit.width)//2,602+(226-fit.height)//2))
draw.text((28,858),'缩小显示',font=small,fill='#b3c1c8')
body=sprite.crop(bounds)
for i,ppu in enumerate([48,72,96]):
    width=ppu*3
    image=ImageOps.contain(body,(width,ppu+40),Image.Resampling.LANCZOS)
    x=196+i*392
    sheet.alpha_composite(image,(x-image.width//2,920-image.height//2))
    draw.text((x-47,970),f'{ppu} px / 格',font=small,fill='#b3c1c8')
output=ROOT/'CosmicWorkbench-review-v2.png'
sheet.convert('RGB').save(output)
assert before=={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
report={'source':source.name,'style_reference':'StorageCore-v27.svg','view':'north',
        'interaction_cell_offset':[0,0,-1],'footprint':[3,1],
        'operator_notch':{'center_x':352,'mouth_width':138,'inner_width':92,'depth':23},
        'exterior_stroke':{'color':'#000000','width':12},'accent':'#C5E5F5',
        'review_only':True,'game_files_changed':False}
(ROOT/'CosmicWorkbench-v2.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('SVG, transparent PNG, black exterior contour and centered trapezoid opening verified. Game files unchanged.')
print(output)
