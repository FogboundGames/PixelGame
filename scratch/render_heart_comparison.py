import re
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

def hex_to_rgb(h):
    return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))

def render_comparison():
    # Read Gemi.unity cube positions
    scene_path = "Assets/Scenes/Gemi.unity"
    with open(scene_path, "r", encoding="utf-8") as f:
        text = f.read()

    blocks = text.split("--- !u!1001 &")
    cubes = []
    
    # We will infer cube color from texture or position
    # Let's read texture if available
    tex_path = "Assets/Textures/PixelArt_Heart.png"
    # Fallback palette for Level_02_Heart:
    # 0: pink #ff3a9b
    # 1: dark rim #c80a64
    # 2: highlight #ffc3e6
    c_pink = (255, 36, 148)
    c_rim = (200, 10, 100)
    c_highlight = (255, 205, 235)
    
    # Try reading texture
    try:
        ref_img = Image.open(r"C:\Users\ezgid\.gemini\antigravity-ide\brain\6c76da88-bba0-4dd5-8054-826797c4d077\.user_uploaded\media_1790718273455.png")
    except:
        ref_img = None

    for b in blocks[1:]:
        name_m = re.search(r"propertyPath: m_Name\s+value:\s*(Pixel_(\d+)_(\d+))", b)
        if name_m:
            gx = int(name_m.group(2))
            gy = int(name_m.group(3))
            pos_x_m = re.search(r"propertyPath: m_LocalPosition\.x\s+value:\s*([^\r\n]+)", b)
            pos_y_m = re.search(r"propertyPath: m_LocalPosition\.y\s+value:\s*([^\r\n]+)", b)
            px = float(pos_x_m.group(1)) if pos_x_m else 0
            py = float(pos_y_m.group(1)) if pos_y_m else 0
            cubes.append((gx, gy, px, py))

    # Determine colors based on grid position
    # Heart shape grid logic:
    # gx 2..21, gy 3..20
    def get_color(gx, gy):
        # Highlight at top-left:
        # gx in [4, 5], gy in [17, 18, 19]
        if (gx == 4 and gy in [16, 17, 18]) or (gx == 5 and gy in [17, 18]) or (gx == 6 and gy == 18):
            return c_highlight
        # Border cubes:
        # We can check if it's on the edge of the heart
        return c_pink

    # Render image canvas
    W, H = 700, 650
    img = Image.new("RGBA", (W, H), (252, 226, 188, 255)) # Sand background
    draw = ImageDraw.Draw(img)

    # Pixel transform: center (350, 320), scale factor
    scale = 105.0 # pixels per world unit
    cx, cy = 350, 325

    # Sort cubes for isometric / 25 deg tilt (render top to bottom: higher gy first or lower gy first?
    # When tilted 25 deg forward, top is further back, bottom is closer -> draw from top (gy high) to bottom (gy low))
    cubes.sort(key=lambda c: -c[1])

    # First draw soft shadow under the heart
    shadow_img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow_img)
    cube_screen_size = 0.25988 * scale

    for gx, gy, px, py in cubes:
        sx = cx + px * scale
        sy = cy - py * scale + 15 # slight downward offset for shadow
        s_draw.rectangle([sx - cube_screen_size/2, sy - cube_screen_size/2, sx + cube_screen_size/2, sy + cube_screen_size/2], fill=(40, 20, 10, 100))
    
    shadow_img = shadow_img.filter(ImageFilter.GaussianBlur(12))
    img.paste(shadow_img, (0, 0), shadow_img)

    # Border detection for accurate coloring
    cube_coords = set((gx, gy) for gx, gy, _, _ in cubes)
    
    for gx, gy, px, py in cubes:
        # Check if border
        is_border = any((gx+dx, gy+dy) not in cube_coords for dx, dy in [(-1,0),(1,0),(0,-1),(0,1)])
        col = c_rim if is_border else get_color(gx, gy)
        
        sx = cx + px * scale
        sy = cy - py * scale
        
        # Snug cube width
        s = cube_screen_size
        
        # Cube front/top face with bevel
        # Base face
        draw.rectangle([sx - s/2, sy - s/2, sx + s/2, sy + s/2], fill=col)
        
        # Bevel highlight (top and left edge)
        r, g, b = col
        high_col = (min(255, int(r*1.15)), min(255, int(g*1.15)), min(255, int(b*1.15)), 255)
        dark_col = (int(r*0.75), int(g*0.75), int(b*0.75), 255)
        
        # Top-left bevel highlight
        draw.line([sx - s/2, sy - s/2, sx + s/2 - 1, sy - s/2], fill=high_col, width=1)
        draw.line([sx - s/2, sy - s/2, sx - s/2, sy + s/2 - 1], fill=high_col, width=1)
        
        # Bottom-right bevel shadow (crease)
        draw.line([sx - s/2, sy + s/2, sx + s/2, sy + s/2], fill=dark_col, width=1)
        draw.line([sx + s/2, sy - s/2, sx + s/2, sy + s/2], fill=dark_col, width=1)

    out_path = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\6c76da88-bba0-4dd5-8054-826797c4d077\heart_snug_spacing_calibrated.png"
    img.save(out_path)
    print(f"Rendered comparison image to {out_path}")

if __name__ == "__main__":
    render_comparison()
