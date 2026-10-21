"""Anchor each layer to the platform, keeping the raised lid's overhang."""
from pathlib import Path
import json
import math
import xml.etree.ElementTree as ET
from PIL import Image, ImageChops, ImageFilter, ImageOps, ImageStat

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
TARGET = PROJECT / 'Textures/Things/Building/CosmicMechGestator'
TARGET.mkdir(parents=True,exist_ok=True)
PREFIX = 'Things/Building/CosmicMechGestator/CosmicMechGestator'
PPU = 256
# Common source-plane scale for all views. Do not crop/rescale the silhouette.
SX, SZ = 3/640, 2/350
WORLD = (3.625,3.125)
LAYER_NAMES = {'Base':'CosmicMechGestator','Glass':'CosmicMechGestatorGlass','Top':'CosmicMechGestatorTop'}
records = {}

def calibrated(image, center, world):
    width,height = [round(value*PPU) for value in world]
    # Sources are rendered at 2 pixels per SVG unit. Anchor the lower seat
    # directly to the destination center; preserve transparent space above it.
    xscale,zscale = SX*PPU/2,SZ*PPU/2
    return image.transform((width,height),Image.Transform.AFFINE,
        (1/xscale,0,center[0]*2-width/(2*xscale),
         0,1/zscale,center[1]*2-height/(2*zscale)),
        Image.Resampling.BICUBIC,fillcolor=(0,0,0,0))

for direction in ('south','north','east'):
    center = (384,400 if direction=='east' else 275)
    world = WORLD[::-1] if direction=='east' else WORLD
    images = {}
    source_composite = None
    for layer in ('Base','Glass','Top'):
        original = Image.open(ROOT/'GameLayers'/f'{layer}_{direction}.png').convert('RGBA')
        source_composite = original.copy() if source_composite is None else Image.alpha_composite(source_composite,original)
        image = calibrated(original,center,world)
        bounds = image.getchannel('A').getbbox()
        assert bounds and bounds[0]>0 and bounds[1]>0 and bounds[2]<image.width and bounds[3]<image.height
        image.save(TARGET/f'{LAYER_NAMES[layer]}_{direction}.png')
        images[layer] = image
    # Layer separation should reproduce the approved artwork. Differences are
    # limited to antialiasing and the hidden rear outline beneath the cap.
    approved = Image.open(ROOT/f'CosmicMechGestator_{direction}-v6.png').convert('RGBA')
    background = Image.new('RGBA',approved.size,'#d7dce0')
    difference = ImageChops.difference(Image.alpha_composite(background,source_composite),Image.alpha_composite(background,approved))
    error = sum(ImageStat.Stat(difference).mean[:3])/3
    assert error < 1, (direction,error)

    # Native animated scan/mist effects use masks of this cylinder rather than
    # the original large vat. Upper hardware must not receive the glow.
    glass = images['Glass'].getchannel('A').point(lambda value:255 if value>8 else 0)
    visible = ImageChops.multiply(glass,ImageOps.invert(images['Top'].getchannel('A')))
    hard = visible.filter(ImageFilter.GaussianBlur(.65))
    hard = ImageChops.multiply(hard,visible).point(lambda value:round(value*.55))
    soft = visible.filter(ImageFilter.GaussianBlur(7))
    soft = ImageChops.multiply(soft,visible).point(lambda value:round(value*.45))
    for suffix,mask in [('Glow',hard),('GlowSoft',soft)]:
        glow = Image.merge('RGBA',(mask,mask,mask,mask))
        glow.save(TARGET/f'CosmicMechGestator{suffix}_{direction}.png')
    records[direction] = {'world_size':list(world),'png_size':list(images['Base'].size),
        'platform_center_svg':list(center),'platform_center_world':[0,0],
        'layer_reconstruction_mean_error':error}

