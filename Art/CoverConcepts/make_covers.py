"""Compose three text-free mod covers from the project's original textures.

Run from any directory: python Art/CoverConcepts/make_covers.py
Only Pillow and the Python standard library are used. Source assets stay intact.
"""
from pathlib import Path
import json
import math

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUT = HERE / "v1"
W, H = 1920, 1080
TEX = ROOT / "Textures" / "Things" / "Building"
ASSETS = {
    "core": "StorageCore/StorageCore.png",
    "unit": "StorageUnit/StorageUnit_south.png",
    "receiver": "StorageReceiver/StorageReceiver.png",
    "supplier": "TransportSupplier/TransportSupplier_south.png",
    "shelf": "StorageSupplyShelf/StorageSupplyShelf.png",
    "fuel": "FuelSupplier/FuelSupplier.png",
    "construction": "ConstructionSupplyUnit/ConstructionSupplyUnit.png",
    "teleporter": "CaravanTeleporter/CaravanTeleporter.png",
    "workbench": "CosmicWorkbench/CosmicWorkbench_south.png",
    "apparel": "StorageApparelAdapter/StorageApparelAdapter_south.png",
}
CYAN = (98, 214, 247)
BLUE = (98, 145, 241)
LILAC = (162, 139, 245)
GOLD = (211, 183, 122)
PLACEMENTS = []


def glow(im, center, radii, color, strength):
    """Soft, truly elliptical falloff; computed at quarter resolution."""
    sw, sh = W // 4, H // 4
    cx, cy = center[0] / 4, center[1] / 4
    rx, ry = radii[0] / 4, radii[1] / 4
    mask = Image.new("L", (sw, sh))
    mask.putdata([
        round(strength * math.exp(-2.7 * (((x-cx)/rx)**2 + ((y-cy)/ry)**2)))
        for y in range(sh) for x in range(sw)
    ])
    layer = Image.new("RGBA", (W, H), color)
    layer.putalpha(mask.resize((W, H), Image.Resampling.BICUBIC))
    im.alpha_composite(layer)


def background(seed, scheme):
    im = Image.new("RGBA", (W, H), (8, 14, 27, 255))
    if scheme == 1:
        glow(im, (1460, 550), (990, 770), (30, 78, 125), 190)
        glow(im, (1680, 110), (780, 650), (86, 59, 128), 100)
        glow(im, (300, 820), (830, 700), (30, 45, 68), 80)
    elif scheme == 2:
        glow(im, (1040, 900), (1300, 590), (31, 81, 104), 180)
        glow(im, (330, 600), (680, 700), (48, 71, 120), 110)
    else:
        glow(im, (1420, 690), (1130, 770), (29, 63, 109), 185)
        glow(im, (1750, 350), (750, 650), (63, 51, 121), 115)
    return im


def strokes(im, draw_fn, color=CYAN, alpha=85, blur=10):
    ink = Image.new("RGBA", (W, H))
    draw_fn(ImageDraw.Draw(ink), (*color, alpha))
    haze = ink.filter(ImageFilter.GaussianBlur(blur))
    im.alpha_composite(haze)
    im.alpha_composite(ink)


def orbit(im, cx, cy, rx, ry, color=CYAN, alpha=75, ticks=False):
    def draw(d, c):
        d.ellipse((cx-rx, cy-ry, cx+rx, cy+ry), outline=c, width=2)
        if ticks:
            for n in range(48):
                a = n * math.tau / 48
                t = 0.975 if n % 4 else 0.94
                d.line((cx+rx*t*math.cos(a), cy+ry*t*math.sin(a),
                        cx+rx*math.cos(a), cy+ry*math.sin(a)), fill=c, width=2)
    strokes(im, draw, color, alpha)


def bezier(points, count=120):
    p0, p1, p2, p3 = points
    return [tuple((1-t)**3*p0[k] + 3*(1-t)**2*t*p1[k] +
                  3*(1-t)*t*t*p2[k] + t**3*p3[k] for k in (0, 1))
            for t in [i/count for i in range(count+1)]]


def ribbon(im, points, color=CYAN, alpha=115):
    curve = bezier(points)
    strokes(im, lambda d, c: d.line(curve, fill=c, width=3), color, alpha, 14)


