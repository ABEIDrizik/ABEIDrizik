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
            this.btnReadInfo = new System.Windows.Forms.Button();
            this.btnFactoryReset = new System.Windows.Forms.Button();
            this.btnRemoveFRP = new System.Windows.Forms.Button();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            //
            // btnReadInfo
            //
            this.btnReadInfo.Location = new System.Drawing.Point(12, 12);
            this.btnReadInfo.Name = "btnReadInfo";
            this.btnReadInfo.Size = new System.Drawing.Size(100, 23);
            this.btnReadInfo.TabIndex = 0;
            this.btnReadInfo.Text = "Read Info";
            this.btnReadInfo.UseVisualStyleBackColor = true;
            this.btnReadInfo.Click += new System.EventHandler(this.btnReadInfo_Click);
            //
            // btnFactoryReset
            //
            this.btnFactoryReset.Location = new System.Drawing.Point(12, 41);
            this.btnFactoryReset.Name = "btnFactoryReset";
            this.btnFactoryReset.Size = new System.Drawing.Size(100, 23);
            this.btnFactoryReset.TabIndex = 1;
            this.btnFactoryReset.Text = "Factory Reset";
            this.btnFactoryReset.UseVisualStyleBackColor = true;
            this.btnFactoryReset.Click += new System.EventHandler(this.btnFactoryReset_Click);
            //
            // btnRemoveFRP
            //
            this.btnRemoveFRP.Location = new System.Drawing.Point(12, 70);
            this.btnRemoveFRP.Name = "btnRemoveFRP";
            this.btnRemoveFRP.Size = new System.Drawing.Size(100, 23);
            this.btnRemoveFRP.TabIndex = 2;
            this.btnRemoveFRP.Text = "Remove FRP";
            this.btnRemoveFRP.UseVisualStyleBackColor = true;
            this.btnRemoveFRP.Click += new System.EventHandler(this.btnRemoveFRP_Click);
            //
            // richTextBox1
            //
            this.richTextBox1.Location = new System.Drawing.Point(118, 12);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(454, 81);
            this.richTextBox1.TabIndex = 3;
            this.richTextBox1.Text = "";
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 106);
            this.Controls.Add(this.richTextBox1);
            this.Controls.Add(this.btnRemoveFRP);
            this.Controls.Add(this.btnFactoryReset);
            this.Controls.Add(this.btnReadInfo);
            this.Name = "Form1";
            this.Text = "MTK Device Manager";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnReadInfo;
        private System.Windows.Forms.Button btnFactoryReset;
        private System.Windows.Forms.Button btnRemoveFRP;
        private System.Windows.Forms.RichTextBox richTextBox1;
    }
}
