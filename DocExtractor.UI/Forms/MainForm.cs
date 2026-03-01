using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using DocExtractor.Data.Export;
using DocExtractor.UI.Context;
using DocExtractor.UI.Controls;
using DocExtractor.UI.Helpers;

namespace DocExtractor.UI.Forms
{
    /// <summary>
    /// Main shell window. Owns navigation, config combo, menu bar, and status bar.
    /// All feature logic lives in the UserControl panels.
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly DocExtractorContext _ctx;
        private readonly ExtractionPanel _extractionPanel;
        private readonly FieldConfigPanel _fieldConfigPanel;
        private readonly SplitRulePanel _splitRulePanel;
        private readonly TrainingPanel _trainingPanel;
        private readonly ProtocolParserPanel _protocolParserPanel;

        private List<(int Id, string Name)> _configItems = new List<(int, string)>();
        private UserControl _activePanel;

        public MainForm()
        {
            _ctx = new DocExtractorContext(Application.StartupPath);
            InitializeComponent();

            _ctx.InitializeLogging();
            _ctx.ConfigService.SeedBuiltInConfigs();
            _ctx.TryLoadModels();

            _extractionPanel = new ExtractionPanel(_ctx);
            _fieldConfigPanel = new FieldConfigPanel(_ctx);
            _fieldConfigPanel.ConfigListChanged += OnConfigListChanged;
            _fieldConfigPanel.ConfigDataSaved += OnConfigDataSaved;
            _splitRulePanel = new SplitRulePanel(_ctx);
            _trainingPanel = new TrainingPanel(_ctx);
            _protocolParserPanel = new ProtocolParserPanel(_ctx);

            _ctx.StatusMessage += msg => _statusBarLabel.Text = msg;

            LoadConfigCombo();
            ShowPanel(_extractionPanel);
            HighlightNavButton(_navExtractionBtn);
        }

        // ── Navigation ────────────────────────────────────────────────────────

        private void ShowPanel(UserControl panel)
        {
            if (_activePanel != null)
            {
                _contentPanel.Controls.Remove(_activePanel);
            }

            panel.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(panel);
            _activePanel = panel;

            if (panel is ExtractionPanel ep) ep.OnActivated();
            else if (panel is FieldConfigPanel fcp) fcp.OnActivated();
            else if (panel is SplitRulePanel srp) srp.OnActivated();
            else if (panel is TrainingPanel tp) tp.OnActivated();
            else if (panel is ProtocolParserPanel pp) pp.OnActivated();
        }

        private void HighlightNavButton(AntdUI.Button active)
        {
            foreach (var btn in new[] { _navExtractionBtn, _navFieldConfigBtn, _navSplitRuleBtn, _navTrainingBtn, _navProtocolBtn })
                btn.Type = btn == active ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
        }

        private void OnNavExtraction(object sender, EventArgs e) { ShowPanel(_extractionPanel); HighlightNavButton(_navExtractionBtn); }
        private void OnNavFieldConfig(object sender, EventArgs e) { ShowPanel(_fieldConfigPanel); HighlightNavButton(_navFieldConfigBtn); }
        private void OnNavSplitRule(object sender, EventArgs e) { ShowPanel(_splitRulePanel); HighlightNavButton(_navSplitRuleBtn); }
        private void OnNavTraining(object sender, EventArgs e) { ShowPanel(_trainingPanel); HighlightNavButton(_navTrainingBtn); }
        private void OnNavProtocol(object sender, EventArgs e) { ShowPanel(_protocolParserPanel); HighlightNavButton(_navProtocolBtn); }

        // ── Config Combo ──────────────────────────────────────────────────────

        private void LoadConfigCombo(int selectId = -1)
        {
            _configCombo.SelectedIndexChanged -= OnConfigComboChanged;
            _configItems = _ctx.ConfigService.GetAll();
            _configCombo.Items.Clear();
            foreach (var item in _configItems) _configCombo.Items.Add(item.Name);

            if (_configItems.Count == 0) return;

            int targetId = selectId > 0 ? selectId : _ctx.ConfigService.GetDefaultConfigId();
            int selectedIndex = 0;
            for (int i = 0; i < _configItems.Count; i++)
            {
                if (_configItems[i].Id == targetId) { selectedIndex = i; break; }
            }

            _configCombo.SelectedIndex = selectedIndex;
            _configCombo.SelectedIndexChanged += OnConfigComboChanged;
            LoadSelectedConfig();
        }

        private void OnConfigComboChanged(object sender, EventArgs e) => LoadSelectedConfig();

        private void LoadSelectedConfig()
        {
            int idx = _configCombo.SelectedIndex;
            if (idx < 0 || idx >= _configItems.Count) return;

            _ctx.CurrentConfigId = _configItems[idx].Id;
            var config = _ctx.ConfigService.GetById(_ctx.CurrentConfigId);
            if (config != null)
            {
                _ctx.CurrentConfig = config;
                _ctx.NotifyConfigChanged();
            }
        }

        private void OnConfigListChanged(int selectId) => LoadConfigCombo(selectId);
        private void OnConfigDataSaved() => LoadConfigCombo(_ctx.CurrentConfigId);

        // ── Menu Handlers ─────────────────────────────────────────────────────

        private void OnMenuAddFiles(object sender, EventArgs e)
        {
            ShowPanel(_extractionPanel);
            HighlightNavButton(_navExtractionBtn);
            _extractionPanel.OnActivated();
        }

        private void OnMenuExport(object sender, EventArgs e) => MessageHelper.Info(this, "请切换到「数据抽取」面板后使用导出功能");

        private void OnOpenTemplateDir()
        {
            string dir = Path.Combine(Application.StartupPath, "templates");
            if (Directory.Exists(dir)) System.Diagnostics.Process.Start("explorer.exe", dir);
            else MessageHelper.Warn(this, "模板目录不存在，将在下次启动时自动创建");
        }

        private void OnRegenerateTemplates()
        {
            try
            {
                string dir = Path.Combine(Application.StartupPath, "templates");
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.xlsx")) File.Delete(f);
                TemplateGenerator.EnsureTemplates(dir);
                MessageHelper.Success(this, "模板已重新生成");
            }
            catch (Exception ex) { MessageHelper.Error(this, $"模板生成失败：{ex.Message}"); }
        }

        private void OnReloadModels()
        {
            _ctx.TryLoadModels();
            MessageHelper.Success(this, "模型已重新加载");
        }

        private void OnOpenModelManager()
        {
            using var form = new ModelManagerForm(_ctx.ModelsDir, _ctx.ReloadModelByName);
            form.ShowDialog(this);
        }

        private void OnOpenPackManager()
        {
            using var form = new PackManagerForm(_ctx.DbPath, () => _ctx.CurrentConfig);
            form.ShowDialog(this);
            LoadConfigCombo(_ctx.CurrentConfigId);
        }

        private void OnOpenDiagnostics()
        {
            using var form = new DiagnosticsForm(_ctx.DbPath, _ctx.ModelsDir);
            form.ShowDialog(this);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _ctx.Dispose();
            base.OnFormClosed(e);
        }
    }
}
