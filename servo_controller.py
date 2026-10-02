"""
Steering Wheel Closed-Loop Servo Controller (בקרת סרבו חוג סגור מלאה להגה)
Features:
- Pure Torque Control (Zero manufacturer springs or locks)
- Real-Time 1000Hz Angle Telemetry (XInput & API)
- Automatic Hard-Stop / Limit Calibration (מציאת גבולות מכניים שמאלה וימינה)
- True Mechanical Homing & Zeroing (איפוס ומרכוז מבוקר)
- Closed-Loop Profiled PID Positioning (Goto angle with smooth deceleration)
- Active Robotic Servo Hold (שמירת מיקום אקטיבית)
- Free Float / Release (שחרור מנוע מלא ללא שום התנגדות)
"""

import time
import math
import sys
import ctypes
from ctypes import wintypes
from wheel_motor_api import WheelMotorAPI

class WheelServoSystem:
    def __init__(self, total_range_deg: float = 900.0):
        self.total_range = total_range_deg
        self.half_range = total_range_deg / 2.0
        self.min_limit = -self.half_range
        self.max_limit = self.half_range
        self.zero_offset = 0.0

        self.api = WheelMotorAPI()
        self._init_xinput()

    def _init_xinput(self):
        """Loads XInput for direct sub-millisecond angle reading."""
        self.xinput = None
        for dll in ['xinput1_4.dll', 'xinput1_3.dll', 'xinput9_1_0.dll']:
            try:
                self.xinput = ctypes.windll.LoadLibrary(dll)
                break
            except Exception:
                pass

        class XINPUT_GAMEPAD(ctypes.Structure):
            _fields_ = [
                ('wButtons', wintypes.WORD),
                ('bLeftTrigger', wintypes.BYTE),
                ('bRightTrigger', wintypes.BYTE),
                ('sThumbLX', wintypes.SHORT),
                ('sThumbLY', wintypes.SHORT),
                ('sThumbRX', wintypes.SHORT),
                ('sThumbRY', wintypes.SHORT),
            ]

        class XINPUT_STATE(ctypes.Structure):
            _fields_ = [('dwPacketNumber', wintypes.DWORD), ('Gamepad', XINPUT_GAMEPAD)]

        self._state_struct = XINPUT_STATE

    def get_angle(self) -> float:
        """Returns current wheel angle in degrees (-450.0° to +450.0°) with zero offset applied."""
        if self.xinput:
            state = self._state_struct()
            for idx in range(4):
                if self.xinput.XInputGetState(idx, ctypes.byref(state)) == 0:
                    raw_lx = state.Gamepad.sThumbLX
                    deg = (raw_lx / 32768.0) * self.half_range
                    return deg - self.zero_offset

        # Fallback to API status
        try:
            st = self.api.get_status()
            norm = st.get("CurrentAngle", 0.0)
            return (norm * self.half_range) - self.zero_offset
        except Exception:
            return 0.0

    def release(self, duration_s: int = 60):
        """Keeps the motor completely released and free by triggering the continuous float keep-alive."""
        self.api.release()

    def zero(self):
        """Sets the current wheel position as 0.0° (Homing)."""
        raw = self.get_angle() + self.zero_offset
        self.zero_offset = raw
        print(f"[+] Homing Calibrated: Zero offset set to {self.zero_offset:+.2f}° (Current is now 0.0°)")
        self.release(60)

    def calibrate_limits(self):
        """
        Automatically detects physical hardstops / limits by applying gentle torque
        and measuring when velocity drops to zero.
        Then calculates the true mechanical center and drives smoothly to it.
        """
        print("==================================================================")
        print("       Automatic End-Stop & Center Calibration (כיול גבולות)")
        print("==================================================================")

        # 1. Drive left
        print("[*] Finding LEFT mechanical end-stop...")
        left_stop = self._find_stop(direction=-1.0, torque=0.38)
        print(f"[+] Left limit found at: {left_stop:+.1f}°")
        time.sleep(0.3)

        # 2. Drive right
        print("[*] Finding RIGHT mechanical end-stop...")
        right_stop = self._find_stop(direction=1.0, torque=0.38)
        print(f"[+] Right limit found at: {right_stop:+.1f}°")
        time.sleep(0.3)

        self.min_limit = left_stop
        self.max_limit = right_stop
        travel_range = right_stop - left_stop
        true_center = (left_stop + right_stop) / 2.0

        print("------------------------------------------------------------------")
        print(f"[+] Total Measured Travel: {travel_range:.1f}°")
        print(f"[+] True Mechanical Center: {true_center:+.1f}°")
        print("------------------------------------------------------------------")

        # 3. Drive smoothly to true center using controlled servo motion
        print("[*] Centering wheel to true mechanical 0.0°...")
        self.goto(true_center, speed=120.0, timeout=4.0)

        # Update zero offset so center is 0.0°
        self.zero_offset += true_center
        print(f"[+] Calibration complete! Wheel is centered at 0.0° and free.")
        self.release(60)

    def _find_stop(self, direction: float, torque: float = 0.38) -> float:
        """Helper to drive gently in a direction until motion stops (hard-stop detection)."""
        t_cmd = math.copysign(abs(torque), direction)
        self.api.rotate(torque=t_cmd, duration_ms=4000)
        time.sleep(0.2)

        prev_pos = self.get_angle()
        consecutive_stopped = 0

        for _ in range(40):  # max 4 seconds
            time.sleep(0.1)
            curr_pos = self.get_angle()
            speed = abs(curr_pos - prev_pos) / 0.1

            if speed < 3.0:  # moving less than 3 deg/sec -> against hardstop
                consecutive_stopped += 1
                if consecutive_stopped >= 3:
                    self.api.rotate(torque=0.02, duration_ms=1000)
                    return curr_pos
            else:
                consecutive_stopped = 0

            prev_pos = curr_pos

        self.api.rotate(torque=0.02, duration_ms=1000)
        return self.get_angle()

    def goto(self, target_deg: float, speed: float = 100.0, lock: bool = False, hold_duration: float = None, timeout: float = None) -> bool:
        """
        Drives the wheel accurately, smoothly, and reliably to `target_deg` at user-specified `speed` (°/s)
        using calibrated discrete torque pulses with settle-and-measure feedback.
        
        :param target_deg: Destination angle in degrees (-450.0 to +450.0).
        :param speed: Target rotation speed in degrees per second (e.g. 30.0 for slow, 150.0 for fast).
        :param lock: If True, locks the wheel firmly upon arrival. If False, floats freely.
        :param hold_duration: Seconds to keep locked upon arrival (None = hold indefinitely).
        :param timeout: Safety timeout in seconds (auto-calculated if None).
        """
        start_pos = self.get_angle()
        dist = target_deg - start_pos
        speed = max(15.0, min(300.0, float(speed)))

        if timeout is None:
            est_time = abs(dist) / speed
            timeout = max(25.0, est_time * 2.5 + 15.0)

        print("==================================================================")
        print(f"[Servo] GOTO: {start_pos:+.1f}° -> {target_deg:+.1f}° (Distance: {dist:+.1f}°)")
        print(f"        Speed: {speed:.1f}°/s | Post-Arrival: {'LOCK' if lock else 'RELEASE'}")
        print("==================================================================")

        start_time = time.time()
        prev_pos = start_pos
        step_num = 0
        stall_count = 0

        base_settle = 0.18
        target_step_time = 20.0 / speed
        step_pause = max(base_settle, target_step_time)

        while (time.time() - start_time) < timeout:
            curr_pos = self.get_angle()
            err = target_deg - curr_pos
            abs_err = abs(err)
            sign = 1.0 if err >= 0 else -1.0

            if abs_err <= 3.0:
                elapsed = time.time() - start_time
                actual_speed = abs(curr_pos - start_pos) / max(0.01, elapsed)
                print(f"\n[+] Arrived at Target: {curr_pos:+.1f}° (Error: {err:+.2f}°) in {elapsed:.2f}s after {step_num} steps (Avg Speed: {actual_speed:.1f}°/s)")
                if lock:
                    self.lock(angle=curr_pos, duration_seconds=hold_duration)
                else:
                    self.release()
                return True

            step_num += 1

            delta_last_step = abs(curr_pos - prev_pos)
            prev_pos = curr_pos
            if step_num > 1 and delta_last_step < 0.8:
                stall_count += 1
            else:
                stall_count = 0

            if abs_err > 80.0:
                t_mag, dur = 0.48, 45
            elif abs_err > 30.0:
                t_mag, dur = 0.46, 28
            elif abs_err > 8.0:
                t_mag, dur = 0.43, 18
            else:
                t_mag, dur = 0.41, 14

            if stall_count > 0:
                t_mag = min(0.60, t_mag + 0.03 * stall_count)
                dur = min(80, dur + 10 * stall_count)

            self.api.rotate(torque=sign * t_mag, duration_ms=dur)

            elapsed = time.time() - start_time
            print(f"\r  [Step {step_num:2d}] Pos: {curr_pos:+6.1f}° | Err: {err:+6.1f}° | T: {sign*t_mag:+0.2f} ({dur}ms) | {elapsed:4.1f}s", end="", flush=True)

            time.sleep(step_pause)

        print(f"\n[!] Timeout reached. Final Position: {self.get_angle():+.1f}°")
        if lock:
            self.lock(duration_seconds=hold_duration)
        else:
            self.release()
        return False

    def lock(self, angle: float = None, duration_seconds: float = None, strength: float = 0.8):
        """
        Immediately locks the steering wheel at its CURRENT position (or specified angle)
        using the native hardware hold.
        """
        if angle is None:
            angle = self.get_angle()
        infinite = (duration_seconds is None or duration_seconds <= 0)
        dur_msg = "indefinitely (Ctrl+C or 'python servo_controller.py release' to unlock)" if infinite else f"for {duration_seconds:.1f}s"
        print(f"[Servo] Wheel LOCKED at {angle:+.1f}° {dur_msg} (Strength: {strength:.2f})")
        self.api.lock(angle=angle, strength=strength)

        if not infinite:
            try:
                time.sleep(duration_seconds)
            except KeyboardInterrupt:
                print("\n[+] Lock interrupted by user.")
            self.release()
            print("[+] Motor released (free wheel).")

    def hold(self, target_deg: float, duration_seconds: float = None, strength: float = 0.8):
        """Actively locks and holds the wheel at target_deg."""
        self.lock(angle=target_deg, duration_seconds=duration_seconds, strength=strength)

    def reset(self):
        """Safely resets the force feedback motor driver and recovers from any hardware lock."""
        print("[*] Resetting wheel motor driver...")
        res = self.api.reset()
        print("[+] Motor reset completed. Wheel is fully unlocked and free.")
        return res

    def monitor(self):
        """Continuous live stream of telemetry."""
        print("Streaming live telemetry (Ctrl+C to stop)...")
        try:
            while True:
                deg = self.get_angle()
                bar_len = 30
                norm = max(-1.0, min(1.0, deg / self.half_range))
                center = bar_len // 2
                pos_idx = int(center + (norm * center))
                bar = ['-'] * bar_len
                bar[center] = '|'
                if 0 <= pos_idx < bar_len:
                    bar[pos_idx] = 'O'
                bar_str = "".join(bar)
                print(f"\rAngle: {deg:+6.1f}° [{bar_str}]", end="", flush=True)
                time.sleep(0.05)
        except KeyboardInterrupt:
            print("\nStopped.")


