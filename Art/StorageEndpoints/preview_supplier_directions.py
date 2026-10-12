from pathlib import Path
from PIL import Image, ImageDraw
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
sheet = Image.new('RGBA', (1280, 650), '#242727')
draw = ImageDraw.Draw(sheet)
draw.text((24, 20), 'MAGIC STORAGE / MATERIAL SUPPLIER / THREE DIRECTIONS / SKY BLUE: 197, 229, 245', fill='#d3d9d7')
for i, direction in enumerate(['south', 'north', 'east']):
    stem = root / f'TransportSupplier_{direction}-v3'
    ET.parse(stem.with_suffix('.svg'))
    sprite = Image.open(stem.with_suffix('.png')).convert('RGBA')
    assert sprite.size == (512, 512)
    assert sprite.getchannel('A').getextrema() == (0, 255)
    x = 16 + i * 416
    draw.text((x + 14, 58), direction.upper(), fill='#d3d9d7')
    for y in range(88, 488, 16):
        for px in range(x, x + 400, 16):
            color = '#e3e4df' if ((px - x) // 16 + (y - 88) // 16) % 2 else '#f1f2ed'
            draw.rectangle((px, y, px + 15, y + 15), fill=color)
    sheet.alpha_composite(sprite.resize((384, 384), Image.Resampling.LANCZOS), (x + 8, 96))
    draw.rectangle((x, 514, x + 399, 628), fill='#7c8273')
    sheet.alpha_composite(sprite.resize((108, 108), Image.Resampling.LANCZOS), (x + 146, 517))
    print(direction, sprite.size, 'transparent RGBA')
destination = root / 'TransportSupplier-directions-review-v3.png'
sheet.convert('RGB').save(destination)
print(destination)
