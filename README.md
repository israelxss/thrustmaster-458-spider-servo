# Steering Wheel Closed-Loop Servo Controller 🎮🏎️
### Force Feedback Racing Wheel Servo Positioning & Motor Control System

A complete closed-loop force feedback (FFB) steering wheel servo system enabling high-precision angular positioning, speed control, active robotic holding/locking, zero-resistance free float, mechanical limit calibration, and real-time telemetry — **with Zero UAC / non-admin execution**.

---

## Key Features

* **Precision Angle & Speed Control (`goto`):** Drive the wheel accurately to any target angle (`-450.0°` to `+450.0°`) at a user-defined speed (`°/s`), with built-in anti-overshoot settling and adaptive stall recovery to overcome mechanical static friction.
* **Selectable Post-Arrival State:** Choose whether the wheel stays firmly **locked** (`lock`) or completely **released** (`release`) once it reaches the destination.
* **Instant Lock at Any Angle (`lock`):** Instantly lock the wheel at its current position (or any target angle) with active robotic resistance, either indefinitely or for a specified duration.
* **Full Motor Release (`release`):** Completely release the motor and eliminate manufacturer centering springs (Free Float / Zero Resistance).
* **Instant Driver Reset (`reset`):** Safely reinitialize and refresh the force feedback motor driver within milliseconds whenever needed.
* **Automatic End-Stop Calibration (`calibrate`):** Detect physical mechanical hardstops left and right and find the true mechanical center automatically.
* **Real-Time Telemetry Monitor (`monitor`):** Stream live steering angles and pedal inputs directly in the terminal.
* **Zero UAC Elevation (Non-Admin):** The background service runs under `SYSTEM`, allowing standard command-line scripts to control the motor immediately without annoying Windows Administrator prompts.

---

## Quick Start CLI Commands

All commands are executed via [`servo_controller.py`](servo_controller.py):

```powershell
# 1. Move to angle with speed control and release upon arrival:
python servo_controller.py goto 200 40 release
python servo_controller.py goto -90 120 release

# 2. Move to angle with speed control and lock upon arrival (indefinitely or for N seconds):
python servo_controller.py goto 0 100 lock
python servo_controller.py goto 45 60 lock 5

# 3. Lock wheel at CURRENT position (indefinitely or for N seconds):
python servo_controller.py lock
python servo_controller.py lock 10

# 4. Completely release wheel (zero resistance free float):
python servo_controller.py release

# 5. Fast motor driver reset & recover:
python servo_controller.py reset

# 6. Calibrate mechanical hard limits and center:
python servo_controller.py calibrate

# 7. Set current position as 0.0° (Homing):
python servo_controller.py zero

# 8. Real-time live angle display in terminal:
python servo_controller.py monitor
```

---

## Automated Verification Suite

To verify all system operational modes on physical hardware:

```powershell
python test_all_modes.py
```

The test suite runs 6 automated checks:
1. Driver reset, status verification, and motor health.
2. Bidirectional rotation (right & left torque response).
3. Slow motion positioning (35°/s) with post-arrival release.
4. Fast motion positioning (140°/s) with post-arrival release.
5. Center positioning (0.0°) with post-arrival active robotic lock (`Holding`).
6. Instant motor release (`Released`, zero resistance).

---

## Architecture Overview

```
+-------------------------------------------------------------+
|             User Application / Python Scripts               |
|      (servo_controller.py / wheel_motor_api.py)             |
+-------------------------------------------------------------+
                              |
                     HTTP REST (Port 16582)
                              |
+-------------------------------------------------------------+
|    WheelCompatibilityService (Windows Service under SYSTEM) |
|  - HttpMotorServer (Embedded High-Speed HTTP Listener)      |
|  - WheelMotorController (Thread-Safe Motor Engine)          |
|  - Windows.Gaming.Input.ForceFeedback (WinRT Engine)        |
+-------------------------------------------------------------+
                              |
                      USB HID / Xbox GIP
                              |
+-------------------------------------------------------------+
|            Physical Steering Wheel & Motor Hardware         |
|      (Thrustmaster, Logitech, Fanatec, Direct Drive)        |
+-------------------------------------------------------------+
```

---

## One-Time Setup

1. **Grant permanent permissions (Zero UAC):**
   Run the setup script once from an elevated PowerShell prompt:
   ```powershell
   powershell -ExecutionPolicy Bypass -File setup_permanent_admin.ps1
   ```
2. The service is installed and listens locally on `http://127.0.0.1:16582`.
3. From this point forward, all Python scripts and CLI commands run smoothly from standard, non-elevated user accounts without any UAC popups.

---

## Compiling from Source (Optional)

> [!NOTE]
> **No compilation required!** The [`published_service/`](published_service/) directory contains precompiled standalone binaries ready to run out of the box.

If you wish to recompile the Windows service from source (using only the lightweight .NET CLI, without Visual Studio):

1. **Install .NET SDK CLI (if not already installed):**
   ```powershell
   winget install Microsoft.DotNet.SDK.8
   ```
2. **Compile in a single command:**
   ```powershell
   dotnet publish service_source/WheelCompatibilityService/WheelCompatibilityService.csproj -c Release -o published_service
   ```
3. **Deploy updated binary to the service (zero admin needed):**
   ```powershell
   Stop-Service WheelCompatibilityService
   Copy-Item published_service\* 'C:\Program Files (x86)\XboxWheelCompatibility\Service\' -Force -Recurse
   Start-Service WheelCompatibilityService
   ```

---

## Project Structure

* [`servo_controller.py`](servo_controller.py) - Main closed-loop servo controller CLI.
* [`wheel_motor_api.py`](wheel_motor_api.py) - Python HTTP client library for motor control.
* [`test_all_modes.py`](test_all_modes.py) - Automated physical verification test suite.
* [`setup_permanent_admin.ps1`](setup_permanent_admin.ps1) - One-time permission setup script (Zero UAC).
* [`published_service/`](published_service/) - Precompiled service binaries (Plug & Play, no SDK required).
* [`service_source/`](service_source/) - Full C# (.NET) source code for the Windows service and `WheelMotorController`.
* [`drivers/`](drivers/) - Original Windows hardware driver packages (`.inf`, `.sys`) for the wheel.
* [`WHEEL_MOTOR_PROTOCOL.md`](WHEEL_MOTOR_PROTOCOL.md) - Complete technical protocol and HTTP endpoint specification.
* [`tmp/`](tmp/) - Temporary scratch scripts and logs kept for reference.

---

## License
This project is licensed under the MIT License.
