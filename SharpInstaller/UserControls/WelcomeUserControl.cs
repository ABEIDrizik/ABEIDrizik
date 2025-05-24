using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharpInstaller.UserControls
{
    public partial class WelcomeUserControl : UserControl
    {
        public WelcomeUserControl()
        {
            InitializeComponent();

            // Programmatically add a welcome label
            Label welcomeLabel = new Label();
            welcomeLabel.Text = "Welcome to the DeviceSavior Tool Installer!";
            welcomeLabel.Font = new Font("Arial", 12, FontStyle.Bold);
            welcomeLabel.AutoSize = true;
            welcomeLabel.Location = new Point(50, 50); // Adjust as needed
            this.Controls.Add(welcomeLabel);
        }
    }
}
