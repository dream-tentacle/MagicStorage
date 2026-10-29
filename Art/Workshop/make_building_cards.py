"""Render approved building cards and vertically combine each four cards."""
from pathlib import Path
from functools import lru_cache
from io import BytesIO
import base64
import argparse
import html
import json
import re
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
SCALE = 3
SIZE = (1400, 400)
FINAL_SIZE = (2800, 800)
COLORS = {'frame': '#d5eaf5', 'title': '#e3f5ff', 'subtitle': '#a9cadc',
          'body': '#deedf5', 'effect': '#f3ed83'}
IDS = ['MS_StorageCore', 'MS_StorageUnit', 'MS_StorageReceiver',
       'MS_CosmicWorkbench', 'MS_StorageSupplyShelf', 'MS_ConstructionSupplyUnit',
       'MS_StorageApparelAdapter', 'MS_TransportSupplier', 'MS_FuelSupplier',
       'MS_CaravanTeleporter', 'MS_NivarianCannonExtractor', 'MS_CosmicMechGestator']


@lru_cache(maxsize=None)
def font(size):
    return ImageFont.truetype('HarmonyOS_Sans_SC_Bold.ttf', size*SCALE)


def save_png(image, path):
    temporary = path.with_name(path.name+'.tmp')
    image.save(temporary,format='PNG')
    temporary.replace(path)


def save_text(value, path):
    temporary = path.with_name(path.name+'.tmp')
    temporary.write_text(value,encoding='utf-8')
    temporary.replace(path)


def load_cards(language='zh', art='latest'):
    copy = (OUT / '建筑宣传文案.md').read_text(encoding='utf-8')
    parts = re.findall(r'\*\*(\d+)．([^*]+)\*\*\s*\n(.*?)(?=\n\*\*\d+．|\Z)', copy, re.S)
    assert len(parts) == len(IDS)
    definitions = {}
    for filename in ['StorageBuildings', 'StorageSupplyShelf', 'TransportSupplier',
                     'FuelSupplier', 'CaravanTeleporter', 'NivarianCannonExtractor',
                     'CosmicMechGestator']:
        for node in ET.parse(ROOT / f'Defs/ThingDefs/{filename}.xml').getroot():
            if node.findtext('defName'):
                definitions[node.findtext('defName')] = node
    latest_art = {}
    if art == 'latest':
        latest_folder = ROOT/'Art/BuildingRefresh'
        manifest = json.loads((latest_folder/'outline-manifest.json').read_text(encoding='utf-8'))
        for item in manifest:
            key = item['name'].removesuffix('_south').removesuffix('_north')
            assert key not in latest_art, key
            latest_art[key] = item
    cards = []
    for expected, ((number, name, section), def_name) in enumerate(zip(parts, IDS), 1):
        assert int(number) == expected
        body = next(line.strip() for line in section.splitlines() if line.strip())
        effects = re.findall(r'^- (.+)$', section, re.M)
        definition = definitions[def_name]
        graphics = definition.find('graphicData')
        texture = graphics.findtext('texPath')
        suffix = '_south' if graphics.findtext('graphicClass') == 'Graphic_Multi' else ''
        # Full game canvases preserve the relationship between drawSize and art.
        source = ROOT / f'Textures/{texture}{suffix}.png'
        layers = []
        for path in ['building/mechGestatorCylinderGraphic', 'building/mechGestatorTopGraphic']:
            layer = definition.find(path)
            if layer is not None:
                layer_suffix = '_south' if layer.findtext('graphicClass') == 'Graphic_Multi' else ''
                assert layer.findtext('drawSize') == graphics.findtext('drawSize')
                layers.append(ROOT / f'Textures/{layer.findtext("texPath")}{layer_suffix}.png')
        draw_size = tuple(float(value.strip()) for value in
                          graphics.findtext('drawSize').strip('()').split(','))
        color = tuple(float(value.strip()) for value in
                      graphics.findtext('color', '(1,1,1)').strip('()').split(','))
        if art == 'latest':
            item = latest_art[def_name.removeprefix('MS_')]
            source = latest_folder/item['png']
            layers = []  # The reviewed gestator already contains glass and lid.
            color = (1,1,1)  # SVG artwork already has its approved colors.
            with Image.open(source) as reviewed:
                bounds = reviewed.getchannel('A').getbbox()
                assert bounds is not None, source
                # One cell scale for all cards; preserve each sprite's aspect
                # ratio and account for outward-stroke transparent padding.
                units_per_pixel = item['footprint'][0]/(bounds[2]-bounds[0])
                draw_size = tuple(value*units_per_pixel for value in reviewed.size)
        cards.append(dict(number=expected, name=name, body=body, effects=effects,
                          english=definition.findtext('label').upper(), source=source, color=color,
                          layers=layers, draw_size=draw_size, def_name=def_name))
    if language == 'en':
        translations = json.loads((OUT/'building_cards_en.json').read_text(encoding='utf-8'))
        assert [item['defName'] for item in translations] == IDS
        for card,translation in zip(cards,translations):
            assert len(card['effects']) == len(translation['effects']), card['name']
            card.update(name=translation['name'], body=translation['body'], effects=translation['effects'])
    for card in cards:
        card['language'] = language
    reference = next(card for card in cards if card['def_name'] == 'MS_CosmicMechGestator')
    icon = load_game_icon(reference)
    left,top,right,bottom = icon.getchannel('A').getbbox()
    visible_world_width = reference['draw_size'][0]*(right-left)/icon.width
    visible_world_height = reference['draw_size'][1]*(bottom-top)/icon.height
    # Keep the gestator's approved 350 x 330 silhouette bounds as the reference.
    pixels_per_unit = min(350/visible_world_width,330/visible_world_height)
    for card in cards:
        card['pixels_per_unit'] = pixels_per_unit
    return cards


