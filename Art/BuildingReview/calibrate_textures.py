"""Install approved art with exact footprint bounds and a shared world scale."""
from pathlib import Path
import json
import re
import shutil
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
ART = PROJECT / 'Art'
TEXTURES = PROJECT / 'Textures'
DEFS = PROJECT / 'Defs' / 'ThingDefs'
REVIEW = Path(__file__).resolve().parent
PIXELS_PER_CELL = 256
PADDING = 16

ASSETS = [
    ('MS_StorageCore', 'StorageBuildings.xml', 'StorageCore/StorageCore-v10', '存储控制核心'),
    ('MS_StorageUnit', 'StorageBuildings.xml', 'StorageUnit/StorageUnit_{direction}-v2', '存储单元'),
    ('MS_StorageReceiver', 'StorageBuildings.xml', 'StorageEndpoints/StorageReceiver-v1', '存储接收器'),
    ('MS_CosmicWorkbench', 'StorageBuildings.xml', 'RemainingBuildings/CosmicWorkbench_{direction}-v1', '宇宙工作台'),
    ('MS_ConstructionSupplyUnit', 'StorageBuildings.xml', 'RemainingBuildings/ConstructionSupplyUnit-v1', '建筑供给单元'),
    ('MS_StorageApparelAdapter', 'StorageBuildings.xml', 'RemainingBuildings/StorageApparelAdapter_{direction}-v2', '衣物适配器'),
    ('MS_StorageSupplyShelf', 'StorageSupplyShelf.xml', 'StorageEndpoints/StorageSupplyShelf_south-v1', '自动补货架'),
    ('MS_TransportSupplier', 'TransportSupplier.xml', 'StorageEndpoints/TransportSupplier_{direction}-v4', '物资补给器'),
    ('MS_FuelSupplier', 'FuelSupplier.xml', 'RemainingBuildings/FuelSupplier-v1', '燃料供给器'),
    ('MS_NivarianCannonExtractor', 'NivarianCannonExtractor.xml', 'RemainingBuildings/NivarianCannonExtractor-v1', '质量驱动器提取口'),
    ('MS_CaravanTeleporter', 'CaravanTeleporter.xml', 'CaravanTeleporter/CaravanTeleporter-v2', '远行队传送器'),
]


def pair(value):
    values = value.strip('()').split(',')
    return tuple(map(float, values)) if len(values) == 2 else (float(values[0]), float(values[0]))


def dimensions(width, height):
    return f'({width:g},{height:g})'


def measured(image, world_size):
    l,t,r,b = image.getchannel('A').getbbox()
    w,h = image.size
    x,z = world_size
    return dict(
        visible_size=[(r-l)*x/w, (b-t)*z/h],
        center_offset_east_north=[((l+r)/2-w/2)*x/w, (h/2-(t+b)/2)*z/h],
        canvas=[w,h], bounds=[l,t,r,b],
    )