# Explicit mirrored west assets keep every pass and the shader masks aligned.
for prefix in [*LAYER_NAMES.values(),'CosmicMechGestatorGlow','CosmicMechGestatorGlowSoft']:
    east = Image.open(TARGET/f'{prefix}_east.png').convert('RGBA')
    ImageOps.mirror(east).save(TARGET/f'{prefix}_west.png')
records['west'] = dict(records['east'],mirrored_from='east')

south = [Image.open(TARGET/f'{LAYER_NAMES[layer]}_south.png').convert('RGBA') for layer in ('Base','Glass','Top')]
icon = Image.alpha_composite(Image.alpha_composite(*south[:2]),south[2])
icon = ImageOps.contain(icon.crop(icon.getchannel('A').getbbox()),(480,480),Image.Resampling.LANCZOS)
icon_canvas = Image.new('RGBA',(512,512))
icon_canvas.alpha_composite(icon,((512-icon.width)//2,(512-icon.height)//2))
icon_canvas.save(TARGET/'CosmicMechGestator_MenuIcon.png')

definition_path = PROJECT/'Defs/ThingDefs/CosmicMechGestator.xml'
tree = ET.parse(definition_path)
thing = tree.getroot().find('ThingDef')
def set_text(parent,path,text):
    node = parent.find(path)
    if node is None:
        node = ET.SubElement(parent,path)
    node.text = text

set_text(thing,'size','(3,2)')
set_text(thing,'interactionCellOffset','(1,0,2)')
def graphic(node,texpath):
    set_text(node,'texPath',texpath)
    set_text(node,'graphicClass','Graphic_Multi')
    set_text(node,'drawSize','(3.625,3.125)')
    set_text(node,'color','(1,1,1)')
    set_text(node,'drawRotated','false')
    set_text(node,'allowFlip','true')
    set_text(node,'drawOffset','(0,0,0)')

graphic(thing.find('graphicData'),PREFIX)
set_text(thing.find('graphicData/shadowData'),'volume','(2.8,1.4,1.8)')
set_text(thing,'uiIconPath',PREFIX+'_MenuIcon')
set_text(thing,'uiIconScale','1')
building = thing.find('building')
graphic(building.find('mechGestatorCylinderGraphic'),PREFIX+'Glass')
graphic(building.find('mechGestatorTopGraphic'),PREFIX+'Top')
set_text(building.find('mechGestatorCylinderGraphic'),'shaderType','Transparent')
set_text(building.find('formingGraphicData'),'drawSize','(0.95,0.95)')
set_text(building,'maxFormedMechDrawSize','(0.95,0.95)')
set_text(building,'formingMechYBobDistance','0.04')
for offset in building.find('formingMechPerRotationOffset'):
    offset.text = '(0,0,0)'

bars = {
    'north': ((594.5-384)*SX,(275-122)*SZ,0),
    'south': ((173.5-384)*SX,(275-427)*SZ,180),
    'east': ((569.5-384)*SX,(400-570.5)*SZ,90),
    'west': (-(569.5-384)*SX,(400-570.5)*SZ,270),
}
for direction,(wx,wz,angle) in bars.items():
    radians = math.radians(angle)
    px = math.cos(radians)*wx+math.sin(radians)*wz
    pz = -math.sin(radians)*wx+math.cos(radians)*wz
    node = building.find('barDrawData/'+direction)
    set_text(node,'preRotationOffset',f'({px:.8f},{pz:.8f})')
    set_text(node,'size','(0.52,0.045)')
    # Verify the game's pre-rotation rule returns the monitor center.
    assert abs(math.cos(radians)*px-math.sin(radians)*pz-wx)<1e-8
    assert abs(math.sin(radians)*px+math.cos(radians)*pz-wz)<1e-8
    records[direction]['bar_center_world'] = [wx,wz]
for state in ('Forming','CycleComplete','Formed'):
    for direction in ('north','east','south','west'):
        set_text(building.find('gestator'+state+'Mote'),direction,f'MS_CosmicMechGestator{state}_{direction.capitalize()}')
ET.indent(tree,space='  ')
tree.write(definition_path,encoding='utf-8',xml_declaration=True)

# Keep the native shader, scan animation and maintained-mote lifetime, with
# local masks and a restrained blue color instead of the original red glow.
effects = ET.Element('Defs')
base = ET.SubElement(effects,'ThingDef',{'ParentName':'MechGestatorFormingBase','Name':'MS_CosmicMechGestatorGlowBase','Abstract':'True','MayRequire':'Ludeon.RimWorld.Biotech'})
data = ET.SubElement(base,'graphicData')
set_text(data,'color','(0.35,0.65,0.85,0.4)')
for direction in ('north','east','south','west'):
    cap = direction.capitalize()
    parent_name = f'MS_CosmicMechGestatorForming{cap}'
    node = ET.SubElement(effects,'ThingDef',{'ParentName':'MS_CosmicMechGestatorGlowBase','Name':parent_name,'MayRequire':'Ludeon.RimWorld.Biotech'})
    set_text(node,'defName',f'MS_CosmicMechGestatorForming_{cap}')
    data = ET.SubElement(node,'graphicData')
    set_text(data,'texPath',PREFIX+f'Glow_{direction}')
    world = records[direction]['world_size']
    set_text(data,'drawSize',f'({world[0]},{world[1]})')
    params = ET.SubElement(data,'shaderParameters',{'Inherit':'False'})
    set_text(params,'_MistTex','/Things/Mote/SmokeTiled')
    set_text(params,'_MistScrollSpeed','0.2')
    set_text(params,'_MistOpacityTex','/'+PREFIX+f'GlowSoft_{direction}')
    set_text(params,'_ScanOffset','0')
    set_text(params,'_ScanScale','12.5')
    for state,color in [('CycleComplete','(0.6,0.8,0.95,0.45)'),('Formed','(0.65,0.9,1,0.45)')]:
        child = ET.SubElement(effects,'ThingDef',{'ParentName':parent_name,'MayRequire':'Ludeon.RimWorld.Biotech'})
        set_text(child,'defName',f'MS_CosmicMechGestator{state}_{cap}')
        set_text(ET.SubElement(child,'graphicData'),'color',color)
effects_path = PROJECT/'Defs/Effects/CosmicMechGestatorMotes.xml'
effects_path.parent.mkdir(parents=True,exist_ok=True)
ET.indent(effects,space='  ')
ET.ElementTree(effects).write(effects_path,encoding='utf-8',xml_declaration=True)

# Validate all new local texture and mote references before deployment.
effect_names = {node.findtext('defName') for node in effects if node.find('defName') is not None}
for state in ('Forming','CycleComplete','Formed'):
    for node in building.find('gestator'+state+'Mote'):
        assert node.text in effect_names
for data in [thing.find('graphicData'),building.find('mechGestatorCylinderGraphic'),building.find('mechGestatorTopGraphic')]:
    for direction in ('north','east','south','west'):
        path = PROJECT/'Textures'/(data.findtext('texPath')+'_'+direction+'.png')
        with Image.open(path) as image:
            assert list(image.size) == records[direction]['png_size']
for node in effects.findall('.//texPath')+effects.findall('.//_MistOpacityTex'):
    assert (PROJECT/'Textures'/(node.text.lstrip('/')+'.png')).is_file()
assert thing.findtext('size')=='(3,2)'
assert len(list(TARGET.glob('*.png'))) == 21
for path in (definition_path,effects_path):
    ET.parse(path)
(ROOT/'game-integration-v6.json').write_text(json.dumps({'pixels_per_cell':PPU,'draw_size':WORLD,'source_to_world':[SX,SZ],'views':records,'texture_count':21},ensure_ascii=False,indent=2),encoding='utf-8')
print('Installed 21 PNGs: four directions × base/glass/top/glow/soft mask + menu icon.')
print('Validated XML, local references, unclipped layers, base-center anchors and progress-bar offsets.')
