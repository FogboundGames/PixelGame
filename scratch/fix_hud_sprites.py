import re

def fix_hud_sprites():
    scene_path = "Assets/Scenes/Gemi.unity"
    with open(scene_path, "r", encoding="utf-8") as f:
        text = f.read()

    # Mapping of GameObject name -> Sprite GUID
    sprites = {
        "SettingsButton": "30372c62b2f43f74aa7ca88ace17a2d5", # btn_settings
        "TopBanner": "69dcb0012ba1ed549a6939e24a74d25d",      # hud_top_banner
        "HeartPill": "2734dca7eaf325841976520e017adec7",      # ui_pill
        "CoinPill": "2734dca7eaf325841976520e017adec7",       # ui_pill
    }

    # Find GameObject blocks
    go_blocks = [m.group(0) for m in re.finditer(r'--- !u!1 &(\d+)[\s\S]*?(?=--- !u|\Z)', text)]
    go_info = {}
    for block in go_blocks:
        id_m = re.search(r'--- !u!1 &(\d+)', block)
        name_m = re.search(r'm_Name:\s*([^\r\n]+)', block)
        if id_m and name_m:
            gid = id_m.group(1)
            name = name_m.group(1).strip()
            comp_ids = re.findall(r'component:\s*\{fileID:\s*(\d+)\}', block)
            go_info[gid] = (name, comp_ids)

    # Find RectTransform blocks
    rt_blocks = [m.group(0) for m in re.finditer(r'--- !u!224 &(\d+)[\s\S]*?(?=--- !u|\Z)', text)]
    parent_map = {}
    rt_to_go = {}
    go_to_rt = {}

    for block in rt_blocks:
        rt_m = re.search(r'--- !u!224 &(\d+)', block)
        go_m = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', block)
        if rt_m and go_m:
            rt_id = rt_m.group(1)
            go_id = go_m.group(1)
            father_m = re.search(r'm_Father:\s*\{fileID:\s*(\d+)\}', block)
            father_id = father_m.group(1) if father_m else "0"
            parent_map[rt_id] = father_id
            rt_to_go[rt_id] = go_id
            go_to_rt[go_id] = rt_id

    image_guid = "fe87c0e1cc204ed48ad3b37840f39efc"
    new_text = text
    count = 0

    for gid, (name, comp_ids) in go_info.items():
        sprite_guid = None
        if name in sprites:
            sprite_guid = sprites[name]
        elif name == "Icon" or name == "PlusButton":
            rt_id = go_to_rt.get(gid)
            father_rt = parent_map.get(rt_id)
            father_go = rt_to_go.get(father_rt)
            father_name = go_info.get(father_go, ("", []))[0] if father_go else ""

            if name == "Icon":
                if father_name == "HeartPill":
                    sprite_guid = "7e3ce9b8036f1514d84c8f7b97b8e8b5" # icon_heart
                elif father_name == "CoinPill":
                    sprite_guid = "491473a610cc0904d947f15bad3ec31f" # icon_coin
            elif name == "PlusButton":
                sprite_guid = "5567196ba159eec43adeda5f68ee8875" # btn_plus

        if sprite_guid:
            for cid in comp_ids:
                pat = rf"(--- !u!114 &{cid}[\s\S]*?m_Script:\s*\{{fileID:\s*11500000,\s*guid:\s*{image_guid}[^\}}]*\}}[\s\S]*?m_Sprite:\s*)\{{[^\}}]*\}}"
                m = re.search(pat, new_text)
                if m:
                    rep = rf"\g<1>{{fileID: 21300000, guid: {sprite_guid}, type: 3}}"
                    new_text = re.sub(pat, rep, new_text)
                    print(f"Updated Image on {name} (component {cid}) -> guid {sprite_guid[:8]}...")
                    count += 1

    with open(scene_path, "w", encoding="utf-8") as f:
        f.write(new_text)

    print(f"Finished updating {count} HUD Image sprites in {scene_path}")

if __name__ == "__main__":
    fix_hud_sprites()
