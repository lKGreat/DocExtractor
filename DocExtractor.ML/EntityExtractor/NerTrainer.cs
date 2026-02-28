using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.ML.Transforms.Text;

namespace DocExtractor.ML.EntityExtractor
{
    /// <summary>
    /// NER 模型训练器
    /// 输入：BIO 标注的字符级训练数据
    /// 使用：LightGBM（FastTree）梯度提升分类
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
        /// <param name="annotatedSamples">标注样本（文本 → BIO标签序列）</param>
        /// <param name="modelSavePath">模型保存路径</param>
        /// <param name="progress">进度报告</param>
        public NerTrainingResult Train(
            IReadOnlyList<NerAnnotation> annotatedSamples,
            string modelSavePath,
            IProgress<string>? progress = null)
        {
            // 将样本展开为字符级序列
            var charSamples = ExpandToCharLevel(annotatedSamples);

            progress?.Report($"字符级样本: {charSamples.Count} 个字符标记");

            if (charSamples.Count < 50)
                throw new InvalidOperationException($"NER 训练数据不足，至少需要50个字符标注，当前 {charSamples.Count}");

            var dataView = _mlContext.Data.LoadFromEnumerable(charSamples);
            var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);

            progress?.Report("构建 NER Pipeline (LightGBM)...");

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "LabelKey",
                    inputColumnName: "Label")
                .Append(_mlContext.Transforms.Text.FeaturizeText(
                    "CharFeats",
                    options: new TextFeaturizingEstimator.Options
                    {
                        CharFeatureExtractor = new WordBagEstimator.Options { NgramLength = 3, UseAllLengths = true },
                        WordFeatureExtractor = null
                    },
                    inputColumnNames: new[] { "CharToken", "CtxLeft1", "CtxLeft2", "CtxRight1", "CtxRight2" }))
                .Append(_mlContext.Transforms.Concatenate("Features", "CharFeats"))
                .Append(_mlContext.MulticlassClassification.Trainers.LightGbm(
                    new LightGbmMulticlassTrainer.Options
                    {
                        LabelColumnName = "LabelKey",
                        FeatureColumnName = "Features",
                        NumberOfLeaves = 31,
                        NumberOfIterations = 100,
                        LearningRate = 0.1
                    }))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            progress?.Report("开始训练（CPU LightGBM）...");
            var model = pipeline.Fit(split.TrainSet);

            progress?.Report("评估模型...");
            var predictions = model.Transform(split.TestSet);
            var metrics = _mlContext.MulticlassClassification.Evaluate(
                predictions,
                labelColumnName: "LabelKey",
                predictedLabelColumnName: "PredictedLabel");

            Directory.CreateDirectory(Path.GetDirectoryName(modelSavePath)!);
            _mlContext.Model.Save(model, dataView.Schema, modelSavePath);

            progress?.Report("NER 模型训练完成！");

            return new NerTrainingResult
            {
                MicroAccuracy = metrics.MicroAccuracy,
                MacroAccuracy = metrics.MacroAccuracy,
                CharSampleCount = charSamples.Count,
                TextSampleCount = annotatedSamples.Count,
                LabelTypes = charSamples.Select(c => c.Label).Distinct().OrderBy(l => l).ToList()
            };
        }

        private static List<NerInput> ExpandToCharLevel(IReadOnlyList<NerAnnotation> samples)
        {
            var result = new List<NerInput>();

            foreach (var sample in samples)
            {
                string text = sample.Text;
                var chars = text.ToCharArray();

                // 构建字符位置→BIO标签的映射
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

                // 生成 NerInput 序列
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
        public string EntityType { get; set; } = string.Empty; // "Value", "Unit", "HexCode" 等
        public string Text { get; set; } = string.Empty;
    }

    public class NerTrainingResult
    {
        public double MicroAccuracy { get; set; }
        public double MacroAccuracy { get; set; }
        public int CharSampleCount { get; set; }
        public int TextSampleCount { get; set; }
        public List<string> LabelTypes { get; set; } = new List<string>();

        public override string ToString() =>
            $"准确率(Micro): {MicroAccuracy:P2} | 文本样本: {TextSampleCount} | 字符标记: {CharSampleCount}";
    }
}
