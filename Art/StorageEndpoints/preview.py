from pathlib import Path
from PIL import Image, ImageDraw
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
names = ['StorageReceiver-v1', 'StorageSupplyShelf_south-v1', 'TransportSupplier_south-v3']
labels = ['01  STORAGE RECEIVER', '02  RESTOCKING SHELF / SOUTH', '03  MATERIAL SUPPLIER / SOUTH']
sprites = []
for name in names:
    ET.parse(root / (name + '.svg'))
    sprite = Image.open(root / (name + '.png')).convert('RGBA')
    print(name, sprite.size, 'alpha:', sprite.getchannel('A').getextrema())
    sprites.append(sprite)

sheet = Image.new('RGBA', (1280, 888), '#242727')
draw = ImageDraw.Draw(sheet)
draw.text((24, 20), 'MAGIC STORAGE / ENDPOINT STUDIES 03 / SKY BLUE: 197, 229, 245', fill='#d3d9d7')
for i, sprite in enumerate(sprites):
    x = 16 + i * 416
    draw.text((x + 14, 58), labels[i], fill='#d3d9d7')
    for y in range(88, 488, 16):
        for px in range(x, x + 400, 16):
            fill = '#e3e4df' if ((px - x) // 16 + (y - 88) // 16) % 2 else '#f1f2ed'
            draw.rectangle((px, y, px + 15, y + 15), fill=fill)
    sheet.alpha_composite(sprite.resize((384, 384), Image.Resampling.LANCZOS), (x + 8, 96))
    draw.text((x + 14, 505), 'SVG + transparent PNG / 1 x 1 footprint', fill='#aab8b1')

draw.text((30, 550), 'FAMILY COMPARISON / approximately equal world scale', fill='#d3d9d7')
draw.rectangle((24, 574, 1256, 851), fill='#7c8273')
core = Image.open(root.parent / 'StorageCore' / 'StorageCore-v10.png').convert('RGBA')
unit = Image.open(root.parent / 'StorageUnit' / 'StorageUnit_south-v2.png').convert('RGBA')
sheet.alpha_composite(core.resize((256, 256), Image.Resampling.LANCZOS), (42, 584))
sheet.alpha_composite(unit.resize((192, 288), Image.Resampling.LANCZOS), (258, 573))
for sprite, x in zip(sprites, [488, 714, 940]):
    sheet.alpha_composite(sprite.resize((108, 108), Image.Resampling.LANCZOS), (x, 705))
for label, x in [('CORE', 132), ('UNIT', 332), ('RECEIVER', 514), ('SHELF', 747), ('SUPPLIER', 970)]:
    draw.text((x, 829), label, fill='#e5ebdf')
sheet.convert('RGB').save(root / 'StorageEndpoints-review-v3.png')
print(root / 'StorageEndpoints-review-v3.png')
