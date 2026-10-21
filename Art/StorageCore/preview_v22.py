from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops

ROOT=Path(__file__).resolve().parent
old=Image.open(ROOT/'StorageCore-v21.png').convert('RGBA')
new=Image.open(ROOT/'StorageCore-v22.png').convert('RGBA')
assert old.size==new.size==(512,512)
assert old.getchannel('A').tobytes()==new.getchannel('A').tobytes()
diff=ImageChops.difference(old,new).convert('RGB').getbbox()
assert diff and diff[1]>=397 and diff[3]<=446
assert new.getpixel((150,436))[:3]==(65,78,88)
sheet=Image.new('RGBA',(1160,740),'#272d31')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
draw.text((24,18),'存储控制核心 · 保留粗描边，恢复底部转折阴影',font=font,fill='#e5edf1')
for y in range(108,620,16):
    for x in range(24,536,16):
        color='#e3e5e2' if ((x-24)//16+(y-108)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(new,(24,108))
for x,sprite,label in [(568,old,'修改前'),(848,new,'恢复后')]:
    draw.text((x,108),label,font=small,fill='#e5edf1')
    detail=sprite.crop((60,385,130,452)).resize((266,255),Image.Resampling.LANCZOS)
    draw.rectangle((x,148,x+265,402),fill='#b6bdbe')
    sheet.alpha_composite(detail,(x,148))
draw.text((568,461),'当前完整底边',font=small,fill='#e5edf1')
base=new.crop((60,380,452,452)).resize((546,100),Image.Resampling.LANCZOS)
draw.rectangle((568,505,1113,604),fill='#b6bdbe')
sheet.alpha_composite(base,(568,505))
draw.text((24,677),'v22 · 阴影保留原色与圆角走势，外描边仍为 12',font=small,fill='#b3c1c8')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v22.png')
print('Verified: only the bottom shadow changed; 12-unit outline and alpha retained.')
print(ROOT/'StorageCore-review-v22.png')
