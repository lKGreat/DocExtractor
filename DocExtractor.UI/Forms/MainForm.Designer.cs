using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class MainForm
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

            // ── 窗口属性 ──────────────────────────────────────────────────────
            Text = "DocExtractor \u2014 文档数据智能抽取系统";
            Size = new Size(1400, 900);
            MinimumSize = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("微软雅黑", 9);

            // ── 菜单栏 ────────────────────────────────────────────────────────
            _menuStrip = new MenuStrip { Dock = DockStyle.Top };
            var fileMenu = new ToolStripMenuItem("文件(&F)");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("添加文件...", null, (s, e) => OnAddFiles(s, e)) { ShortcutKeys = Keys.Control | Keys.O });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("导出结果...", null, (s, e) => OnExport(s, e)) { ShortcutKeys = Keys.Control | Keys.S });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("打开模板目录", null, (s, e) => OnOpenTemplateDir()));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("退出", null, (s, e) => Close()));

            var toolMenu = new ToolStripMenuItem("工具(&T)");
            toolMenu.DropDownItems.Add(new ToolStripMenuItem("重新生成模板", null, (s, e) => OnRegenerateTemplates()));
            toolMenu.DropDownItems.Add(new ToolStripMenuItem("重新加载模型", null, (s, e) => TryLoadModels()));

            var helpMenu = new ToolStripMenuItem("帮助(&H)");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("关于", null, (s, e) =>
                Helpers.MessageHelper.Info(this, "DocExtractor v1.0 \u2014 文档数据智能抽取系统")));

            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, toolMenu, helpMenu });

            // ── 工具栏 ────────────────────────────────────────────────────────
            _toolbar = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8, 6, 8, 6) };
            var toolFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            _configCombo = new ComboBox
            {
                Width = 200,
                Height = 32,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("微软雅黑", 9)
            };

            _runBtn = new AntdUI.Button
            {
                Text = "开始抽取",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(110, 36),
                Font = new Font("微软雅黑", 9, FontStyle.Bold)
            };

            _exportBtn = new AntdUI.Button
            {
                Text = "导出结果",
                Size = new Size(90, 36),
                Enabled = false,
                Font = new Font("微软雅黑", 9)
            };

            var configLabel = new Label { Text = "配置：", TextAlign = ContentAlignment.MiddleLeft, Width = 50, Height = 36 };
            toolFlow.Controls.AddRange(new Control[]
            {
                configLabel, _configCombo,
                new Label { Width = 16 },
                _runBtn, _exportBtn
            });
            _toolbar.Controls.Add(toolFlow);

            // ── 主体 Tabs ─────────────────────────────────────────────────────
            _mainTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("微软雅黑", 9.5f)
            };

            // ── Tab 1：数据抽取 ─────────────────────────────────────────────
            _extractionTab = new TabPage { Text = "  数据抽取  ", Padding = new Padding(4) };
            var extractSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 280,
                FixedPanel = FixedPanel.Panel1
            };

            // 左侧：文件列表
            var filePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            var fileLabel = new Label
            {
                Text = "源文件列表",
                Font = new Font("微软雅黑", 10, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 28
            };
            _fileListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.MultiSimple,
                HorizontalScrollbar = true
            };
            _fileListBox.AllowDrop = true;

            var fileBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
            _addFilesBtn = new AntdUI.Button { Text = "添加文件", Size = new Size(80, 32) };
            _removeFileBtn = new AntdUI.Button { Text = "移除", Size = new Size(60, 32) };
            _clearFilesBtn = new AntdUI.Button { Text = "清空", Size = new Size(60, 32) };
            fileBtnPanel.Controls.AddRange(new Control[] { _addFilesBtn, _removeFileBtn, _clearFilesBtn });

            filePanel.Controls.Add(_fileListBox);
            filePanel.Controls.Add(fileBtnPanel);
            filePanel.Controls.Add(fileLabel);

            // 右侧：结果 + 日志
            var rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 500
            };

            _resultGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = false,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(245, 245, 250)
                },
                Font = new Font("微软雅黑", 9)
            };

            _progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 6, Style = ProgressBarStyle.Continuous };
            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(204, 204, 204),
                Font = new Font("Consolas", 9),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            var logPanel = new Panel { Dock = DockStyle.Fill };
            logPanel.Controls.Add(_logBox);
            logPanel.Controls.Add(_progressBar);

            rightSplit.Panel1.Controls.Add(_resultGrid);
            rightSplit.Panel2.Controls.Add(logPanel);

            extractSplit.Panel1.Controls.Add(filePanel);
            extractSplit.Panel2.Controls.Add(rightSplit);
            _extractionTab.Controls.Add(extractSplit);

            // ── Tab 2：字段配置 ─────────────────────────────────────────────
            _configTab = new TabPage { Text = "  字段配置  ", Padding = new Padding(8) };
            var configSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 400
            };

            // 上：字段定义 Grid
            _fieldsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("微软雅黑", 9)
            };
            _fieldsGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "FieldName", HeaderText = "字段名（英文）", FillWeight = 15 },
                new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "显示名（中文）", FillWeight = 15 },
                new DataGridViewComboBoxColumn
                {
                    Name = "DataType", HeaderText = "数据类型",
                    DataSource = System.Enum.GetNames(typeof(DocExtractor.Core.Models.FieldDataType)),
                    FillWeight = 12
                },
                new DataGridViewCheckBoxColumn { Name = "IsRequired", HeaderText = "必填", FillWeight = 8 },
                new DataGridViewTextBoxColumn { Name = "Variants", HeaderText = "列名变体（逗号分隔）", FillWeight = 50 }
            });

            // 下：全局设置
            var settingsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                Padding = new Padding(8)
            };
            _headerRowsSpinner = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 1, Width = 80 };
            _columnMatchCombo = new ComboBox
            {
                DataSource = System.Enum.GetNames(typeof(DocExtractor.Core.Models.ColumnMatchMode)),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 180
            };

            settingsPanel.Controls.Add(new Label { Text = "表头行数：", TextAlign = ContentAlignment.MiddleRight, Width = 80 }, 0, 0);
            settingsPanel.Controls.Add(_headerRowsSpinner, 1, 0);
            settingsPanel.Controls.Add(new Label { Text = "列名匹配：", TextAlign = ContentAlignment.MiddleRight, Width = 80 }, 2, 0);
            settingsPanel.Controls.Add(_columnMatchCombo, 3, 0);

            var configBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
            _saveConfigBtn = new AntdUI.Button { Text = "保存配置", Type = AntdUI.TTypeMini.Primary, Size = new Size(100, 34) };
            configBtnPanel.Controls.Add(_saveConfigBtn);

            configSplit.Panel1.Controls.Add(_fieldsGrid);
            configSplit.Panel2.Controls.Add(settingsPanel);
            _configTab.Controls.Add(configSplit);
            _configTab.Controls.Add(configBtnPanel);

            // ── Tab 3：拆分规则 ─────────────────────────────────────────────
            _splitTab = new TabPage { Text = "  拆分规则  ", Padding = new Padding(8) };
            _splitGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("微软雅黑", 9)
            };
            _splitGrid.Columns.AddRange(new DataGridViewColumn[]
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

            var splitBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
            _saveSplitBtn = new AntdUI.Button { Text = "保存规则", Type = AntdUI.TTypeMini.Primary, Size = new Size(100, 34) };
            splitBtnPanel.Controls.Add(_saveSplitBtn);

            _splitTab.Controls.Add(_splitGrid);
            _splitTab.Controls.Add(splitBtnPanel);

            // ── Tab 4：模型训练 ─────────────────────────────────────────────
            _trainingTab = new TabPage { Text = "  模型训练  ", Padding = new Padding(8) };
            var trainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 统计
            var statsGroup = new GroupBox { Text = "训练数据统计", Dock = DockStyle.Fill };
            var statsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            _colSampleCountLabel = new Label { Text = "列名分类样本：0 条", Width = 220, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            _nerSampleCountLabel = new Label { Text = "NER 标注样本：0 条", Width = 220, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            statsFlow.Controls.AddRange(new Control[] { _colSampleCountLabel, _nerSampleCountLabel });
            statsGroup.Controls.Add(statsFlow);

            // 训练按钮
            var trainBtnFlow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _trainColumnBtn = new AntdUI.Button { Text = "训练列名分类器", Type = AntdUI.TTypeMini.Primary, Size = new Size(150, 36) };
            _trainNerBtn = new AntdUI.Button { Text = "训练 NER 模型", Type = AntdUI.TTypeMini.Primary, Size = new Size(140, 36) };
            _importCsvBtn = new AntdUI.Button { Text = "导入 CSV/Excel 标注", Size = new Size(160, 36) };
            trainBtnFlow.Controls.AddRange(new Control[] { _trainColumnBtn, _trainNerBtn, _importCsvBtn });

            // 评估结果
            _evalLabel = new Label
            {
                Text = "请先添加训练数据并点击训练按钮",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 9)
            };

            // 训练日志
            _trainProgressBar = new ProgressBar { Dock = DockStyle.Top, Height = 6 };
            _trainLogBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(204, 204, 204),
                Font = new Font("Consolas", 9)
            };
            var trainLogPanel = new Panel { Dock = DockStyle.Fill };
            trainLogPanel.Controls.Add(_trainLogBox);
            trainLogPanel.Controls.Add(_trainProgressBar);

            trainLayout.Controls.Add(statsGroup, 0, 0);
            trainLayout.Controls.Add(trainBtnFlow, 0, 1);
            trainLayout.Controls.Add(_evalLabel, 0, 2);
            trainLayout.Controls.Add(trainLogPanel, 0, 3);
            _trainingTab.Controls.Add(trainLayout);

            // ── 组装 Tabs ──────────────────────────────────────────────────
            _mainTabs.TabPages.AddRange(new TabPage[]
            {
                _extractionTab, _configTab, _splitTab, _trainingTab
            });

            // ── 状态栏 ────────────────────────────────────────────────────────
            _statusStrip = new StatusStrip();
            _statusBarLabel = new ToolStripStatusLabel("就绪") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _statusStrip.Items.Add(_statusBarLabel);

            // ── 组装窗口 ──────────────────────────────────────────────────────
            Controls.Add(_mainTabs);
            Controls.Add(_toolbar);
            Controls.Add(_menuStrip);
            Controls.Add(_statusStrip);
            MainMenuStrip = _menuStrip;

            ResumeLayout(false);
            PerformLayout();
        }

        // ── 控件声明 ──────────────────────────────────────────────────────────

        // 菜单 + 工具栏
        private MenuStrip _menuStrip;
        private Panel _toolbar;
        private ComboBox _configCombo;
        private AntdUI.Button _runBtn;
        private AntdUI.Button _exportBtn;

        // Tab 控件
        private TabControl _mainTabs;
        private TabPage _extractionTab;
        private TabPage _configTab;
        private TabPage _splitTab;
        private TabPage _trainingTab;

        // Tab 1：数据抽取
        private ListBox _fileListBox;
        private AntdUI.Button _addFilesBtn;
        private AntdUI.Button _removeFileBtn;
        private AntdUI.Button _clearFilesBtn;
        private DataGridView _resultGrid;
        private ProgressBar _progressBar;
        private RichTextBox _logBox;

        // Tab 2：字段配置
        private DataGridView _fieldsGrid;
        private NumericUpDown _headerRowsSpinner;
        private ComboBox _columnMatchCombo;
        private AntdUI.Button _saveConfigBtn;

        // Tab 3：拆分规则
        private DataGridView _splitGrid;
        private AntdUI.Button _saveSplitBtn;

        // Tab 4：模型训练
        private Label _colSampleCountLabel;
        private Label _nerSampleCountLabel;
        private AntdUI.Button _trainColumnBtn;
        private AntdUI.Button _trainNerBtn;
        private AntdUI.Button _importCsvBtn;
        private Label _evalLabel;
        private ProgressBar _trainProgressBar;
        private RichTextBox _trainLogBox;

        // 状态栏
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusBarLabel;
    }
}
