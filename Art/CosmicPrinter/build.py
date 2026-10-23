"""PIL printer sprite: desktop all-in-one silhouette, current mod casing palette.

Style sources: Art/BuildingRefresh/GameExport/StorageCore.svg,
CosmicWorkbench_south.svg and StorageReceiver.svg. Coordinates are in the
existing 256 px/cell export scale; the 2x2 building uses a 544 px canvas.
"""
from pathlib import Path
import json
import shutil
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
SIZE, SS, OUTLINE = 544, 4, 10
PALETTE = {
    "shell": "#d8dfe3", "lid": "#cfd4d5", "light": "#e6e9e8",
    "bevel": "#acb8bf", "body": "#8498a4", "side": "#637782",
    "base": "#53616b", "bottom": "#414e58", "recess": "#304553",
    "deep": "#263c4c", "screen": "#344a5b", "blue": "#c5e5f5",
    "blue_dim": "#91adbf", "seam": "#74848c", "edge": "#b9c4ca",
}

# Reproject the component planes to the elevated, parallel view used by the
# other buildings. The top occupies most of the footprint; front faces are
# shallow bands. Strokes are drawn AFTER projection to keep a uniform width.
VIEW_Y = ((0,0), (34,34), (267,392), (276,403), (365,462),
          (480,491), (505,505), (544,544))


def view_y(y):
    for (a,b), (c,d) in zip(VIEW_Y, VIEW_Y[1:]):
        if y <= c:
            return b + (y-a) * (d-b) / (c-a)
    return y


def points(vertices):
    return [(round(x * SS), round(view_y(y) * SS)) for x, y in vertices]


def polygon(draw, vertices, color):
    draw.polygon(points(vertices), fill=PALETTE.get(color, color))


def rectangle(draw, bounds, color, radius=0):
    box = (round(bounds[0]*SS), round(view_y(bounds[1])*SS),
           round(bounds[2]*SS), round(view_y(bounds[3])*SS))
    if radius:
        draw.rounded_rectangle(box, radius=round(radius * SS), fill=PALETTE.get(color, color))
    else:
        draw.rectangle(box, fill=PALETTE.get(color, color))


def line(draw, vertices, color, width=2):
    draw.line(points(vertices), fill=PALETTE.get(color, color), width=round(width * SS), joint="curve")


def ellipse(draw, bounds, color):
    box = (round(bounds[0]*SS), round(view_y(bounds[1])*SS),
           round(bounds[2]*SS), round(view_y(bounds[3])*SS))
    draw.ellipse(box, fill=PALETTE.get(color, color))


