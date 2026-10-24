"""Draw the transport-group command as three pods converging on one point."""

from math import cos, hypot, pi, sin
from pathlib import Path

from PIL import Image, ImageDraw


SIZE = 128
SCALE = 4
WHITE = (255, 255, 255, 255)
image = Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE), (255, 255, 255, 0))
draw = ImageDraw.Draw(image)


def line(points, width=4):
    draw.line([(round(x * SCALE), round(y * SCALE)) for x, y in points],
              fill=WHITE, width=width * SCALE, joint="curve")


def ellipse(box, fill=None, width=4):
    draw.ellipse(tuple(round(value * SCALE) for value in box),
                 fill=fill, outline=WHITE, width=width * SCALE)


def arrow(start, tip):
    dx, dy = tip[0] - start[0], tip[1] - start[1]
    length = hypot(dx, dy)
    ux, uy = dx / length, dy / length
    base = (tip[0] - ux * 10, tip[1] - uy * 10)
    line([start, tip])
    line([(base[0] - uy * 5, base[1] + ux * 5), tip,
          (base[0] + uy * 5, base[1] - ux * 5)])


# Three separate arrows point at the single shared destination above the pods.
ellipse((58, 8, 70, 20), fill=WHITE)
arrow((24, 57), (53, 25))
arrow((64, 57), (64, 30))
arrow((104, 57), (75, 25))

# A semicircle joins a plain U-shaped body, with no seam or landing feet.
for center in (24, 64, 104):
    dome = [(center + 13 * cos(pi + pi * step / 48),
             83 + 13 * sin(pi + pi * step / 48)) for step in range(49)]
    line(dome + [(center + 13, 103), (center - 13, 103), (center - 13, 83)])

output = Path(__file__).resolve().parents[2] / "Textures/UI/Gizmos/MagicStorage/CreateTransportGroup.png"
image = image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
image.save(output)
print(f"Saved {output.name}: {image.width}x{image.height}, {image.mode}")
