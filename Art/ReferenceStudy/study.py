from pathlib import Path
from PIL import Image, ImageDraw
import json
import math

BASE = Path(r'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100')
OUTPUT = Path(__file__).resolve().parent
GROUPS = [
    ('SBZ / machinery and refrigeration', '3668590852', [
        'sbz_WastepackAtomizer_south.png', 'sbz_WastepackAtomizer_north.png',
        'sbz_WastepackAtomizer_east.png', 'sbz_WastepackAtomizerUI.png']),
    ('SBZ / machinery and refrigeration', '3486264784', [
        'sbz_CorpseFridge.png', 'sbz_DoubleChestFridge_south.png',
        'sbz_DoubleWallFridge_south.png', 'sbz_FridgeUI.png',
        'sbz_LargeFridgeUI.png', 'sbz_SingleChestFridge_south.png',
        'sbz_SingleWallFridge_south.png', 'sbz_LargeFridge_south.png']),
    ('Daily furniture / 16 varied objects', '3024562670', [
        'ADH_bed_double_A_south.png', 'ADH_bed_Single_B_south.png',
        'ADH_cabinet_A_south.png', 'ADH_cabinet_B_south.png',
        'ADH_cabinet_C_south.png', 'ADH_cabinet_D_south.png',
        'ADH_chair_A_south.png', 'ADH_chair_D_south.png',
        'ADH_Cooler_A_south.png', 'ADH_TV_A_south.png',
        'ADH_table_A2x2_south.png', 'ADH_table_B2x2.png',
        'ADH_table_lamp_A_south.png', 'ADH_StreetLamp.png',
        'ADH_partition_A_south.png', 'ADH_TableStyling_A_south.png']),
    ('Space Base / furniture and modules', '3523916780', [
        'MUS_SpaceBase_Bed_south.png', 'MUS_SpaceBase_BedV_south.png',
        'MUS_SpaceBase_Cabinet_south.png', 'MUS_SpaceBase_CabinetV_south.png',
        'MUS_SpaceBase_Bookcase_south.png', 'MUS_SpaceBase_Lockers_south.png',
        'MUS_SpaceBase_SchoolDesk_south.png', 'MUS_SpaceBase_HolographicInstrument_south.png',
        'MUS_SpaceBase_BigTelevision_south.png', 'MUS_SpaceBase_FloorLamp_south.png',
        'MUS_SpaceBase_CoralSpecimens_south.png', 'MUS_SpaceBase_BigTable_south.png',
        'MUS_SpaceBase_CompactBattery.png', 'MUS_SpaceBase_MachineryModule_south.png',
        'MUS_SpaceBase_ConnectModule_south.png', 'MUS_SpaceBase_SolarGeneratorModule_south.png']),
    ('UF Light Industries / machinery and storage', '3651799915', [
        'UFLI_3dprinter_south.png', 'UFLI_3dprinter_gantry_south.png',
        'UFLI_Robotarm_south.png', 'UFLI_Brainwasher_south.png',
        'UFLI_InfiniteNutrientPasteDispenser_south.png', 'UFLI_Washingmachine_south.png',
        'UFLI_Butcher_south.png', 'UFLI_Sink_south.png',
        'UFLI_SteamOven_south.png', 'UFLI_stove_south.png',
        'UFLI_Shelf_south.png', 'UFLI_Bookcase_south.png',
        'UFLI_Refrigerator_AS_Base_south.png', 'UFLI_Garbagebin_AS_south.png',
        'UFLI_Wardrobe_AS_Base_south.png', 'UFLI_Container_1X2_AS_Base_south.png']),
    ('UF Light Industries / furniture and devices', '3651799915', [
        'UFLI_Bed_south.png', 'UFLI_4PBed_south.png',
        'UFLI_CabinetA_south.png', 'UFLI_CabinetF_south.png',
        'UFLI_CouchDouble_south.png', 'UFLI_Beach_lounger_south.png',
        'UFLI_desk_south.png', 'UFLI_2X2_Table.png',
        'UFLI_ChessTable.png', 'UFLI_Telescope.png',
        'UFLI_Radar_south.png', 'UFLI_CeleTech.png',
        'UFLI_Decoration_Big_Holographic_south.png', 'UFLI_HolographicBase.png',
        'UFLI_ArchonexushFountain.png', 'UFLI_AGravefountain.png']),
]

pages = {}
manifest = []
for title, mod_id, names in GROUPS:
    index = {}
    for path in (BASE / mod_id / 'Textures').rglob('*.png'):
        index.setdefault(path.name, []).append(path)
    for name in names:
        matches = index.get(name, [])
        if len(matches) != 1:
            raise ValueError(f'{mod_id}: {name}: {len(matches)} matches')
        path = matches[0]
        sprite = Image.open(path).convert('RGBA')
        bbox = sprite.getchannel('A').getbbox()
        if bbox is None:
            raise ValueError(f'Empty sprite: {path}')
        item = {'title': title, 'mod': mod_id, 'name': name, 'path': str(path),
                'size': sprite.size, 'content_bbox': bbox}
        manifest.append(item)
        pages.setdefault(title, []).append((item, sprite.crop(bbox)))

for page_num, (title, items) in enumerate(pages.items(), 1):
    width, height = 1200, 52 + math.ceil(len(items) / 4) * 266
    sheet = Image.new('RGB', (width, height), '#353a3d')
    draw = ImageDraw.Draw(sheet)
    draw.text((16, 16), f'{page_num:02d}  {title}  /  {len(items)} references', fill='#eef3f5')
    for i, (item, sprite) in enumerate(items):
        x, y = (i % 4) * 300, 52 + (i // 4) * 266
        draw.rectangle((x + 7, y + 5, x + 291, y + 227), fill='#bdc2c3')
        sprite.thumbnail((268, 206), Image.Resampling.LANCZOS)
        px, py = x + 150 - sprite.width // 2, y + 116 - sprite.height // 2
        sheet.paste(sprite, (px, py), sprite)
        label = item['name'].removesuffix('.png')
        if len(label) > 44:
            label = label[:42] + '..'
        draw.text((x + 10, y + 234), label, fill='#e1eaee')
        draw.text((x + 10, y + 248), f"{item['mod']} | {item['size'][0]}x{item['size'][1]}", fill='#a2c6d9')
    destination = OUTPUT / f'references-{page_num:02d}.png'
    sheet.save(destination)
    print(f'{destination} ({len(items)} images)')
(OUTPUT / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print(f'{len(manifest)} references from 5 mods')
