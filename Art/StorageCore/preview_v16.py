from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops

ROOT = Path(__file__).resolve().parent
image = Image.open(ROOT/'StorageCore-v16.png').convert('RGBA')
previous = Image.open(ROOT/'StorageCore-v15.png').convert('RGBA')
ring = Image.open(ROOT/'StorageCore-ring-v16.png').convert('RGBA')
assert image.size==(512,512) and ring.size==(512,282)
assert image.getchannel('A').tobytes()==previous.getchannel('A').tobytes()
diff = ImageChops.difference(image,previous).convert('RGB').getbbox()
assert diff and diff[0]>=166 and diff[1]>=190 and diff[2]<=346 and diff[3]<=306

sheet = Image.new('RGBA',(1160,850),'#272d31')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
draw.text((24,18),'存储控制核心 · 浅蓝整环与前侧渐窄阴影',font=font,fill='#e5edf1')
draw.text((24,64),'圆环只有两片色形：完整浅蓝环带 + 单片前侧阴影',font=small,fill='#b3c1c8')
for y in range(118,630,16):
    for x in range(24,536,16):
        color='#e3e5e2' if ((x-24)//16+(y-118)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(image,(24,118))
draw.text((576,108),'圆环本体 · 无球体遮挡',font=small,fill='#e5edf1')
draw.rectangle((576,144,1087,425),fill='#b6bdbe')
sheet.alpha_composite(ring,(576,144))
draw.text((576,448),'装配效果',font=small,fill='#e5edf1')
detail = image.crop((168,180,340,306)).resize((480,352),Image.Resampling.LANCZOS)
draw.rectangle((576,482,1087,833),fill='#aeb5b5')
sheet.alpha_composite(detail,(592,482))
draw.text((24,682),'阴影在前半圈中段较宽，',font=small,fill='#b3c1c8')
draw.text((24,716),'向两端连续收尖至零。',font=small,fill='#b3c1c8')
draw.text((24,782),'v16 · SVG 设计稿',font=small,fill='#b3c1c8')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v16.png')
print('Verified: two orbit color shapes; other sprite regions and alpha unchanged.')
print(ROOT/'StorageCore-review-v16.png')
