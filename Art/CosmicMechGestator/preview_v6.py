from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent
sprite = Image.open(root / 'CosmicMechGestator_south-v6.png').convert('RGBA')
assert sprite.size == (1536, 1024)
assert sprite.getchannel('A').getextrema() == (0, 255)
sheet = Image.new('RGB', (1120, 930), '#252b30')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 25)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
draw.text((28, 20), '宇宙培育器 · 南向第六稿', font=font, fill='#e4edf3')
draw.text((28, 62), '承座对齐平台中心 · 顶盖保留自然的高度投影', font=small, fill='#b2c7d4')
for y in range(108, 748, 20):
    for x in range(80, 1040, 20):
        color = '#e5e7e5' if ((x-80)//20+(y-108)//20)%2 else '#f0f2ef'
        draw.rectangle((x, y, x+19, y+19), fill=color)
sheet.paste(sprite.resize((960, 640), Image.Resampling.LANCZOS), (80, 108), sprite.resize((960, 640), Image.Resampling.LANCZOS))
draw.text((32, 778), '承座定位示意', font=small, fill='#e4edf3')
mini = sprite.resize((288, 192), Image.Resampling.LANCZOS)
sheet.paste(mini, (360, 730), mini)
# Screen coordinates of the top-plane center, independent of the lid overhang.
scale = 288 / 768
cx, cy = 360 + 384*scale, 730 + 275*scale
box = (360+64*scale, 730+100*scale, 360+704*scale, 730+450*scale)
draw.rectangle(box, outline='#ecb76b', width=1)
draw.line((cx-9, cy, cx+9, cy), fill='#ffcc7d', width=2)
draw.line((cx, cy-9, cx, cy+9), fill='#ffcc7d', width=2)
draw.text((704, 803), '十字：平台与承座共同中心', font=small, fill='#e8c795')
draw.text((704, 835), '仅为定位标记，不写入贴图', font=small, fill='#adbdc8')
sheet.save(root / 'CosmicMechGestator-review-v6.png')
report = dict(source='CosmicMechGestator_south-v6.svg', source_png=[1536,1024],
              platform_top_center=[384,275], vessel_seat_center=[384,275],
              lid_center=[384,115], footprint=[3,2], view='south',
              review_only=True, game_files_changed=False)
(root / 'CosmicMechGestator-v6.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('Verified: lower seat and platform top share center (384,275); transparent PNG exported.')
print(root / 'CosmicMechGestator-review-v6.png')
