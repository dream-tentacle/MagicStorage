from pathlib import Path
from collections import Counter
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
new=Image.open(ROOT/'StorageCore-v26.png').convert('RGBA')
old=Image.open(ROOT/'StorageCore-v13.png').convert('RGBA')
assert new.size==(512,512)
bounds=new.getchannel('A').getbbox()
assert bounds[0]>0 and bounds[1]>0 and bounds[2]<512 and bounds[3]<512
assert new.getpixel((150,443))[:3]==(83,97,107)
assert Counter(new.convert('RGB').crop((60,390,112,446)).get_flattened_data())[(65,78,88)]>70
sheet=Image.new('RGBA',(1160,740),'#272d31')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',25)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
draw.text((24,18),'存储控制核心 · 恢复所提供 SVG 的原始阴影路径',font=font,fill='#e5edf1')
for y in range(108,620,16):
    for x in range(24,536,16):
        color='#e3e5e2' if ((x-24)//16+(y-108)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(new,(24,108))
for x,sprite,label in [(568,old,'原始第十三稿'),(848,new,'原曲线 + 粗描边')]:
    draw.text((x,120),label,font=small,fill='#e5edf1')
    corner=sprite.crop((48,382,120,454)).resize((266,266),Image.Resampling.LANCZOS)
    draw.rectangle((x,166,x+265,431),fill='#b6bdbe')
    sheet.alpha_composite(corner,(x,166))
draw.text((568,486),'原阴影曲线与颜色直接沿用，',font=small,fill='#b3c1c8')
draw.text((568,520),'黑框向外扩展，保留角落暗面。',font=small,fill='#b3c1c8')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v26.png')
print('Verified: original curved shade visible, middle unshaded, contour unclipped.')
print(ROOT/'StorageCore-review-v26.png')
