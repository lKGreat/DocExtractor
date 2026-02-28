using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocExtractor.ML.ColumnClassifier;
using DocExtractor.ML.EntityExtractor;
using DocExtractor.ML.SectionClassifier;
using DocExtractor.ML.Training;

namespace DocExtractor.ML
{
    /// <summary>
    /// 三阶段统一训练器：列名分类 → NER → 章节标题
    /// 顺序执行，统一进度报告
    /// </summary>
    public class UnifiedModelTrainer
    {
        /// <summary>
        /// 三阶段顺序训练
        /// </summary>
        /// <param name="columnData">列名训练数据（可为空列表跳过）</param>
        /// <param name="nerData">NER 训练数据（可为空列表跳过）</param>
        /// <param name="sectionData">章节训练数据（可为空列表跳过）</param>
        /// <param name="modelsDir">模型保存目录</param>
        /// <param name="parameters">训练参数</param>
        /// <param name="progress">进度报告：(阶段名, 详情, 百分比 0-100)</param>
        /// <param name="cancellation">取消令牌</param>
        public UnifiedTrainingResult TrainAll(
            IReadOnlyList<ColumnInput> columnData,
            IReadOnlyList<NerAnnotation> nerData,
            IReadOnlyList<SectionInput> sectionData,
            string modelsDir,
            TrainingParameters parameters,
            IProgress<(string Stage, string Detail, double Percent)> progress,
            CancellationToken cancellation)
        {
            var result = new UnifiedTrainingResult();
            Directory.CreateDirectory(modelsDir);

            // Stage 1: 列名分类器 (0-33%)
            if (columnData.Count >= 10)
            {
                progress?.Report(("列名分类", "开始训练...", 0));
                var stageProgress = new Progress<string>(msg =>
                    progress?.Report(("列名分类", msg, 10)));

                cancellation.ThrowIfCancellationRequested();

                var trainer = new ColumnClassifierTrainer(parameters.Seed);
                string modelPath = Path.Combine(modelsDir, "column_classifier.zip");
                result.ColumnEval = trainer.Train(columnData, modelPath, stageProgress, parameters, cancellation);
                progress?.Report(("列名分类", $"完成: {result.ColumnEval}", 33));
            }
            else
            {
                progress?.Report(("列名分类", $"跳过（样本 {columnData.Count} < 10）", 33));
                result.ColumnSkipReason = $"样本不足（{columnData.Count}/10）";
            }

            cancellation.ThrowIfCancellationRequested();

            // Stage 2: NER (33-66%)
            if (nerData.Count >= 20)
            {
                progress?.Report(("NER", "开始训练...", 33));
                var stageProgress = new Progress<string>(msg =>
                    progress?.Report(("NER", msg, 45)));

                cancellation.ThrowIfCancellationRequested();

                var trainer = new NerTrainer(parameters.Seed);
                string modelPath = Path.Combine(modelsDir, "ner_model.zip");
                result.NerEval = trainer.Train(nerData, modelPath, stageProgress, parameters, cancellation);
                progress?.Report(("NER", $"完成: {result.NerEval}", 66));
            }
            else
            {
                progress?.Report(("NER", $"跳过（样本 {nerData.Count} < 20）", 66));
                result.NerSkipReason = $"样本不足（{nerData.Count}/20）";
            }

            cancellation.ThrowIfCancellationRequested();

            // Stage 3: 章节标题 (66-100%)
            if (sectionData.Count >= 20)
            {
                progress?.Report(("章节标题", "开始训练...", 66));
                var stageProgress = new Progress<string>(msg =>
                    progress?.Report(("章节标题", msg, 80)));

                cancellation.ThrowIfCancellationRequested();

                var trainer = new SectionClassifierTrainer(parameters.Seed);
                string modelPath = Path.Combine(modelsDir, "section_classifier.zip");
                result.SectionEval = trainer.Train(sectionData, modelPath, stageProgress, parameters, cancellation);
                progress?.Report(("章节标题", $"完成: {result.SectionEval}", 100));
            }
            else
            {
                progress?.Report(("章节标题", $"跳过（样本 {sectionData.Count} < 20）", 100));
                result.SectionSkipReason = $"样本不足（{sectionData.Count}/20）";
            }

            return result;
        }
    }

    public class UnifiedTrainingResult
    {
        public TrainingEvaluation? ColumnEval { get; set; }
        public NerTrainingResult? NerEval { get; set; }
        public SectionTrainingEvaluation? SectionEval { get; set; }

        public string? ColumnSkipReason { get; set; }
        public string? NerSkipReason { get; set; }
        public string? SectionSkipReason { get; set; }

        public override string ToString()
        {
            var parts = new List<string>();
            if (ColumnEval != null) parts.Add($"列名: {ColumnEval}");
            else if (ColumnSkipReason != null) parts.Add($"列名: 跳过 ({ColumnSkipReason})");
            if (NerEval != null) parts.Add($"NER: {NerEval}");
            else if (NerSkipReason != null) parts.Add($"NER: 跳过 ({NerSkipReason})");
            if (SectionEval != null) parts.Add($"章节: {SectionEval}");
            else if (SectionSkipReason != null) parts.Add($"章节: 跳过 ({SectionSkipReason})");
            return string.Join("\n", parts);
        }
    }
}
