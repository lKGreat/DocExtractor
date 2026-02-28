using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class FieldEditorForm
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
            components = new System.ComponentModel.Container();
            SuspendLayout();

            Text = "字段编辑器";
            Size = new Size(620, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("微软雅黑", 9F);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(12)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            panel.Controls.Add(new Label { Text = "字段名（英文）", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            _fieldNameInput = new TextBox { Dock = DockStyle.Fill };
            panel.Controls.Add(_fieldNameInput, 1, 0);

            panel.Controls.Add(new Label { Text = "显示名（中文）", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            _displayNameInput = new TextBox { Dock = DockStyle.Fill };
            panel.Controls.Add(_displayNameInput, 1, 1);

            panel.Controls.Add(new Label { Text = "数据类型", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            _dataTypeCombo = new ComboBox { Dock = DockStyle.Left, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            panel.Controls.Add(_dataTypeCombo, 1, 2);

            panel.Controls.Add(new Label { Text = "必填", TextAlign = ContentAlignment.MiddleRight }, 0, 3);
            _isRequiredCheck = new CheckBox { Text = "必填字段", AutoSize = true };
            panel.Controls.Add(_isRequiredCheck, 1, 3);

            panel.Controls.Add(new Label { Text = "默认值", TextAlign = ContentAlignment.MiddleRight }, 0, 4);
            _defaultValueInput = new TextBox { Dock = DockStyle.Fill };
            panel.Controls.Add(_defaultValueInput, 1, 4);

            panel.Controls.Add(new Label { Text = "列名变体（每行一个）", TextAlign = ContentAlignment.MiddleRight }, 0, 5);
            _variantsInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Height = 220
            };
            panel.Controls.Add(_variantsInput, 1, 5);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            var confirmBtn = new AntdUI.Button
            {
                Text = "确认",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(90, 32)
            };
            var cancelBtn = new AntdUI.Button
            {
                Text = "取消",
                Size = new Size(90, 32)
            };
            confirmBtn.Click += OnConfirm;
            cancelBtn.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            buttonPanel.Controls.Add(confirmBtn);
            buttonPanel.Controls.Add(cancelBtn);
            panel.Controls.Add(buttonPanel, 1, 6);

            Controls.Add(panel);
            ResumeLayout(false);
        }

        private TextBox _fieldNameInput;
        private TextBox _displayNameInput;
        private ComboBox _dataTypeCombo;
        private CheckBox _isRequiredCheck;
        private TextBox _defaultValueInput;
        private TextBox _variantsInput;
    }
}
