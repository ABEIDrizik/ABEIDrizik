using Guna.UI2.WinForms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Net; // For WebClient (placeholder)
using System.IO; // For Path, Directory
using System.IO.Compression; // For ZipFile (placeholder)
using System.Threading.Tasks; // For Task

namespace SharpInstaller.UserControls
{
    public partial class ProgressbarUserControl : UserControl
    {
        public event EventHandler ExtractionCompleted;

        private Guna2ProgressBar guna2ProgressBar1;
        private Label labelSpeed;
        private Label labelDownloaded;
        private Label labelRemain;
        private Label labelExtract;
        private Label labelStatus; // General status like "Downloading..." or "Extracting..."

        private string _downloadUrl = "YOUR_SERVER_URL_HERE/update.zip"; // Placeholder
        private string _tempDownloadPath = Path.Combine("C:", "Program Files", "temp");
        private string _downloadFilePath;
        private string _extractionPath;

        public ProgressbarUserControl()
        {
            InitializeComponent();
            InitializeCustomComponents();
            _downloadFilePath = Path.Combine(_tempDownloadPath, "update.zip");
        }

        private void InitializeCustomComponents()
        {
            // Initialize Guna2ProgressBar
            guna2ProgressBar1 = new Guna2ProgressBar();
            guna2ProgressBar1.Location = new Point(10, 30);
            guna2ProgressBar1.Size = new Size(this.Width - 20, 20);
            guna2ProgressBar1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            guna2ProgressBar1.ProgressColor = Color.DodgerBlue;
            guna2ProgressBar1.ProgressColor2 = Color.Aqua;

            // Initialize Labels
            labelStatus = new Label { Text = "Status: Idle", Location = new Point(10, 60), AutoSize = true };
            labelSpeed = new Label { Text = "Speed: 0 KB/s", Location = new Point(10, 90), AutoSize = true };
            labelDownloaded = new Label { Text = "Downloaded: 0 MB / 0 MB", Location = new Point(10, 120), AutoSize = true };
            labelRemain = new Label { Text = "Remaining: N/A", Location = new Point(10, 150), AutoSize = true };
            labelExtract = new Label { Text = "Extracting: ...", Location = new Point(10, 180), AutoSize = true };
            labelExtract.Visible = false; // Initially hidden

            this.Controls.Add(guna2ProgressBar1);
            this.Controls.Add(labelStatus);
            this.Controls.Add(labelSpeed);
            this.Controls.Add(labelDownloaded);
            this.Controls.Add(labelRemain);
            this.Controls.Add(labelExtract);
        }

        public async Task StartProcess(string installPath, string downloadUrl = null)
        {
            _extractionPath = installPath;
            if (!string.IsNullOrEmpty(downloadUrl))
            {
                _downloadUrl = downloadUrl;
            }

            if (_downloadUrl == "YOUR_SERVER_URL_HERE/update.zip" || string.IsNullOrEmpty(_downloadUrl))
            {
                UpdateStatus("Error: Download URL not provided.");
                MessageBox.Show("Download URL is not configured. Cannot proceed.", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Optionally invoke ExtractionCompleted here if we want to allow "Next" for testing without download
                // ExtractionCompleted?.Invoke(this, EventArgs.Empty); 
                return;
            }

            try
            {
                // Prepare temp directory
                if (Directory.Exists(_tempDownloadPath))
                {
                    Directory.Delete(_tempDownloadPath, true);
                }
                Directory.CreateDirectory(_tempDownloadPath);

                // Download
                await DownloadFileAsync();

                // Extract
                await ExtractFileAsync();

                UpdateStatus("Installation process completed!");
                ExtractionCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DownloadFileAsync()
        {
            UpdateStatus("Downloading update...");
            labelExtract.Visible = false;
            using (WebClient webClient = new WebClient())
            {
                webClient.DownloadProgressChanged += (s, e) =>
                {
                    guna2ProgressBar1.Value = e.ProgressPercentage;
                    labelDownloaded.Text = $"Downloaded: {e.BytesReceived / (1024.0 * 1024.0):F2} MB / {e.TotalBytesToReceive / (1024.0 * 1024.0):F2} MB";
                    // Basic speed calculation (can be improved)
                    // This requires storing previous time and bytes to calculate speed. For simplicity, omitting here.
                    labelSpeed.Text = $"Speed: ... KB/s"; 
                    labelRemain.Text = $"Remaining: {(e.TotalBytesToReceive - e.BytesReceived) / (1024.0 * 1024.0):F2} MB";
                };

                webClient.DownloadFileCompleted += (s, e) =>
                {
                    if (e.Cancelled)
                    {
                        UpdateStatus("Download cancelled.");
                        throw new OperationCanceledException("Download was cancelled.");
                    }
                    if (e.Error != null)
                    {
                        UpdateStatus($"Download error: {e.Error.Message}");
                        throw e.Error;
                    }
                    UpdateStatus("Download completed.");
                    guna2ProgressBar1.Value = 100;
                };
                await webClient.DownloadFileTaskAsync(new Uri(_downloadUrl), _downloadFilePath);
            }
        }

        private async Task ExtractFileAsync()
        {
            UpdateStatus("Extracting files...");
            labelExtract.Visible = true;
            guna2ProgressBar1.Value = 0; // Reset progress bar for extraction phase
            
            // Placeholder for actual extraction logic.
            // ZipFile.ExtractToDirectory would block. A more complex implementation
            // would be needed for progress reporting during extraction.
            await Task.Run(() => {
                if (!File.Exists(_downloadFilePath))
                {
                    throw new FileNotFoundException("Downloaded file not found!", _downloadFilePath);
                }
                // Simulate extraction progress
                DirectoryInfo tempExtractDir = new DirectoryInfo(Path.Combine(_tempDownloadPath, "_extract"));
                if (tempExtractDir.Exists) tempExtractDir.Delete(true);
                tempExtractDir.Create();

                ZipFile.ExtractToDirectory(_downloadFilePath, tempExtractDir.FullName);

                // Simulate file-by-file "extraction" to final path for label update
                var filesToMove = tempExtractDir.GetFiles("*.*", SearchOption.AllDirectories);
                int totalFiles = filesToMove.Length;
                int filesExtracted = 0;

                foreach (var file in filesToMove)
                {
                    string relativePath = file.FullName.Substring(tempExtractDir.FullName.Length + 1);
                    string destFile = Path.Combine(_extractionPath, relativePath);
                    
                    // Ensure target directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                    this.Invoke((MethodInvoker)delegate {
                       labelExtract.Text = $"Extracting: {file.Name}";
                       if(totalFiles > 0) guna2ProgressBar1.Value = (int)((double)++filesExtracted / totalFiles * 100);
                    });
                    file.MoveTo(destFile); // Or File.Copy + File.Delete if preferred
                    System.Threading.Thread.Sleep(50); // Simulate work
                }
                if (tempExtractDir.Exists) tempExtractDir.Delete(true); // Clean up temporary extraction folder
            });
            labelExtract.Text = "Extraction complete.";
        }

        private void UpdateStatus(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { labelStatus.Text = $"Status: {message}"; });
            }
            else
            {
                labelStatus.Text = $"Status: {message}";
            }
        }
    }
}
