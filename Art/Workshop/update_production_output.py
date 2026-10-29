"""Update the production output cards using the recovered workshop renderer.

Run from any directory: python Art/Workshop/update_production_output.py
The renderer, font, transparent canvas, spacing and game-unit scale are retained.
Workshop SVGs and PNGs stay together in Art; upload PNGs are also copied to
Exports/Workshop/1.2.0.
"""
from io import BytesIO
from pathlib import Path
import base64
import json
import math
import re
import tempfile
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw
import make_building_cards as original


OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
EXPORT = ROOT / 'Exports/Workshop/1.2.0'
NS = '{http://www.w3.org/2000/svg}'
PORT = 'MS_ProductionOutputPort'
GROUP = ['MS_FuelSupplier', 'MS_CaravanTeleporter', 'MS_CosmicMechGestator']
ET.register_namespace('', NS[1:-1])


def port_icon():
    """Pillow raster of the approved flat output tray, at four times SVG size."""
    scale = 4
    image = Image.new('RGBA', (288 * scale, 288 * scale))
    draw = ImageDraw.Draw(image)

    def ellipse(box, fill):
        draw.ellipse(tuple(round(v * scale) for v in box), fill=fill)

    def rect(box, fill):
        draw.rectangle(tuple(round(v * scale) for v in box), fill=fill)

    def polygon(points, fill):
        draw.polygon([(round(x * scale), round(y * scale)) for x, y in points], fill=fill)

    for cy in (136, 142):
        ellipse((31.8614, cy - 112.1386, 256.1386, cy + 112.1386), '#000000')
    rect((31.8614, 136, 256.1386, 142), '#000000')
    ellipse((42, 40, 246, 244), '#637782')
    ellipse((42, 34, 246, 238), '#d8dfe3')
    for outer, inner, start, end, fill in [
        (102, 96, 180, 360, '#e6e9e8'),
        (102, 96, 0, 134, '#acb8bf'),
    ]:
        angles = [math.radians(start + (end - start) * i / 120) for i in range(121)]
        points = [(144 + outer * math.cos(a), 136 + outer * math.sin(a)) for a in angles]
        points += [(144 + inner * math.cos(a), 136 + inner * math.sin(a)) for a in reversed(angles)]
        polygon(points, fill)
    polygon([(114, 68), (174, 68), (189, 83), (189, 189),
             (174, 204), (114, 204), (99, 189), (99, 83)], '#637782')
    polygon([(117, 74), (171, 74), (183, 86), (183, 186),
             (171, 198), (117, 198), (105, 186), (105, 86)], '#304553')
    for y in (91, 134, 177):
        rect((105, y, 183, y + 4), '#637782')
    rect((103, 87, 110, 185), '#93a3ad')
    rect((178, 87, 185, 185), '#acb8bf')
    polygon([(144, 77), (154, 87), (134, 87)], '#c5e5f5')
    shapes = [
        ([(5, 0), (49, 0), (54, 5), (54, 28), (0, 28), (0, 5)], '#637782'),
        ([(5, 0), (49, 0), (54, 5), (54, 24), (0, 24), (0, 5)], '#acb8bf'),
        ([(7, 3), (47, 3), (51, 7), (51, 20), (3, 20), (3, 7)], '#d8dfe3'),
        ([(7, 3), (47, 3), (44, 6), (8, 6), (6, 8), (6, 18), (3, 18), (3, 7)], '#e6e9e8'),
    ]
    for y in (99, 145):
        for points, fill in shapes:
            polygon([(117 + x, y + dy) for x, dy in points], fill)
    return image


def descriptions():
    copy = (OUT / '建筑宣传文案.md').read_text(encoding='utf-8')
    parts = re.findall(r'\*\*(\d+)．([^*]+)\*\*\s*\n(.*?)(?=\n\*\*\d+．|\Z)', copy, re.S)
    ids = original.IDS + [PORT]
    assert len(parts) == len(ids)
    chinese = {}
    for def_name, (number, name, section) in zip(ids, parts):
        chinese[def_name] = dict(number=int(number), name=name,
            body=next(line.strip() for line in section.splitlines() if line.strip()),
            effects=re.findall(r'^- (.+)$', section, re.M))
    english = {entry['defName']: entry for entry in
               json.loads((OUT / 'building_cards_en.json').read_text(encoding='utf-8'))}
    return chinese, english


def old_icon(def_name, chinese):
    """Reuse the reviewed embedded sprite and its exact established dimensions."""
    source = OUT / (chinese[def_name]['name'] + '_介绍_v1.svg')
    root = ET.parse(source).getroot()
    item = root.find('.//' + NS + 'image')
    encoded = item.get('href').split(',', 1)[1]
    image = Image.open(BytesIO(base64.b64decode(encoded))).convert('RGBA')
    return image, (float(item.get('width')), float(item.get('height'))), root