def load_game_icon(card):
    with Image.open(card['source']) as original:
        icon = original.convert('RGBA')
    for source in card['layers']:
        with Image.open(source) as original:
            layer = original.convert('RGBA')
        assert layer.size == icon.size, source
        icon = Image.alpha_composite(icon,layer)
    channels = icon.split()
    rgb = [channel.point([round(value*factor) for value in range(256)])
           for channel,factor in zip(channels[:3],card['color'])]
    return Image.merge('RGBA', (*rgb,channels[3]))


def render_card(card):
    is_english = card['language'] == 'en'
    destination = OUT/'English' if is_english else OUT
    destination.mkdir(exist_ok=True)
    filename = f'{card["def_name"][3:]}_EN.png' if is_english else f'{card["name"]}_介绍_v1.png'
    png_path = destination/filename
    svg_path = png_path.with_suffix('.svg')
    canvas = Image.new('RGBA', tuple(n*SCALE for n in SIZE))
    draw = ImageDraw.Draw(canvas)
    svg = []

    def line(points, color, width):
        draw.line([(round(x*SCALE), round(y*SCALE)) for x,y in points],
                  fill=color, width=round(width*SCALE), joint='curve')
        svg.append(f'<polyline points="{" ".join(f"{x},{y}" for x,y in points)}" '
                   f'fill="none" stroke="{color}" stroke-width="{width}"/>')

    def text(x, baseline, value, size, color):
        assert draw.textlength(value, font=font(size)) <= (1322-x)*SCALE+1, card['name']
        draw.text((x*SCALE, baseline*SCALE), value, font=font(size), fill=color, anchor='ls')
        svg.append(f'<text x="{x}" y="{baseline}" font-size="{size}" fill="{color}">'
                   f'{html.escape(value)}</text>')

    def centered(baseline, value, size, spacing, color):
        face = font(size)
        total = sum(draw.textlength(c, font=face) for c in value)+spacing*SCALE*(len(value)-1)
        x = 925-total/(2*SCALE)
        for char in value:
            text(x, baseline, char, size, color)
            x += draw.textlength(char, font=face)/SCALE+spacing
        return total/SCALE

    def wrap(value, width, size):
        lines, current = [], ''
        tokens = re.findall(r'\S+\s*',value) if is_english else value
        for char in tokens:
            if current and draw.textlength(current+char, font=font(size)) > width*SCALE:
                if not is_english and char in '，。；：、！？”）':
                    lines.append(current[:-1])
                    current = current[-1]+char
                else:
                    lines.append(current)
                    current = char
            else:
                current += char
        if current:
            lines.append(current)
        assert ''.join(lines) == value
        return lines

    for y in range(42,360,17):
        draw.rectangle((48*SCALE,y*SCALE,438*SCALE-1,(y+7)*SCALE-1),
                       fill=(128,128,128,128))
        svg.append(f'<rect x="48" y="{y}" width="390" height="7" '
                   'fill="#808080" fill-opacity="0.5"/>')
    icon = load_game_icon(card)
    bounds = icon.getchannel('A').getbbox()
    assert bounds is not None
    # Crop only after deriving the silhouette's size in game units. Each card
    # shares one world-unit scale, independent of PNG resolution and padding.
    world_width = card['draw_size'][0]*(bounds[2]-bounds[0])/icon.width
    world_height = card['draw_size'][1]*(bounds[3]-bounds[1])/icon.height
    icon_width = world_width*card['pixels_per_unit']
    icon_height = world_height*card['pixels_per_unit']
    assert icon_width <= 390 and icon_height <= 350, card['name']
    icon = icon.crop(bounds)
    buffer = BytesIO()
    icon.save(buffer,format='PNG')
    encoded = base64.b64encode(buffer.getvalue()).decode('ascii')
    icon = icon.resize((round(icon_width*SCALE),round(icon_height*SCALE)),Image.Resampling.LANCZOS)
    ix,iy = round(243*SCALE-icon.width/2),round(200*SCALE-icon.height/2)
    canvas.alpha_composite(icon,(ix,iy))
    svg.append(f'<image x="{ix/SCALE}" y="{iy/SCALE}" width="{icon.width/SCALE}" '
               f'height="{icon.height/SCALE}" preserveAspectRatio="none" '
               f'href="data:image/png;base64,{encoded}"/>')

    title = card['name'].upper() if is_english else card['name']
    title_size,title_spacing = (32,1.5) if is_english else (38,5)
    while sum(draw.textlength(c,font=font(title_size)) for c in title)/SCALE + \
            title_spacing*(len(title)-1) > 730:
        title_size -= 1
    title_width = centered(48,title,title_size,title_spacing,COLORS['title'])
    line([(490,60),(490,30),(925-title_width/2-40,30)],COLORS['frame'],5)
    line([(925+title_width/2+40,30),(1352,30),(1352,60)],COLORS['frame'],5)
    line([(490,338),(490,370),(1352,370),(1352,338)],COLORS['frame'],5)
    line([(490,75),(490,325)],'#7d929f',1)
    line([(1352,75),(1352,325)],'#7d929f',1)
    centered(84,'MAGIC STORAGE' if is_english else card['english'],13,3,COLORS['subtitle'])
    copy_size = 23 if is_english else 26
    effect_gap = 10 if is_english else (6 if card['def_name'] == 'MS_StorageCore' else 12)
    while True:
        body_height = copy_size+6 if is_english else 38
        effect_height = copy_size+5 if is_english else 32
        body_lines = wrap(card['body'],800,copy_size)
        effect_lines = [wrap(value,774,copy_size) for value in card['effects']]
        span = (sum(map(len,effect_lines))-1)*effect_height+(len(effect_lines)-1)*effect_gap
        baseline = min(244,334-span)
        if len(body_lines) <= (3 if is_english else 2) and baseline >= 128+(len(body_lines)-1)*body_height+34:
            break
        assert is_english and copy_size > 20, card['name']
        copy_size -= 1
    for index,value in enumerate(body_lines):
        text(520,128+index*body_height,value,copy_size,COLORS['body'])
    for lines in effect_lines:
        cx,cy,r = 526*SCALE,(baseline-9)*SCALE,4*SCALE
        draw.ellipse((cx-r,cy-r,cx+r,cy+r),fill=COLORS['effect'])
        svg.append(f'<circle cx="526" cy="{baseline-9}" r="4" fill="{COLORS["effect"]}"/>')
        for value in lines:
            assert baseline <= 334
            text(548,baseline,value,copy_size,COLORS['effect'])
            baseline += effect_height
        baseline += effect_gap

    save_png(canvas.resize(FINAL_SIZE,Image.Resampling.LANCZOS),png_path)
    save_text('<svg xmlns="http://www.w3.org/2000/svg" width="2800" height="800" '
                        'viewBox="0 0 1400 400"><g font-family="HarmonyOS Sans SC,HarmonyOS Sans,sans-serif" '
                        'font-weight="700">'+''.join(svg)+'</g></svg>',svg_path)
    return png_path,svg_path


