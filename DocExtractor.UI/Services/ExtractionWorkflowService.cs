using System.Collections.Generic;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Core.Pipeline;
using DocExtractor.ML.ColumnClassifier;
using DocExtractor.ML.EntityExtractor;
using DocExtractor.ML.Inference;
using DocExtractor.ML.SectionClassifier;
using DocExtractor.Parsing.Excel;
using DocExtractor.Parsing.Word;

namespace DocExtractor.UI.Services
{
    /// <summary>
    /// 抽取业务服务：封装 Pipeline 构建与批处理执行。
    /// </summary>
    internal class ExtractionWorkflowService
    {
        public IReadOnlyList<ExtractionResult> ExecuteBatch(
            IReadOnlyList<string> files,
            ExtractionConfig config,
            ColumnClassifierModel columnModel,
            NerModel nerModel,
            SectionClassifierModel sectionModel,
            IProgress<PipelineProgress>? progress = null)
        {
            var normalizer = new HybridColumnNormalizer(columnModel, config.ColumnMatch);
            var ruleDetector = new SectionHeadingDetector();
            var hybridHeadingDetector = new HybridSectionHeadingDetector(ruleDetector, sectionModel);

            var parsers = new IDocumentParser[]
            {
                new WordDocumentParser(hybridHeadingDetector),
                new ExcelDocumentParser(config.HeaderRowCount, config.TargetSheets)
            };

            var pipeline = new ExtractionPipeline(parsers, normalizer, nerModel);
            return pipeline.ExecuteBatch(files, config, progress);
        }
    }
}
