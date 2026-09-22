import math
from PIL import Image, ImageDraw

def render_preview():
    # Load KawaiiCube.obj
    obj_path = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Models\KawaiiCube.obj"
    verts = []
    faces_body = []
    faces_face = []
    current_group = None

    with open(obj_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"): continue
            parts = line.split()
            if parts[0] == "v":
                verts.append([float(parts[1]), float(parts[2]), float(parts[3])])
            elif parts[0] == "g":
                current_group = parts[1]
            elif parts[0] == "f":
                idx = [int(p.split("/")[0]) - 1 for p in parts[1:]]
                if current_group == "01_Body":
                    faces_body.append(idx)
                elif current_group == "02_Face":
                    faces_face.append(idx)

    # Setup rendering
    w, h = 600, 240
    img = Image.new("RGB", (w, h), (26, 28, 38))
    draw = ImageDraw.Draw(img)

    # Draw conveyor belt
    draw.rectangle([0, 150, w, 220], fill=(36, 38, 48))
    draw.line([0, 150, w, 150], fill=(55, 58, 72), width=3)
    draw.line([0, 220, w, 220], fill=(20, 21, 28), width=3)

    # Draw conveyor belt arrows pointing left
    for ax in range(50, w, 90):
        draw.polygon([(ax, 185), (ax + 18, 175), (ax + 18, 195)], fill=(65, 70, 88))

    # Camera & tilt: tilt 15 degrees around X
    tilt_rad = math.radians(15.0)
    cos_t = math.cos(tilt_rad)
    sin_t = math.sin(tilt_rad)

    # Light direction: from top-left front
    lx, ly, lz = -0.3, 0.8, -0.6
    ll = math.sqrt(lx*lx + ly*ly + lz*lz)
    lx, ly, lz = lx/ll, ly/ll, lz/ll

    def render_cube(center_x, center_y, scale, base_rgb):
        # Sort faces back to front
        all_faces = []
        for f in faces_body:
            all_faces.append((f, "body"))
        for f in faces_face:
            all_faces.append((f, "face"))

        # Transform vertices
        tverts = []
        for v in verts:
            x, y, z = v[0], v[1], v[2]
            # Tilt around X: X remains, Y and Z rotate
            # tilt down: camera looks down -> +Y tilts away (+Z), -Z tilts up (+Y)
            yt = y * cos_t - z * sin_t
            zt = y * sin_t + z * cos_t
            tverts.append((x, yt, zt))

        face_depths = []
        for f, mtl in all_faces:
            # Average zt (higher zt is deeper away)
            # In our system -Z is towards camera, so lowest zt is closest to camera
            avg_z = sum(tverts[idx][2] for idx in f) / len(f)
            face_depths.append((avg_z, f, mtl))

        # Sort back to front (largest z first, smallest z last)
        face_depths.sort(key=lambda item: -item[0])

        for avg_z, f, mtl in face_depths:
            v0 = tverts[f[0]]
            v1 = tverts[f[1]]
            v2 = tverts[f[2]]

            # Normal in transformed space
            ax, ay, az = v1[0] - v0[0], v1[1] - v0[1], v1[2] - v0[2]
            bx, by, bz = v2[0] - v0[0], v2[1] - v0[1], v2[2] - v0[2]
            nx = ay * bz - az * by
            ny = az * bx - ax * bz
            nz = ax * by - ay * bx
            nl = math.sqrt(nx*nx + ny*ny + nz*nz)
            if nl < 1e-6: continue
            nx, ny, nz = nx/nl, ny/nl, nz/nl

            # Backface culling: camera looks along +Z transformed axis, so visible faces have nz < 0
            if nz >= 0.05 and mtl != "face": continue

            # Lighting
            dot = max(0.0, nx * lx + ny * ly + nz * lz)
            # Ambient + diffuse
            intensity = 0.45 + 0.55 * dot

            # Specular
            # View vector is (0, 0, -1)
            # Halfway vector
            hx, hy, hz = lx, ly, lz - 1.0
            hl = math.sqrt(hx*hx + hy*hy + hz*hz)
            hx, hy, hz = hx/hl, hy/hl, hz/hl
            spec = max(0.0, nx * hx + ny * hy + nz * hz) ** 28

            if mtl == "body":
                r = int(min(255, base_rgb[0] * intensity + 255 * spec * 0.75))
                g = int(min(255, base_rgb[1] * intensity + 255 * spec * 0.75))
                b = int(min(255, base_rgb[2] * intensity + 255 * spec * 0.75))
                color = (r, g, b)
            else:
                # Face: deep glossy black with slight spec
                f_dot = 0.08 + 0.08 * dot + 0.3 * spec
                color = (int(255 * f_dot), int(255 * f_dot * 1.1), int(255 * f_dot * 1.3))

            pts = []
            for idx in f:
                vx, vy, vz = tverts[idx]
                sx = center_x + vx * scale
                # Screen Y is inverted (top is 0)
                sy = center_y - vy * scale
                pts.append((sx, sy))

            draw.polygon(pts, fill=color)

    # Render Blue and Yellow cubes on conveyor belt
    render_cube(180, 190, 140, (0, 175, 255))
    render_cube(420, 190, 140, (255, 205, 0))

    out_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\kawaii_cubes_rendered_preview.png"
    img.save(out_path)
    print("Rendered preview saved at:", out_path)

if __name__ == "__main__":
    render_preview()
