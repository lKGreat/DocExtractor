using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DocExtractor.Data.ActiveLearning;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.EntityExtractor;
using DocExtractor.UI.Controls;

namespace DocExtractor.UI.Forms
{
    /// <summary>
    /// NLP 主动学习实验室 — 独立窗口
    /// 与 MainForm 并排运行（非模态），通过共享 NerModel 与主界面协同
    /// </summary>
    public class NlpLabForm : Form
    {
        private readonly string _dbPath;
        private readonly string _modelsDir;
        private readonly NerModel _nerModel;
        private readonly ActiveLearningEngine _engine;
        private readonly ScenarioManager _scenarioMgr;

        private NlpScenario? _activeScenario;

        // ── 控件字段 ─────────────────────────────────────────────────────────
        private ComboBox _scenarioCombo      = null!;
        private Button   _newScenarioBtn     = null!;
        private Button   _deleteScenarioBtn  = null!;
        private Button   _importTextsBtn     = null!;
        private Label    _statusBar          = null!;
        private Label    _modelStatusLabel   = null!;
        private Panel    _mainContainer      = null!;
        private Panel    _headerContainer    = null!;

        private Panel   _tabBar        = null!;
        private Button  _tabAnalysis   = null!;
        private Button  _tabLearning   = null!;
        private Button  _tabDashboard  = null!;
        private Panel   _contentPanel  = null!;

        private NlpUnifiedAnnotationPanel? _analysisPanel;
        private NlpActiveLearningPanel?  _learningPanel;
        private NlpQualityDashboardPanel? _dashboardPanel;
        private Control? _activeControl;

        private List<NlpScenario> _scenarios = new List<NlpScenario>();

        public NlpLabForm(string dbPath, string modelsDir, NerModel nerModel)
        {
            _dbPath      = dbPath;
            _modelsDir   = modelsDir;
            _nerModel    = nerModel;
            _engine      = new ActiveLearningEngine(dbPath, modelsDir, nerModel);
            _scenarioMgr = new ScenarioManager(dbPath);

            InitializeComponent();
            InitializeScenarios();
        }

        // ── 界面初始化 ────────────────────────────────────────────────────────

        private void InitializeComponent()
        {
            this.Text          = "NLP 主动学习实验室";
            this.Size          = new Size(1280, 860);
            this.MinimumSize   = new Size(1000, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font          = NlpLabTheme.Body;
            this.BackColor     = NlpLabTheme.BgBody;

            BuildMainContainers();
            BuildTabBar();
            BuildToolbar();
            BuildContentArea();
            BuildStatusBar();
        }

        private void BuildMainContainers()
        {
            _mainContainer = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = NlpLabTheme.BgBody
            };

            _headerContainer = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 92,
                MinimumSize = new Size(0, 84),
                BackColor = NlpLabTheme.BgToolbar
            };

            _mainContainer.Controls.Add(_headerContainer);
            this.Controls.Add(_mainContainer);
        }

        private void BuildToolbar()
        {
            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = NlpLabTheme.BgToolbar,
                Padding   = new Padding(12, 8, 12, 8)
            };

            var logoLabel = new Label
            {
                Text      = "NLP 主动学习实验室",
                Dock      = DockStyle.Left,
                Width     = 180,
                Height    = 36,
                Font      = NlpLabTheme.Title,
                ForeColor = NlpLabTheme.Primary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var scenarioBar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize      = false,
                Padding       = new Padding(8, 0, 0, 0)
            };

            var scenarioLabel = new Label
            {
                Text      = "场景：",
                Width     = 42,
                Height    = 34,
                TextAlign = ContentAlignment.MiddleRight,
                Font      = NlpLabTheme.Body
            };

            _scenarioCombo = new ComboBox
            {
                Width         = 180,
                Height        = 34,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = NlpLabTheme.Body
            };
            _scenarioCombo.SelectedIndexChanged += OnScenarioChanged;

            _newScenarioBtn = NlpLabTheme.MakeDefault(new Button
            {
                Text   = "+ 新建场景",
                Width  = 90,
                Height = 32
            });
            _newScenarioBtn.Click += OnNewScenario;

            _deleteScenarioBtn = NlpLabTheme.MakeGhost(new Button
            {
                Text    = "删除场景",
                Width   = 80,
                Height  = 32,
                Enabled = false
            });
            _deleteScenarioBtn.Click += OnDeleteScenario;

            _importTextsBtn = NlpLabTheme.MakeDefault(new Button
            {
                Text   = "导入文本",
                Width  = 80,
                Height = 32
            });
            _importTextsBtn.Click += OnImportTexts;

