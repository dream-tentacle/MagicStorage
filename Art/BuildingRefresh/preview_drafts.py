from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent.parent
items=json.loads((HERE/'manifest.json').read_text(encoding='utf-8'))
font_path='C:/Windows/Fonts/msyh.ttc'
font=ImageFont.truetype(font_path,23)
small=ImageFont.truetype(font_path,17)
title=ImageFont.truetype(font_path,28)

def fitted(p,box):
    im=Image.open(p).convert('RGBA')
    im=im.crop(im.getbbox())
    im.thumbnail(box,Image.Resampling.LANCZOS)
    return im

for page in range(2):
    canvas=Image.new('RGB',(1400,1760),'#303639')
    d=ImageDraw.Draw(canvas)
    d.text((24,16),f'建筑贴图优化 · 第 {page+1} 页 / 共 2 页',font=title,fill='#edf1f2')
    d.text((24,60),'左：原稿   右：当前 SVG 设计稿    ·    游戏比例及留缝另见 GameExport 检查图',font=small,fill='#b8cbd6')
    for j,item in enumerate(items[page*5:page*5+5]):
        y=104+j*326
        d.text((25,y),item['title'],font=font,fill='#edf1f2')
        for col,source in enumerate([ROOT/item['source'].replace('.svg','.png'),HERE/item['png']]):
            x=24+col*692
            d.rectangle((x,y+39,x+666,y+307),fill='#858a80')
            for gx in range(x,x+666,64): d.line((gx,y+39,gx,y+307),fill='#91968b')
            for gy in range(y+39,y+307,64): d.line((x,gy,x+666,gy),fill='#91968b')
            im=fitted(source,(520,242))
            canvas.paste(im,(x+(666-im.width)//2,y+52+(242-im.height)//2),im)
            # A small 1-cell-scale sample makes line/detail readability reviewable.
            # A common width is used for before/after; no alpha-bbox recentering in game.
            tiny=fitted(source,(item['footprint'][0]*38,item['footprint'][1]*38+12))
            canvas.paste(tiny,(x+16,y+291-tiny.height),tiny)
    canvas.save(HERE/f'BuildingRefresh-comparison-{page+1}.png')

canvas=Image.new('RGB',(1400,1060),'#303639')
d=ImageDraw.Draw(canvas)
d.text((24,16),'魔法存储 · 其余十种建筑优化样稿',font=title,fill='#edf1f2')
d.text((24,60),'保留 #C5E5F5 装饰色 · 小切角机壳 · SVG 外描边为原粗细的 2/3',font=small,fill='#b8cbd6')
for i,item in enumerate(items):
    x=20+(i%5)*278;y=105+(i//5)*471
    d.rectangle((x,y+41,x+262,y+447),fill='#858a80')
    im=fitted(HERE/item['png'],(238,335))
    canvas.paste(im,(x+(262-im.width)//2,y+70+(335-im.height)//2),im)
    d.text((x+6,y+10),item['title'],font=small,fill='#edf1f2')
canvas.save(HERE/'BuildingRefresh-overview.png')

checks=[]
for item in items:
    import xml.etree.ElementTree as ET
    ET.parse(HERE/item['svg'])
    im=Image.open(HERE/item['png'])
    assert im.mode=='RGBA',item['png']
    box=im.getbbox()
    assert box and box[0]>0 and box[1]>0 and box[2]<im.width and box[3]<im.height,item['png']
    checks.append({'name':item['name'],'size':im.size,'alpha_bounds':box})
(HERE/'validation.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print('Created 2 before/after sheets and overview; 10 SVG/PNG files parse and have unclipped transparency.')
