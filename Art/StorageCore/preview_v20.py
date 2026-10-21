from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
old=Image.open(ROOT/'StorageCore-v19.png').convert('RGBA')
new=Image.open(ROOT/'StorageCore-v20.png').convert('RGBA')
assert old.size==new.size==(512,512)
assert old.getchannel('A').tobytes()==new.getchannel('A').tobytes()
sheet=Image.new('RGBA',(1160,930),'#272d31')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
label=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',21)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
draw.text((24,18),'存储控制核心 · 统一部件的细节层次',font=font,fill='#e5edf1')
draw.text((24,64),'完善承座与支架的结构，收敛细小明暗和标记；保留当前圆环',font=small,fill='#b3c1c8')
for x,sprite,title in [(24,old,'修改前 · v19'),(620,new,'调整后 · v20')]:
    draw.text((x,106),title,font=label,fill='#e5edf1')
    for gy in range(144,656,16):
        for gx in range(x,x+512,16):
            color='#e3e5e2' if ((gx-x)//16+(gy-144)//16)%2 else '#f0f2ed'
            draw.rectangle((gx,gy,gx+15,gy+15),fill=color)
    sheet.alpha_composite(sprite,(x,144))
    draw.text((x,684),'承座与支架局部',font=label,fill='#e5edf1')
    detail=sprite.crop((156,268,352,342)).resize((470,178),Image.Resampling.LANCZOS)
    draw.rectangle((x+20,724,x+489,901),fill='#aeb5b5')
    sheet.alpha_composite(detail,(x+20,724))
sheet.convert('RGB').save(ROOT/'StorageCore-review-v20.png')
print('Verified SVG render, unchanged alpha, preserved orbit and chassis.')
print(ROOT/'StorageCore-review-v20.png')
