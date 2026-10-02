# Windows Hardware Drivers for Xbox Steering Wheel (Force Feedback)

This directory contains the original Windows hardware drivers (`.inf`, `.sys`) required for hardware recognition, communication, and motor control of the force feedback racing wheel (`VID_044F&PID_B664`).

---

## Driver Packages Overview

### 1. `dc1-controller/` (`dc1-controller.inf`, `dc1-controller.sys`)
* **Role:** The primary USB composite driver (Xbox Composite Device) that recognizes the physical hardware on the USB bus.
* **Matched Hardware IDs:**
  * `USB\VID_044F&PID_B664` (Thrustmaster racing wheel base)
  * `USB\MS_COMP_XGIP10` (Xbox Game Input Protocol descriptor)

### 2. `xboxgip/` (`xboxgip.inf`, `xboxgip.sys`, `devauthe.sys`)
* **Role:** Microsoft's core kernel-mode driver for the Game Input Protocol (GIP). Handles device authentication, bidirection input stream, and actuators/Force Feedback motor packet transport.

### 3. `xboxgipsynthetic/` (`xboxgipsynthetic.inf`)
* **Role:** Driver for synthetic Xbox virtual input devices and modern Windows.Gaming.Input / XInput runtime mapping.

---

## Manual Driver Installation / Reinstall

If the steering wheel is connected to a fresh Windows PC or driver bindings need to be refreshed, install them using PowerShell (as Administrator):

```powershell
pnputil /add-driver dc1-controller\dc1-controller.inf /install
pnputil /add-driver xboxgip\xboxgip.inf /install
```
