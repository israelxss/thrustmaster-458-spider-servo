using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Gaming.Input;

namespace XboxWheelCompatibility.CommunicationInterface
{
    public interface IWheelCompatibilityService
    {
        public int GetMainWheelIndex();
        public void Stop();
        public void Start();

        // Motor Control API
        public bool ReleaseWheel();
        public bool HoldWheel(double strength);
        public bool CenterWheel(double strength);
        public bool RotateWheel(float torque, int durationMs);
        public bool SetGain(double gain);
        public string GetStatusJson();
    }
}
