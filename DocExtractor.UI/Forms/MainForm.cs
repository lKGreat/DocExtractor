using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Data.Export;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.ColumnClassifier;
using DocExtractor.ML.EntityExtractor;
using DocExtractor.ML.SectionClassifier;
using DocExtractor.ML.Training;
using DocExtractor.UI.Helpers;
using DocExtractor.UI.Logging;
using DocExtractor.UI.Services;
using Microsoft.Extensions.Logging;

namespace DocExtractor.UI.Forms
{
    /// <summary>
    /// 主窗口：单窗口 Tabs 架构，集成数据抽取、字段配置、拆分规则、模型训练
    /// </summary>
    public partial class MainForm : Form
    {
        // ── 状态 ─────────────────────────────────────────────────────────────
        private ExtractionConfig _currentConfig = new ExtractionConfig();
        private List<ExtractedRecord> _lastResults = new List<ExtractedRecord>();
        private readonly string _dbPath;
        private readonly string _modelsDir;
        private ColumnClassifierModel _columnModel;
        private NerModel _nerModel;
        private SectionClassifierModel _sectionModel;
        private ExtractionConfigRepository _configRepo;
        private ConfigWorkflowService _configService;
        private List<(int Id, string Name)> _configItems = new List<(int, string)>();
        private int _currentConfigId = -1;
        private CancellationTokenSource _trainCts;
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;
        private ILogger _trainLogger;
        private readonly ExtractionWorkflowService _extractionService = new ExtractionWorkflowService();
        private readonly TrainingWorkflowService _trainingService = new TrainingWorkflowService();
        private readonly RecommendationService _recommendationService = new RecommendationService();

        public MainForm()
        {
            _dbPath = Path.Combine(Application.StartupPath, "data", "docextractor.db");
            _modelsDir = Path.Combine(Application.StartupPath, "models");
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath));
            Directory.CreateDirectory(_modelsDir);

            _columnModel = new ColumnClassifierModel();
            _nerModel = new NerModel();
            _sectionModel = new SectionClassifierModel();
            TryLoadModels();

            _configRepo = new ExtractionConfigRepository(_dbPath);
            _configService = new ConfigWorkflowService(_configRepo);
            _configService.SeedBuiltInConfigs();

