from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
svg = ET.parse(ROOT/'StorageCore-v12.svg').getroot()
ids = {e.get('id'):e for e in svg.iter() if e.get('id')}
black = [e for e in svg.iter() if e.get('stroke')=='#000000']
assert len(black)==1 and black[0].get('stroke-width')=='4'
assert ids['base'][0].get('href')==black[0].get('href')
assert ids['orbit-loop-geometry'].get('d').startswith(ids['orbit-front-geometry'].get('d'))
image = Image.open(ROOT/'StorageCore-v12.png').convert('RGBA')
assert image.size==(512,512) and image.getchannel('A').getextrema()==(0,255)
bounds = image.getchannel('A').getbbox()
assert bounds[0]>0 and bounds[1]>0 and bounds[2]<512 and bounds[3]<512

sheet = Image.new('RGBA',(1120,730),'#272d31')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
draw.text((24,18),'存储控制核心 · 圆环与底座接缝修正',font=font,fill='#e5edf1')
for y in range(92,604,16):
    for x in range(24,536,16):
        color = '#e3e5e2' if ((x-24)//16+(y-92)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(image,(24,92))
draw.text((570,80),'圆环 · 前后段共用闭合轨迹',font=small,fill='#e5edf1')
ring = image.crop((172,194,340,302)).resize((504,324),Image.Resampling.LANCZOS)
draw.rectangle((570,120,1073,443),fill='#a6aaa8')
sheet.alpha_composite(ring,(570,120))
draw.text((570,480),'底座 · 横边与两侧圆角连续衔接',font=small,fill='#e5edf1')
base = image.crop((60,378,452,452)).resize((504,95),Image.Resampling.LANCZOS)
draw.rectangle((570,523,1073,617),fill='#a6aaa8')
sheet.alpha_composite(base,(570,523))
draw.text((24,677),'修正样稿 v12 · 尚未替换游戏内版本',font=small,fill='#b3c1c8')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v12.png')
print('Verified shared orbit geometry, shared chassis outline and uniform exterior stroke.')
print(ROOT/'StorageCore-review-v12.png')
