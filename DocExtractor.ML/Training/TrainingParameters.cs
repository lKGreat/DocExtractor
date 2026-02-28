namespace DocExtractor.ML.Training
{
    /// <summary>
    /// ML 模型训练参数（三个预设 + 自定义）
    /// </summary>
    public class TrainingParameters
    {
        // ── 通用 ──────────────────────────────────────────────────────────────
        public int Seed { get; set; } = 42;
        public double TestFraction { get; set; } = 0.2;

        /// <summary>交叉验证折数，0 = 不做 CV（单次 80/20 拆分）</summary>
        public int CrossValidationFolds { get; set; } = 0;

        /// <summary>是否启用数据增强（当前仅列名分类器支持）</summary>
        public bool EnableAugmentation { get; set; } = false;

        // ── 列名分类器 (SDCA) ─────────────────────────────────────────────────
        public int ColumnMaxIterations { get; set; } = 100;

        // ── NER (LightGBM) ────────────────────────────────────────────────────
        public int NerIterations { get; set; } = 100;
        public int NerLeaves { get; set; } = 31;
        public double NerLearningRate { get; set; } = 0.1;

        // ── 章节标题 (FastTree) ───────────────────────────────────────────────
        public int SectionTrees { get; set; } = 100;
        public int SectionLeaves { get; set; } = 20;
        public int SectionMinLeaf { get; set; } = 2;

        // ── 预设工厂 ─────────────────────────────────────────────────────────

        /// <summary>快速训练：低迭代，无 CV，无增强</summary>
        public static TrainingParameters Fast() => new TrainingParameters
        {
            ColumnMaxIterations = 50,
            NerIterations = 50,
            NerLeaves = 31,
            NerLearningRate = 0.15,
            SectionTrees = 50,
            SectionLeaves = 20,
            SectionMinLeaf = 2,
            CrossValidationFolds = 0,
            EnableAugmentation = false
        };

        /// <summary>标准训练：中等迭代，5 折 CV，无增强</summary>
        public static TrainingParameters Standard() => new TrainingParameters
        {
            ColumnMaxIterations = 200,
            NerIterations = 200,
            NerLeaves = 31,
            NerLearningRate = 0.1,
            SectionTrees = 200,
            SectionLeaves = 20,
            SectionMinLeaf = 2,
            CrossValidationFolds = 5,
            EnableAugmentation = false
        };

        /// <summary>精细训练：高迭代，5 折 CV，启用增强</summary>
        public static TrainingParameters Fine() => new TrainingParameters
        {
            ColumnMaxIterations = 500,
            NerIterations = 500,
            NerLeaves = 50,
            NerLearningRate = 0.05,
            SectionTrees = 500,
            SectionLeaves = 30,
            SectionMinLeaf = 2,
            CrossValidationFolds = 5,
            EnableAugmentation = true
        };
    }
}
