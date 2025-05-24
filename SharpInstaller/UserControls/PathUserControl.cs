using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO; // For Path operations

namespace SharpInstaller.UserControls
{
    public partial class PathUserControl : UserControl
    {
        private Guna2TextBox pathTextBox;
        private Guna2Button browseButton;
        
        public string SelectedPath
        {
            get { return pathTextBox.Text; }
            set { pathTextBox.Text = value; }
        }

        public PathUserControl()
        {
            InitializeComponent();
            InitializeCustomComponents();
            SelectedPath = Path.Combine("C:", "Program Files (x86)", "DeviceSavior Tool"); // Default Path
        }

        private void InitializeCustomComponents()
        {
            // Initialize TextBox for path
            pathTextBox = new Guna2TextBox();
            pathTextBox.PlaceholderText = "Select Installation Path";
            pathTextBox.Location = new Point(10, 50); // Adjust layout
            pathTextBox.Size = new Size(this.Width - 120, 30); // Adjust layout
            pathTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;


            // Initialize Button for browsing
            browseButton = new Guna2Button();
            browseButton.Text = "Browse";
            browseButton.Location = new Point(pathTextBox.Right + 5, 50); // Adjust layout
            browseButton.Size = new Size(100, 30); // Adjust layout
            browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseButton.Click += BrowseButton_Click;

            Label instructionLabel = new Label();
            instructionLabel.Text = "Select the installation path for DeviceSavior Tool:";
            instructionLabel.Location = new Point(10, 20);
            instructionLabel.AutoSize = true;

            this.Controls.Add(instructionLabel);
            this.Controls.Add(pathTextBox);
            this.Controls.Add(browseButton);
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Installation Folder";
                // Set initial directory if pathTextBox.Text is a valid directory, otherwise let it default
                if (!string.IsNullOrWhiteSpace(pathTextBox.Text) && Directory.Exists(pathTextBox.Text))
                {
                    dialog.SelectedPath = pathTextBox.Text;
                }
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SelectedPath = dialog.SelectedPath;
                }
            }
        }
    }
}
