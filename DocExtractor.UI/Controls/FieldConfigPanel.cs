using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using DocExtractor.Core.Models;
using DocExtractor.Core.Normalization;
using DocExtractor.Data.Export;
using DocExtractor.UI.Context;
using DocExtractor.UI.Forms;
using DocExtractor.UI.Helpers;

namespace DocExtractor.UI.Controls
{
    /// <summary>
    /// Field configuration panel: field definition grid, global settings, config CRUD.
    /// </summary>
    public partial class FieldConfigPanel : UserControl
    {
        private readonly DocExtractorContext _ctx;

        /// <summary>Raised when the config list changes (new/delete). MainForm reloads its combo.</summary>
        public event Action<int> ConfigListChanged;

        /// <summary>Raised when the active config's data is saved. AppContext.CurrentConfig is already updated.</summary>
        public event Action ConfigDataSaved;

        internal FieldConfigPanel(DocExtractorContext ctx)
        {
            _ctx = ctx;
            InitializeComponent();
            WireEvents();
            _ctx.ConfigChanged += LoadConfigToGrids;
        }

        public void OnActivated() => LoadConfigToGrids();

        // ── Event Wiring ──────────────────────────────────────────────────────

        private void WireEvents()
        {
            _saveConfigBtn.Click += OnSaveConfig;
            _setDefaultBtn.Click += OnSetDefault;
            _newConfigBtn.Click += OnNewConfig;
            _deleteConfigBtn.Click += OnDeleteConfig;
            _importConfigBtn.Click += OnImportConfig;
            _exportConfigBtn.Click += OnExportConfig;
            _importJsonBtn.Click += OnImportJson;
            _exportJsonBtn.Click += OnExportJson;
            _fieldsGrid.CellContentClick += OnFieldsGridCellClick;
            _valueCleaningCheckBox.CheckedChanged += OnValueCleaningToggled;
            _cleaningRulesBtn.Click += OnOpenCleaningRules;
            _timeAxisCheckBox.CheckedChanged += OnTimeAxisToggled;
        }

        // ── Grid Loading ──────────────────────────────────────────────────────

        private void LoadConfigToGrids()
        {
            LoadFieldsGrid();
            UpdateConfigTypeBadge();
        }

        private void LoadFieldsGrid()
        {
            _fieldsGrid.Rows.Clear();
            foreach (var f in _ctx.CurrentConfig.Fields)
            {
                _fieldsGrid.Rows.Add(
                    f.FieldName,
                    f.DisplayName,
                    f.DataType.ToString(),
                    f.IsRequired,
                    f.DefaultValue ?? string.Empty,
                    string.Join(",", f.KnownColumnVariants));
            }

            _headerRowsSpinner.Value = _ctx.CurrentConfig.HeaderRowCount;
            var matchItem = _columnMatchCombo.Items.Cast<string>()
                .FirstOrDefault(x => x == _ctx.CurrentConfig.ColumnMatch.ToString());
            if (matchItem != null) _columnMatchCombo.SelectedItem = matchItem;
            _valueNormalizationCheckBox.Checked = _ctx.CurrentConfig.EnableValueNormalization;

            var opts = _ctx.CurrentConfig.NormalizationOptions;
            _valueCleaningCheckBox.Checked = opts?.EnableValueCleaning ?? false;
            _cleaningRulesBtn.Enabled = _valueCleaningCheckBox.Checked;
            _timeAxisCheckBox.Checked = opts?.EnableTimeAxisExpand ?? false;
            _timeAxisToleranceSpinner.Value = (decimal)(opts?.TimeAxisDefaultTolerance ?? 0);
            _timeAxisToleranceSpinner.Enabled = _timeAxisCheckBox.Checked;
        }

        private void UpdateConfigTypeBadge()
        {
            bool isBuiltIn = BuiltInConfigs.BuiltInNames.Contains(_ctx.CurrentConfig.ConfigName);
            _configTypeLabel.Text = isBuiltIn ? "内置配置" : "自定义配置";
            _configTypeLabel.BackColor = isBuiltIn
                ? System.Drawing.Color.FromArgb(22, 119, 255)
                : System.Drawing.Color.FromArgb(82, 196, 26);
            _configTypeLabel.Width = isBuiltIn ? 80 : 90;
            _deleteConfigBtn.Enabled = !isBuiltIn;
        }

        // ── Grid Editing ──────────────────────────────────────────────────────

        private void OnFieldsGridCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_fieldsGrid.Columns[e.ColumnIndex].Name != "EditField") return;

            var row = _fieldsGrid.Rows[e.RowIndex];
            if (row.IsNewRow) return;

            var field = BuildFieldFromRow(row);
            if (field == null) return;

            using var editor = new FieldEditorForm(field);
            if (editor.ShowDialog(this) != DialogResult.OK || editor.EditedField == null) return;

