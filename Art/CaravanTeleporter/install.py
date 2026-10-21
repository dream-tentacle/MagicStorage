"""Install the approved fixed-orientation, footprint-calibrated teleporter art."""
from pathlib import Path
import hashlib
import shutil
import xml.etree.ElementTree as ET
from PIL import Image

root=Path(__file__).resolve().parent
project=root.parents[1]
source=root/'CaravanTeleporter-game-v2.png'
destination=project/'Textures/Things/Building/CaravanTeleporter/CaravanTeleporter.png'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


others={path:digest(path) for path in (project/'Textures').rglob('*.png') if path!=destination}
thing=ET.parse(project/'Defs/ThingDefs/CaravanTeleporter.xml').getroot().find('ThingDef')
assert thing.findtext('defName')=='MS_CaravanTeleporter'
assert thing.findtext('size')=='(1,1)'
assert thing.findtext('rotatable')=='false'
assert thing.findtext('graphicData/graphicClass')=='Graphic_Single'
assert thing.findtext('graphicData/texPath')=='Things/Building/CaravanTeleporter/CaravanTeleporter'
assert thing.findtext('graphicData/drawRotated')=='false'
assert thing.findtext('graphicData/drawOffset')=='(0,0,0)'
assert thing.findtext('graphicData/drawSize')=='(1.125,1.125)'
assert thing.findtext('graphicData/color')=='(1,1,1)'
with Image.open(source) as image:
    assert image.mode=='RGBA'
    assert image.size==(288,288)
    assert image.getchannel('A').getbbox()==(16,16,272,272)
    assert image.getchannel('A').getextrema()==(0,255)
    assert 256/288*1.125==1
destination.parent.mkdir(parents=True,exist_ok=True)
shutil.copy2(source,destination)
assert digest(source)==digest(destination)
assert all(digest(path)==previous for path,previous in others.items())
print('Installed CaravanTeleporter-v2: Graphic_Single, rotation disabled, 288×288 RGBA, exact 1×1 visible footprint, centered.')
print(f'Validated XML and texture reference; {len(others)} other PNG textures unchanged.')
