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

def run_test(fps, smooth_time=0.06):
    dt = 1.0 / fps
    current_pos = 0.0
    vel = 0.0
    
    # 0 to 0.6s: move at constant speed 5.0 unit/s (total 3.0 units)
    # 0.6s to 1.2s: hold position at 3.0
    total_time = 1.2
    steps = int(total_time / dt)
    
    errors = []
    velocities = []
    target_pos = 0.0
    
    reach_target_time = None
    stop_time = None
    stop_start = 0.6
    
    for i in range(steps):
        t = i * dt
        if t < stop_start:
            target_pos = t * 5.0
        else:
            target_pos = stop_start * 5.0 # 3.0 units
            
        current_pos, vel = smooth_damp(current_pos, target_pos, vel, smooth_time, 100.0, dt)
        err = abs(target_pos - current_pos)
        errors.append(err)
        velocities.append(abs(vel))
        
        if t >= stop_start:
            if reach_target_time is None and err < 0.005:
                reach_target_time = t - stop_start
            if stop_time is None and abs(vel) < 0.01:
                stop_time = t - stop_start
                
    move_slice = slice(0, int(stop_start/dt))
    avg_err = sum(errors[move_slice]) / len(errors[move_slice])
    max_err = max(errors[move_slice])
    avg_vel = sum(velocities[move_slice]) / len(velocities[move_slice])
    max_vel = max(velocities[move_slice])
    
    return {
        'fps': fps,
        'avg_err': avg_err,
        'max_err': max_err,
        'avg_vel': avg_vel,
        'max_vel': max_vel,
        'time_to_reach': reach_target_time if reach_target_time else 0.0,
        'time_to_stop': stop_time if stop_time else 0.0
    }

print("=== SMOOTHDAMP BENCHMARK RESULTS (smoothTime = 0.06s, speed = 5 u/s) ===")
for fps in [30, 60, 120]:
    res = run_test(fps)
    print(f"FPS {fps:3d}: AvgErr={res['avg_err']:.4f}u, MaxErr={res['max_err']:.4f}u, AvgVel={res['avg_vel']:.4f}u/s, MaxVel={res['max_vel']:.4f}u/s, ReachTarget={res['time_to_reach']:.4f}s, StopTime={res['time_to_stop']:.4f}s")
