import math

def evaluate_cubic_bezier(p0, p1, p2, p3, t):
    u = 1.0 - t
    tt = t * t
    uu = u * u
    uuu = uu * u
    ttt = tt * t
    
    px = uuu * p0[0] + 3.0 * uu * t * p1[0] + 3.0 * u * tt * p2[0] + ttt * p3[0]
    py = uuu * p0[1] + 3.0 * uu * t * p1[1] + 3.0 * u * tt * p2[1] + ttt * p3[1]
    pz = uuu * p0[2] + 3.0 * uu * t * p1[2] + 3.0 * u * tt * p2[2] + ttt * p3[2]
    return (px, py, pz)

def test_water_trajectory():
    print("--- 1. WATER TRAJECTORY TEST ---")
    # Test A: Slot to Slot shift (Slot 3 -> Slot 2)
    start_pos = (0.0, -1.98, 0.53)
    target_pos = (-1.4, -1.98, 0.53)
    delta = (target_pos[0] - start_pos[0], target_pos[1] - start_pos[1], target_pos[2] - start_pos[2])
    dist = math.sqrt(delta[0]**2 + delta[1]**2 + delta[2]**2)
    assert dist <= 1.8
    p0 = start_pos
    p1 = (start_pos[0] + delta[0] * 0.333, start_pos[1] + delta[1] * 0.333, start_pos[2] + delta[2] * 0.333)
    p2 = (start_pos[0] + delta[0] * 0.667, start_pos[1] + delta[1] * 0.667, start_pos[2] + delta[2] * 0.667)
    p3 = target_pos
    
    max_y_dev = 0.0
    for step in range(101):
        t = step / 100.0
        pt = evaluate_cubic_bezier(p0, p1, p2, p3, t)
        dev = abs(pt[1] - (-1.98))
        if dev > max_y_dev:
            max_y_dev = dev
    
    print(f"Slot-to-slot Y deviation from water: {max_y_dev:.6f}")
    assert max_y_dev < 1e-5, f"Expected 0 Y deviation, got {max_y_dev}"
    print("PASS: Boat moves 100% on water surface during slot sliding without any vertical airborne hop!")

    # Test B: Queue to Slot sail
    start_queue = (-1.2, -4.5, 0.1)
    target_slot = (-2.8, -1.98, 0.53)
    delta_q = (target_slot[0] - start_queue[0], target_slot[1] - start_queue[1], target_slot[2] - start_queue[2])
    dist_q = math.sqrt(delta_q[0]**2 + delta_q[1]**2 + delta_q[2]**2)
    assert dist_q > 1.8
    lat_offset = max(-0.35, min(0.35, (target_slot[0] - start_queue[0]) * 0.22))
    p0_q = start_queue
    p1_q = (start_queue[0] + delta_q[0] * 0.35 + lat_offset, start_queue[1] + delta_q[1] * 0.35, start_queue[2] + delta_q[2] * 0.35)
    p2_q = (start_queue[0] + delta_q[0] * 0.70 - lat_offset * 0.4, start_queue[1] + delta_q[1] * 0.70, start_queue[2] + delta_q[2] * 0.70)
    p3_q = target_slot

    # Verify Y never exceeds target Y (i.e. does not jump into the sky above target)
    max_y = -999.0
    for step in range(101):
        t = step / 100.0
        pt = evaluate_cubic_bezier(p0_q, p1_q, p2_q, p3_q, t)
        if pt[1] > max_y:
            max_y = pt[1]
    
    print(f"Queue-to-slot Max Y: {max_y:.3f} (target Y is {target_slot[1]:.3f})")
    assert max_y <= target_slot[1] + 0.01, "Boat should not jump higher than destination!"
    print("PASS: Boat sails smoothly along water without vertical jumping into air!")

def test_slot_compaction():
    print("\n--- 2. SLOT COMPACTION (1-2-3-4-5) TEST ---")
    
    def compact(slots):
        slots = list(slots)
        for target_idx in range(len(slots)):
            if slots[target_idx] is None:
                for from_idx in range(target_idx + 1, len(slots)):
                    if slots[from_idx] is not None:
                        slots[target_idx] = slots[from_idx]
                        slots[from_idx] = None
                        break
        return slots

    def find_empty_slot(slots):
        for i in range(len(slots)):
            if slots[i] is None:
                return i
        return None

    # Case 1: Slot 2 is empty, Slot 3 has ship
    s1 = ["Ship1", None, "Ship3", "Ship4", None]
    res1 = compact(s1)
    print(f"Initial: {s1} -> Compacted: {res1}")
    assert res1 == ["Ship1", "Ship3", "Ship4", None, None], "Failed Case 1"

    # Case 2: Slot 1 is empty, Slot 2 & 3 have ships
    s2 = [None, "Ship2", "Ship3", None, None]
    res2 = compact(s2)
    print(f"Initial: {s2} -> Compacted: {res2}")
    assert res2 == ["Ship2", "Ship3", None, None, None], "Failed Case 2"

    # Case 3: Only Slot 5 has a ship
    s3 = [None, None, None, None, "Ship5"]
    res3 = compact(s3)
    print(f"Initial: {s3} -> Compacted: {res3}")
    assert res3 == ["Ship5", None, None, None, None], "Failed Case 3"

    # FindEmptySlot test
    assert find_empty_slot([None, "Ship2", None, None, None]) == 0, "Slot 1 should be preferred"
    assert find_empty_slot(["Ship1", None, "Ship3", None, None]) == 1, "Slot 2 should be preferred"
    print("PASS: All 1-2-3-4-5 left-alignment and compaction tests passed successfully!")

if __name__ == "__main__":
    test_water_trajectory()
    test_slot_compaction()