def main(language='zh', art='latest'):
    cards = load_cards(language, art)
    is_english = language == 'en'
    paths = []
    for card in cards:
        png,svg = render_card(card)
        with Image.open(png) as saved:
            assert saved.size == FINAL_SIZE and saved.mode == 'RGBA'
            assert saved.getpixel((0,0))[3] == 0
        document = ET.parse(svg).getroot()
        svg_text = ''.join(node.text or '' for node in
                           document.iter('{http://www.w3.org/2000/svg}text'))
        title = card['name'].upper() if is_english else card['name']
        subtitle = 'MAGIC STORAGE' if is_english else card['english']
        assert title+subtitle+card['body']+''.join(card['effects']) == svg_text
        paths.append((png,svg))
        print(f'Card {card["number"]:02d}: {png.name}')
    group_dir = OUT/'English/Groups' if is_english else OUT/'分组'
    group_dir.mkdir(exist_ok=True)
    ordinary = [card for card in cards if card['def_name'] != 'MS_NivarianCannonExtractor']
    compatibility = [card for card in cards if card['def_name'] == 'MS_NivarianCannonExtractor']
    groups = [(f'建筑介绍_{offset//4+1:02d}',ordinary[offset:offset+4])
              for offset in range(0,len(ordinary),4)]
    groups.append(('建筑介绍_涅瓦莲联动',compatibility))
    if is_english:
        groups = [(f'Buildings_{index+1:02d}_EN' if index < len(groups)-1 else
                   'Buildings_Nivarian_Compatibility_EN',group) for index,(_,group) in enumerate(groups)]
    scale_note = ('建筑图按占地宽度统一比例，以培育器的展示大小为基准；保留各图自身宽高比。'
                  if art == 'latest' else
                  '建筑图按游戏 drawSize 统一比例，以培育器的展示大小为基准；保留贴图画布留白对实际可见尺寸的影响。')
    index_lines = ['# 建筑介绍图', '', '普通建筑按文案顺序每四张纵向合成一组；涅瓦莲联动单独成图。', '',
                   scale_note, '']
    if is_english:
        index_lines = ['# Magic Storage: Building Cards', '',
                       '11 standard buildings in groups of 4 + 4 + 3. Nivarian integration has its own card.', '',
                       'Building art uses one game-unit scale, based on the cosmic mech gestator.', '']
    if art == 'latest':
        index_lines += (['Art source: latest reviewed PNGs in `Art/BuildingRefresh`; includes outward-only outlines and the simplified construction supply hatch. Copy and layout are unchanged.', '']
                        if is_english else
                        ['贴图来源：`Art/BuildingRefresh` 中的最新审核稿，包含向外描边和精简后的建筑供给舱口。保留原有文案与排版，按占地宽度统一展示比例。', ''])
    for name,group in groups:
        height = 800*len(group)
        canvas = Image.new('RGBA',(2800,height))
        svg_groups = []
        for row,card in enumerate(group):
            png,svg = paths[card['number']-1]
            with Image.open(png) as image:
                canvas.alpha_composite(image,(0,800*row))
            source = ET.parse(svg).getroot()
            wrapper = ET.Element('{http://www.w3.org/2000/svg}g',{'transform':f'translate(0,{400*row})'})
            wrapper.extend(list(source))
            svg_groups.append(wrapper)
        destination = group_dir / f'{name}.png'
        save_png(canvas,destination)
        document = ET.Element('{http://www.w3.org/2000/svg}svg',{
            'width':'2800','height':str(height),'viewBox':f'0 0 1400 {400*len(group)}'})
        document.extend(svg_groups)
        ET.register_namespace('','http://www.w3.org/2000/svg')
        save_text(ET.tostring(document,encoding='unicode',xml_declaration=True),destination.with_suffix('.svg'))
        with Image.open(destination) as saved:
            assert saved.size == (2800,height) and saved.mode == 'RGBA'
            assert saved.getpixel((0,0))[3] == 0
        folder = 'Groups' if is_english else '分组'
        index_lines.append(f'- [{name}]({folder}/{name}.png): '+ ', '.join(card['name'] for card in group))
        print(f'{name}: {len(group)} cards, 2800 x {height}')
    index_path = OUT/'English/README.md' if is_english else OUT/'介绍图索引.md'
    save_text('\n'.join(index_lines)+'\n',index_path)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--lang',choices=['zh','en'],default='zh')
    parser.add_argument('--art',choices=['latest','game'],default='latest')
    args = parser.parse_args()
    main(args.lang,args.art)
