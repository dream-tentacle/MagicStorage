from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops

ROOT = Path(__file__).resolve().parent
image = Image.open(ROOT/'StorageCore-v14.png').convert('RGBA')
previous = Image.open(ROOT/'StorageCore-v13.png').convert('RGBA')
assert image.size==previous.size==(512,512)
assert image.getchannel('A').tobytes()==previous.getchannel('A').tobytes()
diff = ImageChops.difference(image,previous)
bounds = diff.convert('RGB').getbbox()
assert bounds and bounds[0]>=254 and bounds[1]>=84 and bounds[2]<=417 and bounds[3]<=123
sheet = Image.new('RGBA',(960,700),'#272d31')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
draw.text((24,18),'存储控制核心 · 恢复节点控制面板',font=font,fill='#e5edf1')
for y in range(92,604,16):
    for x in range(24,536,16):
        color = '#e3e5e2' if ((x-24)//16+(y-92)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(image,(24,92))
draw.text((568,97),'控制面板放大',font=small,fill='#e5edf1')
detail = image.crop((250,80,398,127)).resize((370,118),Image.Resampling.LANCZOS)
sheet.alpha_composite(detail,(568,140))
draw.text((568,315),'缩小显示',font=small,fill='#e5edf1')
draw.rectangle((568,358,937,607),fill='#7c8273')
body = image.crop(image.getchannel('A').getbbox()).resize((192,192),Image.Resampling.LANCZOS)
sheet.alpha_composite(body,(657,387))
draw.text((24,650),'v14 · 圆环、圆角阴影及底边衔接保持当前版本',font=small,fill='#b3c1c8')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v14.png')
print('Verified: only controller pixels changed; alpha and remaining artwork preserved.')
print(ROOT/'StorageCore-review-v14.png')