def sprite(im, name, center, height, aura=CYAN):
    src = Image.open(TEX / ASSETS[name]).convert("RGBA")
    src = src.crop(src.getchannel("A").getbbox())
    width = round(src.width * height / src.height)
    src = src.resize((width, height), Image.Resampling.LANCZOS)
    x, y = round(center[0]-width/2), round(center[1]-height/2)
    assert x >= 0 and y >= 0 and x+width <= W and y+height <= H, (name, (x, y, width, height))
    mask = Image.new("L", (W, H))
    mask.paste(src.getchannel("A"), (x, y))
    bloom = Image.new("RGBA", (W, H), aura)
    bloom.putalpha(mask.filter(ImageFilter.GaussianBlur(19)).point(lambda a: round(a*.25)))
    im.alpha_composite(bloom)
    shadow_mask = Image.new("L", (W, H))
    shadow_mask.paste(src.getchannel("A"), (x+8, y+18))
    shadow = Image.new("RGBA", (W, H), (0, 4, 12))
    shadow.putalpha(shadow_mask.filter(ImageFilter.GaussianBlur(13)).point(lambda a: round(a*.7)))
    im.alpha_composite(shadow)
    im.alpha_composite(src, (x, y))
    PLACEMENTS.append({"asset": name, "box": [x, y, x+width, y+height]})


def save(im, name, zone, color, label):
    path = OUT / f"{name}.png"
    im.convert("RGB").save(path)
    guide = im.copy()
    overlay = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(overlay)
    d.rounded_rectangle(zone, radius=14, fill=(20, 35, 56, 115), outline=(*color, 195), width=2)
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 24)
    d.text((zone[0]+24, zone[1]+20), "TEXT SAFE AREA", font=font, fill=(*color, 240))
    guide.alpha_composite(overlay)
    guide.convert("RGB").save(OUT / f"{name}_文字区参考.jpg", quality=94)
    # Separate removable overlay for image editors.
    overlay.save(OUT / f"{name}_文字区图层.png")
    assert all(b["box"][2] <= zone[0] or b["box"][0] >= zone[2] or
               b["box"][3] <= zone[1] or b["box"][1] >= zone[3] for b in PLACEMENTS), name
    record = {"file": path.name, "label": label, "size": [W, H],
              "text_safe_area_xyxy": zone, "placements": list(PLACEMENTS)}
    PLACEMENTS.clear()
    return record


def cover_core():
    im = background(17, 1)
    glow(im, (1350, 610), (630, 570), CYAN, 54)
    for rx, ry, a in [(490,365,55), (456,340,85), (375,280,35)]:
        orbit(im, 1370, 580, rx, ry, CYAN, a, ticks=rx == 456)
    orbit(im, 1370, 580, 470, 352, LILAC, 40)
    for p, color in [((1050,303), BLUE), ((1660,350), LILAC), ((1685,785), CYAN), ((1060,865), CYAN)]:
        ribbon(im, [(1350,610), (1430,560), (p[0]-70,p[1]+65), p], color, 70)
    sprite(im, "unit", (1050, 303), 246, BLUE)
    sprite(im, "unit", (1660, 350), 220, LILAC)
    sprite(im, "receiver", (1060, 865), 143)
    sprite(im, "teleporter", (1685, 785), 180)
    sprite(im, "core", (1350, 590), 468)
    sprite(im, "workbench", (1430, 945), 125)
    return save(im, "01_魔法核心", [130, 205, 810, 795], CYAN, "CORE / 左侧留白")


def cover_matrix():
    im = background(39, 2)
    # Floor plane exists only beneath the installation, fading toward the title.
    grid = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(grid)
    for end in range(-1100, 3100, 160):
        d.line((970, 435, end, 1080), fill=(94, 164, 190, 27), width=1)
    for y in (500, 552, 618, 700, 801, 925, 1078):
        d.line((100, y, 1830, y), fill=(94, 164, 190, 25), width=1)
    im.alpha_composite(grid)
    for x in (650, 890, 1130, 1370, 1610):
        orbit(im, x, 710, 101, 32, BLUE, 45)
        sprite(im, "unit", (x, 630), 240, BLUE)
    orbit(im, 310, 840, 226, 76, CYAN, 80, True)
    sprite(im, "core", (310, 710), 370)
    for x in (720, 1040, 1360):
        orbit(im, x, 980, 112, 35, CYAN, 60)
        sprite(im, "unit", (x, 850), 300)
    sprite(im, "supplier", (1660, 903), 170)
    return save(im, "02_仓储矩阵", [160, 105, 1700, 415], CYAN, "MATRIX / 上方留白")


