using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Gaming.Input;
using Windows.Gaming.Input.ForceFeedback;

namespace XboxWheelCompatibility.WheelTransformer
{
    public enum WheelOperationMode
    {
        Released,
        Holding,
        Rotating
    }

    public class WheelMotorStatus
    {
        public bool WheelConnected { get; set; }
        public bool HasMotor { get; set; }
        public bool MotorEnabled { get; set; }
        public double MasterGain { get; set; }
        public string SupportedAxes { get; set; } = "None";
        public double CurrentAngle { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public string ActiveEffect { get; set; } = "None";
        public string Mode { get; set; } = "Released";
        public string ConstantEffectState { get; set; } = "None";
        public double ConstantEffectGain { get; set; }
        public double? TargetAngle { get; set; }
        public string LastLoadResult { get; set; } = "None";
        public string LastError { get; set; } = "";
    }

    public static class WheelMotorController
    {
        private static ConstantForceEffect? _constantForceEffect;
        private static string _currentEffectName = "None";
        private static string _lastLoadResult = "None";
        private static string _lastError = "";
        private static readonly object _lock = new();
        private static readonly SemaphoreSlim _motorLock = new(1, 1);

        private static WheelOperationMode _mode = WheelOperationMode.Released;
        private static DateTime _rotateUntil = DateTime.MinValue;
        private static double? _targetAngle = null;
        private static double _holdStrength = 0.8;
        private static int _commandGen = 0;

        private static readonly Timer _holdLoopTimer;
        private static DateTime _lastHoldPacketTime = DateTime.MinValue;
        private static float _lastHoldTorque = 0.0f;

        static WheelMotorController()
        {
            // Robotic hold monitor runs every 50ms
            _holdLoopTimer = new Timer(HoldLoopTick, null, 500, 50);
        }

        private static ForceFeedbackMotor? GetMotor()
        {
            return WheelManager.MainWheel?.WheelMotor;
        }

        private static async Task<bool> EnsureEffectLoadedAsync(ForceFeedbackMotor motor)
        {
            if (!motor.IsEnabled)
            {
                await motor.TryEnableAsync();
                motor.MasterGain = 1.0;
            }

            // If effect is null or has stopped/faulted, cleanly reload a fresh one
            if (_constantForceEffect == null || _constantForceEffect.State != ForceFeedbackEffectState.Running)
            {
                if (_constantForceEffect != null)
                {
                    try { _constantForceEffect.Stop(); } catch { }
                    _constantForceEffect = null;
                }

                _constantForceEffect = new ConstantForceEffect();
                var res = await motor.LoadEffectAsync(_constantForceEffect);
                _lastLoadResult = res.ToString();
                if (res == ForceFeedbackLoadEffectResult.Succeeded)
                {
                    _constantForceEffect.SetParameters(new Vector3(1.0f, 0, 0), TimeSpan.FromSeconds(300));
                    _constantForceEffect.Gain = 0.0;
                    _constantForceEffect.Start();
                }
                return res == ForceFeedbackLoadEffectResult.Succeeded;
            }

            return true;
        }

        public static async Task<bool> ResetMotorAsync()
        {
            await _motorLock.WaitAsync();
            try
            {
                var motor = GetMotor();
                if (motor == null) return false;

                if (!motor.IsEnabled)
                {
                    await motor.TryEnableAsync();
                }
                motor.MasterGain = 1.0;

                if (_constantForceEffect != null)
                {
                    try { _constantForceEffect.Stop(); } catch { }
                    _constantForceEffect = null;
                }

                await EnsureEffectLoadedAsync(motor);

                _mode = WheelOperationMode.Released;
                _targetAngle = null;
                _lastHoldTorque = 0.0f;
                lock (_lock)
                {
                    _currentEffectName = "Reset & Released";
                }
                _lastError = "";
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Reset error: {ex}";
                return false;
            }
            finally
            {
                _motorLock.Release();
            }
        }

