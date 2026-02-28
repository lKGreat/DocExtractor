using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Core.Pipeline;
using DocExtractor.Data.Export;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.ColumnClassifier;
using DocExtractor.ML.EntityExtractor;
using DocExtractor.ML.Inference;
using DocExtractor.Parsing.Excel;
using DocExtractor.Parsing.Word;
using DocExtractor.UI.Helpers;

namespace DocExtractor.UI.Forms
{
    /// <summary>
    /// 主窗口：单窗口 Tabs 架构，集成数据抽取、字段配置、拆分规则、模型训练
    /// </summary>
    public partial class MainForm : Form
    {
        // ── 状态 ─────────────────────────────────────────────────────────────
        private ExtractionConfig _currentConfig = CreateDefaultConfig();
        private List<ExtractedRecord> _lastResults = new List<ExtractedRecord>();
        private readonly string _dbPath;
        private readonly string _modelsDir;
        private ColumnClassifierModel _columnModel;
        private NerModel _nerModel;

        public MainForm()
        {
            _dbPath = Path.Combine(Application.StartupPath, "data", "docextractor.db");
            _modelsDir = Path.Combine(Application.StartupPath, "models");
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath));
            Directory.CreateDirectory(_modelsDir);

            _columnModel = new ColumnClassifierModel();
            _nerModel = new NerModel();
            TryLoadModels();

            InitializeComponent();
            WireEvents();
            LoadConfigList();
            LoadConfigToGrids();
            RefreshTrainingStats();
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

            _configCombo.SelectedIndexChanged += (s, e) => LoadSelectedConfig();

            // Tab 2：字段配置
            _saveConfigBtn.Click += OnSaveConfig;

            // Tab 3：拆分规则
            _saveSplitBtn.Click += OnSaveSplitRules;

