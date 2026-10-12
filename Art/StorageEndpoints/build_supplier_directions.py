from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
source = (root / 'TransportSupplier_south-v3.svg').read_text(encoding='utf-8')
start = source.index('  <!-- Rounded directional channels')
end = source.index('  <rect x="204" y="468"', start)

channel = 'M256 179V275M184 274L244 330Q256 342 268 330L328 274'
for direction, label, angle in [('north', '北', 180), ('east', '东', -90)]:
    rotation = f'rotate({angle} 256 262)'
    # Rotate the operating surface only; casing depth and lighting stay consistent.
    details = f'''  <!-- Recessed guide channel and transfer seam, oriented {direction}. -->
  <g transform="translate(0 3)">
    <path d="{channel}" transform="{rotation}" fill="none" stroke="#f4f7f8" stroke-width="23" stroke-linecap="round" stroke-linejoin="round"/>
  </g>
  <path d="{channel}" transform="{rotation}" fill="none" stroke="#96adbd" stroke-width="21" stroke-linecap="round" stroke-linejoin="round"/>
  <g transform="translate(0 2)">
    <path d="{channel}" transform="{rotation}" fill="none" stroke="#c5e5f5" stroke-width="13" stroke-linecap="round" stroke-linejoin="round"/>
  </g>
  <path d="M256 184V269" transform="{rotation}" stroke="#e4f3fb" stroke-width="3" stroke-linecap="round"/>
  <g transform="{rotation}">
    <rect x="194" y="370" width="124" height="12" rx="6" fill="#adbdc8"/>
    <rect x="202" y="372" width="108" height="5" rx="2.5" fill="#617d90"/>
    <path d="M208 380H304" stroke="#f1f5f7" stroke-width="2" stroke-linecap="round"/>
  </g>

'''
    result = (source[:start] + details + source[end:]).replace('南向第三稿', label + '向第三稿')
    result = result.replace('pointing towards its front', 'pointing ' + direction)
    ET.fromstring(result)
    destination = root / f'TransportSupplier_{direction}-v3.svg'
    destination.write_text(result, encoding='utf-8')
    print(destination)
