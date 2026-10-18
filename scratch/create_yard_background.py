import os
from PIL import Image, ImageDraw, ImageFilter

def create_ground_background():
    w, h = 1080, 1920
    img = Image.new("RGBA", (w, h), (210, 214, 222, 255))
    draw = ImageDraw.Draw(img)
    
    # 1. Base gradient across the whole canvas
    # Top is slightly cooler, bottom has smooth asphalt/concrete tone
    for y in range(h):
        ratio = y / h
        # Smooth transition from top light gray to bottom pavement gray
        r = int(212 - ratio * 12)
        g = int(216 - ratio * 12)
        b = int(224 - ratio * 10)
        draw.line([(0, y), (w, y)], fill=(r, g, b, 255))
        
    # 2. Concrete pavement joint seams (subtle grid like in Image 1)
    seam_color = (190, 195, 204, 180)
    # Horizontal expansion seams
    for y_seam in [1280, 1580, 1860]:
        draw.line([(0, y_seam), (w, y_seam)], fill=seam_color, width=2)
    # Vertical expansion seams
    for x_seam in [270, 540, 810]:
        draw.line([(x_seam, 1200), (x_seam, h)], fill=seam_color, width=2)
        
    # 3. Yellow safety / road divider line (as seen prominently at the top of the container area in Image 1!)
    # Positioned around Y = 1240 (marking the container staging area)
    line_y = 1240
    line_height = 20
    yellow_color = (245, 180, 15, 255)
    yellow_shadow = (180, 184, 192, 120)
    
    # Soft shadow under yellow line
    draw.rectangle([0, line_y + line_height, w, line_y + line_height + 4], fill=yellow_shadow)
    # Yellow stripe
    draw.rectangle([0, line_y, w, line_y + line_height], fill=yellow_color)
    # Soft bright highlight on top edge of yellow stripe
    draw.line([(0, line_y), (w, line_y)], fill=(255, 210, 60, 255), width=2)

    # Save to GroundBackground_Clean.png
    out_path = "Assets/Kenney/GroundBackground_Clean.png"
    img.save(out_path, "PNG")
    print(f"Saved ground background to {out_path}")
    
    # Also backup BeachBackground_Clean.png and update it so all references automatically pick it up
    backup_path = "Assets/Kenney/BeachBackground_Clean_WaterBackup.png"
    if not os.path.exists(backup_path) and os.path.exists("Assets/Kenney/BeachBackground_Clean.png"):
        import shutil
        shutil.copyfile("Assets/Kenney/BeachBackground_Clean.png", backup_path)
        print(f"Backed up original BeachBackground_Clean to {backup_path}")
    
    img.save("Assets/Kenney/BeachBackground_Clean.png", "PNG")
    print("Updated BeachBackground_Clean.png with waterless ground background!")

if __name__ == "__main__":
    create_ground_background()
