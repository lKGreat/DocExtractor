using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

namespace DocExtractor.ML.SectionClassifier
{
    /// <summary>
    /// 章节标题分类器训练器
    /// 使用混合特征：文本 N-gram + 格式数值特征，FastTree 二分类
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
        /// <param name="trainData">训练数据</param>
        /// <param name="modelSavePath">模型保存路径（.zip）</param>
        /// <param name="progress">训练进度回调</param>
        public SectionTrainingEvaluation Train(
            IReadOnlyList<SectionInput> trainData,
            string modelSavePath,
            IProgress<string>? progress = null)
        {
            if (trainData.Count < 20)
                throw new InvalidOperationException(
                    $"训练数据不足，至少需要20条，当前只有{trainData.Count}条（建议正负样本各10条以上）");

            progress?.Report($"加载 {trainData.Count} 条训练数据...");

            var positiveCount = trainData.Count(d => d.IsHeading);
            var negativeCount = trainData.Count - positiveCount;
            progress?.Report($"正样本（章节标题）: {positiveCount}，负样本（普通段落）: {negativeCount}");

            var dataView = _mlContext.Data.LoadFromEnumerable(trainData);
            var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);

            progress?.Report("构建训练 Pipeline（文本N-gram + 格式特征）...");

            var pipeline = BuildPipeline();
            var model = pipeline.Fit(split.TrainSet);

            progress?.Report("评估模型...");
            var predictions = model.Transform(split.TestSet);
            var metrics = _mlContext.BinaryClassification.Evaluate(
                predictions,
                labelColumnName: "Label",
                scoreColumnName: "Score");

            progress?.Report($"保存模型到 {modelSavePath}...");
            Directory.CreateDirectory(Path.GetDirectoryName(modelSavePath)!);
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
                PositiveCount = positiveCount
            };
        }

        private IEstimator<ITransformer> BuildPipeline()
        {
            // 文本特征（N-gram，适合中文短文本）
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

            // 格式数值特征拼接
            var formatConcatenator = _mlContext.Transforms.Concatenate(
                "FormatFeatures",
                "IsBold", "FontSize", "HasNumberPrefix", "TextLength", "HasHeadingStyle", "Position");

            // 最终特征合并
            var featureCombiner = _mlContext.Transforms.Concatenate(
                "Features", "TextFeatures", "FormatFeatures");

            // FastTree 二分类（对小数据集表现好，适合不均衡样本）
            var trainer = _mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 2);

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

        public override string ToString() =>
            $"准确率: {Accuracy:P2} | AUC: {AreaUnderRocCurve:F4} | F1: {F1Score:F4} | " +
            $"精确率: {PositivePrecision:P2} | 召回率: {PositiveRecall:P2} | " +
            $"样本: {SampleCount}（正: {PositiveCount}，负: {SampleCount - PositiveCount}）";
    }
}
