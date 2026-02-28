using System;
using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Core.Normalization;

namespace DocExtractor.ML.Recommendation
{
    /// <summary>
    /// 基于历史抽取数据的组名→细则项推荐引擎
    /// 使用字符 bigram Jaccard 相似度匹配相似组名，聚合推荐细则项
    /// </summary>
    public class GroupItemRecommender
    {
        private readonly IValueNormalizer _valueNormalizer;

        private static readonly FieldDefinition RequiredValueField = new FieldDefinition
        {
            FieldName = "RequiredValue",
            DisplayName = "要求值",
            DataType = FieldDataType.Text
        };

        /// <summary>相似度阈值，低于此值的组名不参与推荐</summary>
        public float SimilarityThreshold { get; set; } = 0.25f;

        public GroupItemRecommender(IValueNormalizer? valueNormalizer = null)
        {
            _valueNormalizer = valueNormalizer ?? new DefaultValueNormalizer();
        }

        /// <summary>
        /// 根据输入组名推荐细则项
        /// </summary>
        /// <param name="inputGroupName">输入的组名</param>
        /// <param name="allGroupNames">知识库中所有唯一组名（归一化后）</param>
        /// <param name="getItemsForGroup">获取某组名下所有项的回调</param>
        /// <returns>推荐列表（按置信度降序），空列表表示无匹配</returns>
        public List<RecommendedItem> Recommend(
            string inputGroupName,
            IReadOnlyList<string> allGroupNames,
            Func<string, IReadOnlyList<KnowledgeItem>> getItemsForGroup)
        {
            if (string.IsNullOrWhiteSpace(inputGroupName) || allGroupNames.Count == 0)
                return new List<RecommendedItem>();

            string normalized = NormalizeGroupName(inputGroupName);

            // 计算相似度并过滤
            var matches = new List<(string Name, float Score)>();
            foreach (var g in allGroupNames)
            {
                float score = CharBigramJaccard(normalized, g);
                if (score >= SimilarityThreshold)
                    matches.Add((g, score));
            }

            matches.Sort((a, b) => b.Score.CompareTo(a.Score));

            if (matches.Count == 0)
                return new List<RecommendedItem>();

            // 聚合匹配组的细则项
            var itemMap = new Dictionary<string, RecommendedItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var match in matches)
            {
                var items = getItemsForGroup(match.Name);
                foreach (var item in items)
                {
                    string key = item.ItemName.Trim();
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    RecommendedItem rec;
                    if (!itemMap.TryGetValue(key, out rec))
                    {
                        rec = new RecommendedItem { ItemName = key };
                        itemMap[key] = rec;
                    }

                    string? normalizedRequiredValue = null;
                    if (!string.IsNullOrWhiteSpace(item.RequiredValue))
                    {
                        normalizedRequiredValue = _valueNormalizer.Normalize(
                            item.RequiredValue,
                            RequiredValueField);
                    }

                    rec.AddEvidence(match.Score, normalizedRequiredValue ?? string.Empty, item.SourceFile);
                }
            }

            var result = new List<RecommendedItem>(itemMap.Values);
            result.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            return result;
        }

        /// <summary>归一化组名：去除首尾空白、序号前缀</summary>
        public static string NormalizeGroupName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            string trimmed = name.Trim();
            int i = 0;
            while (i < trimmed.Length && (char.IsDigit(trimmed[i]) || trimmed[i] == '.'
                                           || trimmed[i] == ' ' || trimmed[i] == '\u3001'
                                           || trimmed[i] == '\uff0e'))
                i++;
            string result = i < trimmed.Length ? trimmed.Substring(i).Trim() : trimmed;
            return result.Length > 0 ? result : trimmed;
        }

        /// <summary>字符 bigram Jaccard 相似度（中文友好）</summary>
        public static float CharBigramJaccard(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0f;
            if (a == b) return 1.0f;

            var setA = GetCharBigrams(a);
            var setB = GetCharBigrams(b);

            int intersection = 0;
            foreach (var bg in setA)
            {
                if (setB.Contains(bg))
                    intersection++;
            }

            int union = setA.Count + setB.Count - intersection;
            return union == 0 ? 0f : (float)intersection / union;
        }

        private static HashSet<string> GetCharBigrams(string text)
        {
            var set = new HashSet<string>();
            for (int i = 0; i < text.Length - 1; i++)
                set.Add(text.Substring(i, 2));
            return set;
        }
    }

    /// <summary>知识库中的单条细则项（轻量传输对象）</summary>
    public class KnowledgeItem
    {
        public string ItemName { get; set; } = string.Empty;
        public string RequiredValue { get; set; }
        public string SourceFile { get; set; }
    }

    /// <summary>推荐结果项</summary>
    public class RecommendedItem
    {
        public string ItemName { get; set; } = string.Empty;
        public string TypicalRequiredValue { get; set; }
        public float Confidence { get; set; }
        public int OccurrenceCount { get; set; }
        public List<string> SourceFiles { get; set; } = new List<string>();

        private readonly Dictionary<string, int> _requiredValueCounts = new Dictionary<string, int>();

        public void AddEvidence(float similarity, string requiredValue, string sourceFile)
        {
            OccurrenceCount++;

            // 置信度 = max(相似度) * 频率增益
            float rawSim = OccurrenceCount == 1
                ? similarity
                : Math.Max(Confidence / FrequencyBoost(OccurrenceCount - 1), similarity);
            Confidence = Math.Min(1f, rawSim * FrequencyBoost(OccurrenceCount));

            // 统计要求值频率
            if (!string.IsNullOrWhiteSpace(requiredValue))
            {
                string rv = requiredValue.Trim();
                if (_requiredValueCounts.ContainsKey(rv))
                    _requiredValueCounts[rv]++;
                else
                    _requiredValueCounts[rv] = 1;

                if (TypicalRequiredValue == null || _requiredValueCounts[rv] > GetCount(TypicalRequiredValue))
                    TypicalRequiredValue = rv;
            }

            if (!string.IsNullOrWhiteSpace(sourceFile) && !SourceFiles.Contains(sourceFile))
                SourceFiles.Add(sourceFile);
        }

        private int GetCount(string val)
        {
            int c;
            return _requiredValueCounts.TryGetValue(val, out c) ? c : 0;
        }

        /// <summary>频率增益（对数衰减，4 次封顶满分）</summary>
        private static float FrequencyBoost(int count)
        {
            if (count <= 0) return 0.7f;
            return Math.Min(1.0f, (float)(Math.Log(count + 1, 2) / Math.Log(5, 2)));
        }
    }
}
