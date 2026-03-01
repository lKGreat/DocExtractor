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

        // NAS-BERT 句级推理引擎（仅标签）
        private PredictionEngine<NerWordInput, NerWordOutput>? _wordEngine;
        // NAS-BERT 句级推理引擎（含置信度分数）
        private PredictionEngine<NerWordInput, NerWordOutputWithScore>? _wordEngineWithScore;

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
                    _wordEngineWithScore?.Dispose();
                    _charEngine?.Dispose();
                    _model = _mlContext.Model.Load(modelPath, out _);
                    _wordEngine = _mlContext.Model.CreatePredictionEngine<NerWordInput, NerWordOutput>(_model);
                    try
                    {
                        _wordEngineWithScore = _mlContext.Model.CreatePredictionEngine<NerWordInput, NerWordOutputWithScore>(_model);
                    }
                    catch
                    {
                        _wordEngineWithScore = null;
                    }
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

        /// <summary>
        /// 带真实置信度的提取：利用 NAS-BERT softmax 分数填充 NamedEntity.Confidence
        /// 返回包含精确置信度的实体列表（而非硬编码的 0.8f）
        /// </summary>
        public IReadOnlyList<NamedEntity> ExtractWithConfidence(string text)
        {
            var labelEntities = ExtractLabelEntitiesWithConfidence(text);
            var result = new List<NamedEntity>(labelEntities.Count);
            foreach (var item in labelEntities)
            {
                if (!TryMapEntityType(item.Label, out var mappedType))
                    continue;

                result.Add(new NamedEntity
                {
                    Text = item.Text,
                    Type = mappedType,
                    StartIndex = item.StartIndex,
                    EndIndex = item.EndIndex,
                    Confidence = item.Confidence
                });
            }
            return result;
        }

        /// <summary>
        /// 字符串标签实体提取（主动学习链路使用）。
        /// 支持自定义标签，不依赖 EntityType 枚举。
        /// </summary>
        public IReadOnlyList<LabelEntity> ExtractLabelEntitiesWithConfidence(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<LabelEntity>();

            var ruleEntities = _ruleExtractor.Extract(text)
                .Select(e => new LabelEntity
                {
                    Text = e.Text,
                    Label = e.Type.ToString(),
                    StartIndex = e.StartIndex,
                    EndIndex = e.EndIndex,
                    Confidence = e.Confidence
                })
                .ToList();

            if (!IsLoaded) return ruleEntities;

            var mlEntities = _useCharLevel
                ? ExtractLabelEntitiesWithCharLevel(text)
                : ExtractLabelEntitiesWithWordLevelAndConfidence(text);

            return MergeLabelEntities(ruleEntities, mlEntities);
        }

        /// <summary>
        /// 计算整段文本的平均预测置信度（用于不确定性采样）
        /// 值越低说明模型越不确定
        /// </summary>
        public float ComputeTextConfidence(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !IsLoaded || _useCharLevel || _wordEngineWithScore == null)
                return 0.5f;

            try
            {
                var chars = text.ToCharArray();
                string spaceSeparated = string.Join(" ", chars.Select(c => c.ToString()));

                NerWordOutputWithScore output;
                lock (_lock)
                {
                    if (_wordEngineWithScore == null) return 0.5f;
                    var input = new NerWordInput { Sentence = spaceSeparated };
                    output = _wordEngineWithScore.Predict(input);
                }

                if (output.Score == null || output.Score.Length == 0) return 0.5f;

                // Score 是 [numTokens * numLabels] 的展平数组或每个 token 的最高分
                // 使用每个 token 最高分的均值作为置信度
                int numTokens = chars.Length;
                int numLabels = output.Score.Length / numTokens;
                if (numLabels <= 0) return 0.5f;

                float totalConf = 0f;
                for (int i = 0; i < numTokens; i++)
                {
                    float max = float.MinValue;
                    for (int j = 0; j < numLabels; j++)
                    {
                        int idx = i * numLabels + j;
                        if (idx < output.Score.Length && output.Score[idx] > max)
                            max = output.Score[idx];
                    }
                    if (max > float.MinValue) totalConf += max;
                }
                return numTokens > 0 ? totalConf / numTokens : 0.5f;
            }
            catch
            {
                return 0.5f;
            }
        }

        /// <summary>NAS-BERT 整句推理（带置信度）</summary>
        private IReadOnlyList<NamedEntity> ExtractWithWordLevelAndConfidence(string text)
        {
            // 如果带置信度引擎不可用，降级到普通推理
            if (_wordEngineWithScore == null) return ExtractWithWordLevel(text);

            var chars = text.ToCharArray();
            string spaceSeparated = string.Join(" ", chars.Select(c => c.ToString()));

            NerWordOutputWithScore output;
            lock (_lock)
            {
                if (_wordEngineWithScore == null) return ExtractWithWordLevel(text);
                var input = new NerWordInput { Sentence = spaceSeparated };
                output = _wordEngineWithScore.Predict(input);
            }

            if (output.PredictedLabel == null || output.PredictedLabel.Length == 0)
                return Array.Empty<NamedEntity>();

            return DecodeBIOSequenceWithScore(text, output.PredictedLabel.ToList(), output.Score);
        }

        /// <summary>NAS-BERT 整句推理（字符串标签 + 置信度）</summary>
        private IReadOnlyList<LabelEntity> ExtractLabelEntitiesWithWordLevelAndConfidence(string text)
        {
            if (_wordEngineWithScore == null) return ExtractLabelEntitiesWithWordLevel(text);

            var chars = text.ToCharArray();
            string spaceSeparated = string.Join(" ", chars.Select(c => c.ToString()));

            NerWordOutputWithScore output;
            lock (_lock)
            {
                if (_wordEngineWithScore == null) return ExtractLabelEntitiesWithWordLevel(text);
                var input = new NerWordInput { Sentence = spaceSeparated };
                output = _wordEngineWithScore.Predict(input);
            }

            if (output.PredictedLabel == null || output.PredictedLabel.Length == 0)
                return Array.Empty<LabelEntity>();

            return DecodeBIOSequenceWithScoreToLabels(text, output.PredictedLabel.ToList(), output.Score);
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

        /// <summary>旧格式字符级逐字预测（字符串标签）</summary>
        private IReadOnlyList<LabelEntity> ExtractLabelEntitiesWithCharLevel(string text)
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

            return DecodeBIOSequenceToLabels(text, predictions);
        }

        /// <summary>NAS-BERT 整句推理（字符串标签）</summary>
        private IReadOnlyList<LabelEntity> ExtractLabelEntitiesWithWordLevel(string text)
        {
            var chars = text.ToCharArray();
            string spaceSeparated = string.Join(" ", chars.Select(c => c.ToString()));

            NerWordOutput output;
            lock (_lock)
            {
                var input = new NerWordInput { Sentence = spaceSeparated };
                output = _wordEngine!.Predict(input);
            }

            if (output.PredictedLabel == null || output.PredictedLabel.Length == 0)
                return Array.Empty<LabelEntity>();

            return DecodeBIOSequenceToLabels(text, output.PredictedLabel.ToList());
        }

        /// <summary>BIO 序列解码（带置信度分数）</summary>
        private static IReadOnlyList<NamedEntity> DecodeBIOSequenceWithScore(string text, List<string> tags, float[]? scores)
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

                    while (end + 1 < tags.Count && tags[end + 1] == "I-" + entityTypeStr)
                        end++;

                    int safeEnd = Math.Min(end, text.Length - 1);
                    if (Enum.TryParse<EntityType>(entityTypeStr, true, out var entityType))
                    {
                        // 计算该实体跨越 tokens 的平均置信度
                        float conf = 0.8f;
                        if (scores != null && scores.Length > 0)
                        {
                            float sum = 0f;
                            int count = 0;
                            for (int k = start; k <= end && k < scores.Length; k++)
                            {
                                sum += scores[k];
                                count++;
                            }
                            if (count > 0) conf = sum / count;
                        }

                        entities.Add(new NamedEntity
                        {
                            Text = text.Substring(start, safeEnd - start + 1),
                            Type = entityType,
                            StartIndex = start,
                            EndIndex = safeEnd,
                            Confidence = conf
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

        private static IReadOnlyList<LabelEntity> DecodeBIOSequenceWithScoreToLabels(string text, List<string> tags, float[]? scores)
        {
            var entities = new List<LabelEntity>();
            int i = 0;

            while (i < tags.Count)
            {
                string tag = tags[i];
                if (!tag.StartsWith("B-", StringComparison.Ordinal))
                {
                    i++;
                    continue;
                }

                string label = tag.Substring(2);
                int start = i;
                int end = i;
                while (end + 1 < tags.Count && tags[end + 1] == "I-" + label)
                    end++;

                int safeEnd = Math.Min(end, text.Length - 1);
                float conf = 0.8f;
                if (scores != null && scores.Length > 0)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int k = start; k <= end && k < scores.Length; k++)
                    {
                        sum += scores[k];
                        count++;
                    }
                    if (count > 0) conf = sum / count;
                }

                entities.Add(new LabelEntity
                {
                    Label = label,
                    Text = text.Substring(start, safeEnd - start + 1),
                    StartIndex = start,
                    EndIndex = safeEnd,
                    Confidence = conf
                });
                i = end + 1;
            }

            return entities;
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

        private static IReadOnlyList<LabelEntity> DecodeBIOSequenceToLabels(string text, List<string> tags)
        {
            var entities = new List<LabelEntity>();
            int i = 0;

            while (i < tags.Count)
            {
                string tag = tags[i];
                if (!tag.StartsWith("B-", StringComparison.Ordinal))
                {
                    i++;
                    continue;
                }

                string label = tag.Substring(2);
                int start = i;
                int end = i;
                while (end + 1 < tags.Count && tags[end + 1] == "I-" + label)
                    end++;

                int safeEnd = Math.Min(end, text.Length - 1);
                entities.Add(new LabelEntity
                {
                    Label = label,
                    Text = text.Substring(start, safeEnd - start + 1),
                    StartIndex = start,
                    EndIndex = safeEnd,
                    Confidence = 0.8f
                });

                i = end + 1;
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

        private static IReadOnlyList<LabelEntity> MergeLabelEntities(
            IReadOnlyList<LabelEntity> ruleEntities,
            IReadOnlyList<LabelEntity> mlEntities)
        {
            var result = new List<LabelEntity>(ruleEntities);
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

        private static bool TryMapEntityType(string label, out EntityType entityType)
        {
            return Enum.TryParse(label, true, out entityType);
        }

        public void Dispose()
        {
            _wordEngine?.Dispose();
            _wordEngineWithScore?.Dispose();
            _charEngine?.Dispose();
        }
    }

    /// <summary>
    /// 主动学习链路使用的字符串标签实体。
    /// 与 EntityType 枚举解耦，支持自定义场景标签。
    /// </summary>
    public class LabelEntity
    {
        public string Label { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public float Confidence { get; set; }
    }
}
