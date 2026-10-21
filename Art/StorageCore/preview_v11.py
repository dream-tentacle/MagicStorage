from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
sprite_path = PROJECT/'Textures/Things/Building/StorageCore/StorageCore.png'
definition_path = PROJECT/'Defs/ThingDefs/StorageBuildings.xml'
before = {str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in (sprite_path,definition_path)}
svg = ET.parse(ROOT/'StorageCore-v11.svg').getroot()
assert not any(element.tag.endswith('Gradient') for element in svg.iter())
black = [element for element in svg.iter() if element.get('stroke')=='#000000']
assert len(black)==1 and black[0].get('stroke-width')=='4'
assert any(element.get('fill')=='#c5e5f5' for element in svg.iter())
new = Image.open(ROOT/'StorageCore-v11.png').convert('RGBA')
old = Image.open(ROOT/'StorageCore-v10.png').convert('RGBA')
assert old.size==new.size==(512,512)
assert new.getchannel('A').getextrema()==(0,255)
old_svg = ET.parse(ROOT/'StorageCore-v10.svg').getroot()
assert black[0].get('d')==list(old_svg)[-1].get('d')
# The former lower bevel's antialiasing extended one pixel beyond the contour.
assert all(abs(a-b)<=1 for a,b in zip(new.getchannel('A').getbbox(),old.getchannel('A').getbbox()))

sheet = Image.new('RGBA',(1280,1040),'#272d31')
draw = ImageDraw.Draw(sheet)
title_font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',28)
label_font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
small_font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
draw.text((28,18),'存储控制核心 · 结构与明暗调整',font=title_font,fill='#e5edf1')
draw.text((28,60),'保留圆角方形与球体环绕结构；集中主装置，精简周边装饰',font=small_font,fill='#b3c1c8')
for column,(sprite,label) in enumerate([(old,'原版设计 · v10'),(new,'修改样稿 · v11')]):
    x0 = 24+column*640
    draw.text((x0+16,103),label,font=label_font,fill='#e5edf1')
    for y in range(144,656,16):
        for x in range(x0+40,x0+552,16):
            color='#e3e5e2' if ((x-x0-40)//16+(y-144)//16)%2 else '#f0f2ed'
            draw.rectangle((x,y,x+15,y+15),fill=color)
    sheet.alpha_composite(sprite,(x0+40,144))

draw.text((28,687),'灰度对比',font=label_font,fill='#e5edf1')
for x,sprite,label in [(40,old,'v10'),(290,new,'v11')]:
    gray = ImageOps.grayscale(sprite).convert('RGBA')
    gray.putalpha(sprite.getchannel('A'))
    gray = gray.resize((224,224),Image.Resampling.LANCZOS)
    draw.rectangle((x,738,x+223,961),fill='#7c8273')
    sheet.alpha_composite(gray,(x,738))
    draw.text((x+88,975),label,font=small_font,fill='#bdc9d0')

draw.text((660,687),'修改稿缩小显示 · 按 2×2 占地',font=label_font,fill='#e5edf1')
draw.rectangle((648,738,1255,961),fill='#7c8273')
body = new.crop(new.getchannel('A').getbbox())
for index,ppu in enumerate([48,72,96]):
    width = ppu*2
    sprite = body.resize((width,width),Image.Resampling.LANCZOS)
    cx = 749+index*202
    sheet.alpha_composite(sprite,(cx-width//2,850-width//2))
    draw.text((cx-49,975),f'{ppu} px / 格',font=small_font,fill='#bdc9d0')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v11.png')
assert before=={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in (sprite_path,definition_path)}
report = {'source':'StorageCore-v11.svg','png':[512,512],
          'alpha_bounds':new.getchannel('A').getbbox(),'exterior_stroke':{'color':'#000000','width':4},
          'accent':'#C5E5F5','review_only':True,'game_files_changed':False}
(ROOT/'StorageCore-v11.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('SVG and alpha verified; exterior contour and bounds preserved; game assets unchanged.')
print(ROOT/'StorageCore-review-v11.png')
