using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.ML;
using Microsoft.ML.TorchSharp;
using Microsoft.ML.TorchSharp.NasBert;
using DocExtractor.ML.Training;

namespace DocExtractor.ML.EntityExtractor
{
    /// <summary>
    /// NER 模型训练器
    /// 使用 TorchSharp NAS-BERT NamedEntityRecognition，句级 BIO 标注
    /// </summary>
    public class NerTrainer
    {
        private readonly MLContext _mlContext;

        public NerTrainer(int? seed = 42)
        {
            _mlContext = new MLContext(seed: seed);
        }

        /// <summary>
        /// 训练 NER 模型
        /// </summary>
        public NerTrainingResult Train(
            IReadOnlyList<NerAnnotation> annotatedSamples,
            string modelSavePath,
            IProgress<string> progress = null,
            TrainingParameters parameters = null,
            CancellationToken cancellation = default)
        {
            var p = parameters ?? TrainingParameters.Standard();

            if (annotatedSamples.Count < 20)
                throw new InvalidOperationException($"NER 训练数据不足，至少需要20条标注文本，当前 {annotatedSamples.Count}");

            cancellation.ThrowIfCancellationRequested();

            // 将字符 span 标注转换为词级 BIO 样本
            progress?.Report("转换标注数据为句级 BIO 格式...");
            var wordSamples = CharSpanToWordBioConverter.ConvertAll(annotatedSamples);
            progress?.Report($"句级样本: {wordSamples.Count} 条");

            cancellation.ThrowIfCancellationRequested();

            var dataView = _mlContext.Data.LoadFromEnumerable(wordSamples);

            // 拆分训练集和验证集
            var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: p.TestFraction, seed: p.Seed);

            // 先统一构建标签字典，再同时转换训练集/验证集，确保 LabelKey 一致
            var labelKeyEstimator = _mlContext.Transforms.Conversion.MapValueToKey(
                outputColumnName: "LabelKey",
                inputColumnName: "Label");
            var labelKeyTransformer = labelKeyEstimator.Fit(dataView);
            var keyedData = labelKeyTransformer.Transform(dataView);
            var keyedValidation = labelKeyTransformer.Transform(split.TestSet);

            cancellation.ThrowIfCancellationRequested();
            progress?.Report($"构建 NAS-BERT NER Pipeline (Epochs={p.NerEpochs}, Batch={p.NerBatchSize})...");

            // 训练器要求标签列为 Key 类型（底层 U4）
            // 训练后将预测标签从 Key 还原为 string[] 以兼容现有推理代码。
            var pipeline = _mlContext.MulticlassClassification.Trainers.NamedEntityRecognition(
                    labelColumnName: "LabelKey",
                    outputColumnName: "PredictedLabel",
                    sentence1ColumnName: "Sentence",
                    batchSize: p.NerBatchSize,
                    maxEpochs: p.NerEpochs,
                    validationSet: keyedValidation)
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue(
                    outputColumnName: "PredictedLabel",
                    inputColumnName: "PredictedLabel"));

            cancellation.ThrowIfCancellationRequested();
            progress?.Report("开始训练（NAS-BERT NER，首次运行将下载预训练权重）...");

            // 用全量数据训练最终模型
            var model = pipeline.Fit(keyedData);

            progress?.Report("评估模型...");
            var testPredictions = model.Transform(keyedValidation);
            var metrics = ComputeTokenLevelMetrics(testPredictions);

            cancellation.ThrowIfCancellationRequested();

            Directory.CreateDirectory(Path.GetDirectoryName(modelSavePath));
            _mlContext.Model.Save(model, keyedData.Schema, modelSavePath);

            progress?.Report("NER 模型训练完成！");

            // 收集 label 类型信息
            var allLabels = wordSamples
                .SelectMany(s => s.Label)
                .Distinct()
                .OrderBy(l => l)
                .ToList();

            return new NerTrainingResult
            {
                MicroAccuracy = metrics.MicroAccuracy,
                MacroAccuracy = metrics.MacroAccuracy,
                CharSampleCount = wordSamples.Sum(s => s.Label.Length),
                TextSampleCount = annotatedSamples.Count,
                LabelTypes = allLabels,
                CrossValidationStdDev = 0,
                CrossValidationFolds = 0
            };
        }

        private (double MicroAccuracy, double MacroAccuracy) ComputeTokenLevelMetrics(IDataView predictions)
        {
            var rows = _mlContext.Data.CreateEnumerable<NerEvalRow>(predictions, reuseRowObject: false);
            long total = 0;
            long correct = 0;
            var perLabel = new Dictionary<string, (long Correct, long Total)>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                if (row.Label == null || row.PredictedLabel == null) continue;
                int n = Math.Min(row.Label.Length, row.PredictedLabel.Length);
                for (int i = 0; i < n; i++)
                {
                    var gold = row.Label[i] ?? "O";
                    var pred = row.PredictedLabel[i] ?? "O";

                    total++;
                    bool isCorrect = string.Equals(gold, pred, StringComparison.Ordinal);
                    if (isCorrect) correct++;

                    if (!perLabel.TryGetValue(gold, out var stat))
                        stat = (0, 0);
                    stat.Total++;
                    if (isCorrect) stat.Correct++;
                    perLabel[gold] = stat;
                }
            }

            double micro = total > 0 ? (double)correct / total : 0;
            double macro = perLabel.Count > 0
                ? perLabel.Values.Average(v => v.Total > 0 ? (double)v.Correct / v.Total : 0)
                : 0;
            return (micro, macro);
        }
    }

    /// <summary>带实体标注的文本样本</summary>
    public class NerAnnotation
    {
        public string Text { get; set; } = string.Empty;
        public List<EntityAnnotation> Entities { get; set; } = new List<EntityAnnotation>();
    }

    public class EntityAnnotation
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class NerTrainingResult
    {
        public double MicroAccuracy { get; set; }
        public double MacroAccuracy { get; set; }
        public int CharSampleCount { get; set; }
        public int TextSampleCount { get; set; }
        public List<string> LabelTypes { get; set; } = new List<string>();
        public double CrossValidationStdDev { get; set; }
        public int CrossValidationFolds { get; set; }

        public override string ToString()
        {
            string acc = CrossValidationFolds > 1
                ? $"准确率(Micro): {MicroAccuracy:P2} \u00b1 {CrossValidationStdDev:P2}"
                : $"准确率(Micro): {MicroAccuracy:P2}";
            return $"{acc} | 文本样本: {TextSampleCount} | 字符标记: {CharSampleCount}";
        }
    }

    internal class NerEvalRow
    {
        public string[] Label { get; set; } = Array.Empty<string>();
        public string[] PredictedLabel { get; set; } = Array.Empty<string>();
    }
}
