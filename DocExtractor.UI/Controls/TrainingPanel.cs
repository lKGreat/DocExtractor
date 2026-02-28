using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Windows.Forms;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.Recommendation;
using DocExtractor.ML.Training;
using DocExtractor.UI.Context;
using DocExtractor.UI.Forms;
using DocExtractor.UI.Helpers;

namespace DocExtractor.UI.Controls
{
    /// <summary>
    /// Model training panel: training data statistics, parameter controls, training execution, and log.
    /// </summary>
    public partial class TrainingPanel : UserControl
    {
        private readonly DocExtractorContext _ctx;
        private CancellationTokenSource _trainCts;

        internal TrainingPanel(DocExtractorContext ctx)
        {
            _ctx = ctx;
            InitializeComponent();
            WireEvents();
            _ctx.TrainLogLine += line => AppendToTrainLog(line);
        }

        public void OnActivated() => RefreshStats();

        // ── Event Wiring ──────────────────────────────────────────────────────

        private void WireEvents()
        {
            _trainUnifiedBtn.Click += OnTrainUnified;
            _cancelTrainBtn.Click += OnCancelTraining;
            _importCsvBtn.Click += OnImportTrainingData;
            _importSectionWordBtn.Click += OnImportSectionFromWord;
            _genFromKnowledgeBtn.Click += OnGenerateFromKnowledge;
            _columnErrorAnalysisBtn.Click += OnColumnErrorAnalysis;
            _presetCombo.SelectedIndexChanged += OnPresetChanged;
            OnPresetChanged(null, EventArgs.Empty);
        }

        // ── Stats Refresh ─────────────────────────────────────────────────────

        public void RefreshStats()
        {
            RefreshSampleCounts();
            RefreshKnowledgeCount();
        }

        private void RefreshSampleCounts()
        {
            try
            {
                using var repo = new TrainingDataRepository(_ctx.DbPath);
                int col = repo.GetColumnSampleCount();
                int ner = repo.GetNerSampleCount();
                int sec = repo.GetSectionSampleCount();

                _colSampleCountLabel.Text = $"列名分类样本：{col} 条";
                _nerSampleCountLabel.Text = $"NER 标注样本：{ner} 条";
                _sectionSampleCountLabel.Text = $"章节标题样本：{sec} 条";

                var colLatest = repo.GetLatestRecord("ColumnClassifier");
                var nerLatest = repo.GetLatestRecord("NER");
                var secLatest = repo.GetLatestRecord("SectionClassifier");

                string colHealth = ComputeHealth(File.Exists(Path.Combine(_ctx.ModelsDir, "column_classifier.zip")), col, 10, TryExtractAccuracy(colLatest?.MetricsJson));
                string nerHealth = ComputeHealth(File.Exists(Path.Combine(_ctx.ModelsDir, "ner_model.zip")), ner, 20, TryExtractAccuracy(nerLatest?.MetricsJson));
                string secHealth = ComputeHealth(File.Exists(Path.Combine(_ctx.ModelsDir, "section_classifier.zip")), sec, 20, TryExtractAccuracy(secLatest?.MetricsJson));

                _modelHealthLabel.Text = $"模型健康度：列名[{colHealth}]  NER[{nerHealth}]  章节[{secHealth}]";
            }
            catch { }
        }

        private void RefreshKnowledgeCount()
        {
            try
            {
                using var repo = new GroupKnowledgeRepository(_ctx.DbPath);
                _knowledgeCountLabel.Text = $"推荐知识库：{repo.GetKnowledgeCount()} 条";
            }
            catch { }
        }

        private static string ComputeHealth(bool modelExists, int samples, int minSamples, double? accuracy)
        {
            if (!modelExists) return "未训练";
            if (samples < minSamples) return "待提升";
            if (accuracy.HasValue)
            {
                if (accuracy.Value >= 0.95 && samples >= 100) return "优秀";
                if (accuracy.Value >= 0.90) return "良好";
            }
            return "可用";
        }

