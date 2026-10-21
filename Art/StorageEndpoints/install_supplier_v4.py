"""Install only the three recolored suppliers, retaining footprint calibration."""
from pathlib import Path
import sys
import hashlib
from PIL import Image

root=Path(__file__).resolve().parent
project=root.parents[1]
sys.path.insert(0,str(root.parent/'BuildingReview'))
from calibrate_textures import calibrated


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


targets=[project/f'Textures/Things/Building/TransportSupplier/TransportSupplier_{direction}.png' for direction in ['south','north','east']]
unchanged={path:digest(path) for path in (project/'Textures').rglob('*.png') if path not in targets}
unchanged.update({path:digest(path) for path in (project/'Defs').rglob('*.xml')})
candidates=[]
for direction,destination in zip(['south','north','east'],targets):
    image=calibrated(root/f'TransportSupplier_{direction}-v4.png',(1,1))
    assert image.size==(288,288)
    assert image.getchannel('A').getbbox()==(16,16,272,272)
    with Image.open(destination) as before:
        assert before.size==image.size
        assert before.getchannel('A').tobytes()==image.getchannel('A').tobytes()
    candidates.append((image,destination))
for image,destination in candidates:
    image.save(destination)
assert all(digest(path)==previous for path,previous in unchanged.items())
print('Installed 3 deep-blue supplier arrows; dimensions, silhouette alpha, centering, other textures and XML unchanged.')
