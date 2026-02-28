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
using DocExtractor.ML.SectionClassifier;
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
        private ExtractionConfig _currentConfig = new ExtractionConfig();
        private List<ExtractedRecord> _lastResults = new List<ExtractedRecord>();
        private readonly string _dbPath;
        private readonly string _modelsDir;
        private ColumnClassifierModel _columnModel;
        private NerModel _nerModel;
        private SectionClassifierModel _sectionModel;
        private ExtractionConfigRepository _configRepo;
        private List<(int Id, string Name)> _configItems = new List<(int, string)>();
        private int _currentConfigId = -1;

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
            _configRepo.SeedBuiltInConfigs();

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

            // Config combo event is wired in LoadConfigList()

            // Tab 2：字段配置
            _saveConfigBtn.Click += OnSaveConfig;
            _setDefaultBtn.Click += OnSetDefault;
            _newConfigBtn.Click += OnNewConfig;
            _deleteConfigBtn.Click += OnDeleteConfig;

            // Tab 3：拆分规则
            _saveSplitBtn.Click += OnSaveSplitRules;

            // Tab 4：模型训练
            _trainColumnBtn.Click += OnTrainColumnClassifier;
            _trainNerBtn.Click += OnTrainNer;
            _trainSectionBtn.Click += OnTrainSectionClassifier;
            _importCsvBtn.Click += OnImportTrainingData;
            _importSectionWordBtn.Click += OnImportSectionFromWord;
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
                    // 构建混合章节标题检测器（规则 + ML 三层级联）
                    var ruleDetector = new SectionHeadingDetector();
                    var hybridHeadingDetector = new HybridSectionHeadingDetector(ruleDetector, _sectionModel);
                    var parsers = new IDocumentParser[]
                    {
                        new WordDocumentParser(hybridHeadingDetector),
                        new ExcelDocumentParser(config.HeaderRowCount, config.TargetSheets)
                    };
                    var pipeline = new ExtractionPipeline(parsers, normalizer, _nerModel);
                    return pipeline.ExecuteBatch(files, config, progress);
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
                    exporter.Export(toExport, _currentConfig.Fields, dlg.FileName);
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
                _currentConfigId = _configRepo.Save(_currentConfig);
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
            _configRepo.SetDefaultConfigId(_currentConfigId);
            MessageHelper.Success(this, $"已将「{_currentConfig.ConfigName}」设为默认配置");
        }

        private void OnNewConfig(object sender, EventArgs e)
        {
            string name = ShowInputDialog("新建配置", "请输入新配置名称：", "自定义配置");
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                var config = new ExtractionConfig { ConfigName = name };
                int id = _configRepo.Save(config);
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
                _configRepo.Delete(_currentConfigId);
                LoadConfigList();
                MessageHelper.Success(this, "配置已删除");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"删除失败：{ex.Message}");
            }
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

        private async void OnTrainSectionClassifier(object sender, EventArgs e)
        {
            _trainSectionBtn.Enabled = false;
            _trainProgressBar.Style = ProgressBarStyle.Marquee;
            AppendTrainLog("开始训练章节标题分类器...");

            try
            {
                List<DocExtractor.Data.Repositories.SectionAnnotation> samples;
                using (var repo = new TrainingDataRepository(_dbPath))
                    samples = repo.GetSectionSamples();

                if (samples.Count < 20)
                {
                    MessageHelper.Warn(this,
                        $"章节标题样本不足（当前 {samples.Count} 条，至少需要 20 条）。\n" +
                        "请先通过「从 Word 导入章节标注」按钮导入标注数据。");
                    return;
                }

                var inputs = samples.ConvertAll(s => new SectionInput
                {
                    Text = s.ParagraphText,
                    IsBold = s.IsBold ? 1f : 0f,
                    FontSize = s.FontSize,
                    HasNumberPrefix = s.ParagraphText.Length > 0 && char.IsDigit(s.ParagraphText[0]) ? 1f : 0f,
                    TextLength = s.ParagraphText.Length,
                    HasHeadingStyle = s.HasHeadingStyle ? 1f : 0f,
                    Position = 0f,   // 导入时不保留位置，训练时设为0
                    IsHeading = s.IsHeading
                });

                var progress = new Progress<string>(msg => AppendTrainLog(msg));
                var trainer = new SectionClassifierTrainer();
                string modelPath = Path.Combine(_modelsDir, "section_classifier.zip");

                var eval = await Task.Run(() => trainer.Train(inputs, modelPath, progress));

                _sectionModel.Reload(modelPath);
                _evalLabel.Text = $"章节标题分类器：{eval}";
                AppendTrainLog($"\n章节标题分类器训练完成！\n{eval}");
                MessageHelper.Success(this, "章节标题分类器训练完成！");
            }
            catch (Exception ex)
            {
                AppendTrainLog($"\n训练失败: {ex.Message}");
                MessageHelper.Error(this, $"训练失败：{ex.Message}");
            }
            finally
            {
                _trainSectionBtn.Enabled = true;
                _trainProgressBar.Style = ProgressBarStyle.Continuous;
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
                var scanner = new WordParagraphScanner();
                using var repo = new TrainingDataRepository(_dbPath);

                foreach (var filePath in dlg.FileNames)
                {
                    var paragraphs = scanner.Scan(filePath);

                    foreach (var p in paragraphs)
                    {
                        repo.AddSectionSample(
                            p.Text,
                            p.AutoIsHeading,
                            p.IsBold,
                            p.FontSize,
                            p.HasHeadingStyle,
                            p.OutlineLevel,
                            filePath);
                        imported++;
                    }

                    AppendTrainLog($"  {Path.GetFileName(filePath)}：扫描 {paragraphs.Count} 个段落");
                }

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

        private void LoadConfigList(int selectId = -1)
        {
            _configCombo.SelectedIndexChanged -= OnConfigComboChanged;

            _configItems = _configRepo.GetAll();
            _configCombo.Items.Clear();
            foreach (var item in _configItems)
                _configCombo.Items.Add(item.Name);

            if (_configItems.Count == 0) return;

            // 确定选中项
            int targetId = selectId > 0 ? selectId : _configRepo.GetDefaultConfigId();
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
            var config = _configRepo.GetById(_currentConfigId);
            if (config != null)
            {
                _currentConfig = config;
                LoadConfigToGrids();
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
                _sectionSampleCountLabel.Text = $"章节标题样本：{repo.GetSectionSampleCount()} 条";
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
    }
}
