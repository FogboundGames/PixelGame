import re

with open(r'Assets\Scenes\Gemi.unity', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

# Let's find all transforms and their hierarchy to compute world positions
transforms = {}
game_objects = {}

for m in re.finditer(r'--- !u!1 &(\d+)\nGameObject:(.*?)(?=\n---|\Z)', text, re.DOTALL):
    go_id = m.group(1)
    name_m = re.search(r'm_Name: (.*)', m.group(2))
    name = name_m.group(1).strip() if name_m else 'unknown'
    game_objects[go_id] = name

for m in re.finditer(r'--- !u!(?:4|224) &(\d+)\n(?:Transform|RectTransform):(.*?)(?=\n---|\Z)', text, re.DOTALL):
    tr_id = m.group(1)
    block = m.group(2)
    go_m = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', block)
    go_id = go_m.group(1) if go_m else None
    fa_m = re.search(r'm_Father: \{fileID: (-?\d+)\}', block)
    father_id = fa_m.group(1) if fa_m else '0'
    
    pos_m = re.search(r'm_LocalPosition: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}', block)
    pos = (float(pos_m.group(1)), float(pos_m.group(2)), float(pos_m.group(3))) if pos_m else (0,0,0)

    scale_m = re.search(r'm_LocalScale: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}', block)
    scale = (float(scale_m.group(1)), float(scale_m.group(2)), float(scale_m.group(3))) if scale_m else (1,1,1)

    transforms[tr_id] = {'go_id': go_id, 'father': father_id, 'pos': pos, 'scale': scale}

def get_world_pos(tr_id):
    if tr_id not in transforms: return (0,0,0)
    pos = transforms[tr_id]['pos']
    father = transforms[tr_id]['father']
    if not father or father == '0':
        return pos
    f_pos = get_world_pos(father)
    f_scale = transforms[father]['scale']
    # (assuming no rotation for simple hierarchy)
    return (f_pos[0] + pos[0] * f_scale[0], f_pos[1] + pos[1] * f_scale[1], f_pos[2] + pos[2] * f_scale[2])

# Find PixelArtContainer
cubes_pos = []
for tr_id, data in transforms.items():
    go_name = game_objects.get(data['go_id'], '')
    if go_name.startswith('Pixel_'):
        cubes_pos.append(get_world_pos(tr_id))

print(f"Total pixel cubes found: {len(cubes_pos)}")
if cubes_pos:
    xs = [p[0] for p in cubes_pos]
    ys = [p[1] for p in cubes_pos]
    zs = [p[2] for p in cubes_pos]
    print(f"Cube bounds X: {min(xs):.3f} to {max(xs):.3f}, center: {(min(xs)+max(xs))*0.5:.3f}, width: {max(xs)-min(xs):.3f}")
    print(f"Cube bounds Y: {min(ys):.3f} to {max(ys):.3f}, center: {(min(ys)+max(ys))*0.5:.3f}, height: {max(ys)-min(ys):.3f}")
    print(f"Cube bounds Z: {min(zs):.3f} to {max(zs):.3f}, center: {(min(zs)+max(zs))*0.5:.3f}")

# Check key objects world positions
for name in ['[Zone_Sand_PlayArea]', '[PixelArtGenerator]', 'PixelArtContainer', '[Zone_Water_LowerArea]', '[WaterSlotsRow]', 'WaterSlot_1', 'WaterSlot_3', 'WaterSlot_5']:
    for tr_id, data in transforms.items():
        if game_objects.get(data['go_id']) == name:
            wp = get_world_pos(tr_id)
            print(f"Object {name} -> LocalPos: {data['pos']}, LocalScale: {data['scale']}, WorldPos: ({wp[0]:.3f}, {wp[1]:.3f}, {wp[2]:.3f})")