            WriteFieldToRow(row, editor.EditedField);
        }

        private FieldDefinition BuildFieldFromRow(DataGridViewRow row)
        {
            var name = row.Cells["FieldName"].Value?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return null;

            var f = new FieldDefinition
            {
                FieldName = name,
                DisplayName = row.Cells["DisplayName"].Value?.ToString() ?? name,
                IsRequired = row.Cells["IsRequired"].Value is true,
                DefaultValue = row.Cells["DefaultValue"].Value?.ToString() ?? string.Empty
            };
            if (Enum.TryParse<FieldDataType>(row.Cells["DataType"].Value?.ToString(), out var dt)) f.DataType = dt;
            var variants = row.Cells["Variants"].Value?.ToString() ?? string.Empty;
            f.KnownColumnVariants = new List<string>(variants.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            return f;
        }

        private static void WriteFieldToRow(DataGridViewRow row, FieldDefinition field)
        {
            row.Cells["FieldName"].Value = field.FieldName;
            row.Cells["DisplayName"].Value = field.DisplayName;
            row.Cells["DataType"].Value = field.DataType.ToString();
            row.Cells["IsRequired"].Value = field.IsRequired;
            row.Cells["DefaultValue"].Value = field.DefaultValue ?? string.Empty;
            row.Cells["Variants"].Value = string.Join(",", field.KnownColumnVariants);
        }

        // ── Value Cleaning ────────────────────────────────────────────────────

        private void OnValueCleaningToggled(object sender, EventArgs e)
        {
            _cleaningRulesBtn.Enabled = _valueCleaningCheckBox.Checked;
        }

        private void OnTimeAxisToggled(object sender, EventArgs e)
        {
            _timeAxisToleranceSpinner.Enabled = _timeAxisCheckBox.Checked;
        }

        private void OnOpenCleaningRules(object sender, EventArgs e)
        {
            EnsureNormalizationOptions();
            var opts = _ctx.CurrentConfig.NormalizationOptions;
            var rules = opts.GetEffectiveCleaningRules();

            using var dlg = new ValueCleaningRulesForm(rules);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            opts.CleaningRules = dlg.ResultRules;
        }

        private void EnsureNormalizationOptions()
        {
            if (_ctx.CurrentConfig.NormalizationOptions == null)
                _ctx.CurrentConfig.NormalizationOptions = new ValueNormalizationOptions();
        }

        // ── Config CRUD ───────────────────────────────────────────────────────

        private void OnSaveConfig(object sender, EventArgs e)
        {
            ReadGridIntoCurrentConfig();
            try
            {
                _ctx.CurrentConfigId = _ctx.ConfigService.Save(_ctx.CurrentConfig);
                MessageHelper.Success(this, "配置已保存");
                ConfigDataSaved?.Invoke();
            }
            catch (Exception ex) { MessageHelper.Error(this, $"保存失败：{ex.Message}"); }
        }

        private void OnSetDefault(object sender, EventArgs e)
        {
            if (_ctx.CurrentConfigId <= 0) return;
            _ctx.ConfigService.SetDefaultConfigId(_ctx.CurrentConfigId);
            MessageHelper.Success(this, $"已将「{_ctx.CurrentConfig.ConfigName}」设为默认配置");
        }

        private void OnNewConfig(object sender, EventArgs e)
        {
            using var dlg = new InputDialogForm("新建配置", "请输入新配置名称：", "自定义配置");
            if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.Result)) return;

            try
            {
                int id = _ctx.ConfigService.Save(new ExtractionConfig { ConfigName = dlg.Result });
                MessageHelper.Success(this, $"配置「{dlg.Result}」已创建");
                ConfigListChanged?.Invoke(id);
            }
            catch (Exception ex) { MessageHelper.Error(this, $"创建失败：{ex.Message}"); }
        }

        private void OnDeleteConfig(object sender, EventArgs e)
        {
            if (_ctx.CurrentConfigId <= 0) return;
            if (BuiltInConfigs.BuiltInNames.Contains(_ctx.CurrentConfig.ConfigName))
            {
                MessageHelper.Warn(this, "内置配置不可删除");
                return;
            }

            if (MessageBox.Show($"确定要删除配置「{_ctx.CurrentConfig.ConfigName}」吗？此操作不可恢复。",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                _ctx.ConfigService.Delete(_ctx.CurrentConfigId);
                MessageHelper.Success(this, "配置已删除");
                ConfigListChanged?.Invoke(-1);
            }
            catch (Exception ex) { MessageHelper.Error(this, $"删除失败：{ex.Message}"); }
        }

        private void OnImportConfig(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "Excel 文件|*.xlsx", Title = "选择字段配置 Excel 文件" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var config = ConfigImporter.ImportFromExcel(dlg.FileName);
                if (BuiltInConfigs.BuiltInNames.Contains(config.ConfigName))
                {
                    MessageHelper.Warn(this, $"配置名「{config.ConfigName}」与内置配置冲突，请修改后重试");
                    return;
                }

                if (!ConfirmOverwriteIfExists(config.ConfigName)) return;

                int id = _ctx.ConfigService.Save(config);
                MessageHelper.Success(this, $"配置「{config.ConfigName}」导入成功（{config.Fields.Count} 个字段）");
                ConfigListChanged?.Invoke(id);
            }
            catch (Exception ex) { MessageHelper.Error(this, $"导入失败：{ex.Message}"); }
        }

        private bool ConfirmOverwriteIfExists(string configName)
        {
            var existing = _ctx.ConfigService.GetAll().Find(c => c.Name == configName);
            if (existing.Id <= 0) return true;
            return MessageBox.Show($"配置「{configName}」已存在，是否覆盖？",
                "确认覆盖", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void OnExportConfig(object sender, EventArgs e)
        {
            if (_ctx.CurrentConfig == null || _ctx.CurrentConfig.Fields.Count == 0)
            {
                MessageHelper.Warn(this, "当前配置无字段可导出"); return;
            }

            ReadGridIntoCurrentConfig();

            using var dlg = new SaveFileDialog { Filter = "Excel 文件|*.xlsx", FileName = $"{_ctx.CurrentConfig.ConfigName}.xlsx" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                TemplateGenerator.GenerateConfigTemplateWithData(dlg.FileName, _ctx.CurrentConfig);
                MessageHelper.Success(this, "配置已导出");
            }
            catch (Exception ex) { MessageHelper.Error(this, $"导出失败：{ex.Message}"); }
        }

        // ── JSON Import / Export ──────────────────────────────────────────────

        private void OnImportJson(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "JSON 配置方案|*.json",
                Title = "选择配置方案 JSON 文件"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                string json = System.IO.File.ReadAllText(dlg.FileName);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<ExtractionConfig>(json);
                if (config == null || string.IsNullOrWhiteSpace(config.ConfigName))
                {
                    MessageHelper.Warn(this, "JSON 文件内容无效或缺少配置名称");
                    return;
                }

                if (BuiltInConfigs.BuiltInNames.Contains(config.ConfigName))
                {
                    MessageHelper.Warn(this, $"配置名「{config.ConfigName}」与内置配置冲突，请修改后重试");
                    return;
                }

                if (!ConfirmOverwriteIfExists(config.ConfigName)) return;

                int id = _ctx.ConfigService.Save(config);
                MessageHelper.Success(this, $"配置「{config.ConfigName}」导入成功（{config.Fields.Count} 个字段）");
                ConfigListChanged?.Invoke(id);
            }
            catch (Exception ex) { MessageHelper.Error(this, $"JSON 导入失败：{ex.Message}"); }
        }

        private void OnExportJson(object sender, EventArgs e)
        {
            if (_ctx.CurrentConfig == null || _ctx.CurrentConfig.Fields.Count == 0)
            {
                MessageHelper.Warn(this, "当前配置无字段可导出");
                return;
            }

            ReadGridIntoCurrentConfig();

            using var dlg = new SaveFileDialog
            {
                Filter = "JSON 配置方案|*.json",
                FileName = $"{_ctx.CurrentConfig.ConfigName}.json"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(_ctx.CurrentConfig, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(dlg.FileName, json);
                MessageHelper.Success(this, "配置方案已导出为 JSON");
            }
            catch (Exception ex) { MessageHelper.Error(this, $"JSON 导出失败：{ex.Message}"); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ReadGridIntoCurrentConfig()
        {
            _ctx.CurrentConfig.Fields.Clear();
            foreach (DataGridViewRow row in _fieldsGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var f = BuildFieldFromRow(row);
                if (f != null) _ctx.CurrentConfig.Fields.Add(f);
            }
            _ctx.CurrentConfig.HeaderRowCount = (int)_headerRowsSpinner.Value;
            if (Enum.TryParse<ColumnMatchMode>(_columnMatchCombo.SelectedItem?.ToString(), out var cm))
                _ctx.CurrentConfig.ColumnMatch = cm;
            _ctx.CurrentConfig.EnableValueNormalization = _valueNormalizationCheckBox.Checked;

            EnsureNormalizationOptions();
            _ctx.CurrentConfig.NormalizationOptions.EnableValueCleaning = _valueCleaningCheckBox.Checked;
            _ctx.CurrentConfig.NormalizationOptions.EnableTimeAxisExpand = _timeAxisCheckBox.Checked;
            _ctx.CurrentConfig.NormalizationOptions.TimeAxisDefaultTolerance = (double)_timeAxisToleranceSpinner.Value;
        }
    }
}
