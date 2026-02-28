using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class ModelManagerForm
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

            Text = "模型版本管理";
            Size = new Size(760, 500);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("微软雅黑", 9F);

            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8, 8, 8, 4)
            };
            topPanel.Controls.Add(new Label { Text = "模型：", Width = 40, Height = 30, TextAlign = ContentAlignment.MiddleRight });
            _modelCombo = new ComboBox
            {
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _refreshBtn = new AntdUI.Button { Text = "刷新", Size = new Size(80, 32) };
            _rollbackBtn = new AntdUI.Button
            {
                Text = "回滚到选中版本",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(150, 32)
            };
            topPanel.Controls.Add(_modelCombo);
            topPanel.Controls.Add(_refreshBtn);
            topPanel.Controls.Add(_rollbackBtn);

            _versionGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _versionGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Version",
                HeaderText = "版本"
            });
            _versionGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Accuracy",
                HeaderText = "准确率"
            });
            _versionGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Samples",
                HeaderText = "样本数"
            });
            _versionGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "TrainedAt",
                HeaderText = "训练时间"
            });
            _versionGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Current",
                HeaderText = "状态"
            });

            Controls.Add(_versionGrid);
            Controls.Add(topPanel);
            ResumeLayout(false);
        }

        private ComboBox _modelCombo;
        private AntdUI.Button _refreshBtn;
        private AntdUI.Button _rollbackBtn;
        private DataGridView _versionGrid;
    }
}
