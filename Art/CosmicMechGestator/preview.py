from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parent
project=root.parents[1]
xml=project/'Defs/ThingDefs/CosmicMechGestator.xml'
xml_before=hashlib.sha256(xml.read_bytes()).hexdigest()
svg=ET.parse(root/'CosmicMechGestator_south-v2.svg').getroot()
assert svg.get('viewBox')=='0 0 512 768'
sprite=Image.open(root/'CosmicMechGestator_south-v2.png').convert('RGBA')
assert sprite.size==(1024,1536)
assert sprite.getchannel('A').getextrema()==(0,255)
body=sprite.crop(sprite.getchannel('A').getbbox()).resize((512,768),Image.Resampling.LANCZOS)
game=Image.new('RGBA',(544,800),(0,0,0,0))
game.alpha_composite(body,(16,16))
assert game.getchannel('A').getbbox()==(16,16,528,784)
assert 512/544*2.125==2
assert 768/800*3.125==3
game.save(root/'CosmicMechGestator_south-game-v2.png')

sheet=Image.new('RGBA',(1180,960),'#242727')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
draw.text((26,20),'宇宙机械体培育器 · 2×3 · 南向第二稿',font=font,fill='#e4eceb')
draw.text((26,63),'立体玻璃圆筒 / 厚顶盖 / 侧面供料管 / 天蓝色 #C5E5F5',font=small,fill='#aab8b1')
for y in range(108,916,20):
    for x in range(24,584,20):
        color='#e3e4df' if ((x-24)//20+(y-108)//20)%2 else '#f1f2ed'
        draw.rectangle((x,y,min(x+19,583),min(y+19,915)),fill=color)
sheet.alpha_composite(sprite.resize((512,768),Image.Resampling.LANCZOS),(48,128))

draw.text((630,115),'占地校准 · 蓝框 2×3 格',font=font,fill='#e4eceb')
draw.rectangle((616,166,1155,590),fill='#7c8273')
for x in [758,886,1014]:
    draw.line((x,166,x,590),fill='#a1a693')
for y in [186,314,442,570]:
    draw.line((616,y,1155,y),fill='#a1a693')
sheet.alpha_composite(game.resize((272,400),Image.Resampling.LANCZOS),(750,178))
draw.rectangle((758,186,1014,570),outline='#55b8e4',width=2)

draw.text((630,633),'缩小效果',font=font,fill='#e4eceb')
draw.rectangle((616,681,1155,915),fill='#7c8273')
for size,position in [((136,200),(659,696)),((68,100),(851,746)),((34,50),(992,771))]:
    sheet.alpha_composite(game.resize(size,Image.Resampling.LANCZOS),position)
output=root/'CosmicMechGestator-review-v2.png'
sheet.convert('RGB').save(output)
report=dict(source='CosmicMechGestator_south-v2.svg',source_png=[1024,1536],game_png=[544,800],visible_bounds=[16,16,528,784],recommended_draw_size=[2.125,3.125],center_offset=[0,0],footprint=[2,3],view='south',formation_area='empty',review_only=True,layer_groups=['gestator-base','cylinder-glass','gestator-top'])
(root/'CosmicMechGestator-v2.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
assert hashlib.sha256(xml.read_bytes()).hexdigest()==xml_before
print('SVG valid; RGBA PNG verified; visible body exactly 2×3 cells and centered. Existing ThingDef XML unchanged.')
print(output)