            scenarioBar.Controls.AddRange(new Control[] {
                scenarioLabel, _scenarioCombo, _newScenarioBtn, _deleteScenarioBtn, _importTextsBtn
            });

            toolbar.Controls.Add(scenarioBar);
            toolbar.Controls.Add(logoLabel);

            _headerContainer.Controls.Add(toolbar);
        }

        private void BuildTabBar()
        {
            _tabBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = NlpLabTheme.BgToolbar,
                Padding   = new Padding(12, 0, 12, 0)
            };

            var tabFlow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Left,
                AutoSize      = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding       = new Padding(0, 4, 0, 0)
            };

            _tabAnalysis  = CreateTabButton("文本分析");
            _tabLearning  = CreateTabButton("主动学习");
            _tabDashboard = CreateTabButton("质量仪表盘");

            _tabAnalysis.Click  += (s, e) => ShowPage(0);
            _tabLearning.Click  += (s, e) => ShowPage(1);
            _tabDashboard.Click += (s, e) => ShowPage(2);

            tabFlow.Controls.AddRange(new Control[] { _tabAnalysis, _tabLearning, _tabDashboard });

            // separator line at the bottom
            var separator = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 1,
                BackColor = NlpLabTheme.Border
            };

            _tabBar.Controls.Add(tabFlow);
            _tabBar.Controls.Add(separator);

            _headerContainer.Controls.Add(_tabBar);
        }

        private static Button CreateTabButton(string text)
        {
            var btn = new Button
            {
                Text      = text,
                Width     = 100,
                Height    = 32,
                Margin    = new Padding(0, 0, 2, 0),
                FlatStyle = FlatStyle.Flat,
                Font      = NlpLabTheme.Body,
                ForeColor = NlpLabTheme.TextSecondary,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 244, 255);
            return btn;
        }

        private void BuildContentArea()
        {
            _contentPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = NlpLabTheme.BgBody,
                Padding   = new Padding(0)
            };
            _mainContainer.Controls.Add(_contentPanel);
        }

        private void BuildStatusBar()
        {
            _statusBar = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 26,
                Font      = NlpLabTheme.Small,
                ForeColor = NlpLabTheme.TextTertiary,
                BackColor = NlpLabTheme.BgStatusBar,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0),
                Text      = "就绪"
            };

            _modelStatusLabel = new Label
            {
                Dock      = DockStyle.Right,
                Width     = 140,
                Height    = 26,
                Font      = NlpLabTheme.Small,
                ForeColor = _nerModel.IsLoaded ? Color.FromArgb(82, 196, 26) : Color.DarkOrange,
                BackColor = NlpLabTheme.BgStatusBar,
                TextAlign = ContentAlignment.MiddleRight,
                Padding   = new Padding(0, 0, 12, 0),
                Text      = _nerModel.IsLoaded ? "● 模型已加载" : "○ 模型未加载"
            };

            var statusPanel = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 26,
                BackColor = NlpLabTheme.BgStatusBar
            };
            statusPanel.Controls.Add(_modelStatusLabel);
            statusPanel.Controls.Add(_statusBar);

            this.Controls.Add(statusPanel);
        }

        // ── 场景管理 ─────────────────────────────────────────────────────────

        private void InitializeScenarios()
        {
            _scenarioMgr.EnsureBuiltInScenarios();
            ReloadScenarioCombo();
            if (_scenarioCombo.Items.Count > 0)
                _scenarioCombo.SelectedIndex = 0;
        }

        private void ReloadScenarioCombo()
        {
            _scenarios = _scenarioMgr.GetAllScenarios();
            _scenarioCombo.Items.Clear();
            foreach (var s in _scenarios)
                _scenarioCombo.Items.Add(s.Name);
        }

        private void OnScenarioChanged(object sender, EventArgs e)
        {
            int idx = _scenarioCombo.SelectedIndex;
            if (idx < 0 || idx >= _scenarios.Count) return;

            _activeScenario = _scenarios[idx];
            _deleteScenarioBtn.Enabled = !_activeScenario.IsBuiltIn;

            DisposePanel(ref _analysisPanel);
            DisposePanel(ref _learningPanel);
            DisposePanel(ref _dashboardPanel);

            int page = GetActivePage();
            ShowPage(page, force: true);

            UpdateStatus($"已切换到场景：{_activeScenario.Name}（{_activeScenario.EntityTypes.Count} 种实体类型）");
        }

        private static void DisposePanel<T>(ref T? panel) where T : Control
        {
            panel?.Dispose();
            panel = null;
        }

        private void OnNewScenario(object sender, EventArgs e)
        {
            using var dlg = new NewScenarioDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            int id = _scenarioMgr.CreateScenario(
                dlg.ScenarioName,
                dlg.Description,
                dlg.EntityTypes,
                dlg.EnabledModes,
                dlg.TemplateConfigJson);
            ReloadScenarioCombo();
            for (int i = 0; i < _scenarios.Count; i++)
            {
                if (_scenarios[i].Id == id) { _scenarioCombo.SelectedIndex = i; break; }
            }
        }

        private void OnDeleteScenario(object sender, EventArgs e)
        {
            if (_activeScenario == null || _activeScenario.IsBuiltIn) return;
            if (MessageBox.Show($"确定删除场景\"{_activeScenario.Name}\"？（数据不会删除）",
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _scenarioMgr.DeleteScenario(_activeScenario.Id);
            ReloadScenarioCombo();
            if (_scenarioCombo.Items.Count > 0) _scenarioCombo.SelectedIndex = 0;
        }

        private void OnImportTexts(object sender, EventArgs e)
        {
            if (_activeScenario == null) return;

            using var ofd = new OpenFileDialog
            {
                Title       = "选择文本文件（每行一条）",
                Filter      = "文本文件 (*.txt)|*.txt|所有文件|*.*",
                Multiselect = false
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var lines = System.IO.File.ReadAllLines(ofd.FileName, System.Text.Encoding.UTF8);
                int count = _engine.EnqueueTextsForReview(lines, _activeScenario.Id);
                MessageBox.Show($"已导入 {lines.Length} 行，其中 {count} 条加入了不确定性队列",
                    "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateStatus($"已导入 {count} 条文本到主动学习队列");

                _learningPanel?.OnActivated();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── 页面导航 ─────────────────────────────────────────────────────────

        private int _currentPage = -1;

        private void ShowPage(int page, bool force = false)
        {
            if (_activeScenario == null) return;
            if (page == _currentPage && !force) return;
            _currentPage = page;

            HighlightTab(page);

            Control pageControl = page switch
            {
                0 => GetAnalysisPanel(),
                1 => GetLearningPanel(),
                2 => GetDashboardPanel(),
                _ => GetAnalysisPanel()
            };

            if (_activeControl != null)
                _contentPanel.Controls.Remove(_activeControl);

            pageControl.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(pageControl);
            _activeControl = pageControl;

            if (page == 1) _learningPanel?.OnActivated();
            if (page == 2) _dashboardPanel?.OnActivated();
        }

        private int GetActivePage()
        {
            if (_activeControl == _analysisPanel)  return 0;
            if (_activeControl == _learningPanel)  return 1;
            if (_activeControl == _dashboardPanel) return 2;
            return 0;
        }

        private NlpUnifiedAnnotationPanel GetAnalysisPanel()
        {
            if (_analysisPanel == null)
            {
                _analysisPanel = new NlpUnifiedAnnotationPanel(_engine, _activeScenario!);
                _analysisPanel.AnnotationSubmitted += () =>
                {
                    UpdateStatus($"已提交标注，当前场景共 {_engine.GetVerifiedCount(_activeScenario!.Id)} 条样本");
                };
            }
            return _analysisPanel;
        }

        private NlpActiveLearningPanel GetLearningPanel()
        {
            if (_learningPanel == null)
            {
                _learningPanel = new NlpActiveLearningPanel(_engine, _activeScenario!);
                _learningPanel.TrainingCompleted += () =>
                {
                    UpdateStatus("训练完成，模型已更新");
                    _dashboardPanel?.OnActivated();
                };
            }
            return _learningPanel;
        }

        private NlpQualityDashboardPanel GetDashboardPanel()
        {
            if (_dashboardPanel == null)
                _dashboardPanel = new NlpQualityDashboardPanel(_engine, _scenarioMgr, _activeScenario!);
            return _dashboardPanel;
        }

        private void HighlightTab(int page)
        {
            var tabs = new[] { _tabAnalysis, _tabLearning, _tabDashboard };
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = i == page;
                tabs[i].ForeColor = active ? NlpLabTheme.Primary : NlpLabTheme.TextSecondary;
                tabs[i].Font      = active ? NlpLabTheme.BodyBold : NlpLabTheme.Body;
                tabs[i].BackColor = active ? Color.FromArgb(230, 244, 255) : Color.Transparent;
            }
        }

        private void UpdateStatus(string msg)
        {
            if (this.IsHandleCreated && _statusBar != null)
            {
                if (this.InvokeRequired)
                    this.Invoke((Action)(() => _statusBar.Text = msg));
                else
                    _statusBar.Text = msg;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ShowPage(0);
        }
    }

    // ── 新建场景对话框 ────────────────────────────────────────────────────────

    internal class NewScenarioDialog : Form
    {
        public string       ScenarioName { get; private set; } = string.Empty;
        public string       Description  { get; private set; } = string.Empty;
        public List<string> EntityTypes  { get; private set; } = new List<string>();
        public List<AnnotationMode> EnabledModes { get; private set; } = new List<AnnotationMode> { AnnotationMode.SpanEntity };
        public string TemplateConfigJson { get; private set; } = "{}";

        private TextBox _nameBox  = null!;
        private TextBox _descBox  = null!;
        private TextBox _typesBox = null!;
        private CheckedListBox _modeList = null!;
        private TextBox _templateBox = null!;

        public NewScenarioDialog()
        {
            Text            = "新建场景";
            Size            = new Size(560, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Font            = NlpLabTheme.Body;

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                Padding     = new Padding(14),
                RowCount    = 7
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            _nameBox  = new TextBox { Dock = DockStyle.Fill };
            _descBox  = new TextBox { Dock = DockStyle.Fill };
            _typesBox = new TextBox
            {
                Dock       = DockStyle.Fill,
                Multiline  = true,
                ScrollBars = ScrollBars.Vertical,
                Text       = "KeyInfo\nPerson\nOrganization\nDate\nNumber"
            };
            _modeList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true
            };
            _modeList.Items.Add(AnnotationMode.SpanEntity, true);
            _modeList.Items.Add(AnnotationMode.KvSchema, true);
            _modeList.Items.Add(AnnotationMode.EnumBitfield, true);
            _modeList.Items.Add(AnnotationMode.Relation, true);
            _modeList.Items.Add(AnnotationMode.Sequence, true);
            _templateBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = AnnotationTemplateFactory.BuildDefaultTemplateJson("自定义场景")
            };

            var hint = new Label
            {
                Text      = "实体类型：每行一个，将作为标签显示在提取结果中",
                Dock      = DockStyle.Fill,
                Font      = NlpLabTheme.Small,
                ForeColor = NlpLabTheme.TextTertiary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var okBtn = NlpLabTheme.MakePrimary(new Button
            {
                Text         = "创建",
                DialogResult = DialogResult.OK,
                Width        = 80,
                Height       = 32
            });
            okBtn.Click += (s, e) =>
            {
                ScenarioName = _nameBox.Text.Trim();
                Description  = _descBox.Text.Trim();
                EntityTypes  = new List<string>();
                EnabledModes = new List<AnnotationMode>();
                foreach (var line in _typesBox.Lines)
                {
                    string t = line.Trim();
                    if (!string.IsNullOrEmpty(t)) EntityTypes.Add(t);
                }
                foreach (var item in _modeList.CheckedItems)
                {
                    if (item is AnnotationMode mode)
                        EnabledModes.Add(mode);
                }
                TemplateConfigJson = _templateBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(TemplateConfigJson))
                    TemplateConfigJson = AnnotationTemplateFactory.BuildDefaultTemplateJson(ScenarioName);
                if (string.IsNullOrEmpty(ScenarioName))
                { MessageBox.Show("场景名称不能为空", "提示"); DialogResult = DialogResult.None; }
                else if (EntityTypes.Count == 0)
                { MessageBox.Show("至少需要一种实体类型", "提示"); DialogResult = DialogResult.None; }
                else if (EnabledModes.Count == 0)
                { MessageBox.Show("至少需要选择一种标注模式", "提示"); DialogResult = DialogResult.None; }
            };

            var cancelBtn = NlpLabTheme.MakeGhost(new Button
            {
                Text         = "取消",
                DialogResult = DialogResult.Cancel,
                Width        = 80,
                Height       = 32
            });
            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            btnPanel.Controls.AddRange(new Control[] { cancelBtn, okBtn });

            layout.Controls.Add(new Label { Text = "场景名称", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill });
            layout.Controls.Add(_nameBox);
            layout.Controls.Add(new Label { Text = "描述", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill });
            layout.Controls.Add(_descBox);
            layout.Controls.Add(new Label { Text = "实体类型", TextAlign = ContentAlignment.TopRight, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) });
            layout.Controls.Add(_typesBox);
            layout.Controls.Add(new Label { Text = "标注模式", TextAlign = ContentAlignment.TopRight, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) });
            layout.Controls.Add(_modeList);
            layout.Controls.Add(new Label { Text = "模板JSON", TextAlign = ContentAlignment.TopRight, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) });
            layout.Controls.Add(_templateBox);
            layout.Controls.Add(new Label());
            layout.Controls.Add(hint);
            layout.Controls.Add(new Label());
            layout.Controls.Add(btnPanel);

            this.Controls.Add(layout);
            this.AcceptButton = okBtn;
            this.CancelButton = cancelBtn;
        }
    }
}
