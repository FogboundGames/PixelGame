import re
import os

def process_yaml_file(filepath):
    print(f"Processing {filepath}...")
    with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
        text = f.read()

    # Split into header and blocks
    parts = text.split('\n--- !u!')
    header = parts[0]
    raw_blocks = parts[1:]

    # Pass 1: Map GameObject fileID to its name
    go_names = {} # fileID (str) -> name (str)
    for b in raw_blocks:
        first_line = b.split('\n', 1)[0]
        # Example: 1 &1241628236 or 1 &8529083208746402546 stripped
        m = re.match(r'1\s+&(\d+)', first_line)
        if m:
            go_id = m.group(1)
            name_m = re.search(r'm_Name:\s*([^\r\n]+)', b)
            if name_m:
                go_names[go_id] = name_m.group(1).strip("'\" ")

    print(f"  Mapped {len(go_names)} GameObjects.")

    # Pass 2: Update components based on GameObject name or block type
    new_blocks = []
    spot_updated = 0
    cont_updated = 0
    pill_updated = 0
    text_updated = 0
    col_updated = 0

    for b in raw_blocks:
        first_line = b.split('\n', 1)[0]
        class_m = re.match(r'(\d+)\s+&(\d+)', first_line)
        if not class_m:
            new_blocks.append(b)
            continue

        class_id = class_m.group(1)
        comp_id = class_m.group(2)

        # Find which GameObject this component belongs to
        go_ref = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', b)
        go_name = go_names.get(go_ref.group(1), "") if go_ref else ""

        # RectTransform (class 224)
        if class_id == '224':
            if re.match(r'^Spot_\d', go_name):
                # LocalScale -> 1, 1, 1; SizeDelta -> 160, 163
                b = re.sub(r'm_LocalScale:\s*\{[^\}]+\}', 'm_LocalScale: {x: 1, y: 1, z: 1}', b)
                b = re.sub(r'm_SizeDelta:\s*\{[^\}]+\}', 'm_SizeDelta: {x: 160, y: 163}', b)
                spot_updated += 1
            elif 'UI_Container_' in go_name or go_name == 'Container_UI':
                b = re.sub(r'm_LocalScale:\s*\{[^\}]+\}', 'm_LocalScale: {x: 1, y: 1, z: 1}', b)
                b = re.sub(r'm_SizeDelta:\s*\{[^\}]+\}', 'm_SizeDelta: {x: 160, y: 163}', b)
                cont_updated += 1
            elif go_name == 'Badge_Pill':
                b = re.sub(r'm_AnchoredPosition:\s*\{[^\}]+\}', 'm_AnchoredPosition: {x: 0, y: 14}', b)
                b = re.sub(r'm_SizeDelta:\s*\{[^\}]+\}', 'm_SizeDelta: {x: 140, y: 110}', b)
                pill_updated += 1

        # BoxCollider (class 65)
        elif class_id == '65':
            if 'UI_Container_' in go_name or go_name == 'Container_UI':
                b = re.sub(r'm_Size:\s*\{[^\}]+\}', 'm_Size: {x: 1.6, y: 1.63, z: 0.2}', b)
                col_updated += 1

        # Image on Badge_Pill (class 114)
        elif class_id == '114':
            if go_name == 'Badge_Pill' and 'guid: fe87c0e1cc204ed48ad3b37840f39efc' in b:
                b = re.sub(r'm_Color:\s*\{[^\}]+\}', 'm_Color: {r: 1, g: 1, b: 1, a: 0}', b)
            elif go_name == 'Badge_Text' and ('guid: f4688fdb7df04437aeb418b961361dc5' in b or 'TMPro' in b):
                b = re.sub(r'm_fontSize:\s*\d+', 'm_fontSize: 58', b)
                b = re.sub(r'm_fontSizeBase:\s*\d+', 'm_fontSizeBase: 58', b)
                b = re.sub(r'm_fontSizeMin:\s*\d+', 'm_fontSizeMin: 36', b)
                b = re.sub(r'm_fontSizeMax:\s*\d+', 'm_fontSizeMax: 64', b)
                text_updated += 1

        # Material 1559106767 (class 21)
        elif class_id == '21' and comp_id == '1559106767':
            b = re.sub(r'_OutlineWidth:\s*[0-9.]+', '_OutlineWidth: 0.28', b)
            b = re.sub(r'_OutlineColor:\s*\{[^\}]+\}', '_OutlineColor: {r: 0, g: 0, b: 0, a: 1}', b)

        new_blocks.append(b)

    output_text = header + '\n--- !u!' + '\n--- !u!'.join(new_blocks)
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(output_text)

    print(f"  Finished {filepath}: spots={spot_updated}, conts={cont_updated}, pills={pill_updated}, texts={text_updated}, cols={col_updated}")

if __name__ == '__main__':
    process_yaml_file('Assets/Prefabs/Container_UI.prefab')
    process_yaml_file('Assets/Scenes/Gemi.unity')
