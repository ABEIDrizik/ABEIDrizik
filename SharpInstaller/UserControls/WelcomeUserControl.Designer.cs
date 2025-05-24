namespace SharpInstaller.UserControls
{
    partial class WelcomeUserControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "WelcomeUserControl";
            this.Size = new System.Drawing.Size(400, 300); // Default size, will be filled by panel
        }
    }
}
