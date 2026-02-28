using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class ExportFieldSelectionForm
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
            this._hintLabel = new Label();
            this._fieldList = new CheckedListBox();
            this._selectAllBtn = new AntdUI.Button();
            this._clearAllBtn = new AntdUI.Button();
            this._okBtn = new AntdUI.Button();
            this._cancelBtn = new AntdUI.Button();
            this.SuspendLayout();

            this._hintLabel.Text = "勾选需要导出的字段（默认全选）";
            this._hintLabel.Left = 12;
            this._hintLabel.Top = 12;
            this._hintLabel.Width = 380;
            this._hintLabel.Height = 20;

            this._fieldList.Left = 12;
            this._fieldList.Top = 36;
            this._fieldList.Width = 380;
            this._fieldList.Height = 370;
            this._fieldList.CheckOnClick = true;

            this._selectAllBtn.Text = "全选";
            this._selectAllBtn.Size = new Size(80, 30);
            this._selectAllBtn.Left = 12;
            this._selectAllBtn.Top = 414;
            this._selectAllBtn.Click += OnSelectAll;

            this._clearAllBtn.Text = "全不选";
            this._clearAllBtn.Size = new Size(80, 30);
            this._clearAllBtn.Left = 100;
            this._clearAllBtn.Top = 414;
            this._clearAllBtn.Click += OnClearAll;

            this._okBtn.Text = "确定";
            this._okBtn.Type = AntdUI.TTypeMini.Primary;
            this._okBtn.Size = new Size(80, 30);
            this._okBtn.Left = 228;
            this._okBtn.Top = 452;
            this._okBtn.Click += OnConfirm;

            this._cancelBtn.Text = "取消";
            this._cancelBtn.Size = new Size(80, 30);
            this._cancelBtn.Left = 316;
            this._cancelBtn.Top = 452;
            this._cancelBtn.Click += OnCancel;

            this.Controls.Add(this._hintLabel);
            this.Controls.Add(this._fieldList);
            this.Controls.Add(this._selectAllBtn);
            this.Controls.Add(this._clearAllBtn);
            this.Controls.Add(this._okBtn);
            this.Controls.Add(this._cancelBtn);

            this.Text = "选择导出字段";
            this.Width = 420;
            this.Height = 520;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("微软雅黑", 9F);
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Name = "ExportFieldSelectionForm";

            this.ResumeLayout(false);
        }

        private Label _hintLabel;
        private CheckedListBox _fieldList;
        private AntdUI.Button _selectAllBtn;
        private AntdUI.Button _clearAllBtn;
        private AntdUI.Button _okBtn;
        private AntdUI.Button _cancelBtn;
    }
}
