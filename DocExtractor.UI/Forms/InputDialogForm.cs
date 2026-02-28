using System;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    /// <summary>Single-line text input dialog. Replaces inline new Form() usage.</summary>
    public partial class InputDialogForm : Form
    {
        public string Result { get; private set; } = string.Empty;

        public InputDialogForm(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            Text = title;
            _promptLabel.Text = prompt;
            _inputBox.Text = defaultValue;
        }

        private void OnConfirm(object sender, EventArgs e)
        {
            Result = _inputBox.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnCancel(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
