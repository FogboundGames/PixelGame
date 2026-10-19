import re
import os

def update_prefab(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Root sizeDelta {x: 205, y: 150} -> {x: 160, y: 163}
    content = re.sub(
        r'(m_Name:\s*Container_UI[\s\S]*?m_SizeDelta:\s*\{x:)[^,]+(,\s*y:)[^}]+(\})',
        r'\g<1> 160\g<2> 163\g<3>',
        content,
        count=1
    )
    # If not matched by name first, check under RectTransform of root
    content = re.sub(
        r'(--- !u!224 &2044377078688014023[\s\S]*?m_SizeDelta:\s*\{x:)[^,]+(,\s*y:)[^}]+(\})',
        r'\g<1> 160\g<2> 163\g<3>',
        content
    )

    # 2. BoxCollider size {x: 2.05, y: 1.5, z: 0.2} -> {x: 1.6, y: 1.63, z: 0.2}
    content = re.sub(
        r'(--- !u!65 &3106520957228004876[\s\S]*?m_Size:\s*\{x:)[^,]+(,\s*y:)[^,]+(,\s*z:)[^}]+(\})',
        r'\g<1> 1.6\g<2> 1.63\g<3> 0.2\g<4>',
        content
    )

    # 3. Badge_Pill RectTransform
    content = re.sub(
        r'(--- !u!224 &120861373580120422[\s\S]*?m_AnchoredPosition:\s*\{x:)[^,]+(,\s*y:)[^}]+(\}[\s\S]*?m_SizeDelta:\s*\{x:)[^,]+(,\s*y:)[^}]+(\})',
        r'\g<1> 0\g<2> 14\g<3> 140\g<4> 110\g<5>',
        content
    )

    # 4. Badge_Pill Image color alpha to 0
    content = re.sub(
        r'(--- !u!114 &5905799692971565822[\s\S]*?m_Color:\s*\{r:)[^,]+(,\s*g:)[^,]+(,\s*b:)[^,]+(,\s*a:)[^}]+(\})',
        r'\g<1> 1\g<2> 1\g<3> 1\g<4> 0\g<5>',
        content
    )

    # 5. Badge_Text TextMeshProUGUI font sizes
    content = re.sub(
        r'(--- !u!114 &2808168229821511808[\s\S]*?m_fontSize:\s*)\d+(\s*m_fontSizeBase:\s*)\d+',
        r'\g<1>58\g<2>58',
        content
    )
    content = re.sub(
        r'(--- !u!114 &2808168229821511808[\s\S]*?m_fontSizeMin:\s*)\d+(\s*m_fontSizeMax:\s*)\d+',
        r'\g<1>36\g<2>64',
        content
    )

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f'Successfully updated prefab: {filepath}')


