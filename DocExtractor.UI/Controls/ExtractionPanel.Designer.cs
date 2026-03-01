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
            // ── Action Bar controls ────────────────────────────────────────────
            this._actionBar = new Panel();
            this._addFilesBtn = new AntdUI.Button();
            this._previewBtn = new AntdUI.Button();
            this._runBtn = new AntdUI.Button();
            this._stopBtn = new AntdUI.Button();
            this._exportBtn = new AntdUI.Button();
            this._statsLabel = new Label();
            this._progressBar = new ProgressBar();

            // ── Layout ────────────────────────────────────────────────────────
            this._mainSplit = new SplitContainer();
            this._leftSplit = new SplitContainer();
            this._rightSplit = new SplitContainer();

            // ── File Panel ────────────────────────────────────────────────────
            this._filePanel = new Panel();
            this._fileListLabel = new Label();
            this._fileListBox = new ListBox();
            this._fileContextMenu = new ContextMenuStrip();
            this._removeFileMenuItem = new ToolStripMenuItem();
            this._clearAllMenuItem = new ToolStripMenuItem();
            this._fileBtnPanel = new FlowLayoutPanel();
            this._removeFileBtn = new AntdUI.Button();
            this._clearFilesBtn = new AntdUI.Button();

            // ── Table List Panel ──────────────────────────────────────────────
            this._tableListPanel = new Panel();
            this._tableListLabel = new Label();
            this._tableListBox = new ListBox();

            // ── Result Tabs ───────────────────────────────────────────────────
            this._resultTabs = new TabControl();

            // ── 文档预览 Tab ────────────────────────────────────────────────
            this._previewTab = new TabPage();
            this._previewSplit = new SplitContainer();
            this._columnMapPanel = new Panel();
            this._columnMapLabel = new Label();
            this._columnMapGrid = new DataGridView();
            this._dataPreviewPanel = new Panel();
            this._dataPreviewLabel = new Label();
            this._dataPreviewGrid = new DataGridView();

            // ── 抽取结果 Tab ────────────────────────────────────────────────
            this._resultTab = new TabPage();
            this._resultSearchPanel = new Panel();
            this._resultSearchLabel = new Label();
            this._resultSearchBox = new TextBox();
            this._resultGrid = new DataGridView();

            // ── 输出配置 Tab ────────────────────────────────────────────────
            this._outputConfigTab = new TabPage();
            this._outputConfigSplit = new SplitContainer();
            this._outputFieldPanel = new Panel();
            this._outputFieldLabel = new Label();
            this._outputFieldGrid = new DataGridView();
            this._outputFieldBtnPanel = new FlowLayoutPanel();
            this._moveUpBtn = new AntdUI.Button();
            this._moveDownBtn = new AntdUI.Button();
            this._sheetRulePanel = new Panel();
            this._sheetRuleLabel = new Label();
            this._splitModeLabel = new Label();
            this._splitModeCombo = new ComboBox();
            this._splitFieldLabel = new Label();
            this._splitFieldCombo = new ComboBox();
            this._sheetTemplateLabel = new Label();
            this._sheetTemplateBox = new TextBox();
            this._summarySheetCheck = new CheckBox();
            this._saveOutputConfigBtn = new AntdUI.Button();

            // ── 智能推荐 Tab ────────────────────────────────────────────────
            this._recommendTab = new TabPage();
            this._recommendLayout = new Panel();
            this._recommendTopPanel = new FlowLayoutPanel();
            this._recommendGroupCombo = new ComboBox();
            this._recommendBtn = new AntdUI.Button();
            this._recommendCountLabel = new Label();
            this._recommendGrid = new DataGridView();
            this._recommendHintLabel = new Label();

            // ── Log Panel ─────────────────────────────────────────────────────
            this._logPanel = new Panel();
            this._logBox = new RichTextBox();

            this.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this._mainSplit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._leftSplit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._rightSplit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._previewSplit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._outputConfigSplit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._columnMapGrid).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._dataPreviewGrid).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._resultGrid).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._outputFieldGrid).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._recommendGrid).BeginInit();

            // ── Action Bar ────────────────────────────────────────────────────
            this._actionBar.Dock = DockStyle.Top;
            this._actionBar.Height = 54;
            this._actionBar.Padding = new Padding(8, 8, 8, 6);

            var actionFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };

            this._addFilesBtn.Text = "添加文件";
            this._addFilesBtn.Size = new Size(90, 36);
            this._addFilesBtn.Margin = new Padding(0, 0, 4, 0);

            this._previewBtn.Text = "快速预览";
            this._previewBtn.Size = new Size(90, 36);
            this._previewBtn.Margin = new Padding(0, 0, 4, 0);

            this._runBtn.Text = "开始抽取";
            this._runBtn.Type = AntdUI.TTypeMini.Primary;
            this._runBtn.Size = new Size(110, 36);
            this._runBtn.Font = new Font("微软雅黑", 9, FontStyle.Bold);
            this._runBtn.Margin = new Padding(0, 0, 4, 0);

            this._stopBtn.Text = "停止";
            this._stopBtn.Type = AntdUI.TTypeMini.Error;
            this._stopBtn.Size = new Size(70, 36);
            this._stopBtn.Enabled = false;
            this._stopBtn.Margin = new Padding(0, 0, 4, 0);

            this._exportBtn.Text = "导出结果";
            this._exportBtn.Size = new Size(90, 36);
            this._exportBtn.Enabled = false;
            this._exportBtn.Margin = new Padding(0, 0, 8, 0);

            actionFlow.Controls.AddRange(new Control[] {
                this._addFilesBtn, this._previewBtn, this._runBtn,
                this._stopBtn, this._exportBtn
            });

            this._statsLabel.Text = "";
            this._statsLabel.Dock = DockStyle.Fill;
            this._statsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._statsLabel.Font = new Font("微软雅黑", 9);
            this._statsLabel.ForeColor = Color.FromArgb(80, 80, 80);

            this._progressBar.Dock = DockStyle.Bottom;
            this._progressBar.Height = 4;
            this._progressBar.Style = ProgressBarStyle.Continuous;

            this._actionBar.Controls.Add(this._statsLabel);
            this._actionBar.Controls.Add(actionFlow);
            this._actionBar.Controls.Add(this._progressBar);

            // ── Main Splitter (Left | Right) ──────────────────────────────────
            this._mainSplit.Dock = DockStyle.Fill;
            this._mainSplit.Orientation = Orientation.Vertical;
            this._mainSplit.SplitterDistance = 260;
            this._mainSplit.FixedPanel = FixedPanel.Panel1;

            // ── Left Splitter (Files | Tables) ────────────────────────────────
            this._leftSplit.Dock = DockStyle.Fill;
            this._leftSplit.Orientation = Orientation.Horizontal;
            this._leftSplit.SplitterDistance = 260;

            // ── File Panel ────────────────────────────────────────────────────
            this._filePanel.Dock = DockStyle.Fill;
            this._filePanel.Padding = new Padding(4, 4, 4, 0);

            this._fileListLabel.Text = "源文件列表";
            this._fileListLabel.Font = new Font("微软雅黑", 9, FontStyle.Bold);
            this._fileListLabel.Dock = DockStyle.Top;
            this._fileListLabel.Height = 24;

            this._fileListBox.Dock = DockStyle.Fill;
            this._fileListBox.SelectionMode = SelectionMode.MultiExtended;
            this._fileListBox.HorizontalScrollbar = true;
            this._fileListBox.Font = new Font("微软雅黑", 8.5f);

            this._removeFileMenuItem.Text = "移除选中文件";
            this._clearAllMenuItem.Text = "清空列表";
            this._fileContextMenu.Items.AddRange(new ToolStripItem[] {
                this._removeFileMenuItem, new ToolStripSeparator(), this._clearAllMenuItem
            });
            this._fileListBox.ContextMenuStrip = this._fileContextMenu;

            this._fileBtnPanel.Dock = DockStyle.Bottom;
            this._fileBtnPanel.Height = 36;
            this._fileBtnPanel.Padding = new Padding(0, 2, 0, 0);

            this._removeFileBtn.Text = "移除";
            this._removeFileBtn.Size = new Size(56, 28);
            this._removeFileBtn.Margin = new Padding(0, 0, 4, 0);

            this._clearFilesBtn.Text = "清空";
            this._clearFilesBtn.Size = new Size(56, 28);

            this._fileBtnPanel.Controls.AddRange(new Control[] { this._removeFileBtn, this._clearFilesBtn });
            this._filePanel.Controls.Add(this._fileListBox);
            this._filePanel.Controls.Add(this._fileBtnPanel);
            this._filePanel.Controls.Add(this._fileListLabel);

            // ── Table List Panel ──────────────────────────────────────────────
            this._tableListPanel.Dock = DockStyle.Fill;
            this._tableListPanel.Padding = new Padding(4, 4, 4, 4);

            this._tableListLabel.Text = "文档表格列表";
            this._tableListLabel.Font = new Font("微软雅黑", 9, FontStyle.Bold);
            this._tableListLabel.Dock = DockStyle.Top;
            this._tableListLabel.Height = 24;
            this._tableListLabel.ForeColor = Color.FromArgb(60, 100, 160);

            this._tableListBox.Dock = DockStyle.Fill;
            this._tableListBox.Font = new Font("微软雅黑", 8.5f);

            this._tableListPanel.Controls.Add(this._tableListBox);
            this._tableListPanel.Controls.Add(this._tableListLabel);

            this._leftSplit.Panel1.Controls.Add(this._filePanel);
            this._leftSplit.Panel2.Controls.Add(this._tableListPanel);
            this._mainSplit.Panel1.Controls.Add(this._leftSplit);

            // ── Right Splitter (Tabs | Log) ───────────────────────────────────
            this._rightSplit.Dock = DockStyle.Fill;
            this._rightSplit.Orientation = Orientation.Horizontal;
            this._rightSplit.SplitterDistance = 520;

            // ── Tabs ──────────────────────────────────────────────────────────
            this._resultTabs.Dock = DockStyle.Fill;
            this._resultTabs.Font = new Font("微软雅黑", 9);

            // ── 文档预览 Tab ────────────────────────────────────────────────
            this._previewTab.Text = "文档预览";

            this._previewSplit.Dock = DockStyle.Fill;
            this._previewSplit.Orientation = Orientation.Horizontal;
            this._previewSplit.SplitterDistance = 180;

            this._columnMapPanel.Dock = DockStyle.Fill;
            this._columnMapPanel.Padding = new Padding(4, 2, 4, 0);

            this._columnMapLabel.Text = "列映射状态（点击左侧表格列表切换）";
            this._columnMapLabel.Dock = DockStyle.Top;
            this._columnMapLabel.Height = 22;
            this._columnMapLabel.Font = new Font("微软雅黑", 8.5f);
            this._columnMapLabel.ForeColor = Color.FromArgb(80, 80, 80);

            this._columnMapGrid.Dock = DockStyle.Fill;
            this._columnMapGrid.ReadOnly = false;
            this._columnMapGrid.AllowUserToAddRows = false;
            this._columnMapGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this._columnMapGrid.Font = new Font("微软雅黑", 8.5f);
            this._columnMapGrid.RowHeadersVisible = false;
            this._columnMapGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this._columnMapGrid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 250, 255)
            };
            this._columnMapGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "ColRaw", HeaderText = "原始列名", FillWeight = 28, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColMapped", HeaderText = "匹配字段", FillWeight = 28, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColMethod", HeaderText = "匹配方式", FillWeight = 16, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColConf", HeaderText = "置信度", FillWeight = 14, ReadOnly = true },
                new DataGridViewComboBoxColumn { Name = "ColFix", HeaderText = "修正字段", FillWeight = 24, ReadOnly = false },
            });

            this._columnMapPanel.Controls.Add(this._columnMapGrid);
            this._columnMapPanel.Controls.Add(this._columnMapLabel);

            this._dataPreviewPanel.Dock = DockStyle.Fill;
            this._dataPreviewPanel.Padding = new Padding(4, 2, 4, 4);

            this._dataPreviewLabel.Text = "数据预览（前5行）";
            this._dataPreviewLabel.Dock = DockStyle.Top;
            this._dataPreviewLabel.Height = 22;
            this._dataPreviewLabel.Font = new Font("微软雅黑", 8.5f);
            this._dataPreviewLabel.ForeColor = Color.FromArgb(80, 80, 80);

            this._dataPreviewGrid.Dock = DockStyle.Fill;
            this._dataPreviewGrid.ReadOnly = true;
            this._dataPreviewGrid.AllowUserToAddRows = false;
            this._dataPreviewGrid.Font = new Font("微软雅黑", 8.5f);
            this._dataPreviewGrid.RowHeadersVisible = false;
            this._dataPreviewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            this._dataPreviewPanel.Controls.Add(this._dataPreviewGrid);
            this._dataPreviewPanel.Controls.Add(this._dataPreviewLabel);

            this._previewSplit.Panel1.Controls.Add(this._columnMapPanel);
            this._previewSplit.Panel2.Controls.Add(this._dataPreviewPanel);
            this._previewTab.Controls.Add(this._previewSplit);

            // ── 抽取结果 Tab ────────────────────────────────────────────────
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
            this._resultGrid.ReadOnly = true;
            this._resultGrid.AllowUserToAddRows = false;
            this._resultGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this._resultGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this._resultGrid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(245, 245, 250)
            };
            this._resultGrid.Font = new Font("微软雅黑", 9);

            this._resultTab.Controls.Add(this._resultGrid);
            this._resultTab.Controls.Add(this._resultSearchPanel);

            // ── 输出配置 Tab ────────────────────────────────────────────────
            this._outputConfigTab.Text = "输出配置";

            this._outputConfigSplit.Dock = DockStyle.Fill;
            this._outputConfigSplit.Orientation = Orientation.Horizontal;
            this._outputConfigSplit.SplitterDistance = 260;

            this._outputFieldPanel.Dock = DockStyle.Fill;
            this._outputFieldPanel.Padding = new Padding(4, 2, 4, 0);

            this._outputFieldLabel.Text = "字段输出映射（可编辑列名、调整顺序）";
            this._outputFieldLabel.Dock = DockStyle.Top;
            this._outputFieldLabel.Height = 22;
            this._outputFieldLabel.Font = new Font("微软雅黑", 8.5f);
            this._outputFieldLabel.ForeColor = Color.FromArgb(80, 80, 80);

            this._outputFieldGrid.Dock = DockStyle.Fill;
            this._outputFieldGrid.AllowUserToAddRows = false;
            this._outputFieldGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this._outputFieldGrid.Font = new Font("微软雅黑", 8.5f);
            this._outputFieldGrid.RowHeadersVisible = false;
            this._outputFieldGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;

            var colEnable = new DataGridViewCheckBoxColumn { Name = "OFEnable", HeaderText = "导出", Width = 50, ReadOnly = false };
            var colSrc = new DataGridViewTextBoxColumn { Name = "OFSource", HeaderText = "输入字段", FillWeight = 35, ReadOnly = true };
            var colOut = new DataGridViewTextBoxColumn { Name = "OFOutput", HeaderText = "输出列名", FillWeight = 45, ReadOnly = false };
            this._outputFieldGrid.Columns.AddRange(new DataGridViewColumn[] { colEnable, colSrc, colOut });
            this._outputFieldGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this._outputFieldGrid.Columns["OFEnable"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            this._outputFieldGrid.Columns["OFEnable"].Width = 50;

            this._outputFieldBtnPanel.Dock = DockStyle.Bottom;
            this._outputFieldBtnPanel.Height = 36;
            this._outputFieldBtnPanel.Padding = new Padding(0, 2, 0, 0);

            this._moveUpBtn.Text = "↑ 上移";
            this._moveUpBtn.Size = new Size(72, 28);
            this._moveUpBtn.Margin = new Padding(0, 0, 4, 0);

            this._moveDownBtn.Text = "↓ 下移";
            this._moveDownBtn.Size = new Size(72, 28);

            this._outputFieldBtnPanel.Controls.AddRange(new Control[] { this._moveUpBtn, this._moveDownBtn });
            this._outputFieldPanel.Controls.Add(this._outputFieldGrid);
            this._outputFieldPanel.Controls.Add(this._outputFieldBtnPanel);
            this._outputFieldPanel.Controls.Add(this._outputFieldLabel);

            // ── Sheet 规则面板 ────────────────────────────────────────────────
            this._sheetRulePanel.Dock = DockStyle.Fill;
            this._sheetRulePanel.Padding = new Padding(8, 4, 8, 4);

            this._sheetRuleLabel.Text = "Sheet 输出规则";
            this._sheetRuleLabel.Font = new Font("微软雅黑", 9, FontStyle.Bold);
            this._sheetRuleLabel.Dock = DockStyle.Top;
            this._sheetRuleLabel.Height = 26;

            var ruleLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                AutoSize = true
            };
            ruleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            ruleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 6; i++) ruleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            this._splitModeLabel.Text = "Sheet 切分方式:";
            this._splitModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._splitModeLabel.Dock = DockStyle.Fill;

            this._splitModeCombo.Dock = DockStyle.Fill;
            this._splitModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this._splitModeCombo.Items.AddRange(new object[] { "按来源文件", "按字段值", "不切分（单Sheet）" });
            this._splitModeCombo.SelectedIndex = 0;
            this._splitModeCombo.Font = new Font("微软雅黑", 9);
            this._splitModeCombo.Margin = new Padding(0, 4, 0, 0);

            this._splitFieldLabel.Text = "切分字段:";
            this._splitFieldLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._splitFieldLabel.Dock = DockStyle.Fill;

            this._splitFieldCombo.Dock = DockStyle.Fill;
            this._splitFieldCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this._splitFieldCombo.Font = new Font("微软雅黑", 9);
            this._splitFieldCombo.Margin = new Padding(0, 4, 0, 0);

            this._sheetTemplateLabel.Text = "Sheet 命名模板:";
            this._sheetTemplateLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._sheetTemplateLabel.Dock = DockStyle.Fill;

            this._sheetTemplateBox.Text = "{0}";
            this._sheetTemplateBox.Dock = DockStyle.Fill;
            this._sheetTemplateBox.Font = new Font("微软雅黑", 9);
            this._sheetTemplateBox.Margin = new Padding(0, 6, 0, 0);

            this._summarySheetCheck.Text = "多 Sheet 时生成「全部数据」汇总 Sheet";
            this._summarySheetCheck.Checked = true;
            this._summarySheetCheck.Dock = DockStyle.Fill;
            this._summarySheetCheck.Font = new Font("微软雅黑", 9);

            ruleLayout.Controls.Add(this._splitModeLabel, 0, 0);
            ruleLayout.Controls.Add(this._splitModeCombo, 1, 0);
            ruleLayout.Controls.Add(this._splitFieldLabel, 0, 1);
            ruleLayout.Controls.Add(this._splitFieldCombo, 1, 1);
            ruleLayout.Controls.Add(this._sheetTemplateLabel, 0, 2);
            ruleLayout.Controls.Add(this._sheetTemplateBox, 1, 2);
            ruleLayout.Controls.Add(new Label(), 0, 3);
            ruleLayout.Controls.Add(this._summarySheetCheck, 1, 3);

            this._saveOutputConfigBtn.Text = "保存输出方案";
            this._saveOutputConfigBtn.Type = AntdUI.TTypeMini.Primary;
            this._saveOutputConfigBtn.Size = new Size(120, 32);
            var saveBtnFlow = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(0, 4, 0, 0) };
            saveBtnFlow.Controls.Add(this._saveOutputConfigBtn);

            this._sheetRulePanel.Controls.Add(ruleLayout);
            this._sheetRulePanel.Controls.Add(saveBtnFlow);
            this._sheetRulePanel.Controls.Add(this._sheetRuleLabel);

            this._outputConfigSplit.Panel1.Controls.Add(this._outputFieldPanel);
            this._outputConfigSplit.Panel2.Controls.Add(this._sheetRulePanel);
            this._outputConfigTab.Controls.Add(this._outputConfigSplit);

            // ── 智能推荐 Tab ────────────────────────────────────────────────
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

            this._recommendLayout.Controls.Add(this._recommendGrid);
            this._recommendLayout.Controls.Add(this._recommendHintLabel);
            this._recommendLayout.Controls.Add(this._recommendTopPanel);
            this._recommendTab.Controls.Add(this._recommendLayout);

            // ── Add Tabs ─────────────────────────────────────────────────────
            this._resultTabs.TabPages.AddRange(new TabPage[] {
                this._previewTab, this._resultTab, this._outputConfigTab, this._recommendTab
            });

            // ── Log Panel ─────────────────────────────────────────────────────
            this._logBox.Dock = DockStyle.Fill;
            this._logBox.ReadOnly = true;
            this._logBox.BackColor = Color.FromArgb(30, 30, 30);
            this._logBox.ForeColor = Color.FromArgb(204, 204, 204);
            this._logBox.Font = new Font("Consolas", 9);
            this._logBox.ScrollBars = RichTextBoxScrollBars.Vertical;

            this._logPanel.Dock = DockStyle.Fill;
            this._logPanel.Controls.Add(this._logBox);

            // ── Assemble ──────────────────────────────────────────────────────
            this._rightSplit.Panel1.Controls.Add(this._resultTabs);
            this._rightSplit.Panel2.Controls.Add(this._logPanel);
            this._mainSplit.Panel2.Controls.Add(this._rightSplit);

            this.Controls.Add(this._mainSplit);
            this.Controls.Add(this._actionBar);

            ((System.ComponentModel.ISupportInitialize)this._mainSplit).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._leftSplit).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._rightSplit).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._previewSplit).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._outputConfigSplit).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._columnMapGrid).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._dataPreviewGrid).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._resultGrid).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._outputFieldGrid).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._recommendGrid).EndInit();

            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Name = "ExtractionPanel";
            this.Size = new Size(1200, 750);

            this.ResumeLayout(false);
        }

        // ── Action Bar ────────────────────────────────────────────────────────
        private Panel _actionBar;
        private AntdUI.Button _addFilesBtn;
        private AntdUI.Button _previewBtn;
        private AntdUI.Button _runBtn;
        private AntdUI.Button _stopBtn;
        private AntdUI.Button _exportBtn;
        private Label _statsLabel;
        private ProgressBar _progressBar;

        // ── Layout ────────────────────────────────────────────────────────────
        private SplitContainer _mainSplit;
        private SplitContainer _leftSplit;
        private SplitContainer _rightSplit;

        // ── File List ─────────────────────────────────────────────────────────
        private Panel _filePanel;
        private Label _fileListLabel;
        private ListBox _fileListBox;
        private ContextMenuStrip _fileContextMenu;
        private ToolStripMenuItem _removeFileMenuItem;
        private ToolStripMenuItem _clearAllMenuItem;
        private FlowLayoutPanel _fileBtnPanel;
        private AntdUI.Button _removeFileBtn;
        private AntdUI.Button _clearFilesBtn;

        // ── Table List ────────────────────────────────────────────────────────
        private Panel _tableListPanel;
        private Label _tableListLabel;
        private ListBox _tableListBox;

        // ── Tabs ──────────────────────────────────────────────────────────────
        private TabControl _resultTabs;

        // ── 文档预览 Tab ───────────────────────────────────────────────────────
        private TabPage _previewTab;
        private SplitContainer _previewSplit;
        private Panel _columnMapPanel;
        private Label _columnMapLabel;
        private DataGridView _columnMapGrid;
        private Panel _dataPreviewPanel;
        private Label _dataPreviewLabel;
        private DataGridView _dataPreviewGrid;

        // ── 抽取结果 Tab ───────────────────────────────────────────────────────
        private TabPage _resultTab;
        private Panel _resultSearchPanel;
        private Label _resultSearchLabel;
        private TextBox _resultSearchBox;
        private DataGridView _resultGrid;

        // ── 输出配置 Tab ───────────────────────────────────────────────────────
        private TabPage _outputConfigTab;
        private SplitContainer _outputConfigSplit;
        private Panel _outputFieldPanel;
        private Label _outputFieldLabel;
        private DataGridView _outputFieldGrid;
        private FlowLayoutPanel _outputFieldBtnPanel;
        private AntdUI.Button _moveUpBtn;
        private AntdUI.Button _moveDownBtn;
        private Panel _sheetRulePanel;
        private Label _sheetRuleLabel;
        private Label _splitModeLabel;
        private ComboBox _splitModeCombo;
        private Label _splitFieldLabel;
        private ComboBox _splitFieldCombo;
        private Label _sheetTemplateLabel;
        private TextBox _sheetTemplateBox;
        private CheckBox _summarySheetCheck;
        private AntdUI.Button _saveOutputConfigBtn;

        // ── 智能推荐 Tab ───────────────────────────────────────────────────────
        private TabPage _recommendTab;
        private Panel _recommendLayout;
        private FlowLayoutPanel _recommendTopPanel;
        private ComboBox _recommendGroupCombo;
        private AntdUI.Button _recommendBtn;
        private Label _recommendCountLabel;
        private DataGridView _recommendGrid;
        private Label _recommendHintLabel;

        // ── Log ───────────────────────────────────────────────────────────────
        private Panel _logPanel;
        private RichTextBox _logBox;
    }
}
