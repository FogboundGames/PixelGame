import re

scene_path = 'c:/Users/ezgid/OneDrive/Masaüstü/PixelGame/Assets/Scenes/Gemi.unity'
with open(scene_path, 'r', encoding='utf-8') as f:
    text = f.read()

spot_xs = [-345.0, -115.0, 115.0, 345.0]
row_ys = [-230.0, -410.0]

# 1. Update Spot_0 .. Spot_7
for r in range(2):
    for c in range(4):
        i = r * 4 + c
        target_x = spot_xs[c]
        target_y = row_ys[r]
        pattern = rf'(m_Name:\s*Spot_{i}[\s\S]*?m_LocalScale:\s*\{{x:\s*)([0-9\.]+)(,\s*y:\s*)([0-9\.]+)(,\s*z:\s*)([0-9\.]+)(\}}[\s\S]*?m_AnchoredPosition:\s*\{{x:\s*)([\-0-9\.]+)(,\s*y:\s*)([\-0-9\.]+)(\}}[\s\S]*?m_SizeDelta:\s*\{{x:\s*)([0-9\.]+)(,\s*y:\s*)([0-9\.]+)(\}})'
        m = re.search(pattern, text)
        if m:
            before = m.group(0)
            after = re.sub(r'm_LocalScale:\s*\{x:\s*[0-9\.]+,\s*y:\s*[0-9\.]+,\s*z:\s*[0-9\.]+\}', 'm_LocalScale: {x: 1.06, y: 1.06, z: 1.06}', before)
            after = re.sub(r'm_AnchoredPosition:\s*\{x:\s*[\-0-9\.]+,\s*y:\s*[\-0-9\.]+\}', f'm_AnchoredPosition: {{x: {target_x}, y: {target_y}}}', after)
            after = re.sub(r'm_SizeDelta:\s*\{x:\s*[0-9\.]+,\s*y:\s*[0-9\.]+\}', 'm_SizeDelta: {x: 205, y: 150}', after)
            text = text[:m.start()] + after + text[m.end():]
            print(f'Updated Spot_{i}: pos=({target_x}, {target_y}), scale=1.06')

# 2. Update Container RT, Badge_Pill RT, and Badge_Text TMP for each of 8 containers
for i in range(8):
    m = re.search(rf'--- !u!1 &(\d+)\s+GameObject:[^\n]*\n(?:  [^\n]*\n)*?  m_Name:\s*UI_Container_{i}_\w+', text)
    if not m:
        print(f'Container {i} not found!')
        continue
    c_go_id = m.group(1)
    
    # container RT
    c_rt_m = re.search(rf'--- !u!224 &(\d+)\s+RectTransform:[^\n]*\n(?:  [^\n]*\n)*?  m_GameObject:\s*\{{fileID:\s*{c_go_id}\}}', text)
    c_rt_id = c_rt_m.group(1)
    
    # update container RT sizeDelta
    c_rt_block = re.search(rf'(--- !u!224 &{c_rt_id}\s+RectTransform:[\s\S]*?m_SizeDelta:\s*\{{x:\s*)([0-9\.]+)(,\s*y:\s*)([0-9\.]+)(\}}[\s\S]*?)(--- !u!|\Z)', text)
    if c_rt_block:
        orig = c_rt_block.group(0)
        repl = re.sub(r'm_SizeDelta:\s*\{x:\s*[0-9\.]+,\s*y:\s*[0-9\.]+\}', 'm_SizeDelta: {x: 205, y: 150}', orig)
        text = text[:c_rt_block.start()] + repl + text[c_rt_block.end():]

    # Find pill RT
    c_children_m = re.search(rf'--- !u!224 &{c_rt_id}\s+RectTransform:[\s\S]*?m_Children:([\s\S]*?)  m_Father:', text)
    pill_rt_id = re.findall(r'fileID:\s*(\d+)', c_children_m.group(1))[0]

    # update pill RT position and sizeDelta
    pill_rt_block = re.search(rf'(--- !u!224 &{pill_rt_id}\s+RectTransform:[\s\S]*?m_AnchoredPosition:\s*\{{x:\s*)([\-0-9\.]+)(,\s*y:\s*)([\-0-9\.]+)(\}}[\s\S]*?m_SizeDelta:\s*\{{x:\s*)([0-9\.]+)(,\s*y:\s*)([0-9\.]+)(\}}[\s\S]*?)(--- !u!|\Z)', text)
    if pill_rt_block:
        orig = pill_rt_block.group(0)
        repl = re.sub(r'm_AnchoredPosition:\s*\{x:\s*[\-0-9\.]+,\s*y:\s*[\-0-9\.]+\}', 'm_AnchoredPosition: {x: 0, y: 26}', orig)
        repl = re.sub(r'm_SizeDelta:\s*\{x:\s*[0-9\.]+,\s*y:\s*[0-9\.]+\}', 'm_SizeDelta: {x: 110, y: 50}', repl)
        text = text[:pill_rt_block.start()] + repl + text[pill_rt_block.end():]
        print(f'Updated Pill {i} RT pose to (0, 26), size (110, 50)')

    # Find text RT and TMP
    pill_children_m = re.search(rf'--- !u!224 &{pill_rt_id}\s+RectTransform:[\s\S]*?m_Children:([\s\S]*?)  m_Father:', text)
    text_rt_id = re.findall(r'fileID:\s*(\d+)', pill_children_m.group(1))[0]
    text_go_m = re.search(rf'--- !u!224 &{text_rt_id}\s+RectTransform:[^\n]*\n(?:  [^\n]*\n)*?  m_GameObject:\s*\{{fileID:\s*(\d+)\}}', text)
    cur_text_go_id = text_go_m.group(1)
    
    tmp_m = re.search(rf'--- !u!114 &(\d+)\s+MonoBehaviour:[^\n]*\n(?:  [^\n]*\n)*?  m_GameObject:\s*\{{fileID:\s*{cur_text_go_id}\}}', text)
    if tmp_m:
        tmp_id = tmp_m.group(1)
        tmp_block = re.search(rf'(--- !u!114 &{tmp_id}\s+MonoBehaviour:[\s\S]*?)(--- !u!|\Z)', text)
        if tmp_block:
            orig = tmp_block.group(1)
            repl = re.sub(r'm_fontSize:\s*[0-9\.]+', 'm_fontSize: 32', orig)
            repl = re.sub(r'm_fontSizeBase:\s*[0-9\.]+', 'm_fontSizeBase: 32', repl)
            repl = re.sub(r'm_enableAutoSizing:\s*[0-9]+', 'm_enableAutoSizing: 1', repl)
            repl = re.sub(r'm_fontSizeMin:\s*[0-9\.]+', 'm_fontSizeMin: 20', repl)
            repl = re.sub(r'm_fontSizeMax:\s*[0-9\.]+', 'm_fontSizeMax: 36', repl)
            text = text[:tmp_block.start()] + repl + text[tmp_block.start() + len(orig):]

with open(scene_path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully updated Gemi.unity with aligned spots and Y=26 text!')
