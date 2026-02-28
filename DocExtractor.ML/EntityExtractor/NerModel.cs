using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML;
using DocExtractor.Core.Interfaces;

namespace DocExtractor.ML.EntityExtractor
{
    /// <summary>
    /// 基于 NAS-BERT 的命名实体识别模型
    /// 将单元格文本拆分为字符序列（空格分隔） → 整句预测 BIO 标签 → 组合成实体
    /// 兼容旧格式（字符级 LightGBM）的降级加载
    /// </summary>
    public class NerModel : IEntityExtractor, IDisposable
    {
        private readonly MLContext _mlContext;
        private ITransformer? _model;

        // NAS-BERT 句级推理引擎
        private PredictionEngine<NerWordInput, NerWordOutput>? _wordEngine;

        // 旧格式字符级推理引擎（降级兼容）
        private PredictionEngine<NerInput, NerPrediction>? _charEngine;
        private bool _useCharLevel = false;

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
                try
                {
                    // 尝试加载为 NAS-BERT 句级模型
                    _wordEngine?.Dispose();
                    _charEngine?.Dispose();
                    _model = _mlContext.Model.Load(modelPath, out _);
                    _wordEngine = _mlContext.Model.CreatePredictionEngine<NerWordInput, NerWordOutput>(_model);
                    _charEngine = null;
                    _useCharLevel = false;
                }
                catch
                {
                    try
                    {
                        // 降级：尝试加载为旧格式字符级模型
                        _model = _mlContext.Model.Load(modelPath, out _);
                        _charEngine = _mlContext.Model.CreatePredictionEngine<NerInput, NerPrediction>(_model);
                        _wordEngine = null;
                        _useCharLevel = true;
                    }
                    catch
                    {
                        // 两种格式都不兼容，需要重新训练
                        _model = null;
                        _wordEngine = null;
                        _charEngine = null;
                        _useCharLevel = false;
                    }
                }
            }
        }

        public IReadOnlyList<NamedEntity> Extract(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<NamedEntity>();

            // 1. 规则引擎（正则）先处理高置信度模式
            var ruleEntities = _ruleExtractor.Extract(text);

            // 2. 如果ML模型已加载，用ML处理规则未覆盖的区域
            if (!IsLoaded) return ruleEntities;

            var mlEntities = _useCharLevel ? ExtractWithCharLevel(text) : ExtractWithWordLevel(text);

            // 3. 合并：规则优先，ML填补空白
            return MergeEntities(ruleEntities, mlEntities);
        }

        public IReadOnlyList<NamedEntity> Extract(string text, EntityType type)
        {
            return Extract(text).Where(e => e.Type == type).ToList();
        }

        /// <summary>NAS-BERT 整句推理</summary>
        private IReadOnlyList<NamedEntity> ExtractWithWordLevel(string text)
        {
            var chars = text.ToCharArray();
            // 每个字符用空格分隔，与训练时格式一致
            string spaceSeparated = string.Join(" ", chars.Select(c => c.ToString()));

            NerWordOutput output;
            lock (_lock)
            {
                var input = new NerWordInput { Sentence = spaceSeparated };
                output = _wordEngine!.Predict(input);
            }

            if (output.PredictedLabel == null || output.PredictedLabel.Length == 0)
                return Array.Empty<NamedEntity>();

            // BIO 标签数组 → 实体列表
            return DecodeBIOSequence(text, output.PredictedLabel.ToList());
        }

        /// <summary>旧格式字符级逐字预测（降级兼容）</summary>
        private IReadOnlyList<NamedEntity> ExtractWithCharLevel(string text)
        {
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

            List<string> predictions;
            lock (_lock)
            {
                predictions = new List<string>(chars.Length);
                foreach (var inp in inputs)
                    predictions.Add(_charEngine!.Predict(inp).PredictedLabel);
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

                    // 边界保护：确保 end 不超出 text 长度
                    int safeEnd = Math.Min(end, text.Length - 1);
                    if (Enum.TryParse<EntityType>(entityTypeStr, true, out var entityType))
                    {
                        entities.Add(new NamedEntity
                        {
                            Text = text.Substring(start, safeEnd - start + 1),
                            Type = entityType,
                            StartIndex = start,
                            EndIndex = safeEnd,
                            Confidence = 0.8f
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
                bool overlaps = coveredRanges.Any(r =>
                    mlEnt.StartIndex <= r.End && mlEnt.EndIndex >= r.Start);
                if (!overlaps)
                    result.Add(mlEnt);
            }

            return result.OrderBy(e => e.StartIndex).ToList();
        }

        public void Dispose()
        {
            _wordEngine?.Dispose();
            _charEngine?.Dispose();
        }
    }
}
