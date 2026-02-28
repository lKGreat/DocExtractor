using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Controls
{
    partial class ExtractionPanel
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
            this._actionBar = new Panel();
            this._addFilesBtn = new AntdUI.Button();
            this._previewBtn = new AntdUI.Button();
            this._runBtn = new AntdUI.Button();
            this._exportBtn = new AntdUI.Button();
            this._mainSplit = new SplitContainer();
            this._filePanel = new Panel();
            this._fileListLabel = new Label();
            this._fileListBox = new ListBox();
            this._fileContextMenu = new ContextMenuStrip();
            this._removeFileMenuItem = new ToolStripMenuItem();
            this._clearAllMenuItem = new ToolStripMenuItem();
            this._fileBtnPanel = new FlowLayoutPanel();
            this._removeFileBtn = new AntdUI.Button();
            this._clearFilesBtn = new AntdUI.Button();
            this._rightSplit = new SplitContainer();
            this._resultTabs = new TabControl();
            this._resultTab = new TabPage();
            this._resultSearchPanel = new Panel();
            this._resultSearchLabel = new Label();
            this._resultSearchBox = new TextBox();
            this._resultGrid = new DataGridView();
            this._recommendTab = new TabPage();
            this._recommendLayout = new Panel();
            this._recommendTopPanel = new FlowLayoutPanel();
            this._recommendGroupCombo = new ComboBox();
            this._recommendBtn = new AntdUI.Button();
            this._recommendCountLabel = new Label();
            this._recommendGrid = new DataGridView();
            this._recommendHintLabel = new Label();
            this._logPanel = new Panel();
            this._progressBar = new ProgressBar();
            this._logBox = new RichTextBox();

            this.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this._mainSplit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._rightSplit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._resultGrid).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._recommendGrid).BeginInit();

            // ── Action Bar ────────────────────────────────────────────────────
            this._actionBar.Dock = DockStyle.Top;
            this._actionBar.Height = 48;
            this._actionBar.Padding = new Padding(8, 6, 8, 6);

            var actionFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            this._addFilesBtn.Text = "添加文件";
            this._addFilesBtn.Size = new Size(90, 36);

            this._previewBtn.Text = "快速预览";
            this._previewBtn.Size = new Size(90, 36);

            this._runBtn.Text = "开始抽取";
            this._runBtn.Type = AntdUI.TTypeMini.Primary;
            this._runBtn.Size = new Size(110, 36);
            this._runBtn.Font = new Font("微软雅黑", 9, FontStyle.Bold);

            this._exportBtn.Text = "导出结果";
            this._exportBtn.Size = new Size(90, 36);
            this._exportBtn.Enabled = false;

            actionFlow.Controls.AddRange(new Control[] { this._addFilesBtn, this._previewBtn, this._runBtn, this._exportBtn });
            this._actionBar.Controls.Add(actionFlow);

            // ── Main Splitter (Left: files | Right: results+log) ──────────────
            this._mainSplit.Dock = DockStyle.Fill;
            this._mainSplit.Orientation = Orientation.Vertical;
            this._mainSplit.SplitterDistance = 280;
            this._mainSplit.FixedPanel = FixedPanel.Panel1;

            // ── File Panel (left) ─────────────────────────────────────────────
            this._filePanel.Dock = DockStyle.Fill;
            this._filePanel.Padding = new Padding(4);

            this._fileListLabel.Text = "源文件列表";
            this._fileListLabel.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            this._fileListLabel.Dock = DockStyle.Top;
            this._fileListLabel.Height = 28;

            this._fileListBox.Dock = DockStyle.Fill;
            this._fileListBox.SelectionMode = SelectionMode.MultiExtended;
            this._fileListBox.HorizontalScrollbar = true;

            this._removeFileMenuItem.Text = "移除选中文件";
            this._clearAllMenuItem.Text = "清空列表";
            this._fileContextMenu.Items.AddRange(new ToolStripItem[] { this._removeFileMenuItem, new ToolStripSeparator(), this._clearAllMenuItem });
            this._fileListBox.ContextMenuStrip = this._fileContextMenu;

            this._fileBtnPanel.Dock = DockStyle.Bottom;
            this._fileBtnPanel.Height = 40;

            this._removeFileBtn.Text = "移除";
            this._removeFileBtn.Size = new Size(60, 32);

            this._clearFilesBtn.Text = "清空";
            this._clearFilesBtn.Size = new Size(60, 32);

            this._fileBtnPanel.Controls.AddRange(new Control[] { this._removeFileBtn, this._clearFilesBtn });

            this._filePanel.Controls.Add(this._fileListBox);
            this._filePanel.Controls.Add(this._fileBtnPanel);
            this._filePanel.Controls.Add(this._fileListLabel);

            // ── Right Splitter (Top: result tabs | Bottom: log) ───────────────
            this._rightSplit.Dock = DockStyle.Fill;
            this._rightSplit.Orientation = Orientation.Horizontal;
            this._rightSplit.SplitterDistance = 500;

            // ── Result Tabs ───────────────────────────────────────────────────
            this._resultTabs.Dock = DockStyle.Fill;
            this._resultTabs.Font = new Font("微软雅黑", 9);

            this._resultTab.Text = "抽取结果";
            this._resultSearchPanel.Dock = DockStyle.Top;
            this._resultSearchPanel.Height = 36;
            this._resultSearchPanel.Padding = new Padding(6, 6, 6, 4);

            this._resultSearchLabel.Text = "快速搜索：";
            this._resultSearchLabel.Width = 68;
            this._resultSearchLabel.Dock = DockStyle.Left;
            this._resultSearchLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this._resultSearchBox.Dock = DockStyle.Fill;

            this._resultSearchPanel.Controls.Add(this._resultSearchBox);
            this._resultSearchPanel.Controls.Add(this._resultSearchLabel);

            this._resultGrid.Dock = DockStyle.Fill;
            this._resultGrid.ReadOnly = false;
            this._resultGrid.AllowUserToAddRows = false;
            this._resultGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this._resultGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this._resultGrid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(245, 245, 250) };
            this._resultGrid.Font = new Font("微软雅黑", 9);

            this._resultTab.Controls.Add(this._resultGrid);
            this._resultTab.Controls.Add(this._resultSearchPanel);

            // ── Recommend Tab ─────────────────────────────────────────────────
            this._recommendTab.Text = "智能推荐";
            this._recommendLayout.Dock = DockStyle.Fill;

            this._recommendTopPanel.Dock = DockStyle.Top;
            this._recommendTopPanel.Height = 40;
            this._recommendTopPanel.Padding = new Padding(4);

            this._recommendGroupCombo.Width = 260;
            this._recommendGroupCombo.DropDownStyle = ComboBoxStyle.DropDown;
            this._recommendGroupCombo.Font = new Font("微软雅黑", 9);

            this._recommendBtn.Text = "推荐";
            this._recommendBtn.Type = AntdUI.TTypeMini.Primary;
            this._recommendBtn.Size = new Size(70, 32);

            this._recommendCountLabel.Text = "知识库：0 条";
            this._recommendCountLabel.Width = 160;
            this._recommendCountLabel.Height = 32;
            this._recommendCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this._recommendTopPanel.Controls.Add(new Label { Text = "组名:", Width = 40, Height = 32, TextAlign = System.Drawing.ContentAlignment.MiddleRight });
            this._recommendTopPanel.Controls.AddRange(new Control[] { this._recommendGroupCombo, this._recommendBtn, this._recommendCountLabel });

            this._recommendGrid.Dock = DockStyle.Fill;
            this._recommendGrid.ReadOnly = true;
            this._recommendGrid.AllowUserToAddRows = false;
            this._recommendGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this._recommendGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this._recommendGrid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(245, 250, 255) };
            this._recommendGrid.Font = new Font("微软雅黑", 9);
            this._recommendGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "RecNo", HeaderText = "#", FillWeight = 5, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "RecItem", HeaderText = "推荐项目", FillWeight = 25 },
                new DataGridViewTextBoxColumn { Name = "RecValue", HeaderText = "典型要求值", FillWeight = 25 },
                new DataGridViewTextBoxColumn { Name = "RecConf", HeaderText = "置信度", FillWeight = 12 },
                new DataGridViewTextBoxColumn { Name = "RecCount", HeaderText = "出现次数", FillWeight = 10 },
                new DataGridViewTextBoxColumn { Name = "RecSources", HeaderText = "来源文件", FillWeight = 23 }
            });

            this._recommendHintLabel.Text = "暂无匹配数据。请先通过数据抽取积累更多文档，\n系统会自动学习组名与细则的对应关系。";
            this._recommendHintLabel.Dock = DockStyle.Fill;
            this._recommendHintLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this._recommendHintLabel.ForeColor = Color.Gray;
            this._recommendHintLabel.Font = new Font("微软雅黑", 10);
            this._recommendHintLabel.Visible = true;

            this._recommendLayout.Controls.Add(this._recommendGrid);
            this._recommendLayout.Controls.Add(this._recommendHintLabel);
            this._recommendLayout.Controls.Add(this._recommendTopPanel);
            this._recommendTab.Controls.Add(this._recommendLayout);

            this._resultTabs.TabPages.AddRange(new TabPage[] { this._resultTab, this._recommendTab });

            // ── Log Panel ─────────────────────────────────────────────────────
            this._progressBar.Dock = DockStyle.Top;
            this._progressBar.Height = 6;
            this._progressBar.Style = ProgressBarStyle.Continuous;

            this._logBox.Dock = DockStyle.Fill;
            this._logBox.ReadOnly = true;
            this._logBox.BackColor = Color.FromArgb(30, 30, 30);
            this._logBox.ForeColor = Color.FromArgb(204, 204, 204);
            this._logBox.Font = new Font("Consolas", 9);
            this._logBox.ScrollBars = RichTextBoxScrollBars.Vertical;

            this._logPanel.Dock = DockStyle.Fill;
            this._logPanel.Controls.Add(this._logBox);
            this._logPanel.Controls.Add(this._progressBar);

            this._rightSplit.Panel1.Controls.Add(this._resultTabs);
            this._rightSplit.Panel2.Controls.Add(this._logPanel);

            this._mainSplit.Panel1.Controls.Add(this._filePanel);
            this._mainSplit.Panel2.Controls.Add(this._rightSplit);

            this.Controls.Add(this._mainSplit);
            this.Controls.Add(this._actionBar);

            ((System.ComponentModel.ISupportInitialize)this._mainSplit).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._rightSplit).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._resultGrid).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._recommendGrid).EndInit();

            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Name = "ExtractionPanel";
            this.Size = new Size(1100, 700);

            this.ResumeLayout(false);
        }

        // ── Action Bar ────────────────────────────────────────────────────────
        private Panel _actionBar;
        private AntdUI.Button _addFilesBtn;
        private AntdUI.Button _previewBtn;
        private AntdUI.Button _runBtn;
        private AntdUI.Button _exportBtn;

        // ── Layout ────────────────────────────────────────────────────────────
        private SplitContainer _mainSplit;
        private Panel _filePanel;
        private SplitContainer _rightSplit;

        // ── File List ─────────────────────────────────────────────────────────
        private Label _fileListLabel;
        private ListBox _fileListBox;
        private ContextMenuStrip _fileContextMenu;
        private ToolStripMenuItem _removeFileMenuItem;
        private ToolStripMenuItem _clearAllMenuItem;
        private FlowLayoutPanel _fileBtnPanel;
        private AntdUI.Button _removeFileBtn;
        private AntdUI.Button _clearFilesBtn;

        // ── Result Area ───────────────────────────────────────────────────────
        private TabControl _resultTabs;
        private TabPage _resultTab;
        private Panel _resultSearchPanel;
        private Label _resultSearchLabel;
        private TextBox _resultSearchBox;
        private DataGridView _resultGrid;

        // ── Recommendation Area ───────────────────────────────────────────────
        private TabPage _recommendTab;
        private Panel _recommendLayout;
        private FlowLayoutPanel _recommendTopPanel;
        private ComboBox _recommendGroupCombo;
        private AntdUI.Button _recommendBtn;
        private Label _recommendCountLabel;
        private DataGridView _recommendGrid;
        private Label _recommendHintLabel;

        // ── Log Area ──────────────────────────────────────────────────────────
        private Panel _logPanel;
        private ProgressBar _progressBar;
        private RichTextBox _logBox;
    }
}
