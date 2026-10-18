"""Render approved building cards and vertically combine each four cards."""
from pathlib import Path
from functools import lru_cache
from io import BytesIO
import base64
import html
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
       'MS_CaravanTeleporter', 'MS_NivarianCannonExtractor']


@lru_cache(maxsize=None)
def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/msyhbd.ttc', size*SCALE)


def load_cards():
    copy = (OUT / '建筑宣传文案.md').read_text(encoding='utf-8')
    parts = re.findall(r'\*\*(\d+)．([^*]+)\*\*\s*\n(.*?)(?=\n\*\*\d+．|\Z)', copy, re.S)
    assert len(parts) == len(IDS) == 11
    definitions = {}
    for filename in ['StorageBuildings', 'StorageSupplyShelf', 'TransportSupplier',
                     'FuelSupplier', 'CaravanTeleporter', 'NivarianCannonExtractor']:
        for node in ET.parse(ROOT / f'Defs/ThingDefs/{filename}.xml').getroot():
            if node.findtext('defName'):
                definitions[node.findtext('defName')] = node
    cards = []
    for expected, ((number, name, section), def_name) in enumerate(zip(parts, IDS), 1):
        assert int(number) == expected
        body = next(line.strip() for line in section.splitlines() if line.strip())
        effects = re.findall(r'^- (.+)$', section, re.M)
        definition = definitions[def_name]
        graphics = definition.find('graphicData')
        texture = graphics.findtext('texPath')
        suffix = '_south' if graphics.findtext('graphicClass') == 'Graphic_Multi' else ''
        source = ROOT / f'Textures/{texture}{suffix}.png'
        color = tuple(float(value.strip()) for value in
                      graphics.findtext('color', '(1,1,1)').strip('()').split(','))
        cards.append(dict(number=expected, name=name, body=body, effects=effects,
                          english=definition.findtext('label').upper(), source=source, color=color))
    return cards


def render_card(card):
    png_path = OUT / f'{card["name"]}_介绍_v1.png'
    svg_path = png_path.with_suffix('.svg')
    # Preserve the approved core preview exactly.
    if card['number'] == 1 and png_path.exists() and svg_path.exists():
        return png_path, svg_path
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
        for char in value:
            if current and draw.textlength(current+char, font=font(size)) > width*SCALE:
                if char in '，。；：、！？”）':
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
    with Image.open(card['source']) as original:
        icon = original.convert('RGBA')
    channels = icon.split()
    rgb = [channel.point([round(value*factor) for value in range(256)])
           for channel,factor in zip(channels[:3],card['color'])]
    icon = Image.merge('RGBA', (*rgb,channels[3]))
    bounds = icon.getchannel('A').getbbox()
    assert bounds is not None
    icon = icon.crop(bounds)
    buffer = BytesIO()
    icon.save(buffer,format='PNG')
    encoded = base64.b64encode(buffer.getvalue()).decode('ascii')
    factor = min(256*SCALE/icon.width,270*SCALE/icon.height)
    icon = icon.resize((round(icon.width*factor),round(icon.height*factor)),Image.Resampling.LANCZOS)
    ix,iy = round(243*SCALE-icon.width/2),round(200*SCALE-icon.height/2)
    canvas.alpha_composite(icon,(ix,iy))
    svg.append(f'<image x="{ix/SCALE}" y="{iy/SCALE}" width="{icon.width/SCALE}" '
               f'height="{icon.height/SCALE}" href="data:image/png;base64,{encoded}"/>')

    title_size,title_spacing = 38,5
    while sum(draw.textlength(c,font=font(title_size)) for c in card['name'])/SCALE + \
            title_spacing*(len(card['name'])-1) > 730:
        title_size -= 1
    title_width = centered(48,card['name'],title_size,title_spacing,COLORS['title'])
    line([(490,60),(490,30),(925-title_width/2-40,30)],COLORS['frame'],5)
    line([(925+title_width/2+40,30),(1352,30),(1352,60)],COLORS['frame'],5)
    line([(490,338),(490,370),(1352,370),(1352,338)],COLORS['frame'],5)
    line([(490,75),(490,325)],'#7d929f',1)
    line([(1352,75),(1352,325)],'#7d929f',1)
    centered(84,card['english'],13,3,COLORS['subtitle'])
    body_lines = wrap(card['body'],800,26)
    assert len(body_lines) <= 2, card['name']
    for index,value in enumerate(body_lines):
        text(520,128+index*38,value,26,COLORS['body'])
    effect_lines = [wrap(value,774,26) for value in card['effects']]
    span = (sum(map(len,effect_lines))-1)*32+(len(effect_lines)-1)*12
    baseline = min(244,334-span)
    assert baseline >= 128+(len(body_lines)-1)*38+38, card['name']
    for lines in effect_lines:
        cx,cy,r = 526*SCALE,(baseline-9)*SCALE,4*SCALE
        draw.ellipse((cx-r,cy-r,cx+r,cy+r),fill=COLORS['effect'])
        svg.append(f'<circle cx="526" cy="{baseline-9}" r="4" fill="{COLORS["effect"]}"/>')
        for value in lines:
            assert baseline <= 334
            text(548,baseline,value,26,COLORS['effect'])
            baseline += 32
        baseline += 12

    canvas.resize(FINAL_SIZE,Image.Resampling.LANCZOS).save(png_path)
    svg_path.write_text('<svg xmlns="http://www.w3.org/2000/svg" width="2800" height="800" '
                        'viewBox="0 0 1400 400"><g font-family="Microsoft YaHei,sans-serif" '
                        'font-weight="700">'+''.join(svg)+'</g></svg>',encoding='utf-8')
    return png_path,svg_path


