import re, shutil

scene_path = 'Assets/Scenes/Gemi.unity'
backup_path = 'Assets/Scenes/Gemi.unity.pre_ship_color_bak'
shutil.copyfile(scene_path, backup_path)
print(f'Backup created at {backup_path}')

with open(scene_path, 'r', encoding='utf-8') as f:
    text = f.read()

docs = text.split('\n--- !u!')
header = docs[0]
doc_items = docs[1:]

doc_by_fid = {}
fid_order = []

for d in doc_items:
    fl = d.splitlines()[0]
    m = re.search(r'(\d+)\s+&(\d+)', fl)
    if m:
        fid = m.group(2)
        tid = m.group(1)
        doc_by_fid[fid] = {'tid': tid, 'content': d}
        fid_order.append(fid)

# Index GameObjects and Transforms
gos = {} # fid -> doc
trs = {} # fid -> doc
scs = {} # goid -> (fid, col_str)

for fid, data in doc_by_fid.items():
    tid = data['tid']
    c = data['content']
    if tid == '1': # GameObject
        name_m = re.search(r'm_Name:\s*([^\n\r]+)', c)
        name = name_m.group(1) if name_m else 'Unknown'
        gos[fid] = {'name': name, 'doc': c}
    elif tid in ('4', '224'): # Transform or RectTransform
        goid_m = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', c)
        goid = goid_m.group(1) if goid_m else None
        fa_m = re.search(r'm_Father:\s*\{fileID:\s*(\d+)\}', c)
        fa = fa_m.group(1) if fa_m else None
        ch = re.findall(r'- \{fileID:\s*(\d+)\}', c)
        trs[fid] = {'goid': goid, 'fa': fa, 'ch': ch, 'doc': c, 'tid': tid}
    elif tid == '114' and 'ShipController' in c:
        goid_m = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', c)
        goid = goid_m.group(1) if goid_m else None
        col_m = re.search(r'm_ShipColor:\s*\{([^}]+)\}', c)
        col_str = col_m.group(1) if col_m else ''
        if goid:
            scs[goid] = (fid, col_str)

print(f'Total GameObjects: {len(gos)}, Transforms: {len(trs)}, ShipControllers: {len(scs)}')

# Material GUIDs
MAT_PINK = '1be47a3c2f175def968dd2a5a9d59f3f'      # FF3A9B
MAT_ROSE = 'd4080d74f10b554fbf50932937c0ad59'      # C80A64
MAT_LIGHT = '078377e51af05ad988c44fa9f3163242'     # FFC3E6
FONT_GUID = 'de29a109ff4a76b45afbcf71cd43d47f'     # LilitaOne SDF

# Map GameObject to Transform
go_to_tr = {data['goid']: fid for fid, data in trs.items() if data['goid']}