def calibrated(source, cells):
    # Always use the original art export; repeated installation never resamples itself.
    with Image.open(source) as original:
        original.load()
        assert original.mode == 'RGBA', source
        bounds = original.getchannel('A').getbbox()
        assert bounds is not None, source
        width,height = [round(value*PIXELS_PER_CELL) for value in cells]
        body = original.crop(bounds).resize((width,height), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', (width+PADDING*2, height+PADDING*2), (0,0,0,0))
    canvas.alpha_composite(body, (PADDING,PADDING))
    assert canvas.getchannel('A').getbbox() == (PADDING,PADDING,width+PADDING,height+PADDING), source
    return canvas


def update_graphic(text, defname, size):
    newline = '\r\n' if '\r\n' in text else '\n'
    count = [0]

    def update_thing(match):
        block = match.group(0)
        if f'<defName>{defname}</defName>' not in block:
            return block
        count[0] += 1

        def update_data(graphic_match):
            graphic = graphic_match.group(0)
            graphic,n = re.subn(r'<drawSize>.*?</drawSize>', f'<drawSize>{dimensions(*size)}</drawSize>', graphic, count=1)
            assert n == 1
            # Symmetric padding centers every view, including the mirrored west view.
            graphic = re.sub(r'\s*<drawOffset(?:North|South|East|West)?>.*?</drawOffset(?:North|South|East|West)?>', '', graphic)
            return graphic.replace('</graphicData>', '  <drawOffset>(0,0,0)</drawOffset>'+newline+'    </graphicData>')

        block,n = re.subn(r'<graphicData>.*?</graphicData>', update_data, block, count=1, flags=re.S)
        assert n == 1
        return block

    result = re.sub(r'<ThingDef\b[^>]*>.*?</ThingDef>', update_thing, text, flags=re.S)
    assert count[0] == 1, defname
    ET.fromstring(result)
    return result


def preview(records):
    # Every tile uses the same pixels-per-world-cell scale; the blue box is the footprint.
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 20)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
    rows = (len(records)+2)//3
    sheet = Image.new('RGBA',(1440,104+rows*340),'#242727')
    draw = ImageDraw.Draw(sheet)
    draw.text((24,20),'魔法存储 · 机身尺寸与中心校准',font=font,fill='#e4eceb')
    draw.text((24,60),'各图使用相同格子比例 · 蓝框为占地边界 · 十字为占地中心',font=small,fill='#aab8b1')
    directions = {'single':'固定朝向','south':'南向','north':'北向','east':'东向'}
    scale = 108
    for i,item in enumerate(records):
        x,y = 16+(i%3)*476,104+(i//3)*340
        draw.text((x+8,y+5),item['label']+' · '+directions[item['direction']],font=font,fill='#e4eceb')
        draw.rectangle((x,y+40,x+459,y+315),fill='#7c8273')
        cx,cy = x+230,y+178
        for gx in range(cx-216,cx+217,scale):
            draw.line((gx,y+40,gx,y+315),fill='#949989')
        for gy in range(cy-108,cy+109,scale):
            draw.line((x,gy,x+459,gy),fill='#949989')
        with Image.open(PROJECT/item['texture']) as sprite:
            w = round(sprite.width*scale/PIXELS_PER_CELL)
            h = round(sprite.height*scale/PIXELS_PER_CELL)
            sheet.alpha_composite(sprite.resize((w,h),Image.Resampling.LANCZOS),(cx-w//2,cy-h//2))
        bw,bh = [round(c*scale) for c in item['footprint']]
        draw.rectangle((cx-bw//2,cy-bh//2,cx+bw//2,cy+bh//2),outline='#55b8e4',width=2)
        draw.line((cx-6,cy,cx+6,cy),fill='#f4a76f',width=1)
        draw.line((cx,cy-6,cx,cy+6),fill='#f4a76f',width=1)
        draw.text((x+8,y+319),dimensions(*item['footprint'])+' 格 · 中心偏移 0',font=small,fill='#aab8b1')
    destination = REVIEW/'Buildings-calibrated-review.png'
    sheet.convert('RGB').save(destination)
    print(destination)


def main():
    files = {}
    copies = []
    records = []
    for defname,xmlfile,source,label in ASSETS:
        path = DEFS/xmlfile
        if path not in files:
            files[path] = path.read_bytes().decode('utf-8')
        thing = next(t for t in ET.fromstring(files[path]).findall('ThingDef') if t.findtext('defName')==defname)
        gx = thing.find('graphicData')
        assert gx.findtext('drawRotated') == 'false'
        footprint = pair(thing.findtext('size'))
        old_size = pair(gx.findtext('drawSize'))
        new_size = tuple(c+PADDING*2/PIXELS_PER_CELL for c in footprint)
        directions = ['south','north','east'] if gx.findtext('graphicClass')=='Graphic_Multi' else ['single']
        for direction in directions:
            oriented_cells = footprint[::-1] if direction=='east' else footprint
            old_world = old_size[::-1] if direction=='east' else old_size
            new_world = new_size[::-1] if direction=='east' else new_size
            src = ART/(source.format(direction=direction)+'.png')
            suffix = '_'+direction if direction!='single' else ''
            dst = TEXTURES/(gx.findtext('texPath')+suffix+'.png')
            with Image.open(dst) as image:
                before = measured(image,old_world)
            canvas = calibrated(src,oriented_cells)
            after = measured(canvas,new_world)
            assert all(abs(a-b)<1e-9 for a,b in zip(after['visible_size'],oriented_cells)), (defname,direction,after)
            assert all(abs(value)<1e-9 for value in after['center_offset_east_north']), (defname,direction,after)
            copies.append((canvas,dst))
            records.append(dict(defname=defname,label=label,direction=direction,texture=dst.relative_to(PROJECT).as_posix(),footprint=oriented_cells,draw_size=new_world,before=before,after=after))
        files[path] = update_graphic(files[path],defname,new_size)

    # Complete validation of all candidate images/XML before installing any changes.
    for canvas,dst in copies:
        canvas.save(dst)
    for path,text in files.items():
        path.write_bytes(text.encode('utf-8'))

    for item in records:
        with Image.open(PROJECT/item['texture']) as image:
            result = measured(image,item['draw_size'])
        assert result == item['after'], item['texture']
        thing = next(t for t in ET.parse(DEFS/next(a[1] for a in ASSETS if a[0]==item['defname'])).getroot().findall('ThingDef') if t.findtext('defName')==item['defname'])
        size = pair(thing.findtext('graphicData/drawSize'))
        if item['direction']=='east':
            size = size[::-1]
        assert list(size)==list(item['draw_size'])
        assert thing.findtext('graphicData/drawOffset')=='(0,0,0)'
        # West uses the mirrored east image; symmetric padding keeps its bounds centered.
        if item['direction']=='east':
            with Image.open(PROJECT/item['texture']) as image:
                mirrored = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                assert measured(mirrored,size)['center_offset_east_north']==[0.0,0.0]
        print(item['defname'],item['direction'],dimensions(*item['footprint']),'center=(0,0)')
    (REVIEW/'texture-calibration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
    preview(records)
    print(f'Validated {len(records)} installed direction textures for {len(ASSETS)} buildings; updated {len(files)} XML files.')


if __name__=='__main__':
    main()
