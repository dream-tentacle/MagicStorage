from pathlib import Path
import json
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parent
ET.parse(root/'CaravanTeleporter-v2.svg')
sprite=Image.open(root/'CaravanTeleporter-v2.png').convert('RGBA')
assert sprite.size==(512,512)
assert sprite.getchannel('A').getextrema()==(0,255)
body=sprite.crop(sprite.getchannel('A').getbbox()).resize((256,256),Image.Resampling.LANCZOS)
game=Image.new('RGBA',(288,288),(0,0,0,0))
game.alpha_composite(body,(16,16))
assert game.getchannel('A').getbbox()==(16,16,272,272)
game.save(root/'CaravanTeleporter-game-v2.png')

sheet=Image.new('RGBA',(1120,720),'#242727')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
draw.text((26,20),'远行队传送器 · 第二稿',font=font,fill='#e4eceb')
draw.text((26,61),'1×1 · 固定朝向 · 深蓝螺旋与天蓝色装饰',font=small,fill='#aab8b1')
for y in range(104,664,20):
    for x in range(24,584,20):
        color='#e3e4df' if ((x-24)//20+(y-104)//20)%2 else '#f1f2ed'
        draw.rectangle((x,y,x+19,y+19),fill=color)
sheet.alpha_composite(sprite,(48,128))
draw.text((628,116),'缩小效果',font=font,fill='#e4eceb')
draw.rectangle((620,162,1095,390),fill='#7c8273')
for size,position in [(216,(636,166)),(108,(878,208)),(54,(1020,235))]:
    sheet.alpha_composite(game.resize((size,size),Image.Resampling.LANCZOS),position)
draw.text((628,431),'同系列 · 相同格子比例',font=font,fill='#e4eceb')
draw.rectangle((620,477,1095,664),fill='#7c8273')
art=root.parent
for file,x in [('Things/Building/StorageReceiver/StorageReceiver.png',628),('Things/Building/TransportSupplier/TransportSupplier_south.png',784)]:
    other=Image.open(art.parent/'Textures'/file).convert('RGBA')
    sheet.alpha_composite(other.resize((144,144),Image.Resampling.LANCZOS),(x,488))
sheet.alpha_composite(game.resize((144,144),Image.Resampling.LANCZOS),(940,488))
draw.text((652,638),'接收器',font=small,fill='#e4eceb')
draw.text((801,638),'物资补给器',font=small,fill='#e4eceb')
draw.text((954,638),'远行队传送器',font=small,fill='#e4eceb')
output=root/'CaravanTeleporter-review-v2.png'
sheet.convert('RGB').save(output)
report=dict(source='CaravanTeleporter-v2.svg',source_png=[512,512],game_png=[288,288],visible_bounds=[16,16,272,272],recommended_draw_size=[1.125,1.125],center_offset=[0,0],footprint=[1,1])
(root/'CaravanTeleporter-v2.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('SVG valid; PNG transparent RGBA; game bounds centered at 1×1 world footprint.')
print(output)
