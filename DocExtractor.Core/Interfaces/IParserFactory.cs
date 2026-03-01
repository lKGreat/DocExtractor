using DocExtractor.Core.Models;

namespace DocExtractor.Core.Interfaces
{
    /// <summary>
    /// 文档解析器工厂接口：根据配置创建对应格式的解析器实例
    /// </summary>
    public interface IParserFactory
    {
        /// <summary>判断是否支持指定扩展名（含点号，如 ".docx"）</summary>
        bool CanHandle(string fileExtension);

        /// <summary>根据抽取配置创建解析器实例</summary>
        IDocumentParser Create(ExtractionConfig config);
    }
}
