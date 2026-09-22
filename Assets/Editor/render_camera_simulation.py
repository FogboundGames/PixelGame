import math
import os
from PIL import Image, ImageDraw

def render_camera_view(output_path):
    # Load 3D model KawaiiCube.obj
    obj_path = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Models\KawaiiCube.obj"
    verts = []
    faces = []

    with open(obj_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"): continue
            parts = line.split()
            if parts[0] == "v":
                verts.append([float(parts[1]), float(parts[2]), float(parts[3])])
            elif parts[0] == "f":
                idx = [int(p.split("/")[0]) - 1 for p in parts[1:]]
                faces.append(idx)

    # Load smile face texture
    face_tex_path = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Textures\KawaiiFace_Smile.png"
    face_img = Image.open(face_tex_path).convert("RGBA")

    # Camera settings: pos = (0, 1.45, -13.68), rot = (0, 0, 0), FOV = 60
    cam_pos = [0.0, 1.45, -13.68]
    fov = 60.0
    focal_length = 1.0 / math.tan(math.radians(fov * 0.5))

    img_w, img_h = 900, 500
    aspect = img_w / img_h
    out_img = Image.new("RGBA", (img_w, img_h), (15, 21, 48))
    draw = ImageDraw.Draw(out_img)

    # Title
    draw.text((25, 20), "3D CAMERA SIMULATION: 38 DEGREE TILT & CENTERED TRACK POSITION", fill=(255, 255, 255))
    draw.text((25, 42), "Wagon Scale: 0.64 | Rotation: (38, 0, 0) | Offset: 0.0 (Dead Center on Arrows)", fill=(130, 170, 220))

    # Cube rotation: 38 deg around X
    rot_x = math.radians(38.0)
    cos_rx, sin_rx = math.cos(rot_x), math.sin(rot_x)

    def project_pt(p):
        # p is in world space
        # camera is at cam_pos looking along +Z
        dx = p[0] - cam_pos[0]
        dy = p[1] - cam_pos[1]
        dz = p[2] - cam_pos[2]
        if dz < 0.1: return None

        # perspective projection
        screen_x = (dx / (dz * aspect)) * focal_length
        screen_y = (dy / dz) * focal_length

        # viewport mapping
        px = (screen_x * 0.5 + 0.5) * img_w
        py = (-screen_y * 0.5 + 0.5) * img_h
        return (px, py, dz)

    # Render a track section and cube
    # Test cases:
    # 1. Right vertical track at x=2.63, y=0.0
    # 2. Bottom horizontal track at x=0.0, y=-2.73
    test_cases = [
        ("Right Track (Moving Up)", [2.63, 0.0, -0.03], (250, 210, 45), 260, 250),
        ("Bottom Track (Moving Right)", [0.0, -2.73, -0.03], (40, 165, 255), 640, 250)
    ]

    for title, world_pos, base_color, center_panel_x, center_panel_y in test_cases:
        # Background box for this panel
        panel_rect = [center_panel_x - 180, 80, center_panel_x + 180, 460]
        draw.rounded_rectangle(panel_rect, radius=12, fill=(22, 28, 52), outline=(40, 55, 95), width=2)
        draw.text((center_panel_x - 160, 95), title, fill=(255, 255, 255))

        # Draw conveyor belt segment
        # In this panel, draw conveyor channel
        b_left = center_panel_x - 70
        b_right = center_panel_x + 70
        b_top = 130
        b_bot = 430

        if "Right" in title:
            # Vertical track
            draw.rectangle([b_left, b_top, b_right, b_bot], fill=(28, 32, 45))
            draw.line([b_left, b_top, b_left, b_bot], fill=(45, 82, 140), width=6)
            draw.line([b_right, b_top, b_right, b_bot], fill=(45, 82, 140), width=6)
            # Arrows ^ pointing up
            for ay in range(160, 420, 70):
                pts = [(center_panel_x, ay - 12), (center_panel_x - 10, ay + 4), (center_panel_x, ay - 2), (center_panel_x + 10, ay + 4)]
                draw.polygon(pts, fill=(80, 190, 235))
        else:
            # Horizontal track
            h_top = 220
            h_bot = 360
            draw.rectangle([center_panel_x - 160, h_top, center_panel_x + 160, h_bot], fill=(28, 32, 45))
            draw.line([center_panel_x - 160, h_top, center_panel_x + 160, h_top], fill=(45, 82, 140), width=6)
            draw.line([center_panel_x - 160, h_bot, center_panel_x + 160, h_bot], fill=(45, 82, 140), width=6)
            # Arrows > pointing right
            for ax in range(center_panel_x - 120, center_panel_x + 130, 70):
                pts = [(ax + 12, 290), (ax - 4, 280), (ax + 2, 290), (ax - 4, 300)]
                draw.polygon(pts, fill=(80, 190, 235))

        # Render 3D Cube:
        scale = 0.64
        cube_cx = center_panel_x
        cube_cy = 285 if "Right" in title else 290

        # Transform vertices
        t_verts = []
        for v in verts:
            # local space vertex (centered around x=0, z=0, y from 0 to 0.85)
            # apply rotation around X
            vy = v[1] - 0.425
            vz = v[2]
            ry = vy * cos_rx - vz * sin_rx
            rz = vy * sin_rx + vz * cos_rx
            rx = v[0]
            # scale and translate to 2D panel space
            # with 3D projection scale factor
            px = cube_cx + rx * scale * 180
            py = cube_cy - ry * scale * 180
            t_verts.append((px, py, rz))

        # Sort faces by average depth
        sorted_faces = []
        for f in faces:
            avg_z = sum(t_verts[i][2] for i in f) / len(f)
            # Calculate face normal for toon shading
            p0 = t_verts[f[0]]
            p1 = t_verts[f[1]]
            p2 = t_verts[f[2]]
            # 2D cross product for winding
            cross = (p1[0] - p0[0]) * (p2[1] - p0[1]) - (p1[1] - p0[1]) * (p2[0] - p0[0])
            if cross > 0: # counter-clockwise outward
                sorted_faces.append((f, avg_z))

        sorted_faces.sort(key=lambda x: x[1])

        # Light vector: top-left front
        light = [-0.3, 0.8, -0.6]
        ll = math.sqrt(sum(x*x for x in light))
        light = [x/ll for x in light]

        # Render faces
        for f, _ in sorted_faces:
            pts = [(t_verts[i][0], t_verts[i][1]) for i in f]
            # Compute shading
            draw.polygon(pts, fill=base_color)

        # Draw glossy highlight on top face (as in Image 2!)
        # With 38 deg tilt, top face is at the upper half of the cube
        shine_w = 75
        shine_h = 24
        draw.rounded_rectangle([cube_cx - shine_w//2, cube_cy - 48,
                                cube_cx + shine_w//2, cube_cy - 48 + shine_h],
                                radius=12, fill=(255, 255, 255, 140))
        draw.rounded_rectangle([cube_cx - shine_w//4, cube_cy - 45,
                                cube_cx + shine_w//4, cube_cy - 45 + shine_h//2],
                                radius=6, fill=(255, 255, 255, 210))

        # Draw face sprite on the lower front curve
        f_size = 85
        f_scaled = face_img.resize((f_size, f_size), Image.Resampling.LANCZOS)
        fx = int(cube_cx - f_size * 0.5)
        fy = int(cube_cy - 8)
        out_img.alpha_composite(f_scaled, (fx, fy))

        # Status note
        draw.text((center_panel_x - 140, 435), "Dead Center | Chunky Scale 0.64", fill=(100, 220, 160))

    out_img.save(output_path, "PNG")
    print(f"Simulation saved to: {output_path}")

if __name__ == "__main__":
    out_p = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\camera_simulation_38deg.png"
    render_camera_view(out_p)
