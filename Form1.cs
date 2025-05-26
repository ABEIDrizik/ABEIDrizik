using System;
using System.Windows.Forms;
using System.Drawing; // Added for Point, Size, etc.
using System.Threading; // Required for CancellationTokenSource
using System.Threading.Tasks; // Required for Task

namespace MTKDeviceManager
{
    public partial class Form1 : Form
    {
        // UI Control Declarations
        public Button BtnReadInfo;
        public Button BtnDAFile;
        public TextBox tbDAFile;
        public RichTextBox richTextBox1;
        public ProgressBar progressBar1;
        public OpenFileDialog openFileDialog1; // For selecting DA file

        private AuthDeviceHandler _deviceHandler;
        private CancellationTokenSource _cancellationTokenSource;

        public Form1()
        {
            InitializeComponent();

            // Wire up event handlers
            this.BtnDAFile.Click += new System.EventHandler(this.BtnDAFile_Click);
            this.BtnReadInfo.Click += new System.EventHandler(this.BtnReadInfo_Click);
        }

        // Basic initialization of components (can be expanded by a designer)
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
            this.BtnReadInfo.Click += new System.EventHandler(this.BtnReadInfo_Click_1);
            // 
            // BtnDAFile
            // 
            this.BtnDAFile.Location = new System.Drawing.Point(12, 41);
            this.BtnDAFile.Name = "BtnDAFile";
            this.BtnDAFile.Size = new System.Drawing.Size(75, 23);
            this.BtnDAFile.TabIndex = 1;
            this.BtnDAFile.Text = "DA File...";
            this.BtnDAFile.UseVisualStyleBackColor = true;
            // 
            // tbDAFile
            // 
            this.tbDAFile.Location = new System.Drawing.Point(93, 43);
            this.tbDAFile.Name = "tbDAFile";
            this.tbDAFile.ReadOnly = true;
            this.tbDAFile.Size = new System.Drawing.Size(379, 20);
            this.tbDAFile.TabIndex = 2;
            // 
            // richTextBox1
            // 
            this.richTextBox1.Location = new System.Drawing.Point(12, 70);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(460, 150);
            this.richTextBox1.TabIndex = 3;
            this.richTextBox1.Text = "";
            // 
            // progressBar1
            // 
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
            this.Name = "Form1";
            this.Text = "Mediatek Device Manager";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void BtnDAFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "DA Files (*.bin)|*.bin|All files (*.*)|*.*";
            openFileDialog1.Title = "Select Download Agent File";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                tbDAFile.Text = openFileDialog1.FileName;
                richTextBox1.AppendText($"DA File selected: {tbDAFile.Text}\n");
            }
        }

        private async void BtnReadInfo_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
            progressBar1.Value = 0;
            BtnReadInfo.Enabled = false;
            BtnDAFile.Enabled = false;

            // Prepare progress reporters
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
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Set DA file if one is selected
                if (!string.IsNullOrWhiteSpace(tbDAFile.Text))
                {
                    try
                    {
                         _deviceHandler.SetDAFile(tbDAFile.Text);
                    }
                    catch (ArgumentException ex)
                    {
                        richTextBox1.AppendText($"Error setting DA file: {ex.Message}\n");
                        // Optionally, clear tbDAFile.Text or alert user more directly
                        // For now, processing will continue if DA is not essential for all steps
                    }
                }
                else
                {
                    richTextBox1.AppendText("No DA file selected. Proceeding without it for operations that might not require it.\n");
                }

                richTextBox1.AppendText("Starting device detection and processing...\n");
                bool success = await _deviceHandler.ProcessDeviceAsync(_cancellationTokenSource.Token);

                if (success)
                {
                    richTextBox1.AppendText("Operation completed successfully.\n");
                }
                else
                {
                    richTextBox1.AppendText("Operation failed or was cancelled.\n");
                }
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText($"An unexpected error occurred: {ex.Message}\nStack Trace: {ex.StackTrace}\n");
            }
            finally
            {
                BtnReadInfo.Enabled = true;
                BtnDAFile.Enabled = true;
                _cancellationTokenSource.Dispose();
            }
        }

        private void BtnReadInfo_Click_1(object sender, EventArgs e)
        {

        }
    }
}
