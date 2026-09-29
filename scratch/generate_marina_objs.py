import os
import math

os.makedirs("Assets/Models/Marina", exist_ok=True)

def write_obj(filepath, vertices, normals, uvs, faces):
    with open(filepath, "w", encoding="utf-8") as f:
        f.write("# Generated Marina 3D Model for Konsept 2\n")
        for v in vertices:
            f.write(f"v {v[0]:.5f} {v[1]:.5f} {v[2]:.5f}\n")
        for vt in uvs:
            f.write(f"vt {vt[0]:.5f} {vt[1]:.5f}\n")
        for vn in normals:
            f.write(f"vn {vn[0]:.5f} {vn[1]:.5f} {vn[2]:.5f}\n")
        for face in faces:
            # 1-indexed in OBJ: v/vt/vn
            f_str = " ".join(f"{idx[0]}/{idx[1]}/{idx[2]}" for idx in face)
            f.write(f"f {f_str}\n")
    print(f"Saved OBJ to {filepath}")

# ==========================================
# 1. BUOY.OBJ (Low-Profile Nautical Buoy)
# ==========================================
def generate_buoy_obj():
    verts = []
    norms = []
    uvs = []
    faces = []

    radial_segs = 18
    height_segs = 12
    radius = 0.12
    height = 0.18

    # Rounded floating buoy body
    for y in range(height_segs + 1):
        v = y / height_segs
        # Shape: slightly flatter bottom, rounded bulbous middle, dome top
        angle_v = (v - 0.5) * math.pi # -pi/2 to +pi/2
        r = radius * (math.cos(angle_v * 0.72) ** 0.8)
        pos_y = math.sin(angle_v) * (height * 0.5)

        for x in range(radial_segs + 1):
            u = x / radial_segs
            phi = u * math.pi * 2.0
            pos_x = math.cos(phi) * r
            pos_z = math.sin(phi) * r

            verts.append((pos_x, pos_y, pos_z))
            nx = math.cos(phi)
            ny = math.sin(angle_v) * 0.6
            nz = math.sin(phi)
            length = math.sqrt(nx*nx + ny*ny + nz*nz) or 1.0
            norms.append((nx/length, ny/length, nz/length))
            uvs.append((u, v))

    for y in range(height_segs):
        for x in range(radial_segs):
            c0 = y * (radial_segs + 1) + x + 1
            c1 = c0 + radial_segs + 1
            c2 = c1 + 1
            c3 = c0 + 1
            # Triangle 1 & 2 (1-indexed)
            faces.append([(c0, c0, c0), (c1, c1, c1), (c3, c3, c3)])
            faces.append([(c3, c3, c3), (c1, c1, c1), (c2, c2, c2)])

    # Top eyelet ring (torus for chain hook)
    ring_segs = 14
    tube_segs = 8
    ring_r = 0.038
    tube_r = 0.010
    ring_center_y = height * 0.5 + ring_r
    base_idx = len(verts)

    for r in range(ring_segs):
        phi = r / ring_segs * math.pi * 2.0
        cx = math.cos(phi) * ring_r
        cy = ring_center_y + math.sin(phi) * ring_r
        cz = 0.0

        for t in range(tube_segs):
            theta = t / tube_segs * math.pi * 2.0
            ox = math.cos(phi) * math.cos(theta) * tube_r
            oy = math.sin(phi) * math.cos(theta) * tube_r
            oz = math.sin(theta) * tube_r

            verts.append((cx + ox, cy + oy, cz + oz))
            l = math.sqrt(ox*ox + oy*oy + oz*oz) or 1.0
            norms.append((ox/l, oy/l, oz/l))
            uvs.append((r / ring_segs, 0.15))

    for r in range(ring_segs):
        next_r = (r + 1) % ring_segs
        for t in range(tube_segs):
            next_t = (t + 1) % tube_segs
            i0 = base_idx + r * tube_segs + t + 1
            i1 = base_idx + next_r * tube_segs + t + 1
            i2 = base_idx + next_r * tube_segs + next_t + 1
            i3 = base_idx + r * tube_segs + next_t + 1

            faces.append([(i0, i0, i0), (i1, i1, i1), (i2, i2, i2)])
            faces.append([(i0, i0, i0), (i2, i2, i2), (i3, i3, i3)])

    write_obj("Assets/Models/Marina/Buoy.obj", verts, norms, uvs, faces)