        private static double? TryExtractAccuracy(string metricsText)
        {
            if (string.IsNullOrWhiteSpace(metricsText)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(metricsText, @"(\d+(?:\.\d+)?)%");
            if (!m.Success) return null;
            return double.TryParse(m.Groups[1].Value, out double pct) ? pct / 100.0 : (double?)null;
        }

        // ── Training Parameters ───────────────────────────────────────────────

        private void OnPresetChanged(object sender, EventArgs e)
        {
            bool isCustom = _presetCombo.SelectedIndex == 3;
            SetParamControlsEnabled(isCustom);
            if (!isCustom) ApplyPresetToUI(BuildPreset(_presetCombo.SelectedIndex));
        }

        private void SetParamControlsEnabled(bool enabled)
        {
            _cvFoldsSpinner.Enabled = enabled;
            _testFractionSpinner.Enabled = enabled;
            _augmentCheckbox.Enabled = enabled;
            _colEpochsSpinner.Enabled = enabled;
            _colBatchSpinner.Enabled = enabled;
            _nerEpochsSpinner.Enabled = enabled;
            _nerBatchSpinner.Enabled = enabled;
            _secTreesSpinner.Enabled = enabled;
            _secLeavesSpinner.Enabled = enabled;
            _secMinLeafSpinner.Enabled = enabled;
        }

        private static TrainingParameters BuildPreset(int index)
        {
            switch (index)
            {
                case 0: return TrainingParameters.Fast();
                case 2: return TrainingParameters.Fine();
                default: return TrainingParameters.Standard();
            }
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

        private TrainingParameters ReadParamsFromUI()
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

        // ── Training Execution ────────────────────────────────────────────────

        private async void OnTrainUnified(object sender, EventArgs e)
        {
            SetTrainingUiState(true);
            _ctx.TrainLogger?.LogInformation("========== 统一模型训练开始 ==========");

            try
            {
                var bundle = _ctx.TrainingService.LoadTrainingData(_ctx.DbPath);
                _ctx.TrainLogger?.LogInformation($"数据统计 — 列名:{bundle.ColumnInputs.Count}  NER:{bundle.NerSamples.Count}  章节:{bundle.SectionInputs.Count}");

                var parameters = ReadParamsFromUI();
                _trainCts = new CancellationTokenSource();
                var ct = _trainCts.Token;

                var progress = new Progress<(string Stage, string Detail, double Percent)>(info =>
                {
                    _ctx.TrainLogger?.LogInformation($"[{info.Stage}] {info.Detail}");
                    UpdateTrainProgress(info.Percent);
                });

                var result = await System.Threading.Tasks.Task.Run(() =>
                    _ctx.TrainingService.TrainUnified(bundle, _ctx.ModelsDir, parameters, progress, ct));

                _ctx.TrainingService.ReloadModels(_ctx.ModelsDir, _ctx.ColumnModel, _ctx.NerModel, _ctx.SectionModel);
                _ctx.TrainingService.SaveTrainingHistory(_ctx.DbPath, result, bundle, parameters);

                _evalLabel.Text = "统一训练完成";
                ShowTrainingComparison("ColumnClassifier", result.ToString(), bundle.ColumnInputs.Count);
                _ctx.TrainLogger?.LogInformation($"\n========== 统一训练完成 ==========\n{result}");
                MessageHelper.Success(this, "统一模型训练完成！");
            }
            catch (OperationCanceledException)
            {
                _ctx.TrainLogger?.LogInformation("\n统一训练已取消");
                MessageHelper.Warn(this, "训练已取消");
            }
            catch (Exception ex)
            {
                _ctx.TrainLogger?.LogError(ex, "统一训练失败");
                MessageHelper.Error(this, $"统一训练失败：{ex.Message}");
            }
            finally
            {
                _trainCts?.Dispose();
                _trainCts = null;
                SetTrainingUiState(false);
                RefreshStats();
            }
        }

        private void OnCancelTraining(object sender, EventArgs e)
        {
            _trainCts?.Cancel();
            _ctx.TrainLogger?.LogInformation("已请求取消训练...");
        }

        private void UpdateTrainProgress(double percent)
        {
            if (_trainProgressBar.InvokeRequired)
            {
                _trainProgressBar.Invoke(new Action<double>(UpdateTrainProgress), percent);
                return;
            }
            if (percent >= 0 && percent <= 100)
            {
                _trainProgressBar.Style = ProgressBarStyle.Continuous;
                _trainProgressBar.Value = Math.Min(100, (int)percent);
            }
        }

        private void SetTrainingUiState(bool training)
        {
            _trainUnifiedBtn.Enabled = !training;
            _cancelTrainBtn.Enabled = training;
            _presetCombo.Enabled = !training;
            _trainProgressBar.Style = training ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        }

        private void ShowTrainingComparison(string modelType, string currentMetrics, int currentSamples)
        {
            try
            {
                using var repo = new TrainingDataRepository(_ctx.DbPath);
                var prev = repo.GetLatestRecord(modelType);
                _evalCompareLabel.Text = prev != null
                    ? $"上次（{prev.TrainedAt}）：样本 {prev.SampleCount} → 当前 {currentSamples} | {prev.MetricsJson}"
                    : "（首次训练，无历史对比）";
            }
            catch { _evalCompareLabel.Text = ""; }
        }

        // ── Data Import ───────────────────────────────────────────────────────

        private void OnImportTrainingData(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "CSV/Excel 文件|*.csv;*.xlsx", Title = "选择列名标注数据文件（格式：列名,规范字段名）" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                int imported = _ctx.TrainingService.ImportColumnSamples(_ctx.DbPath, dlg.FileName);
                RefreshStats();
                _ctx.TrainLogger?.LogInformation($"从文件导入 {imported} 条列名标注");
                MessageHelper.Success(this, $"成功导入 {imported} 条标注数据");
            }
            catch (Exception ex) { MessageHelper.Error(this, $"导入失败：{ex.Message}"); }
        }

        private void OnImportSectionFromWord(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "Word 文档|*.docx", Title = "选择用于章节标题标注的 Word 文档（支持多选）", Multiselect = true };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var result = _ctx.TrainingService.ImportSectionSamples(_ctx.DbPath, dlg.FileNames);
                foreach (var file in result.PerFileCounts)
                    _ctx.TrainLogger?.LogInformation($"  {file.fileName}：扫描 {file.count} 个段落");

                RefreshStats();
                _ctx.TrainLogger?.LogInformation($"\n共导入 {result.ImportedCount} 条段落");
                MessageHelper.Success(this, $"已从 {dlg.FileNames.Length} 个文件导入 {result.ImportedCount} 条段落。");
            }
            catch (Exception ex) { MessageHelper.Error(this, $"导入失败：{ex.Message}"); }
        }

