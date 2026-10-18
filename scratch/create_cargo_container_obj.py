import os
import math

def generate_cargo_container_obj():
    os.makedirs("Assets/Meshes", exist_ok=True)
    
    verts = []
    normals = []
    uvs = []
    body_faces = []
    hw_faces = []
    
    def add_vert(x, y, z):
        verts.append((x, y, z))
        return len(verts)

    def add_box(min_p, max_p, faces_list):
        # min_p = (x0, y0, z0), max_p = (x1, y1, z1)
        x0, y0, z0 = min_p
        x1, y1, z1 = max_p
        
        # 8 corners
        v1 = add_vert(x0, y0, z0)
        v2 = add_vert(x1, y0, z0)
        v3 = add_vert(x1, y1, z0)
        v4 = add_vert(x0, y1, z0)
        
        v5 = add_vert(x0, y0, z1)
        v6 = add_vert(x1, y0, z1)
        v7 = add_vert(x1, y1, z1)
        v8 = add_vert(x0, y1, z1)
        
        # 6 faces (quads as 2 tris each)
        # Front (-Z)
        faces_list.extend([(v1, v3, v2), (v1, v4, v3)])
        # Back (+Z)
        faces_list.extend([(v6, v7, v5), (v5, v7, v8)])
        # Left (-X)
        faces_list.extend([(v5, v8, v4), (v5, v4, v1)])
        # Right (+X)
        faces_list.extend([(v2, v3, v7), (v2, v7, v6)])
        # Top (+Y)
        faces_list.extend([(v4, v7, v3), (v4, v8, v7)])
        # Bottom (-Y)
        faces_list.extend([(v5, v1, v2), (v5, v2, v6)])

    # Dimensions
    hw = 0.52   # Half-width (X: -0.52 to 0.52 -> width 1.04)
    h_h = 0.88  # Height (Y: 0.0 to 0.88)
    hl = 0.62   # Half-length (Z: -0.62 to 0.62 -> length 1.24)
    
    # 1. Main Outer Shell (Frame and basic body)
    # Bottom plate
    add_box((-hw, 0.0, -hl), (hw, 0.08, hl), body_faces)
    # Top plate
    add_box((-hw, h_h - 0.08, -hl), (hw, h_h, hl), body_faces)
    # 4 Corner Pillars
    col_w = 0.08
    add_box((-hw, 0.0, -hl), (-hw + col_w, h_h, -hl + col_w), body_faces) # Front-left
    add_box((hw - col_w, 0.0, -hl), (hw, h_h, -hl + col_w), body_faces)   # Front-right
    add_box((-hw, 0.0, hl - col_w), (-hw + col_w, h_h, hl), body_faces)   # Back-left
    add_box((hw - col_w, 0.0, hl - col_w), (hw, h_h, hl), body_faces)     # Back-right
    
    # Back Wall
    add_box((-hw + col_w, 0.06, hl - col_w * 0.6), (hw - col_w, h_h - 0.06, hl), body_faces)
    
    # 2. Corrugated Sidewalls (Left & Right)
    # Recessed base wall
    recess = 0.025
    add_box((-hw + recess, 0.06, -hl + col_w), (-hw + col_w, h_h - 0.06, hl - col_w), body_faces)
    add_box((hw - col_w, 0.06, -hl + col_w), (hw - recess, h_h - 0.06, hl - col_w), body_faces)
    
    # Vertical ribs on sidewalls (Image 1 has corrugated vertical ridges)
    num_ribs = 7
    rib_width = 0.045
    rib_depth = 0.028
    z_start = -hl + col_w + 0.06
    z_end = hl - col_w - 0.06
    z_step = (z_end - z_start) / (num_ribs - 1)
    
    for i in range(num_ribs):
        z_c = z_start + i * z_step
        # Left side rib
        add_box((-hw, 0.07, z_c - rib_width * 0.5), (-hw + recess + rib_depth, h_h - 0.07, z_c + rib_width * 0.5), body_faces)
        # Right side rib
        add_box((hw - recess - rib_depth, 0.07, z_c - rib_width * 0.5), (hw, h_h - 0.07, z_c + rib_width * 0.5), body_faces)
        
    # 3. Corrugated Roof
    # Base roof panel
    add_box((-hw + col_w, h_h - 0.07, -hl + col_w), (hw - col_w, h_h - 0.02, hl - col_w), body_faces)
    # Transverse roof ribs
    num_roof_ribs = 7
    for i in range(num_roof_ribs):
        z_c = z_start + i * z_step
        add_box((-hw + col_w + 0.02, h_h - 0.02, z_c - rib_width * 0.5), (hw - col_w - 0.02, h_h, z_c + rib_width * 0.5), body_faces)
        
    # 4. Front Face: Double Doors
    # Front header beam & threshold
    add_box((-hw, h_h - 0.08, -hl), (hw, h_h, -hl + 0.04), body_faces)
    add_box((-hw, 0.0, -hl), (hw, 0.08, -hl + 0.04), body_faces)
    
    # Left Door leaf (X: -0.44 to -0.01, Y: 0.07 to 0.81, Z: -hl + 0.01 to -hl + 0.035)
    add_box((-hw + col_w, 0.07, -hl + 0.01), (-0.01, h_h - 0.07, -hl + 0.035), body_faces)
    # Right Door leaf (X: 0.01 to 0.44)
    add_box((0.01, 0.07, -hl + 0.01), (hw - col_w, h_h - 0.07, -hl + 0.035), body_faces)
    
    # Recessed horizontal panels on each door (Image 1 door styling)
    for y_panel in [0.22, 0.44, 0.66]:
        # Left door horizontal rib
        add_box((-hw + col_w + 0.04, y_panel - 0.02, -hl + 0.035), (-0.05, y_panel + 0.02, -hl + 0.045), body_faces)
        # Right door horizontal rib
        add_box((0.05, y_panel - 0.02, -hl + 0.035), (hw - col_w - 0.04, y_panel + 0.02, -hl + 0.045), body_faces)
        
    # 5. Front Door Hardware (WHITE / SILVER LOCKING RODS & HANDLES as in Image 1!)
    rod_radius = 0.018
    # Left rod at X = -0.22, Right rod at X = 0.22
    for rod_x in [-0.22, 0.22]:
        # Vertical rod (from Y=0.04 to Y=0.84, Z = -hl - 0.015)
        rz = -hl - 0.015
        add_box((rod_x - rod_radius, 0.04, rz - rod_radius), (rod_x + rod_radius, h_h - 0.04, rz + rod_radius), hw_faces)
        
        # Top bracket / cam
        add_box((rod_x - 0.032, h_h - 0.09, rz - 0.025), (rod_x + 0.032, h_h - 0.03, rz + 0.02), hw_faces)
        # Bottom bracket / cam
        add_box((rod_x - 0.032, 0.03, rz - 0.025), (rod_x + 0.032, 0.09, rz + 0.02), hw_faces)
        
        # Center bracket
        add_box((rod_x - 0.028, 0.42, rz - 0.022), (rod_x + 0.028, 0.47, rz + 0.02), hw_faces)
        
        # Horizontal latch handle in the middle (facing toward center)
        handle_dir = 1 if rod_x < 0 else -1
        add_box((rod_x, 0.43, rz - 0.03), (rod_x + handle_dir * 0.09, 0.46, rz - 0.01), hw_faces)
        # Handle rubber grip / tip
        add_box((rod_x + handle_dir * 0.07, 0.415, rz - 0.035), (rod_x + handle_dir * 0.095, 0.475, rz - 0.005), hw_faces)

    # Write OBJ file with MTL references
    out_obj = "Assets/Meshes/CargoContainer.obj"
    out_mtl = "Assets/Meshes/CargoContainer.mtl"
    
    with open(out_mtl, "w") as f:
        f.write("# Cargo Container Materials\n")
        f.write("newmtl ContainerBody\n")
        f.write("Kd 1.0 1.0 1.0\n")
        f.write("d 1.0\n\n")
        f.write("newmtl ContainerHardware\n")
        f.write("Kd 0.96 0.97 0.99\n")
        f.write("d 1.0\n")
        
    with open(out_obj, "w") as f:
        f.write("# 3D Cargo Container Model matching Image 1\n")
        f.write("mtllib CargoContainer.mtl\n\n")
        
        for v in verts:
            f.write(f"v {v[0]:.4f} {v[1]:.4f} {v[2]:.4f}\n")
            
        f.write("\ng ContainerBody\n")
        f.write("usemtl ContainerBody\n")
        for face in body_faces:
            f.write(f"f {face[0]} {face[1]} {face[2]}\n")
            
        f.write("\ng ContainerHardware\n")
        f.write("usemtl ContainerHardware\n")
        for face in hw_faces:
            f.write(f"f {face[0]} {face[1]} {face[2]}\n")
            
    print(f"Generated CargoContainer.obj with {len(verts)} vertices, {len(body_faces)} body faces, {len(hw_faces)} hardware faces.")

if __name__ == "__main__":
    generate_cargo_container_obj()
