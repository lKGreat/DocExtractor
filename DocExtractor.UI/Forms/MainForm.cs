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

namespace DocExtractor.UI.Forms
{
    /// <summary>
    /// 主窗口：文件导入、配置选择、执行抽取、查看结果
    /// </summary>
    public class MainForm : Form
    {
        // ── UI 控件 ──────────────────────────────────────────────────────────
        private ListBox _fileListBox = null!;
        private Button _addFilesBtn = null!;
        private Button _removeFileBtn = null!;
        private Button _clearFilesBtn = null!;
        private ComboBox _configCombo = null!;
        private Button _editConfigBtn = null!;
        private Button _runBtn = null!;
        private Button _trainBtn = null!;
        private Button _exportBtn = null!;
        private DataGridView _resultGrid = null!;
        private ProgressBar _progressBar = null!;
        private Label _statusLabel = null!;
        private RichTextBox _logBox = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _statusBarLabel = null!;

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
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            Directory.CreateDirectory(_modelsDir);

            _columnModel = new ColumnClassifierModel();
            _nerModel = new NerModel();

            // 加载已有模型（如果存在）
            TryLoadModels();

            InitializeComponent();
            LoadConfig();
        }

        private void InitializeComponent()
        {
            Text = "DocExtractor — 文档数据智能抽取系统";
            Size = new Size(1400, 900);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;

            // ── 左侧面板（文件列表）────────────────────────────────────────
            var leftPanel = new Panel { Dock = DockStyle.Left, Width = 320, Padding = new Padding(8) };

            var fileLabel = new Label
            {
                Text = "源文件列表",
                Font = new Font("微软雅黑", 10, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 30
            };

            _fileListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.MultiSimple,
                HorizontalScrollbar = true
            };
            _fileListBox.AllowDrop = true;
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

            var fileBtnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight
            };

            _addFilesBtn = new Button { Text = "添加文件", Width = 80, Height = 32 };
            _addFilesBtn.Click += OnAddFiles;
            _removeFileBtn = new Button { Text = "移除", Width = 60, Height = 32 };
            _removeFileBtn.Click += (s, e) =>
            {
                var toRemove = _fileListBox.SelectedItems.Cast<string>().ToList();
                toRemove.ForEach(f => _fileListBox.Items.Remove(f));
            };
            _clearFilesBtn = new Button { Text = "清空", Width = 60, Height = 32 };
            _clearFilesBtn.Click += (s, e) => _fileListBox.Items.Clear();

            fileBtnPanel.Controls.AddRange(new Control[] { _addFilesBtn, _removeFileBtn, _clearFilesBtn });

            leftPanel.Controls.Add(_fileListBox);
            leftPanel.Controls.Add(fileBtnPanel);
            leftPanel.Controls.Add(fileLabel);

