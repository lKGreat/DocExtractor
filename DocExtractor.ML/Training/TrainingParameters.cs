namespace DocExtractor.ML.Training
{
    /// <summary>
    /// ML 模型训练参数（三个预设 + 自定义）
    /// 列名分类器和 NER 使用 TorchSharp NAS-BERT (Epochs/BatchSize)
    /// 章节标题分类器使用 FastTree (Trees/Leaves)
    /// </summary>
    public class TrainingParameters
    {
        // ── 通用 ──────────────────────────────────────────────────────────────
        public int Seed { get; set; } = 42;
        public double TestFraction { get; set; } = 0.2;

        /// <summary>交叉验证折数，0 = 不做 CV（TorchSharp 模型不支持 CV，仅章节分类器可用）</summary>
        public int CrossValidationFolds { get; set; } = 0;

        /// <summary>是否启用数据增强（当前仅列名分类器支持）</summary>
        public bool EnableAugmentation { get; set; } = false;

        // ── 列名分类器 (NAS-BERT TextClassification) ─────────────────────────
        public int ColumnEpochs { get; set; } = 4;
        public int ColumnBatchSize { get; set; } = 32;

        // ── NER (NAS-BERT NamedEntityRecognition) ───────────────────────────
        public int NerEpochs { get; set; } = 4;
        public int NerBatchSize { get; set; } = 32;

        // ── 章节标题 (FastTree) ───────────────────────────────────────────────
        public int SectionTrees { get; set; } = 100;
        public int SectionLeaves { get; set; } = 20;
        public int SectionMinLeaf { get; set; } = 2;

        // ── 预设工厂 ─────────────────────────────────────────────────────────

        /// <summary>快速训练：低 Epoch，无增强</summary>
        public static TrainingParameters Fast() => new TrainingParameters
        {
            ColumnEpochs = 2,
            ColumnBatchSize = 32,
            NerEpochs = 2,
            NerBatchSize = 32,
            SectionTrees = 50,
            SectionLeaves = 20,
            SectionMinLeaf = 2,
            CrossValidationFolds = 0,
            EnableAugmentation = false
        };

        /// <summary>标准训练：中等 Epoch</summary>
        public static TrainingParameters Standard() => new TrainingParameters
        {
            ColumnEpochs = 4,
            ColumnBatchSize = 32,
            NerEpochs = 4,
            NerBatchSize = 32,
            SectionTrees = 200,
            SectionLeaves = 20,
            SectionMinLeaf = 2,
            CrossValidationFolds = 5,
            EnableAugmentation = false
        };

        /// <summary>精细训练：高 Epoch，小 Batch，启用增强</summary>
        public static TrainingParameters Fine() => new TrainingParameters
        {
            ColumnEpochs = 8,
            ColumnBatchSize = 16,
            NerEpochs = 8,
            NerBatchSize = 16,
            SectionTrees = 500,
            SectionLeaves = 30,
            SectionMinLeaf = 2,
            CrossValidationFolds = 5,
            EnableAugmentation = true
        };
    }
}
