using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms; // For MessageBox
using Guna.UI2.WinForms; 
using SharpInstaller.UserControls; 
using System.IO; // For Directory operations

namespace SharpInstaller
{
    public partial class Form1 : Form
    {
        // GunaUI Controls
        private Guna.UI2.WinForms.Guna2Button BtnNetx;
        private Guna.UI2.WinForms.Guna2Button BtnBack;
        private Guna.UI2.WinForms.Guna2Panel Guna2Panel1;

        // UserControls
        private SharpInstaller.UserControls.WelcomeUserControl welcomeControl;
        private SharpInstaller.UserControls.TermsUserControl termsControl;
        private SharpInstaller.UserControls.PathUserControl pathControl; 
        private SharpInstaller.UserControls.ProgressbarUserControl progressControl; 
        private SharpInstaller.UserControls.FinishUserControl finishControl; 

        // Store install path for FinishUserControl
        private string currentInstallPath = string.Empty;

        public Form1()
        {
            InitializeComponent();

            // Instantiate GunaUI Controls
            this.Guna2Panel1 = new Guna.UI2.WinForms.Guna2Panel();
            this.Guna2Panel1.Name = "Guna2Panel1";
            this.Guna2Panel1.Dock = System.Windows.Forms.DockStyle.Fill; 

            this.BtnNetx = new Guna.UI2.WinForms.Guna2Button();
            this.BtnNetx.Name = "BtnNetx";
            this.BtnNetx.Text = "Next"; // Initial text
            this.BtnNetx.Location = new System.Drawing.Point(700, 400); 
            this.BtnNetx.Size = new System.Drawing.Size(75, 23);
            this.BtnNetx.Click += new System.EventHandler(this.BtnNetx_Click); 

            this.BtnBack = new Guna.UI2.WinForms.Guna2Button();
            this.BtnBack.Name = "BtnBack";
            this.BtnBack.Text = "Back";
            this.BtnBack.Location = new System.Drawing.Point(12, 400);
            this.BtnBack.Size = new System.Drawing.Size(75, 23);
            this.BtnBack.Click += new System.EventHandler(this.BtnBack_Click); 

            this.Controls.Add(this.Guna2Panel1);
            this.Controls.Add(this.BtnNetx);
            this.Controls.Add(this.BtnBack);
            
            this.BtnNetx.BringToFront();
            this.BtnBack.BringToFront();

            // Initialize UserControls
            this.welcomeControl = new SharpInstaller.UserControls.WelcomeUserControl();
            // Other UserControls will be initialized on demand.

            // Load the initial UserControl
            LoadUserControl(this.welcomeControl);

            // Initial Button States
            this.BtnNetx.Enabled = true;
            this.BtnBack.Enabled = false; 
        }

        public void LoadUserControl(System.Windows.Forms.UserControl userControl)
        {
            if (this.Guna2Panel1 != null)
            {
                this.Guna2Panel1.Controls.Clear();
                userControl.Dock = System.Windows.Forms.DockStyle.Fill;
                this.Guna2Panel1.Controls.Add(userControl);
            }
        }
        
        private async void BtnNetx_Click(object sender, EventArgs e) 
        {
            // Handle "Finish" button click to exit application
            if (this.BtnNetx.Text == "Finish")
            {
                Application.Exit();
                return;
            }

            if (Guna2Panel1.Controls.Contains(welcomeControl))
            {
                if (termsControl == null)
                {
                    termsControl = new SharpInstaller.UserControls.TermsUserControl();
                    termsControl.AgreementChanged += TermsControl_AgreementChanged;
                }
                LoadUserControl(termsControl);
                BtnNetx.Enabled = termsControl.IsAgreed; 
                BtnBack.Enabled = true; 
                this.BtnNetx.Text = "Next"; 
            }
            else if (Guna2Panel1.Controls.Contains(termsControl) && termsControl.IsAgreed)
            {
                if (pathControl == null) 
                {
                    pathControl = new SharpInstaller.UserControls.PathUserControl();
                }
                LoadUserControl(pathControl);
                BtnNetx.Enabled = true; 
                BtnBack.Enabled = true;
                this.BtnNetx.Text = "Next"; 
            }
            else if (Guna2Panel1.Controls.Contains(pathControl))
            {
                currentInstallPath = pathControl.SelectedPath; // Store path
                try
                {
                    if (string.IsNullOrWhiteSpace(currentInstallPath))
                    {
                        MessageBox.Show("Please select a valid installation path.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!Directory.Exists(currentInstallPath))
                    {
                        Directory.CreateDirectory(currentInstallPath);
                    }
                    
                    if (progressControl == null) 
                    {
                        progressControl = new SharpInstaller.UserControls.ProgressbarUserControl();
                        progressControl.ExtractionCompleted -= ProgressControl_ExtractionCompleted; 
                        progressControl.ExtractionCompleted += ProgressControl_ExtractionCompleted; 
                    }
                    LoadUserControl(progressControl);
                    BtnNetx.Enabled = false; 
                    BtnBack.Enabled = true;  
                    this.BtnNetx.Text = "Next"; 
                    
                    await progressControl.StartProcess(currentInstallPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (Guna2Panel1.Controls.Contains(progressControl)) 
            {
                // This block is executed when BtnNetx is clicked AFTER ExtractionCompleted (which enables BtnNetx)
                if (finishControl == null) 
                {
                    finishControl = new SharpInstaller.UserControls.FinishUserControl();
                }
                LoadUserControl(finishControl);
                // Pass the stored install path. Optional: pass app name if known/configurable.
                finishControl.SetInstallPath(currentInstallPath /*, "YourApp.exe" */); 
                
                BtnNetx.Text = "Finish";
                BtnNetx.Enabled = true; // Keep "Finish" button enabled to exit
                BtnBack.Enabled = false; // Disable "Back" on Finish screen
            }
        }

        private void BtnBack_Click(object sender, EventArgs e)
        {
            if (Guna2Panel1.Controls.Contains(termsControl))
            {
                LoadUserControl(welcomeControl);
                BtnNetx.Enabled = true; 
                BtnNetx.Text = "Next";
                BtnBack.Enabled = false; 
            }
            else if (Guna2Panel1.Controls.Contains(pathControl))
            {
                LoadUserControl(termsControl);
                BtnNetx.Enabled = termsControl.IsAgreed; 
                BtnNetx.Text = "Next";
                BtnBack.Enabled = true; 
            }
            else if (Guna2Panel1.Controls.Contains(progressControl))
            {
                LoadUserControl(pathControl);
                BtnNetx.Enabled = true; 
                BtnNetx.Text = "Next";
                BtnBack.Enabled = true; 
            }
            // No "Back" logic from FinishUserControl as BtnBack will be disabled.
        }

        private void TermsControl_AgreementChanged(object sender, EventArgs e)
        {
            if (Guna2Panel1.Controls.Contains(termsControl)) 
            {
                BtnNetx.Enabled = termsControl.IsAgreed;
            }
        }
        
        private void ProgressControl_ExtractionCompleted(object sender, EventArgs e)
        {
            if (this.InvokeRequired) 
            {
                this.Invoke((MethodInvoker)delegate {
                    HandleExtractionCompletion();
                });
            }
            else
            {
                HandleExtractionCompletion();
            }
        }

        private void HandleExtractionCompletion()
        {
            if (Guna2Panel1.Controls.Contains(progressControl)) 
            {
                BtnNetx.Enabled = true; 
                // BtnBack.Enabled = true; // Keep Back enabled as per current logic on Progress screen
            }
        }
        
        private void buttonExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
