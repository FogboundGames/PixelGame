import math
import os

def create_kawaii_cube_obj(output_path):
    vertices = []
    normals = []
    uvs = []

    body_faces = []

    def add_vertex(pos, norm, uv):
        vertices.append(pos)
        nl = math.sqrt(norm[0]**2 + norm[1]**2 + norm[2]**2)
        if nl > 1e-6:
            normals.append((norm[0]/nl, norm[1]/nl, norm[2]/nl))
        else:
            normals.append((0, 1, 0))
        uvs.append(uv)
        return len(vertices)

    # -------------------------------------------------------------
    # 1. PUFFY JELLY CUBE BODY (Chubby, squishy, rounded toy cube)
    # -------------------------------------------------------------
    half_w = 0.50
    h_max = 0.85
    half_d = 0.50
    r = 0.22 # Deep, soft rounded corners matching Image 2

    cx_lim = half_w - r # 0.28
    cy_min = r          # 0.22
    cy_max = h_max - r  # 0.63
    cz_lim = half_d - r # 0.28

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

    def make_coords(min_val, max_val, r, n_bevel=8, n_center=4):
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

    x_coords = make_coords(-half_w, half_w, r, 8, 4)
    y_coords = make_coords(0.0, h_max, r, 8, 4)
    z_coords = make_coords(-half_d, half_d, r, 8, 4)

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
                    body_faces.append([p00, p01, p11])
                    body_faces.append([p00, p11, p10])
                else:
                    body_faces.append([p00, p10, p11])
                    body_faces.append([p00, p11, p01])

    # -Z Front
    build_face(x_coords, y_coords,
               lambda u, v: ((u, v, -half_d), (u + 0.5, v)), False)

    # +Z Back
    build_face(list(reversed(x_coords)), y_coords,
               lambda u, v: ((u, v, half_d), (1.0 - (u + 0.5), v)), False)

    # +X Right
    build_face(z_coords, y_coords,
               lambda u, v: ((half_w, v, u), (u + 0.5, v)), False)

    # -X Left
    build_face(list(reversed(z_coords)), y_coords,
               lambda u, v: ((-half_w, v, u), (1.0 - (u + 0.5), v)), False)

    # +Y Top (tilted towards camera, prominent highlight)
    build_face(x_coords, list(reversed(z_coords)),
               lambda u, v: ((u, h_max, v), (u + 0.5, v + 0.5)), False)

    # -Y Bottom
    build_face(x_coords, z_coords,
               lambda u, v: ((u, 0.0, v), (u + 0.5, 1.0 - (v + 0.5))), False)

    # -------------------------------------------------------------
    # WRITE CLEAN OBJ FILE
    # -------------------------------------------------------------
    with open(output_path, "w", encoding="utf-8") as f:
        f.write("# KawaiiCube Clean 3D Model for PixelGame\n")
        f.write("o KawaiiCube\n\n")

        for v in vertices:
            f.write(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}\n")
        f.write("\n")

        for vt in uvs:
            f.write(f"vt {vt[0]:.6f} {vt[1]:.6f}\n")
        f.write("\n")

        for vn in normals:
            f.write(f"vn {vn[0]:.6f} {vn[1]:.6f}\n")
        f.write("\n")

        f.write("g 01_Body\n")
        f.write("usemtl 01_Body\n")
        for face in body_faces:
            f.write(f"f {face[0]}/{face[0]}/{face[0]} {face[1]}/{face[1]}/{face[1]} {face[2]}/{face[2]}/{face[2]}\n")
        f.write("\n")

    print(f"KawaiiCube.obj successfully created at: {output_path} ({len(vertices)} verts, {len(body_faces)} tris)")

if __name__ == "__main__":
    out_file = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Models\KawaiiCube.obj"
    create_kawaii_cube_obj(out_file)