        private static async void HoldLoopTick(object? state)
        {
            if (_mode != WheelOperationMode.Holding || !_targetAngle.HasValue) return;

            // Non-blocking try-lock: if another command is using the motor, skip this tick
            if (!await _motorLock.WaitAsync(0)) return;

            try
            {
                if (_mode != WheelOperationMode.Holding || !_targetAngle.HasValue) return;

                var wheel = WheelManager.MainWheel;
                if (wheel == null) return;

                var motor = wheel.WheelMotor;
                if (motor == null) return;

                await EnsureEffectLoadedAsync(motor);
                if (_constantForceEffect == null) return;

                double currentAngle = wheel.GetCurrentReading().Wheel * 450.0;
                double error = _targetAngle.Value - currentAngle;
                double absError = Math.Abs(error);
                DateTime now = DateTime.UtcNow;

                if (absError <= 1.5)
                {
                    if (_lastHoldTorque > 0.001f)
                    {
                        _constantForceEffect.Gain = 0.0;
                        _lastHoldTorque = 0.0f;
                        _lastHoldPacketTime = now;
                    }
                }
                else
                {
                    if ((now - _lastHoldPacketTime).TotalMilliseconds >= 50)
                    {
                        double p = absError * 0.035 * _holdStrength;
                        double fade = Math.Min(1.0, (absError - 1.5) / 3.0);
                        double ff = 0.35 * _holdStrength * fade;
                        float totalTorque = (float)Math.Clamp(p + ff, 0.40, 0.90);

                        Vector3 direction = error >= 0 ? new Vector3(1.0f, 0, 0) : new Vector3(-1.0f, 0, 0);
                        _constantForceEffect.SetParameters(direction, TimeSpan.FromSeconds(300));
                        _constantForceEffect.Gain = totalTorque;
                        if (_constantForceEffect.State != ForceFeedbackEffectState.Running)
                        {
                            _constantForceEffect.Start();
                        }

                        _lastHoldTorque = totalTorque;
                        _lastHoldPacketTime = now;

                        lock (_lock)
                        {
                            _currentEffectName = $"Holding at {_targetAngle.Value:+0.0;-0.0}° (Resisting: {totalTorque:F2}, Err: {error:+0.0}°)";
                        }
                    }
                }
            }
            catch { }
            finally
            {
                _motorLock.Release();
            }
        }

        public static WheelMotorStatus GetStatus()
        {
            var status = new WheelMotorStatus();
            var wheel = WheelManager.MainWheel;
            if (wheel == null)
            {
                return status;
            }

            status.WheelConnected = true;
            try
            {
                var reading = wheel.GetCurrentReading();
                status.CurrentAngle = reading.Wheel;
                status.Throttle = reading.Throttle;
                status.Brake = reading.Brake;
            }
            catch (Exception ex)
            {
                status.LastError = $"Reading error: {ex}";
            }

            var motor = wheel.WheelMotor;
            if (motor != null)
            {
                status.HasMotor = true;
                status.MotorEnabled = motor.IsEnabled;
                status.MasterGain = motor.MasterGain;
                status.SupportedAxes = motor.SupportedAxes.ToString();
                status.ActiveEffect = _currentEffectName;
                status.Mode = _mode.ToString();
                status.TargetAngle = _targetAngle;

                if (_constantForceEffect != null)
                {
                    status.ConstantEffectState = _constantForceEffect.State.ToString();
                    status.ConstantEffectGain = _constantForceEffect.Gain;
                }
                status.LastLoadResult = _lastLoadResult;
                status.LastError = _lastError;
            }

            return status;
        }

        public static async Task<bool> ReleaseWheelAsync()
        {
            await _motorLock.WaitAsync();
            try
            {
                Interlocked.Increment(ref _commandGen);
                _mode = WheelOperationMode.Released;
                _rotateUntil = DateTime.MinValue;
                _targetAngle = null;
                _lastHoldTorque = 0.0f;

                lock (_lock)
                {
                    _currentEffectName = "Released (Zero Resistance)";
                }

                var motor = GetMotor();
                if (motor == null) return false;

                await EnsureEffectLoadedAsync(motor);
                if (_constantForceEffect == null) return false;

                _constantForceEffect.SetParameters(new Vector3(1.0f, 0, 0), TimeSpan.FromSeconds(300));
                _constantForceEffect.Gain = 0.0;
                if (_constantForceEffect.State != ForceFeedbackEffectState.Running)
                {
                    _constantForceEffect.Start();
                }

                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Release error: {ex.Message}";
                return false;
            }
            finally
            {
                _motorLock.Release();
            }
        }

        public static Task<bool> HoldWheelAsync(double strength = 0.8)
        {
            return HoldWheelAsync(null, strength);
        }

