"""Export the approved output port at the same canvas size as other single-cell buildings."""
from pathlib import Path
import sys

from PIL import Image


ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'Art/Workshop'))
from update_production_output import port_icon


def main():
    image = port_icon()
    bounds = image.getchannel('A').getbbox()
    assert bounds is not None
    image = image.crop(bounds)
    # Match the established 288px canvas and 28px transparent border.
    factor = 232 / max(image.size)
    image = image.resize(tuple(round(size * factor) for size in image.size), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', (288, 288))
    canvas.alpha_composite(image, ((288 - image.width) // 2, (288 - image.height) // 2))
    target = ROOT / 'Textures/Things/Building/ProductionOutputPort/ProductionOutputPort.png'
    target.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(target)
    print('Exported:', target)


if __name__ == '__main__':
    main()
