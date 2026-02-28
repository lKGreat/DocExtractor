using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.ML;
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.ML.Transforms.Text;
using DocExtractor.ML.Training;

namespace DocExtractor.ML.EntityExtractor
{
    /// <summary>
    /// NER 模型训练器
    /// 输入：BIO 标注的字符级训练数据
    /// 使用：LightGBM 梯度提升分类，支持参数化训练、交叉验证和取消
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

            // 将样本展开为字符级序列
            var charSamples = ExpandToCharLevel(annotatedSamples);

            progress?.Report($"字符级样本: {charSamples.Count} 个字符标记");

            if (charSamples.Count < 50)
                throw new InvalidOperationException($"NER 训练数据不足，至少需要50个字符标注，当前 {charSamples.Count}");

            cancellation.ThrowIfCancellationRequested();

            var dataView = _mlContext.Data.LoadFromEnumerable(charSamples);

            progress?.Report($"构建 NER Pipeline (LightGBM, 迭代={p.NerIterations}, 叶={p.NerLeaves}, 率={p.NerLearningRate})...");

            var pipeline = BuildPipeline(p);

            cancellation.ThrowIfCancellationRequested();

            // 交叉验证
            double cvStdDev = 0;
            if (p.CrossValidationFolds > 1)
            {
                progress?.Report($"执行 {p.CrossValidationFolds} 折交叉验证...");
                var cvResults = _mlContext.MulticlassClassification.CrossValidate(
                    dataView, pipeline, numberOfFolds: p.CrossValidationFolds,
                    labelColumnName: "LabelKey");

                var microAccuracies = cvResults.Select(r => r.Metrics.MicroAccuracy).ToArray();
                double mean = microAccuracies.Average();
                cvStdDev = Math.Sqrt(microAccuracies.Select(x => (x - mean) * (x - mean)).Average());

                progress?.Report($"CV 结果: Micro {mean:P2} \u00b1 {cvStdDev:P2}");
                cancellation.ThrowIfCancellationRequested();
            }

            // 单次拆分评估
            var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: p.TestFraction, seed: p.Seed);

            progress?.Report("开始训练（CPU LightGBM）...");
            cancellation.ThrowIfCancellationRequested();

            // 用全量数据训练最终模型
            var model = pipeline.Fit(dataView);

            progress?.Report("评估模型...");
            var testPredictions = model.Transform(split.TestSet);
            var metrics = _mlContext.MulticlassClassification.Evaluate(
                testPredictions,
                labelColumnName: "LabelKey",
                predictedLabelColumnName: "PredictedLabel");

            cancellation.ThrowIfCancellationRequested();

            Directory.CreateDirectory(Path.GetDirectoryName(modelSavePath));
            _mlContext.Model.Save(model, dataView.Schema, modelSavePath);

            progress?.Report("NER 模型训练完成！");

            return new NerTrainingResult
            {
                MicroAccuracy = metrics.MicroAccuracy,
                MacroAccuracy = metrics.MacroAccuracy,
                CharSampleCount = charSamples.Count,
                TextSampleCount = annotatedSamples.Count,
                LabelTypes = charSamples.Select(c => c.Label).Distinct().OrderBy(l => l).ToList(),
                CrossValidationStdDev = cvStdDev,
                CrossValidationFolds = p.CrossValidationFolds
            };
        }

        private IEstimator<ITransformer> BuildPipeline(TrainingParameters p)
        {
            return _mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "LabelKey",
                    inputColumnName: "Label")
                .Append(_mlContext.Transforms.Text.FeaturizeText(
                    "CharTokenFeats",
                    options: new TextFeaturizingEstimator.Options
                    {
                        CharFeatureExtractor = new WordBagEstimator.Options { NgramLength = 3, UseAllLengths = true },
                        WordFeatureExtractor = null
                    },
                    inputColumnNames: new[] { "CharToken" }))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("CtxL2Key", "CtxLeft2"))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("CtxL1Key", "CtxLeft1"))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("CtxR1Key", "CtxRight1"))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("CtxR2Key", "CtxRight2"))
                .Append(_mlContext.Transforms.Conversion.ConvertType("CtxL2F", "CtxL2Key", Microsoft.ML.Data.DataKind.Single))
                .Append(_mlContext.Transforms.Conversion.ConvertType("CtxL1F", "CtxL1Key", Microsoft.ML.Data.DataKind.Single))
                .Append(_mlContext.Transforms.Conversion.ConvertType("CtxR1F", "CtxR1Key", Microsoft.ML.Data.DataKind.Single))
                .Append(_mlContext.Transforms.Conversion.ConvertType("CtxR2F", "CtxR2Key", Microsoft.ML.Data.DataKind.Single))
                .Append(_mlContext.Transforms.Concatenate("Features",
                    "CharTokenFeats", "CtxL2F", "CtxL1F", "CtxR1F", "CtxR2F", "Position"))
                .Append(_mlContext.MulticlassClassification.Trainers.LightGbm(
                    new LightGbmMulticlassTrainer.Options
                    {
                        LabelColumnName = "LabelKey",
                        FeatureColumnName = "Features",
                        NumberOfLeaves = p.NerLeaves,
                        NumberOfIterations = p.NerIterations,
                        LearningRate = p.NerLearningRate
                    }))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
        }

        private static List<NerInput> ExpandToCharLevel(IReadOnlyList<NerAnnotation> samples)
        {
            var result = new List<NerInput>();

            foreach (var sample in samples)
            {
                string text = sample.Text;
                var chars = text.ToCharArray();

                var charLabels = new string[chars.Length];
                for (int i = 0; i < chars.Length; i++)
                    charLabels[i] = "O";

                foreach (var entity in sample.Entities)
                {
                    for (int i = entity.StartIndex; i <= entity.EndIndex && i < chars.Length; i++)
                    {
                        charLabels[i] = i == entity.StartIndex
                            ? $"B-{entity.EntityType}"
                            : $"I-{entity.EntityType}";
                    }
                }

                for (int i = 0; i < chars.Length; i++)
                {
                    result.Add(new NerInput
                    {
                        CharToken = chars[i].ToString(),
                        Position = (float)i / chars.Length,
                        CtxLeft2 = i >= 2 ? chars[i - 2].ToString() : "[PAD]",
                        CtxLeft1 = i >= 1 ? chars[i - 1].ToString() : "[PAD]",
                        CtxRight1 = i + 1 < chars.Length ? chars[i + 1].ToString() : "[PAD]",
                        CtxRight2 = i + 2 < chars.Length ? chars[i + 2].ToString() : "[PAD]",
                        Label = charLabels[i]
                    });
                }
            }

            return result;
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
}
