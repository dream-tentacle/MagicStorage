from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parent
project=root.parents[1]
xml=project/'Defs/ThingDefs/CosmicMechGestator.xml'
xml_before=hashlib.sha256(xml.read_bytes()).hexdigest()
svg=ET.parse(root/'CosmicMechGestator_south-v5.svg').getroot()
assert svg.get('viewBox')=='0 0 768 512'
sprite=Image.open(root/'CosmicMechGestator_south-v5.png').convert('RGBA')
assert sprite.size==(1536,1024)
assert sprite.getchannel('A').getextrema()==(0,255)
body=sprite.crop(sprite.getchannel('A').getbbox()).resize((768,512),Image.Resampling.LANCZOS)
game=Image.new('RGBA',(800,544),(0,0,0,0))
game.alpha_composite(body,(16,16))
assert game.getchannel('A').getbbox()==(16,16,784,528)
assert 768/800*3.125==3
assert 512/544*2.125==2
game.save(root/'CosmicMechGestator_south-game-v5.png')

sheet=Image.new('RGBA',(1160,1000),'#242727')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
draw.text((26,20),'宇宙机械体培育器 · 南向第五稿',font=font,fill='#e4eceb')
draw.text((26,62),'横向 3 格 × 纵深 2 格 · 统一俯视角度与整体结构',font=small,fill='#aab8b1')
for y in range(108,752,20):
    for x in range(24,1136,20):
        color='#e3e4df' if ((x-24)//20+(y-108)//20)%2 else '#f1f2ed'
        draw.rectangle((x,y,min(x+19,1135),min(y+19,751)),fill=color)
sheet.alpha_composite(sprite.resize((960,640),Image.Resampling.LANCZOS),(100,110))

draw.text((32,782),'格子比例示意 · 南向横 3、竖 2',font=small,fill='#e4eceb')
draw.rectangle((24,820,1135,975),fill='#7c8273')
# At 64 pixels per cell, the opaque body occupies exactly 192×128 pixels.
sheet.alpha_composite(game.resize((200,136),Image.Resampling.LANCZOS),(160,829))
for x in [164,228,292,356]:
    draw.line((x,833,x,961),fill='#55b8e4',width=1)
for y in [833,897,961]:
    draw.line((164,y,356,y),fill='#55b8e4',width=1)
draw.text((450,843),'较小显示',font=small,fill='#dce6e0')
sheet.alpha_composite(game.resize((150,102),Image.Resampling.LANCZOS),(608,846))
sheet.alpha_composite(game.resize((100,68),Image.Resampling.LANCZOS),(898,864))
output=root/'CosmicMechGestator-review-v5.png'
sheet.convert('RGB').save(output)
report=dict(source='CosmicMechGestator_south-v5.svg',source_png=[1536,1024],game_png=[800,544],visible_bounds=[16,16,784,528],recommended_draw_size=[3.125,2.125],center_offset=[0,0],footprint=[3,2],view='south',formation_area='empty',review_only=True,layer_groups=['gestator-base','cylinder-glass','gestator-top'])
(root/'CosmicMechGestator-v5.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
assert hashlib.sha256(xml.read_bytes()).hexdigest()==xml_before
print('SVG valid; transparent PNG verified; south view horizontal 3 × depth 2 cells, centered. Existing game XML unchanged.')
print(output)