        private async void OnGenerateFromKnowledge(object sender, EventArgs e)
        {
            _genFromKnowledgeBtn.Enabled = false;
            _trainProgressBar.Style = ProgressBarStyle.Marquee;
            _ctx.TrainLogger?.LogInformation("从知识库生成训练数据...");

            try
            {
                var configs = DocExtractor.Core.Models.BuiltInConfigs.GetAll().ToArray();
                int colAdded, secPosAdded, secNegAdded;
                var genResult = await System.Threading.Tasks.Task.Run(() =>
                    _ctx.TrainingService.GenerateFromKnowledge(_ctx.DbPath, configs));
                colAdded = genResult.colAdded;
                secPosAdded = genResult.secPosAdded;
                secNegAdded = genResult.secNegAdded;

                RefreshStats();
                string summary = $"列名分类样本新增 {colAdded} 条\n章节标题正样本新增 {secPosAdded} 条\n章节标题负样本新增 {secNegAdded} 条";
                _ctx.TrainLogger?.LogInformation($"\n知识库训练数据生成完成：\n{summary}");

                if (colAdded + secPosAdded + secNegAdded == 0)
                    MessageHelper.Warn(this, "知识库中无新数据可生成（数据已全部存在，或知识库为空）。\n请先通过「开始抽取」生成抽取结果。");
                else
                    MessageHelper.Success(this, $"已从知识库自动生成训练数据：\n{summary}");
            }
            catch (Exception ex)
            {
                _ctx.TrainLogger?.LogError(ex, "生成失败");
                MessageHelper.Error(this, $"生成失败：{ex.Message}");
            }
            finally
            {
                _genFromKnowledgeBtn.Enabled = true;
                _trainProgressBar.Style = ProgressBarStyle.Continuous;
            }
        }

        private void OnColumnErrorAnalysis(object sender, EventArgs e)
        {
            try
            {
                var items = _ctx.TrainingService.BuildColumnErrorAnalysis(_ctx.DbPath, _ctx.ColumnModel);
                if (items.Count == 0) { MessageHelper.Info(this, "未发现列名分类错误样本，或模型尚未加载。"); return; }
                using var form = new ColumnErrorAnalysisForm(items);
                form.ShowDialog(this);
            }
            catch (Exception ex) { MessageHelper.Error(this, $"错误分析失败：{ex.Message}"); }
        }

        // ── Log ───────────────────────────────────────────────────────────────

        private void AppendToTrainLog(string line)
        {
            if (_trainLogBox.InvokeRequired) { _trainLogBox.Invoke(new Action<string>(AppendToTrainLog), line); return; }
            _trainLogBox.AppendText(line + Environment.NewLine);
            _trainLogBox.ScrollToCaret();
        }
    }
}
