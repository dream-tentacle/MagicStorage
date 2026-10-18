"""Compose a workshop introduction from the approved copy and original texture."""
from pathlib import Path
import base64
import html
import re
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
SCALE = 3
WIDTH, HEIGHT = 1400, 400
COLORS = {
    'frame': '#d5eaf5', 'title': '#e3f5ff', 'subtitle': '#a9cadc',
    'body': '#deedf5', 'effect': '#f3ed83',
}
copy = (OUT / '建筑宣传文案.md').read_text(encoding='utf-8')
section = copy.split('**1．存储控制核心**', 1)[1].split('**2．', 1)[0]
body = next(line.strip() for line in section.splitlines() if line.strip())
effects = re.findall(r'^- (.+)$', section, re.M)
assert len(effects) == 4
canvas = Image.new('RGBA', (WIDTH*SCALE, HEIGHT*SCALE))
draw = ImageDraw.Draw(canvas)
svg = []

def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/msyhbd.ttc', size*SCALE)

def line(points, color, width):
    draw.line([(round(x*SCALE), round(y*SCALE)) for x, y in points],
              fill=color, width=round(width*SCALE), joint='curve')
    svg.append(f'<polyline points="{" ".join(f"{x},{y}" for x,y in points)}" '
               f'fill="none" stroke="{color}" stroke-width="{width}"/>')

def text(x, baseline, value, size, color):
    assert draw.textlength(value, font=font(size)) <= (1322-x)*SCALE
    draw.text((x*SCALE, baseline*SCALE), value, font=font(size), fill=color, anchor='ls')
    svg.append(f'<text x="{x}" y="{baseline}" font-size="{size}" fill="{color}">'
               f'{html.escape(value)}</text>')

def centered(baseline, value, size, spacing, color):
    face = font(size)
    total = sum(draw.textlength(c, font=face) for c in value) + spacing*SCALE*(len(value)-1)
    x = 925-total/(2*SCALE)
    for char in value:
        text(x, baseline, char, size, color)
        x += draw.textlength(char, font=face)/SCALE + spacing
    return total/SCALE

for y in range(42, 360, 17):
    draw.rectangle((48*SCALE, y*SCALE, 438*SCALE-1, (y+7)*SCALE-1),
                   fill=(128,128,128,128))
    svg.append(f'<rect x="48" y="{y}" width="390" height="7" fill="#808080" fill-opacity="0.5"/>')

source = ROOT / 'Textures/Things/Building/StorageCore/StorageCore.png'
with Image.open(source) as original:
    icon = original.convert('RGBA')
icon = icon.crop(icon.getchannel('A').getbbox())
factor = min(256*SCALE/icon.width, 270*SCALE/icon.height)
icon = icon.resize((round(icon.width*factor), round(icon.height*factor)), Image.Resampling.LANCZOS)
ix, iy = round(243*SCALE-icon.width/2), round(200*SCALE-icon.height/2)
canvas.alpha_composite(icon, (ix, iy))
encoded = base64.b64encode(source.read_bytes()).decode('ascii')
svg.append(f'<image x="{ix/SCALE}" y="{iy/SCALE}" width="{icon.width/SCALE}" '
           f'height="{icon.height/SCALE}" preserveAspectRatio="xMidYMid meet" '
           f'href="data:image/png;base64,{encoded}"/>')

title_width = centered(48, '存储控制核心', 38, 5, COLORS['title'])
line([(490,60),(490,30),(925-title_width/2-40,30)], COLORS['frame'], 5)
line([(925+title_width/2+40,30),(1352,30),(1352,60)], COLORS['frame'], 5)
line([(490,338),(490,370),(1352,370),(1352,338)], COLORS['frame'], 5)
line([(490,75),(490,325)], '#7d929f', 1)
line([(1352,75),(1352,325)], '#7d929f', 1)
centered(84, 'STORAGE CONTROL CORE', 13, 3, COLORS['subtitle'])

lines, current = [], ''
for char in body:
    if current and draw.textlength(current+char, font=font(26)) > 800*SCALE:
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
assert len(lines) <= 2
for index, value in enumerate(lines):
    text(520, 128+index*38, value, 26, COLORS['body'])

for baseline, value in zip([220,258,296,334], effects):
    cx, cy, r = 526*SCALE, (baseline-9)*SCALE, 4*SCALE
    draw.ellipse((cx-r,cy-r,cx+r,cy+r), fill=COLORS['effect'])
    svg.append(f'<circle cx="526" cy="{baseline-9}" r="4" fill="{COLORS["effect"]}"/>')
    text(548, baseline, value, 26, COLORS['effect'])

result = canvas.resize((2800,800), Image.Resampling.LANCZOS)
path = OUT / '存储控制核心_介绍_v1.png'
result.save(path)
svg_path = OUT / '存储控制核心_介绍_v1.svg'
svg_path.write_text('<svg xmlns="http://www.w3.org/2000/svg" width="2800" height="800" '
                    'viewBox="0 0 1400 400"><g font-family="Microsoft YaHei,sans-serif" '
                    'font-weight="700">'+''.join(svg)+'</g></svg>', encoding='utf-8')
with Image.open(path) as saved:
    assert saved.size == (2800,800) and saved.mode == 'RGBA'
    assert saved.getpixel((0,0))[3] == 0
print(path)
print(svg_path)
