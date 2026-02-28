using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class ColumnMappingReviewForm
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

            Text = "低置信度列映射确认";
            Size = new Size(820, 460);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("微软雅黑", 9F);

            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = "请确认以下低置信度列的正确字段。确认后将自动写入训练数据（已验证）。",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RawColumn",
                HeaderText = "原始列名",
                ReadOnly = true,
                FillWeight = 30
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SuggestedField",
                HeaderText = "系统建议",
                ReadOnly = true,
                FillWeight = 20
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Confidence",
                HeaderText = "置信度",
                ReadOnly = true,
                FillWeight = 10
            });
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "CorrectField",
                HeaderText = "确认字段",
                FillWeight = 40
            });

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8, 8, 8, 8)
            };
            var confirmBtn = new AntdUI.Button
            {
                Text = "确认并保存",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(120, 32)
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

            Controls.Add(_grid);
            Controls.Add(buttonPanel);
            Controls.Add(hint);
            ResumeLayout(false);
        }

        private DataGridView _grid;
    }
}
