import re

def calibrate_cubes_in_scene():
    scene_path = "Assets/Scenes/Gemi.unity"
    with open(scene_path, "r", encoding="utf-8") as f:
        text = f.read()

    # Parameters
    step = 0.25988 # Exact uniform mathematical step
    cube_scale = 0.2605 # Snug physical contact preventing any sand background peeking through
    spacing = 0.0 # 0% physical gap (snug bevel-to-bevel contact like Image 3 reference)
    
    # Heart center bounds in grid:
    # gx: 2..21 (center = 11.5)
    # gy: 3..20 (center = 11.5)
    center_x = 0.046555
    center_y = -0.23938
    
    # We update every PrefabInstance corresponding to Pixel_X_Y
    # Find all PrefabInstance blocks
    blocks = text.split("--- !u!1001 &")
    updated_blocks = [blocks[0]]
    
    cube_count = 0
    for block in blocks[1:]:
        name_m = re.search(r"propertyPath: m_Name\s+value:\s*(Pixel_(\d+)_(\d+))", block)
        if name_m:
            gx = int(name_m.group(2))
            gy = int(name_m.group(3))
            
            target_x = center_x + (gx - 11.5) * step
            target_y = center_y + (gy - 11.5) * step
            
            # Replace localPosition.x
            block = re.sub(
                r"(propertyPath: m_LocalPosition\.x\s+value:\s*)([^\r\n]+)",
                rf"\g<1>{target_x:.7f}",
                block
            )
            # Replace localPosition.y
            block = re.sub(
                r"(propertyPath: m_LocalPosition\.y\s+value:\s*)([^\r\n]+)",
                rf"\g<1>{target_y:.7f}",
                block
            )
            # Ensure localScale is snug cube_scale
            block = re.sub(
                r"(propertyPath: m_LocalScale\.x\s+value:\s*)([^\r\n]+)",
                rf"\g<1>{cube_scale:.5f}",
                block
            )
            block = re.sub(
                r"(propertyPath: m_LocalScale\.y\s+value:\s*)([^\r\n]+)",
                rf"\g<1>{cube_scale:.5f}",
                block
            )
            block = re.sub(
                r"(propertyPath: m_LocalScale\.z\s+value:\s*)([^\r\n]+)",
                rf"\g<1>{cube_scale:.5f}",
                block
            )
            cube_count += 1
            
        updated_blocks.append(block)
        
    new_text = "--- !u!1001 &".join(updated_blocks)
    
    # Also update PixelArtGenerator values in scene
    new_text = re.sub(
        r"(m_CubeSpacing:\s*)([^\r\n]+)",
        rf"\g<1>0",
        new_text
    )
    new_text = re.sub(
        r"(m_CubeSpacingX:\s*)([^\r\n]+)",
        rf"\g<1>0",
        new_text
    )
    
    with open(scene_path, "w", encoding="utf-8") as f:
        f.write(new_text)
        
    print(f"Successfully calibrated {cube_count} cubes in {scene_path} with step={step:.6f}, scale={cube_scale:.5f} (snug seamless grid)")

    # Also update Level_02_Heart.asset
    level_path = "Assets/Levels/Level_02_Heart.asset"
    with open(level_path, "r", encoding="utf-8") as f:
        level_text = f.read()
    level_text = re.sub(r"(m_CubeSpacing:\s*)([^\r\n]+)", r"\g<1>0", level_text)
    level_text = re.sub(r"(m_CubeSpacingX:\s*)([^\r\n]+)", r"\g<1>0", level_text)
    with open(level_path, "w", encoding="utf-8") as f:
        f.write(level_text)
    print(f"Successfully updated {level_path} with m_CubeSpacing=0 and m_CubeSpacingX=0")

if __name__ == "__main__":
    calibrate_cubes_in_scene()
