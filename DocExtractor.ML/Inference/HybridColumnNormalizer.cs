using System;
using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.ML.ColumnClassifier;

namespace DocExtractor.ML.Inference
{
    /// <summary>
    /// 混合列名规范化器：规则匹配优先，置信度不足时降级到 ML 模型
    /// </summary>
    public class HybridColumnNormalizer : IColumnNormalizer
    {
        private readonly ColumnClassifierModel _mlModel;
        private readonly ColumnMatchMode _mode;

        /// <summary>ML 模型置信度阈值（低于此值降级到规则）</summary>
        private const float MlConfidenceThreshold = 0.6f;

        public HybridColumnNormalizer(
            ColumnClassifierModel mlModel,
            ColumnMatchMode mode = ColumnMatchMode.HybridMlFirst)
        {
            _mlModel = mlModel;
            _mode = mode;
        }

        public ColumnMappingResult? Normalize(string rawColumnName, IReadOnlyList<FieldDefinition> fields)
        {
            if (string.IsNullOrWhiteSpace(rawColumnName)) return null;

            string normalized = rawColumnName.Trim();

            return _mode switch
            {
                ColumnMatchMode.RuleOnly => TryRuleMatch(normalized, fields),
                ColumnMatchMode.MlOnly => TryMlMatch(normalized, fields),
                ColumnMatchMode.HybridMlFirst => TryMlFirst(normalized, fields),
                ColumnMatchMode.HybridRuleFirst => TryRuleFirst(normalized, fields),
                _ => TryRuleFirst(normalized, fields)
            };
        }

        public IReadOnlyList<ColumnMappingResult?> NormalizeBatch(
            IReadOnlyList<string> rawColumnNames,
            IReadOnlyList<FieldDefinition> fields)
        {
            return rawColumnNames.Select(n => Normalize(n, fields)).ToList();
        }

        // ── 策略实现 ──────────────────────────────────────────────────────────

        private ColumnMappingResult? TryMlFirst(string colName, IReadOnlyList<FieldDefinition> fields)
        {
            // 1. 精确匹配（最高优先）
            var exact = TryExactMatch(colName, fields);
            if (exact != null) return exact;

            // 2. ML 模型预测
            if (_mlModel.IsLoaded)
            {
                var (fieldName, confidence) = _mlModel.Predict(colName);
                if (fieldName != null && confidence >= MlConfidenceThreshold &&
                    fields.Any(f => f.FieldName == fieldName))
                {
                    return new ColumnMappingResult
                    {
                        RawName = colName,
                        CanonicalFieldName = fieldName,
                        Confidence = confidence,
                        MatchMethod = "ml"
                    };
                }
            }

            // 3. 降级：规则匹配（变体列表）
            return TryVariantMatch(colName, fields);
        }

        private ColumnMappingResult? TryRuleFirst(string colName, IReadOnlyList<FieldDefinition> fields)
        {
            return TryExactMatch(colName, fields)
                ?? TryVariantMatch(colName, fields)
                ?? TryMlMatch(colName, fields);
        }

        private ColumnMappingResult? TryRuleMatch(string colName, IReadOnlyList<FieldDefinition> fields)
        {
            return TryExactMatch(colName, fields) ?? TryVariantMatch(colName, fields);
        }

        private ColumnMappingResult? TryMlMatch(string colName, IReadOnlyList<FieldDefinition> fields)
        {
            if (!_mlModel.IsLoaded) return null;
            var (fieldName, confidence) = _mlModel.Predict(colName);
            if (fieldName == null || confidence < MlConfidenceThreshold) return null;
            if (!fields.Any(f => f.FieldName == fieldName)) return null;

            return new ColumnMappingResult
            {
                RawName = colName,
                CanonicalFieldName = fieldName,
                Confidence = confidence,
                MatchMethod = "ml"
            };
        }

        // ── 规则匹配 ──────────────────────────────────────────────────────────

        /// <summary>完全匹配：与规范名或显示名完全相同</summary>
        private static ColumnMappingResult? TryExactMatch(string colName, IReadOnlyList<FieldDefinition> fields)
        {
            foreach (var f in fields)
            {
                if (string.Equals(colName, f.FieldName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(colName, f.DisplayName, StringComparison.Ordinal))
                {
                    return new ColumnMappingResult
                    {
                        RawName = colName,
                        CanonicalFieldName = f.FieldName,
                        Confidence = 1.0f,
                        MatchMethod = "exact"
                    };
                }
            }
            return null;
        }

        /// <summary>变体列表匹配：在 KnownColumnVariants 中查找</summary>
        private static ColumnMappingResult? TryVariantMatch(string colName, IReadOnlyList<FieldDefinition> fields)
        {
            // 先精确匹配变体
            foreach (var f in fields)
            {
                foreach (var variant in f.KnownColumnVariants)
                {
                    if (string.Equals(colName, variant, StringComparison.Ordinal))
                        return new ColumnMappingResult
                        {
                            RawName = colName,
                            CanonicalFieldName = f.FieldName,
                            Confidence = 0.95f,
                            MatchMethod = "rule_exact"
                        };
                }
            }

            // 再包含匹配（列名包含变体关键词）
            foreach (var f in fields)
            {
                foreach (var variant in f.KnownColumnVariants)
                {
                    if (colName.Contains(variant) || variant.Contains(colName))
                        return new ColumnMappingResult
                        {
                            RawName = colName,
                            CanonicalFieldName = f.FieldName,
                            Confidence = 0.75f,
                            MatchMethod = "rule_contains"
                        };
                }
            }

            return null;
        }
    }
}
