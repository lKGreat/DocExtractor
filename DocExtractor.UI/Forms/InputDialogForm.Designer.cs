using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class InputDialogForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this._promptLabel = new Label();
            this._inputBox = new TextBox();
            this._okBtn = new AntdUI.Button();
            this._cancelBtn = new AntdUI.Button();
            this.SuspendLayout();

            this._promptLabel.Left = 12;
            this._promptLabel.Top = 16;
            this._promptLabel.Width = 360;
            this._promptLabel.Height = 22;
            this._promptLabel.Text = "请输入：";

            this._inputBox.Left = 12;
            this._inputBox.Top = 44;
            this._inputBox.Width = 360;

            this._okBtn.Text = "确定";
            this._okBtn.Type = AntdUI.TTypeMini.Primary;
            this._okBtn.Size = new Size(80, 30);
            this._okBtn.Left = 210;
            this._okBtn.Top = 82;
            this._okBtn.Click += OnConfirm;

            this._cancelBtn.Text = "取消";
            this._cancelBtn.Size = new Size(80, 30);
            this._cancelBtn.Left = 298;
            this._cancelBtn.Top = 82;
            this._cancelBtn.Click += OnCancel;

            this.Controls.Add(this._promptLabel);
            this.Controls.Add(this._inputBox);
            this.Controls.Add(this._okBtn);
            this.Controls.Add(this._cancelBtn);

            this.Text = "输入";
            this.Width = 400;
            this.Height = 150;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("微软雅黑", 9F);
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Name = "InputDialogForm";

            this.ResumeLayout(false);
        }

        private Label _promptLabel;
        private TextBox _inputBox;
        private AntdUI.Button _okBtn;
        private AntdUI.Button _cancelBtn;
    }
}
