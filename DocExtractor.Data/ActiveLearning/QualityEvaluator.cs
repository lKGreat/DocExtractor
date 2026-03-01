using System;
using System.Collections.Generic;
using System.Linq;
using DocExtractor.Data.Repositories;
using Newtonsoft.Json;

namespace DocExtractor.Data.ActiveLearning
{
    /// <summary>
    /// 质量评估器：基于标注数据集计算 NER 模型的精确率、召回率、F1
    /// 支持 entity-level 精确匹配，以及按实体类型分项统计
    /// </summary>
    public class QualityEvaluator
    {
        /// <summary>
        /// 在给定场景的已验证标注样本上评估模型质量
        /// 使用 k-fold 交叉验证以避免过拟合
        /// </summary>
        public NlpQualityMetrics EvaluateOnAnnotations(
            List<NlpAnnotatedText> samples,
            Func<string, List<ActiveEntityAnnotation>> predictor,
            int kFolds = 5)
        {
            if (samples.Count == 0)
                return new NlpQualityMetrics();

            if (samples.Count < kFolds * 2)
                return EvaluateAll(samples, predictor);

            var shuffled = samples.OrderBy(_ => Guid.NewGuid()).ToList();
            var foldSize = shuffled.Count / kFolds;

            var allTp = new Dictionary<string, int>();
            var allFp = new Dictionary<string, int>();
            var allFn = new Dictionary<string, int>();

            for (int fold = 0; fold < kFolds; fold++)
            {
                var testSet = shuffled.Skip(fold * foldSize).Take(foldSize).ToList();
                foreach (var sample in testSet)
                {
                    var gold      = DeserializeAnnotations(sample.AnnotationsJson);
                    var predicted = predictor(sample.RawText);
                    AccumulateMetrics(gold, predicted, allTp, allFp, allFn);
                }
            }

            return ComputeMetrics(allTp, allFp, allFn, samples.Count);
        }

        public NlpQualityMetrics EvaluateAll(
            List<NlpAnnotatedText> samples,
            Func<string, List<ActiveEntityAnnotation>> predictor)
        {
            var allTp = new Dictionary<string, int>();
            var allFp = new Dictionary<string, int>();
            var allFn = new Dictionary<string, int>();

            foreach (var sample in samples)
            {
                var gold      = DeserializeAnnotations(sample.AnnotationsJson);
                var predicted = predictor(sample.RawText);
                AccumulateMetrics(gold, predicted, allTp, allFp, allFn);
            }

            return ComputeMetrics(allTp, allFp, allFn, samples.Count);
        }

        // ── 内部计算 ─────────────────────────────────────────────────────────

        private static void AccumulateMetrics(
            List<ActiveEntityAnnotation> gold,
            List<ActiveEntityAnnotation> predicted,
            Dictionary<string, int> tp,
            Dictionary<string, int> fp,
            Dictionary<string, int> fn)
        {
            var goldByType = gold.GroupBy(e => e.EntityType).ToDictionary(g => g.Key, g => g.ToList());
            var predByType = predicted.GroupBy(e => e.EntityType).ToDictionary(g => g.Key, g => g.ToList());
            var allTypes   = new HashSet<string>(goldByType.Keys.Union(predByType.Keys));

            foreach (var type in allTypes)
            {
                var goldList = goldByType.TryGetValue(type, out var gl) ? gl : new List<ActiveEntityAnnotation>();
                var predList = predByType.TryGetValue(type, out var pl) ? pl : new List<ActiveEntityAnnotation>();

                int matched = 0;
                var usedPred = new HashSet<int>();

                foreach (var g in goldList)
                {
                    for (int pi = 0; pi < predList.Count; pi++)
                    {
                        if (usedPred.Contains(pi)) continue;
                        if (IsMatch(g, predList[pi]))
                        {
                            matched++;
                            usedPred.Add(pi);
                            break;
                        }
                    }
                }

                if (!tp.ContainsKey(type)) { tp[type] = 0; fp[type] = 0; fn[type] = 0; }
                tp[type] += matched;
                fp[type] += Math.Max(0, predList.Count - matched);
                fn[type] += Math.Max(0, goldList.Count - matched);
            }
        }

        private static bool IsMatch(ActiveEntityAnnotation gold, ActiveEntityAnnotation pred) =>
            string.Equals(gold.EntityType, pred.EntityType, StringComparison.OrdinalIgnoreCase)
            && gold.StartIndex == pred.StartIndex
            && gold.EndIndex == pred.EndIndex;

        private static NlpQualityMetrics ComputeMetrics(
            Dictionary<string, int> tp,
            Dictionary<string, int> fp,
            Dictionary<string, int> fn,
            int sampleCount)
        {
            int totalTp = tp.Values.Sum();
            int totalFp = fp.Values.Sum();
            int totalFn = fn.Values.Sum();

            double precision = (totalTp + totalFp) > 0 ? (double)totalTp / (totalTp + totalFp) : 0;
            double recall    = (totalTp + totalFn) > 0 ? (double)totalTp / (totalTp + totalFn) : 0;
            double f1        = (precision + recall) > 0 ? 2 * precision * recall / (precision + recall) : 0;

            var perTypeF1 = new Dictionary<string, double>();
            var perTypeSupport = new Dictionary<string, int>();
            foreach (var type in tp.Keys)
            {
                int tpT = tp[type];
                int fpT = fp.TryGetValue(type, out int v1) ? v1 : 0;
                int fnT = fn.TryGetValue(type, out int v2) ? v2 : 0;
                double p = (tpT + fpT) > 0 ? (double)tpT / (tpT + fpT) : 0;
                double r = (tpT + fnT) > 0 ? (double)tpT / (tpT + fnT) : 0;
                perTypeF1[type] = (p + r) > 0 ? 2 * p * r / (p + r) : 0;
                perTypeSupport[type] = tpT + fnT;
            }

            double macroF1 = perTypeF1.Count == 0 ? 0 : perTypeF1.Values.Average();

            return new NlpQualityMetrics
            {
                F1          = Math.Round(f1, 4),
                MicroF1     = Math.Round(f1, 4),
                MacroF1     = Math.Round(macroF1, 4),
                Precision   = Math.Round(precision, 4),
                Recall      = Math.Round(recall, 4),
                SampleCount = sampleCount,
                PerTypeF1   = perTypeF1,
                PerTypeSupport = perTypeSupport
            };
        }

        private static List<ActiveEntityAnnotation> DeserializeAnnotations(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<ActiveEntityAnnotation>();
            try { return JsonConvert.DeserializeObject<List<ActiveEntityAnnotation>>(json) ?? new List<ActiveEntityAnnotation>(); }
            catch { return new List<ActiveEntityAnnotation>(); }
        }
    }
}
