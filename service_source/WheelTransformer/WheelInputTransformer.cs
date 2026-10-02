using System;
using System.Collections.Generic;

namespace XboxWheelCompatibility.WheelTransformer
{
    public class WheelInputTransformer
    {
        public static void Start()
        {
            LifecycleManager.Start();

            WheelManager.Initialize();
            // InjectionManager.Initialize(); // Disabled: Prevents XboxgipSynthetic.dll 0xc0000409 crash in Session 0
        }

        public static void Stop()
        {
            LifecycleManager.Stop();
            // InjectionManager.Destroy();
        }
    }
}
