from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
sheet = Image.new('RGBA', (1600, 800), '#242727')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 24)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
draw.text((32, 23), '衣物适配器 · 三个方向第二稿', font=font, fill='#e4eceb')
draw.text((32, 64), '统一衣物舱、正挂衣物与控制模块', font=small, fill='#aab8b1')
for i, (direction, label) in enumerate([('south','南向'), ('north','北向'), ('east','东向')]):
    stem = root / f'StorageApparelAdapter_{direction}-v2'
    svg = ET.parse(stem.with_suffix('.svg')).getroot()
    sprite = Image.open(stem.with_suffix('.png')).convert('RGBA')
    assert sprite.size == (int(svg.get('width')), int(svg.get('height')))
    assert sprite.getchannel('A').getextrema() == (0, 255)
    x = 32+i*520
    draw.text((x+8, 112), label, font=font, fill='#e4eceb')
    for y in range(158, 678, 20):
        for px in range(x,x+496,20):
            fill = '#e3e4df' if ((px-x)//20+(y-158)//20)%2 else '#f1f2ed'
            draw.rectangle((px,y,min(px+19,x+495),min(y+19,677)),fill=fill)
    size = (round(sprite.width*0.46),round(sprite.height*0.46))
    sheet.alpha_composite(sprite.resize(size,Image.Resampling.LANCZOS),(x+(496-size[0])//2,158+(520-size[1])//2))
    draw.rectangle((x,700,x+495,779),fill='#7c8273')
    size = (round(sprite.width*0.07),round(sprite.height*0.07))
    sheet.alpha_composite(sprite.resize(size,Image.Resampling.LANCZOS),(x+(496-size[0])//2,700+(80-size[1])//2))
    print(direction, 'SVG and transparent PNG validated')
destination = root / 'StorageApparelAdapter-directions-review-v2.png'
sheet.convert('RGB').save(destination)
print(destination)
