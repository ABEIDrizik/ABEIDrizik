using Guna.UI2.WinForms; // Added for Guna2CheckBox
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SharpInstaller.UserControls
{
    public partial class TermsUserControl : UserControl
    {
        public event EventHandler AgreementChanged; // Event to notify main form
        private RichTextBox termsRichTextBox;
        private Guna.UI2.WinForms.Guna2CheckBox agreeCheckBox;

        public bool IsAgreed => agreeCheckBox.Checked;

        public TermsUserControl()
        {
            InitializeComponent();
            InitializeCustomComponents();
        }

        private void InitializeCustomComponents()
        {
            // Initialize RichTextBox for terms
            termsRichTextBox = new RichTextBox();
            termsRichTextBox.Dock = DockStyle.Fill; // Fill most of the space
            termsRichTextBox.ReadOnly = true;
            termsRichTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            termsRichTextBox.Text = "Please read and accept the terms and conditions...\n\n1. Term A\n2. Term B\n3. Term C..."; // Placeholder text
            
            // Initialize Guna2CheckBox for agreement
            agreeCheckBox = new Guna.UI2.WinForms.Guna2CheckBox();
            agreeCheckBox.Text = "I agree to the terms and conditions";
            agreeCheckBox.Dock = DockStyle.Bottom; // Position at the bottom
            agreeCheckBox.CheckedChanged += AgreeCheckBox_CheckedChanged;

            // Add controls to the UserControl
            this.Controls.Add(termsRichTextBox);
            this.Controls.Add(agreeCheckBox);
        }

        private void AgreeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            AgreementChanged?.Invoke(this, EventArgs.Empty); // Raise event
        }
    }
}