            InitializeComponent();
            InitializeLogging();
            WireEvents();
            LoadConfigList();
            LoadConfigToGrids();
            RefreshTrainingStats();
            OnPresetChanged(null, EventArgs.Empty); // 初始化预设参数状态
            RefreshRecommendCombo();
        }

        private void InitializeLogging()
        {
            string logDir = Path.Combine(Application.StartupPath, "logs");
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddProvider(new UiFileLoggerProvider(WriteStructuredLogToUi, logDir));
            });

            _logger = _loggerFactory.CreateLogger("Pipeline");
            _trainLogger = _loggerFactory.CreateLogger("Training");
            _logger.LogInformation("应用启动：DocExtractor UI 初始化完成");
        }

        private void WriteStructuredLogToUi(string category, string line)
        {
            if (string.Equals(category, "Training", StringComparison.OrdinalIgnoreCase))
                WriteTrainLogToUi(line);
            else
                WriteLogToUi(line);
        }

        // ── 事件绑定 ──────────────────────────────────────────────────────────

        private void WireEvents()
        {
            // Tab 1：数据抽取
            _addFilesBtn.Click += OnAddFiles;
            _removeFileBtn.Click += (s, e) =>
            {
                var toRemove = _fileListBox.SelectedItems.Cast<string>().ToList();
                toRemove.ForEach(f => _fileListBox.Items.Remove(f));
            };
            _clearFilesBtn.Click += (s, e) => _fileListBox.Items.Clear();
            _previewBtn.Click += OnQuickPreview;
            _runBtn.Click += OnRunExtraction;
            _exportBtn.Click += OnExport;

            _fileListBox.DragEnter += (s, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                    e.Effect = DragDropEffects.Copy;
            };
            _fileListBox.DragDrop += (s, e) =>
            {
                var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
                if (files != null) AddFiles(files);
            };

            // 右键：点到未选中项时切换为只选该项，保留已有多选时不变
            _fileListBox.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                int idx = _fileListBox.IndexFromPoint(e.Location);
                if (idx >= 0 && !_fileListBox.GetSelected(idx))
                {
                    for (int i = 0; i < _fileListBox.Items.Count; i++)
                        _fileListBox.SetSelected(i, false);
                    _fileListBox.SetSelected(idx, true);
                }
            };

            // 右键菜单：动态显示选中数量，按情况启/禁
            _fileContextMenu.Opening += (s, e) =>
            {
                int n = _fileListBox.SelectedItems.Count;
                _removeFileMenuItem.Text = n > 0 ? $"移除选中文件（{n} 个）" : "移除选中文件";
                _removeFileMenuItem.Enabled = n > 0;
                _clearAllMenuItem.Enabled = _fileListBox.Items.Count > 0;
            };

            _removeFileMenuItem.Click += (s, e) =>
            {
                var toRemove = _fileListBox.SelectedItems.Cast<string>().ToList();
                toRemove.ForEach(f => _fileListBox.Items.Remove(f));
            };

            _clearAllMenuItem.Click += (s, e) => _fileListBox.Items.Clear();

            // Config combo event is wired in LoadConfigList()

            // Tab 2：字段配置
            _saveConfigBtn.Click += OnSaveConfig;
            _setDefaultBtn.Click += OnSetDefault;
            _newConfigBtn.Click += OnNewConfig;
            _deleteConfigBtn.Click += OnDeleteConfig;
            _importConfigBtn.Click += OnImportConfig;
            _exportConfigBtn.Click += OnExportConfig;

            // Tab 3：拆分规则
            _saveSplitBtn.Click += OnSaveSplitRules;

            // Tab 4：模型训练
            _trainUnifiedBtn.Click += OnTrainUnified;
            _genFromKnowledgeBtn.Click += OnGenerateFromKnowledge;
            _importCsvBtn.Click += OnImportTrainingData;
            _importSectionWordBtn.Click += OnImportSectionFromWord;
            _presetCombo.SelectedIndexChanged += OnPresetChanged;
            _cancelTrainBtn.Click += OnCancelTraining;

            // Tab 1：智能推荐
            _recommendBtn.Click += OnRecommend;
        }

        // ── Tab 1：数据抽取事件 ───────────────────────────────────────────────

        private void OnAddFiles(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Word/Excel 文件|*.docx;*.xlsx;*.xls|Word 文档|*.docx|Excel 表格|*.xlsx;*.xls|所有文件|*.*"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                AddFiles(dlg.FileNames);
        }

        private void AddFiles(string[] files)
        {
            foreach (var f in files)
            {
                if (!_fileListBox.Items.Contains(f))
                    _fileListBox.Items.Add(f);
            }
        }

        private async void OnQuickPreview(object sender, EventArgs e)
        {
            if (_fileListBox.Items.Count == 0)
            {
                MessageHelper.Warn(this, "请先添加要预览的文件");
                return;
            }

            string filePath = _fileListBox.SelectedItem?.ToString()
                              ?? _fileListBox.Items[0]?.ToString()
                              ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            _previewBtn.Enabled = false;
            try
            {
                AppendLog($"开始快速预览：{Path.GetFileName(filePath)}");
                var preview = await Task.Run(() =>
                    _extractionService.Preview(filePath, _currentConfig, _columnModel, _sectionModel));

                if (!preview.Success)
                {
                    MessageHelper.Error(this, $"预览失败：{preview.ErrorMessage}");
                    return;
                }

                AppendLog($"预览完成：{Path.GetFileName(filePath)} | 表格 {preview.Tables.Count} 个");
                foreach (var table in preview.Tables)
                {
                    int mapped = table.Columns.Count(c => !string.IsNullOrWhiteSpace(c.MappedFieldName));
                    int lowConfidence = table.Columns.Count(c => c.IsLowConfidence);
                    AppendLog($"  表格{table.TableIndex + 1} ({table.RowCount}x{table.ColCount}) 匹配列 {mapped}/{table.Columns.Count}，低置信度 {lowConfidence}");
                }

                foreach (var warning in preview.Warnings.Take(10))
                    AppendLog($"  [预览警告] {warning}");

                if (preview.Warnings.Count > 0)
                {
                    MessageHelper.Warn(this,
                        $"预览完成：发现 {preview.Warnings.Count} 个低置信度列，请检查配置或手工修正列映射。");
                }
                else
                {
                    MessageHelper.Success(this, "预览完成：列映射状态良好。");
                }
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"预览失败：{ex.Message}");
            }
            finally
            {
                _previewBtn.Enabled = true;
            }
        }

        private async void OnRunExtraction(object sender, EventArgs e)
        {
            if (_fileListBox.Items.Count == 0)
            {
                MessageHelper.Warn(this, "请先添加要处理的 Word/Excel 文件");
                return;
            }

            _runBtn.Enabled = false;
            _exportBtn.Enabled = false;
            _progressBar.Value = 0;
            _resultGrid.Rows.Clear();
            _resultGrid.Columns.Clear();
            _lastResults.Clear();

            var files = _fileListBox.Items.Cast<string>().ToList();
            var config = _currentConfig;

            var progress = new Progress<PipelineProgress>(p =>
            {
                _progressBar.Value = Math.Min(p.Percent, 100);
                _statusBarLabel.Text = $"[{p.Stage}] {p.Message}";
                AppendLog($"[{p.Stage}] {p.Message}");
            });

            try
            {
                var results = await Task.Run(() =>
                {
                    return _extractionService.ExecuteBatch(
                        files,
                        config,
                        _columnModel,
                        _nerModel,
                        _sectionModel,
                        progress);
                });

                // 显示管道错误（解析异常等）
                foreach (var r in results.Where(r => !r.Success))
                    AppendLog($"[错误] {Path.GetFileName(r.SourceFile)}: {r.ErrorMessage}");

                // 显示警告
                foreach (var r in results.Where(r => r.Warnings.Count > 0))
                    foreach (var w in r.Warnings)
                        AppendLog($"[警告] {Path.GetFileName(r.SourceFile)}: {w}");

                _lastResults = results.SelectMany(r => r.Records).ToList();
                var completeResults = _lastResults.Where(r => r.IsComplete).ToList();
                ShowResults(completeResults, config.Fields);

                int total = _lastResults.Count;
                int complete = completeResults.Count;
                AppendLog($"\n完成！共抽取 {total} 条记录（完整: {complete}，不完整: {total - complete}，列表仅显示完整记录）");
                _statusBarLabel.Text = $"完成 | 完整 {complete}/{total} 条记录";
                _exportBtn.Enabled = complete > 0;

                // 自动学习：将有 GroupName 的完整记录写入知识库
                AutoLearnGroupKnowledge(completeResults);

                if (complete > 0)
                    MessageHelper.Success(this, $"抽取完成，共 {total} 条（完整 {complete} 条已显示）");
                else if (results.Any(r => !r.Success))
                    MessageHelper.Error(this, $"抽取失败：{results.First(r => !r.Success).ErrorMessage}");
                else
                    MessageHelper.Warn(this, "未抽取到数据，请检查配置的字段列名变体是否匹配文档表格列头");
            }
            catch (Exception ex)
            {
                AppendLog($"\n错误: {ex.Message}");
                MessageHelper.Error(this, $"抽取失败：{ex.Message}");
            }
            finally
            {
                _runBtn.Enabled = true;
                _progressBar.Value = 0;
            }
        }

        private void OnExport(object sender, EventArgs e)
        {
            var toExport = _lastResults.Where(r => r.IsComplete).ToList();
            if (toExport.Count == 0) return;

            var selectedFieldNames = ShowExportFieldSelection(_currentConfig.Fields);
            if (selectedFieldNames == null) return;
            if (selectedFieldNames.Count == 0)
            {
                MessageHelper.Warn(this, "请至少选择一个导出字段");
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Filter = "Excel 文件|*.xlsx",
                FileName = $"抽取结果_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var exporter = new ExcelExporter();
                    exporter.Export(toExport, _currentConfig.Fields, dlg.FileName, selectedFieldNames);
                    AppendLog($"已导出到: {dlg.FileName}");
                    MessageHelper.Success(this, "导出成功！");
                }
                catch (Exception ex)
                {
                    MessageHelper.Error(this, $"导出失败：{ex.Message}");
                }
            }
        }

        // ── Tab 2：字段配置事件 ───────────────────────────────────────────────

        private void OnSaveConfig(object sender, EventArgs e)
        {
            SaveFieldsFromGrid();
            SaveGlobalSettings();

            try
            {
                _currentConfigId = _configService.Save(_currentConfig);
                LoadConfigList(_currentConfigId);
                AppendLog($"配置「{_currentConfig.ConfigName}」已保存");
                MessageHelper.Success(this, "配置已保存");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"保存失败：{ex.Message}");
            }
        }

        private void OnSetDefault(object sender, EventArgs e)
        {
            if (_currentConfigId <= 0) return;
            _configService.SetDefaultConfigId(_currentConfigId);
            MessageHelper.Success(this, $"已将「{_currentConfig.ConfigName}」设为默认配置");
        }

        private void OnNewConfig(object sender, EventArgs e)
        {
            string name = ShowInputDialog("新建配置", "请输入新配置名称：", "自定义配置");
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                var config = new ExtractionConfig { ConfigName = name };
                int id = _configService.Save(config);
                LoadConfigList(id);
                MessageHelper.Success(this, $"配置「{name}」已创建");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"创建失败：{ex.Message}");
            }
        }

        private void OnDeleteConfig(object sender, EventArgs e)
        {
            if (_currentConfigId <= 0) return;

            if (BuiltInConfigs.BuiltInNames.Contains(_currentConfig.ConfigName))
            {
                MessageHelper.Warn(this, "内置配置不可删除");
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除配置「{_currentConfig.ConfigName}」吗？此操作不可恢复。",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            try
            {
                _configService.Delete(_currentConfigId);
                LoadConfigList();
                MessageHelper.Success(this, "配置已删除");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"删除失败：{ex.Message}");
            }
        }

        private void OnImportConfig(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Excel 文件|*.xlsx",
                Title = "选择字段配置 Excel 文件"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var config = ConfigImporter.ImportFromExcel(dlg.FileName);

                // 内置配置名不允许覆盖
                if (BuiltInConfigs.BuiltInNames.Contains(config.ConfigName))
                {
                    MessageHelper.Warn(this,
                        $"配置名「{config.ConfigName}」与内置配置冲突，请修改 Excel 中的配置名称后重试");
                    return;
                }

                // 同名配置确认覆盖
                var existing = _configItems.Find(c => c.Name == config.ConfigName);
                if (existing.Id > 0)
                {
                    var result = MessageBox.Show(
                        $"配置「{config.ConfigName}」已存在，是否覆盖？",
                        "确认覆盖", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) return;
                }

                int id = _configService.Save(config);
                LoadConfigList(id);
                MessageHelper.Success(this, $"配置「{config.ConfigName}」导入成功（{config.Fields.Count} 个字段）");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"导入失败：{ex.Message}");
            }
        }

        private void OnExportConfig(object sender, EventArgs e)
        {
            if (_currentConfig == null || _currentConfig.Fields.Count == 0)
            {
                MessageHelper.Warn(this, "当前配置无字段可导出");
                return;
            }

            SaveFieldsFromGrid();
            SaveGlobalSettings();

            using var dlg = new SaveFileDialog
            {
                Filter = "Excel 文件|*.xlsx",
                FileName = $"{_currentConfig.ConfigName}.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                TemplateGenerator.GenerateConfigTemplateWithData(dlg.FileName, _currentConfig);
                MessageHelper.Success(this, "配置已导出");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"导出失败：{ex.Message}");
            }
        }

        private void UpdateConfigTypeBadge()
        {
            if (_currentConfig == null) return;
            bool isBuiltIn = BuiltInConfigs.BuiltInNames.Contains(_currentConfig.ConfigName);
            _configTypeLabel.Text = isBuiltIn ? "内置配置" : "自定义配置";
            _configTypeLabel.BackColor = isBuiltIn
                ? Color.FromArgb(22, 119, 255)
                : Color.FromArgb(82, 196, 26);
            _configTypeLabel.Width = isBuiltIn ? 80 : 90;
            _deleteConfigBtn.Enabled = !isBuiltIn;
        }

        private void SaveFieldsFromGrid()
        {
            _currentConfig.Fields.Clear();
            foreach (DataGridViewRow row in _fieldsGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var fieldName = row.Cells["FieldName"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(fieldName)) continue;

                var f = new FieldDefinition
                {
                    FieldName = fieldName!,
                    DisplayName = row.Cells["DisplayName"].Value?.ToString() ?? fieldName!,
                    IsRequired = row.Cells["IsRequired"].Value is true,
                    DefaultValue = row.Cells["DefaultValue"].Value?.ToString()
                };

                if (Enum.TryParse<FieldDataType>(row.Cells["DataType"].Value?.ToString(), out var dt))
                    f.DataType = dt;

                var variants = row.Cells["Variants"].Value?.ToString() ?? string.Empty;
                f.KnownColumnVariants = new List<string>(
                    variants.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

                _currentConfig.Fields.Add(f);
            }
        }

        private void SaveGlobalSettings()
        {
            _currentConfig.HeaderRowCount = (int)_headerRowsSpinner.Value;
            if (Enum.TryParse<ColumnMatchMode>(_columnMatchCombo.SelectedItem?.ToString(), out var cm))
                _currentConfig.ColumnMatch = cm;
            _currentConfig.EnableValueNormalization = _valueNormalizationCheckBox.Checked;
        }

        // ── Tab 3：拆分规则事件 ───────────────────────────────────────────────

        private void OnSaveSplitRules(object sender, EventArgs e)
        {
            _currentConfig.SplitRules.Clear();
            foreach (DataGridViewRow row in _splitGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var ruleName = row.Cells["RuleName"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(ruleName)) continue;

                var r = new SplitRule
                {
                    RuleName = ruleName!,
                    TriggerColumn = row.Cells["TriggerColumn"].Value?.ToString() ?? string.Empty,
                    GroupByColumn = row.Cells["GroupByColumn"].Value?.ToString() ?? string.Empty,
                    InheritParentFields = row.Cells["InheritParent"].Value is true,
                    IsEnabled = row.Cells["Enabled"].Value is true || row.Cells["Enabled"].Value == null
                };

                if (Enum.TryParse<SplitType>(row.Cells["Type"].Value?.ToString(), out var st))
                    r.Type = st;

                var delimiters = row.Cells["Delimiters"].Value?.ToString() ?? "/;、";
                r.Delimiters = new List<string>(
                    delimiters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

                if (int.TryParse(row.Cells["Priority"].Value?.ToString(), out int pri))
                    r.Priority = pri;

                _currentConfig.SplitRules.Add(r);
            }

            AppendLog("拆分规则已保存");
            MessageHelper.Success(this, "拆分规则已保存");
        }

        // ── Tab 4：训练参数管理 ───────────────────────────────────────────────

        private void OnPresetChanged(object sender, EventArgs e)
        {
            int idx = _presetCombo.SelectedIndex;
            bool isCustom = idx == 3;

            // 启用/禁用参数控件
            _cvFoldsSpinner.Enabled = isCustom;
            _testFractionSpinner.Enabled = isCustom;
            _augmentCheckbox.Enabled = isCustom;
            _colEpochsSpinner.Enabled = isCustom;
            _colBatchSpinner.Enabled = isCustom;
            _nerEpochsSpinner.Enabled = isCustom;
            _nerBatchSpinner.Enabled = isCustom;
            _secTreesSpinner.Enabled = isCustom;
            _secLeavesSpinner.Enabled = isCustom;
            _secMinLeafSpinner.Enabled = isCustom;

            if (isCustom) return;

            // 自动填充预设参数
            TrainingParameters p;
            switch (idx)
            {
                case 0: p = TrainingParameters.Fast(); break;
                case 2: p = TrainingParameters.Fine(); break;
                default: p = TrainingParameters.Standard(); break;
            }
            ApplyPresetToUI(p);
        }

        private void ApplyPresetToUI(TrainingParameters p)
        {
            _cvFoldsSpinner.Value = p.CrossValidationFolds;
            _testFractionSpinner.Value = (decimal)p.TestFraction;
            _augmentCheckbox.Checked = p.EnableAugmentation;
            _colEpochsSpinner.Value = p.ColumnEpochs;
            _colBatchSpinner.Value = p.ColumnBatchSize;
            _nerEpochsSpinner.Value = p.NerEpochs;
            _nerBatchSpinner.Value = p.NerBatchSize;
            _secTreesSpinner.Value = p.SectionTrees;
            _secLeavesSpinner.Value = p.SectionLeaves;
            _secMinLeafSpinner.Value = p.SectionMinLeaf;
        }

        private TrainingParameters BuildTrainingParameters()
        {
            return new TrainingParameters
            {
                CrossValidationFolds = (int)_cvFoldsSpinner.Value,
                TestFraction = (double)_testFractionSpinner.Value,
                EnableAugmentation = _augmentCheckbox.Checked,
                ColumnEpochs = (int)_colEpochsSpinner.Value,
                ColumnBatchSize = (int)_colBatchSpinner.Value,
                NerEpochs = (int)_nerEpochsSpinner.Value,
                NerBatchSize = (int)_nerBatchSpinner.Value,
                SectionTrees = (int)_secTreesSpinner.Value,
                SectionLeaves = (int)_secLeavesSpinner.Value,
                SectionMinLeaf = (int)_secMinLeafSpinner.Value
            };
        }

        private void SetTrainingUIState(bool training)
        {
            _trainUnifiedBtn.Enabled = !training;
            _cancelTrainBtn.Enabled = training;
            _presetCombo.Enabled = !training;
            _trainProgressBar.Style = training ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        }

        private void OnCancelTraining(object sender, EventArgs e)
        {
            _trainCts?.Cancel();
            AppendTrainLog("已请求取消训练...");
        }

        private void ShowTrainingComparison(string modelType, string currentMetrics, int currentSamples)
        {
            try
            {
                using var repo = new TrainingDataRepository(_dbPath);
                var prev = repo.GetLatestRecord(modelType);
                if (prev != null)
                {
                    _evalCompareLabel.Text = $"上次（{prev.TrainedAt}）：样本 {prev.SampleCount} → 当前 {currentSamples} | {prev.MetricsJson}";
                }
                else
                {
                    _evalCompareLabel.Text = "（首次训练，无历史对比）";
                }
            }
            catch
            {
                _evalCompareLabel.Text = "";
            }
        }

        // ── Tab 4：模型训练事件 ───────────────────────────────────────────────

        private void OnImportTrainingData(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "CSV/Excel 文件|*.csv;*.xlsx",
                Title = "选择列名标注数据文件（格式：列名,规范字段名）"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                int imported = _trainingService.ImportColumnSamples(_dbPath, dlg.FileName);

                RefreshTrainingStats();
                AppendTrainLog($"从文件导入 {imported} 条列名标注");
                MessageHelper.Success(this, $"成功导入 {imported} 条标注数据");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"导入失败：{ex.Message}");
            }
        }

        private async void OnTrainUnified(object sender, EventArgs e)
        {
            SetTrainingUIState(true);
            AppendTrainLog("========== 统一模型训练开始 ==========");

            try
            {
                var bundle = _trainingService.LoadTrainingData(_dbPath);
                AppendTrainLog($"数据统计 — 列名:{bundle.ColumnInputs.Count}  NER:{bundle.NerSamples.Count}  章节:{bundle.SectionInputs.Count}");

                var parameters = BuildTrainingParameters();
                _trainCts = new CancellationTokenSource();
                var ct = _trainCts.Token;

                var progress = new Progress<(string Stage, string Detail, double Percent)>(info =>
                {
                    AppendTrainLog($"[{info.Stage}] {info.Detail}");
                    if (info.Percent >= 0 && info.Percent <= 100)
                    {
                        _trainProgressBar.Style = ProgressBarStyle.Continuous;
                        _trainProgressBar.Value = Math.Min(100, (int)info.Percent);
                    }
                });

                var result = await Task.Run(() =>
                    _trainingService.TrainUnified(bundle, _modelsDir, parameters, progress, ct));

                _trainingService.ReloadModels(_modelsDir, _columnModel, _nerModel, _sectionModel);

                _evalLabel.Text = "统一训练完成";
                AppendTrainLog($"\n========== 统一训练完成 ==========\n{result}");

                _trainingService.SaveTrainingHistory(_dbPath, result, bundle, parameters);

                MessageHelper.Success(this, "统一模型训练完成！");
            }
            catch (OperationCanceledException)
            {
                AppendTrainLog("\n统一训练已取消");
                MessageHelper.Warn(this, "训练已取消");
            }
            catch (Exception ex)
            {
                AppendTrainLog($"\n统一训练失败: {ex.Message}");
                MessageHelper.Error(this, $"统一训练失败：{ex.Message}");
            }
            finally
            {
                _trainCts?.Dispose();
                _trainCts = null;
                SetTrainingUIState(false);
                RefreshTrainingStats();
            }
        }

        private void OnImportSectionFromWord(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Word 文档|*.docx",
                Title = "选择用于章节标题标注的 Word 文档（支持多选）",
                Multiselect = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            int imported = 0;

            try
            {
                var importResult = _trainingService.ImportSectionSamples(_dbPath, dlg.FileNames);
                imported = importResult.ImportedCount;
                foreach (var file in importResult.PerFileCounts)
                    AppendTrainLog($"  {file.fileName}：扫描 {file.count} 个段落");

                RefreshTrainingStats();
                AppendTrainLog(
                    $"\n共导入 {imported} 条段落（规则置信度≥0.85自动标记为章节标题，其余为普通段落）\n" +
                    "如需修正标注，可编辑数据库 SectionTrainingData 表的 IsHeading 字段");
                MessageHelper.Success(this,
                    $"已从 {dlg.FileNames.Length} 个文件导入 {imported} 条段落。\n" +
                    "规则置信度≥0.85的段落自动标记为章节标题，其余标记为普通段落。\n" +
                    "积累足够样本（≥20条）后可点击「训练章节标题分类器」。");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"导入失败：{ex.Message}");
            }
        }

        private async void OnGenerateFromKnowledge(object sender, EventArgs e)
        {
            _genFromKnowledgeBtn.Enabled = false;
            _trainProgressBar.Style = ProgressBarStyle.Marquee;
            AppendTrainLog("从知识库生成训练数据...");

            try
            {
                // 收集所有内置配置（提供 KnownColumnVariants）
                var configs = DocExtractor.Core.Models.BuiltInConfigs.GetAll().ToArray();

                var (colAdded, secPosAdded, secNegAdded) = await Task.Run(() =>
                {
                    return _trainingService.GenerateFromKnowledge(_dbPath, configs);
                });

                RefreshTrainingStats();

                string summary =
                    $"列名分类样本  新增 {colAdded} 条\n" +
                    $"章节标题正样本 新增 {secPosAdded} 条（知识库中的组名）\n" +
                    $"章节标题负样本 新增 {secNegAdded} 条（知识库中的细则项名）";

                AppendTrainLog($"\n知识库训练数据生成完成：\n{summary}");

                if (colAdded + secPosAdded + secNegAdded == 0)
                {
                    MessageHelper.Warn(this,
                        "知识库中无新数据可生成（数据已全部存在，或知识库为空）。\n" +
                        "请先通过「开始抽取」生成抽取结果，抽取完成后系统会自动学习知识库。");
                }
                else
                {
                    MessageHelper.Success(this,
                        $"已从知识库自动生成训练数据：\n{summary}\n\n" +
                        "现在可以直接点击训练按钮开始训练！");
                }
            }
            catch (Exception ex)
            {
                AppendTrainLog($"\n生成失败: {ex.Message}");
                MessageHelper.Error(this, $"生成失败：{ex.Message}");
            }
            finally
            {
                _genFromKnowledgeBtn.Enabled = true;
                _trainProgressBar.Style = ProgressBarStyle.Continuous;
            }
        }

        // ── 智能推荐 ────────────────────────────────────────────────────────────

        private void AutoLearnGroupKnowledge(List<ExtractedRecord> completeResults)
        {
            try
            {
                // 只处理含组名的记录
                var withGroup = completeResults
                    .Where(r => r.Fields.ContainsKey("GroupName")
                                && !string.IsNullOrWhiteSpace(r.Fields["GroupName"]))
                    .ToList();

                if (withGroup.Count == 0) return;

                var summary = _recommendationService.AutoLearnGroupKnowledge(_dbPath, withGroup);
                foreach (var detail in summary.FileDetails)
                {
                    string action = detail.WasReplaced ? "更新" : "新录";
                    AppendLog($"知识库{action}：{System.IO.Path.GetFileName(detail.SourceFile)} " +
                              $"→ {detail.GroupCount} 个组 / {detail.InsertedCount} 条细则");
                }

                if (summary.ReplacedFiles > 0)
                    AppendLog($"[去重] {summary.ReplacedFiles} 个文件已存在旧记录，已自动清空后重新录入");

                AppendLog($"知识库合计学习 {summary.TotalGroups} 个组、{summary.TotalInserted} 条记录（共 {summary.FileDetails.Count} 个文件）");

                // 刷新推荐面板的组名下拉列表和知识库计数
                RefreshRecommendCombo();
                RefreshTrainingStats();

                // 自动切换到推荐 Tab 并填入第一个组名
                _resultTabs.SelectedIndex = 1;
                if (_recommendGroupCombo.Items.Count > 0 && _recommendGroupCombo.SelectedIndex < 0)
                    _recommendGroupCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                AppendLog($"[警告] 知识库学习失败: {ex.Message}");
            }
        }

        private void RefreshRecommendCombo()
        {
            try
            {
                var items = _recommendationService.BuildRecommendGroups(_dbPath, _lastResults);
                _recommendGroupCombo.Items.Clear();
                foreach (var item in items)
                    _recommendGroupCombo.Items.Add(item);
            }
            catch { }
        }

        private void OnRecommend(object sender, EventArgs e)
        {
            string groupName = _recommendGroupCombo.Text?.Trim();
            if (string.IsNullOrWhiteSpace(groupName))
            {
                MessageHelper.Warn(this, "请输入或选择一个组名");
                return;
            }

            _recommendGrid.Rows.Clear();

            try
            {
                var response = _recommendationService.Recommend(_dbPath, groupName);
                _recommendCountLabel.Text = $"知识库：{response.KnowledgeCount} 条";

                if (response.Items.Count == 0)
                {
                    _recommendHintLabel.Visible = true;
                    _recommendGrid.Visible = false;
                    return;
                }

                _recommendHintLabel.Visible = false;
                _recommendGrid.Visible = true;

                for (int i = 0; i < response.Items.Count; i++)
                {
                    var item = response.Items[i];
                    int rowIdx = _recommendGrid.Rows.Add(
                        (i + 1).ToString(),
                        item.ItemName,
                        item.TypicalRequiredValue ?? "",
                        $"{item.Confidence:P1}",
                        $"{item.OccurrenceCount} 次",
                        string.Join(", ", item.SourceFiles.Select(f => Path.GetFileName(f)))
                    );

                    // 置信度着色
                    var row = _recommendGrid.Rows[rowIdx];
                    if (item.Confidence >= 0.8f)
                        row.DefaultCellStyle.BackColor = Color.FromArgb(230, 255, 230);     // 绿色
                    else if (item.Confidence >= 0.5f)
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 220);     // 黄色
                    else
                        row.DefaultCellStyle.ForeColor = Color.Gray;                         // 灰色
                }

                AppendLog($"推荐完成：组名「{groupName}」→ {response.Items.Count} 条推荐项");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"推荐失败：{ex.Message}");
            }
        }

        // ── 菜单事件 ──────────────────────────────────────────────────────────

        private void OnOpenTemplateDir()
        {
            string templateDir = Path.Combine(Application.StartupPath, "templates");
            if (Directory.Exists(templateDir))
                System.Diagnostics.Process.Start("explorer.exe", templateDir);
            else
                MessageHelper.Warn(this, "模板目录不存在，将在下次启动时自动创建");
        }

        private void OnRegenerateTemplates()
        {
            try
            {
                string templateDir = Path.Combine(Application.StartupPath, "templates");
                // 删除旧模板
                if (Directory.Exists(templateDir))
                {
                    foreach (var f in Directory.GetFiles(templateDir, "*.xlsx"))
                        File.Delete(f);
                }
                TemplateGenerator.EnsureTemplates(templateDir);
                MessageHelper.Success(this, "模板已重新生成");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"模板生成失败：{ex.Message}");
            }
        }

        // ── 辅助方法 ──────────────────────────────────────────────────────────

        private void ShowResults(List<ExtractedRecord> records, IReadOnlyList<FieldDefinition> fields)
        {
            _resultGrid.Columns.Clear();
            _resultGrid.Rows.Clear();

            _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "_Source", HeaderText = "来源文件" });
            _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "_Complete", HeaderText = "完整" });

            foreach (var f in fields)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = f.FieldName,
                    HeaderText = f.DisplayName.Length > 0 ? f.DisplayName : f.FieldName
                };

                // 组名列：蓝绿色表头背景以区分结构字段
                if (f.FieldName == "GroupName")
                {
                    col.HeaderCell.Style.BackColor = Color.FromArgb(0, 176, 240);
                    col.HeaderCell.Style.ForeColor = Color.White;
                    col.DefaultCellStyle.BackColor = Color.FromArgb(235, 250, 255);
                    col.DefaultCellStyle.Font = new System.Drawing.Font(_resultGrid.Font, System.Drawing.FontStyle.Bold);
                    col.MinimumWidth = 140;
                }

                _resultGrid.Columns.Add(col);
            }

            // 如果有 GroupName 数据但配置里没有该字段，追加兜底列（确保始终可见）
            bool hasGroupNameCol = fields.Any(f => f.FieldName == "GroupName");
            bool hasGroupNameData = records.Any(r => r.Fields.ContainsKey("GroupName") &&
                                                      !string.IsNullOrWhiteSpace(r.Fields["GroupName"]));
            int groupNameColIdx = -1;
            if (!hasGroupNameCol && hasGroupNameData)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = "GroupName",
                    HeaderText = "组名"
                };
                col.HeaderCell.Style.BackColor = Color.FromArgb(0, 176, 240);
                col.HeaderCell.Style.ForeColor = Color.White;
                col.DefaultCellStyle.BackColor = Color.FromArgb(235, 250, 255);
                col.DefaultCellStyle.Font = new System.Drawing.Font(_resultGrid.Font, System.Drawing.FontStyle.Bold);
                col.MinimumWidth = 140;
                _resultGrid.Columns.Insert(2, col);   // 插在最前（来源、完整 后面）
                groupNameColIdx = 2;
            }

            foreach (var r in records)
            {
                var row = new DataGridViewRow();
                row.CreateCells(_resultGrid);

                row.Cells[0].Value = Path.GetFileName(r.SourceFile);
                row.Cells[1].Value = r.IsComplete ? "\u2713" : "\u2717";
                if (!r.IsComplete)
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);

                if (groupNameColIdx >= 0)
                    row.Cells[groupNameColIdx].Value = r.GetField("GroupName");

                int offset = groupNameColIdx >= 0 ? 1 : 0;  // 兜底列已占一格，后移
                for (int i = 0; i < fields.Count; i++)
                    row.Cells[i + 2 + offset].Value = r.GetField(fields[i].FieldName);

                _resultGrid.Rows.Add(row);
            }
        }

        private void AppendLog(string message)
        {
            if (_logger != null)
            {
                _logger.LogInformation(message);
                return;
            }
            WriteLogToUi($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private void AppendTrainLog(string message)
        {
            if (_trainLogger != null)
            {
                _trainLogger.LogInformation(message);
            }
            else
            {
                WriteTrainLogToUi($"[{DateTime.Now:HH:mm:ss}] {message}");
            }
        }

        private void WriteLogToUi(string line)
        {
            if (_logBox.InvokeRequired)
            {
                _logBox.Invoke(new Action<string>(WriteLogToUi), line);
                return;
            }
            _logBox.AppendText(line + Environment.NewLine);
            _logBox.ScrollToCaret();
        }

        private void WriteTrainLogToUi(string line)
        {
            if (_trainLogBox.InvokeRequired)
            {
                _trainLogBox.Invoke(new Action<string>(WriteTrainLogToUi), line);
                return;
            }
            _trainLogBox.AppendText(line + Environment.NewLine);
            _trainLogBox.ScrollToCaret();
        }

        private void LoadConfigList(int selectId = -1)
        {
            _configCombo.SelectedIndexChanged -= OnConfigComboChanged;

            _configItems = _configService.GetAll();
            _configCombo.Items.Clear();
            foreach (var item in _configItems)
                _configCombo.Items.Add(item.Name);

            if (_configItems.Count == 0) return;

            // 确定选中项
            int targetId = selectId > 0 ? selectId : _configService.GetDefaultConfigId();
            int selectedIndex = 0;
            if (targetId > 0)
            {
                for (int i = 0; i < _configItems.Count; i++)
                {
                    if (_configItems[i].Id == targetId)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            _configCombo.SelectedIndex = selectedIndex;
            _configCombo.SelectedIndexChanged += OnConfigComboChanged;
            LoadSelectedConfig();
        }

        private void OnConfigComboChanged(object sender, EventArgs e)
        {
            LoadSelectedConfig();
        }

        private void LoadSelectedConfig()
        {
            int idx = _configCombo.SelectedIndex;
            if (idx < 0 || idx >= _configItems.Count) return;

            _currentConfigId = _configItems[idx].Id;
            var config = _configService.GetById(_currentConfigId);
            if (config != null)
            {
                _currentConfig = config;
                LoadConfigToGrids();
                UpdateConfigTypeBadge();
            }
        }

        private void LoadConfigToGrids()
        {
            // 字段定义 → Grid
            _fieldsGrid.Rows.Clear();
            foreach (var f in _currentConfig.Fields)
            {
                _fieldsGrid.Rows.Add(
                    f.FieldName,
                    f.DisplayName,
                    f.DataType.ToString(),
                    f.IsRequired,
                    f.DefaultValue ?? string.Empty,
                    string.Join(",", f.KnownColumnVariants));
            }

            // 拆分规则 → Grid
            _splitGrid.Rows.Clear();
            foreach (var r in _currentConfig.SplitRules)
            {
                _splitGrid.Rows.Add(
                    r.RuleName,
                    r.Type.ToString(),
                    r.TriggerColumn,
                    string.Join(",", r.Delimiters),
                    r.GroupByColumn,
                    r.InheritParentFields,
                    r.Priority.ToString(),
                    r.IsEnabled);
            }

            // 全局设置
            _headerRowsSpinner.Value = _currentConfig.HeaderRowCount;
            var matchItem = _columnMatchCombo.Items.Cast<string>()
                .FirstOrDefault(x => x == _currentConfig.ColumnMatch.ToString());
            if (matchItem != null)
                _columnMatchCombo.SelectedItem = matchItem;
            _valueNormalizationCheckBox.Checked = _currentConfig.EnableValueNormalization;
        }

        private void RefreshTrainingStats()
        {
            try
            {
                using var repo = new TrainingDataRepository(_dbPath);
                _colSampleCountLabel.Text = $"列名分类样本：{repo.GetColumnSampleCount()} 条";
                _nerSampleCountLabel.Text = $"NER 标注样本：{repo.GetNerSampleCount()} 条";
                _sectionSampleCountLabel.Text = $"章节标题样本：{repo.GetSectionSampleCount()} 条";
            }
            catch { }

            try
            {
                using var kRepo = new GroupKnowledgeRepository(_dbPath);
                int count = kRepo.GetKnowledgeCount();
                _knowledgeCountLabel.Text = $"推荐知识库：{count} 条";
                _recommendCountLabel.Text = $"知识库：{count} 条";
            }
            catch { }
        }

        private void TryLoadModels()
        {
            string colModelPath = Path.Combine(_modelsDir, "column_classifier.zip");
            string nerModelPath = Path.Combine(_modelsDir, "ner_model.zip");
            string sectionModelPath = Path.Combine(_modelsDir, "section_classifier.zip");

            try { if (File.Exists(colModelPath)) _columnModel.Load(colModelPath); }
            catch { }
            try { if (File.Exists(nerModelPath)) _nerModel.Load(nerModelPath); }
            catch { }
            try { if (File.Exists(sectionModelPath)) _sectionModel.Load(sectionModelPath); }
            catch { }
        }

        private static string ShowInputDialog(string title, string prompt, string defaultValue)
        {
            using var form = new Form
            {
                Text = title, Width = 400, Height = 160,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false, MinimizeBox = false
            };
            var label = new Label { Text = prompt, Left = 12, Top = 16, Width = 360 };
            var textBox = new TextBox { Text = defaultValue, Left = 12, Top = 42, Width = 360 };
            var okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 210, Top = 78, Width = 75 };
            var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 295, Top = 78, Width = 75 };
            form.Controls.AddRange(new Control[] { label, textBox, okBtn, cancelBtn });
            form.AcceptButton = okBtn;
            form.CancelButton = cancelBtn;
            return form.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : string.Empty;
        }

        private List<string>? ShowExportFieldSelection(IReadOnlyList<FieldDefinition> fields)
        {
            using var form = new Form
            {
                Text = "选择导出字段",
                Width = 420,
                Height = 520,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var hint = new Label
            {
                Text = "勾选需要导出的字段（默认全选）",
                Left = 12,
                Top = 12,
                Width = 380
            };

            var checkedList = new CheckedListBox
            {
                Left = 12,
                Top = 36,
                Width = 380,
                Height = 380,
                CheckOnClick = true
            };

            foreach (var field in fields)
            {
                string display = string.IsNullOrWhiteSpace(field.DisplayName)
                    ? field.FieldName
                    : $"{field.DisplayName} ({field.FieldName})";
                checkedList.Items.Add(display, true);
            }

            var selectAllBtn = new Button { Text = "全选", Left = 12, Top = 428, Width = 80 };
            var clearAllBtn = new Button { Text = "全不选", Left = 100, Top = 428, Width = 80 };
            var okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 236, Top = 460, Width = 75 };
            var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 317, Top = 460, Width = 75 };

            selectAllBtn.Click += (_, _) =>
            {
                for (int i = 0; i < checkedList.Items.Count; i++)
                    checkedList.SetItemChecked(i, true);
            };
            clearAllBtn.Click += (_, _) =>
            {
                for (int i = 0; i < checkedList.Items.Count; i++)
                    checkedList.SetItemChecked(i, false);
            };

            form.Controls.Add(hint);
            form.Controls.Add(checkedList);
            form.Controls.Add(selectAllBtn);
            form.Controls.Add(clearAllBtn);
            form.Controls.Add(okBtn);
            form.Controls.Add(cancelBtn);
            form.AcceptButton = okBtn;
            form.CancelButton = cancelBtn;

            if (form.ShowDialog() != DialogResult.OK)
                return null;

            var selected = new List<string>();
            for (int i = 0; i < checkedList.Items.Count; i++)
            {
                if (checkedList.GetItemChecked(i))
                    selected.Add(fields[i].FieldName);
            }

            return selected;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _loggerFactory?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
