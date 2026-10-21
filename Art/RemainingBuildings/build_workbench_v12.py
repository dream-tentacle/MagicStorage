from pathlib import Path
import math
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v11.svg')
svg=tree.getroot()
face=next(e for e in svg.iter() if e.get('d')=='M316 232L338 210V228L316 250Z')
# Align to the visible inner corners of the 8-unit outline, rather than its
# centerline. Both vertices receive the same X translation, preserving shape.
shift=(8/2)*(math.sqrt(2)-1)
x0,x1=316-shift,338-shift
face.set('d',f'M{x0:.7f} 232L{x1:.7f} 210V228L{x0:.7f} 250Z')
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v12.svg',encoding='utf-8')

export=(root/'export_workbench_v11.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v12.cjs').write_text(export.replace('north-v11','north-v12'),encoding='utf-8')
preview=(root/'preview_workbench_v11.py').read_text(encoding='utf-8')
preview=preview.replace('north-v11','north-v12').replace('review-v11','review-v12').replace('Workbench-v11.json','Workbench-v12.json')
preview=preview.replace('north-v8.png','north-v11.png')
preview=preview.replace('宇宙工作台 · 按红框补齐左侧斜面','宇宙工作台 · 斜面横坐标对齐')
preview=preview.replace('左侧浅色面补至原深色斜边上沿；其余部分恢复第八稿','仅将左侧浅色面水平左移，对齐黑描边内侧拐点；高度与形状保留')
preview=preview.replace('628<=changed[0]','624<=changed[0]')
preview=preview.replace('left face only, following the user red outline','left face translated horizontally to align the visible contour corners')
(root/'preview_workbench_v12.py').write_text(preview,encoding='utf-8')
print(f'Left face X translation: {-shift:.7f}; visible corner X coordinates: {x0:.7f}, {x1:.7f}. Y coordinates unchanged.')
