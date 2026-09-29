import math

def smooth_damp(current, target, current_velocity, smooth_time, max_speed, delta_time):
    smooth_time = max(0.0001, smooth_time)
    omega = 2.0 / smooth_time
    x = omega * delta_time
    exp = 1.0 / (1.0 + x + 0.48 * x * x + 0.235 * x * x * x)
    change = current - target
    original_to = target

    max_change = max_speed * smooth_time
    if change > max_change:
        change = max_change
    elif change < -max_change:
        change = -max_change
    target = current - change

    temp = (current_velocity + omega * change) * delta_time
    current_velocity = (current_velocity - omega * temp) * exp
    output = target + (change + temp) * exp

    if (original_to - current > 0 and output - original_to > 0) or (original_to - current < 0 and output - original_to < 0):
        output = original_to
        current_velocity = (output - original_to) / delta_time

    return output, current_velocity

def run_reversal_test(fps=60):
    dt = 1.0 / fps
    current_pos = 0.0
    smooth_vel = 0.0
    
    # 0 to 0.4s: moving right at 6 u/s (x = 0 to 2.4)
    # 0.4s: sudden reversal! moving left at 6 u/s (x = 2.4 down to -1.2)
    # total 1.0s
    total_time = 1.0
    steps = int(total_time / dt)
    
    target_pos = 0.0
    prev_visual_pos = 0.0
    smoothed_vel_metric = 0.0
    vel_smooth_deriv = 0.0
    current_banking = 0.0
    roll_smooth_vel = 0.0
    
    overshoot_pos = 0.0
    reversal_step = int(0.4 / dt)
    
    print("\n=== DIRECTION REVERSAL TEST (RIGHT -> LEFT at t=0.4s) ===")
    
    for i in range(steps):
        t = i * dt
        if t < 0.4:
            target_pos = t * 6.0
        else:
            target_pos = 2.4 - (t - 0.4) * 6.0
            
        current_pos, smooth_vel = smooth_damp(current_pos, target_pos, smooth_vel, 0.06, 100.0, dt)
        
        # Velocity calculation as in ShipController
        raw_vel = (current_pos - prev_visual_pos) / dt
        prev_visual_pos = current_pos
        smoothed_vel_metric, vel_smooth_deriv = smooth_damp(smoothed_vel_metric, raw_vel, vel_smooth_deriv, 0.04, 100.0, dt)
        
        # Banking roll calculation
        target_roll = max(-10.0, min(10.0, -smoothed_vel_metric * 2.5))
        current_banking, roll_smooth_vel = smooth_damp(current_banking, target_roll, roll_smooth_vel, 0.08, 100.0, dt)
        
        if t >= 0.4 and current_pos > overshoot_pos:
            overshoot_pos = current_pos
            
        if 0.38 <= t <= 0.52:
            print(f"t={t:.3f}s: Target={target_pos:+.3f}, Visual={current_pos:+.3f}, RawVel={raw_vel:+.2f}, SmoothVel={smoothed_vel_metric:+.2f}, BankingRoll={current_banking:+.2f}°")
            
    print(f"Max Visual Position reached after reversal: {overshoot_pos:.4f}u (Target at reversal was 2.4000u)")
    print(f"Position Overshoot beyond reversal point: {overshoot_pos - 2.4:.4f}u")

run_reversal_test(60)
