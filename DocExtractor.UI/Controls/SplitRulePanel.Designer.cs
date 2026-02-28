using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Controls
{
    partial class SplitRulePanel
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
            this._splitGrid = new DataGridView();
            this._btnBar = new FlowLayoutPanel();
            this._saveSplitBtn = new AntdUI.Button();

            this.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this._splitGrid).BeginInit();

            // ── Split Rule Grid ───────────────────────────────────────────────
            this._splitGrid.Dock = DockStyle.Fill;
            this._splitGrid.AllowUserToAddRows = true;
            this._splitGrid.AllowUserToDeleteRows = true;
            this._splitGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this._splitGrid.Font = new Font("微软雅黑", 9);
            this._splitGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "RuleName", HeaderText = "规则名", FillWeight = 15 },
                new DataGridViewComboBoxColumn
                {
                    Name = "Type", HeaderText = "拆分类型",
                    DataSource = System.Enum.GetNames(typeof(DocExtractor.Core.Models.SplitType)),
                    FillWeight = 20
                },
                new DataGridViewTextBoxColumn { Name = "TriggerColumn", HeaderText = "触发字段", FillWeight = 15 },
                new DataGridViewTextBoxColumn { Name = "Delimiters", HeaderText = "分隔符", FillWeight = 15 },
                new DataGridViewTextBoxColumn { Name = "GroupByColumn", HeaderText = "分组字段", FillWeight = 15 },
                new DataGridViewCheckBoxColumn { Name = "InheritParent", HeaderText = "继承父行", FillWeight = 10 },
                new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "优先级", FillWeight = 10 },
                new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "启用", FillWeight = 8 }
            });

            // ── Button Bar ────────────────────────────────────────────────────
            this._btnBar.Dock = DockStyle.Bottom;
            this._btnBar.Height = 42;

            this._saveSplitBtn.Text = "保存规则";
            this._saveSplitBtn.Type = AntdUI.TTypeMini.Primary;
            this._saveSplitBtn.Size = new Size(100, 34);

            this._btnBar.Controls.Add(this._saveSplitBtn);

            this.Controls.Add(this._splitGrid);
            this.Controls.Add(this._btnBar);

            ((System.ComponentModel.ISupportInitialize)this._splitGrid).EndInit();

            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = new Font("微软雅黑", 9F);
            this.Name = "SplitRulePanel";
            this.Size = new Size(900, 600);
            this.Padding = new Padding(8);

            this.ResumeLayout(false);
        }

        private DataGridView _splitGrid;
        private FlowLayoutPanel _btnBar;
        private AntdUI.Button _saveSplitBtn;
    }
}
