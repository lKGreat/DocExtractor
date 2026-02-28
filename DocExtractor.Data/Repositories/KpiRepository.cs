using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DocExtractor.Data.Repositories
{
    /// <summary>
    /// KPI 诊断数据聚合仓储。
    /// </summary>
    public class KpiRepository
    {
        private readonly string _dbPath;
        private readonly string _modelsDir;

        public KpiRepository(string dbPath, string modelsDir)
        {
            _dbPath = dbPath;
            _modelsDir = modelsDir;
        }

        public KpiSnapshot GetSnapshot()
        {
            var snapshot = new KpiSnapshot();

            using var trainRepo = new TrainingDataRepository(_dbPath);
            using var knowledgeRepo = new GroupKnowledgeRepository(_dbPath);

            snapshot.ColumnSamples = trainRepo.GetColumnSampleCount();
            snapshot.NerSamples = trainRepo.GetNerSampleCount();
            snapshot.SectionSamples = trainRepo.GetSectionSampleCount();
            snapshot.KnowledgeCount = knowledgeRepo.GetKnowledgeCount();

            snapshot.ColumnModelExists = File.Exists(Path.Combine(_modelsDir, "column_classifier.zip"));
            snapshot.NerModelExists = File.Exists(Path.Combine(_modelsDir, "ner_model.zip"));
            snapshot.SectionModelExists = File.Exists(Path.Combine(_modelsDir, "section_classifier.zip"));

            var colLatest = trainRepo.GetLatestRecord("ColumnClassifier");
            snapshot.ColumnAccuracy = TryExtractPercent(colLatest?.MetricsJson);

            // 当前仓库没有自动覆盖率上报，默认 0（由 CI 未来接入）
            snapshot.TestCoverage = 0;

            if (snapshot.TestCoverage < 70)
                snapshot.Suggestions.Add("高：补齐测试覆盖率并接入 CI 质量门禁");
            if (snapshot.ColumnSamples < 100)
                snapshot.Suggestions.Add("中：持续积累列名标注样本以稳定准确率");
            if (snapshot.KnowledgeCount < 5000)
                snapshot.Suggestions.Add("低：继续导入文档扩充知识库规模");

            return snapshot;
        }

        private static double? TryExtractPercent(string? metrics)
        {
            if (string.IsNullOrWhiteSpace(metrics)) return null;
            var match = Regex.Match(metrics, @"(\d+(?:\.\d+)?)%");
            if (!match.Success) return null;
            return double.TryParse(match.Groups[1].Value, out var pct) ? pct : (double?)null;
        }
    }

    public class KpiSnapshot
    {
        public int ColumnSamples { get; set; }
        public int NerSamples { get; set; }
        public int SectionSamples { get; set; }
        public int KnowledgeCount { get; set; }
        public bool ColumnModelExists { get; set; }
        public bool NerModelExists { get; set; }
        public bool SectionModelExists { get; set; }
        public double? ColumnAccuracy { get; set; }
        public double TestCoverage { get; set; }
        public List<string> Suggestions { get; set; } = new List<string>();
    }
}
