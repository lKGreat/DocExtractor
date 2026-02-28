using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.TorchSharp;
using DocExtractor.ML.Training;

namespace DocExtractor.ML.ColumnClassifier
{
    /// <summary>
    /// 列名分类器训练器
    /// 使用 TorchSharp NAS-BERT TextClassification，支持参数化训练和取消
    /// </summary>
    public class ColumnClassifierTrainer
    {
        private readonly MLContext _mlContext;

        public ColumnClassifierTrainer(int? seed = 42)
        {
            _mlContext = new MLContext(seed: seed);
        }

        /// <summary>
        /// 训练列名分类模型并返回评估结果
        /// </summary>
        public TrainingEvaluation Train(
            IReadOnlyList<ColumnInput> trainData,
            string modelSavePath,
            IProgress<string> progress = null,
            TrainingParameters parameters = null,
            CancellationToken cancellation = default)
        {
            var p = parameters ?? TrainingParameters.Standard();

            if (trainData.Count < 10)
                throw new InvalidOperationException($"训练数据不足，至少需要10条，当前只有{trainData.Count}条");

            // 数据增强
            IReadOnlyList<ColumnInput> finalData = trainData;
            if (p.EnableAugmentation)
            {
                progress?.Report("执行数据增强...");
                finalData = ColumnDataAugmenter.Augment(trainData);
                progress?.Report($"增强后样本数: {finalData.Count}（原始 {trainData.Count}）");
            }

            cancellation.ThrowIfCancellationRequested();
            progress?.Report($"加载 {finalData.Count} 条训练数据...");

            var dataView = _mlContext.Data.LoadFromEnumerable(finalData);

            // 拆分训练集和验证集（NAS-BERT 使用 validationSet 参数替代 CV）
            var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: p.TestFraction, seed: p.Seed);

            cancellation.ThrowIfCancellationRequested();
            progress?.Report($"构建 NAS-BERT TextClassification Pipeline (Epochs={p.ColumnEpochs}, Batch={p.ColumnBatchSize})...");

            // 验证集也需要 Label 为 Key 类型，先用 MapValueToKey 预转换
            var labelKeyTransform = _mlContext.Transforms.Conversion.MapValueToKey(
                outputColumnName: "Label", inputColumnName: "Label").Fit(dataView);
            var validationKeyed = labelKeyTransform.Transform(split.TestSet);

            var pipeline = BuildPipeline(p, validationKeyed);

            cancellation.ThrowIfCancellationRequested();
            progress?.Report("开始训练（NAS-BERT，首次运行将下载预训练权重）...");

            // 用全量数据训练最终模型
            var model = pipeline.Fit(dataView);

            progress?.Report("评估模型...");
            var testPredictions = model.Transform(split.TestSet);
            var metrics = _mlContext.MulticlassClassification.Evaluate(
                testPredictions,
                labelColumnName: "Label",
                predictedLabelColumnName: "PredictedLabel");

            cancellation.ThrowIfCancellationRequested();

            progress?.Report($"保存模型到 {modelSavePath}...");
            Directory.CreateDirectory(Path.GetDirectoryName(modelSavePath));
            _mlContext.Model.Save(model, dataView.Schema, modelSavePath);

            progress?.Report("训练完成！");

            return new TrainingEvaluation
            {
                MicroAccuracy = metrics.MicroAccuracy,
                MacroAccuracy = metrics.MacroAccuracy,
                LogLoss = metrics.LogLoss,
                SampleCount = trainData.Count,
                AugmentedSampleCount = finalData.Count,
                ClassCount = trainData.Select(d => d.Label).Distinct().Count(),
                CrossValidationStdDev = 0,
                CrossValidationFolds = 0
            };
        }

        private IEstimator<ITransformer> BuildPipeline(TrainingParameters p, IDataView validationSet)
        {
            // TextClassification 要求 Label 列为 Key 类型（UInt32），需先 MapValueToKey
            return _mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "Label",
                    inputColumnName: "Label")
                .Append(_mlContext.MulticlassClassification.Trainers.TextClassification(
                    labelColumnName: "Label",
                    sentence1ColumnName: "ColumnText",
                    maxEpochs: p.ColumnEpochs,
                    batchSize: p.ColumnBatchSize,
                    validationSet: validationSet))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue(
                    outputColumnName: "PredictedLabel",
                    inputColumnName: "PredictedLabel"));
        }
    }

    public class TrainingEvaluation
    {
        public double MicroAccuracy { get; set; }
        public double MacroAccuracy { get; set; }
        public double LogLoss { get; set; }
        public int SampleCount { get; set; }
        public int AugmentedSampleCount { get; set; }
        public int ClassCount { get; set; }
        public double CrossValidationStdDev { get; set; }
        public int CrossValidationFolds { get; set; }

        public override string ToString()
        {
            string acc = CrossValidationFolds > 1
                ? $"准确率(Micro): {MicroAccuracy:P2} \u00b1 {CrossValidationStdDev:P2}"
                : $"准确率(Micro): {MicroAccuracy:P2}";
            string aug = AugmentedSampleCount > SampleCount
                ? $" | 增强后:{AugmentedSampleCount}"
                : "";
            return $"{acc} | 准确率(Macro): {MacroAccuracy:P2} | " +
                   $"LogLoss: {LogLoss:F4} | 样本:{SampleCount}{aug} | 类别:{ClassCount}";
        }
    }
}
