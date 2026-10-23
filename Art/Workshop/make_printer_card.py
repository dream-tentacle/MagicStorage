"""Render the standalone printer card with the established workshop renderer.

Its approved copy is separate from the grouped building catalog so rerunning
this script never regenerates the existing cards or group images.
"""
import json
import xml.etree.ElementTree as ET
from PIL import Image
from make_building_cards import OUT, ROOT, FINAL_SIZE, load_cards, render_card, save_text


def render(language, copy, pixels_per_unit):
    definition = ET.parse(ROOT/'Defs/ThingDefs/CosmicPrinter.xml').getroot().find('ThingDef')
    graphics = definition.find('graphicData')
    card = dict(copy[language], def_name=copy['defName'], number=13,
                english=definition.findtext('label').upper(), language=language,
                source=ROOT/f'Textures/{graphics.findtext("texPath")}.png',
                layers=[], color=(1,1,1), pixels_per_unit=pixels_per_unit,
                draw_size=tuple(float(value) for value in
                                graphics.findtext('drawSize').strip('()').split(',')))
    png, svg = render_card(card)
    with Image.open(png) as result:
        assert result.size == FINAL_SIZE and result.mode == 'RGBA'
        assert result.getpixel((0,0))[3] == 0
    text = ''.join(node.text or '' for node in
                   ET.parse(svg).getroot().iter('{http://www.w3.org/2000/svg}text'))
    title = card['name'].upper() if language == 'en' else card['name']
    subtitle = 'MAGIC STORAGE' if language == 'en' else card['english']
    assert text == title+subtitle+card['body']+''.join(card['effects'])
    assert len(card['effects']) == 2
    print(f'{language}: {png.name}; {FINAL_SIZE[0]} x {FINAL_SIZE[1]}; copy verified')


def add_index(path, entry):
    content = path.read_text(encoding='utf-8')
    if entry not in content:
        save_text(content.rstrip()+'\n\n'+entry+'\n', path)


def main():
    copy = json.loads((OUT/'cosmic_printer_card.json').read_text(encoding='utf-8'))
    assert copy['standalone'] is True
    # Existing cards already derive their shared scale from the gestator.
    pixels_per_unit = load_cards('zh', 'latest')[0]['pixels_per_unit']
    for language in ('zh', 'en'):
        render(language, copy, pixels_per_unit)
    add_index(OUT/'介绍图索引.md',
              '- [宇宙打印机](宇宙打印机_介绍_v1.png)：单独介绍图，不加入分组长图。')
    add_index(OUT/'English/README.md',
              '- [Cosmic Printer](CosmicPrinter_EN.png): Standalone card, excluded from grouped images.')


if __name__ == '__main__':
    main()
