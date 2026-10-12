from pathlib import Path
from PIL import Image, ImageDraw
import xml.etree.ElementTree as ET
import colorsys
import re

root = Path(__file__).resolve().parent

def neutralize_metal():
    source = root / 'StorageCore.svg'
    text = source.read_text(encoding='utf-8')
    def neutral(match):
        rgb = tuple(int(match.group()[i:i + 2], 16) / 255 for i in (1, 3, 5))
        hue, saturation, value = colorsys.rgb_to_hsv(*rgb)
        if 55 <= hue * 360 <= 155 and saturation < .36:
            gray = round((rgb[0] * .2126 + rgb[1] * .7152 + rgb[2] * .0722) * 255)
            return '#' + f'{gray:02x}' * 3
        return match.group()
    text = re.sub(r'#[0-9a-fA-F]{6}\b', neutral, text)
    ET.fromstring(text)
    source.write_text(text, encoding='utf-8')

def preview():
    sprite = Image.open(root / 'StorageCore-v10.png').convert('RGBA')
    sheet = Image.new('RGBA', (960, 624), '#242727')
    draw = ImageDraw.Draw(sheet)
    draw.text((28, 20), 'STORAGE CONTROL CORE  /  SVG STUDY 10  /  SKY BLUE: 197, 229, 245', fill='#d3d9d7')
    # The same asset at three scales: checkerboard and two muted map-like surfaces.
    for y in range(64, 576, 16):
        for x in range(24, 536, 16):
            fill = '#e3e4df' if ((x - 24) // 16 + (y - 64) // 16) % 2 else '#f1f2ed'
            draw.rectangle((x, y, x + 15, y + 15), fill=fill)
    sheet.alpha_composite(sprite, (24, 64))
    for bounds, fill in [((584, 64, 904, 384), '#7c8273'), ((584, 424, 744, 584), '#8a8680')]:
        draw.rectangle(bounds, fill=fill)
        step = 128 if bounds[0] == 584 and bounds[1] == 64 else 64
        center_x = (bounds[0] + bounds[2]) // 2
        center_y = (bounds[1] + bounds[3]) // 2
        draw.line((center_x, bounds[1], center_x, bounds[3]), fill='#696e65')
        draw.line((bounds[0], center_y, bounds[2], center_y), fill='#696e65')
    sheet.alpha_composite(sprite.resize((256, 256), Image.Resampling.LANCZOS), (616, 96))
    sheet.alpha_composite(sprite.resize((128, 128), Image.Resampling.LANCZOS), (600, 440))
    draw.text((24, 590), '512 px / alpha checker', fill='#c0c7c3')
    draw.text((584, 395), '256 px', fill='#c0c7c3')
    draw.text((770, 500), '128 px', fill='#c0c7c3')
    sheet.convert('RGB').save(root / 'StorageCore-review-v10.png')
    alpha = sprite.getchannel('A')
    print(f'PNG: {sprite.size}, RGBA, alpha range {alpha.getextrema()}, content bounds {alpha.getbbox()}')
    print(root / 'StorageCore-review-v10.png')

if __name__ == '__main__':
    import sys
    if '--neutralize' in sys.argv:
        neutralize_metal()
    else:
        preview()
