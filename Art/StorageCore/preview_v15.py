from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops

ROOT = Path(__file__).resolve().parent
image = Image.open(ROOT/'StorageCore-v15.png').convert('RGBA')
previous = Image.open(ROOT/'StorageCore-v14.png').convert('RGBA')
assert image.size==previous.size==(512,512)
assert image.getchannel('A').tobytes()==previous.getchannel('A').tobytes()
diff = ImageChops.difference(image,previous).convert('RGB').getbbox()
assert diff and diff[0]>=168 and diff[1]>=190 and diff[2]<=342 and diff[3]<=302
sheet = Image.new('RGBA',(1160,740),'#272d31')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
draw.text((24,18),'存储控制核心 · 原圆环弧线与单侧阴影',font=font,fill='#e5edf1')
draw.text((24,64),'恢复原来的弧线，仅延长后段以接合前段；保留深色侧面',font=small,fill='#b3c1c8')
for y in range(118,630,16):
    for x in range(24,536,16):
        color='#e3e5e2' if ((x-24)//16+(y-118)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(image,(24,118))
draw.text((576,116),'主装置放大',font=small,fill='#e5edf1')
detail = image.crop((158,154,352,344)).resize((543,532),Image.Resampling.LANCZOS)
draw.rectangle((576,154,1118,685),fill='#aeb5b5')
sheet.alpha_composite(detail,(576,154))
draw.text((24,684),'v15 · 控制面板、底座圆角阴影沿用当前稿',font=small,fill='#b3c1c8')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v15.png')
print('Verified unchanged alpha and artwork outside the orbit; SVG paths are joined.')
print(ROOT/'StorageCore-review-v15.png')
