using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;

namespace MTKDeviceManager
{
    public class DeviceManager
    {
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private RichTextBox logTextBox;

        public DeviceManager(RichTextBox logTextBox)
        {
            this.logTextBox = logTextBox;
            worker.WorkerReportsProgress = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
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
            // In a real application, we would wait for a device to be connected.
            // For this example, we'll just simulate a delay.
            Thread.Sleep(2000);
            worker.ReportProgress(0, "Device connected: Mediatek Android USB (COM20)");

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
