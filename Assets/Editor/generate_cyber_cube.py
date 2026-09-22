import math
import os

def create_cyber_cube_obj(output_path):
    vertices = []
    normals = []
    uvs = []
    
    groups = {
        "01_CyberCube_Body": [],
        "02_CyberCube_Accent": [],
        "03_CyberCube_Neon": [],
        "04_CyberCube_Base": []
    }

    def add_vertex(pos, norm, uv):
        vertices.append(pos)
        nl = math.sqrt(norm[0]**2 + norm[1]**2 + norm[2]**2)
        if nl > 1e-6:
            normals.append((norm[0]/nl, norm[1]/nl, norm[2]/nl))
        else:
            normals.append((0, 1, 0))
        uvs.append(uv)
        return len(vertices) # 1-based index

    # -------------------------------------------------------------
    # 1. BODY: Smooth Rounded Beveled Cube (1.0 x 1.0 x 1.0, Y from 0 to 1)
    # -------------------------------------------------------------
    r = 0.16 # bevel radius
    half = 0.50
    cx_lim = half - r # 0.34
    cy_min = r # 0.16
    cy_max = 1.0 - r # 0.84
    cz_lim = half - r # 0.34

    def project_to_rounded_box(x, y, z):
        cx = max(-cx_lim, min(cx_lim, x))
        cy = max(cy_min, min(cy_max, y))
        cz = max(-cz_lim, min(cz_lim, z))
        
        dx = x - cx
        dy = y - cy
        dz = z - cz
        dist = math.sqrt(dx*dx + dy*dy + dz*dz)
        if dist > 1e-6:
            nx = dx / dist
            ny = dy / dist
            nz = dz / dist
            px = cx + nx * r
            py = cy + ny * r
            pz = cz + nz * r
            return (px, py, pz), (nx, ny, nz)
        else:
            return (x, y, z), (0, 1, 0)

    def make_1d_coords(min_val, max_val, r, n_bevel=6, n_center=6):
        c_min = min_val + r
        c_max = max_val - r
        coords = []
        for i in range(n_bevel):
            t = i / float(n_bevel)
            angle = (1.0 - t) * (math.pi * 0.5)
            coords.append(c_min - r * math.cos(angle))
        for i in range(n_center + 1):
            t = i / float(n_center)
            coords.append(c_min + t * (c_max - c_min))
        for i in range(1, n_bevel + 1):
            t = i / float(n_bevel)
            angle = t * (math.pi * 0.5)
            coords.append(c_max + r * math.sin(angle))
        return coords

    x_coords = make_1d_coords(-0.5, 0.5, r, 5, 4)
    y_coords = make_1d_coords(0.0, 1.0, r, 5, 4)
    z_coords = make_1d_coords(-0.5, 0.5, r, 5, 4)

    def build_face(u_vals, v_vals, make_pt, invert_winding=False):
        grid = []
        for v in v_vals:
            row = []
            for u in u_vals:
                pos_raw, uv = make_pt(u, v)
                pos, norm = project_to_rounded_box(*pos_raw)
                idx = add_vertex(pos, norm, uv)
                row.append(idx)
            grid.append(row)
        
        for j in range(len(v_vals) - 1):
            for i in range(len(u_vals) - 1):
                p00 = grid[j][i]
                p10 = grid[j][i+1]
                p11 = grid[j+1][i+1]
                p01 = grid[j+1][i]
                if not invert_winding:
                    groups["01_CyberCube_Body"].append([p00, p01, p11])
                    groups["01_CyberCube_Body"].append([p00, p11, p10])
                else:
                    groups["01_CyberCube_Body"].append([p00, p10, p11])
                    groups["01_CyberCube_Body"].append([p00, p11, p01])

    # +Z Face (Front) - Outward: normal +Z
    build_face(x_coords, y_coords,
               lambda u, v: ((u, v, 0.5), ((u + 0.5), v)), False)

    # -Z Face (Back) - Outward: normal -Z
    build_face(list(reversed(x_coords)), y_coords,
               lambda u, v: ((u, v, -0.5), (1.0 - (u + 0.5), v)), False)

    # +X Face (Right) - Outward: normal +X
    build_face(list(reversed(z_coords)), y_coords,
               lambda u, v: ((0.5, v, u), ((u + 0.5), v)), False)

    # -X Face (Left) - Outward: normal -X
    build_face(z_coords, y_coords,
               lambda u, v: ((-0.5, v, u), (1.0 - (u + 0.5), v)), False)

    # +Y Face (Top) - Outward: normal +Y (invert winding so (dX x dZ) points +Y)
    build_face(x_coords, z_coords,
               lambda u, v: ((u, 1.0, v), ((u + 0.5), (v + 0.5))), True)

    # -Y Face (Bottom) - Outward: normal -Y
    build_face(x_coords, z_coords,
               lambda u, v: ((u, 0.0, v), ((u + 0.5), 1.0 - (v + 0.5))), False)

    # -------------------------------------------------------------
    # 2. TOP BUTTON: Rounded plateau on +Y (Submesh: CyberCube_Neon)
    # -------------------------------------------------------------
    btn_w = 0.58
    btn_h = 0.12
    btn_r = 0.11
    btn_half = btn_w * 0.5
    btn_core = btn_half - btn_r
    btn_top_y = 1.0 + btn_h

    def make_squircle_contour(radius_core, corner_r, segs=8):
        pts = []
        corners = [
            (radius_core, radius_core, 0),
            (-radius_core, radius_core, math.pi * 0.5),
            (-radius_core, -radius_core, math.pi),
            (radius_core, -radius_core, math.pi * 1.5)
        ]
        for cx, cz, base_angle in corners:
            for s in range(segs):
                angle = base_angle + (s / float(segs)) * (math.pi * 0.5)
                px = cx + corner_r * math.cos(angle)
                pz = cz + corner_r * math.sin(angle)
                nx = math.cos(angle)
                nz = math.sin(angle)
                pts.append((px, pz, nx, nz))
        return pts

    btn_contour = make_squircle_contour(btn_core, btn_r, 8)
    n_btn = len(btn_contour)

    ring_base = []
    ring_mid = []
    ring_top = []

    for px, pz, nx, nz in btn_contour:
        idx0 = add_vertex((px, 1.002, pz), (nx * 0.8, 0, nz * 0.8), (px + 0.5, pz + 0.5))
        idx1 = add_vertex((px, 1.095, pz), (nx, 0.2, nz), (px + 0.5, pz + 0.5))
        in_px = px * 0.90
        in_pz = pz * 0.90
        idx2 = add_vertex((in_px, btn_top_y, in_pz), (nx * 0.2, 0.98, nz * 0.2), (in_px + 0.5, in_pz + 0.5))
        ring_base.append(idx0)
        ring_mid.append(idx1)
        ring_top.append(idx2)

    center_btn = add_vertex((0, btn_top_y + 0.008, 0), (0, 1, 0), (0.5, 0.5))

    for i in range(n_btn):
        ni = (i + 1) % n_btn
        # Walls: outward
        groups["03_CyberCube_Neon"].append([ring_base[i], ring_base[ni], ring_mid[ni]])
        groups["03_CyberCube_Neon"].append([ring_base[i], ring_mid[ni], ring_mid[i]])
        # Chamfer: outward
        groups["03_CyberCube_Neon"].append([ring_mid[i], ring_mid[ni], ring_top[ni]])
        groups["03_CyberCube_Neon"].append([ring_mid[i], ring_top[ni], ring_top[i]])
        # Top cap: upward normal
        groups["03_CyberCube_Neon"].append([center_btn, ring_top[i], ring_top[ni]])

    # -------------------------------------------------------------
    # 3. 4 SIDE CAPSULE PANELS (Accent Bezel & Glowing Neon Light)
    # -------------------------------------------------------------
    def make_capsule_polygon(w, h, segs=10):
        r_cap = w * 0.5
        straight_h = max(0.0, (h - w) * 0.5)
        pts = []
        # Top semi-circle: 0 to pi (counter-clockwise from right to left)
        for s in range(segs):
            angle = (s / float(segs)) * math.pi
            px = r_cap * math.cos(angle)
            py = straight_h + r_cap * math.sin(angle)
            pts.append((px, py, math.cos(angle), math.sin(angle)))
        # Bottom semi-circle: pi to 2*pi (counter-clockwise from left to right)
        for s in range(segs):
            angle = math.pi + (s / float(segs)) * math.pi
            px = r_cap * math.cos(angle)
            py = -straight_h + r_cap * math.sin(angle)
            pts.append((px, py, math.cos(angle), math.sin(angle)))
        return pts

    bezel_outer_pts = make_capsule_polygon(0.36, 0.70, 10)
    bezel_inner_pts = make_capsule_polygon(0.22, 0.54, 10)
    neon_pill_pts   = make_capsule_polygon(0.20, 0.52, 10)
    n_bezel = len(bezel_outer_pts)

    faces_transform = [
        # +Z Front: (x, y, z) -> (x, 0.50 + y, 0.50 + z), Normal (nx, ny, nz)
        (lambda x, y, z: (x, 0.50 + y, 0.50 + z),
         lambda nx, ny, nz: (nx, ny, nz)),
        # -Z Back: (x, y, z) -> (-x, 0.50 + y, -0.50 - z), Normal (-nx, ny, -nz)
        (lambda x, y, z: (-x, 0.50 + y, -0.50 - z),
         lambda nx, ny, nz: (-nx, ny, -nz)),
        # +X Right: (x, y, z) -> (0.50 + z, 0.50 + y, -x), Normal (nz, ny, -nx)
        (lambda x, y, z: (0.50 + z, 0.50 + y, -x),
         lambda nx, ny, nz: (nz, ny, -nx)),
        # -X Left: (x, y, z) -> (-0.50 - z, 0.50 + y, x), Normal (-nz, ny, nx)
        (lambda x, y, z: (-0.50 - z, 0.50 + y, x),
         lambda nx, ny, nz: (-nz, ny, nx))
    ]

    for face_xform, norm_xform in faces_transform:
        # A. Accent Bezel Frame (Submesh: CyberCube_Accent)
        outer_rim = []
        bezel_crest = []
        inner_rim = []
        for (ox, oy, onx, ony), (ix, iy, inx, iny) in zip(bezel_outer_pts, bezel_inner_pts):
            p_out = face_xform(ox, oy, 0.020)
            n_out = norm_xform(onx * 0.6, ony * 0.6, 0.4)
            
            p_crest = face_xform(ox * 0.85 + ix * 0.15, oy * 0.85 + iy * 0.15, 0.046)
            n_crest = norm_xform(0, 0, 1.0)
            
            p_in = face_xform(ix, iy, 0.030)
            n_in = norm_xform(-inx * 0.5, -iny * 0.5, 0.5)

            idx_o = add_vertex(p_out, n_out, (ox + 0.5, oy + 0.5))
            idx_c = add_vertex(p_crest, n_crest, (ox + 0.5, oy + 0.5))
            idx_i = add_vertex(p_in, n_in, (ix + 0.5, iy + 0.5))
            outer_rim.append(idx_o)
            bezel_crest.append(idx_c)
            inner_rim.append(idx_i)

        for i in range(n_bezel):
            ni = (i + 1) % n_bezel
            groups["02_CyberCube_Accent"].append([outer_rim[i], bezel_crest[ni], outer_rim[ni]])
            groups["02_CyberCube_Accent"].append([outer_rim[i], bezel_crest[i], bezel_crest[ni]])
            groups["02_CyberCube_Accent"].append([bezel_crest[i], inner_rim[ni], bezel_crest[ni]])
            groups["02_CyberCube_Accent"].append([bezel_crest[i], inner_rim[i], inner_rim[ni]])

        # B. Glowing Neon Light Bar (Submesh: CyberCube_Neon)
        neon_rim = []
        neon_mid = []
        for px, py, nx, ny in neon_pill_pts:
            p_edge = face_xform(px, py, 0.034)
            n_edge = norm_xform(nx * 0.7, ny * 0.7, 0.5)

            p_mid = face_xform(px * 0.75, py * 0.75, 0.054)
            n_mid = norm_xform(nx * 0.3, ny * 0.3, 0.9)

            idx_e = add_vertex(p_edge, n_edge, (px + 0.5, py + 0.5))
            idx_m = add_vertex(p_mid, n_mid, (px + 0.5, py + 0.5))
            neon_rim.append(idx_e)
            neon_mid.append(idx_m)

        p_cen = face_xform(0, 0, 0.062)
        n_cen = norm_xform(0, 0, 1.0)
        idx_cen = add_vertex(p_cen, n_cen, (0.5, 0.5))

        for i in range(len(neon_pill_pts)):
            ni = (i + 1) % len(neon_pill_pts)
            groups["03_CyberCube_Neon"].append([neon_rim[i], neon_mid[ni], neon_rim[ni]])
            groups["03_CyberCube_Neon"].append([neon_rim[i], neon_mid[i], neon_mid[ni]])
            groups["03_CyberCube_Neon"].append([neon_mid[i], idx_cen, neon_mid[ni]])

    # -------------------------------------------------------------
    # 4. BOTTOM BASE PANEL & 4 CORNER FOOT PADS (Submesh: CyberCube_Base)
    # -------------------------------------------------------------
    base_size = 0.72
    base_half = base_size * 0.5
    base_r = 0.08
    base_core = base_half - base_r
    base_contour = make_squircle_contour(base_core, base_r, 6)
    n_base = len(base_contour)

    base_ring = []
    for px, pz, nx, nz in base_contour:
        idx = add_vertex((px, 0.002, pz), (0, -1, 0), (px + 0.5, pz + 0.5))
        base_ring.append(idx)
    base_center = add_vertex((0, 0.002, 0), (0, -1, 0), (0.5, 0.5))

    for i in range(n_base):
        ni = (i + 1) % n_base
        groups["04_CyberCube_Base"].append([base_ring[ni], base_ring[i], base_center])

    # 4 corner round foot pads (cylindrical + rounded cap)
    pad_r = 0.10
    pad_h = 0.026
    pad_offset = 0.33
    pad_positions = [
        ( pad_offset, pad_offset),
        (-pad_offset, pad_offset),
        (-pad_offset, -pad_offset),
        ( pad_offset, -pad_offset)
    ]
    pad_segs = 12

    for cx, cz in pad_positions:
        rim_top = []
        rim_bot = []
        for s in range(pad_segs):
            angle = (s / float(pad_segs)) * (math.pi * 2.0)
            dx = math.cos(angle)
            dz = math.sin(angle)
            px = cx + dx * pad_r
            pz = cz + dz * pad_r
            
            idx_t = add_vertex((px, 0.000, pz), (dx * 0.7, 0.3, dz * 0.7), (dx * 0.5 + 0.5, dz * 0.5 + 0.5))
            idx_b = add_vertex((px, -pad_h, pz), (dx * 0.7, -0.3, dz * 0.7), (dx * 0.5 + 0.5, dz * 0.5 + 0.5))
            rim_top.append(idx_t)
            rim_bot.append(idx_b)

        pad_cap_center = add_vertex((cx, -pad_h, cz), (0, -1.0, 0), (0.5, 0.5))

        for s in range(pad_segs):
            ns = (s + 1) % pad_segs
            groups["04_CyberCube_Base"].append([rim_top[s], rim_bot[s], rim_bot[ns]])
            groups["04_CyberCube_Base"].append([rim_top[s], rim_bot[ns], rim_top[ns]])
            groups["04_CyberCube_Base"].append([rim_bot[ns], rim_bot[s], pad_cap_center])

    # -------------------------------------------------------------
    # WRITE OBJ FILE
    # -------------------------------------------------------------
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        f.write("# CyberCube 3D Model for PixelGame\n")
        f.write("o CyberCube\n\n")

        for v in vertices:
            f.write(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}\n")
        f.write("\n")

        for vt in uvs:
            f.write(f"vt {vt[0]:.6f} {vt[1]:.6f}\n")
        f.write("\n")

        for vn in normals:
            f.write(f"vn {vn[0]:.6f} {vn[1]:.6f} {vn[2]:.6f}\n")
        f.write("\n")

        f.write("g 01_CyberCube_Body\n")
        f.write("usemtl 01_CyberCube_Body\n")
        for face in groups["01_CyberCube_Body"]:
            f.write(f"f {face[0]}/{face[0]}/{face[0]} {face[1]}/{face[1]}/{face[1]} {face[2]}/{face[2]}/{face[2]}\n")
        f.write("\n")

        f.write("g 02_CyberCube_Accent\n")
        f.write("usemtl 02_CyberCube_Accent\n")
        for face in groups["02_CyberCube_Accent"]:
            f.write(f"f {face[0]}/{face[0]}/{face[0]} {face[1]}/{face[1]}/{face[1]} {face[2]}/{face[2]}/{face[2]}\n")
        f.write("\n")

        f.write("g 03_CyberCube_Neon\n")
        f.write("usemtl 03_CyberCube_Neon\n")
        for face in groups["03_CyberCube_Neon"]:
            f.write(f"f {face[0]}/{face[0]}/{face[0]} {face[1]}/{face[1]}/{face[1]} {face[2]}/{face[2]}/{face[2]}\n")
        f.write("\n")

        f.write("g 04_CyberCube_Base\n")
        f.write("usemtl 04_CyberCube_Base\n")
        for face in groups["04_CyberCube_Base"]:
            f.write(f"f {face[0]}/{face[0]}/{face[0]} {face[1]}/{face[1]}/{face[1]} {face[2]}/{face[2]}/{face[2]}\n")
        f.write("\n")

    print(f"CyberCube.obj successfully regenerated at: {output_path}")

if __name__ == "__main__":
    out_file = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Models\CyberCube.obj"
    create_cyber_cube_obj(out_file)
