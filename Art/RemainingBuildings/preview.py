from pathlib import Path
import json
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent
manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 24)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
title_font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 28)
sprites = {}
for asset in manifest:
    stem = root / asset['stem']
    ET.parse(stem.with_suffix('.svg'))
    sprite = Image.open(stem.with_suffix('.png')).convert('RGBA')
    assert sprite.size == (asset['width'], asset['height'])
    assert sprite.getchannel('A').getextrema() == (0, 255)
    sprites[asset['stem']] = sprite


def checker(draw, box, step=20):
    x0, y0, x1, y1 = box
    for y in range(y0, y1, step):
        for x in range(x0, x1, step):
            fill = '#e3e4df' if ((x-x0)//step+(y-y0)//step)%2 else '#f1f2ed'
            draw.rectangle((x, y, min(x+step-1, x1-1), min(y+step-1, y1-1)), fill=fill)


sheet = Image.new('RGBA', (1600, 1580), '#242727')
draw = ImageDraw.Draw(sheet)
draw.text((32, 23), '魔法存储 · 其余设备第一稿', font=title_font, fill='#e4eceb')
draw.text((33, 67), '天蓝色 #C5E5F5  /  灰白机身  /  黑色外轮廓', font=small, fill='#aab8b1')
for i, (stem, label) in enumerate([
    ('ConstructionSupplyUnit-v1', '建筑供给单元 · 1×1'),
    ('FuelSupplier-v1', '燃料供给器 · 1×1'),
    ('NivarianCannonExtractor-v1', '质量驱动器提取口 · 1×1'),
]):
    x = 32 + i*520
    draw.text((x+6, 113), label, font=font, fill='#e4eceb')
    checker(draw, (x, 160, x+496, 656))
    sheet.alpha_composite(sprites[stem].resize((480,480), Image.Resampling.LANCZOS), (x+8,168))

draw.text((38, 694), '宇宙工作台 · 3×1 · 南向', font=font, fill='#e4eceb')
checker(draw, (32, 740, 1568, 1052))
sheet.alpha_composite(sprites['CosmicWorkbench_south-v1'].resize((924,308), Image.Resampling.LANCZOS), (338,742))
draw.text((38, 1082), '衣物适配器 · 2×1 · 南向', font=font, fill='#e4eceb')
checker(draw, (32, 1128, 1080, 1548))
sheet.alpha_composite(sprites['StorageApparelAdapter_south-v1'].resize((840,420), Image.Resampling.LANCZOS), (136,1128))
draw.text((1120, 1082), '存储线 · 连接示意', font=font, fill='#e4eceb')
draw.rectangle((1112, 1128, 1567, 1547), fill='#7c8273')
atlas = sprites['StorageConduit_Atlas-v1']


def tile(mask, size):
    x, y = (mask%4)*256+32, (3-mask//4)*256+32
    return atlas.crop((x,y,x+192,y+192)).resize((size,size), Image.Resampling.LANCZOS)


for col, mask in enumerate([2,10,12]):
    sheet.alpha_composite(tile(mask, 72), (1228+col*72,1254))
for col, mask in enumerate([2,11,9]):
    sheet.alpha_composite(tile(mask, 72), (1228+col*72,1326))
sheet.convert('RGB').save(root / 'RemainingBuildings-review-v1.png')

# Direction studies retain comparable per-cell scale instead of filling each cell.
sheet = Image.new('RGBA', (1600, 1530), '#242727')
draw = ImageDraw.Draw(sheet)
draw.text((32,23), '工作台与衣物适配器 · 三个方向', font=title_font, fill='#e4eceb')
for row, (stem, label, width) in enumerate([
    ('CosmicWorkbench', '宇宙工作台', 1536),
    ('StorageApparelAdapter', '衣物适配器', 1024),
]):
    y = 96+row*702
    draw.text((38,y), label, font=font, fill='#e4eceb')
    for col, (direction, name) in enumerate([('south','南向'),('north','北向'),('east','东向')]):
        x = 32+col*520
        draw.text((x+8,y+48),name,font=small,fill='#aab8b1')
        checker(draw,(x,y+87,x+496,y+653))
        source=sprites[f'{stem}_{direction}-v1']
        scale = min(480/width, 540/width, 0.4)
        size=(round(source.width*scale),round(source.height*scale))
        sheet.alpha_composite(source.resize(size,Image.Resampling.LANCZOS), (x+(496-size[0])//2,y+87+(566-size[1])//2))
sheet.convert('RGB').save(root / 'RemainingBuildings-directions-v1.png')

# All 16 clipped UV tiles and a continuous sample, useful for checking joints.
sheet=Image.new('RGBA',(1160,870),'#242727')
draw=ImageDraw.Draw(sheet)
draw.text((30,24),'存储线 · 十六种连接形态',font=title_font,fill='#e4eceb')
for mask in range(16):
    x=32+(mask%4)*200
    y=98+(mask//4)*180
    draw.text((x+8,y),f'{mask:02d}',font=small,fill='#aab8b1')
    draw.rectangle((x,y+30,x+159,y+161),fill='#7c8273')
    sheet.alpha_composite(tile(mask,128),(x+16,y+32))
draw.text((868,102),'连续连接',font=font,fill='#e4eceb')
draw.rectangle((852,159,1123,760),fill='#7c8273')
sample={(0,0):2,(1,0):14,(2,0):8,(1,1):5,(1,2):7,(2,2):8,(1,3):5,(1,4):3,(2,4):8}
for (col,row),mask in sample.items():
    sheet.alpha_composite(tile(mask,88),(856+col*88,193+row*88))
sheet.convert('RGB').save(root / 'StorageConduit-review-v1.png')
print(f'Validated {len(manifest)} SVG + RGBA PNG assets; created 3 review sheets.')