def build():
    HERE.mkdir(parents=True, exist_ok=True)
    target = ROOT / "Textures/Things/Building/CosmicPrinter/CosmicPrinter.png"
    previous = HERE / "CosmicPrinter-initial.png"
    if target.exists() and not previous.exists():
        shutil.copyfile(target, previous)

    # A broad lid above a recessed output slot, a left-front control shelf,
    # and a quiet lower cabinet. No perspective skew in the internal equipment.
    body = [(53,235),(491,235),(508,253),(508,473),(486,505),
            (58,505),(36,478),(36,262)]
    lid = [(58,34),(486,34),(510,50),(510,255),(502,276),
           (42,276),(34,255),(34,50)]
    controls = [(45,270),(235,270),(235,333),(222,347),
                (51,347),(34,333),(32,293)]

    # Dilate the union geometrically before painting it. Once the filled art
    # is composited, black is visible only OUTSIDE the combined silhouette.
    contour = Image.new("RGBA", (SIZE*SS, SIZE*SS))
    cd = ImageDraw.Draw(contour)
    for shape in (body, lid, controls):
        polygon(cd, shape, "#000000")
        # Wrap past both ends so Pillow treats the closing vertex as a join,
        # not two flat stroke caps that leave a gap at the upper-left corner.
        closed = shape[-1:] + shape + shape[:2]
        cd.line(points(closed), fill="#000000", width=OUTLINE*2*SS, joint="curve")

    art = Image.new("RGBA", contour.size)
    d = ImageDraw.Draw(art)

    polygon(d, body, "base")
    # Continuous lower plinth and its restrained front inset match the core.
    polygon(d, [(36,440),(508,440),(508,473),(486,505),(58,505),(36,478)], "bottom")
    polygon(d, [(46,440),(498,440),(498,472),(481,492),(63,492),(46,473)], "side")
    rectangle(d, (228,483,316,490), "deep", 2)
    rectangle(d, (244,485,300,488), "blue")

    # Lower cabinet front: broad unbroken planes, upper-left highlights and
    # a shared lower/right bevel rather than black lines around every part.
    polygon(d, [(36,337),(508,337),(508,451),(489,480),(57,480),(36,454)], "shell")
    polygon(d, [(36,337),(47,343),(47,449),(65,469),(57,480),(36,454)], "light")
    polygon(d, [(497,343),(508,337),(508,451),(489,480),(57,480),
                (65,469),(481,469),(497,447)], "bevel")
    line(d, [(59,443),(485,443)], "edge", 2)
    line(d, [(62,445),(482,445)], "#b4bfc5", 1.5)

    # A quiet output aperture: one dark cavity, a shaded floor and a thin lip.
    polygon(d, [(54,266),(492,266),(498,278),(498,352),(483,365),
                (63,365),(45,350),(45,280)], "deep")
    polygon(d, [(54,334),(488,334),(488,351),(477,359),
                (63,359),(49,347)], "recess")
    polygon(d, [(63,357),(477,357),(488,348),(488,354),(477,365),
                (62,365),(45,350),(45,344)], "body")

    # Lid lower fascia, and a quiet scanner-like top plane. The upper casing
    # is the photographic reference's defining mass, not a glowing viewport.
    polygon(d, lid, "side")
    polygon(d, [(58,34),(486,34),(510,50),(510,255),(502,267),
                (42,267),(34,255),(34,50)], "lid")
    polygon(d, [(58,34),(486,34),(510,50),(499,54),(480,41),
                (64,41),(45,54),(45,251),(34,255),(34,50)], "light")
    polygon(d, [(510,50),(510,255),(502,267),(42,267),(48,256),
                (493,256),(499,250),(499,54)], "bevel")
    polygon(d, [(42,267),(502,267),(502,276),(42,276)], "#b9c4ca")
    line(d, [(48,267),(497,267)], "light", 2)

    # Thin lid seam and rear hinge pair: no text, logos or large decorative
    # shapes on the top. They follow the existing equipment's muted hardware.
    polygon(d, [(66,61),(478,61),(489,69),(489,231),(478,241),
                (66,241),(55,231),(55,69)], "#b6c0c5")
    polygon(d, [(68,64),(476,64),(486,71),(486,229),(476,237),
                (68,237),(58,229),(58,71)], "lid")
    line(d, [(68,65),(476,65)], "#dde1e0", 2)
    rectangle(d, (99,45,151,52), "body", 2)
    rectangle(d, (393,45,445,52), "body", 2)
    rectangle(d, (103,45,147,48), "edge", 1)
    rectangle(d, (397,45,441,48), "edge", 1)
    # Small recessed status lens, using the same pale blue as the workbench.
    rectangle(d, (435,246,476,252), "seam", 1)
    rectangle(d, (438,247,472,250), "blue", 1)

    # Left-front control shelf. It projects over the left end of the aperture
    # just like the reference, without turning the whole front into a screen.
    polygon(d, controls, "side")
    polygon(d, [(45,270),(235,270),(235,323),(222,336),(51,336),
                (34,324),(32,293)], "shell")
    polygon(d, [(45,274),(228,274),(228,320),(217,329),(53,329),
                (40,320),(38,294)], "screen")
    line(d, [(47,275),(225,275)], "edge", 2)
    polygon(d, [(51,336),(222,336),(235,323),(235,333),(222,347),
                (51,347),(34,333),(34,324)], "bevel")
    rectangle(d, (69,284,126,304), "deep", 2)
    line(d, [(76,289),(98,289),(98,294),(113,294)], "blue_dim", 2)
    rectangle(d, (76,298,109,301), "blue")
    ellipse(d, (48,307,63,322), "edge")
    ellipse(d, (52,311,59,318), "blue")
    for x in (80,107,134):
        rectangle(d, (x,311,x+18,318), "edge", 3)
    rectangle(d, (153,284,210,294), "#597180", 3)
    rectangle(d, (153,300,210,310), "#597180", 3)
    line(d, [(168,289),(194,289)], "blue", 2)
    line(d, [(169,305),(194,305)], "edge", 2)
    ellipse(d, (185,315,194,324), "blue_dim")
    ellipse(d, (202,315,211,324), "blue")

    image = Image.alpha_composite(contour, art).resize((SIZE,SIZE), Image.Resampling.LANCZOS)
    # Match existing dense-layout exports: 16 px canvas padding + 12 px inset.
    fitted = image.crop(image.getbbox()).resize((488,488), Image.Resampling.LANCZOS)
    image = Image.new("RGBA", (SIZE,SIZE))
    image.alpha_composite(fitted, (28,28))
    alpha = image.getchannel("A")
    box = alpha.getbbox()
    assert box[0] > 0 and box[1] > 0 and box[2] < SIZE and box[3] < SIZE, box
    target.parent.mkdir(parents=True, exist_ok=True)
    image.save(HERE / "CosmicPrinter-v5.png")
    image.save(target)

    # A neutral preview for review; it is not part of the in-game texture.
    preview = Image.new("RGBA", (640,640), "#d7dce0")
    preview.alpha_composite(image, (48,48))
    preview.convert("RGB").save(HERE / "CosmicPrinter-preview.png")
    preview.convert("RGB").save(HERE / "CosmicPrinter-preview-v5.png")
    report = dict(size=[SIZE,SIZE], footprint=[2,2], draw_size=[2.2325,2.2325],
                  palette=PALETTE, outline_pixels=OUTLINE, alpha_bbox=list(box),
                  transparent_background=True, generator="PIL", supersampling=SS,
                  view="elevated orthographic; parallel casing edges; compressed front faces",
                  reference_shape="wide all-in-one printer, left-front controls, central output slot",
                  style_sources=["Art/BuildingRefresh/GameExport/StorageCore.svg",
                                 "Art/BuildingRefresh/GameExport/CosmicWorkbench_south.svg",
                                 "Art/BuildingRefresh/GameExport/StorageReceiver.svg"],
                  target=str(target.relative_to(ROOT)).replace("\\","/"))
    (HERE / "manifest.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Updated printer texture: {SIZE}x{SIZE} RGBA; alpha bounds={box}; exterior outline={OUTLINE}px")


if __name__ == "__main__":
    build()
