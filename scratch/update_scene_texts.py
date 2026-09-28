import re

scene_path = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Scenes\Gemi.unity"

with open(scene_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update all Badge_Text font sizes and sizeDeltas
# In the Text components under Badge_Text:
# m_FontSize: 50 -> m_FontSize: 100
# m_MaxSize: 40 / 50 -> m_MaxSize: 100

# Let's count how many m_FontSize: 50
count_font = content.count("    m_FontSize: 50")
print(f"Found {count_font} instances of m_FontSize: 50")

content = content.replace("    m_FontSize: 50", "    m_FontSize: 100")
content = content.replace("    m_MaxSize: 40", "    m_MaxSize: 100")
content = content.replace("    m_MaxSize: 50", "    m_MaxSize: 100")

# For Badge_Text RectTransforms:
# m_Name: Badge_Text ... m_SizeDelta: {x: 120, y: 70} -> {x: 240, y: 130}
pattern_badge = r"(m_Name: Badge_Text[\s\S]*?m_SizeDelta: )\{x: 120, y: 70\}"
content, count_delta = re.subn(pattern_badge, r"\g<1>{x: 240, y: 130}", content)
print(f"Updated {count_delta} Badge_Text sizeDeltas")

# For Ship_Capacity_Canvas RectTransforms:
# Replace the tilted rotation:
# m_LocalRotation: {x: 0.5591929, y: -0, z: -0, w: 0.8290376} -> {x: 0, y: 0, z: 0, w: 1}
count_rot = content.count("  m_LocalRotation: {x: 0.5591929, y: -0, z: -0, w: 0.8290376}")
print(f"Found {count_rot} tilted canvas rotations")
content = content.replace("  m_LocalRotation: {x: 0.5591929, y: -0, z: -0, w: 0.8290376}", "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}")

with open(scene_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Saved updated Gemi.unity successfully!")
