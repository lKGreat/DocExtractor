using System;
using System.Collections.Generic;
using System.Drawing;
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

        private Panel   _navPanel     = null!;
        private Button  _navAnalysisBtn   = null!;
        private Button  _navLearningBtn   = null!;
        private Button  _navDashboardBtn  = null!;
        private Panel   _contentPanel = null!;

        private NlpTextAnalysisPanel?    _analysisPanel;
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
            this.Text            = "NLP 主动学习实验室";
            this.Size            = new Size(1280, 860);
            this.MinimumSize     = new Size(1000, 680);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.Font            = new Font("微软雅黑", 9F);
            this.BackColor       = Color.FromArgb(245, 247, 250);

            BuildToolbar();
            BuildNavPanel();
            BuildContentArea();
            BuildStatusBar();
        }

        private void BuildToolbar()
        {
            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = Color.FromArgb(255, 255, 255),
                Padding   = new Padding(10, 8, 10, 8)
            };

            // 左侧 Logo 标题
            var logoLabel = new Label
            {
                Text      = "🧠 NLP 主动学习实验室",
                Dock      = DockStyle.Left,
                Width     = 200,
                Height    = 36,
                Font      = new Font("微软雅黑", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 144, 255),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 场景选择区
            var scenarioBar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding       = new Padding(0, 0, 0, 0),
                AutoSize      = false
            };

            var scenarioLabel = new Label
            {
                Text      = "场景：",
                Width     = 42,
                Height    = 34,
                TextAlign = ContentAlignment.MiddleRight,
                Font      = new Font("微软雅黑", 9F)
            };

            _scenarioCombo = new ComboBox
            {
                Width         = 180,
                Height        = 34,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("微软雅黑", 9F)
            };
            _scenarioCombo.SelectedIndexChanged += OnScenarioChanged;

            _newScenarioBtn = new Button
            {
                Text      = "+ 新建场景",
                Width     = 90,
                Height    = 32,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("微软雅黑", 8.5F)
            };
            _newScenarioBtn.Click += OnNewScenario;

            _deleteScenarioBtn = new Button
            {
                Text      = "删除场景",
                Width     = 80,
                Height    = 32,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("微软雅黑", 8.5F),
                Enabled   = false
            };
            _deleteScenarioBtn.Click += OnDeleteScenario;

            _importTextsBtn = new Button
            {
                Text      = "导入文本",
                Width     = 80,
                Height    = 32,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("微软雅黑", 8.5F)
            };
            _importTextsBtn.Click += OnImportTexts;

            scenarioBar.Controls.AddRange(new Control[] {
                scenarioLabel, _scenarioCombo, _newScenarioBtn, _deleteScenarioBtn, _importTextsBtn
            });

            toolbar.Controls.Add(scenarioBar);
            toolbar.Controls.Add(logoLabel);

            this.Controls.Add(toolbar);
        }

        private void BuildNavPanel()
        {
            _navPanel = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 100,
                BackColor = Color.FromArgb(40, 44, 52),
                Padding   = new Padding(6, 8, 6, 8)
            };

            _navAnalysisBtn = CreateNavBtn("文本分析");
            _navAnalysisBtn.Click += (s, e) => ShowPage(0);

            _navLearningBtn = CreateNavBtn("主动学习");
            _navLearningBtn.Click += (s, e) => ShowPage(1);

            _navDashboardBtn = CreateNavBtn("质量仪表盘");
            _navDashboardBtn.Click += (s, e) => ShowPage(2);

            // 底部提示
            var modelStatus = new Label
            {
                Text      = _nerModel.IsLoaded ? "● 模型已加载" : "○ 模型未加载",
                Dock      = DockStyle.Bottom,
                Height    = 28,
                Font      = new Font("微软雅黑", 7.5F),
                ForeColor = _nerModel.IsLoaded ? Color.LightGreen : Color.Orange,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _navPanel.Controls.Add(modelStatus);
            _navPanel.Controls.Add(_navDashboardBtn);
            _navPanel.Controls.Add(_navLearningBtn);
            _navPanel.Controls.Add(_navAnalysisBtn);

            this.Controls.Add(_navPanel);
        }

        private static Button CreateNavBtn(string text)
        {
            var btn = new Button
            {
                Text      = text,
                Dock      = DockStyle.Top,
                Height    = 52,
                Margin    = new Padding(0, 0, 0, 4),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("微软雅黑", 8.5F),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.Transparent
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => ((Button)s!).ForeColor = Color.White;
            btn.MouseLeave += (s, e) =>
            {
                var b = (Button)s!;
                if (b.Tag as string != "active") b.ForeColor = Color.FromArgb(180, 180, 180);
            };
            return btn;
        }

        private void BuildContentArea()
        {
            _contentPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding   = new Padding(0)
            };
            this.Controls.Add(_contentPanel);
        }

        private void BuildStatusBar()
        {
            _statusBar = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 26,
                Font      = new Font("微软雅黑", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                BackColor = Color.FromArgb(240, 240, 240),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0),
                Text      = "就绪"
            };
            this.Controls.Add(_statusBar);
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

            // 重建面板（场景切换时重新创建，确保数据绑定正确）
            _analysisPanel  = null;
            _learningPanel  = null;
            _dashboardPanel = null;

            // 显示当前已激活的页面
            int page = GetActivePage();
            ShowPage(page, force: true);

            UpdateStatus($"已切换到场景：{_activeScenario.Name}（{_activeScenario.EntityTypes.Count} 种实体类型）");
        }

        private void OnNewScenario(object sender, EventArgs e)
        {
            using var dlg = new NewScenarioDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            int id = _scenarioMgr.CreateScenario(dlg.ScenarioName, dlg.Description, dlg.EntityTypes);
            ReloadScenarioCombo();
            // 选中新建的场景
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
                Title           = "选择文本文件（每行一条）",
                Filter          = "文本文件 (*.txt)|*.txt|所有文件|*.*",
                Multiselect     = false
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var lines = System.IO.File.ReadAllLines(ofd.FileName, System.Text.Encoding.UTF8);
                int count = _engine.EnqueueTextsForReview(lines, _activeScenario.Id);
                MessageBox.Show($"已导入 {lines.Length} 行，其中 {count} 条加入了不确定性队列",
                    "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateStatus($"已导入 {count} 条文本到主动学习队列");

                // 刷新主动学习面板
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

            HighlightNavBtn(page);

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
            if (_activeControl == _learningPanel)   return 1;
            if (_activeControl == _dashboardPanel)  return 2;
            return 0;
        }

        private NlpTextAnalysisPanel GetAnalysisPanel()
        {
            if (_analysisPanel == null)
            {
                _analysisPanel = new NlpTextAnalysisPanel(_engine, _activeScenario!);
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

        private void HighlightNavBtn(int page)
        {
            var buttons = new[] { _navAnalysisBtn, _navLearningBtn, _navDashboardBtn };
            for (int i = 0; i < buttons.Length; i++)
            {
                bool active = i == page;
                buttons[i].BackColor = active ? Color.FromArgb(24, 144, 255) : Color.Transparent;
                buttons[i].ForeColor = active ? Color.White : Color.FromArgb(180, 180, 180);
                buttons[i].Tag       = active ? "active" : "";
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

        private TextBox   _nameBox    = null!;
        private TextBox   _descBox    = null!;
        private TextBox   _typesBox   = null!;

        public NewScenarioDialog()
        {
            Text            = "新建场景";
            Size            = new Size(460, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Font            = new Font("微软雅黑", 9F);

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                Padding     = new Padding(14),
                RowCount    = 5
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            _nameBox = new TextBox { Dock = DockStyle.Fill };
            _descBox = new TextBox { Dock = DockStyle.Fill };
            _typesBox = new TextBox
            {
                Dock       = DockStyle.Fill,
                Multiline  = true,
                ScrollBars = ScrollBars.Vertical,
                Text       = "KeyInfo\nPerson\nOrganization\nDate\nNumber"
            };

            var hint = new Label
            {
                Text      = "实体类型：每行一个，将作为标签显示在提取结果中",
                Dock      = DockStyle.Fill,
                Font      = new Font("微软雅黑", 8F),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var okBtn = new Button
            {
                Text         = "创建",
                DialogResult = DialogResult.OK,
                BackColor    = Color.FromArgb(24, 144, 255),
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat,
                Width        = 80,
                Height       = 32
            };
            okBtn.FlatAppearance.BorderSize = 0;
            okBtn.Click += (s, e) =>
            {
                ScenarioName = _nameBox.Text.Trim();
                Description  = _descBox.Text.Trim();
                EntityTypes  = new List<string>();
                foreach (var line in _typesBox.Lines)
                {
                    string t = line.Trim();
                    if (!string.IsNullOrEmpty(t)) EntityTypes.Add(t);
                }
                if (string.IsNullOrEmpty(ScenarioName))
                { MessageBox.Show("场景名称不能为空", "提示"); DialogResult = DialogResult.None; }
                else if (EntityTypes.Count == 0)
                { MessageBox.Show("至少需要一种实体类型", "提示"); DialogResult = DialogResult.None; }
            };

            var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            btnPanel.Controls.AddRange(new Control[] { cancelBtn, okBtn });

            layout.Controls.Add(new Label { Text = "场景名称", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }); layout.Controls.Add(_nameBox);
            layout.Controls.Add(new Label { Text = "描述",     TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }); layout.Controls.Add(_descBox);
            layout.Controls.Add(new Label { Text = "实体类型", TextAlign = ContentAlignment.TopRight, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) }); layout.Controls.Add(_typesBox);
            layout.Controls.Add(new Label()); layout.Controls.Add(hint);
            layout.Controls.Add(new Label()); layout.Controls.Add(btnPanel);

            this.Controls.Add(layout);
            this.AcceptButton = okBtn;
            this.CancelButton = cancelBtn;
        }
    }
}