def main():
    cards = load_cards()
    paths = []
    for card in cards:
        png,svg = render_card(card)
        with Image.open(png) as saved:
            assert saved.size == FINAL_SIZE and saved.mode == 'RGBA'
            assert saved.getpixel((0,0))[3] == 0
        document = ET.parse(svg).getroot()
        svg_text = ''.join(node.text or '' for node in
                           document.iter('{http://www.w3.org/2000/svg}text'))
        assert card['name']+card['english']+card['body']+''.join(card['effects']) == svg_text
        paths.append((png,svg))
        print(f'Card {card["number"]:02d}: {png.name}')
    group_dir = OUT / '分组'
    group_dir.mkdir(exist_ok=True)
    index_lines = ['# 建筑介绍图', '', '按已保存的文案顺序，每四张纵向合成一组。', '']
    for offset in range(0,len(cards),4):
        group = cards[offset:offset+4]
        height = 800*len(group)
        canvas = Image.new('RGBA',(2800,height))
        svg_groups = []
        for row,(png,svg) in enumerate(paths[offset:offset+4]):
            with Image.open(png) as image:
                canvas.alpha_composite(image,(0,800*row))
            source = ET.parse(svg).getroot()
            wrapper = ET.Element('{http://www.w3.org/2000/svg}g',{'transform':f'translate(0,{400*row})'})
            wrapper.extend(list(source))
            svg_groups.append(wrapper)
        name = f'建筑介绍_{offset//4+1:02d}'
        destination = group_dir / f'{name}.png'
        canvas.save(destination)
        document = ET.Element('{http://www.w3.org/2000/svg}svg',{
            'width':'2800','height':str(height),'viewBox':f'0 0 1400 {400*len(group)}'})
        document.extend(svg_groups)
        ET.register_namespace('','http://www.w3.org/2000/svg')
        ET.ElementTree(document).write(destination.with_suffix('.svg'),encoding='utf-8',xml_declaration=True)
        with Image.open(destination) as saved:
            assert saved.size == (2800,height) and saved.mode == 'RGBA'
            assert saved.getpixel((0,0))[3] == 0
        index_lines.append(f'- [{name}](分组/{name}.png)：'+ '、'.join(card['name'] for card in group))
        print(f'Group {offset//4+1:02d}: {len(group)} cards, 2800 x {height}')
    (OUT/'介绍图索引.md').write_text('\n'.join(index_lines)+'\n',encoding='utf-8')


if __name__ == '__main__':
    main()
