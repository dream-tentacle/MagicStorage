from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent
sheet = Image.new('RGB', (1536, 870), '#252b30')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 28)
label = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 23)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
draw.text((28,20), '宇宙培育器 · 三视图', font=font, fill='#e4edf3')
draw.text((28,65), '保持柱体尺寸与高度一致；承座落在各方向的平台中央', font=small, fill='#b2c7d4')
views = [('south','南向 · 3 × 2',275), ('north','北向 · 3 × 2',275), ('east','侧向 · 2 × 3',400)]
for index, (direction, title, anchor_y) in enumerate(views):
    x0 = index*512
    draw.text((x0+28,117), title, font=label, fill='#e4edf3')
    for y in range(160,600,20):
        for x in range(x0+12,x0+500,20):
            color = '#e5e7e5' if ((x-x0-12)//20+(y-160)//20)%2 else '#f0f2ef'
            draw.rectangle((x,y,min(x+19,x0+499),y+19),fill=color)
    sprite = Image.open(root/f'CosmicMechGestator_{direction}-v6.png').convert('RGBA')
    assert sprite.size == (1536,1536 if direction == 'east' else 1024)
    assert sprite.getchannel('A').getextrema() == (0,255)
    bounds = sprite.getchannel('A').getbbox()
    assert bounds[0] > 0 and bounds[1] > 0 and bounds[2] < sprite.width and bounds[3] < sprite.height
    # All columns use the same scale, with lower vessel centers on one line.
    image = sprite.resize((480,480 if direction == 'east' else 320),Image.Resampling.LANCZOS)
    y0 = round(350-anchor_y*.625)
    sheet.paste(image,(x0+16,y0),image)
    small_image = sprite.resize((192,192 if direction == 'east' else 128),Image.Resampling.LANCZOS)
    sheet.paste(small_image,(x0+160,round(762-anchor_y*.25)),small_image)
draw.text((28,627), '缩小显示', font=label, fill='#e4edf3')
draw.text((28,835), '南向保留已确认版本；侧向用于东向，西向可镜像。当前为设计稿，尚未替换游戏贴图。',font=small,fill='#b2c7d4')
output = root/'CosmicMechGestator-directions-review-v6.png'
sheet.save(output)
print('Three SVG views rendered; transparent PNGs have unclipped bounds; shared vessel scale preserved.')
print(output)