            // Tab 4：模型训练
            _trainColumnBtn.Click += OnTrainColumnClassifier;
            _trainNerBtn.Click += OnTrainNer;
            _importCsvBtn.Click += OnImportTrainingData;
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
                    var normalizer = new HybridColumnNormalizer(_columnModel, config.ColumnMatch);
                    var parsers = new IDocumentParser[]
                    {
                        new WordDocumentParser(),
                        new ExcelDocumentParser(config.HeaderRowCount, config.TargetSheets)
                    };
                    var pipeline = new ExtractionPipeline(parsers, normalizer, _nerModel);
                    return pipeline.ExecuteBatch(files, config, progress);
                });

                _lastResults = results.SelectMany(r => r.Records).ToList();
                ShowResults(_lastResults, config.Fields);

                int total = _lastResults.Count;
                int complete = _lastResults.Count(r => r.IsComplete);
                AppendLog($"\n完成！共抽取 {total} 条记录（完整: {complete}，不完整: {total - complete}）");
                _statusBarLabel.Text = $"完成 | {total} 条记录";
                _exportBtn.Enabled = total > 0;

                // 显示警告
                foreach (var r in results.Where(r => r.Warnings.Count > 0))
                    foreach (var w in r.Warnings)
                        AppendLog($"[警告] {Path.GetFileName(r.SourceFile)}: {w}");

                MessageHelper.Success(this, $"抽取完成，共 {total} 条记录");
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
            if (_lastResults.Count == 0) return;

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
                    exporter.Export(_lastResults, _currentConfig.Fields, dlg.FileName);
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
            AppendLog("字段配置已保存");
            MessageHelper.Success(this, "字段配置已保存");
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
                    IsRequired = row.Cells["IsRequired"].Value is true
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

        // ── Tab 4：模型训练事件 ───────────────────────────────────────────────

        private async void OnTrainColumnClassifier(object sender, EventArgs e)
        {
            _trainColumnBtn.Enabled = false;
            _trainProgressBar.Style = ProgressBarStyle.Marquee;
            _trainLogBox.Clear();

            try
            {
                List<(string ColumnText, string FieldName)> samples;
                using (var repo = new TrainingDataRepository(_dbPath))
                    samples = repo.GetColumnSamples();

                if (samples.Count < 10)
                {
                    MessageHelper.Warn(this,
                        $"列名分类样本不足（当前 {samples.Count} 条，至少需要 10 条），请先导入训练数据");
                    return;
                }

                var inputs = samples.ConvertAll(s => new ColumnInput
                {
                    ColumnText = s.ColumnText,
                    Label = s.FieldName
                });

                var progress = new Progress<string>(msg => AppendTrainLog(msg));
                var trainer = new ColumnClassifierTrainer();
                string modelPath = Path.Combine(_modelsDir, "column_classifier.zip");

                var eval = await Task.Run(() => trainer.Train(inputs, modelPath, progress));

                _columnModel.Reload(modelPath);
                _evalLabel.Text = $"列名分类器：{eval}";
                AppendTrainLog($"\n训练完成！{eval}");
                MessageHelper.Success(this, "列名分类器训练完成");
            }
            catch (Exception ex)
            {
                AppendTrainLog($"\n训练失败: {ex.Message}");
                MessageHelper.Error(this, $"训练失败：{ex.Message}");
            }
            finally
            {
                _trainColumnBtn.Enabled = true;
                _trainProgressBar.Style = ProgressBarStyle.Continuous;
                RefreshTrainingStats();
            }
        }

        private async void OnTrainNer(object sender, EventArgs e)
        {
            _trainNerBtn.Enabled = false;
            _trainProgressBar.Style = ProgressBarStyle.Marquee;

            try
            {
                List<NerAnnotation> samples;
                using (var repo = new TrainingDataRepository(_dbPath))
                    samples = repo.GetNerSamples();

                if (samples.Count < 20)
                {
                    MessageHelper.Warn(this,
                        $"NER 样本不足（当前 {samples.Count} 条，至少需要 20 条）");
                    return;
                }

                var progress = new Progress<string>(msg => AppendTrainLog(msg));
                var trainer = new NerTrainer();
                string modelPath = Path.Combine(_modelsDir, "ner_model.zip");

                var eval = await Task.Run(() => trainer.Train(samples, modelPath, progress));
                _nerModel.Load(modelPath);

                _evalLabel.Text = $"NER 模型：{eval}";
                AppendTrainLog($"\nNER 训练完成！{eval}");
                MessageHelper.Success(this, "NER 模型训练完成");
            }
            catch (Exception ex)
            {
                AppendTrainLog($"\nNER 训练失败: {ex.Message}");
                MessageHelper.Error(this, $"NER 训练失败：{ex.Message}");
            }
            finally
            {
                _trainNerBtn.Enabled = true;
                _trainProgressBar.Style = ProgressBarStyle.Continuous;
                RefreshTrainingStats();
            }
        }

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
                int imported = 0;
                using var repo = new TrainingDataRepository(_dbPath);

                foreach (var line in File.ReadAllLines(dlg.FileName, System.Text.Encoding.UTF8))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        repo.AddColumnSample(parts[0].Trim(), parts[1].Trim(), dlg.FileName);
                        imported++;
                    }
                }

                RefreshTrainingStats();
                AppendTrainLog($"从文件导入 {imported} 条列名标注");
                MessageHelper.Success(this, $"成功导入 {imported} 条标注数据");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"导入失败：{ex.Message}");
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
                _resultGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = f.FieldName,
                    HeaderText = f.DisplayName.Length > 0 ? f.DisplayName : f.FieldName
                });
            }

            foreach (var r in records)
            {
                var row = new DataGridViewRow();
                row.CreateCells(_resultGrid);

                row.Cells[0].Value = Path.GetFileName(r.SourceFile);
                row.Cells[1].Value = r.IsComplete ? "\u2713" : "\u2717";
                if (!r.IsComplete)
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);

                for (int i = 0; i < fields.Count; i++)
                    row.Cells[i + 2].Value = r.GetField(fields[i].FieldName);

                _resultGrid.Rows.Add(row);
            }
        }

        private void AppendLog(string message)
        {
            if (_logBox.InvokeRequired)
            {
                _logBox.Invoke(new Action(() => AppendLog(message)));
                return;
            }
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            _logBox.ScrollToCaret();
        }

        private void AppendTrainLog(string message)
        {
            if (_trainLogBox.InvokeRequired)
            {
                _trainLogBox.Invoke(new Action(() => AppendTrainLog(message)));
                return;
            }
            _trainLogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            _trainLogBox.ScrollToCaret();
        }

        private void LoadConfigList()
        {
            _configCombo.Items.Clear();
            _configCombo.Items.Add("遥测/遥控配置");
            _configCombo.Items.Add("通用表格模式");
            _configCombo.SelectedIndex = 0;
        }

        private void LoadSelectedConfig()
        {
            if (_configCombo.SelectedIndex == 0)
                _currentConfig = CreateTelemetryConfig();
            else
                _currentConfig = CreateDefaultConfig();

            LoadConfigToGrids();
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
        }

        private void RefreshTrainingStats()
        {
            try
            {
                using var repo = new TrainingDataRepository(_dbPath);
                _colSampleCountLabel.Text = $"列名分类样本：{repo.GetColumnSampleCount()} 条";
                _nerSampleCountLabel.Text = $"NER 标注样本：{repo.GetNerSampleCount()} 条";
            }
            catch { }
        }

        private void TryLoadModels()
        {
            string colModelPath = Path.Combine(_modelsDir, "column_classifier.zip");
            string nerModelPath = Path.Combine(_modelsDir, "ner_model.zip");

            try { if (File.Exists(colModelPath)) _columnModel.Load(colModelPath); }
            catch { }
            try { if (File.Exists(nerModelPath)) _nerModel.Load(nerModelPath); }
            catch { }
        }

        // ── 内置配置模板 ──────────────────────────────────────────────────────

        private static ExtractionConfig CreateDefaultConfig() => new ExtractionConfig
        {
            ConfigName = "默认配置",
            Fields = new List<FieldDefinition>(),
            HeaderRowCount = 1
        };

        private static ExtractionConfig CreateTelemetryConfig() => new ExtractionConfig
        {
            ConfigName = "遥测解析配置",
            HeaderRowCount = 1,
            ColumnMatch = ColumnMatchMode.HybridMlFirst,
            Fields = new List<FieldDefinition>
            {
                new FieldDefinition { FieldName = "Index", DisplayName = "序号",
                    KnownColumnVariants = new List<string> { "序号", "No.", "编号" } },
                new FieldDefinition { FieldName = "System", DisplayName = "所属系统",
                    KnownColumnVariants = new List<string> { "所属系统", "系统", "System" } },
                new FieldDefinition { FieldName = "APID", DisplayName = "APID值", DataType = FieldDataType.HexCode,
                    KnownColumnVariants = new List<string> { "APID值", "APID", "应用标识" }, IsRequired = true },
                new FieldDefinition { FieldName = "StartByte", DisplayName = "起始字节", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "起始字节", "起始字节序号", "开始字节" }, IsRequired = true },
                new FieldDefinition { FieldName = "BitOffset", DisplayName = "起始位",
                    KnownColumnVariants = new List<string> { "起始位", "起始比特" } },
                new FieldDefinition { FieldName = "BitLength", DisplayName = "位长度", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "位长度", "字节长度", "比特数", "长度" }, IsRequired = true },
                new FieldDefinition { FieldName = "ChannelName", DisplayName = "波道名称",
                    KnownColumnVariants = new List<string> { "波道名称", "参数名称", "通道名称", "名称" }, IsRequired = true },
                new FieldDefinition { FieldName = "TelemetryCode", DisplayName = "遥测代号",
                    KnownColumnVariants = new List<string> { "遥测代号", "参数代号", "代号", "标识" }, IsRequired = true },
                new FieldDefinition { FieldName = "Endianness", DisplayName = "字节端序",
                    KnownColumnVariants = new List<string> { "字节端序", "端序", "大小端" } },
                new FieldDefinition { FieldName = "FormulaType", DisplayName = "公式类型",
                    KnownColumnVariants = new List<string> { "公式类型", "转换类型", "类型" } },
                new FieldDefinition { FieldName = "CoeffA", DisplayName = "系数A", DataType = FieldDataType.Decimal,
                    KnownColumnVariants = new List<string> { "A", "系数A", "公式系数/A" } },
                new FieldDefinition { FieldName = "CoeffB", DisplayName = "系数B", DataType = FieldDataType.Decimal,
                    KnownColumnVariants = new List<string> { "B", "系数B", "公式系数/B" } },
                new FieldDefinition { FieldName = "Precision", DisplayName = "小数位数", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "小数位数", "精度", "小数位" } },
                new FieldDefinition { FieldName = "Unit", DisplayName = "量纲",
                    KnownColumnVariants = new List<string> { "量纲", "单位", "工程量纲" } },
                new FieldDefinition { FieldName = "EnumMap", DisplayName = "枚举解译",
                    KnownColumnVariants = new List<string> { "枚举解译", "离散值", "枚举值", "状态描述" },
                    DataType = FieldDataType.Enumeration }
            },
            SplitRules = new List<SplitRule>
            {
                new SplitRule
                {
                    RuleName = "枚举值展开",
                    Type = SplitType.SubTableExpand,
                    TriggerColumn = "EnumMap",
                    InheritParentFields = true,
                    Priority = 10
                }
            }
        };
    }
}