def update_scene(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Update Spots: Spot_0 through Spot_7
    # Spot localScale {x: 1.06, y: 1.06, z: 1.06} -> {x: 1, y: 1, z: 1}
    # Spot sizeDelta {x: 205, y: 150} -> {x: 160, y: 163}
    for i in range(8):
        pattern = rf'(m_Name:\s*Spot_{i}\b[\s\S]*?m_LocalScale:\s*\{{x:)[^,]+(,\s*y:)[^,]+(,\s*z:)[^}}]+(\}}[\s\S]*?m_SizeDelta:\s*\{{x:)[^,]+(,\s*y:)[^}}]+(\}})'
        # Let's find GameObject then its RectTransform
        # In Unity YAML: GameObject has m_Component with RectTransform ID.
        pass

    # A robust regex for any Spot_0..Spot_7 GameObject & its RectTransform:
    # First, let's find the RectTransforms belonging to Spot_0..7:
    spot_go_ids = re.findall(r'--- !u!1 &(\d+)\nGameObject:\n[\s\S]*?m_Name:\s*Spot_(\d)', content)
    print(f'Found {len(spot_go_ids)} Spot GameObjects in scene.')
    for go_id, spot_num in spot_go_ids:
        # Find RectTransform pointing to this go_id:
        rt_pattern = rf'(--- !u!224 &\d+[\s\S]*?m_GameObject:\s*\{{fileID:\s*{go_id}\}}[\s\S]*?m_LocalScale:\s*\{{x:)[^,]+(,\s*y:)[^,]+(,\s*z:)[^}}]+(\}}[\s\S]*?m_SizeDelta:\s*\{{x:)[^,]+(,\s*y:)[^}}]+(\}})'
        content = re.sub(rt_pattern, r'\g<1> 1\g<2> 1\g<3> 1\g<4> 160\g<5> 163\g<6>', content)

    # 2. Update all UI_Container_*:
    # RectTransform sizeDelta -> 160, 163
    # LocalScale -> 1, 1, 1
    cont_go_ids = re.findall(r'--- !u!1 &(\d+)\nGameObject:\n[\s\S]*?m_Name:\s*(UI_Container_\d_\w+)', content)
    print(f'Found {len(cont_go_ids)} UI_Container GameObjects in scene.')
    for go_id, cont_name in cont_go_ids:
        rt_pattern = rf'(--- !u!224 &\d+[\s\S]*?m_GameObject:\s*\{{fileID:\s*{go_id}\}}[\s\S]*?m_LocalScale:\s*\{{x:)[^,]+(,\s*y:)[^,]+(,\s*z:)[^}}]+(\}}[\s\S]*?m_SizeDelta:\s*\{{x:)[^,]+(,\s*y:)[^}}]+(\}})'
        content = re.sub(rt_pattern, r'\g<1> 1\g<2> 1\g<3> 1\g<4> 160\g<5> 163\g<6>', content)
        # Also BoxCollider
        col_pattern = rf'(--- !u!65 &\d+[\s\S]*?m_GameObject:\s*\{{fileID:\s*{go_id}\}}[\s\S]*?m_Size:\s*\{{x:)[^,]+(,\s*y:)[^,]+(,\s*z:)[^}}]+(\}})'
        content = re.sub(col_pattern, r'\g<1> 1.6\g<2> 1.63\g<3> 0.2\g<4>', content)

    # 3. Update all Badge_Pill in scene:
    pill_go_ids = re.findall(r'--- !u!1 &(\d+)\nGameObject:\n[\s\S]*?m_Name:\s*Badge_Pill\b', content)
    print(f'Found {len(pill_go_ids)} Badge_Pill GameObjects in scene.')
    for go_id in pill_go_ids:
        rt_pattern = rf'(--- !u!224 &\d+[\s\S]*?m_GameObject:\s*\{{fileID:\s*{go_id}\}}[\s\S]*?m_AnchoredPosition:\s*\{{x:)[^,]+(,\s*y:)[^}}]+(\}}[\s\S]*?m_SizeDelta:\s*\{{x:)[^,]+(,\s*y:)[^}}]+(\}})'
        content = re.sub(rt_pattern, r'\g<1> 0\g<2> 14\g<3> 140\g<4> 110\g<5>', content)
        # Image color alpha -> 0
        img_pattern = rf'(--- !u!114 &\d+[\s\S]*?m_GameObject:\s*\{{fileID:\s*{go_id}\}}[\s\S]*?m_Script:\s*\{{fileID:\s*11500000,\s*guid:\s*fe87c0e1cc204ed48ad3b37840f39efc[^\n]*\n[\s\S]*?m_Color:\s*\{{r:)[^,]+(,\s*g:)[^,]+(,\s*b:)[^,]+(,\s*a:)[^}}]+(\}})'
        content = re.sub(img_pattern, r'\g<1> 1\g<2> 1\g<3> 1\g<4> 0\g<5>', content)

    # 4. Update all Badge_Text in scene:
    text_go_ids = re.findall(r'--- !u!1 &(\d+)\nGameObject:\n[\s\S]*?m_Name:\s*Badge_Text\b', content)
    print(f'Found {len(text_go_ids)} Badge_Text GameObjects in scene.')
    for go_id in text_go_ids:
        tmp_pattern = rf'(--- !u!114 &\d+[\s\S]*?m_GameObject:\s*\{{fileID:\s*{go_id}\}}[\s\S]*?m_fontSize:\s*)\d+(\s*m_fontSizeBase:\s*)\d+'
        content = re.sub(tmp_pattern, r'\g<1>58\g<2>58', content)
        tmp_min_max = rf'(--- !u!114 &\d+[\s\S]*?m_GameObject:\s*\{{fileID:\s*{go_id}\}}[\s\S]*?m_fontSizeMin:\s*)\d+(\s*m_fontSizeMax:\s*)\d+'
        content = re.sub(tmp_min_max, r'\g<1>36\g<2>64', content)

    # 5. Update Material 1559106767 in Gemi.unity
    mat_pattern = r'(--- !u!21 &1559106767[\s\S]*?_OutlineWidth:\s*)[0-9.]+'
    content = re.sub(mat_pattern, r'\g<1>0.28', content)

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f'Successfully updated scene: {filepath}')


if __name__ == '__main__':
    update_prefab('Assets/Prefabs/Container_UI.prefab')
    update_scene('Assets/Scenes/Gemi.unity')
