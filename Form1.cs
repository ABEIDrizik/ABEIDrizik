using System;
using System.Windows.Forms;
using System.Drawing; 
using System.Threading; 
using System.Threading.Tasks; 
using System.IO; // Added for Path.GetFileName

namespace MTKDeviceManager
{
    /// <summary>
    /// Main form for the Mediatek Device Manager application.
    /// Provides UI for device interaction and displays logs and progress.
    /// </summary>
    public partial class Form1 : Form
    {
        // UI Control Declarations (designer would typically manage these in Form1.Designer.cs)
        public Button BtnReadInfo;
        public Button BtnDAFile;
        public TextBox tbDAFile;
        public RichTextBox richTextBox1;
        public ProgressBar progressBar1;
        public OpenFileDialog openFileDialog1; 

        private AuthDeviceHandler _deviceHandler;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="Form1"/> class.
        /// </summary>
        public Form1()
        {
            InitializeComponent(); 
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.BtnReadInfo = new System.Windows.Forms.Button();
            this.BtnDAFile = new System.Windows.Forms.Button();
            this.tbDAFile = new System.Windows.Forms.TextBox();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.SuspendLayout();
            // 
            // BtnReadInfo
            // 
            this.BtnReadInfo.Location = new System.Drawing.Point(12, 12);
            this.BtnReadInfo.Name = "BtnReadInfo";
            this.BtnReadInfo.Size = new System.Drawing.Size(150, 23);
            this.BtnReadInfo.TabIndex = 0;
            this.BtnReadInfo.Text = "Read Device Info";
            this.BtnReadInfo.UseVisualStyleBackColor = true;
            this.BtnReadInfo.Click += new System.EventHandler(this.BtnReadInfo_Click); 
            // 
            // BtnDAFile
            // 
            this.BtnDAFile.Location = new System.Drawing.Point(12, 41);
            this.BtnDAFile.Name = "BtnDAFile";
            this.BtnDAFile.Size = new System.Drawing.Size(75, 23);
            this.BtnDAFile.TabIndex = 1;
            this.BtnDAFile.Text = "DA File...";
            this.BtnDAFile.UseVisualStyleBackColor = true;
            this.BtnDAFile.Click += new System.EventHandler(this.BtnDAFile_Click); 
            // 
            // tbDAFile
            // 
            this.tbDAFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbDAFile.Location = new System.Drawing.Point(93, 43);
            this.tbDAFile.Name = "tbDAFile";
            this.tbDAFile.ReadOnly = true; 
            this.tbDAFile.Size = new System.Drawing.Size(379, 20);
            this.tbDAFile.TabIndex = 2;
            // 
            // richTextBox1
            // 
            this.richTextBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.richTextBox1.Location = new System.Drawing.Point(12, 70);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.ReadOnly = true; 
            this.richTextBox1.Size = new System.Drawing.Size(460, 150);
            this.richTextBox1.TabIndex = 3;
            this.richTextBox1.Text = "";
            this.richTextBox1.TextChanged += (sender, e) => { 
                richTextBox1.SelectionStart = richTextBox1.Text.Length;
                richTextBox1.ScrollToCaret();
            };
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(12, 226);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(460, 23);
            this.progressBar1.TabIndex = 4;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 261);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.richTextBox1);
            this.Controls.Add(this.tbDAFile);
            this.Controls.Add(this.BtnDAFile);
            this.Controls.Add(this.BtnReadInfo);
            this.MinimumSize = new System.Drawing.Size(300, 200); 
            this.Name = "Form1";
            this.Text = "Mediatek Device Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing); 
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        /// <summary>
        /// Handles the Click event of the DAFile button. Opens a file dialog to select a Download Agent file.
        /// </summary>
        private void BtnDAFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "DA Files (*.bin)|*.bin|All files (*.*)|*.*";
            openFileDialog1.Title = "Select Download Agent File";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                tbDAFile.Text = openFileDialog1.FileName;
                richTextBox1.AppendText($"ℹ️ DA File selected: {Path.GetFileName(tbDAFile.Text)}\n");
            }
        }

        /// <summary>
        /// Handles the Click event of the ReadInfo button. Initiates the device detection and information retrieval process.
        /// This method is asynchronous to keep the UI responsive.
        /// </summary>
        private async void BtnReadInfo_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
            progressBar1.Value = 0;
            BtnReadInfo.Enabled = false;
            BtnDAFile.Enabled = false;

            // Safely dispose of the previous CancellationTokenSource if it exists
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var logProgress = new Progress<string>(msg => {
                if (richTextBox1.InvokeRequired)
                {
                    richTextBox1.Invoke((MethodInvoker)delegate { richTextBox1.AppendText(msg); });
                }
                else
                {
                    richTextBox1.AppendText(msg);
                }
            });

            var barProgress = new Progress<int>(val => {
                if (progressBar1.InvokeRequired)
                {
                    progressBar1.Invoke((MethodInvoker)delegate { progressBar1.Value = Math.Min(val, progressBar1.Maximum); });
                }
                else
                {
                    progressBar1.Value = Math.Min(val, progressBar1.Maximum);
                }
            });

            _deviceHandler = new AuthDeviceHandler(logProgress, barProgress);
            
            try
            {
                logProgress.Report("🚀 Initializing operation...\n");
                if (!string.IsNullOrWhiteSpace(tbDAFile.Text))
                {
                    try
                    {
                         _deviceHandler.SetDAFile(tbDAFile.Text);
                    }
                    catch (ArgumentException ex)
                    {
                        logProgress.Report($"❌ Error setting DA file: {ex.Message}\n"); 
                    }
                }
                else
                {
                    logProgress.Report("ℹ️ No DA file selected. Proceeding without DA-specific operations.\n");
                }

                logProgress.Report("⏳ Starting device detection and processing...\n");
                bool success = await _deviceHandler.ProcessDeviceAsync(_cancellationTokenSource.Token);

                if (success)
                {
                    logProgress.Report("🎉 Operation completed successfully.\n");
                }
                else
                {
                    logProgress.Report("❌ Operation failed or was cancelled by user/timeout. Check logs for details.\n");
                }
            }
            catch (OperationCanceledException) 
            {
                logProgress.Report("🚫 Operation was cancelled by the user.\n");
            }
            catch (Exception ex) 
            {
                logProgress.Report($"❌ An critical unexpected error occurred in UI: {ex.Message}\n" +
                                   $"Technical Details: {ex.GetType().Name}\n" +
                                   $"Stack Trace (for debugging):\n{ex.StackTrace}\n");
            }
            finally
            {
                BtnReadInfo.Enabled = true;
                BtnDAFile.Enabled = true;
                // _cancellationTokenSource is disposed at the start of the next click or in FormClosing
                progressBar1.Value = 0; 
                logProgress.Report("--------------------------------------------------\n");
            }
        }

        /// <summary>
        /// Handles the FormClosing event. Cancels any ongoing operations.
        /// </summary>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!BtnReadInfo.Enabled) // Operation is in progress
            {
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    richTextBox1.AppendText("ℹ️ Application closing: Requesting cancellation of ongoing operation...\n");
                    _cancellationTokenSource.Cancel();
                    // Consider if a brief delay or check is needed here to allow cancellation to propagate.
                    // For simplicity, we'll let the operation attempt to cancel.
                }
            }
            _cancellationTokenSource?.Dispose(); // Dispose CTS when form closes
        }
    }
}
