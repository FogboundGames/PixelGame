import re

scene_path = 'c:/Users/ezgid/OneDrive/Masaüstü/PixelGame/Assets/Scenes/Gemi.unity'
with open(scene_path, 'r', encoding='utf-8') as f:
    text = f.read()

spot_rts = ['607417917', '401593328', '1826787811', '695870477', '696804671', '1972700043', '2078403691', '1892276227']
spot_xs = [-345.0, -115.0, 115.0, 345.0]
row_ys = [-230.0, -410.0]

for idx, rtid in enumerate(spot_rts):
    r = idx // 4
    c = idx % 4
    tx = spot_xs[c]
    ty = row_ys[r]
    
    # 1. Update Spot RT
    spot_block = re.search(rf'(--- !u!224 &{rtid}\s+RectTransform:[\s\S]*?)(--- !u!|\Z)', text)
    if spot_block:
        orig = spot_block.group(1)
        repl = re.sub(r'm_LocalScale:\s*\{x:\s*[0-9\.]+,\s*y:\s*[0-9\.]+,\s*z:\s*[0-9\.]+\}', 'm_LocalScale: {x: 1.06, y: 1.06, z: 1.06}', orig)
        repl = re.sub(r'm_AnchoredPosition:\s*\{x:\s*[\-0-9\.]+,\s*y:\s*[\-0-9\.]+\}', f'm_AnchoredPosition: {{x: {tx}, y: {ty}}}', repl)
        repl = re.sub(r'm_SizeDelta:\s*\{x:\s*[0-9\.]+,\s*y:\s*[0-9\.]+\}', 'm_SizeDelta: {x: 205, y: 150}', repl)
        text = text[:spot_block.start()] + repl + text[spot_block.start() + len(orig):]
        print(f'Spot {idx} (RT {rtid}) updated to pos=({tx}, {ty}), scale=1.06')

    # 2. Find child container RT
    m = re.search(rf'--- !u!224 &{rtid}\s+RectTransform:[\s\S]*?m_Children:([\s\S]*?)  m_Father:', text)
    c_rt_id = re.findall(r'fileID:\s*(\d+)', m.group(1))[0]
    
    # update container sizeDelta
    c_block = re.search(rf'(--- !u!224 &{c_rt_id}\s+RectTransform:[\s\S]*?)(--- !u!|\Z)', text)
    if c_block:
        orig = c_block.group(1)
        repl = re.sub(r'm_SizeDelta:\s*\{x:\s*[0-9\.]+,\s*y:\s*[0-9\.]+\}', 'm_SizeDelta: {x: 205, y: 150}', orig)
        text = text[:c_block.start()] + repl + text[c_block.start() + len(orig):]

    # 3. Find Pill RT
    m2 = re.search(rf'--- !u!224 &{c_rt_id}\s+RectTransform:[\s\S]*?m_Children:([\s\S]*?)  m_Father:', text)
    pill_rt_id = re.findall(r'fileID:\s*(\d+)', m2.group(1))[0]
    
    # update Pill RT pos and sizeDelta
    p_block = re.search(rf'(--- !u!224 &{pill_rt_id}\s+RectTransform:[\s\S]*?)(--- !u!|\Z)', text)
    if p_block:
        orig = p_block.group(1)
        repl = re.sub(r'm_AnchoredPosition:\s*\{x:\s*[\-0-9\.]+,\s*y:\s*[\-0-9\.]+\}', 'm_AnchoredPosition: {x: 0, y: 26}', orig)
        repl = re.sub(r'm_SizeDelta:\s*\{x:\s*[0-9\.]+,\s*y:\s*[0-9\.]+\}', 'm_SizeDelta: {x: 110, y: 50}', repl)
        text = text[:p_block.start()] + repl + text[p_block.start() + len(orig):]
        print(f'Pill {idx} (RT {pill_rt_id}) updated to pos=(0, 26), size=(110, 50)')

with open(scene_path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully updated all 8 spots and pills in Gemi.unity!')