ships_updated = 0
for ship_goid, (sc_fid, col_str) in scs.items():
    ship_name = gos.get(ship_goid, {}).get('name', 'Unknown')
    ship_tr_fid = go_to_tr.get(ship_goid)
    if not ship_tr_fid: continue
    
    ship_tr = trs[ship_tr_fid]
    
    # Determine which material
    if 'r: 0.784' in col_str or 'r: 0.78' in col_str:
        chosen_mat_guid = MAT_ROSE
    elif 'g: 0.764' in col_str or 'g: 0.76' in col_str:
        chosen_mat_guid = MAT_LIGHT
    else:
        chosen_mat_guid = MAT_PINK
        
    # Inspect children of ship_tr
    for child_tr_fid in ship_tr['ch']:
        child_tr = trs.get(child_tr_fid)
        if not child_tr: continue
        child_goid = child_tr['goid']
        child_name = gos.get(child_goid, {}).get('name', '')
        
        # 1. VisualRoot -> MeshRenderer material
        if '[VisualRoot]' in child_name:
            # Find MeshRenderer on child_goid
            # Look through all components in child_goid doc
            cg_doc = gos.get(child_goid, {}).get('doc', '')
            comp_fids = re.findall(r'- component:\s*\{fileID:\s*(\d+)\}', cg_doc)
            for cf in comp_fids:
                cdata = doc_by_fid.get(cf)
                if cdata and cdata['tid'] == '23': # MeshRenderer
                    # Replace m_Materials
                    mr_doc = cdata['content']
                    mr_doc = re.sub(
                        r'm_Materials:\s*\n\s*-\s*\{[^\}]*\}',
                        f'm_Materials:\n  - {{fileID: 2100000, guid: {chosen_mat_guid}, type: 2}}',
                        mr_doc
                    )
                    cdata['content'] = mr_doc
                    doc_by_fid[cf] = cdata
                    
        # 2. Ship_Capacity_Canvas -> position, rotation, scale
        elif 'Ship_Capacity_Canvas' in child_name:
            c_doc = child_tr['doc']
            # update position, rotation, scale
            c_doc = re.sub(r'm_LocalPosition:\s*\{[^\}]*\}', 'm_LocalPosition: {x: 0, y: 2.15, z: -0.18}', c_doc)
            c_doc = re.sub(r'm_LocalRotation:\s*\{[^\}]*\}', 'm_LocalRotation: {x: 0, y: 0, z: 0, w: 1}', c_doc)
            c_doc = re.sub(r'm_LocalScale:\s*\{[^\}]*\}', 'm_LocalScale: {x: 0.024, y: 0.024, z: 0.024}', c_doc)
            child_tr['doc'] = c_doc
            doc_by_fid[child_tr_fid]['content'] = c_doc
            
            # Inspect children of Canvas (Badge_Text)
            for text_tr_fid in child_tr['ch']:
                text_tr = trs.get(text_tr_fid)
                if not text_tr: continue
                text_goid = text_tr['goid']
                text_name = gos.get(text_goid, {}).get('name', '')
                if 'Badge_Text' in text_name:
                    t_doc = text_tr['doc']
                    t_doc = re.sub(r'm_LocalPosition:\s*\{[^\}]*\}', 'm_LocalPosition: {x: 0, y: 0, z: 0}', t_doc)
                    t_doc = re.sub(r'm_LocalRotation:\s*\{[^\}]*\}', 'm_LocalRotation: {x: 0, y: 0, z: 0, w: 1}', t_doc)
                    t_doc = re.sub(r'm_SizeDelta:\s*\{[^\}]*\}', 'm_SizeDelta: {x: 180, y: 120}', t_doc)
                    t_doc = re.sub(r'm_AnchoredPosition:\s*\{[^\}]*\}', 'm_AnchoredPosition: {x: 0, y: 0}', t_doc)
                    text_tr['doc'] = t_doc
                    doc_by_fid[text_tr_fid]['content'] = t_doc
                    
                    # Update TextMeshProUGUI component
                    tg_doc = gos.get(text_goid, {}).get('doc', '')
                    t_comp_fids = re.findall(r'- component:\s*\{fileID:\s*(\d+)\}', tg_doc)
                    for tcf in t_comp_fids:
                        tcdata = doc_by_fid.get(tcf)
                        if tcdata and tcdata['tid'] == '114' and 'TextMeshProUGUI' in tcdata['content']:
                            tmp_doc = tcdata['content']
                            # set font asset
                            tmp_doc = re.sub(
                                r'm_fontAsset:\s*\{[^\}]*\}',
                                f'm_fontAsset: {{fileID: 11400000, guid: {FONT_GUID}, type: 2}}',
                                tmp_doc
                            )
                            # set font color white
                            tmp_doc = re.sub(
                                r'm_fontColor:\s*\{[^\}]*\}',
                                'm_fontColor: {r: 1, g: 1, b: 1, a: 1}',
                                tmp_doc
                            )
                            # set font size
                            tmp_doc = re.sub(r'm_fontSize:\s*[\d\.]+', 'm_fontSize: 58', tmp_doc)
                            tmp_doc = re.sub(r'm_fontSizeBase:\s*[\d\.]+', 'm_fontSizeBase: 58', tmp_doc)
                            # set outline
                            if 'm_outlineWidth:' in tmp_doc:
                                tmp_doc = re.sub(r'm_outlineWidth:\s*[\d\.]+', 'm_outlineWidth: 0.28', tmp_doc)
                            else:
                                tmp_doc += '\n  m_outlineWidth: 0.28'
                            if 'm_outlineColor:' in tmp_doc:
                                tmp_doc = re.sub(r'm_outlineColor:\s*\{[^\}]*\}', 'm_outlineColor: {r: 0.07, g: 0.07, b: 0.086, a: 1}', tmp_doc)
                            else:
                                tmp_doc += '\n  m_outlineColor: {r: 0.07, g: 0.07, b: 0.086, a: 1}'
                            tcdata['content'] = tmp_doc
                            doc_by_fid[tcf] = tcdata

    ships_updated += 1

print(f'Successfully updated {ships_updated} ships!')

# Reconstruct file
new_text = header + '\n--- !u!' + '\n--- !u!'.join(doc_by_fid[fid]['content'] for fid in fid_order)

with open(scene_path, 'w', encoding='utf-8') as f:
    f.write(new_text)

print('Wrote updated Assets/Scenes/Gemi.unity successfully!')
