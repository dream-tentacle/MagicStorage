"""Editable SVG studies for the remaining Magic Storage buildings."""
from pathlib import Path
from html import escape
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
ROOT.mkdir(parents=True, exist_ok=True)
MANIFEST = []
SKY = '#c5e5f5'


class Drawing:
    def __init__(self):
        self.parts = []

    def raw(self, value):
        self.parts.append(value)

    def rect(self, x, y, w, h, color, radius=0, stroke=None, sw=2):
        outline = f' stroke="{stroke}" stroke-width="{sw}"' if stroke else ''
        self.raw(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{radius}" fill="{color}"{outline}/>')

    def path(self, data, color='none', stroke=None, sw=2):
        outline = f' stroke="{stroke}" stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round"' if stroke else ''
        self.raw(f'<path d="{data}" fill="{color}"{outline}/>')

    def ellipse(self, cx, cy, rx, ry, color, stroke=None, sw=2):
        outline = f' stroke="{stroke}" stroke-width="{sw}"' if stroke else ''
        self.raw(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{color}"{outline}/>')

    def group(self, transform):
        self.raw(f'<g transform="{transform}">')

    def endgroup(self):
        self.raw('</g>')

    def svg(self, width, height, title):
        return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">\n'
                f'  <title>{escape(title)}</title>\n'
                '  <desc>Original Magic Storage design study. Pearl-grey industrial casing, restrained sky-blue RGB(197,229,245) inlays, and a uniform black exterior contour. Interior edges use muted blue-grey.</desc>\n'
                + '\n'.join('  ' + part for part in self.parts) + '\n</svg>\n')


def write(d, width, height, stem, label, category='building'):
    text = d.svg(width, height, label)
    ET.fromstring(text)
    (ROOT / (stem + '.svg')).write_text(text, encoding='utf-8')
    MANIFEST.append(dict(stem=stem, label=label, width=width, height=height, category=category))


def casing(d, width=512, height=512):
    """Keep downward-facing casing depth in every orientation."""
    d.rect(26, 70, width-52, height-96, '#798893', 28)
    d.rect(26, 42, width-52, height-96, '#d8dfe3', 28)
    d.path(f'M26 {height-82}Q26 {height-54} 54 {height-54}H{width-54}Q{width-26} {height-54} {width-26} {height-82}', stroke='#25313c', sw=4)
    d.path(f'M40 200V76Q40 56 61 56H{width-62}Q{width-44} 56 {width-40} 73', stroke='#f2f5f6', sw=3)
    d.path(f'M{width-39} 85V{height-95}Q{width-39} {height-68} {width-66} {height-68}H63', stroke='#a8b6bf', sw=4)


def contour(d, width=512, height=512):
    d.path(f'M54 42H{width-54}Q{width-26} 42 {width-26} 70V{height-54}Q{width-26} {height-26} {width-54} {height-26}H54Q26 {height-26} 26 {height-54}V70Q26 42 54 42Z', stroke='#000000', sw=8)


def status(d, x, y, width=126):
    d.rect(x, y, width, 29, '#aebdc6', 5)
    d.rect(x+4, y+4, width-8, 21, '#344a5b', 3)
    d.path(f'M{x+16} {y+14}H{x+37}M{x+47} {y+14}H{x+68}', stroke=SKY, sw=3)
    d.path(f'M{x+83} {y+14}H{x+width-15}', stroke='#91afc3', sw=2)


def front_strip(d, width=512, height=512):
    x = width/2-52
    d.rect(x, height-44, 104, 7, '#344756', 3.5)
    d.path(f'M{x+13} {height-41}H{x+39}M{x+73} {height-41}H{x+92}', stroke=SKY, sw=2)


def vents(d, x, y, count=4, length=42, gap=10):
    for i in range(count):
        d.rect(x+i*gap, y, 4, length, '#7c919f', 1)


# 1. Construction supply unit: two restrained uprights around a stepped supply aperture.
d = Drawing()
casing(d)
status(d, 193, 65)
d.raw('<!-- Raised material-transfer housing and recessed layered aperture. -->')
d.path('M130 136H382L405 160V364L380 392H132L107 364V160Z', '#849ba9')
d.path('M130 125H382L405 149V346L380 374H132L107 346V149Z', '#c5d0d8')
d.path('M132 132H378L393 149V341L376 362H136L119 341V149Z', '#e1e7eb')
d.path('M153 173H359L375 192V301L355 327H157L137 301V192Z', '#617d90')
d.path('M157 182H355L363 195V296L349 313H163L149 296V195Z', '#adc1ce')
d.path('M151 195H361V212H151Z', '#456176')
d.path('M155 224H357V235H155ZM155 264H357V275H155Z', '#849faf')
d.path('M160 225H352M160 265H352', stroke='#d5e3ec', sw=2)
d.rect(125, 344, 262, 15, '#c5e5f5', 3)
d.path('M136 347H376', stroke='#edf6fb', sw=2)
d.rect(63, 160, 30, 216, '#acbcc6', 7)
d.rect(65, 156, 26, 208, '#dce5ea', 6)
d.rect(73, 184, 10, 134, SKY, 2)
d.rect(419, 160, 30, 216, '#acbcc6', 7)
d.rect(421, 156, 26, 208, '#dce5ea', 6)
d.rect(429, 184, 10, 134, SKY, 2)
d.rect(173, 401, 166, 27, '#bac9d2', 5)
d.path('M190 410H322', stroke='#f1f5f7', sw=2)
d.path('M205 421H307', stroke='#8da3b2', sw=3)
front_strip(d)
contour(d)
write(d, 512, 512, 'ConstructionSupplyUnit-v1', '建筑供给单元')


# 2. Fuel supplier: a sealed processing drum, distinct from the open receiver.
d = Drawing()
casing(d)
status(d, 71, 68, 126)
d.rect(80, 153, 88, 223, '#b6c4cd', 10)
d.rect(85, 148, 78, 214, '#e1e7eb', 8)
d.rect(102, 174, 44, 60, '#bdced9', 4)
d.rect(113, 185, 22, 38, SKY, 3)
vents(d, 104, 294, 4, 45, 11)
d.raw('<!-- Rounded sealed fuel-processing chamber, with a sky-blue band and a mechanical cap. -->')
d.path('M218 170Q218 140 276 133Q359 123 384 165V338Q384 373 304 383Q218 379 218 346Z', '#8299a8')
d.rect(218, 161, 166, 164, '#b7c8d3', 37)
d.path('M224 174V313Q223 340 250 351H350Q379 341 378 315V174', '#cfdbe2')
d.path('M358 178V312Q357 337 340 347H351Q379 337 378 312V178Z', '#a5bac7')
d.ellipse(301, 171, 83, 42, '#e6edf1')
d.ellipse(301, 171, 64, 28, '#bacdd9')
d.ellipse(301, 167, 61, 26, SKY)
d.path('M248 155Q300 131 354 155', stroke='#eaf6fc', sw=3)
d.rect(287, 153, 28, 24, '#8ea6b7', 5)
d.rect(291, 151, 20, 20, '#dce7ee', 4)
d.path('M225 244Q301 271 377 244V268Q301 295 225 268Z', '#98adbc')
d.path('M225 246Q301 273 377 246V263Q301 290 225 263Z', SKY)
d.path('M231 249Q301 273 371 249', stroke='#edf8fd', sw=2)
d.path('M215 338H387V363Q387 380 365 386H237Q215 380 215 363Z', '#b2c2cd')
d.path('M224 343H378', stroke='#edf3f7', sw=3)
d.rect(245, 357, 112, 16, '#8da4b3', 4)
d.rect(252, 359, 98, 6, '#496578', 2)
d.path('M149 384V401Q149 412 164 412H210', stroke='#a6bac8', sw=13)
d.path('M149 384V401Q149 412 164 412H210', stroke=SKY, sw=7)
front_strip(d)
contour(d)
write(d, 512, 512, 'FuelSupplier-v1', '燃料供给器')


# 3. Nivarian extractor: an open docking collar without a central sphere or icon.
d = Drawing()
casing(d)
status(d, 193, 65)
d.path('M125 129H387L410 153V365L387 392H125L102 365V153Z', '#a7b9c5')
d.path('M129 133H383L398 152V354L382 376H130L114 354V152Z', '#e0e7eb')
d.ellipse(256, 273, 137, 118, '#8199a9')
d.ellipse(256, 253, 137, 118, '#c4d4df')
d.ellipse(256, 253, 117, 99, '#e6edf2')
d.ellipse(256, 253, 98, 83, '#506c80')
d.ellipse(256, 269, 92, 66, '#819daf')
d.path('M176 245Q256 293 336 245V290Q256 338 176 290Z', '#a4bdcd')
d.path('M183 291Q256 331 329 291', stroke=SKY, sw=5)
d.path('M151 220Q168 164 219 150M293 150Q342 164 361 220', stroke=SKY, sw=9)
for x in [113, 365]:
    d.rect(x, 227, 34, 65, '#91a8b8', 5)
    d.rect(x+3, 221, 28, 61, '#d8e4eb', 4)
    d.rect(x+10, 235, 14, 30, SKY, 2)
d.rect(220, 363, 72, 37, '#9ab0c0', 6)
d.rect(224, 359, 64, 32, '#d8e4eb', 5)
d.rect(237, 371, 38, 7, SKY, 2)
front_strip(d)
contour(d)
write(d, 512, 512, 'NivarianCannonExtractor-v1', '涅瓦莲质量驱动器提取口')


def workbench_surface(d):
    """3×1 bench: broad empty working area, left transfer pad, right controls."""
    d.raw('<!-- Broad working surface with a quiet central fabrication bed. -->')
    d.rect(72, 88, 1392, 294, '#a4b7c4', 18)
    d.rect(77, 83, 1382, 289, '#e4ebef', 16)
    d.path('M96 89H1437', stroke='#f7f9fa', sw=3)
    d.path('M422 103V346M1221 103V346', stroke='#b1c2ce', sw=3)

    # Low intake/return pad at the left end.
    d.rect(110, 142, 263, 171, '#9fb4c2', 12)
    d.rect(119, 151, 245, 150, '#6e8b9f', 8)
    d.rect(129, 162, 225, 130, '#b6cbd9', 5)
    d.path('M151 179V193M332 179V193M151 262V276H183M300 276H332V262', stroke=SKY, sw=4)
    d.rect(176, 325, 135, 12, '#bdccd6', 3)
    d.path('M186 327H301', stroke='#f4f7f9', sw=2)

    # Central fabrication bed with two unobtrusive clamp rails.
    d.rect(463, 130, 710, 204, '#bbcbd6', 13)
    d.rect(469, 136, 698, 190, '#d2e2ec', 10)
    d.path('M487 143H1148', stroke='#edf5fa', sw=3)
    d.path('M489 175V156H534M1102 156H1147V175M489 287V307H534M1102 307H1147V287', stroke='#8aa5b8', sw=5)
    d.rect(691, 118, 252, 12, '#adc0cd', 3)
    d.rect(691, 333, 252, 12, '#adc0cd', 3)
    d.path('M711 124H758M876 124H923M711 339H758M876 339H923', stroke=SKY, sw=3)

    # An inset control terminal occupies only the right-hand end.
    d.rect(1260, 124, 155, 116, '#a6bac8', 9)
    d.rect(1267, 130, 141, 103, '#3f596d', 6)
    d.path('M1287 153H1324M1287 165H1314M1287 191H1390', stroke='#8eafc5', sw=3)
    d.rect(1340, 148, 45, 27, SKY, 3)
    d.path('M1333 212H1388', stroke=SKY, sw=3)
    d.rect(1268, 262, 141, 74, '#c7d5de', 7)
    d.path('M1290 285H1387M1290 309H1353', stroke='#9bb2c2', sw=4)
    d.rect(1371, 302, 17, 10, SKY, 2)


def apparel_surface(d):
    """2×1 adapter: open clothing compartment and a compact service cabinet."""
    d.raw('<!-- Clothing bay with a visible physical hanging garment, not a printed pictogram. -->')
    d.rect(71, 91, 605, 303, '#a4b7c5', 17)
    d.rect(78, 98, 591, 283, '#c4d3de', 12)
    d.rect(99, 119, 550, 241, '#496579', 8)
    d.rect(105, 149, 538, 205, '#8da7b9', 5)
    d.path('M113 351H635', stroke='#b7cddc', sw=3)
    d.path('M120 144H627', stroke='#bed1df', sw=9)
    d.path('M125 140H622', stroke='#edf4f9', sw=2)

    # Hanger, collar and a broad folded garment form a single readable silhouette.
    d.path('M370 140V157M370 157L299 189H441Z', stroke='#dce8f0', sw=5)
    d.path('M338 189L317 192L282 237L306 257L327 235V327Q370 338 414 327V235L435 257L459 237L424 192L402 189L382 211H358Z', '#7391a7')
    d.path('M338 180L317 184L282 229L306 247L327 225V314Q370 326 414 314V225L435 247L459 229L424 184L402 180L383 201H358Z', '#d4e5f0')
    d.path('M338 180L358 201H383L402 180L389 178Q370 192 350 178Z', '#97b3c7')
    d.path('M371 213V304', stroke='#adc5d6', sw=3)
    d.path('M333 306Q370 314 408 306', stroke='#edf6fc', sw=3)
    d.rect(87, 131, 8, 195, SKY, 2)
    d.rect(653, 131, 8, 195, SKY, 2)
    d.rect(220, 372, 305, 18, '#c8d6df', 4)
    d.path('M236 376H509', stroke='#f1f6f9', sw=2)

    d.rect(707, 91, 240, 303, '#aebfcb', 14)
    d.rect(713, 86, 228, 298, '#e2e9ee', 12)
    status(d, 753, 108, 146)
    d.rect(747, 170, 160, 134, '#c3d2dc', 7)
    d.rect(753, 176, 148, 121, '#e7edf1', 5)
    d.rect(778, 274, 99, 7, '#a1b7c7', 3)
    d.rect(747, 317, 160, 39, '#c3d2dc', 5)
    d.path('M773 337H819', stroke='#91aabb', sw=4)
    d.rect(855, 331, 24, 10, SKY, 2)


def oriented_surface(d, width, direction, draw_surface):
    # Keep outside lighting fixed. Rotate only the equipment on the top plane.
    if direction == 'south':
        draw_surface(d)
    elif direction == 'north':
        d.group(f'rotate(180 {width/2} 236)')
        draw_surface(d)
        d.endgroup()
    else:
        d.group(f'translate(20 {width}) rotate(-90)')
        draw_surface(d)
        d.endgroup()


def apparel_back(d):
    # Back of the clothing compartment: quiet cladding, no inverted hanging garment.
    d.rect(348, 91, 605, 303, '#a4b7c5', 17)
    d.rect(355, 86, 591, 298, '#e2e9ee', 12)
    d.rect(383, 116, 535, 235, '#b9cbd7', 8)
    d.rect(389, 122, 523, 222, '#d8e2e9', 6)
    d.path('M650 127V337', stroke='#b1c5d3', sw=3)
    d.path('M402 130H634M669 130H898', stroke='#eef4f8', sw=2)
    d.rect(365, 137, 8, 194, SKY, 2)
    d.rect(929, 137, 8, 194, SKY, 2)
    d.rect(571, 365, 158, 11, '#bbcdd9', 3)
    d.rect(77, 91, 240, 303, '#aebfcb', 14)
    d.rect(83, 86, 228, 298, '#e2e9ee', 12)
    d.rect(117, 116, 160, 201, '#bdcfdc', 7)
    d.rect(123, 122, 148, 188, '#dae4ec', 5)
    d.rect(158, 275, 78, 7, '#a1b7c7', 3)
    vents(d, 168, 333, 6, 28, 11)


def apparel_side(d):
    # The long side faces the viewer, with the access lip along the east edge.
    d.rect(98, 70, 296, 592, '#a4b7c5', 17)
    d.rect(105, 76, 282, 580, '#e2e9ee', 12)
    d.rect(129, 103, 210, 526, '#bbcedc', 8)
    d.rect(135, 109, 198, 514, '#d8e3eb', 6)
    d.path('M142 119V611', stroke='#eff5f9', sw=3)
    d.path('M142 366H325', stroke='#b1c5d3', sw=3)
    d.rect(356, 120, 19, 493, '#829eb2', 5)
    d.rect(358, 125, 8, 483, SKY, 3)
    d.rect(373, 133, 10, 467, '#c4d5e1', 3)
    d.rect(229, 354, 11, 49, '#9db5c7', 3)
    d.rect(98, 708, 296, 228, '#aebfcb', 14)
    d.rect(105, 702, 282, 224, '#e2e9ee', 12)
    status(d, 170, 727, 146)
    d.rect(146, 789, 199, 95, '#bdcfdc', 7)
    d.rect(152, 795, 187, 83, '#dae4ec', 5)
    vents(d, 221, 899, 6, 12, 11)


for stem, label, width, surface in [
    ('CosmicWorkbench', '宇宙工作台', 1536, workbench_surface),
    ('StorageApparelAdapter', '衣物适配器', 1024, apparel_surface),
]:
    for direction, direction_label in [('south', '南'), ('north', '北'), ('east', '东')]:
        w, h = (512, width) if direction == 'east' else (width, 512)
        d = Drawing()
        casing(d, w, h)
        if stem == 'StorageApparelAdapter' and direction == 'north':
            apparel_back(d)
        elif stem == 'StorageApparelAdapter' and direction == 'east':
            apparel_side(d)
        else:
            oriented_surface(d, width, direction, surface)
        front_strip(d, w, h)
        contour(d, w, h)
        write(d, w, h, f'{stem}_{direction}-v1', f'{label} — {direction_label}向')


# 4×4 linked atlas: bits Up=1, Right=2, Down=4, Left=8.
# RimWorld crops each quarter to its middle 3/4, so 256px tiles use 32px gutters.
# SVG y runs down, unlike texture UV v: mask 0 is on the bottom row.
directions = [(1, 128, -8), (2, 264, 128), (4, 128, 264), (8, -8, 128)]


def conduit_tile(d, mask):
    paths = ' '.join(f'M128 128L{x} {y}' for bit, x, y in directions if mask & bit)
    if not paths:
        d.rect(114, 114, 28, 28, '#000000', 7)
        d.rect(117, 117, 22, 22, '#a5bac8', 5)
        d.rect(122, 122, 12, 12, SKY, 3)
    else:
        # Layer whole junctions, avoiding outlines cutting across their own branches.
        for color, sw in [('#000000', 14), ('#a5bac8', 10), (SKY, 6)]:
            d.path(paths, stroke=color, sw=sw)
        d.path(paths, stroke='#e8f5fc', sw=1.5)


d = Drawing()
d.raw('<defs>')
for mask in range(16):
    d.raw(f'<clipPath id="tile-{mask}"><rect width="256" height="256"/></clipPath>')
d.raw('</defs>')
for mask in range(16):
    tx = (mask % 4)*256
    ty = (3-mask//4)*256
    d.group(f'translate({tx} {ty})')
    d.raw(f'<g clip-path="url(#tile-{mask})">')
    conduit_tile(d, mask)
    d.endgroup()
    d.endgroup()
write(d, 1024, 1024, 'StorageConduit_Atlas-v1', '存储线 — 十六种连接形态', 'atlas')

d = Drawing()
conduit_tile(d, 10)
write(d, 256, 256, 'StorageConduit_MenuIcon-v1', '存储线 — 图标', 'icon')
(ROOT / 'manifest.json').write_text(json.dumps(MANIFEST, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Created {len(MANIFEST)} standalone SVG assets in {ROOT}')
