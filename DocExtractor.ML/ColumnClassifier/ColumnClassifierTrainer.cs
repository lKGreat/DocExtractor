using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

namespace DocExtractor.ML.ColumnClassifier
{
    /// <summary>
    /// 列名分类器训练器
    /// 使用 SDCA + N-gram 文本特征，支持 CPU 本地训练和模型保存
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
        /// <param name="trainData">训练数据</param>
        /// <param name="modelSavePath">模型保存路径（.zip）</param>
        /// <param name="progress">训练进度报告</param>
        public TrainingEvaluation Train(
            IReadOnlyList<ColumnInput> trainData,
            string modelSavePath,
            IProgress<string>? progress = null)
        {
            if (trainData.Count < 10)
                throw new InvalidOperationException($"训练数据不足，至少需要10条，当前只有{trainData.Count}条");

            progress?.Report($"加载 {trainData.Count} 条训练数据...");

            var dataView = _mlContext.Data.LoadFromEnumerable(trainData);

            // 80/20 分割用于评估
            var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);

            progress?.Report("构建训练 Pipeline...");

            var pipeline = BuildPipeline();
            var trainPipeline = pipeline.Append(
                _mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "LabelKey",
                    featureColumnName: "Features",
                    maximumNumberOfIterations: 100))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue(
                    outputColumnName: "PredictedLabel",
                    inputColumnName: "PredictedLabel"));

            progress?.Report("开始训练（CPU）...");
            var model = trainPipeline.Fit(split.TrainSet);

            progress?.Report("评估模型...");
            var predictions = model.Transform(split.TestSet);
            var metrics = _mlContext.MulticlassClassification.Evaluate(
                predictions,
                labelColumnName: "LabelKey",
                predictedLabelColumnName: "PredictedLabel");

            progress?.Report($"保存模型到 {modelSavePath}...");
            Directory.CreateDirectory(Path.GetDirectoryName(modelSavePath)!);
            _mlContext.Model.Save(model, dataView.Schema, modelSavePath);

            progress?.Report("训练完成！");

            return new TrainingEvaluation
            {
                MicroAccuracy = metrics.MicroAccuracy,
                MacroAccuracy = metrics.MacroAccuracy,
                LogLoss = metrics.LogLoss,
                SampleCount = trainData.Count,
                ClassCount = trainData.Select(d => d.Label).Distinct().Count()
            };
        }

        private IEstimator<ITransformer> BuildPipeline()
        {
            return _mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "LabelKey",
                    inputColumnName: "Label")
                .Append(_mlContext.Transforms.Text.FeaturizeText(
                    outputColumnName: "Features",
                    options: new TextFeaturizingEstimator.Options
                    {
                        // 字符 N-gram (2-4)：适合中文短文本
                        CharFeatureExtractor = new WordBagEstimator.Options
                        {
                            NgramLength = 4,
                            UseAllLengths = true
                        },
                        // 词 N-gram (1-2)：抓取词序特征
                        WordFeatureExtractor = new WordBagEstimator.Options
                        {
                            NgramLength = 2,
                            UseAllLengths = true
                        }
                    },
                    inputColumnNames: new[] { "ColumnText" }));
        }
    }

    public class TrainingEvaluation
    {
        public double MicroAccuracy { get; set; }
        public double MacroAccuracy { get; set; }
        public double LogLoss { get; set; }
        public int SampleCount { get; set; }
        public int ClassCount { get; set; }

        public override string ToString() =>
            $"准确率(Micro): {MicroAccuracy:P2} | 准确率(Macro): {MacroAccuracy:P2} | " +
            $"LogLoss: {LogLoss:F4} | 样本:{SampleCount} | 类别:{ClassCount}";
    }
}
