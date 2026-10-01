import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

def render_perspective_showcase():
    # 1. Load boat-house-a OBJ
    obj_path = r"Assets\Kenney\kenney_watercraft-pack\Models\OBJ format\boat-house-a.obj"
    verts, vts, faces = [], [], []
    with open(obj_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"): continue
            parts = line.split()
            if parts[0] == "v": verts.append([float(x) for x in parts[1:]])
            elif parts[0] == "vt": vts.append([float(x) for x in parts[1:]])
            elif parts[0] == "f":
                poly = []
                for p in parts[1:]:
                    vals = p.split("/")
                    poly.append((int(vals[0])-1, int(vals[1])-1 if len(vals)>1 and vals[1] else 0))
                faces.append(poly)

    verts = np.array(verts)[:, :3]

    # 2. Procedural multi-tone palette texture (matching ShipController.cs)
    size = 64
    tile_size = size // 16
    def make_tex(ship_color):
        out = np.zeros((size, size, 3), dtype=np.uint8)
        glass_col = np.array([195, 230, 255], dtype=np.float32)
        rubber_col = np.array([36, 40, 48], dtype=np.float32)
        roof_col = np.array([255, 255, 255], dtype=np.float32) * 0.82 + ship_color * 0.18
        waterline_col = ship_color * 0.82
        for ty in range(16):
            for tx in range(16):
                if tx == 1: col = glass_col
                elif tx == 7: col = rubber_col
                elif tx == 9: col = roof_col
                elif tx == 11: col = waterline_col
                else: col = ship_color
                out[ty*tile_size:(ty+1)*tile_size, tx*tile_size:(tx+1)*tile_size] = col.astype(np.uint8)
        return out

    tex_pink = make_tex(np.array([245, 45, 135], dtype=np.float32))
    flat_pink = np.full((size, size, 3), [245, 45, 135], dtype=np.uint8)

    # Canvas
    W, H = 1200, 680
    img = Image.new("RGB", (W, H), (14, 22, 36))
    draw = ImageDraw.Draw(img)

    # Header banner
    draw.rectangle([0, 0, W, 70], fill=(22, 34, 54))
    draw.text((35, 22), "GEMI PERSPEKTIF & DERINLIK IYILESTIRMESI (PIKSELART ILE %100 UYUM)", fill=(255, 255, 255))

    light_dir = np.array([-0.35, 0.82, -0.45])
    light_dir = light_dir / np.linalg.norm(light_dir)

    def render_boat(cx, cy, tilt_deg, tex_arr, scale=62.0, huge_badge=False, show_shadow=True):
        theta = math.radians(tilt_deg)
        rx = np.array([
            [1, 0, 0],
            [0, math.cos(theta), -math.sin(theta)],
            [0, math.sin(theta), math.cos(theta)]
        ])
        rot_verts = (rx @ verts.T).T

        # Depth sort
        face_depths = []
        for poly in faces:
            avg_z = sum(rot_verts[v_i, 2] for v_i, vt_i in poly) / len(poly)
            face_depths.append(avg_z)
        sort_idx = np.argsort(face_depths)

        proj_2d = rot_verts[:, :2] * np.array([scale, -scale]) + np.array([cx, cy])

        if show_shadow:
            sw = int(120 * (scale / 60.0))
            sh = int((190 if abs(tilt_deg) < 35 else 110) * (scale / 60.0))
            draw.ellipse([cx - sw//2 + 8, cy - sh//2 + 30, cx + sw//2 + 8, cy + sh//2 + 30], fill=(8, 22, 42))

        tw, th = tex_arr.shape[1], tex_arr.shape[0]

        for fi in sort_idx:
            poly = faces[fi]
            p0 = rot_verts[poly[0][0]]
            p1 = rot_verts[poly[1][0]]
            p2 = rot_verts[poly[2][0]]
            n = np.cross(p1 - p0, p2 - p0)
            norm = np.linalg.norm(n)
            n = n / norm if norm > 1e-6 else np.array([0, 1, 0])

            if n[2] > 0.05: continue # backface culling

            avg_u = sum(vts[vt_i][0] for v_i, vt_i in poly) / len(poly)
            avg_v = sum(vts[vt_i][1] for v_i, vt_i in poly) / len(poly)
            px = int(np.clip(avg_u * tw, 0, tw - 1))
            py = int(np.clip((1.0 - avg_v) * th, 0, th - 1))
            base_col = tex_arr[py, px].astype(float)

            dot = max(0.0, np.dot(n, light_dir))
            shade = 1.15 if dot > 0.45 else (0.95 if dot > 0.18 else 0.72)
            top_light = max(0.0, n[1]) * 0.28
            col = np.clip(base_col * shade + top_light * 255.0, 0, 255).astype(int)

            poly_pts = [tuple(proj_2d[v_i]) for v_i, vt_i in poly]
            if len(poly_pts) >= 3:
                draw.polygon(poly_pts, fill=tuple(col), outline=tuple((col * 0.78).astype(int)))

        # Badge
        if huge_badge:
            # Old oversized badge covering whole boat
            bx, by = cx, cy + 5
            draw.ellipse([bx - 38, by - 38, bx + 38, by + 38], fill=(255, 255, 255), outline=(30, 30, 30), width=4)
            draw.ellipse([bx - 32, by - 32, bx + 32, by + 32], fill=(245, 45, 135))
            draw.text((bx - 14, by - 12), "16", fill=(255, 255, 255), stroke_width=2, stroke_fill=(30, 30, 30))
        else:
            # Elegant hero badge on cabin roof
            bx, by = cx, cy - 18
            draw.ellipse([bx - 22, by - 22, bx + 22, by + 22], fill=(255, 255, 255), outline=(30, 30, 30), width=3)
            draw.ellipse([bx - 18, by - 18, bx + 18, by + 18], fill=(245, 45, 135))
            draw.text((bx - 9, by - 8), "16", fill=(255, 255, 255), stroke_width=2, stroke_fill=(30, 30, 30))

    # Water backgrounds for comparison boxes
    # Box 1: Old
    draw.rectangle([50, 100, 560, 620], fill=(16, 48, 82), outline=(40, 70, 110), width=2)
    draw.rectangle([50, 100, 560, 145], fill=(24, 60, 100))
    draw.text((70, 115), "ÖNCEKI: -60° Dik Kuşbakışı, Düz Pembe, Dev Rozet", fill=(255, 140, 140))
    render_boat(305, 360, -60, flat_pink, scale=58.0, huge_badge=True)

    # Annotations for Old
    draw.text((70, 530), "• -60° eğim boyu %50 basıklaştırıyor (2B şerit gibi)", fill=(200, 210, 220))
    draw.text((70, 555), "• Kauçuk tampon, cam ve tavan yok (tek renk pembe)", fill=(200, 210, 220))
    draw.text((70, 580), "• Dev rozet kabini ve tüm gövdeyi tamamen örtüyor", fill=(200, 210, 220))

    # Box 2: New
    draw.rectangle([640, 100, 1150, 620], fill=(16, 52, 90), outline=(0, 200, 170), width=2)
    draw.rectangle([640, 100, 1150, 145], fill=(18, 75, 110))
    draw.text((660, 115), "YENI: -28° İzometrik Perspektif, Akıllı Palet & 3B Derinlik", fill=(100, 255, 210))
    render_boat(895, 360, -28, tex_pink, scale=62.0, huge_badge=False)

    # Annotations for New
    draw.text((660, 530), "• -28° eğim üstteki pikselart küpleriyle %100 uyumlu", fill=(200, 240, 255))
    draw.text((660, 555), "• Grafit kauçuk tampon + gök mavisi cam + beyaz tavan", fill=(200, 240, 255))
    draw.text((660, 580), "• Çatıya oturan orantılı hero badge + gerçekçi su gölgesi", fill=(200, 240, 255))

    out_file = "scratch/perspective_showcase_result.png"
    img.save(out_file)
    print(f"Saved showcase to {out_file} successfully!")

    # Also copy to artifacts dir
    art_dir = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\63c02eea-87da-4b2d-ae5e-b8879530b435"
    if os.path.exists(art_dir):
        art_out = os.path.join(art_dir, "perspective_showcase_result.png")
        img.save(art_out)
        print(f"Copied showcase to artifact: {art_out}")

if __name__ == "__main__":
    render_perspective_showcase()
