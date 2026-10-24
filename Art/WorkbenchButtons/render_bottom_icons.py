"""Render the polygon-based SVG sources into antialiased game PNG textures."""
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

from PIL import Image, ImageColor, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SOURCES = Path(__file__).resolve().parent
TEXTURES = ROOT / 'Textures' / 'UI' / 'Gizmos' / 'MagicStorage'
SCALE = 32
OUTPUT_SIZE = 96

icon_names = ('BottomEmpty', 'BottomFilled', 'BottomHover',
              'FavoriteEmpty', 'FavoriteFilled', 'FavoriteHover')
names = sys.argv[1:] or icon_names
assert all(name in icon_names for name in names)
for name in names:
    source = SOURCES / f'{name}.svg'
    svg = ET.parse(source).getroot()
    polygon = svg.find('{http://www.w3.org/2000/svg}polygon')
    assert svg.get('viewBox') == '0 0 24 24'
    points = [tuple(float(v) * SCALE for v in pair.split(','))
              for pair in polygon.get('points').split()]
    canvas = Image.new('RGBA', (24 * SCALE, 24 * SCALE))
    draw = ImageDraw.Draw(canvas)
    fill = polygon.get('fill')
    if fill != 'none':
        fill_alpha = round(float(polygon.get('fill-opacity', '1')) * 255)
        draw.polygon(points, fill=ImageColor.getrgb(fill) + (fill_alpha,))
    stroke = ImageColor.getrgb(polygon.get('stroke')) + (255,)
    stroke_width = float(polygon.get('stroke-width')) * SCALE
    draw.line(points + [points[0]], fill=stroke, width=round(stroke_width), joint='curve')
    # Pillow's line has open end caps, even when its last point repeats the first.
    # Round every join explicitly so the polygon's first corner closes too.
    radius = stroke_width / 2
    for x, y in points:
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=stroke)
    texture = canvas.resize((OUTPUT_SIZE, OUTPUT_SIZE), Image.Resampling.LANCZOS)
    center = texture.getpixel((OUTPUT_SIZE // 2, OUTPUT_SIZE // 2))
    assert texture.getpixel((0, 0))[3] == 0
    expected_alpha = 0 if fill == 'none' else round(float(polygon.get('fill-opacity', '1')) * 255)
    assert center[3] == expected_alpha
    if fill != 'none':
        assert all(abs(actual - expected) <= 2
                   for actual, expected in zip(center[:3], ImageColor.getrgb(fill)))
    # Every vertex, including the path's first vertex, must have a closed stroke.
    for x, y in points:
        pixel = (round(x / SCALE * OUTPUT_SIZE / 24), round(y / SCALE * OUTPUT_SIZE / 24))
        assert texture.getpixel(pixel)[3] > 220
    destination = TEXTURES / f'{name}.png'
    texture.save(destination)
    print(f'Rendered and validated {destination.relative_to(ROOT)} ({OUTPUT_SIZE} x {OUTPUT_SIZE}, RGBA)')
