from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
source = root / 'StorageCore-v8.svg'
text = source.read_text(encoding='utf-8')
contour_colours = {'#26313b', '#293641', '#25313c', '#b5c2ca', '#bdc8cf',
                   '#a9c4d2', '#526f80', '#7893a4'}

def uniform_contour(match):
    tag = match.group()
    stroke = re.search(r'stroke="([^"]+)"', tag)
    if stroke and stroke.group(1).lower() in contour_colours:
        tag = re.sub(r'stroke="[^"]+"', 'stroke="#000000"', tag)
        tag = re.sub(r'stroke-width="[^"]+"', 'stroke-width="4"', tag)
    return tag

text = re.sub(r'<(?:rect|path|circle|ellipse|g)\b[^>]*>', uniform_contour, text)
text = text.replace('rx="4.5" fill="#344756"/>', 'rx="4.5" fill="#344756" stroke="#000000" stroke-width="4"/>')
text = text.replace('rx="11" fill="#aebdc6"/>', 'rx="11" fill="#aebdc6" stroke="#000000" stroke-width="4"/>')
text = text.replace('    <path d="M256 185L189 292H323Z" fill="none" stroke="#b1bfc8" stroke-width="9" stroke-linejoin="round"/>',
                    '    <path d="M256 185L189 292H323Z" fill="none" stroke="#000000" stroke-width="17" stroke-linejoin="round"/>\n'
                    '    <path d="M256 185L189 292H323Z" fill="none" stroke="#b1bfc8" stroke-width="9" stroke-linejoin="round"/>')
text = text.replace('rx="60" ry="20" fill="#b4c3cd"/>', 'rx="60" ry="20" fill="#b4c3cd" stroke="#000000" stroke-width="4"/>')
text = text.replace('rx="57" ry="18" fill="#49657a"/>', 'rx="57" ry="18" fill="#49657a" stroke="#000000" stroke-width="4"/>')
text = text.replace('    <circle cx="256" cy="246" r="48" fill="#48687d" stroke="#243e50" stroke-width="2"/>\n'
                    '    <circle cx="256" cy="244" r="46" fill="url(#nucleus)"/>',
                    '    <circle cx="256" cy="244" r="48" fill="url(#nucleus)" stroke="#000000" stroke-width="4"/>')
ET.fromstring(text)
source.write_text(text, encoding='utf-8')
print('Structural contours: pure black, 4 SVG units; rail borders: 4 units on each side.')
