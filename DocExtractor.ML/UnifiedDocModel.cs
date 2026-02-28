using System;
using System.IO;
using DocExtractor.ML.ColumnClassifier;
using DocExtractor.ML.EntityExtractor;
using DocExtractor.ML.SectionClassifier;

namespace DocExtractor.ML
{
    /// <summary>
    /// 统一文档模型容器，持有三个模型实例并提供统一加载和状态查询
    /// </summary>
    public class UnifiedDocModel : IDisposable
    {
        public ColumnClassifierModel ColumnClassifier { get; }
        public NerModel Ner { get; }
        public SectionClassifierModel Section { get; }

        public UnifiedDocModel()
        {
            ColumnClassifier = new ColumnClassifierModel();
            Ner = new NerModel();
            Section = new SectionClassifierModel();
        }

        /// <summary>
        /// 从模型目录加载所有可用模型（静默跳过不存在的模型）
        /// </summary>
        public void LoadAll(string modelsDir)
        {
            string colPath = Path.Combine(modelsDir, "column_classifier.zip");
            string nerPath = Path.Combine(modelsDir, "ner_model.zip");
            string secPath = Path.Combine(modelsDir, "section_classifier.zip");

            if (File.Exists(colPath)) ColumnClassifier.Load(colPath);
            if (File.Exists(nerPath)) Ner.Load(nerPath);
            if (File.Exists(secPath)) Section.Load(secPath);
        }

        /// <summary>三个模型的加载状态</summary>
        public (bool Column, bool Ner, bool Section) Status =>
            (ColumnClassifier.IsLoaded, Ner.IsLoaded, Section.IsLoaded);

        public void Dispose()
        {
            ColumnClassifier?.Dispose();
            Ner?.Dispose();
            Section?.Dispose();
        }
    }
}
