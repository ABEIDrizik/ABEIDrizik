using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
// For shortcut creation, we might need to add a reference to IWshRuntimeLibrary (COM).
// This is tricky in a pure .NET environment without directly invoking shell commands or using a library.
// For now, we'll use a placeholder method and note the dependency.

namespace SharpInstaller.UserControls
{
    public partial class FinishUserControl : UserControl
    {
        private Label labelMessage;
        private string _applicationName = "MyApp.exe"; // Placeholder, user needs to provide this
        private string _installPath;

        public FinishUserControl()
        {
            InitializeComponent();
            InitializeCustomComponents();
        }

        private void InitializeCustomComponents()
        {
            labelMessage = new Label();
            labelMessage.Text = "Installation Successful!";
            labelMessage.Font = new Font("Arial", 14, FontStyle.Bold);
            labelMessage.AutoSize = true;
            labelMessage.Location = new Point(50, 50); // Adjust as needed

            Label infoLabel = new Label();
            infoLabel.Text = "DeviceSavior Tool has been successfully installed.\n" +
                             "A shortcut has been created on your desktop (if possible).";
            infoLabel.Font = new Font("Arial", 10);
            infoLabel.AutoSize = true;
            infoLabel.Location = new Point(50, 100); // Adjust as needed
            
            this.Controls.Add(labelMessage);
            this.Controls.Add(infoLabel);
        }

        public void SetInstallPath(string installPath, string appNameInZip = null)
        {
            _installPath = installPath;
            if (!string.IsNullOrEmpty(appNameInZip))
            {
                _applicationName = appNameInZip;
            }
            CreateDesktopShortcut();
        }

        private void CreateDesktopShortcut()
        {
            try
            {
                // --- Placeholder for shortcut creation ---
                // This is a complex task that often requires COM interop (IWshRuntimeLibrary)
                // or calling PowerShell. For now, we simulate and inform the user.
                
                string exePathInInstallDir = Path.Combine(_installPath, _applicationName);
                if (!File.Exists(exePathInInstallDir))
                {
                    Console.WriteLine($"Shortcut Error: Target exe not found at {exePathInInstallDir}");
                    // Update a label or show a message if UI is available for this error
                    return;
                }

                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutLocation = Path.Combine(desktopPath, Path.GetFileNameWithoutExtension(_applicationName) + ".lnk");
                
                // Simplistic placeholder: just create a dummy file to indicate attempt
                // File.WriteAllText(shortcutLocation + ".txt", $"This is a placeholder for a shortcut to {exePathInInstallDir}");
                
                // Actual shortcut creation would look something like this (requires COM reference):
                /*
                WshShell shell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutLocation);
                shortcut.Description = "Shortcut for DeviceSavior Tool";
                shortcut.TargetPath = exePathInInstallDir;
                // shortcut.WorkingDirectory = _installPath; // Optional
                // shortcut.IconLocation = exePathInInstallDir + ",0"; // Optional
                shortcut.Save();
                */

                Console.WriteLine($"Placeholder: Shortcut creation for '{exePathInInstallDir}' to '{shortcutLocation}' would be attempted here.");
                // If you have a status label on FinishUserControl, update it here.
                // e.g., this.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "statusLabel").Text = "Desktop shortcut created.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating shortcut (placeholder): {ex.Message}");
                // Update a label or show a message
            }
        }
    }
}

// Required for actual shortcut:
// Add COM Reference: Windows Script Host Object Model (IWshRuntimeLibrary)
// using IWshRuntimeLibrary;
