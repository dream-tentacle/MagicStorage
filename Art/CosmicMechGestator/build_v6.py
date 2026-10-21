from pathlib import Path
import math
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
source = (root / 'CosmicMechGestator_south-v5.svg').read_text(encoding='utf-8')
source = source.replace('南向第五稿', '南向第六稿')
source = source.replace(
    '<desc>' + source.split('<desc>', 1)[1].split('</desc>', 1)[0] + '</desc>',
    '<desc>South-facing 3×2 gestator study. The platform top center and the lower vessel seat share the anchor (384,275). The elevated lid projects north naturally. Sky-blue accents and a uniform black exterior contour.</desc>')
start = source.index('    <!-- All circular tank parts')
end = source.index('  </g>', start)
source = source[:start] + '    <!-- The lower seat is anchored to the platform top center (384,275). -->\n    <g id="vessel-seat" transform="translate(0 -27)">\n' + source[start:end] + '    </g>\n' + source[end:]
source = source.replace('<g id="cylinder-glass"', '<g transform="translate(0 -27)" id="cylinder-glass"')
source = source.replace('<g id="gestator-top">', '<g id="gestator-top" transform="translate(0 -27)">')
source = source.replace('L271 220', 'L271 193').replace('L271 336', 'L271 309')
source = source.replace('L497 220', 'L497 193').replace('L497 336', 'L497 309')
source = source.replace('L271 215', 'L271 188').replace('L271 331', 'L271 304')
source = source.replace('L497 215', 'L497 188').replace('L497 331', 'L497 304')
# The exterior follows the raised lid where it crosses the rear platform edge.
dx = 132 * math.sqrt(1 - ((100 - 115) / 92.4) ** 2)
source = source.replace('H266.4A132 92.4 0 0 1 501.6 100', f'H{384-dx:.4f}A132 92.4 0 0 1 {384+dx:.4f} 100')
output = root / 'CosmicMechGestator_south-v6.svg'
output.write_text(source, encoding='utf-8')
ET.parse(output)
assert (64 + 640 / 2, 100 + 350 / 2) == (384, 302 - 27)
print(output)
