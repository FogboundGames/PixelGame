import os
import re
import uuid

def generate_guid():
    return uuid.uuid4().hex

def create_cube_material(src_path, dst_path, r, g, b, sr, sg, sb, mat_name):
    with open(src_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # replace name
    content = re.sub(r'm_Name:\s*PixelCube_Cartoon', f'm_Name: {mat_name}', content)
    
    # replace base color and color
    content = re.sub(
        r'- _BaseColor:\s*\{r:\s*[^,]+,\s*g:\s*[^,]+,\s*b:\s*[^,]+,\s*a:\s*[^}]+\}',
        f'- _BaseColor: {{r: {r}, g: {g}, b: {b}, a: 1}}',
        content
    )
    content = re.sub(
        r'- _Color:\s*\{r:\s*[^,]+,\s*g:\s*[^,]+,\s*b:\s*[^,]+,\s*a:\s*[^}]+\}',
        f'- _Color: {{r: {r}, g: {g}, b: {b}, a: 1}}',
        content
    )
    content = re.sub(
        r'- _SColor:\s*\{r:\s*[^,]+,\s*g:\s*[^,]+,\s*b:\s*[^,]+,\s*a:\s*[^}]+\}',
        f'- _SColor: {{r: {sr}, g: {sg}, b: {sb}, a: 1}}',
        content
    )
    
    with open(dst_path, 'w', encoding='utf-8') as f:
        f.write(content)
        
    meta_path = dst_path + '.meta'
    if not os.path.exists(meta_path):
        guid = generate_guid()
        meta_content = f"fileFormatVersion: 2\nguid: {guid}\nMaterialImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
        with open(meta_path, 'w', encoding='utf-8') as mf:
            mf.write(meta_content)
    print(f"Created cube material: {dst_path}")

def create_sailor_material(src_path, dst_path, r, g, b, mat_name):
    with open(src_path, 'r', encoding='utf-8') as f:
        content = f.read()
        
    old_name = os.path.splitext(os.path.basename(src_path))[0]
    content = re.sub(rf'm_Name:\s*{old_name}', f'm_Name: {mat_name}', content)
    
    content = re.sub(
        r'- _BaseColor:\s*\{r:\s*[^,]+,\s*g:\s*[^,]+,\s*b:\s*[^,]+,\s*a:\s*[^}]+\}',
        f'- _BaseColor: {{r: {r}, g: {g}, b: {b}, a: 1}}',
        content
    )
    content = re.sub(
        r'- _Color:\s*\{r:\s*[^,]+,\s*g:\s*[^,]+,\s*b:\s*[^,]+,\s*a:\s*[^}]+\}',
        f'- _Color: {{r: {r}, g: {g}, b: {b}, a: 1}}',
        content
    )
    
    with open(dst_path, 'w', encoding='utf-8') as f:
        f.write(content)
        
    meta_path = dst_path + '.meta'
    if not os.path.exists(meta_path):
        guid = generate_guid()
        meta_content = f"fileFormatVersion: 2\nguid: {guid}\nMaterialImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
        with open(meta_path, 'w', encoding='utf-8') as mf:
            mf.write(meta_content)
    print(f"Created sailor material: {dst_path}")

cube_src = 'Assets/Materials/PixelCube_Cartoon.mat'
# 1. Pink
create_cube_material(cube_src, 'Assets/Materials/Creative_Mat_Pink.mat', 1.0, 0.22, 0.44, 0.85, 0.15, 0.35, 'Creative_Mat_Pink')
# 2. Yellow
create_cube_material(cube_src, 'Assets/Materials/Creative_Mat_Yellow.mat', 1.0, 0.82, 0.00, 0.85, 0.65, 0.00, 'Creative_Mat_Yellow')
# 3. Blue
create_cube_material(cube_src, 'Assets/Materials/Creative_Mat_Blue.mat', 0.0, 0.71, 0.95, 0.00, 0.50, 0.75, 'Creative_Mat_Blue')

sailor_team_src = 'Assets/Models/Sailor_Runner/Materials/M_TeamColor.mat'
sailor_navy_src = 'Assets/Models/Sailor_Runner/Materials/M_Navy.mat'

# Pink Sailor
create_sailor_material(sailor_team_src, 'Assets/Materials/Creative_Sailor_Team_Pink.mat', 1.0, 0.22, 0.44, 'Creative_Sailor_Team_Pink')
create_sailor_material(sailor_navy_src, 'Assets/Materials/Creative_Sailor_Stripes_Pink.mat', 1.0, 0.22, 0.44, 'Creative_Sailor_Stripes_Pink')

# Yellow Sailor
create_sailor_material(sailor_team_src, 'Assets/Materials/Creative_Sailor_Team_Yellow.mat', 1.0, 0.82, 0.00, 'Creative_Sailor_Team_Yellow')
create_sailor_material(sailor_navy_src, 'Assets/Materials/Creative_Sailor_Stripes_Yellow.mat', 1.0, 0.82, 0.00, 'Creative_Sailor_Stripes_Yellow')

# Blue Sailor
create_sailor_material(sailor_team_src, 'Assets/Materials/Creative_Sailor_Team_Blue.mat', 0.0, 0.71, 0.95, 'Creative_Sailor_Team_Blue')
create_sailor_material(sailor_navy_src, 'Assets/Materials/Creative_Sailor_Stripes_Blue.mat', 0.0, 0.71, 0.95, 'Creative_Sailor_Stripes_Blue')
