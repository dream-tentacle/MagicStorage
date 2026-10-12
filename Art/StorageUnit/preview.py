from pathlib import Path
from PIL import Image, ImageDraw
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
ET.parse(root / 'StorageUnit_south-v2.svg')
sprite = Image.open(root / 'StorageUnit_south-v2.png').convert('RGBA')
sheet = Image.new('RGBA', (960, 832), '#242727')
draw = ImageDraw.Draw(sheet)
draw.text((26, 20), 'STORAGE UNIT / SOUTH STUDY 02 / SKY BLUE: 197, 229, 245', fill='#d3d9d7')
for y in range(48, 816, 16):
    for x in range(24, 536, 16):
        fill = '#e3e4df' if ((x - 24) // 16 + (y - 48) // 16) % 2 else '#f1f2ed'
        draw.rectangle((x, y, x + 15, y + 15), fill=fill)
sheet.alpha_composite(sprite, (24, 48))
draw.text((548, 785), '512 x 768 / transparent', fill='#c0c7c3')
for bounds, fill in [((584, 56, 904, 416), '#7c8273'), ((584, 452, 904, 736), '#8a8680')]:
    draw.rectangle(bounds, fill=fill)
sheet.alpha_composite(sprite.resize((192, 288), Image.Resampling.LANCZOS), (544, 92))
sheet.alpha_composite(sprite.resize((96, 144), Image.Resampling.LANCZOS), (594, 492))
# Show the approved core beside the unit at approximately the same world scale.
core = Image.open(root.parent / 'StorageCore' / 'StorageCore-v10.png').convert('RGBA')
sheet.alpha_composite(core.resize((256, 256), Image.Resampling.LANCZOS), (674, 108))
sheet.alpha_composite(core.resize((128, 128), Image.Resampling.LANCZOS), (722, 500))
draw.text((584, 423), '192 x 288 / core beside unit', fill='#c0c7c3')
draw.text((584, 747), '96 x 144 / core beside unit', fill='#c0c7c3')
sheet.convert('RGB').save(root / 'StorageUnit-review-v2.png')
alpha = sprite.getchannel('A')
print(f'PNG: {sprite.size}, RGBA, alpha range {alpha.getextrema()}, content bounds {alpha.getbbox()}')
print(root / 'StorageUnit-review-v2.png')