def make_card(def_name, language, chinese, english):
    card = dict(chinese[def_name])
    if def_name == PORT:
        image = port_icon()
        _, reference, _ = old_icon('MS_StorageReceiver', chinese)
        bounds = image.getchannel('A').getbbox()
        icon_size = (reference[0], reference[0] * (bounds[3] - bounds[1]) / (bounds[2] - bounds[0]))
        subtitle = 'PRODUCTION OUTPUT PORT'
    else:
        image, icon_size, svg = old_icon(def_name, chinese)
        subtitle = ''.join(n.text or '' for n in svg.iter(NS + 'text') if n.get('y') == '84')
    bounds = image.getchannel('A').getbbox()
    stream = BytesIO()
    image.save(stream, format='PNG')
    stream.seek(0)
    card.update(def_name=def_name, language=language, english=subtitle,
                source=stream, layers=[], color=(1, 1, 1), pixels_per_unit=1,
                draw_size=(icon_size[0] * image.width / (bounds[2] - bounds[0]),
                           icon_size[1] * image.height / (bounds[3] - bounds[1])))
    if language == 'en':
        translated = english[def_name]
        card.update(name=translated['name'], body=translated['body'], effects=translated['effects'])
    return card


def main():
    chinese, english = descriptions()
    EXPORT.mkdir(parents=True, exist_ok=True)
    # Only the new card is rebuilt. Existing cards are rendered from their final
    # SVG coordinates, preserving any last-minute approved text changes.
    with tempfile.TemporaryDirectory(prefix='.render-', dir=EXPORT) as temporary:
        assert Path(temporary).resolve().is_relative_to(EXPORT.resolve())
        original.OUT = Path(temporary)
        for language in ('zh', 'en'):
            card = make_card(PORT, language, chinese, english)
            png, svg = original.render_card(card)
            destination = OUT / 'English' if language == 'en' else OUT
            (destination / svg.name).write_bytes(svg.read_bytes())
            (destination / png.name).write_bytes(png.read_bytes())
            png_name = 'ProductionOutputPort_EN.png' if language == 'en' else '生产输出口_介绍.png'
            (EXPORT / png_name).write_bytes(png.read_bytes())

            svg_group_path = (OUT / 'English/Groups/Buildings_03_EN.svg' if language == 'en'
                              else OUT / '分组/建筑介绍_03.svg')
            root = ET.parse(svg_group_path).getroot()
            existing = []
            for group in root.findall(NS + 'g'):
                texts = ''.join(n.text or '' for n in group.iter(NS + 'text'))
                if 'PRODUCTION OUTPUT PORT' not in texts:
                    existing.append(group)
            assert len(existing) == 3
            groups = []
            wrapper = ET.Element(NS + 'g', {'transform': 'translate(0,0)'})
            wrapper.extend(list(ET.parse(svg).getroot()))
            groups.append(wrapper)
            for row, group in enumerate(existing, 1):
                group.set('transform', f'translate(0,{400 * row})')
                groups.append(group)
            root[:] = groups
            root.set('height', '3200')
            root.set('viewBox', '0 0 1400 1600')
            ET.ElementTree(root).write(svg_group_path, encoding='utf-8', xml_declaration=True)

            # The old SVG embeds sprites at their original size. Rasterize the
            # existing three cards with the same font and 3x -> 2x antialiasing.
            strips = [Image.open(png).convert('RGBA')]
            for group in existing:
                strips.append(render_saved_card(group))
            combined = Image.new('RGBA', (2800, 3200))
            for row, strip in enumerate(strips):
                combined.alpha_composite(strip, (0, row * 800))
            name = 'Buildings_03_EN.png' if language == 'en' else '建筑介绍_03.png'
            combined.save(EXPORT / name)
            combined.save(svg_group_path.with_suffix('.png'))
            assert combined.getpixel((0, 0))[3] == 0
            print('Updated:', svg_group_path.relative_to(ROOT), '->', name)
    print('Original HarmonyOS font, transparency, letter spacing and sprite scale retained.')


def render_saved_card(wrapper):
    """Render the limited vocabulary of the original, retained workshop SVGs."""
    scale = original.SCALE
    canvas = Image.new('RGBA', (1400 * scale, 400 * scale))
    draw = ImageDraw.Draw(canvas)
    for node in wrapper.iter():
        name = node.tag.removeprefix(NS)
        if name == 'rect':
            x, y, w, h = (float(node.get(k, '0')) * scale for k in ('x', 'y', 'width', 'height'))
            draw.rectangle((x, y, x + w - 1, y + h - 1), fill=(128, 128, 128, 128))
        elif name == 'image':
            image = Image.open(BytesIO(base64.b64decode(node.get('href').split(',', 1)[1]))).convert('RGBA')
            image = image.resize(tuple(round(float(node.get(k)) * scale) for k in ('width', 'height')),
                                 Image.Resampling.LANCZOS)
            canvas.alpha_composite(image, tuple(round(float(node.get(k)) * scale) for k in ('x', 'y')))
        elif name == 'text':
            draw.text(tuple(float(node.get(k)) * scale for k in ('x', 'y')), node.text or '',
                      font=original.font(int(node.get('font-size'))), fill=node.get('fill'), anchor='ls')
        elif name == 'polyline':
            values = [float(n) for n in re.split(r'[ ,]+', node.get('points').strip())]
            draw.line([(round(values[i] * scale), round(values[i + 1] * scale))
                       for i in range(0, len(values), 2)], fill=node.get('stroke'),
                      width=round(float(node.get('stroke-width')) * scale), joint='curve')
        elif name == 'circle':
            x, y, r = (float(node.get(k)) * scale for k in ('cx', 'cy', 'r'))
            draw.ellipse((x - r, y - r, x + r, y + r), fill=node.get('fill'))
        else:
            assert name == 'g', name
    return canvas.resize(original.FINAL_SIZE, Image.Resampling.LANCZOS)


if __name__ == '__main__':
    main()
