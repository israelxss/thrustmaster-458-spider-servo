using System.Text.Json;
using ServiceWire.TcpIp;
using Windows.Gaming.Input;
using XboxWheelCompatibility.CommunicationInterface;
using XboxWheelCompatibility.WheelTransformer;

namespace XboxWheelCompatibility.WheelCompatibilityService
{
    public class WheelCompatibilityWorker : BackgroundService, IWheelCompatibilityService
    {
        private readonly ILogger<WheelCompatibilityWorker> Logger;
        private readonly TcpHost TCPHost;
        private readonly HttpMotorServer HTTPServer;

        public int GetMainWheelIndex()
        {
            return RacingWheel.RacingWheels.ToList().IndexOf(WheelManager.MainWheel);
        }

        public void Start()
        {
            WheelInputTransformer.Start();
        }

        public void Stop()
        {
            WheelInputTransformer.Stop();
        }

        public bool ReleaseWheel()
        {
            return WheelMotorController.ReleaseWheelAsync().GetAwaiter().GetResult();
        }

        public bool HoldWheel(double strength)
        {
            return WheelMotorController.HoldWheelAsync(strength).GetAwaiter().GetResult();
        }

        public bool CenterWheel(double strength)
        {
            return WheelMotorController.CenterWheelAsync(strength).GetAwaiter().GetResult();
        }

        public bool RotateWheel(float torque, int durationMs)
        {
            return WheelMotorController.RotateWheelAsync(torque, durationMs).GetAwaiter().GetResult();
        }

        public bool SetGain(double gain)
        {
            return WheelMotorController.SetGainAsync(gain).GetAwaiter().GetResult();
        }

        public string GetStatusJson()
        {
            return JsonSerializer.Serialize(WheelMotorController.GetStatus());
        }

        public WheelCompatibilityWorker(ILogger<WheelCompatibilityWorker> logger)
        {
            Logger = logger;
            TCPHost = new TcpHost(16581);
            HTTPServer = new HttpMotorServer(16582);
        }

        protected override Task ExecuteAsync(CancellationToken Cancellation)
        {
            TCPHost.AddService<IWheelCompatibilityService>(this);

            WheelInputTransformer.Start();

            TCPHost.Open();
            HTTPServer.Start();

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken Cancellation)
        {
            HTTPServer.Stop();
            TCPHost.Close();

            WheelInputTransformer.Stop();

            return Task.CompletedTask;
        }
    }
}