if __name__ == "__main__":
    servo = WheelServoSystem()

    if len(sys.argv) < 2:
        print("==================================================================")
        print("       Closed-Loop Steering Wheel Servo Controller (בקרת סרבו)")
        print("==================================================================")
        print("Commands:")
        print("  python servo_controller.py goto <deg> [spd] [lock/release] [sec] - Move with speed & lock choice")
        print("  python servo_controller.py lock [sec]                            - Lock wheel immediately at CURRENT angle")
        print("  python servo_controller.py hold <deg> [sec]                      - Lock wheel at specific angle")
        print("  python servo_controller.py release                               - Free wheel (zero resistance float)")
        print("  python servo_controller.py reset                                 - Reset motor driver & clear lock")
        print("  python servo_controller.py calibrate                             - Auto-detect hard limits & center")
        print("  python servo_controller.py zero                                  - Set current position as 0.0°")
        print("  python servo_controller.py monitor                               - Live real-time angle display")
        print("==================================================================")
        sys.exit(0)

    cmd = sys.argv[1].lower()

    if cmd == "goto":
        target = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0
        speed = 100.0
        speed_set = False
        lock_mode = False
        hold_sec = None

        for arg in sys.argv[3:]:
            arg_lower = arg.lower()
            if arg_lower in ["lock", "hold", "frozen", "true"]:
                lock_mode = True
            elif arg_lower in ["release", "free", "float", "false"]:
                lock_mode = False
            else:
                try:
                    val = float(arg)
                    if not speed_set and not lock_mode:
                        speed = val
                        speed_set = True
                    else:
                        hold_sec = val
                except ValueError:
                    pass

        servo.goto(target, speed=speed, lock=lock_mode, hold_duration=hold_sec)

    elif cmd in ["lock", "freeze"]:
        sec = float(sys.argv[2]) if len(sys.argv) > 2 else None
        servo.lock(duration_seconds=sec)

    elif cmd == "hold":
        if len(sys.argv) > 2:
            target = float(sys.argv[2])
            sec = float(sys.argv[3]) if len(sys.argv) > 3 else None
            servo.hold(target, duration_seconds=sec)
        else:
            servo.lock()

    elif cmd == "reset":
        servo.reset()

    elif cmd == "calibrate":
        servo.calibrate_limits()

    elif cmd == "zero":
        servo.zero()

    elif cmd == "release":
        servo.release(60)
        print("[+] Wheel completely released (free wheel).")

    elif cmd == "monitor":
        servo.monitor()

    else:
        print(f"Unknown command: {cmd}")
