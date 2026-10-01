import re

with open('Assets/Scenes/Gemi.unity', 'r', encoding='utf-8') as f:
    text = f.read()

targets = ["WaterSlot_1", "Waiting_Ship_0", "MarinaSlotLayout", "ShipQueuePool"]
for t in targets:
    m = re.search(r'm_Name:\s*.*?(' + re.escape(t) + r')[\s\S]*?m_Component:\s*\n\s*-\s*component:\s*\{fileID:\s*(\d+)\}', text)
    if m:
        tr_id = m.group(2)
        tr_block = re.search(r'--- !u!4 &' + tr_id + r'[\s\S]*?(?=--- !u!|\Z)', text)
        if tr_block:
            print(m.group(1), f'(id {tr_id}):')
            for l in tr_block.group(0).splitlines():
                if any(k in l for k in ['Position', 'Rotation', 'Scale', 'Euler']):
                    print(' ', l)
