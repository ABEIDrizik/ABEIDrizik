using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MTKDeviceManager
{
    public partial class Form1 : Form
    {
        private readonly MtkDevice _mtkDevice;

        public Form1()
        {
            InitializeComponent();
            _mtkDevice = new MtkDevice();
            _mtkDevice.Log += OnLog;
        }

        private void OnLog(string message)
        {
            if (richTextBox1.InvokeRequired)
            {
                richTextBox1.Invoke(new Action(() => richTextBox1.AppendText(message + Environment.NewLine)));
            }
            else
            {
                richTextBox1.AppendText(message + Environment.NewLine);
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            await Task.Run(() => _mtkDevice.Start());
        }
    }
}
