from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent
art = root.parent
assets = [
    ('存储控制核心 · 2×2', 'StorageCore/StorageCore-v10'),
    ('存储单元 · 1×2 · 南／北／东', 'StorageUnit/StorageUnit_south-v2'),
    ('存储接收器 · 1×1', 'StorageEndpoints/StorageReceiver-v1'),
    ('自动补货架 · 1×1 · 单图', 'StorageEndpoints/StorageSupplyShelf_south-v1'),
    ('物资补给器 · 1×1 · 南／北／东', 'StorageEndpoints/TransportSupplier_south-v3'),
    ('建筑供给单元 · 1×1', 'RemainingBuildings/ConstructionSupplyUnit-v1'),
    ('燃料供给器 · 1×1', 'RemainingBuildings/FuelSupplier-v1'),
    ('质量驱动器提取口 · 1×1', 'RemainingBuildings/NivarianCannonExtractor-v1'),
    ('衣物适配器 · 2×1 · 南／北／东', 'RemainingBuildings/StorageApparelAdapter_south-v2'),
]
sheet = Image.new('RGBA', (1600, 1720), '#242727')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 21)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
draw.text((28,22), '魔法存储 · 当前建筑图稿总览', font=font, fill='#e4eceb')
draw.text((28,64), '十种建筑 · 最新版本 · 天蓝色 #C5E5F5', font=small, fill='#aab8b1')
for i,(label,relative) in enumerate(assets):
    x=24+(i%3)*520
    y=112+(i//3)*436
    draw.text((x+8,y),label,font=font,fill='#e4eceb')
    draw.rectangle((x,y+42,x+495,y+413),fill='#7c8273')
    sprite=Image.open(art/(relative+'.png')).convert('RGBA')
    bounds=sprite.getchannel('A').getbbox()
    sprite=sprite.crop(bounds)
    scale=min(456/sprite.width,332/sprite.height)
    size=(round(sprite.width*scale),round(sprite.height*scale))
    sheet.alpha_composite(sprite.resize(size,Image.Resampling.LANCZOS),(x+(496-size[0])//2,y+42+(372-size[1])//2))
label='宇宙工作台 · 3×1 · 南／北／东'
draw.text((32,1440),label,font=font,fill='#e4eceb')
draw.rectangle((24,1482,1567,1687),fill='#7c8273')
sprite=Image.open(art/'RemainingBuildings/CosmicWorkbench_south-v1.png').convert('RGBA')
sprite=sprite.crop(sprite.getchannel('A').getbbox())
factor=180/sprite.height
size=(round(sprite.width*factor),180)
sheet.alpha_composite(sprite.resize(size,Image.Resampling.LANCZOS),((1600-size[0])//2,1494))
out=root/'Buildings-current-review.png'
sheet.convert('RGB').save(out)
print(out)

lines=['# 魔法存储建筑图稿清单', '', 'SVG 为源稿，PNG 为透明导出。自动补货架按用户要求只保留单图；存储线不在本次绘制范围。', '', '| 建筑 | 当前 SVG 源稿 |', '| --- | --- |']
for label,relative in assets:
    path=art/(relative+'.svg')
    lines.append(f'| {label} | [{path.stem}]({path.as_posix()}) |')
path=art/'RemainingBuildings/CosmicWorkbench_south-v1.svg'
lines.append(f'| 宇宙工作台 · 3×1 · 南／北／东 | [{path.stem}]({path.as_posix()}) |')
lines.extend(['', '可旋转建筑的北向、东向源稿与对应南向文件在同一目录。', ''])
(root/'Buildings-current.md').write_text('\n'.join(lines),encoding='utf-8')
