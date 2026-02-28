using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class ValueCleaningRulesForm
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
            this._rulesGrid = new DataGridView();
            this._btnPanel = new FlowLayoutPanel();
            this._selectAllBtn = new AntdUI.Button();
            this._deselectAllBtn = new AntdUI.Button();
            this._okBtn = new AntdUI.Button();
            this._cancelBtn = new AntdUI.Button();
            this._headerLabel = new Label();

            this.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this._rulesGrid).BeginInit();

            // ── Header ──────────────────────────────────────────────────────
            this._headerLabel.Dock = DockStyle.Top;
            this._headerLabel.Height = 36;
            this._headerLabel.Text = "选择要启用的值清洗规则（勾选启用，取消勾选禁用）";
            this._headerLabel.Font = new Font("微软雅黑", 9F);
            this._headerLabel.Padding = new Padding(8, 8, 0, 0);

            // ── Rules Grid ──────────────────────────────────────────────────
            this._rulesGrid.Dock = DockStyle.Fill;
            this._rulesGrid.AllowUserToAddRows = false;
            this._rulesGrid.AllowUserToDeleteRows = false;
            this._rulesGrid.AllowUserToResizeRows = false;
            this._rulesGrid.RowHeadersVisible = false;
            this._rulesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this._rulesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this._rulesGrid.Font = new Font("微软雅黑", 9F);
            this._rulesGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "启用", FillWeight = 8 },
                new DataGridViewTextBoxColumn { Name = "RuleName", HeaderText = "规则名称", ReadOnly = true, FillWeight = 18 },
                new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "说明", ReadOnly = true, FillWeight = 34 },
                new DataGridViewTextBoxColumn { Name = "Example", HeaderText = "示例", ReadOnly = true, FillWeight = 40 }
            });

            // ── Button Panel ────────────────────────────────────────────────
            this._btnPanel.Dock = DockStyle.Bottom;
            this._btnPanel.Height = 46;
            this._btnPanel.Padding = new Padding(8, 4, 8, 4);
            this._btnPanel.FlowDirection = FlowDirection.RightToLeft;

            this._cancelBtn.Text = "取消";
            this._cancelBtn.Size = new Size(80, 32);

            this._okBtn.Text = "确定";
            this._okBtn.Type = AntdUI.TTypeMini.Primary;
            this._okBtn.Size = new Size(80, 32);

            this._deselectAllBtn.Text = "全不选";
            this._deselectAllBtn.Size = new Size(80, 32);
            this._deselectAllBtn.Margin = new Padding(0, 0, 16, 0);

            this._selectAllBtn.Text = "全选";
            this._selectAllBtn.Size = new Size(80, 32);

            this._btnPanel.Controls.AddRange(new Control[]
            {
                this._cancelBtn,
                this._okBtn,
                this._deselectAllBtn,
                this._selectAllBtn
            });

            // ── Form ────────────────────────────────────────────────────────
            this.Controls.Add(this._rulesGrid);
            this.Controls.Add(this._headerLabel);
            this.Controls.Add(this._btnPanel);

            ((System.ComponentModel.ISupportInitialize)this._rulesGrid).EndInit();

            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = new Font("微软雅黑", 9F);
            this.Text = "值清洗规则配置";
            this.Name = "ValueCleaningRulesForm";
            this.Size = new Size(640, 340);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            this.ResumeLayout(false);
        }

        private Label _headerLabel;
        private DataGridView _rulesGrid;
        private FlowLayoutPanel _btnPanel;
        private AntdUI.Button _selectAllBtn;
        private AntdUI.Button _deselectAllBtn;
        private AntdUI.Button _okBtn;
        private AntdUI.Button _cancelBtn;
    }
}
