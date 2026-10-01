import re

with open('Assets/Scenes/Gemi.unity', 'r', encoding='utf-8') as f:
    text = f.read()

gos = {}
trs = {}
for doc in text.split('--- !u!'):
    lines = doc.splitlines()
    if not lines: continue
    fl = lines[0]
    m = re.search(r'(\d+)\s+&(\d+)', fl)
    if not m: continue
    t_id, f_id = m.group(1), m.group(2)
    if t_id == '1':
        name = re.search(r'm_Name: ([^\n\r]+)', doc).group(1)
        gos[f_id] = name
    elif t_id in ('4', '224'):
        goid_m = re.search(r'm_GameObject: \{fileID: (\d+)\}', doc)
        if not goid_m: continue
        goid = goid_m.group(1)
        fa_m = re.search(r'm_Father: \{fileID: (\d+)\}', doc)
        fa = fa_m.group(1) if fa_m else None
        ch = re.findall(r'- \{fileID: (\d+)\}', doc)
        trs[f_id] = {'goid': goid, 'fa': fa, 'ch': ch, 'type': t_id}

def dump_tree(tr_id, indent=0):
    tr = trs.get(tr_id)
    if not tr: return
    gname = gos.get(tr['goid'], 'Unknown')
    print('  '*indent + f'- {gname} (TR:{tr_id}, GO:{tr["goid"]}, type:{tr["type"]})')
    for ch_id in tr['ch']:
        dump_tree(ch_id, indent+1)

dump_tree('256429731')
