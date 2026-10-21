"""Validate all staged art/XML, then copy approved exports into the mod."""
from pathlib import Path
import json
import re
import shutil
import hashlib
import math
import xml.etree.ElementTree as ET
from PIL import Image,ImageChops,ImageFilter,ImageOps,ImageStat,ImageDraw,ImageFont

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent.parent
STAGE=HERE/'GameExport'
manifest=json.loads((STAGE/'manifest.json').read_text(encoding='utf-8'))
PPU=manifest['ppu'];PAD=manifest['padding']
GAP=manifest['inset_pixels']
xml={};outputs=[];report=[]

def load(path):
    with Image.open(path) as image:
        assert image.mode=='RGBA',path
        return image.copy()
def unclipped(image,label):
    l,t,r,b=image.getbbox()
    assert l>0 and t>0 and r<image.width and b<image.height,(label,image.getbbox())
def read_xml(name):
    path=ROOT/'Defs/ThingDefs'/name
    if path not in xml:xml[path]=path.read_bytes().decode('utf-8')
    return path
def graphic_flags(block):
    # Every directional asset now exists explicitly, including west.
    return re.sub(r'<allowFlip>.*?</allowFlip>','<allowFlip>false</allowFlip>',block)
def update_thing(filename,defname,graphic_update):
    path=read_xml(filename);changed=[0]
    def replace(match):
        block=match.group()
        if '<defName>'+defname+'</defName>' not in block:return block
        changed[0]+=1
        return graphic_update(block)
    xml[path]=re.sub(r'<ThingDef\b[^>]*>.*?</ThingDef>',replace,xml[path],flags=re.S)
    assert changed[0]==1,(filename,defname)

for item in manifest['ordinary']:
    image=load(STAGE/item['target'])
    assert list(image.size)==item['size'],item['target']
    unclipped(image,item['target'])
    expected=(PAD+GAP,PAD+GAP,image.width-PAD-GAP,image.height-PAD-GAP)
    assert image.getbbox()==expected,(item['target'],image.getbbox(),expected)
    outputs.append(item['target'])
    report.append(dict(family=item['family'],direction=item['direction'],target=item['target'],
                       size=list(image.size),visible_cells=[(expected[2]-expected[0])/PPU,(expected[3]-expected[1])/PPU],
                       inset_cells=GAP/PPU,adjacent_gap_cells=2*GAP/PPU,center_world=[0,0]))

families=list(dict.fromkeys(item['family'] for item in manifest['ordinary']))
for family in families:
    items=[r for r in manifest['ordinary'] if r['family']==family]
    filename=items[0]['xml']
    reference=next((r for r in items if r['direction'] in ['north','single']),items[0])
    world=reference['world_size']
    size=f'({world[0]:g},{world[1]:g})'
    def update(block,size=size,multi=len(items)>1):
        def data(match):
            data=match.group()
            data=re.sub(r'<drawSize>.*?</drawSize>',f'<drawSize>{size}</drawSize>',data,count=1)
            data=re.sub(r'<color>.*?</color>','<color>(1,1,1)</color>',data,count=1)
            data=re.sub(r'<drawOffset>.*?</drawOffset>','<drawOffset>(0,0,0)</drawOffset>',data,count=1)
            return graphic_flags(data) if multi else data
        return re.sub(r'<graphicData>.*?</graphicData>',data,block,count=1,flags=re.S)
    update_thing(filename,'MS_'+family,update)

