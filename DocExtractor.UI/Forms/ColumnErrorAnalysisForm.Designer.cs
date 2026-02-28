using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class ColumnErrorAnalysisForm
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

            Text = "列名分类错误分析";
            Size = new Size(820, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("微软雅黑", 9F);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "No", HeaderText = "#", FillWeight = 8 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RawName", HeaderText = "原始列名", FillWeight = 32 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Predicted", HeaderText = "预测结果", FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Actual", HeaderText = "正确字段", FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Confidence", HeaderText = "置信度", FillWeight = 12 });

            Controls.Add(_grid);
            ResumeLayout(false);
        }

        private DataGridView _grid;
    }
}
