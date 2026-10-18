"""Install only a 1024×1024 core, preserving its calibrated normalized bounds."""
from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image

art = Path(__file__).resolve().parent
project = art.parents[1]
destination = project / 'Textures/Things/Building/StorageCore/StorageCore.png'
output = art / 'StorageCore-test1024.png'
backup = art / 'StorageCore-before-test1024.png'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


# Capture every other deployed texture and all XML before making the single-file change.
unchanged = {path: digest(path) for path in (project/'Textures').rglob('*.png') if path != destination}
unchanged.update({path: digest(path) for path in (project/'Defs').rglob('*.xml')})
with Image.open(destination) as reference:
    original_size = reference.size
    l,t,r,b = reference.getchannel('A').getbbox()
    fractions = ((r-l)/reference.width, (b-t)/reference.height)
    offsets = ((l+r)/2/reference.width-.5, (t+b)/2/reference.height-.5)
    assert offsets == (0.0,0.0), offsets

if not backup.exists():
    shutil.copy2(destination, backup)

with Image.open(art/'StorageCore-v10-render2048.png') as rendered:
    assert rendered.mode == 'RGBA'
    body = rendered.crop(rendered.getchannel('A').getbbox())
    # An even pixel count keeps the visible body exactly centered on the even canvas.
    body_size = tuple(2*round(1024*fraction/2) for fraction in fractions)
    body = body.resize(body_size, Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', (1024,1024), (0,0,0,0))
    padding = tuple((1024-value)//2 for value in body_size)
    canvas.alpha_composite(body,padding)
    expected = (padding[0],padding[1],padding[0]+body_size[0],padding[1]+body_size[1])
    assert canvas.getchannel('A').getbbox() == expected
    canvas.save(output)
shutil.copy2(output,destination)

with Image.open(destination) as installed:
    assert installed.size == (1024,1024)
    assert installed.mode == 'RGBA'
    assert installed.getchannel('A').getextrema() == (0,255)
    assert installed.getchannel('A').getbbox() == expected
    for actual,wanted in zip(body_size,fractions):
        assert abs(actual/1024-wanted) <= 1/1024
assert all(digest(path)==previous for path,previous in unchanged.items())
report = dict(original_size=original_size,test_size=[1024,1024],body_bounds=expected,center_offset=[0,0],source='StorageCore-v10.svg',other_textures_and_xml_unchanged=True)
(art/'StorageCore-test1024.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(f'Installed core {original_size} -> 1024×1024 RGBA; body={expected}; center=(0,0).')
print(f'Confirmed {len(unchanged)} other texture/XML files unchanged.')
