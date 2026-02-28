using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;
using DocExtractor.ML.Training;

namespace DocExtractor.ML.SectionClassifier
{
    /// <summary>
    /// 章节标题分类器训练器
    /// 使用混合特征：文本 N-gram + 格式数值特征，FastTree 二分类
    /// 支持参数化训练、交叉验证和取消
    /// </summary>
    public class SectionClassifierTrainer
    {
        private readonly MLContext _mlContext;

        public SectionClassifierTrainer(int? seed = 42)
        {
            _mlContext = new MLContext(seed: seed);
        }

        /// <summary>
        /// 训练章节标题分类模型
        /// </summary>
        public SectionTrainingEvaluation Train(
            IReadOnlyList<SectionInput> trainData,
            string modelSavePath,
            IProgress<string> progress = null,
            TrainingParameters parameters = null,
            CancellationToken cancellation = default)
        {
            var p = parameters ?? TrainingParameters.Standard();

            if (trainData.Count < 20)
                throw new InvalidOperationException(
                    $"训练数据不足，至少需要20条，当前只有{trainData.Count}条（建议正负样本各10条以上）");

            progress?.Report($"加载 {trainData.Count} 条训练数据...");

            var positiveCount = trainData.Count(d => d.IsHeading);
            var negativeCount = trainData.Count - positiveCount;
            progress?.Report($"正样本（章节标题）: {positiveCount}，负样本（普通段落）: {negativeCount}");

            cancellation.ThrowIfCancellationRequested();

            var dataView = _mlContext.Data.LoadFromEnumerable(trainData);

            progress?.Report($"构建训练 Pipeline（FastTree, 树={p.SectionTrees}, 叶={p.SectionLeaves}）...");

            var pipeline = BuildPipeline(p);

            cancellation.ThrowIfCancellationRequested();

            // 交叉验证
            double cvStdDev = 0;
            double cvF1StdDev = 0;
            if (p.CrossValidationFolds > 1)
            {
                progress?.Report($"执行 {p.CrossValidationFolds} 折交叉验证...");
                var cvResults = _mlContext.BinaryClassification.CrossValidate(
                    dataView, pipeline, numberOfFolds: p.CrossValidationFolds,
                    labelColumnName: "Label");

                var accuracies = cvResults.Select(r => r.Metrics.Accuracy).ToArray();
                var f1s = cvResults.Select(r => r.Metrics.F1Score).ToArray();
                double accMean = accuracies.Average();
                cvStdDev = Math.Sqrt(accuracies.Select(x => (x - accMean) * (x - accMean)).Average());
                double f1Mean = f1s.Average();
                cvF1StdDev = Math.Sqrt(f1s.Select(x => (x - f1Mean) * (x - f1Mean)).Average());

                progress?.Report($"CV 结果: Acc {accMean:P2} \u00b1 {cvStdDev:P2} | F1 {f1Mean:F4} \u00b1 {cvF1StdDev:F4}");
                cancellation.ThrowIfCancellationRequested();
            }

            // 单次拆分评估
            var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: p.TestFraction, seed: p.Seed);

            progress?.Report("开始训练...");
            cancellation.ThrowIfCancellationRequested();

            // 用全量数据训练最终模型
            var model = pipeline.Fit(dataView);

            progress?.Report("评估模型...");
            var testPredictions = model.Transform(split.TestSet);
            var metrics = _mlContext.BinaryClassification.Evaluate(
                testPredictions,
                labelColumnName: "Label",
                scoreColumnName: "Score");

            cancellation.ThrowIfCancellationRequested();

            progress?.Report($"保存模型到 {modelSavePath}...");
            Directory.CreateDirectory(Path.GetDirectoryName(modelSavePath));
            _mlContext.Model.Save(model, dataView.Schema, modelSavePath);

            progress?.Report("训练完成！");

            return new SectionTrainingEvaluation
            {
                Accuracy = metrics.Accuracy,
                AreaUnderRocCurve = metrics.AreaUnderRocCurve,
                F1Score = metrics.F1Score,
                PositivePrecision = metrics.PositivePrecision,
                PositiveRecall = metrics.PositiveRecall,
                SampleCount = trainData.Count,
                PositiveCount = positiveCount,
                CrossValidationStdDev = cvStdDev,
                CrossValidationF1StdDev = cvF1StdDev,
                CrossValidationFolds = p.CrossValidationFolds
            };
        }

        private IEstimator<ITransformer> BuildPipeline(TrainingParameters p)
        {
            var textFeaturizer = _mlContext.Transforms.Text.FeaturizeText(
                outputColumnName: "TextFeatures",
                options: new TextFeaturizingEstimator.Options
                {
                    CharFeatureExtractor = new WordBagEstimator.Options
                    {
                        NgramLength = 3,
                        UseAllLengths = true
                    },
                    WordFeatureExtractor = new WordBagEstimator.Options
                    {
                        NgramLength = 2,
                        UseAllLengths = true
                    }
                },
                inputColumnNames: new[] { "Text" });

            var formatConcatenator = _mlContext.Transforms.Concatenate(
                "FormatFeatures",
                "IsBold", "FontSize", "HasNumberPrefix", "TextLength", "HasHeadingStyle", "Position");

            var featureCombiner = _mlContext.Transforms.Concatenate(
                "Features", "TextFeatures", "FormatFeatures");

            var trainer = _mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: p.SectionLeaves,
                numberOfTrees: p.SectionTrees,
                minimumExampleCountPerLeaf: p.SectionMinLeaf);

            return textFeaturizer
                .Append(formatConcatenator)
                .Append(featureCombiner)
                .Append(trainer);
        }
    }

    public class SectionTrainingEvaluation
    {
        public double Accuracy { get; set; }
        public double AreaUnderRocCurve { get; set; }
        public double F1Score { get; set; }
        public double PositivePrecision { get; set; }
        public double PositiveRecall { get; set; }
        public int SampleCount { get; set; }
        public int PositiveCount { get; set; }
        public double CrossValidationStdDev { get; set; }
        public double CrossValidationF1StdDev { get; set; }
        public int CrossValidationFolds { get; set; }

        public override string ToString()
        {
            string acc = CrossValidationFolds > 1
                ? $"准确率: {Accuracy:P2} \u00b1 {CrossValidationStdDev:P2}"
                : $"准确率: {Accuracy:P2}";
            string f1 = CrossValidationFolds > 1
                ? $"F1: {F1Score:F4} \u00b1 {CrossValidationF1StdDev:F4}"
                : $"F1: {F1Score:F4}";
            return $"{acc} | AUC: {AreaUnderRocCurve:F4} | {f1} | " +
                   $"精确率: {PositivePrecision:P2} | 召回率: {PositiveRecall:P2} | " +
                   $"样本: {SampleCount}（正: {PositiveCount}，负: {SampleCount - PositiveCount}）";
        }
    }
}
