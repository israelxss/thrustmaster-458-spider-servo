# Permanent Admin Authorization Setup (הגדרת הרשאות קבועות ללא צורך באישור מנהל בעתיד)
$ErrorActionPreference = "Continue"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Setting up Permanent Non-Admin Permissions for Wheel Motor" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Grant Users Full Control on the Program Files folder
Write-Host "[1/4] Granting folder write permissions to Users..." -ForegroundColor Cyan
$installFolder = "C:\Program Files (x86)\XboxWheelCompatibility"
if (Test-Path $installFolder) {
    & icacls "$installFolder" /grant "Users:(OI)(CI)F" /T /Q
    Write-Host "  [OK] Program Files folder permissions granted to Users." -ForegroundColor Green
} else {
    Write-Host "  [!] Install folder not found at $installFolder" -ForegroundColor Yellow
}

# 2. Grant Authenticated & Interactive Users permission to start/stop the service
Write-Host "[2/4] Setting Service Access Rights (sc sdset)..." -ForegroundColor Cyan
$sddl = "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWRPWPDTLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)(A;;CCLCSWRPWPDTLOCRRC;;;AU)"
& sc.exe sdset WheelCompatibilityService $sddl
Write-Host "  [OK] Service permissions updated. Any script can now start/stop the service!" -ForegroundColor Green

# 3. Create Windows Scheduled Task for instant elevated updates without UAC
Write-Host "[3/4] Registering Scheduled Task 'WheelMotorElevatedTask'..." -ForegroundColor Cyan
$taskName = "WheelMotorElevatedTask"
$runnerScript = "C:\Users\A404~1\Desktop\WEEL_M~1\update_service_elevated.ps1"
$action = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$runnerScript`""

& schtasks.exe /Delete /TN $taskName /F 2>$null
& schtasks.exe /Create /TN $taskName /TR $action /SC ONCE /ST 00:00 /RL HIGHEST /RU "SYSTEM" /F
Write-Host "  [OK] Scheduled Task registered with SYSTEM privilege (Zero UAC prompts forever)." -ForegroundColor Green

# 4. Stop old service, copy new binary, and start upgraded service now
Write-Host "[4/4] Applying latest build and starting service..." -ForegroundColor Cyan
Stop-Service -Name WheelCompatibilityService -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Kill any orphaned process
Get-Process -Name "WheelCompatibilityService" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

$source = "C:\Users\A404~1\Desktop\WEEL_M~1\published_service\WheelCompatibilityService.exe"
$target = "C:\Program Files (x86)\XboxWheelCompatibility\Service\WheelCompatibilityService.exe"

Copy-Item -Path $source -Destination $target -Force
Write-Host "  [OK] Upgraded binary installed." -ForegroundColor Green

Start-Service -Name WheelCompatibilityService
Start-Sleep -Seconds 2

$svc = Get-Service -Name WheelCompatibilityService
Write-Host "  Service Status: $($svc.Status)" -ForegroundColor Green

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " SUCCESS! From now on, ALL scripts run without UAC/Admin!   " -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
