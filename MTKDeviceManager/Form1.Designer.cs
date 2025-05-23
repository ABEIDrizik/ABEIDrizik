namespace MTKDeviceManager
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.richTextBoxLog = new System.Windows.Forms.RichTextBox();
            this.btnStartOperations = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // richTextBoxLog
            // 
            this.richTextBoxLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBoxLog.Location = new System.Drawing.Point(0, 23); // Assuming button height of 23
            this.richTextBoxLog.Name = "richTextBoxLog";
            this.richTextBoxLog.ReadOnly = true;
            this.richTextBoxLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.richTextBoxLog.Size = new System.Drawing.Size(800, 427);
            this.richTextBoxLog.TabIndex = 1;
            this.richTextBoxLog.Text = "";
            // 
            // btnStartOperations
            // 
            this.btnStartOperations.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnStartOperations.Location = new System.Drawing.Point(0, 0);
            this.btnStartOperations.Name = "btnStartOperations";
            this.btnStartOperations.Size = new System.Drawing.Size(800, 23);
            this.btnStartOperations.TabIndex = 0;
            this.btnStartOperations.Text = "Start MTK Operations";
            this.btnStartOperations.UseVisualStyleBackColor = true;
            this.btnStartOperations.Click += new System.EventHandler(this.btnStartOperations_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.richTextBoxLog);
            this.Controls.Add(this.btnStartOperations);
            this.Name = "Form1";
            this.Text = "MTK Device Manager";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox richTextBoxLog;
        private System.Windows.Forms.Button btnStartOperations;
    }
}
