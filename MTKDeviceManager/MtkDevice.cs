using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace MTKDeviceManager
{
    class MtkDevice
    {
        public event Action<string> Log;

        public void Start()
        {
            Log?.Invoke("Starting device detection...");
            var query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            var watcher = new System.Management.ManagementEventWatcher(query);
            watcher.EventArrived += (sender, e) =>
            {
                var instance = (System.Management.ManagementBaseObject)e.NewEvent["TargetInstance"];
                var deviceId = (string)instance["DeviceID"];
                if (deviceId.Contains("VID_0E8D&PID_0003") || deviceId.Contains("VID_0E8D&PID_2000"))
                {
                    Log?.Invoke($"MTK device connected: {instance["Caption"]}");
                    var communicator = new MtkDeviceCommunicator();
                    if (communicator.Connect())
                    {
                        Log?.Invoke("Device connected successfully.");
                        Log?.Invoke(communicator.GetDeviceInfo());
                        Log?.Invoke(communicator.Frp());
                        Log?.Invoke(communicator.FactoryReset());
                        communicator.Disconnect();
                        Log?.Invoke("Device disconnected.");
                    }
                    else
                    {
                        Log?.Invoke("Failed to connect to the device.");
                    }
                }
            };
            watcher.Start();
        }
    }
}
