"""
Wheel Motor Controller API Client for Python.
Enables programmatic control over steering wheel holding, release, rotation, and telemetry.
Connects directly to the native Wheel Motor Service on localhost:16582.
"""

import http.client
import json

class WheelMotorAPI:
    def __init__(self, host="127.0.0.1", port=16582):
        self.host = host
        self.port = port
        self.conn = None
        self._ensure_conn()

    def _ensure_conn(self):
        if self.conn is None:
            self.conn = http.client.HTTPConnection(self.host, self.port, timeout=2.0)

    def _request(self, method: str, path: str):
        for attempt in range(2):
            try:
                self._ensure_conn()
                self.conn.request(method, path)
                resp = self.conn.getresponse()
                data = resp.read().decode("utf-8")
                parsed = json.loads(data)
                if isinstance(parsed, dict):
                    if "status" in parsed and isinstance(parsed["status"], dict):
                        self.last_status = parsed["status"]
                    elif "CurrentAngle" in parsed:
                        self.last_status = parsed
                return parsed
            except Exception:
                try:
                    if self.conn:
                        self.conn.close()
                except Exception:
                    pass
                self.conn = None
                if attempt == 1:
                    raise
        return {}

    def get_status(self) -> dict:
        """Retrieves full telemetry including angle, pedals, motor status, and active effect."""
        return self._request("GET", "/api/status")

    def release(self) -> dict:
        """Releases the steering wheel motor completely (zero resistance, free wheel)."""
        return self._request("POST", "/api/motor/release")

    def hold(self, strength: float = 0.8) -> dict:
        """Locks/holds the steering wheel with damping/resistance."""
        strength = max(0.0, min(1.0, float(strength)))
        return self._request("POST", f"/api/motor/hold?strength={strength:.2f}")

    def lock(self, angle: float = None, strength: float = 0.8) -> dict:
        """Locks/holds the wheel at an exact angle (or current angle if None)."""
        strength = max(0.0, min(1.0, float(strength)))
        query = f"strength={strength:.2f}"
        if angle is not None:
            query += f"&angle={float(angle):.2f}"
        return self._request("POST", f"/api/motor/lock?{query}")

    def reset(self) -> dict:
        """Safely resets the force feedback motor driver and clears any hardware failsafe lock."""
        return self._request("POST", "/api/motor/reset")

    def center(self, strength: float = 0.8) -> dict:
        """Applies a centering spring effect that pulls the wheel straight (0 degrees)."""
        strength = max(0.0, min(1.0, float(strength)))
        return self._request("POST", f"/api/motor/center?strength={strength:.2f}")

    def rotate(self, torque: float = 0.5, duration_ms: int = 500) -> dict:
        """
        Actively rotates the wheel left or right with the commanded torque.
        torque: -1.0 (full left) to +1.0 (full right).
        duration_ms: duration in milliseconds.
        """
        torque = max(-1.0, min(1.0, float(torque)))
        return self._request("POST", f"/api/motor/rotate?torque={torque:.2f}&duration={int(duration_ms)}")

    def set_gain(self, gain: float = 1.0) -> dict:
        """Sets the master motor gain (0.0 to 1.0)."""
        gain = max(0.0, min(1.0, float(gain)))
        return self._request("POST", f"/api/motor/gain?value={gain:.2f}")

if __name__ == "__main__":
    import sys

    api = WheelMotorAPI()
    if len(sys.argv) < 2:
        print("Usage: python wheel_motor_api.py [release | hold | center | rotate <torque> | status]")
        sys.exit(0)

    cmd = sys.argv[1].lower()
    try:
        if cmd == "release":
            res = api.release()
            print("RELEASE SUCCESS:", json.dumps(res, indent=2))
        elif cmd == "hold":
            str_val = float(sys.argv[2]) if len(sys.argv) > 2 else 0.8
            res = api.hold(str_val)
            print(f"HOLD SUCCESS (strength={str_val}):", json.dumps(res, indent=2))
        elif cmd == "center":
            str_val = float(sys.argv[2]) if len(sys.argv) > 2 else 0.8
            res = api.center(str_val)
            print(f"CENTER SUCCESS (strength={str_val}):", json.dumps(res, indent=2))
        elif cmd == "rotate":
            torque = float(sys.argv[2]) if len(sys.argv) > 2 else 0.5
            duration = int(sys.argv[3]) if len(sys.argv) > 3 else 500
            res = api.rotate(torque, duration)
            print(f"ROTATE SUCCESS (torque={torque}, duration={duration}ms):", json.dumps(res, indent=2))
        elif cmd == "status":
            res = api.get_status()
            print("STATUS:", json.dumps(res, indent=2))
        else:
            print(f"Unknown command: {cmd}")
    except Exception as e:
        print(f"Error communicating with Wheel Motor API: {e}")
        print("Make sure the upgraded Wheel Compatibility Service is running.")
