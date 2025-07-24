using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;
using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace MTKDeviceManager
{
    public class DeviceManager
    {
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private RichTextBox logTextBox;
        public static UsbDeviceNotifier UsbDeviceNotifier = new UsbDeviceNotifier();

        public DeviceManager(RichTextBox logTextBox)
        {
            this.logTextBox = logTextBox;
            worker.WorkerReportsProgress = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            UsbDeviceNotifier.OnDeviceNotify += OnDeviceNotifyEvent;
        }

        private void OnDeviceNotifyEvent(object sender, DeviceNotifyEventArgs e)
        {
            // A MediaTek device in Preloader/BROM mode has VID 0x0E8D and PID 0x0003.
            if (e.Device.IdVendor == 0x0E8D && e.Device.IdProduct == 0x0003)
            {
                logTextBox.Invoke((MethodInvoker)delegate {
                    logTextBox.AppendText($"Device event: {e.EventType}, Device: {e.Device.FullName}" + Environment.NewLine);
                });
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            logTextBox.AppendText(e.UserState.ToString() + Environment.NewLine);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var operation = (DeviceOperation)e.Argument;
            var device = new MTKDevice();

            worker.ReportProgress(0, "Waiting for device...");

            // The actual device detection is now handled by the OnDeviceNotifyEvent.
            // We can use an event to signal when a device is connected.
            // For this example, we will assume the device is connected and proceed.

            UsbRegistry usbRegistry = UsbDevice.AllDevices.FirstOrDefault(d => d.Vid == 0x0E8D && d.Pid == 0x0003);
            if (usbRegistry == null)
            {
                e.Result = "No MTK device in BROM mode found.";
                return;
            }

            worker.ReportProgress(0, $"Device found: {usbRegistry.FullName}");

            if (device.ConnectBrom())
            {
                worker.ReportProgress(0, "BROM connection successful.");
                if (device.SendDA("C:\\DA.bin"))
                {
                    worker.ReportProgress(0, "DA sent successfully.");
                    switch (operation)
                    {
                        case DeviceOperation.ReadInfo:
                            e.Result = device.GetDeviceInfo();
                            break;
                        case DeviceOperation.FactoryReset:
                            e.Result = device.FactoryReset();
                            break;
                        case DeviceOperation.RemoveFRP:
                            e.Result = device.RemoveFRP();
                            break;
                    }
                }
                else
                {
                    e.Result = "Failed to send DA.";
                }
            }
            else
            {
                e.Result = "Failed to connect in BROM mode.";
            }
        }

        public void StartOperation(DeviceOperation operation, RunWorkerCompletedEventHandler onCompleted)
        {
            if (!worker.IsBusy)
            {
                worker.RunWorkerCompleted += onCompleted;
                worker.RunWorkerAsync(operation);
            }
        }
    }

    public enum DeviceOperation
    {
        ReadInfo,
        FactoryReset,
        RemoveFRP
    }
}