        public static async Task<bool> HoldWheelAsync(double? targetAngle, double strength = 0.8)
        {
            await _motorLock.WaitAsync();
            try
            {
                Interlocked.Increment(ref _commandGen);
                var wheel = WheelManager.MainWheel;
                if (wheel == null) return false;

                var motor = wheel.WheelMotor;
                if (motor == null) return false;

                await EnsureEffectLoadedAsync(motor);
                if (_constantForceEffect == null) return false;

                double currentDeg = wheel.GetCurrentReading().Wheel * 450.0;
                _targetAngle = targetAngle ?? currentDeg;
                _holdStrength = Math.Clamp(strength, 0.1, 1.0);
                _mode = WheelOperationMode.Holding;
                _rotateUntil = DateTime.MinValue;
                _lastHoldTorque = 0.0f;
                _lastHoldPacketTime = DateTime.MinValue;

                lock (_lock)
                {
                    _currentEffectName = $"Locked/Holding at {_targetAngle.Value:+0.0;-0.0}° (Strength: {_holdStrength:F2})";
                }

                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Hold error: {ex.Message}";
                return false;
            }
            finally
            {
                _motorLock.Release();
            }
        }

        public static async Task<bool> CenterWheelAsync(double strength = 0.8)
        {
            return await HoldWheelAsync(0.0, strength);
        }

        public static async Task<bool> RotateWheelAsync(float torque, int durationMs = 500)
        {
            await _motorLock.WaitAsync();
            try
            {
                var motor = GetMotor();
                if (motor == null) return false;

                await EnsureEffectLoadedAsync(motor);
                if (_constantForceEffect == null) return false;

                float clampedTorque = Math.Clamp(torque, -1.0f, 1.0f);
                float absTorque = Math.Abs(clampedTorque);
                if (absTorque < 0.02f)
                {
                    _constantForceEffect.Gain = 0.0;
                    _mode = WheelOperationMode.Released;
                    lock (_lock) { _currentEffectName = "Released (Zero Resistance)"; }
                    return true;
                }

                _mode = WheelOperationMode.Rotating;
                _targetAngle = null;

                Vector3 direction = clampedTorque >= 0 ? new Vector3(1.0f, 0, 0) : new Vector3(-1.0f, 0, 0);
                _constantForceEffect.SetParameters(direction, TimeSpan.FromSeconds(300));
                _constantForceEffect.Gain = Math.Clamp(absTorque, 0.0, 1.0);
                if (_constantForceEffect.State != ForceFeedbackEffectState.Running)
                {
                    _constantForceEffect.Start();
                }

                lock (_lock)
                {
                    _currentEffectName = $"Rotating (Torque: {clampedTorque:+0.00;-0.00}, Duration: {durationMs}ms)";
                }

                if (durationMs > 0)
                {
                    await Task.Delay(durationMs);
                    _constantForceEffect.Gain = 0.0;
                    _mode = WheelOperationMode.Released;
                    lock (_lock) { _currentEffectName = "Released (Zero Resistance)"; }
                }

                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Rotate error: {ex}";
                return false;
            }
            finally
            {
                _motorLock.Release();
            }
        }

