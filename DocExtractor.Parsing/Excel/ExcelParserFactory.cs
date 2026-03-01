using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;

namespace DocExtractor.Parsing.Excel
{
    /// <summary>
    /// Excel 文档解析器工厂（从 ExtractionConfig 中读取 HeaderRowCount 和 TargetSheets）
    /// </summary>
    public class ExcelParserFactory : IParserFactory
    {
        public bool CanHandle(string fileExtension) =>
            fileExtension.ToLower() == ".xlsx" || fileExtension.ToLower() == ".xls";

        public IDocumentParser Create(ExtractionConfig config) =>
            new ExcelDocumentParser(config.HeaderRowCount, config.TargetSheets);
    }
}
