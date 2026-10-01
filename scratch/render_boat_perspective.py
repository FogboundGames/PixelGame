import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

def render_comparison():
    # 1. Load OBJ
    obj_path = r"Assets\Kenney\kenney_watercraft-pack\Models\OBJ format\boat-house-a.obj"
    verts = []
    vts = []
    faces = [] # (v_idx, vt_idx)

    with open(obj_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"): continue
            parts = line.split()
            if parts[0] == "v":
                verts.append([float(parts[1]), float(parts[2]), float(parts[3])])
            elif parts[0] == "vt":
                vts.append([float(parts[1]), float(parts[2])])
            elif parts[0] == "f":
                poly = []
                for p in parts[1:]:
                    vals = p.split("/")
                    v_i = int(vals[0]) - 1
                    vt_i = int(vals[1]) - 1 if len(vals) > 1 and vals[1] else 0
                    poly.append((v_i, vt_i))
                faces.append(poly)

    verts = np.array(verts)

    # Load textures
    tex_smart = Image.open("scratch/test_boat_colors/procedural_64_pink.png").convert("RGB")
    tex_smart_arr = np.array(tex_smart)
    tw, th = tex_smart.size

    # Flat pink texture for OLD
    flat_pink = np.full((th, tw, 3), [200, 10, 100], dtype=np.uint8)

    # Canvas
    w, h = 800, 480
    out_img = Image.new("RGB", (w, h), (18, 55, 95))
    draw = ImageDraw.Draw(out_img)

    # Lighting direction (similar to Directional Light in scene)
    light_dir = np.array([-0.3, 0.8, -0.5])
    light_dir = light_dir / np.linalg.norm(light_dir)

    def render_model(center_x, center_y, tilt_x_deg, tex_arr, show_old_huge_text=False):
        theta = math.radians(tilt_x_deg)
        # Rotation around X
        rx = np.array([
            [1, 0, 0],
            [0, math.cos(theta), -math.sin(theta)],
            [0, math.sin(theta), math.cos(theta)]
        ])

        # Rotate vertices
        rot_verts = (rx @ verts.T).T

        # Sort faces by average depth (Z) for painter's algorithm
        face_depths = []
        for poly in faces:
            avg_z = sum(rot_verts[v_i, 2] for v_i, vt_i in poly) / len(poly)
            face_depths.append(avg_z)

        sort_idx = np.argsort(face_depths) # back to front

        # Scale and project
        scale = 55.0
        proj_2d = rot_verts[:, :2] * np.array([scale, -scale]) + np.array([center_x, center_y])

        # Soft shadow under boat
        shadow_w = 110
        shadow_h = 160 if abs(tilt_x_deg) < 40 else 90
        shadow_box = [center_x - shadow_w//2, center_y - shadow_h//2 + 25, center_x + shadow_w//2, center_y + shadow_h//2 + 25]
        draw.ellipse(shadow_box, fill=(10, 32, 60))

        for fi in sort_idx:
            poly = faces[fi]
            # Normal calculation for lighting
            p0 = rot_verts[poly[0][0]]
            p1 = rot_verts[poly[1][0]]
            p2 = rot_verts[poly[2][0]]
            n = np.cross(p1 - p0, p2 - p0)
            norm = np.linalg.norm(n)
            if norm > 1e-6:
                n = n / norm
            else:
                n = np.array([0, 1, 0])

            # Backface culling in orthographic: n[2] must face camera (-Z in this frame)
            if n[2] > 0.05: continue # facing away

            # Sample texture at average UV
            avg_u = sum(vts[vt_i][0] for v_i, vt_i in poly) / len(poly)
            avg_v = sum(vts[vt_i][1] for v_i, vt_i in poly) / len(poly)
            px = int(np.clip(avg_u * tw, 0, tw - 1))
            py = int(np.clip((1.0 - avg_v) * th, 0, th - 1))
            base_col = tex_arr[py, px].astype(float)

            # Cel lighting
            dot = max(0.0, np.dot(n, light_dir))
            # Cel bands
            if dot > 0.5:
                shade = 1.15 # highlight
            elif dot > 0.2:
                shade = 0.95
            else:
                shade = 0.70 # shadow

            # Plastic top light
            top_light = max(0.0, n[1]) * 0.25
            col = np.clip(base_col * shade + top_light * 255.0, 0, 255).astype(int)

            poly_pts = [tuple(proj_2d[v_i]) for v_i, vt_i in poly]
            if len(poly_pts) >= 3:
                draw.polygon(poly_pts, fill=tuple(col), outline=tuple((col * 0.75).astype(int)))

        # Draw number badge
        if show_old_huge_text:
            # Giant text covering entire boat
            draw.text((center_x - 45, center_y - 45), "16", fill=(255, 255, 255), stroke_width=6, stroke_fill=(20, 20, 20))
        else:
            # Elegant hero badge on cabin roof
            draw.text((center_x - 18, center_y - 25), "16", fill=(255, 255, 255), stroke_width=3, stroke_fill=(20, 20, 20))

    # Left: Old (Flat -60 deg tilt, flat solid pink, huge text)
    draw.text((70, 25), "ÖNCEKİ DURUM (-60° Dik Eğim, Düz Pembe, Dev Text)", fill=(240, 160, 160))
    render_model(230, 250, -60, flat_pink, show_old_huge_text=True)

    # Right: New (-28 deg Isometric Tilt, Smart Palette, 3D Depth, Clean Badge)
    draw.text((450, 25), "YENİ PERSPEKTİF (-28° Pikselart Uyumu & 3B Derinlik)", fill=(140, 255, 210))
    render_model(600, 250, -28, tex_smart_arr, show_old_huge_text=False)

    out_img.save("scratch/test_boat_colors/comparison_render.png")
    print("Rendered comparison successfully!")

if __name__ == "__main__":
    render_comparison()
