using System;
using System.Linq;
using System.Windows.Forms;
using DocExtractor.ML.ModelRegistry;
using DocExtractor.UI.Helpers;

namespace DocExtractor.UI.Forms
{
    public partial class ModelManagerForm : Form
    {
        private readonly string _modelsDir;
        private readonly Action<string>? _reloadCallback;

        public ModelManagerForm(string modelsDir, Action<string>? reloadCallback = null)
        {
            _modelsDir = modelsDir;
            _reloadCallback = reloadCallback;
            InitializeComponent();
            WireEvents();
            LoadModelNames();
        }

        private void WireEvents()
        {
            _modelCombo.SelectedIndexChanged += (_, _) => RefreshVersions();
            _refreshBtn.Click += (_, _) => RefreshVersions();
            _rollbackBtn.Click += (_, _) => RollbackSelectedVersion();
        }

        private void LoadModelNames()
        {
            _modelCombo.Items.Clear();
            _modelCombo.Items.Add("column_classifier");
            _modelCombo.Items.Add("ner_model");
            _modelCombo.Items.Add("section_classifier");
            _modelCombo.SelectedIndex = 0;
        }

        private void RefreshVersions()
        {
            string modelName = _modelCombo.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(modelName)) return;

            _versionGrid.Rows.Clear();
            var store = new ModelRegistryStore(_modelsDir);
            var current = store.GetCurrentVersion(modelName);
            var versions = store.GetVersions(modelName);

            foreach (var version in versions)
            {
                bool isCurrent = string.Equals(version.Version, current, StringComparison.OrdinalIgnoreCase);
                int row = _versionGrid.Rows.Add(
                    version.Version,
                    version.Accuracy.ToString("P2"),
                    version.Samples.ToString(),
                    version.TrainedAt,
                    isCurrent ? "当前" : string.Empty);
                if (isCurrent)
                    _versionGrid.Rows[row].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(230, 255, 230);
            }
        }

        private void RollbackSelectedVersion()
        {
            if (_versionGrid.SelectedRows.Count == 0)
            {
                MessageHelper.Warn(this, "请选择要回滚的版本");
                return;
            }

            string modelName = _modelCombo.SelectedItem?.ToString() ?? string.Empty;
            string version = _versionGrid.SelectedRows[0].Cells["Version"].Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(version))
                return;

            try
            {
                var store = new ModelRegistryStore(_modelsDir);
                bool ok = store.Rollback(modelName, version);
                if (!ok)
                {
                    MessageHelper.Warn(this, "回滚失败：目标版本不存在");
                    return;
                }

                _reloadCallback?.Invoke(modelName);
                RefreshVersions();
                MessageHelper.Success(this, $"已回滚 {modelName} 到版本 {version}");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"回滚失败：{ex.Message}");
            }
        }
    }
}
