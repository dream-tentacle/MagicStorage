from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
manifest = json.loads((root / 'directions-manifest.json').read_text(encoding='utf-8'))
sheet = Image.new('RGBA', (1440, 920), '#242727')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 24)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
draw.text((28, 24), '存储单元 · 三个方向', font=font, fill='#e4eceb')
draw.text((28, 65), '沿用精简匣盖设计 · 天蓝色 #C5E5F5', font=small, fill='#aab8b1')
for i, (asset, label) in enumerate(zip(manifest, ['南向', '北向', '东向'])):
    stem = root / asset['stem']
    ET.parse(stem.with_suffix('.svg'))
    sprite = Image.open(stem.with_suffix('.png')).convert('RGBA')
    assert sprite.size == (asset['width'], asset['height'])
    assert sprite.getchannel('A').getextrema() == (0, 255)
    x = 24+i*472
    draw.text((x+8, 116), label, font=font, fill='#e4eceb')
    for y in range(162, 786, 20):
        for px in range(x, x+448, 20):
            color = '#e3e4df' if ((px-x)//20+(y-162)//20)%2 else '#f1f2ed'
            draw.rectangle((px,y,min(px+19,x+447),min(y+19,785)),fill=color)
    size = (round(sprite.width*0.85),round(sprite.height*0.85))
    # Wide artwork includes generous transparent margins, so fit its full canvas.
    factor = min(432/sprite.width, 608/sprite.height)
    size = (round(sprite.width*factor),round(sprite.height*factor))
    sheet.alpha_composite(sprite.resize(size,Image.Resampling.LANCZOS),(x+(448-size[0])//2,162+(624-size[1])//2))
    draw.rectangle((x,806,x+447,897),fill='#7c8273')
    factor = 0.14
    size=(round(sprite.width*factor),round(sprite.height*factor))
    sheet.alpha_composite(sprite.resize(size,Image.Resampling.LANCZOS),(x+(448-size[0])//2,806+(92-size[1])//2))
    print(asset['stem'], 'SVG + RGBA PNG validated')
output=root/'StorageUnit-directions-review-v2.png'
sheet.convert('RGB').save(output)
print(output)
