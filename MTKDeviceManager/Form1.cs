using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace MTKDeviceManager
{
    public partial class Form1 : Form
    {
        private DeviceManager deviceManager;

        public Form1()
        {
            InitializeComponent();
            deviceManager = new DeviceManager(richTextBox1);
        }

        private void btnReadInfo_Click(object sender, EventArgs e)
        {
            deviceManager.StartOperation(DeviceOperation.ReadInfo, OnOperationCompleted);
        }

        private void btnFactoryReset_Click(object sender, EventArgs e)
        {
            deviceManager.StartOperation(DeviceOperation.FactoryReset, OnOperationCompleted);
        }

        private void btnRemoveFRP_Click(object sender, EventArgs e)
        {
            deviceManager.StartOperation(DeviceOperation.RemoveFRP, OnOperationCompleted);
        }

        private void OnOperationCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show("An error occurred: " + e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                if (e.Result is bool boolResult)
                {
                    string message = boolResult ? "Operation completed successfully." : "Operation failed.";
                    MessageBox.Show(message, "Result", MessageBoxButtons.OK, boolResult ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show("Operation completed: " + e.Result.ToString(), "Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
