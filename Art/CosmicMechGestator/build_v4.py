"""Balance the 3×2 study's cylinder height without changing its platform layout."""
from pathlib import Path
from copy import deepcopy
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tag=lambda value:'{'+ns+'}'+value
svg=ET.parse(root/'CosmicMechGestator_south-v3.svg').getroot()
svg.find(tag('title')).text='魔法存储 — 宇宙机械体培育器，3×2 南向第四稿'
svg.find(tag('desc')).text='Three cells horizontally and two in depth. A rounded, moderately raised lid and a visibly deeper glass vessel balance the low wide platform. Pearl-grey metal and sky-blue accents. Base layout and south-facing controls retain the previous design.'


def child(parent,name,**attrs):
    return ET.SubElement(parent,tag(name),{key.replace('_','-'):str(value) for key,value in attrs.items()})


def path(parent,d,fill='none',stroke=None,width=2):
    attrs=dict(d=d,fill=fill)
    if stroke:
        attrs.update(stroke=stroke,stroke_width=width,stroke_linecap='round',stroke_linejoin='round')
    return child(parent,'path',**attrs)


# A more circular cap and a visible short side face replace the flattened ellipse.
lid=svg.find(".//*[@id='gestator-top']")
lid.clear()
lid.set('id','gestator-top')
path(lid,'M266 115V129C266 182 319 225 384 225C449 225 502 182 502 129V115Z','#91aabd')
path(lid,'M280 165C322 218 446 218 488 165V175C446 225 322 225 280 175Z','#c5e5f5')
child(lid,'ellipse',cx=384,cy=115,rx=118,ry=96,fill='url(#lid-metal)')
path(lid,'M283 98C304 54 344 29 375 27',stroke='#f4f8fa',width=3)
path(lid,'M321 190Q384 219 447 190',stroke='#95afc2',width=2.5)
child(lid,'rect',x=350,y=88,width=68,height=32,rx=8,fill='#b5c9d8')
child(lid,'rect',x=354,y=91,width=60,height=25,rx=6,fill='#dce9f1')
path(lid,'M366 100H401',stroke='#9bb5c9',width=3)
path(lid,'M370 110H397',stroke='#c5e5f5',width=4)

# Slightly narrower, longer glass walls provide vertical volume below the rounded lid.
glass=svg.find(".//*[@id='cylinder-glass']")
glass.set('transform','translate(384 0) scale(.94 1) translate(-384 0)')
shape='M263 148C263 94 505 94 505 148V311C505 402 263 402 263 311Z'
clip=svg.find(".//*[@id='tank-body-clip']/"+tag('path'))
clip.set('d',shape)
list(glass)[0].set('d',shape)
list(glass)[1].set('d','M269 262C269 217 499 217 499 262V312C499 388 269 388 269 312Z')
for node in glass.iter(tag('path')):
    if node.get('d','').startswith('M285 224'):
        node.set('d','M285 224V307Q285 341 305 352')
    elif node.get('d','').startswith('M486 224'):
        node.set('d','M486 224V310Q486 343 466 354')

# Lower the circular seat just enough to expose the deeper glass, retaining the console.
base=svg.find(".//*[@id='gestator-base']")
nodes=list(base)
start=next(i for i,node in enumerate(nodes) if node.tag==tag('ellipse') and node.get('cy')=='342')
seat=ET.Element(tag('g'),{'id':'vessel-seat','transform':'translate(0 8)'})
for node in nodes[start:]:
    seat.append(deepcopy(node))
    base.remove(node)
base.append(seat)

# Follow the rounded cap only where it extends above the rear platform edge.
outside=list(svg)[-1]
assert outside.tag==tag('path') and outside.get('stroke')=='#000000'
outside.set('d','M56 84H272.32A118 96 0 0 1 495.68 84H712Q732 84 732 104V456Q732 476 712 476H56Q36 476 36 456V104Q36 84 56 84Z')
ET.indent(svg,space='  ')
destination=root/'CosmicMechGestator_south-v4.svg'
destination.write_text(ET.tostring(svg,encoding='unicode')+'\n',encoding='utf-8')
print(destination)
