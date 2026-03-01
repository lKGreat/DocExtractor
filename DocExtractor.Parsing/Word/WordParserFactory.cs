using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;

namespace DocExtractor.Parsing.Word
{
    /// <summary>
    /// Word 文档解析器工厂
    /// </summary>
    public class WordParserFactory : IParserFactory
    {
        private readonly ISectionHeadingDetector? _headingDetector;

        public WordParserFactory(ISectionHeadingDetector? headingDetector = null)
        {
            _headingDetector = headingDetector;
        }

        public bool CanHandle(string fileExtension) =>
            fileExtension.ToLower() == ".docx" || fileExtension.ToLower() == ".doc";

        public IDocumentParser Create(ExtractionConfig config) =>
            new WordDocumentParser(_headingDetector);
    }
}
