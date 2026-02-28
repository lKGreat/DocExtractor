using System.Collections.Generic;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Interfaces
{
    /// <summary>
    /// 文档解析器接口：将 Word/Excel 文件转换为 RawTable 集合
    /// </summary>
    public interface IDocumentParser
    {
        /// <summary>解析文件，返回所有提取到的原始表格</summary>
        IReadOnlyList<RawTable> Parse(string filePath);

        /// <summary>是否支持该文件扩展名</summary>
        bool CanHandle(string fileExtension);
    }
}
