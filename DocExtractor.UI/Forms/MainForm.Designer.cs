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
                SelectionMode = SelectionMode.MultiExtended,
                HorizontalScrollbar = true
            };
            _fileListBox.AllowDrop = true;

            _fileContextMenu = new ContextMenuStrip();
            _removeFileMenuItem = new ToolStripMenuItem("移除选中文件");
            _clearAllMenuItem = new ToolStripMenuItem("清空列表");
            _fileContextMenu.Items.AddRange(new ToolStripItem[]
            {
                _removeFileMenuItem,
                new ToolStripSeparator(),
                _clearAllMenuItem
            });
            _fileListBox.ContextMenuStrip = _fileContextMenu;

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

            // 右侧上方：TabControl（抽取结果 + 智能推荐）
            _resultTabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("微软雅黑", 9) };

            // ── 子 Tab：抽取结果 ──
            var resultTabPage = new TabPage { Text = "抽取结果" };
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
            resultTabPage.Controls.Add(_resultGrid);

            // ── 子 Tab：智能推荐 ──
            var recommendTabPage = new TabPage { Text = "智能推荐" };
            var recommendLayout = new Panel { Dock = DockStyle.Fill };

            // 顶部：组名输入 + 按钮
            var recommendTopPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
            _recommendGroupCombo = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDown, Font = new Font("微软雅黑", 9) };
            _recommendBtn = new AntdUI.Button { Text = "推荐", Type = AntdUI.TTypeMini.Primary, Size = new Size(70, 32) };
            _recommendCountLabel = new Label { Text = "知识库：0 条", Width = 160, Height = 32, TextAlign = ContentAlignment.MiddleLeft };
            recommendTopPanel.Controls.Add(new Label { Text = "组名:", Width = 40, Height = 32, TextAlign = ContentAlignment.MiddleRight });
            recommendTopPanel.Controls.AddRange(new Control[] { _recommendGroupCombo, _recommendBtn, _recommendCountLabel });

            // 推荐结果 Grid
            _recommendGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(245, 250, 255) },
                Font = new Font("微软雅黑", 9)
            };
            _recommendGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "RecNo", HeaderText = "#", FillWeight = 5, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "RecItem", HeaderText = "推荐项目", FillWeight = 25 },
                new DataGridViewTextBoxColumn { Name = "RecValue", HeaderText = "典型要求值", FillWeight = 25 },
                new DataGridViewTextBoxColumn { Name = "RecConf", HeaderText = "置信度", FillWeight = 12 },
                new DataGridViewTextBoxColumn { Name = "RecCount", HeaderText = "出现次数", FillWeight = 10 },
                new DataGridViewTextBoxColumn { Name = "RecSources", HeaderText = "来源文件", FillWeight = 23 }
            });

            // 空提示
            _recommendHintLabel = new Label
            {
                Text = "暂无匹配数据。请先通过数据抽取积累更多文档，\n系统会自动学习组名与细则的对应关系。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray,
                Font = new Font("微软雅黑", 10),
                Visible = true
            };

            recommendLayout.Controls.Add(_recommendGrid);
            recommendLayout.Controls.Add(_recommendHintLabel);
            recommendLayout.Controls.Add(recommendTopPanel);
            recommendTabPage.Controls.Add(recommendLayout);

            _resultTabs.TabPages.Add(resultTabPage);
            _resultTabs.TabPages.Add(recommendTabPage);

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

            rightSplit.Panel1.Controls.Add(_resultTabs);
            rightSplit.Panel2.Controls.Add(logPanel);

            extractSplit.Panel1.Controls.Add(filePanel);
            extractSplit.Panel2.Controls.Add(rightSplit);
            _extractionTab.Controls.Add(extractSplit);

            // ── Tab 2：字段配置 ─────────────────────────────────────────────
            _configTab = new TabPage { Text = "  字段配置  ", Padding = new Padding(8) };

            // 顶部信息栏：配置类型标签 + 导入/导出按钮
            var configTopPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(4) };
            var configTopFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _configTypeLabel = new Label
            {
                Text = "内置配置",
                Font = new Font("微软雅黑", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(22, 119, 255),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(80, 28),
                Margin = new Padding(0, 6, 12, 0)
            };
            _importConfigBtn = new AntdUI.Button { Text = "从 Excel 导入", Size = new Size(120, 32) };
            _exportConfigBtn = new AntdUI.Button { Text = "导出为 Excel", Size = new Size(110, 32) };
            configTopFlow.Controls.AddRange(new Control[] { _configTypeLabel, _importConfigBtn, _exportConfigBtn });
            configTopPanel.Controls.Add(configTopFlow);

            // 中：字段定义 Grid + 全局设置（SplitContainer）
            var configSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 400
            };

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
                new DataGridViewCheckBoxColumn { Name = "IsRequired", HeaderText = "必填", FillWeight = 6 },
                new DataGridViewTextBoxColumn { Name = "DefaultValue", HeaderText = "默认值", FillWeight = 12 },
                new DataGridViewTextBoxColumn { Name = "Variants", HeaderText = "列名变体（逗号分隔）", FillWeight = 40 }
            });

            // 下：全局设置
            var settingsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(8)
            };
            settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            _headerRowsSpinner = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 1, Width = 80 };
            _columnMatchCombo = new ComboBox
            {
                DataSource = System.Enum.GetNames(typeof(DocExtractor.Core.Models.ColumnMatchMode)),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 180
            };
            _valueNormalizationCheckBox = new CheckBox
            {
                Text = "启用抽取值归一化（推荐）",
                Checked = true,
                AutoSize = true
            };

            settingsPanel.Controls.Add(new Label { Text = "表头行数：", TextAlign = ContentAlignment.MiddleRight, Width = 80 }, 0, 0);
            settingsPanel.Controls.Add(_headerRowsSpinner, 1, 0);
            settingsPanel.Controls.Add(new Label { Text = "列名匹配：", TextAlign = ContentAlignment.MiddleRight, Width = 80 }, 2, 0);
            settingsPanel.Controls.Add(_columnMatchCombo, 3, 0);
            settingsPanel.Controls.Add(_valueNormalizationCheckBox, 0, 1);
            settingsPanel.SetColumnSpan(_valueNormalizationCheckBox, 4);

            // 底部操作按钮
            var configBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
            _saveConfigBtn = new AntdUI.Button { Text = "保存配置", Type = AntdUI.TTypeMini.Primary, Size = new Size(100, 34) };
            _setDefaultBtn = new AntdUI.Button { Text = "设为默认", Size = new Size(100, 34) };
            _newConfigBtn = new AntdUI.Button { Text = "新建配置", Size = new Size(100, 34) };
            _deleteConfigBtn = new AntdUI.Button { Text = "删除配置", Type = AntdUI.TTypeMini.Error, Size = new Size(100, 34) };
            configBtnPanel.Controls.AddRange(new Control[] { _saveConfigBtn, _setDefaultBtn, _newConfigBtn, _deleteConfigBtn });

            configSplit.Panel1.Controls.Add(_fieldsGrid);
            configSplit.Panel2.Controls.Add(settingsPanel);
            // Dock 顺序: Bottom → Top → Fill
            _configTab.Controls.Add(configSplit);
            _configTab.Controls.Add(configTopPanel);
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
                RowCount = 6,
                ColumnCount = 1
            };
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // 统计
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));  // 训练参数
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // 操作按钮
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));   // 结果对比
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 日志
            trainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));    // 保留

            // ── 统计 ──
            var statsGroup = new GroupBox { Text = "训练数据统计", Dock = DockStyle.Fill };
            var statsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            _colSampleCountLabel = new Label { Text = "列名分类样本：0 条", Width = 200, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            _nerSampleCountLabel = new Label { Text = "NER 标注样本：0 条", Width = 200, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            _sectionSampleCountLabel = new Label { Text = "章节标题样本：0 条", Width = 200, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            _knowledgeCountLabel = new Label { Text = "推荐知识库：0 条", Width = 200, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            _importCsvBtn = new AntdUI.Button { Text = "导入 CSV/Excel 标注", Size = new Size(160, 30) };
            _importSectionWordBtn = new AntdUI.Button { Text = "从 Word 导入章节标注", Size = new Size(170, 30) };
            _genFromKnowledgeBtn = new AntdUI.Button
            {
                Text = "从知识库生成训练数据",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(175, 30)
            };
            statsFlow.Controls.AddRange(new Control[]
            {
                _colSampleCountLabel, _nerSampleCountLabel,
                _sectionSampleCountLabel, _knowledgeCountLabel,
                _genFromKnowledgeBtn, _importCsvBtn, _importSectionWordBtn
            });
            statsGroup.Controls.Add(statsFlow);

            // ── 训练参数 ──
            var paramsGroup = new GroupBox { Text = "训练参数", Dock = DockStyle.Fill };
            var paramsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 3, Padding = new Padding(4) };
            paramsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            paramsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            paramsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            // Row 0: 预设选择
            _presetCombo = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _presetCombo.Items.AddRange(new object[] { "快速", "标准", "精细", "自定义" });
            _presetCombo.SelectedIndex = 1;
            _cvFoldsSpinner = new NumericUpDown { Minimum = 0, Maximum = 10, Value = 5, Width = 55 };
            _testFractionSpinner = new NumericUpDown { Minimum = 0.1m, Maximum = 0.5m, Value = 0.2m, Width = 55, DecimalPlaces = 1, Increment = 0.1m };
            _augmentCheckbox = new CheckBox { Text = "数据增强", AutoSize = true, Checked = false };

            paramsLayout.Controls.Add(new Label { Text = "预设:", TextAlign = ContentAlignment.MiddleRight, Width = 45 }, 0, 0);
            paramsLayout.Controls.Add(_presetCombo, 1, 0);
            paramsLayout.Controls.Add(new Label { Text = "CV折数:", TextAlign = ContentAlignment.MiddleRight, Width = 55 }, 2, 0);
            paramsLayout.Controls.Add(_cvFoldsSpinner, 3, 0);
            paramsLayout.Controls.Add(new Label { Text = "测试集:", TextAlign = ContentAlignment.MiddleRight, Width = 55 }, 4, 0);
            paramsLayout.Controls.Add(_testFractionSpinner, 5, 0);
            paramsLayout.Controls.Add(_augmentCheckbox, 6, 0);

            // Row 1: 列名 + NER 参数 (NAS-BERT: Epochs + BatchSize)
            _colEpochsSpinner = new NumericUpDown { Minimum = 1, Maximum = 50, Value = 4, Width = 55 };
            _colBatchSpinner = new NumericUpDown { Minimum = 4, Maximum = 128, Value = 32, Width = 55 };
            _nerEpochsSpinner = new NumericUpDown { Minimum = 1, Maximum = 50, Value = 4, Width = 55 };
            _nerBatchSpinner = new NumericUpDown { Minimum = 4, Maximum = 128, Value = 32, Width = 55 };

            paramsLayout.Controls.Add(new Label { Text = "列名Epoch:", TextAlign = ContentAlignment.MiddleRight, Width = 72 }, 0, 1);
            paramsLayout.Controls.Add(_colEpochsSpinner, 1, 1);
            paramsLayout.Controls.Add(new Label { Text = "列名Batch:", TextAlign = ContentAlignment.MiddleRight, Width = 72 }, 2, 1);
            paramsLayout.Controls.Add(_colBatchSpinner, 3, 1);
            paramsLayout.Controls.Add(new Label { Text = "NER Epoch:", TextAlign = ContentAlignment.MiddleRight, Width = 72 }, 4, 1);
            paramsLayout.Controls.Add(_nerEpochsSpinner, 5, 1);
            paramsLayout.Controls.Add(new Label { Text = "NER Batch:", TextAlign = ContentAlignment.MiddleRight, Width = 72 }, 6, 1);
            paramsLayout.Controls.Add(_nerBatchSpinner, 7, 1);

            // Row 2: 章节参数
            _secTreesSpinner = new NumericUpDown { Minimum = 10, Maximum = 1000, Value = 200, Width = 60 };
            _secLeavesSpinner = new NumericUpDown { Minimum = 5, Maximum = 100, Value = 20, Width = 55 };
            _secMinLeafSpinner = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 2, Width = 55 };

            paramsLayout.Controls.Add(new Label { Text = "章节树数:", TextAlign = ContentAlignment.MiddleRight, Width = 65 }, 0, 2);
            paramsLayout.Controls.Add(_secTreesSpinner, 1, 2);
            paramsLayout.Controls.Add(new Label { Text = "章节叶:", TextAlign = ContentAlignment.MiddleRight, Width = 55 }, 2, 2);
            paramsLayout.Controls.Add(_secLeavesSpinner, 3, 2);
            paramsLayout.Controls.Add(new Label { Text = "最小叶样本:", TextAlign = ContentAlignment.MiddleRight, Width = 80 }, 4, 2);
            paramsLayout.Controls.Add(_secMinLeafSpinner, 5, 2);

            paramsGroup.Controls.Add(paramsLayout);

            // ── 操作按钮 ──
            var trainBtnFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
            _trainUnifiedBtn = new AntdUI.Button { Text = "开始训练", Type = AntdUI.TTypeMini.Primary, Size = new Size(150, 36), Font = new Font("微软雅黑", 9, FontStyle.Bold) };
            _cancelTrainBtn = new AntdUI.Button { Text = "取消训练", Type = AntdUI.TTypeMini.Error, Size = new Size(100, 36), Enabled = false };
            trainBtnFlow.Controls.AddRange(new Control[] { _trainUnifiedBtn, _cancelTrainBtn });

            // ── 结果对比 ──
            _evalLabel = new Label
            {
                Text = "请先添加训练数据并点击训练按钮",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 9),
                AutoSize = false
            };
            _evalCompareLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 8.5f),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            var evalPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            evalPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            evalPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            evalPanel.Controls.Add(_evalLabel, 0, 0);
            evalPanel.Controls.Add(_evalCompareLabel, 0, 1);

            // ── 训练日志 ──
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
            trainLayout.Controls.Add(paramsGroup, 0, 1);
            trainLayout.Controls.Add(trainBtnFlow, 0, 2);
            trainLayout.Controls.Add(evalPanel, 0, 3);
            trainLayout.Controls.Add(trainLogPanel, 0, 4);
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
        private ContextMenuStrip _fileContextMenu;
        private ToolStripMenuItem _removeFileMenuItem;
        private ToolStripMenuItem _clearAllMenuItem;
        private AntdUI.Button _addFilesBtn;
        private AntdUI.Button _removeFileBtn;
        private AntdUI.Button _clearFilesBtn;
        private TabControl _resultTabs;
        private DataGridView _resultGrid;
        private ProgressBar _progressBar;
        private RichTextBox _logBox;

        // Tab 1：智能推荐
        private ComboBox _recommendGroupCombo;
        private AntdUI.Button _recommendBtn;
        private Label _recommendCountLabel;
        private DataGridView _recommendGrid;
        private Label _recommendHintLabel;

        // Tab 2：字段配置
        private Label _configTypeLabel;
        private AntdUI.Button _importConfigBtn;
        private AntdUI.Button _exportConfigBtn;
        private DataGridView _fieldsGrid;
        private NumericUpDown _headerRowsSpinner;
        private ComboBox _columnMatchCombo;
        private CheckBox _valueNormalizationCheckBox;
        private AntdUI.Button _saveConfigBtn;
        private AntdUI.Button _setDefaultBtn;
        private AntdUI.Button _newConfigBtn;
        private AntdUI.Button _deleteConfigBtn;

        // Tab 3：拆分规则
        private DataGridView _splitGrid;
        private AntdUI.Button _saveSplitBtn;

        // Tab 4：模型训练
        private Label _colSampleCountLabel;
        private Label _nerSampleCountLabel;
        private Label _sectionSampleCountLabel;
        private Label _knowledgeCountLabel;
        private AntdUI.Button _cancelTrainBtn;
        private AntdUI.Button _genFromKnowledgeBtn;
        private AntdUI.Button _importCsvBtn;
        private AntdUI.Button _importSectionWordBtn;
        private Label _evalLabel;
        private Label _evalCompareLabel;
        private ProgressBar _trainProgressBar;
        private RichTextBox _trainLogBox;

        // Tab 4：训练参数
        private ComboBox _presetCombo;
        private NumericUpDown _cvFoldsSpinner;
        private NumericUpDown _testFractionSpinner;
        private CheckBox _augmentCheckbox;
        private NumericUpDown _colEpochsSpinner;
        private NumericUpDown _colBatchSpinner;
        private NumericUpDown _nerEpochsSpinner;
        private NumericUpDown _nerBatchSpinner;
        private NumericUpDown _secTreesSpinner;
        private NumericUpDown _secLeavesSpinner;
        private NumericUpDown _secMinLeafSpinner;
        private AntdUI.Button _trainUnifiedBtn;

        // 状态栏
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusBarLabel;
    }
}
