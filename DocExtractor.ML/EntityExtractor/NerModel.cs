using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML;
using DocExtractor.Core.Interfaces;

namespace DocExtractor.ML.EntityExtractor
{
    /// <summary>
    /// 基于字符级BIO标注的命名实体识别模型
    /// 将单元格文本拆分为字符序列 → 预测每个字符的BIO标签 → 组合成实体
    /// </summary>
    public class NerModel : IEntityExtractor, IDisposable
    {
        private readonly MLContext _mlContext;
        private ITransformer? _model;
        private PredictionEngine<NerInput, NerPrediction>? _engine;
        private readonly object _lock = new object();

        public bool IsLoaded => _model != null;

        // 规则引擎（先于ML运行，处理高置信度的模式匹配）
        private static readonly RuleBasedEntityExtractor _ruleExtractor = new RuleBasedEntityExtractor();

        public NerModel()
        {
            _mlContext = new MLContext(seed: 42);
        }

        public void Load(string modelPath)
        {
            if (!File.Exists(modelPath)) return;
            lock (_lock)
            {
                _engine?.Dispose();
                _model = _mlContext.Model.Load(modelPath, out _);
                _engine = _mlContext.Model.CreatePredictionEngine<NerInput, NerPrediction>(_model);
            }
        }

        public IReadOnlyList<NamedEntity> Extract(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<NamedEntity>();

            // 1. 规则引擎（正则）先处理高置信度模式
            var ruleEntities = _ruleExtractor.Extract(text);

            // 2. 如果ML模型已加载，用ML处理规则未覆盖的区域
            if (!IsLoaded) return ruleEntities;

            var mlEntities = ExtractWithMl(text);

            // 3. 合并：规则优先，ML填补空白
            return MergeEntities(ruleEntities, mlEntities);
        }

        public IReadOnlyList<NamedEntity> Extract(string text, EntityType type)
        {
            return Extract(text).Where(e => e.Type == type).ToList();
        }

        private IReadOnlyList<NamedEntity> ExtractWithMl(string text)
        {
            // 字符化 → 生成 NerInput 序列 → 预测 → BIO解码
            var chars = text.ToCharArray();
            var inputs = new List<NerInput>(chars.Length);

            for (int i = 0; i < chars.Length; i++)
            {
                inputs.Add(new NerInput
                {
                    CharToken = chars[i].ToString(),
                    Position = (float)i / chars.Length,
                    CtxLeft2 = i >= 2 ? chars[i - 2].ToString() : "[PAD]",
                    CtxLeft1 = i >= 1 ? chars[i - 1].ToString() : "[PAD]",
                    CtxRight1 = i + 1 < chars.Length ? chars[i + 1].ToString() : "[PAD]",
                    CtxRight2 = i + 2 < chars.Length ? chars[i + 2].ToString() : "[PAD]"
                });
            }

            // 逐字符预测（串行，WinForms CPU 可接受短文本）
            var predictions = new List<string>(chars.Length);
            lock (_lock)
            {
                foreach (var inp in inputs)
                    predictions.Add(_engine!.Predict(inp).PredictedLabel);
            }

            return DecodeBIOSequence(text, predictions);
        }

        /// <summary>BIO 序列解码：将字符级标签还原为实体</summary>
        private static IReadOnlyList<NamedEntity> DecodeBIOSequence(string text, List<string> tags)
        {
            var entities = new List<NamedEntity>();
            int i = 0;

            while (i < tags.Count)
            {
                string tag = tags[i];
                if (tag.StartsWith("B-"))
                {
                    string entityTypeStr = tag.Substring(2);
                    int start = i;
                    int end = i;

                    // 收集连续的 I-XXX 标签
                    while (end + 1 < tags.Count && tags[end + 1] == "I-" + entityTypeStr)
                        end++;

                    if (Enum.TryParse<EntityType>(entityTypeStr, true, out var entityType))
                    {
                        entities.Add(new NamedEntity
                        {
                            Text = text.Substring(start, end - start + 1),
                            Type = entityType,
                            StartIndex = start,
                            EndIndex = end,
                            Confidence = 0.8f // ML 预测置信度估算
                        });
                    }
                    i = end + 1;
                }
                else
                {
                    i++;
                }
            }

            return entities;
        }

        private static IReadOnlyList<NamedEntity> MergeEntities(
            IReadOnlyList<NamedEntity> ruleEntities,
            IReadOnlyList<NamedEntity> mlEntities)
        {
            var result = new List<NamedEntity>(ruleEntities);
            var coveredRanges = ruleEntities.Select(e => (Start: e.StartIndex, End: e.EndIndex)).ToList();

            foreach (var mlEnt in mlEntities)
            {
                // 只添加规则未覆盖的区域
                bool overlaps = coveredRanges.Any(r =>
                    mlEnt.StartIndex <= r.End && mlEnt.EndIndex >= r.Start);
                if (!overlaps)
                    result.Add(mlEnt);
            }

            return result.OrderBy(e => e.StartIndex).ToList();
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }
    }
}