def cover_network():
    im = background(65, 3)
    routes = [
        ([(1100,720),(1230,630),(1390,490),(1495,295)], LILAC),
        ([(1100,720),(1340,820),(1560,790),(1710,625)], CYAN),
        ([(1100,720),(1040,870),(770,930),(580,855)], BLUE),
        ([(1495,295),(1700,300),(1820,440),(1710,625)], LILAC),
        ([(580,855),(810,1080),(1320,1020),(1510,945)], CYAN),
    ]
    for points, color in routes:
        ribbon(im, points, color, 145)
    for (x,y,rx,ry,c) in [(1100,750,262,210,CYAN), (1495,295,165,131,LILAC),
                           (1710,625,135,108,CYAN), (580,855,119,94,BLUE)]:
        orbit(im, x, y, rx, ry, c, 65, True)
        orbit(im, x, y, rx+15, ry+12, c, 30)
    sprite(im, "unit", (1495, 290), 240, LILAC)
    sprite(im, "teleporter", (1710, 620), 180)
    sprite(im, "supplier", (580, 855), 146, BLUE)
    sprite(im, "workbench", (1510, 945), 124)
    sprite(im, "core", (1100, 720), 372)
    return save(im, "03_传送网络", [130, 130, 830, 560], LILAC, "NETWORK / 左上留白")


def overview(records):
    sheet = Image.new("RGB", (1920, 520), (12, 18, 30))
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 25)
    small = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
    labels = ["01  魔法核心", "02  仓储矩阵", "03  传送网络"]
    captions = ["左侧标题区 · 核心装置主视觉", "上方标题区 · 仓储设施群", "左上标题区 · 光流连接设施"]
    for i, rec in enumerate(records):
        x = 24 + i*632
        d.text((x, 27), labels[i], font=font, fill=(224,234,244))
        img = Image.open(OUT / rec["file"]).resize((608,342), Image.Resampling.LANCZOS)
        sheet.paste(img, (x,82))
        d.text((x, 446), captions[i], font=small, fill=(139,163,186))
    sheet.save(OUT / "三款封面对比.jpg", quality=96)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    records = [cover_core(), cover_matrix(), cover_network()]
    overview(records)
    (OUT / "layout.json").write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
    (OUT / "使用说明.md").write_text(
        "# 魔法存储封面方案\n\n"
        "三款正式封面均为 1920×1080、RGB、无文字 PNG。建筑来自当前 mod 的 Textures，"
        "按透明边界裁切、等比缩放，未重绘或修改原素材。背景、光环和连接线由 Pillow 绘制。\n\n"
        "- 01_魔法核心：主推方案；左侧适合两行标题和两三行简介。文字安全区 (130,205)–(810,795)。\n"
        "- 02_仓储矩阵：突出容量与秩序；上方可放居中或左对齐的大标题。安全区 (160,105)–(1700,415)。\n"
        "- 03_传送网络：突出设施协同；左上适合标题、副标题。安全区 (130,130)–(830,560)。\n\n"
        "推荐使用暖白标题、灰蓝副标题；标题用粗体，避免压住设施。正式 PNG 不含文字框；"
        "带“文字区参考”的 JPG 仅用于排版，透明“文字区图层”可单独叠入图像编辑器并在导出前隐藏。\n\n"
        "三款封面对比.jpg 用于快速选款；layout.json 记录文字区和素材坐标；"
        "make_covers.py 可重新生成全部结果。未修改 About/Preview.png，也未部署。\n",
        encoding="utf-8")
    for rec in records:
        path = OUT / rec["file"]
        with Image.open(path) as im:
            assert im.size == (W,H) and im.mode == "RGB"
            im.verify()
        print(f"OK {path.name}: 1920x1080 RGB; {len(rec['placements'])} assets; clear text zone; {path.stat().st_size} bytes")
    print(f"Output: {OUT}")


if __name__ == "__main__":
    main()