        public static async Task<bool> GotoAngleAsync(double targetDeg, double speedDegS = 100.0, bool lockAtEnd = false, int timeoutMs = 8000)
        {
            await _motorLock.WaitAsync();
            try
            {
                var wheel = WheelManager.MainWheel;
                if (wheel == null) return false;
                var motor = wheel.WheelMotor;
                if (motor == null) return false;

                await EnsureEffectLoadedAsync(motor);
                if (_constantForceEffect == null) return false;

                int currentGen = Interlocked.Increment(ref _commandGen);
                _mode = WheelOperationMode.Rotating;
                _rotateUntil = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                _targetAngle = targetDeg;

                speedDegS = Math.Clamp(speedDegS, 20.0, 300.0);

                double prevPos = wheel.GetCurrentReading().Wheel * 450.0;
                DateTime prevTime = DateTime.UtcNow;
                DateTime startTime = prevTime;
                DateTime lastPacketTime = DateTime.MinValue;
                float lastSentTorque = 0.0f;
                int settledFrames = 0;

                while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
                {
                    if (Volatile.Read(ref _commandGen) != currentGen)
                    {
                        return false;
                    }

                    await Task.Delay(25);
                    DateTime now = DateTime.UtcNow;
                    double dt = (now - prevTime).TotalSeconds;
                    if (dt < 0.005) dt = 0.005;
                    prevTime = now;

                    double currPos = wheel.GetCurrentReading().Wheel * 450.0;
                    double vel = (currPos - prevPos) / dt;
                    prevPos = currPos;

                    double error = targetDeg - currPos;
                    double absErr = Math.Abs(error);

                    // Arrival detection
                    if (absErr <= 2.5 && Math.Abs(vel) < 25.0)
                    {
                        settledFrames++;
                        if (settledFrames >= 3)
                        {
                            if (lockAtEnd)
                            {
                                _targetAngle = targetDeg;
                                _holdStrength = 0.8;
                                _mode = WheelOperationMode.Holding;
                                _constantForceEffect.Gain = 0.0;
                                lock (_lock) { _currentEffectName = $"Locked/Holding at {targetDeg:+0.0;-0.0}°"; }
                                return true;
                            }
                            else
                            {
                                _mode = WheelOperationMode.Released;
                                _targetAngle = null;
                                _constantForceEffect.Gain = 0.0;
                                lock (_lock) { _currentEffectName = "Released (Zero Resistance)"; }
                                return true;
                            }
                        }
                    }
                    else
                    {
                        settledFrames = 0;
                    }

                    // Physics-based active braking control logic
                    double vMag = Math.Abs(vel);
                    double stoppingDist = (vMag * vMag) / 2400.0;
                    bool movingTowardsTarget = (vel > 0 && error > 0) || (vel < 0 && error < 0);

                    float cmdTorque;
                    if (absErr <= 2.0 && vMag < 20.0)
                    {
                        cmdTorque = 0.0f;
                    }
                    else if (movingTowardsTarget && absErr <= (stoppingDist + 3.0) && vMag > 20.0)
                    {
                        cmdTorque = -(float)(Math.Sign(vel) * 0.44);
                    }
                    else if (!movingTowardsTarget && vMag > 25.0)
                    {
                        cmdTorque = -(float)(Math.Sign(vel) * 0.44);
                    }
                    else if (vMag < speedDegS * 0.9)
                    {
                        cmdTorque = (float)(Math.Sign(error) * 0.44);
                    }
                    else
                    {
                        cmdTorque = 0.0f;
                    }

                    bool dirChanged = (cmdTorque > 0 && lastSentTorque <= 0) || (cmdTorque < 0 && lastSentTorque >= 0) || (cmdTorque == 0 && lastSentTorque != 0);
                    bool torqueDiff = Math.Abs(cmdTorque - lastSentTorque) > 0.08f;
                    bool timeElapsed = (now - lastPacketTime).TotalMilliseconds >= 100;

                    if (dirChanged || torqueDiff || timeElapsed)
                    {
                        lastPacketTime = now;
                        lastSentTorque = cmdTorque;

                        if (Math.Abs(cmdTorque) < 0.02f)
                        {
                            _constantForceEffect.Gain = 0.0;
                        }
                        else
                        {
                            Vector3 direction = cmdTorque >= 0 ? new Vector3(1.0f, 0, 0) : new Vector3(-1.0f, 0, 0);
                            _constantForceEffect.SetParameters(direction, TimeSpan.FromSeconds(300));
                            _constantForceEffect.Gain = Math.Abs(cmdTorque);
                            if (_constantForceEffect.State != ForceFeedbackEffectState.Running)
                            {
                                _constantForceEffect.Start();
                            }
                        }

                        lock (_lock)
                        {
                            _currentEffectName = $"Goto -> {targetDeg:+0.0}° (Err: {error:+0.0}°, Vel: {vel:+0}°, T: {cmdTorque:+0.00})";
                        }
                    }
                }

                if (lockAtEnd)
                {
                    _targetAngle = targetDeg;
                    _holdStrength = 0.8;
                    _mode = WheelOperationMode.Holding;
                    _constantForceEffect.Gain = 0.0;
                }
                else
                {
                    _mode = WheelOperationMode.Released;
                    _targetAngle = null;
                    _constantForceEffect.Gain = 0.0;
                }
                return false;
            }
            catch (Exception ex)
            {
                _lastError = $"Goto error: {ex.Message}";
                return false;
            }
            finally
            {
                _motorLock.Release();
            }
        }

        public static async Task<bool> SetGainAsync(double gain)
        {
            await _motorLock.WaitAsync();
            try
            {
                var motor = GetMotor();
                if (motor == null) return false;

                if (!motor.IsEnabled) await motor.TryEnableAsync();
                motor.MasterGain = Math.Clamp(gain, 0.0, 1.0);
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"SetGain error: {ex.Message}";
                return false;
            }
            finally
            {
                _motorLock.Release();
            }
        }
    }
}
