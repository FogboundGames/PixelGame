import re

with open('Assets/Scenes/Gemi.unity', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

parts = text.split('\n--- !u!')
header = parts[0]
raw_blocks = parts[1:]

go_names = {}
for b in raw_blocks:
    first_line = b.split('\n', 1)[0]
    m = re.match(r'1\s+&(\d+)', first_line)
    if m:
        go_id = m.group(1)
        name_m = re.search(r'm_Name:\s*([^\r\n]+)', b)
        if name_m:
            go_names[go_id] = name_m.group(1).strip('\'\" \r\n')

print('1241628236 in go_names?', '1241628236' in go_names, repr(go_names.get('1241628236')))

for b in raw_blocks:
    if '&1241628237' in b[:50]:
        first_line = b.split('\n', 1)[0]
        class_m = re.match(r'(\d+)\s+&(\d+)', first_line)
        print('class_m:', class_m.groups())
        go_ref = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', b)
        print('go_ref:', go_ref.group(1))
        go_name = go_names.get(go_ref.group(1), '')
        print('go_name:', repr(go_name))
        print('re.match result:', re.match(r'^Spot_\d', go_name))
        # Test replace
        b_sub = re.sub(r'm_LocalScale:\s*\{[^\}]+\}', 'm_LocalScale: {x: 1, y: 1, z: 1}', b)
        b_sub = re.sub(r'm_SizeDelta:\s*\{[^\}]+\}', 'm_SizeDelta: {x: 160, y: 163}', b_sub)
        print('b_sub contains 160?', '160' in b_sub)
