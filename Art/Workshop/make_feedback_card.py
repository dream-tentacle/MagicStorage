"""Typeset player quotes using the existing workshop card palette (Pillow)."""
from functools import lru_cache
import argparse
import json
from pathlib import Path
import re

from PIL import Image, ImageDraw, ImageFont


OUT = Path(__file__).resolve().parent
SCALE = 2
WIDTH = 1400
COLORS = {
    'frame': '#d5eaf5',
    'title': '#e3f5ff', 'subtitle': '#a9cadc', 'body': '#deedf5',
    'highlight': '#f3ed83', 'rule': '#7d929f',
}


@lru_cache(maxsize=None)
def font(size, emoji=False):
    return ImageFont.truetype(
        'seguiemj.ttf' if emoji else 'HarmonyOS_Sans_SC_Bold.ttf', size*SCALE)


def render(language='zh'):
    is_english = language == 'en'
    copy_path = OUT/('player_feedback_en.json' if is_english else 'player_feedback.json')
    copy = json.loads(copy_path.read_text(encoding='utf-8'))
    assert len(copy['quotes']) == 6
    assert all(isinstance(q['text'], str) and q['text'] for q in copy['quotes']), \
        'Every quote must have its confirmed original text before rendering.'
    measure = ImageDraw.Draw(Image.new('RGB', (1, 1)))

    def advance(char):
        return measure.textlength(char, font=font(27, ord(char) > 0xffff))/SCALE

    def wrap(value, max_width):
        lines, current, width = [], '', 0
        tokens = re.findall(r'\S+\s*', value) if is_english else value
        for char in tokens:
            step = sum(advance(c) for c in char)
            if current and width+step > max_width:
                if not is_english and char in '，。；：、！？”）':
                    lines.append(current[:-1])
                    current = current[-1]+char
                else:
                    lines.append(current)
                    current = char
                width = sum(advance(c) for c in current)
            else:
                current += char
                width += step
        if current:
            lines.append(current)
        assert ''.join(lines) == value
        assert all(sum(advance(c) for c in line) <= max_width for line in lines)
        return lines

    rows = []
    y = 130
    for quote in copy['quotes']:
        lines = wrap(quote['text'], 1112)
        height = 48+len(lines)*43
        rows.append((quote, lines, y, height))
        y += height+20
    height = y+48
    canvas = Image.new('RGBA', (WIDTH*SCALE, height*SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    def line(points, color, width=1):
        draw.line([(round(x*SCALE), round(y*SCALE)) for x, y in points],
                  fill=COLORS.get(color, color), width=round(width*SCALE))

    def text(x, y, value, size, color):
        draw.text((round(x*SCALE), round(y*SCALE)), value, font=font(size),
                  fill=COLORS[color], anchor='lt')

    def centered(value, y, size, spacing, color):
        face = font(size)
        total = sum(draw.textlength(c, font=face)/SCALE for c in value)
        total += spacing*(len(value)-1)
        x = (WIDTH-total)/2
        baseline = round(y*SCALE)-face.getbbox(value, anchor='ls')[1]
        for char in value:
            draw.text((round(x*SCALE), baseline), char, font=face,
                      fill=COLORS[color], anchor='ls')
            x += draw.textlength(char, font=face)/SCALE+spacing
        return total

    centered(copy['title'], 47, 40 if is_english else 48,
             3 if is_english else 6, 'title')

    for quote, lines, top, row_height in rows:
        bottom = top+row_height
        line([(60, top+15), (60, top), (96, top)], 'frame', 2)
        line([(1304, bottom), (1340, bottom), (1340, bottom-15)], 'frame', 2)
        text(85, top+18, '“', 46, 'highlight')

        highlight_positions = set()
        for phrase in quote.get('highlights', []):
            assert phrase in quote['text'], phrase
            start = quote['text'].index(phrase)
            highlight_positions.update(range(start, start+len(phrase)))
        offset = 0
        for row, value in enumerate(lines):
            x = 140
            baseline = top+54+row*43
            for char in value:
                emoji = ord(char) > 0xffff
                color = 'highlight' if offset in highlight_positions or emoji else 'body'
                position = (round(x*SCALE), round(baseline*SCALE))
                face = font(27, emoji)
                bounds = draw.textbbox(position, char, font=face, anchor='ls')
                assert bounds[0] >= 132*SCALE and bounds[2] <= 1260*SCALE
                assert bounds[1] >= (top+14)*SCALE and bounds[3] <= (bottom-14)*SCALE
                draw.text(position, char,
                          font=face, fill=COLORS[color], anchor='ls',
                          embedded_color=emoji)
                x += advance(char)
                offset += 1
        assert offset == len(quote['text'])

    destination = (OUT/'English'/'Groups'/'PlayerFeedback_EN.png' if is_english else
                   OUT/'分组'/'玩家反馈_宣传图_v1.png')
    destination.parent.mkdir(exist_ok=True)
    canvas.save(destination, optimize=True)
    assert canvas.size == (2800, height*2)
    assert canvas.mode == 'RGBA' and canvas.getpixel((0, 0))[3] == 0
    print(f'{destination}\n{canvas.width} x {canvas.height}; six quotes typeset')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--language', choices=('zh', 'en'), default='zh')
    render(parser.parse_args().language)
