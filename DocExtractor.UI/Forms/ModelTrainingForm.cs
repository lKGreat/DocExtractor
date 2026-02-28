using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.ColumnClassifier;
using DocExtractor.ML.EntityExtractor;

namespace DocExtractor.UI.Forms
{
    /// <summary>
    /// 模型训练窗口：显示训练数据统计、触发训练、展示评估结果
    /// </summary>
    public class ModelTrainingForm : Form
    {
        private readonly string _dbPath;
        private readonly string _modelsDir;
        private readonly ColumnClassifierModel _columnModel;
        private readonly NerModel _nerModel;

        private Label _colSampleCountLabel = null!;
        private Label _nerSampleCountLabel = null!;
        private Button _trainColumnBtn = null!;
        private Button _trainNerBtn = null!;
        private Button _importCsvBtn = null!;
        private ProgressBar _progressBar = null!;
        private RichTextBox _logBox = null!;
        private Label _evalLabel = null!;

        public ModelTrainingForm(string dbPath, string modelsDir,
            ColumnClassifierModel columnModel, NerModel nerModel)
        {
            _dbPath = dbPath;
            _modelsDir = modelsDir;
            _columnModel = columnModel;
            _nerModel = nerModel;
            InitializeComponent();
            RefreshStats();
        }

        private void InitializeComponent()
        {
            Text = "模型训练管理";
            Size = new Size(700, 550);
            StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                Padding = new Padding(12)
            };

            // 统计区
            var statsGroup = new GroupBox { Text = "训练数据统计", Dock = DockStyle.Fill, Height = 100 };
            var statsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            _colSampleCountLabel = new Label { Text = "列名分类样本：0 条", Width = 220, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            _nerSampleCountLabel = new Label { Text = "NER 标注样本：0 条", Width = 220, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            statsPanel.Controls.AddRange(new Control[] { _colSampleCountLabel, _nerSampleCountLabel });
            statsGroup.Controls.Add(statsPanel);

            // 操作区
            var actionGroup = new GroupBox { Text = "训练操作", Dock = DockStyle.Fill, Height = 80 };
            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };

            _trainColumnBtn = new Button
            {
                Text = "训练列名分类器",
                Width = 150,
                Height = 36,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _trainColumnBtn.Click += OnTrainColumnClassifier;

            _trainNerBtn = new Button
            {
                Text = "训练 NER 模型",
                Width = 140,
                Height = 36,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _trainNerBtn.Click += OnTrainNer;

            _importCsvBtn = new Button { Text = "从 CSV 导入标注", Width = 140, Height = 36 };
            _importCsvBtn.Click += OnImportCsv;

            actionPanel.Controls.AddRange(new Control[] { _trainColumnBtn, _trainNerBtn, _importCsvBtn });
            actionGroup.Controls.Add(actionPanel);

            // 评估结果
            _evalLabel = new Label
            {
                Text = "请先添加训练数据并点击训练按钮",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 9)
            };

            // 进度 + 日志
            _progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 8 };
            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(204, 204, 204),
                Font = new Font("Consolas", 9)
            };

            var logPanel = new Panel { Dock = DockStyle.Fill };
            logPanel.Controls.Add(_logBox);
            logPanel.Controls.Add(_progressBar);

            layout.Controls.Add(statsGroup, 0, 0);
            layout.Controls.Add(actionGroup, 0, 1);
            layout.Controls.Add(_evalLabel, 0, 2);
            layout.Controls.Add(logPanel, 0, 3);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Controls.Add(layout);
        }

        private void RefreshStats()
        {
            try
            {
                using var repo = new TrainingDataRepository(_dbPath);
                _colSampleCountLabel.Text = $"列名分类样本：{repo.GetColumnSampleCount()} 条";
                _nerSampleCountLabel.Text = $"NER 标注样本：{repo.GetNerSampleCount()} 条";
            }
            catch { }
        }

        private async void OnTrainColumnClassifier(object? sender, EventArgs e)
        {
            _trainColumnBtn.Enabled = false;
            _progressBar.Style = ProgressBarStyle.Marquee;
            _logBox.Clear();

            try
            {
                List<(string ColumnText, string FieldName)> samples;
                using (var repo = new TrainingDataRepository(_dbPath))
                    samples = repo.GetColumnSamples();

                if (samples.Count < 10)
                {
                    MessageBox.Show($"列名分类样本不足（当前 {samples.Count} 条，至少需要 10 条）。\n" +
                        "请先在标注工具中添加训练数据，或从 CSV 导入。",
                        "数据不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var inputs = samples.ConvertAll(s => new ColumnInput
                {
                    ColumnText = s.ColumnText,
                    Label = s.FieldName
                });

                var progress = new Progress<string>(msg =>
                {
                    _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                    _logBox.ScrollToCaret();
                });

                var trainer = new ColumnClassifierTrainer();
                string modelPath = Path.Combine(_modelsDir, "column_classifier.zip");

                var eval = await Task.Run(() => trainer.Train(inputs, modelPath, progress));

                // 热加载新模型
                _columnModel.Reload(modelPath);

                _evalLabel.Text = $"列名分类器评估：{eval}";
                AppendLog($"\n✓ 训练完成！{eval}");
            }
            catch (Exception ex)
            {
                AppendLog($"\n✗ 训练失败: {ex.Message}");
                MessageBox.Show($"训练失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _trainColumnBtn.Enabled = true;
                _progressBar.Style = ProgressBarStyle.Continuous;
                RefreshStats();
            }
        }

        private async void OnTrainNer(object? sender, EventArgs e)
        {
            _trainNerBtn.Enabled = false;
            _progressBar.Style = ProgressBarStyle.Marquee;

            try
            {
                List<NerAnnotation> samples;
                using (var repo = new TrainingDataRepository(_dbPath))
                    samples = repo.GetNerSamples();

                if (samples.Count < 20)
                {
                    MessageBox.Show($"NER 样本不足（当前 {samples.Count} 条，至少需要 20 条）。",
                        "数据不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var progress = new Progress<string>(msg =>
                {
                    _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                    _logBox.ScrollToCaret();
                });

                var trainer = new NerTrainer();
                string modelPath = Path.Combine(_modelsDir, "ner_model.zip");

                var eval = await Task.Run(() => trainer.Train(samples, modelPath, progress));
                _nerModel.Load(modelPath);

                _evalLabel.Text = $"NER 评估：{eval}";
                AppendLog($"\n✓ NER 训练完成！{eval}");
            }
            catch (Exception ex)
            {
                AppendLog($"\n✗ NER 训练失败: {ex.Message}");
            }
            finally
            {
                _trainNerBtn.Enabled = true;
                _progressBar.Style = ProgressBarStyle.Continuous;
            }
        }

        private void OnImportCsv(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "CSV 文件|*.csv",
                Title = "选择列名标注 CSV 文件（格式：列名,规范字段名）"
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

                RefreshStats();
                AppendLog($"✓ 从 CSV 导入 {imported} 条列名标注");
                MessageBox.Show($"成功导入 {imported} 条标注数据。", "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AppendLog(string msg)
        {
            if (_logBox.InvokeRequired)
            {
                _logBox.Invoke(new Action(() => AppendLog(msg)));
                return;
            }
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            _logBox.ScrollToCaret();
        }
    }
}
