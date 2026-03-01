using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Core.Pipeline;
using DocExtractor.Core.Models.Preview;
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
    /// 使用 IParserFactory 注册机制创建解析器，避免硬编码。
    /// </summary>
    internal class ExtractionWorkflowService
    {
        private readonly List<IParserFactory> _factories = new List<IParserFactory>();

        public ExtractionWorkflowService()
        {
            // 默认注册 Word 和 Excel 工厂；后续可调用 RegisterParser 扩展
            _factories.Add(new ExcelParserFactory());
        }

        /// <summary>注册额外的解析器工厂（需在 Execute 之前调用）</summary>
        public void RegisterParser(IParserFactory factory) => _factories.Add(factory);

        /// <summary>初始化带 ML 模型的解析器（需在模型加载后调用）</summary>
        public void InitWordParser(ISectionHeadingDetector headingDetector)
        {
            _factories.RemoveAll(f => f is WordParserFactory);
            _factories.Insert(0, new WordParserFactory(headingDetector));
        }

        private IDocumentParser[] BuildParsers(ExtractionConfig config) =>
            _factories.Select(f => f.Create(config)).ToArray();

        public IReadOnlyList<ExtractionResult> ExecuteBatch(
            IReadOnlyList<string> files,
            ExtractionConfig config,
            ColumnClassifierModel columnModel,
            NerModel nerModel,
            SectionClassifierModel sectionModel,
            IProgress<PipelineProgress>? progress = null,
            CancellationToken cancellation = default)
        {
            EnsureWordParser(sectionModel);
            var normalizer = new HybridColumnNormalizer(columnModel, config.ColumnMatch);
            var parsers = BuildParsers(config);
            var pipeline = new ExtractionPipeline(parsers, normalizer, nerModel);
            return pipeline.ExecuteBatch(files, config, progress, cancellation);
        }

        public ExtractionPreviewResult Preview(
            string filePath,
            ExtractionConfig config,
            ColumnClassifierModel columnModel,
            SectionClassifierModel sectionModel)
        {
            EnsureWordParser(sectionModel);
            var normalizer = new HybridColumnNormalizer(columnModel, config.ColumnMatch);
            var parsers = BuildParsers(config);
            var previewService = new ExtractionPreviewService(parsers, normalizer);
            return previewService.Preview(filePath, config);
        }

        /// <summary>根据文件扩展名获取对应的解析器实例（用于数据预览）</summary>
        public IDocumentParser? GetParserForFile(string filePath, ExtractionConfig config)
        {
            string ext = Path.GetExtension(filePath);
            var factory = _factories.FirstOrDefault(f => f.CanHandle(ext));
            return factory?.Create(config);
        }

        private void EnsureWordParser(SectionClassifierModel sectionModel)
        {
            if (_factories.Any(f => f is WordParserFactory)) return;

            var ruleDetector = new SectionHeadingDetector();
            var hybridDetector = new HybridSectionHeadingDetector(ruleDetector, sectionModel);
            _factories.Insert(0, new WordParserFactory(hybridDetector));
        }
    }
}