# ==========================================
# 2. CHAIN.OBJ (Interlocking Links Grid)
# ==========================================
def generate_chain_obj(length=1.04, link_count=9, sag=0.015):
    verts = []
    norms = []
    uvs = []
    faces = []

    link_len = (length / (link_count - 1)) * 0.95
    link_radius = 0.024
    wire_radius = 0.0075

    for idx in range(link_count):
        t = idx / (link_count - 1)
        z = t * length
        sag_y = -math.sin(t * math.pi) * sag
        is_vertical = (idx % 2 == 1)

        base_idx = len(verts)
        ring_segs = 14
        tube_segs = 6
        half_len = link_len * 0.5

        for r in range(ring_segs):
            angle = r / ring_segs * math.pi * 2.0
            lz = math.sin(angle) * half_len
            lx = math.cos(angle) * link_radius

            for tb in range(tube_segs):
                tube_angle = tb / tube_segs * math.pi * 2.0
                ox = math.cos(angle) * math.cos(tube_angle) * wire_radius
                oy = math.sin(tube_angle) * wire_radius
                oz = math.sin(angle) * math.cos(tube_angle) * wire_radius

                pt_x = lx + ox
                pt_y = oy
                pt_z = lz + oz

                norm_x = math.cos(angle) * math.cos(tube_angle)
                norm_y = math.sin(tube_angle)
                norm_z = math.sin(angle) * math.cos(tube_angle)

                if is_vertical:
                    # 90 deg around Z
                    pt_x, pt_y = -pt_y, pt_x
                    norm_x, norm_y = -norm_y, norm_x

                verts.append((pt_x, pt_y + sag_y, pt_z + z))
                norms.append((norm_x, norm_y, norm_z))
                uvs.append((r / ring_segs, tb / tube_segs))

        for r in range(ring_segs):
            next_r = (r + 1) % ring_segs
            for tb in range(tube_segs):
                next_tb = (tb + 1) % tube_segs
                i0 = base_idx + r * tube_segs + tb + 1
                i1 = base_idx + next_r * tube_segs + tb + 1
                i2 = base_idx + next_r * tube_segs + next_tb + 1
                i3 = base_idx + r * tube_segs + next_tb + 1

                faces.append([(i0, i0, i0), (i1, i1, i1), (i2, i2, i2)])
                faces.append([(i0, i0, i0), (i2, i2, i2), (i3, i3, i3)])

    write_obj("Assets/Models/Marina/Chain.obj", verts, norms, uvs, faces)

# ==========================================
# 3. SIGN.OBJ (Wooden Berth Number Sign)
# ==========================================
def generate_sign_obj():
    verts = []
    norms = []
    uvs = []
    faces = []

    w, h, d = 0.28, 0.22, 0.03
    hw, hh, hd = w * 0.5, h * 0.5, d * 0.5

    def add_quad(v0, v1, v2, v3, n, uv0=(0,0), uv1=(1,0), uv2=(1,1), uv3=(0,1)):
        b = len(verts)
        verts.extend([v0, v1, v2, v3])
        norms.extend([n, n, n, n])
        uvs.extend([uv0, uv1, uv2, uv3])
        faces.append([(b+1, b+1, b+1), (b+3, b+3, b+3), (b+2, b+2, b+2)])
        faces.append([(b+1, b+1, b+1), (b+4, b+4, b+4), (b+3, b+3, b+3)])

    # Front Face (+Z) with full UV
    add_quad((-hw, -hh, hd), (hw, -hh, hd), (hw, hh, hd), (-hw, hh, hd), (0,0,1),
             (0,0), (1,0), (1,1), (0,1))
    # Back Face (-Z)
    add_quad((hw, -hh, -hd), (-hw, -hh, -hd), (-hw, hh, -hd), (hw, hh, -hd), (0,0,-1),
             (0.5,0.5), (0.5,0.5), (0.5,0.5), (0.5,0.5))
    # Top
    add_quad((-hw, hh, hd), (hw, hh, hd), (hw, hh, -hd), (-hw, hh, -hd), (0,1,0),
             (0.5,0.5), (0.5,0.5), (0.5,0.5), (0.5,0.5))
    # Bottom
    add_quad((-hw, -hh, -hd), (hw, -hh, -hd), (hw, -hh, hd), (-hw, -hh, hd), (0,-1,0),
             (0.5,0.5), (0.5,0.5), (0.5,0.5), (0.5,0.5))
    # Left
    add_quad((-hw, -hh, -hd), (-hw, -hh, hd), (-hw, hh, hd), (-hw, hh, -hd), (-1,0,0),
             (0.5,0.5), (0.5,0.5), (0.5,0.5), (0.5,0.5))
    # Right
    add_quad((hw, -hh, hd), (hw, -hh, -hd), (hw, hh, -hd), (hw, hh, hd), (1,0,0),
             (0.5,0.5), (0.5,0.5), (0.5,0.5), (0.5,0.5))

    # Wooden post beneath
    pw, ph = 0.035, 0.12
    pt, pb = -hh, -hh - ph
    add_quad((-pw, pb, hd), (pw, pb, hd), (pw, pt, hd), (-pw, pt, hd), (0,0,1), (0.5,0.5),(0.5,0.5),(0.5,0.5),(0.5,0.5))
    add_quad((pw, pb, -hd), (-pw, pb, -hd), (-pw, pt, -hd), (pw, pt, -hd), (0,0,-1), (0.5,0.5),(0.5,0.5),(0.5,0.5),(0.5,0.5))
    add_quad((-pw, pb, -hd), (-pw, pb, hd), (-pw, pt, hd), (-pw, pt, -hd), (-1,0,0), (0.5,0.5),(0.5,0.5),(0.5,0.5),(0.5,0.5))
    add_quad((pw, pb, hd), (pw, pb, -hd), (pw, pt, -hd), (pw, pt, hd), (1,0,0), (0.5,0.5),(0.5,0.5),(0.5,0.5),(0.5,0.5))
    add_quad((-pw, pb, -hd), (pw, pb, -hd), (pw, pb, hd), (-pw, pb, hd), (0,-1,0), (0.5,0.5),(0.5,0.5),(0.5,0.5),(0.5,0.5))

    write_obj("Assets/Models/Marina/Sign.obj", verts, norms, uvs, faces)

if __name__ == "__main__":
    generate_buoy_obj()
    generate_chain_obj()
    generate_sign_obj()
