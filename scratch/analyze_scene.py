import re

scene_path = r"Assets\Scenes\SampleScene.unity"

with open(scene_path, "r", encoding="utf-8", errors="ignore") as f:
    content = f.read()

# Split into YAML documents
docs = content.split("--- !u!")

game_objects = {} # fileID -> name
transforms = {} # fileID -> {go_id, father_id, children: []}
components = {} # go_id -> list of component names/types
canvases = []

for doc in docs:
    lines = doc.splitlines()
    if not lines:
        continue
    header = lines[0] # e.g. "1 &1417467672" or "4 &1417467673"
    m = re.match(r"^(\d+)\s+&(-?\d+)", header)
    if not m:
        continue
    type_id, file_id = m.group(1), m.group(2)
    
    if type_id == "1": # GameObject
        name = "Unknown"
        is_active = 1
        for line in lines:
            if line.startswith("  m_Name:"):
                name = line.split(":", 1)[1].strip()
            elif line.startswith("  m_IsActive:"):
                is_active = line.split(":", 1)[1].strip()
        game_objects[file_id] = {"name": name, "active": is_active}
        
    elif type_id in ("4", "224"): # Transform or RectTransform
        go_id = None
        father_id = None
        children = []
        in_children = False
        for line in lines:
            line_str = line.strip()
            if line_str.startswith("m_GameObject:"):
                m_go = re.search(r"fileID:\s*(-?\d+)", line_str)
                if m_go:
                    go_id = m_go.group(1)
            elif line_str.startswith("m_Father:"):
                m_fa = re.search(r"fileID:\s*(-?\d+)", line_str)
                if m_fa:
                    father_id = m_fa.group(1)
            elif line_str.startswith("m_Children:"):
                in_children = True
            elif in_children:
                if line_str.startswith("- {fileID:"):
                    m_ch = re.search(r"fileID:\s*(-?\d+)", line_str)
                    if m_ch:
                        children.append(m_ch.group(1))
                else:
                    in_children = False
        transforms[file_id] = {"go_id": go_id, "father_id": father_id, "children": children}
        
    elif type_id == "223": # Canvas
        canvases.append(file_id)

# Map transform to game object
tr_to_go = {tr_id: data["go_id"] for tr_id, data in transforms.items() if data["go_id"]}
go_to_tr = {data["go_id"]: tr_id for tr_id, data in transforms.items() if data["go_id"]}

# Root transforms are those where father_id is '0' or None
roots = [tr_id for tr_id, data in transforms.items() if not data["father_id"] or data["father_id"] == "0"]

def print_tree(tr_id, depth=0):
    go_id = transforms[tr_id]["go_id"]
    go_info = game_objects.get(go_id, {"name": f"Go_{go_id}", "active": "1"})
    name = go_info["name"]
    active = "" if go_info["active"] == "1" else " (inactive)"
    
    # Don't expand pixel cubes if there are hundreds
    if "Pixel_" in name and depth > 2:
        return
    
    print("  " * depth + f"- {name}{active} [TR:{tr_id}, GO:{go_id}]")
    
    # Count children if pixel cubes
    children = transforms[tr_id]["children"]
    pixel_children = [c for c in children if c in transforms and "Pixel_" in game_objects.get(transforms[c]["go_id"], {}).get("name", "")]
    if len(pixel_children) > 5:
        non_pixels = [c for c in children if c not in pixel_children]
        for c in non_pixels:
            if c in transforms:
                print_tree(c, depth + 1)
        print("  " * (depth + 1) + f"... and {len(pixel_children)} Pixel_* cubes ...")
    else:
        for c in children:
            if c in transforms:
                print_tree(c, depth + 1)

print("=== ALL NAMED GAMEOBJECTS (non-pixel) ===")
for go_id, info in game_objects.items():
    name = info["name"]
    if not name.startswith("Pixel_") and not name.startswith("Dirt_"):
        print(f"GO {go_id}: {name} (active: {info['active']})")

print("\n=== CANVASES ===")
for c in canvases:
    print(f"Canvas: {c}")

