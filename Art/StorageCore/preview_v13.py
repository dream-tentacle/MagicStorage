from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
old = Image.open(ROOT/'StorageCore-v12.png').convert('RGBA')
image = Image.open(ROOT/'StorageCore-v13.png').convert('RGBA')
assert image.size==old.size==(512,512)
assert image.getchannel('A').getextrema()==(0,255)
assert image.getchannel('A').getbbox()==old.getchannel('A').getbbox()
# This revision only restores the lower face's shading.
assert image.crop((0,0,512,405)).tobytes()==old.crop((0,0,512,405)).tobytes()
sheet = Image.new('RGBA',(1120,730),'#272d31')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
draw.text((24,18),'存储控制核心 · 恢复圆角转折阴影',font=font,fill='#e5edf1')
for y in range(92,604,16):
    for x in range(24,536,16):
        color='#e3e5e2' if ((x-24)//16+(y-92)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(image,(24,92))
for x,sprite,label in [(570,old,'修改前 · 平直阴影'),(840,image,'本次 · 恢复转折')]:
    draw.text((x,132),label,font=small,fill='#e5edf1')
    corner = sprite.crop((60,372,132,452)).resize((252,280),Image.Resampling.LANCZOS)
    draw.rectangle((x,175,x+251,454),fill='#b8bdb9')
    sheet.alpha_composite(corner,(x,175))
draw.text((570,498),'恢复后的底边',font=small,fill='#e5edf1')
base = image.crop((60,380,452,452)).resize((516,95),Image.Resampling.LANCZOS)
draw.rectangle((570,540,1085,634),fill='#b8bdb9')
sheet.alpha_composite(base,(570,540))
draw.text((24,677),'v13 · 保留圆环与边缘衔接修正',font=small,fill='#b3c1c8')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v13.png')
print('Restored original swept shadow; closed orbit and chassis geometry unchanged.')
print(ROOT/'StorageCore-review-v13.png')
