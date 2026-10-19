import re
import os

def update_all():
    filepath = 'Assets/Scenes/Gemi.unity'
    with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
        text = f.read()

    parts = text.split('\n--- !u!')
    header = parts[0]
    raw_blocks = parts[1:]

    # Map GameObject IDs to names
    go_names = {}
    for b in raw_blocks:
        first_line = b.split('\n', 1)[0]
        m = re.match(r'1\s+&(\d+)', first_line)
        if m:
            go_id = m.group(1)
            name_m = re.search(r'm_Name:\s*([^\r\n]+)', b)
            if name_m:
                go_names[go_id] = name_m.group(1).strip('\'\" \r\n')

    print(f'Total GameObjects mapped: {len(go_names)}')

    new_blocks = []
    spots_changed = []
    conts_changed = []
    pills_changed = []
    texts_changed = []

    for b in raw_blocks:
        first_line = b.split('\n', 1)[0]
        class_m = re.match(r'(\d+)\s+&(\d+)', first_line)
        if not class_m:
            new_blocks.append(b)
            continue

        class_id = class_m.group(1)
        comp_id = class_m.group(2)

        go_ref = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', b)
        go_name = go_names.get(go_ref.group(1), "") if go_ref else ""

        if class_id == '224': # RectTransform
            if re.match(r'^Spot_\d', go_name):
                b = re.sub(r'm_LocalScale:\s*\{[^\}]+\}', 'm_LocalScale: {x: 1, y: 1, z: 1}', b)
                b = re.sub(r'm_SizeDelta:\s*\{[^\}]+\}', 'm_SizeDelta: {x: 160, y: 163}', b)
                spots_changed.append(go_name)
            elif 'UI_Container_' in go_name:
                b = re.sub(r'm_LocalScale:\s*\{[^\}]+\}', 'm_LocalScale: {x: 1, y: 1, z: 1}', b)
                b = re.sub(r'm_SizeDelta:\s*\{[^\}]+\}', 'm_SizeDelta: {x: 160, y: 163}', b)
                conts_changed.append(go_name)
            elif go_name == 'Badge_Pill':
                b = re.sub(r'm_AnchoredPosition:\s*\{[^\}]+\}', 'm_AnchoredPosition: {x: 0, y: 14}', b)
                b = re.sub(r'm_SizeDelta:\s*\{[^\}]+\}', 'm_SizeDelta: {x: 140, y: 110}', b)
                pills_changed.append(comp_id)

        elif class_id == '65': # BoxCollider
            if 'UI_Container_' in go_name:
                b = re.sub(r'm_Size:\s*\{[^\}]+\}', 'm_Size: {x: 1.6, y: 1.63, z: 0.2}', b)

        elif class_id == '114': # MonoBehaviour
            if go_name == 'Badge_Pill' and 'guid: fe87c0e1cc204ed48ad3b37840f39efc' in b:
                # Pill background transparent
                b = re.sub(r'm_Color:\s*\{[^\}]+\}', 'm_Color: {r: 1, g: 1, b: 1, a: 0}', b)
            elif go_name == 'Badge_Text':
                b = re.sub(r'm_fontSize:\s*\d+', 'm_fontSize: 58', b)
                b = re.sub(r'm_fontSizeBase:\s*\d+', 'm_fontSizeBase: 58', b)
                b = re.sub(r'm_fontSizeMin:\s*\d+', 'm_fontSizeMin: 36', b)
                b = re.sub(r'm_fontSizeMax:\s*\d+', 'm_fontSizeMax: 64', b)
                texts_changed.append(comp_id)

        elif class_id == '21' and comp_id == '1559106767': # Font material instance
            b = re.sub(r'_OutlineWidth:\s*[0-9.]+', '_OutlineWidth: 0.28', b)
            b = re.sub(r'_OutlineColor:\s*\{[^\}]+\}', '_OutlineColor: {r: 0, g: 0, b: 0, a: 1}', b)

        new_blocks.append(b)

    out = header + '\n--- !u!' + '\n--- !u!'.join(new_blocks)
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(out)

    print(f'Updated spots: {spots_changed}')
    print(f'Updated conts: {conts_changed}')
    print(f'Updated pills: {len(pills_changed)}')
    print(f'Updated texts: {len(texts_changed)}')

if __name__ == '__main__':
    update_all()
