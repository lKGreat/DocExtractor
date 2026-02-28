using System.Collections.Generic;

namespace DocExtractor.Core.Models.Preview
{
    /// <summary>
    /// 文档抽取预览结果。
    /// </summary>
    public class ExtractionPreviewResult
    {
        public string SourceFile { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<TablePreviewInfo> Tables { get; set; } = new List<TablePreviewInfo>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