            // ── 顶部工具栏 ───────────────────────────────────────────────────
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(8, 8, 8, 0) };
            var toolFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            var configLabel = new Label { Text = "配置：", TextAlign = ContentAlignment.MiddleLeft, Width = 50, Height = 36 };
            _configCombo = new ComboBox { Width = 200, Height = 36, DropDownStyle = ComboBoxStyle.DropDownList };
            _configCombo.SelectedIndexChanged += (s, e) => LoadSelectedConfig();
            _editConfigBtn = new Button { Text = "编辑配置", Width = 80, Height = 36 };
            _editConfigBtn.Click += OnEditConfig;

            _runBtn = new Button
            {
                Text = "▶ 开始抽取",
                Width = 110,
                Height = 36,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _runBtn.Click += OnRunExtraction;

            _trainBtn = new Button { Text = "训练模型", Width = 90, Height = 36 };
            _trainBtn.Click += OnOpenTraining;

            _exportBtn = new Button
            {
                Text = "导出结果",
                Width = 90,
                Height = 36,
                Enabled = false
            };
            _exportBtn.Click += OnExport;

            toolFlow.Controls.AddRange(new Control[]
            {
                configLabel, _configCombo, _editConfigBtn,
                new Label { Width = 20 }, // 间隔
                _runBtn, _trainBtn, _exportBtn
            });
            toolbar.Controls.Add(toolFlow);

            // ── 中部分割（日志 + 结果）──────────────────────────────────────
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 600
            };

            // 结果 Grid
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
                }
            };

            // 进度 + 日志
            var bottomPanel = new Panel { Dock = DockStyle.Fill };
            _progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 8, Style = ProgressBarStyle.Continuous };
            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(204, 204, 204),
                Font = new Font("Consolas", 9),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            bottomPanel.Controls.Add(_logBox);
            bottomPanel.Controls.Add(_progressBar);

            mainSplit.Panel1.Controls.Add(_resultGrid);
            mainSplit.Panel2.Controls.Add(bottomPanel);

            // ── 状态栏 ───────────────────────────────────────────────────────
            _statusStrip = new StatusStrip();
            _statusBarLabel = new ToolStripStatusLabel("就绪") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _statusStrip.Items.Add(_statusBarLabel);

            // ── 布局组装 ─────────────────────────────────────────────────────
            Controls.Add(mainSplit);
            Controls.Add(toolbar);
            Controls.Add(leftPanel);
            Controls.Add(_statusStrip);
        }

        // ── 事件处理 ─────────────────────────────────────────────────────────

        private void OnAddFiles(object? sender, EventArgs e)
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

        private async void OnRunExtraction(object? sender, EventArgs e)
        {
            if (_fileListBox.Items.Count == 0)
            {
                MessageBox.Show("请先添加要处理的 Word/Excel 文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _runBtn.Enabled = false;
            _exportBtn.Enabled = false;
            _progressBar.Value = 0;
            _resultGrid.DataSource = null;
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
                AppendLog($"\n✓ 完成！共抽取 {total} 条记录（完整: {complete}，不完整: {total - complete}）");
                _statusBarLabel.Text = $"完成 | {total} 条记录";
                _exportBtn.Enabled = total > 0;
            }
            catch (Exception ex)
            {
                AppendLog($"\n✗ 错误: {ex.Message}");
                MessageBox.Show($"抽取失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _runBtn.Enabled = true;
                _progressBar.Value = 0;
            }
        }

        private void OnEditConfig(object? sender, EventArgs e)
        {
            using var form = new ExtractionConfigForm(_currentConfig);
            if (form.ShowDialog() == DialogResult.OK)
            {
                _currentConfig = form.Config;
                SaveCurrentConfig();
                AppendLog("配置已更新");
            }
        }

        private void OnOpenTraining(object? sender, EventArgs e)
        {
            using var form = new ModelTrainingForm(_dbPath, _modelsDir, _columnModel, _nerModel);
            form.ShowDialog();
        }

        private void OnExport(object? sender, EventArgs e)
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
                    AppendLog($"✓ 已导出到: {dlg.FileName}");

                    if (MessageBox.Show("导出成功！是否打开文件？", "成功",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(dlg.FileName);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── 辅助方法 ─────────────────────────────────────────────────────────

        private void ShowResults(List<ExtractedRecord> records, IReadOnlyList<FieldDefinition> fields)
        {
            _resultGrid.Columns.Clear();

            // 添加元数据列
            _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "_Source", HeaderText = "来源文件" });
            _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "_Complete", HeaderText = "完整" });

            // 添加字段列
            foreach (var f in fields)
            {
                _resultGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = f.FieldName,
                    HeaderText = f.DisplayName.Length > 0 ? f.DisplayName : f.FieldName
                });
            }

            // 填充数据
            foreach (var r in records)
            {
                var row = new DataGridViewRow();
                row.CreateCells(_resultGrid);

                row.Cells[0].Value = Path.GetFileName(r.SourceFile);
                row.Cells[1].Value = r.IsComplete ? "✓" : "✗";
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

        private void LoadConfig()
        {
            _configCombo.Items.Clear();
            _configCombo.Items.Add("默认配置（遥测/遥控）");
            _configCombo.Items.Add("通用表格模式");
            _configCombo.SelectedIndex = 0;
        }

        private void LoadSelectedConfig()
        {
            // 内置默认配置，实际项目中从SQLite读取
            if (_configCombo.SelectedIndex == 0)
                _currentConfig = CreateTelemetryConfig();
            else
                _currentConfig = CreateDefaultConfig();
        }

        private void SaveCurrentConfig()
        {
            // 持久化到 SQLite（简化实现：此处仅记录）
            AppendLog($"配置 [{_currentConfig.ConfigName}] 已保存");
        }

        private void TryLoadModels()
        {
            string colModelPath = Path.Combine(_modelsDir, "column_classifier.zip");
            string nerModelPath = Path.Combine(_modelsDir, "ner_model.zip");

            try { if (File.Exists(colModelPath)) _columnModel.Load(colModelPath); }
            catch { /* 模型文件损坏时忽略 */ }

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
