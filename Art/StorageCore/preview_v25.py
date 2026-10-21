from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
old=Image.open(ROOT/'StorageCore-v24.png').convert('RGBA')
new=Image.open(ROOT/'StorageCore-v25.png').convert('RGBA')
assert old.getchannel('A').tobytes()==new.getchannel('A').tobytes()
assert new.getpixel((150,436))[:3]==(83,97,107)
sheet=Image.new('RGBA',(1160,700),'#272d31')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',25)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
draw.text((24,18),'存储控制核心 · 加大角落暗面的弧形过渡',font=font,fill='#e5edf1')
for y in range(96,608,16):
    for x in range(24,536,16):
        color='#e3e5e2' if ((x-24)//16+(y-96)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(new,(24,96))
for x,sprite,label in [(568,old,'上一稿'),(848,new,'加大弧形过渡')]:
    draw.text((x,128),label,font=small,fill='#e5edf1')
    corner=sprite.crop((58,382,130,454)).resize((266,266),Image.Resampling.LANCZOS)
    draw.rectangle((x,174,x+265,439),fill='#b6bdbe')
    sheet.alpha_composite(corner,(x,174))
draw.text((568,492),'内弧沿侧边与底边作圆滑过渡，',font=small,fill='#b3c1c8')
draw.text((568,528),'阴影仍只在两个角落。',font=small,fill='#b3c1c8')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v25.png')
print('Verified: alpha unchanged and the straight center remains unshaded.')
print(ROOT/'StorageCore-review-v25.png')
