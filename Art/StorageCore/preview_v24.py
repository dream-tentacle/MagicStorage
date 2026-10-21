from pathlib import Path
from collections import Counter
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
old=Image.open(ROOT/'StorageCore-v23.png').convert('RGBA')
new=Image.open(ROOT/'StorageCore-v24.png').convert('RGBA')
assert old.getchannel('A').tobytes()==new.getchannel('A').tobytes()
assert old.crop((0,0,512,400)).tobytes()==new.crop((0,0,512,400)).tobytes()
assert new.getpixel((150,436))[:3]==(83,97,107)
pixels=Counter(new.convert('RGB').crop((64,400,112,446)).get_flattened_data())
assert pixels[(65,78,88)]>70
sheet=Image.new('RGBA',(1040,700),'#272d31')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',25)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
draw.text((24,18),'存储控制核心 · 恢复沿圆角转折的暗面',font=font,fill='#e5edf1')
for y in range(96,608,16):
    for x in range(24,536,16):
        color='#e3e5e2' if ((x-24)//16+(y-96)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+15,y+15),fill=color)
sheet.alpha_composite(new,(24,96))
draw.text((592,112),'左下圆角放大',font=small,fill='#e5edf1')
corner=new.crop((58,382,130,454)).resize((360,360),Image.Resampling.LANCZOS)
for y in range(158,518,20):
    for x in range(592,952,20):
        color='#e3e5e2' if ((x-592)//20+(y-158)//20)%2 else '#f0f2ed'
        draw.rectangle((x,y,x+19,y+19),fill=color)
sheet.alpha_composite(corner,(592,158))
draw.text((592,562),'暗面紧贴圆角内侧，',font=small,fill='#b3c1c8')
draw.text((592,594),'中间直边保留原底色。',font=small,fill='#b3c1c8')
sheet.convert('RGB').save(ROOT/'StorageCore-review-v24.png')
print('Verified: corner shade visible next to the black contour; straight edge unshaded.')
print(ROOT/'StorageCore-review-v24.png')