prefix='Textures/Things/Building/CosmicMechGestator/CosmicMechGestator'
gestator_report=[]
for item in manifest['gestator']:
    images={name:load(STAGE/path) for name,path in item['layers'].items()}
    for key,image in images.items():
        assert list(image.size)==item['size']
        unclipped(image,item['direction']+key)
    composite=Image.alpha_composite(Image.alpha_composite(images['Base'],images['Glass']),images['Top'])
    reference=load(STAGE/item['reference'])
    background=Image.new('RGBA',reference.size,'#d7dce0')
    diff=ImageChops.difference(Image.alpha_composite(background,composite),Image.alpha_composite(background,reference))
    error=sum(ImageStat.Stat(diff).mean[:3])/3
    assert error<0.35,(item['direction'],error)
    # Restrict native animated mist/scan textures to glass visible in front of
    # forming mechs and behind the lid/manifolds, using the same canvas/anchor.
    glass=images['Glass'].getchannel('A').point(lambda v:255 if v>8 else 0)
    visible=ImageChops.multiply(glass,ImageOps.invert(images['Top'].getchannel('A')))
    for suffix,radius,alpha in [('Glow',.65,.55),('GlowSoft',7,.45)]:
        mask=ImageChops.multiply(visible.filter(ImageFilter.GaussianBlur(radius)),visible)
        mask=mask.point(lambda value:round(value*alpha))
        glow=Image.merge('RGBA',(mask,mask,mask,mask))
        path=prefix+suffix+'_'+item['direction']+'.png'
        glow.save(STAGE/path);outputs.append(path)
    outputs.extend(item['layers'].values())
    gestator_report.append(dict(direction=item['direction'],size=item['size'],world_size=item['world_size'],
                                platform_center_svg=item['center_svg'],platform_center_world=[0,0],
                                visual_scale=item.get('visual_scale',1),
                                floor_clearance_pixels=item['floor_clearance_pixels'],
                                platform_plane_clearance_pixels=item['platform_plane_clearance_pixels'],
                                visible_cells=[(composite.getbbox()[2]-composite.getbbox()[0])/PPU,
                                               (composite.getbbox()[3]-composite.getbbox()[1])/PPU],
                                reconstruction_mean_error=error))
    if item['direction']=='south':
        icon=ImageOps.contain(composite.crop(composite.getbbox()),(480,480),Image.Resampling.LANCZOS)
        canvas=Image.new('RGBA',(512,512));canvas.alpha_composite(icon,((512-icon.width)//2,(512-icon.height)//2))
        icon_path=prefix+'_MenuIcon.png';canvas.save(STAGE/icon_path);outputs.append(icon_path)

def gestator_update(block):
    for tag in ['graphicData','mechGestatorCylinderGraphic','mechGestatorTopGraphic']:
        def change(match):
            value=graphic_flags(match.group())
            value=re.sub(r'<drawSize>.*?</drawSize>','<drawSize>(3.625,3.125)</drawSize>',value,count=1)
            value=re.sub(r'<drawOffset>.*?</drawOffset>','<drawOffset>(0,0,0)</drawOffset>',value,count=1)
            return value
        block,n=re.subn(r'<'+tag+r'>.*?</'+tag+r'>',change,block,count=1,flags=re.S)
        assert n==1,tag
    scale=manifest['gestator'][0].get('visual_scale',1)
    # Use source-space controller centers, not previously scaled XML values,
    # so reinstalling cannot shrink bars or forming mechs a second time.
    document=ET.fromstring(block)
    building=document.find('building')
    original=building.find('barDrawData')
    revised=ET.fromstring(ET.tostring(original,encoding='unicode'))
    controls={
        'north':((594.5-384)*3/640,(275-122)*2/350,0),
        'south':((173.5-384)*3/640,(275-427)*2/350,180),
        'east':((569.5-384)*3/640,(400-570.5)*2/350,90),
        'west':(-(569.5-384)*3/640,(400-570.5)*2/350,270),
    }
    for direction,(x,z,angle) in controls.items():
        x*=scale;z*=scale;radians=math.radians(angle)
        px=math.cos(radians)*x+math.sin(radians)*z
        pz=-math.sin(radians)*x+math.cos(radians)*z
        node=revised.find(direction)
        node.find('preRotationOffset').text=f'({px:.8f},{pz:.8f})'
        node.find('size').text=f'({.52*scale:g},{.045*scale:g})'
    replacement=ET.tostring(revised,encoding='unicode').rstrip()
    block=re.sub(r'<barDrawData>.*?</barDrawData>',lambda _:replacement,block,count=1,flags=re.S)
    block=re.sub(r'(<formingGraphicData>.*?<drawSize>).*?(</drawSize>)',
                 lambda m:m[1]+f'({.95*scale:g},{.95*scale:g})'+m[2],block,count=1,flags=re.S)
    block=re.sub(r'<maxFormedMechDrawSize>.*?</maxFormedMechDrawSize>',
                 f'<maxFormedMechDrawSize>({.95*scale:g},{.95*scale:g})</maxFormedMechDrawSize>',block,count=1)
    block=re.sub(r'<formingMechYBobDistance>.*?</formingMechYBobDistance>',
                 f'<formingMechYBobDistance>{.04*scale:g}</formingMechYBobDistance>',block,count=1)
    block=re.sub(r'(<shadowData>.*?<volume>).*?(</volume>)',
                 lambda m:m[1]+f'({2.8*scale:g},{1.4*scale:g},{1.8*scale:g})'+m[2],block,count=1,flags=re.S)
    return block
update_thing('CosmicMechGestator.xml','MS_CosmicMechGestator',gestator_update)

# Validate references and source-sensitive facts before modifying workspace XML.
for path,value in xml.items():
    tree=ET.fromstring(value)
    for definition in tree.findall('ThingDef'):
        name=definition.findtext('defName')
        if name not in {'MS_'+f for f in families}|{'MS_CosmicMechGestator'}:continue
        graphic=definition.find('graphicData')
        suffixes=['_north','_south','_east','_west'] if graphic.findtext('graphicClass')=='Graphic_Multi' else ['']
        for suffix in suffixes:
            assert (STAGE/('Textures/'+graphic.findtext('texPath')+suffix+'.png')).is_file(),name+suffix
        if name=='MS_CosmicMechGestator':
            assert definition.findtext('size')=='(3,2)'
            for tag in ['mechGestatorCylinderGraphic','mechGestatorTopGraphic']:
                layer=definition.find('building/'+tag)
                assert layer.findtext('drawSize')==graphic.findtext('drawSize')
                for suffix in suffixes:assert (STAGE/('Textures/'+layer.findtext('texPath')+suffix+'.png')).is_file()
effects=ET.parse(ROOT/'Defs/Effects/CosmicMechGestatorMotes.xml').getroot()
for node in effects.findall('.//texPath')+effects.findall('.//_MistOpacityTex'):
    assert (STAGE/('Textures/'+node.text.lstrip('/')+'.png')).is_file(),node.text
assert len(outputs)==44 and len(set(outputs))==44,len(outputs)

# Save a review at the exact game texture scale before deployment.
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
sheet=Image.new('RGB',(1440,2300),'#303639');draw=ImageDraw.Draw(sheet)
draw.text((20,14),'游戏贴图导出校准 · 每格 80 像素 · 蓝框为占地',font=font,fill='#eef2f3')
preview=[*manifest['ordinary'],*[
    dict(family='CosmicMechGestator',direction=r['direction'],footprint=[2,3] if r['direction'] in ['east','west'] else [3,2],
         target=r['reference'],world_size=r['world_size']) for r in manifest['gestator']]]
for i,item in enumerate(preview):
    x=16+(i%4)*356;y=55+(i//4)*318
    draw.text((x+6,y),item['family']+' / '+item['direction'],font=font,fill='#eef2f3')
    draw.rectangle((x,y+30,x+340,y+307),fill='#858a80')
    image=load(STAGE/item['target'])
    scaled=image.resize((round(item['world_size'][0]*80),round(item['world_size'][1]*80)),Image.Resampling.LANCZOS)
    cx=x+170;cy=y+170
    sheet.paste(scaled,(cx-scaled.width//2,cy-scaled.height//2),scaled)
    cw,ch=[round(c*80) for c in item['footprint']]
    draw.rectangle((cx-cw//2,cy-ch//2,cx+cw//2,cy+ch//2),outline='#69bddc',width=1)
sheet.save(STAGE/'GameIntegration-review.png')

# Only after all images, partitions and references pass, install to workspace.
for target in outputs:
    destination=ROOT/target;destination.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(STAGE/target,destination)
for path,value in xml.items():
    path.write_bytes(value.encode('utf-8'))
for target in outputs:
    assert hashlib.sha256((ROOT/target).read_bytes()).digest()==hashlib.sha256((STAGE/target).read_bytes()).digest()
for path in xml:ET.parse(path)
(STAGE/'integration-report.json').write_text(json.dumps(dict(texture_count=len(outputs),pixels_per_cell=PPU,
    outline_pixels=manifest['outline_pixels'],ordinary=report,gestator=gestator_report,
    xml_files=[str(p.relative_to(ROOT)) for p in xml],outputs=outputs),ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Installed and validated {len(outputs)} PNGs for 12 buildings, updated {len(xml)} XML files.')
print('All ordinary views have real inside-footprint clearance and zero center offset; all 4 gestator layer reconstructions passed.')
