import re

with open('Assets/Scenes/SampleScene.unity', 'r', encoding='utf-8') as f:
    content = f.read()

gos = {}
for block in content.split('--- !u!1 &'):
    lines = block.split('\n')
    if len(lines) > 0 and lines[0].strip():
        gid = lines[0].strip()
        m = re.search(r'm_Name:\s*([^\r\n]+)', block)
        if m:
            gos[gid] = m.group(1).strip()

trs = {}
for block in content.split('--- !u!224 &'):
    lines = block.split('\n')
    if len(lines) > 0 and lines[0].strip():
        tr_id = lines[0].strip()
        m_go = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', block)
        m_fa = re.search(r'm_Father:\s*\{fileID:\s*(\d+)\}', block)
        m_pos = re.search(r'm_AnchoredPosition:\s*([^\r\n]+)', block)
        m_size = re.search(r'm_SizeDelta:\s*([^\r\n]+)', block)
        if m_go:
            trs[tr_id] = {
                'go_id': m_go.group(1),
                'father': m_fa.group(1) if m_fa else '0',
                'pos': m_pos.group(1) if m_pos else '',
                'size': m_size.group(1) if m_size else ''
            }

print("=== ALL UI HIERARCHY ===")
for tr_id, data in trs.items():
    go_name = gos.get(data['go_id'], 'unknown')
    father_tr = data['father']
    father_name = 'ROOT'
    if father_tr in trs:
        father_name = gos.get(trs[father_tr]['go_id'], father_tr)
    print(go_name, '--> Parent:', father_name, '| Pos:', data['pos'], '| Size:', data['size'])
