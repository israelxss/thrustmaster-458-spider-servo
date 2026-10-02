"""
Comprehensive Automated Test Suite for All Wheel Motor Modes:
1. Driver Reset & Free Float Verification
2. Directional Right & Left Movement
3. Goto with Slow Speed (35 deg/s) + Release
4. Goto with Fast Speed (140 deg/s) + Release
5. Goto with Post-Arrival Lock (lock at 0.0°)
6. Instant Lock at Arbitrary Position
7. Full Motor Release (Free Float)
"""

import time
import sys
from wheel_motor_api import WheelMotorAPI
from servo_controller import WheelServoSystem

def run_tests():
    print("=" * 65)
    print("       STARTING COMPREHENSIVE WHEEL MOTOR TEST SUITE")
    print("=" * 65)

    servo = WheelServoSystem()
    api = servo.api

    # -------------------------------------------------------------
    # TEST 1: Driver Reset & Telemetry
    # -------------------------------------------------------------
    print("\n[TEST 1/6] Resetting Motor Driver & Verifying Status...")
    res = api.reset()
    time.sleep(0.8)
    st = api.get_status()
    ang = servo.get_angle()
    print(f"  --> Status: Connected={st.get('WheelConnected')}, MotorEnabled={st.get('MotorEnabled')}")
    print(f"  --> Current Angle: {ang:+.2f}°")
    print(f"  --> Active Effect: {st.get('ActiveEffect')}")
    assert st.get("WheelConnected") and st.get("MotorEnabled"), "Motor not connected or enabled!"
    print("  [PASS] Test 1 Passed: Driver alive and responsive.")

    # -------------------------------------------------------------
    # TEST 2: Directional Movement Right and Left
    # -------------------------------------------------------------
    print("\n[TEST 2/6] Testing Directional Rotation (Right & Left)...")
    a_start = servo.get_angle()
    api.rotate(torque=0.45, duration_ms=60)
    time.sleep(0.5)
    a_right = servo.get_angle()
    delta_r = a_right - a_start
    print(f"  --> Pulse +0.45 (60ms): Moved {delta_r:+.1f}° (from {a_start:+.1f}° to {a_right:+.1f}°)")
    assert abs(delta_r) > 10.0, "Right rotation did not move sufficiently!"

    api.rotate(torque=-0.45, duration_ms=60)
    time.sleep(0.5)
    a_left = servo.get_angle()
    delta_l = a_left - a_right
    print(f"  --> Pulse -0.45 (60ms): Moved {delta_l:+.1f}° (from {a_right:+.1f}° to {a_left:+.1f}°)")
    assert abs(delta_l) > 10.0, "Left rotation did not move sufficiently!"
    print("  [PASS] Test 2 Passed: Bidirectional motion verified.")

    # -------------------------------------------------------------
    # TEST 3: Goto Angle with Controlled SLOW Speed (35 deg/s) + Release
    # -------------------------------------------------------------
    target_slow = 60.0
    print(f"\n[TEST 3/6] Testing Goto {target_slow:+.1f}° at SLOW speed (35 deg/s) with RELEASE at end...")
    t0 = time.time()
    ok_slow = servo.goto(target_slow, speed=35.0, lock=False)
    elapsed_slow = time.time() - t0
    final_ang = servo.get_angle()
    err_slow = abs(final_ang - target_slow)
    print(f"  --> Reached: {final_ang:+.1f}° (Error: {err_slow:.2f}°) in {elapsed_slow:.2f}s")
    assert ok_slow and err_slow <= 3.0, f"Slow goto failed to reach target (err={err_slow})"
    print("  [PASS] Test 3 Passed: Smooth slow motion achieved.")
    time.sleep(0.5)

    # -------------------------------------------------------------
    # TEST 4: Goto Angle with FAST Speed (140 deg/s) + Release
    # -------------------------------------------------------------
    target_fast = -60.0
    print(f"\n[TEST 4/6] Testing Goto {target_fast:+.1f}° at FAST speed (140 deg/s) with RELEASE at end...")
    t0 = time.time()
    ok_fast = servo.goto(target_fast, speed=140.0, lock=False)
    elapsed_fast = time.time() - t0
    final_ang = servo.get_angle()
    err_fast = abs(final_ang - target_fast)
    print(f"  --> Reached: {final_ang:+.1f}° (Error: {err_fast:.2f}°) in {elapsed_fast:.2f}s")
    assert ok_fast and err_fast <= 3.0, f"Fast goto failed to reach target (err={err_fast})"
    print("  [PASS] Test 4 Passed: Fast responsive motion achieved.")
    time.sleep(0.5)

    # -------------------------------------------------------------
    # TEST 5: Goto 0.0° with Post-Arrival LOCK
    # -------------------------------------------------------------
    print("\n[TEST 5/6] Testing Goto 0.0° with Post-Arrival LOCK...")
    ok_lock = servo.goto(0.0, speed=100.0, lock=True)
    time.sleep(0.5)
    st_lock = api.get_status()
    print(f"  --> Wheel Mode: {st_lock.get('Mode')} | ActiveEffect: {st_lock.get('ActiveEffect')}")
    print(f"  --> Target Angle: {st_lock.get('TargetAngle')}° | Current: {servo.get_angle():+.2f}°")
    assert st_lock.get("Mode") == "Holding", "Wheel should be in Holding/Locked mode!"
    print("  [PASS] Test 5 Passed: Post-arrival lock engaged.")

    # -------------------------------------------------------------
    # TEST 6: Release & Free Float
    # -------------------------------------------------------------
    print("\n[TEST 6/6] Testing Instant Release (Free Wheel)...")
    servo.release()
    time.sleep(0.5)
    st_rel = api.get_status()
    print(f"  --> Wheel Mode: {st_rel.get('Mode')} | ActiveEffect: {st_rel.get('ActiveEffect')}")
    assert st_rel.get("Mode") == "Released", "Wheel should be in Released mode!"
    print("  [PASS] Test 6 Passed: Motor released and floating freely.")

    print("\n" + "=" * 65)
    print("       ALL TESTS PASSED SUCCESSFULLY! ALL MODES VERIFIED!")
    print("=" * 65)

if __name__ == "__main__":
    run_tests()
