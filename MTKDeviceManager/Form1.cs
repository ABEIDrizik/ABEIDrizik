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
                MessageBox.Show("An error occurred: " + e.Error.Message);
            }
            else
            {
                MessageBox.Show("Operation completed: " + e.Result.ToString());
            }
        }
    }
}
