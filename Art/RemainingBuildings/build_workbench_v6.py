from pathlib import Path
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
ns='http://www.w3.org/2000/svg'
ET.register_namespace('',ns)
tree=ET.parse(root/'CosmicWorkbench_north-v5.svg')
svg=tree.getroot()
svg.find(f'{{{ns}}}title').text='魔法存储 — 宇宙工作台，第六稿'
svg.find(f'{{{ns}}}desc').text=(
    'Original 3×1 assembly workbench study informed by the broad value grouping and '
    'functional equipment details of the user-selected UF Light Industries references. '
    'A vise mounted on an integrated utility housing, a slotted assembly plate, '
    'a recessed hand-tool well and a shallow operator-side trapezoid opening. '
    'Sky-blue accent RGB(197,229,245), flat grey metal and uniform black exterior contour. '
    'No external mod artwork is embedded. Transparent draft, not installed.')
vise=next(element for element in svg if element.get('id')=='bench-vise')
for element in list(svg):
    if element.get('id') in ('assembly-surface','bench-vise','flush-controls'):
        svg.remove(element)

def add(text):
    svg.append(ET.fromstring(f'<g xmlns="{ns}">{text}</g>'))

add('''
  <g id="rear-backstop">
    <path d="M230 87H541V102H230Z" fill="#7e909b"/>
    <path d="M230 87H541V93H230Z" fill="#b9c4ca"/>
    <path d="M230 99H541V102H230Z" fill="#607784"/>
  </g>
  <g id="vise-utility-housing">
    <path d="M87 94H224V211H87Z" fill="#b7bfc2"/>
    <path d="M87 90H224V99H87Z" fill="#dce1de"/>
    <path d="M218 99H224V211H218Z" fill="#8e9fa8"/>
    <path d="M87 196H224V211H87Z" fill="#97a6ae"/>
    <path d="M102 201H110V206H102ZM115 201H123V206H115ZM128 201H136V206H128Z" fill="#596f7d"/>
    <path d="M100 105H126V111H100Z" fill="#c5e5f5"/>
  </g>
  <g id="slotted-assembly-plate">
    <path d="M249 112H522L530 120V198H241V120Z" fill="#7d8f99"/>
    <path d="M251 115H520L526 122V190H245V122Z" fill="#bcc6ca"/>
    <path d="M245 190H526V198H245Z" fill="#8c9da7"/>
    <path d="M265 130H506V134H265ZM265 154H506V158H265ZM265 178H506V182H265Z" fill="#657b89"/>
    <path d="M265 134H506V136H265ZM265 158H506V160H265ZM265 182H506V184H265Z" fill="#d7dfe0"/>
  </g>
  <g id="tool-well">
    <path d="M566 117H663V196H558V125Z" fill="#9baab2"/>
    <path d="M568 121H659V188H562V127Z" fill="#b9c4c8"/>
    <path d="M562 188H659V196H562Z" fill="#8197a4"/>
    <g transform="rotate(-16 610 155)">
      <path d="M595 128H602V138L610 142L618 138V128H625V141L617 149V176Q617 181 612 181Q607 181 607 176V149L595 141Z" fill="#6c8290"/>
      <path d="M610 152H613V174H610Z" fill="#a6b7c0"/>
    </g>
    <path d="M642 131H648V174L645 181L642 174Z" fill="#637e8f"/>
    <path d="M642 137H648V150H642Z" fill="#c5e5f5"/>
  </g>
''')
svg.append(vise)
ET.indent(tree,space='  ')
tree.write(root/'CosmicWorkbench_north-v6.svg',encoding='utf-8')

export=(root/'export_workbench_v5.cjs').read_text(encoding='utf-8')
(root/'export_workbench_v6.cjs').write_text(export.replace('north-v5','north-v6'),encoding='utf-8')
preview=(root/'preview_workbench_v5.py').read_text(encoding='utf-8')
preview=preview.replace('north-v5','north-v6').replace('review-v5','review-v6').replace('Workbench-v5.json','Workbench-v6.json')
preview=preview.replace('宇宙工作台 · 操作区调整','宇宙工作台 · 装配台面方案')
preview=preview.replace('取消立式显示器 · 嵌入式机械拨杆 · 保留中央梯形凹口','整合式台钳底座 · 带导槽的装配板 · 嵌入式工具位')
preview=preview.replace("'controls':'flush mechanical sliders, no monitor'", "'controls':'mechanical vise and recessed hand tools, no monitor'")
(root/'preview_workbench_v6.py').write_text(preview,encoding='utf-8')
print(root/'CosmicWorkbench_north-v6.svg')
