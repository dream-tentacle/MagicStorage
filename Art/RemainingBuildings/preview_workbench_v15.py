from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parents[1]
paths=[PROJECT/'Defs/ThingDefs/StorageBuildings.xml',*list((PROJECT/'Textures/Things/Building/CosmicWorkbench').glob('*.png'))]
before={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
source=ROOT/'CosmicWorkbench_north-v15.svg'
svg=ET.parse(source).getroot()
assert svg.get('viewBox')=='0 0 768 352'
assert not any(e.tag.endswith('Gradient') for e in svg.iter())
black=[e for e in svg.iter() if e.get('stroke')=='#000000']
assert len(black)==1 and all(e.get('stroke-width')=='8' for e in black)
sprite=Image.open(source.with_suffix('.png')).convert('RGBA')
assert sprite.size==(1536,704)
bounds=sprite.getchannel('A').getbbox()
assert bounds[0]>0 and bounds[1]>0 and bounds[2]<sprite.width and bounds[3]<sprite.height
assert sprite.getpixel((384*2,250*2))[3]==0
assert sprite.getpixel((384*2,280*2))[3]==0
assert sprite.getpixel((150*2,265*2))[3]==0

sheet=Image.new('RGBA',(1120,1040),'#272d31')
draw=ImageDraw.Draw(sheet)
title=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',28)
label=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',21)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
draw.text((28,18),'宇宙工作台 · 统一缺口连接点',font=title,fill='#e5edf1')
draw.text((28,64),'取消补片；斜面、横向面和倒角使用同一组连接点',font=small,fill='#b3c1c8')
for y in range(112,596,16):
    for x in range(24,1096,16):
        color='#e3e5e2' if ((x-24)//16+(y-112)//16)%2 else '#f0f2ed'
        draw.rectangle((x,y,min(x+15,1095),min(y+15,595)),fill=color)
hero=sprite.resize((1056,484),Image.Resampling.LANCZOS)
sheet.alpha_composite(hero,(32,112))
draw.text((28,624),'缺口局部对比',font=label,fill='#e5edf1')
for x,image,name in [(24,Image.open(ROOT/'CosmicWorkbench_north-v14.png').convert('RGBA'),'修改前'),(572,sprite,'修改后')]:
    draw.rectangle((x,670,x+523,1015),fill='#d7ddda')
    draw.text((x+18,680),name,font=label,fill='#344a5b')
    detail=image.crop((608,400,696,508)).resize((248,304),Image.Resampling.LANCZOS)
    sheet.alpha_composite(detail,(x+138,710))
output=ROOT/'CosmicWorkbench-review-v15.png'
sheet.convert('RGB').save(output)
assert before=={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
report={'source':source.name,'view':'north','footprint':[3,1],
        'operator_notch':{'center_x':384,'mouth_width':136,'inner_width':92,'depth':22},
        'silhouette':'chamfered worktop with concealed supports and a shallow central recess',
        'exterior_stroke':{'color':'#000000','width':8},'accent':'#C5E5F5',
        'controls':'mechanical vise and a flat inset display without an upright stand','worktop_base_color':'#D8DFE3','left_junction':[336.3431458,210.75],'shared_vertices':True,'joint_patches':0,'review_only':True,'game_files_changed':False}
(ROOT/'CosmicWorkbench-v15.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Verified transparent PNG, matching black exterior strokes, shallow notch and open knee space. Game files unchanged.')
print(output)
