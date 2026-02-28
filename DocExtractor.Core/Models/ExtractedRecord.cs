using System.Collections.Generic;

namespace DocExtractor.Core.Models
{
    /// <summary>
    /// 单条已抽取记录（字段名 → 规范化值）
    /// </summary>
    public class ExtractedRecord
    {
        /// <summary>记录唯一ID（自动生成）</summary>
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>来源表格信息</summary>
        public string SourceFile { get; set; } = string.Empty;
        public int SourceTableIndex { get; set; }
        public int SourceRowIndex { get; set; }

        /// <summary>
        /// 抽取的字段值，key = FieldDefinition.FieldName
        /// </summary>
        public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 原始列值（列规范名 → 原始文本），用于调试和校正
        /// </summary>
        public Dictionary<string, string> RawValues { get; set; } = new Dictionary<string, string>();

        /// <summary>是否通过规则完整抽取（无缺失必填字段）</summary>
        public bool IsComplete { get; set; }

        /// <summary>抽取过程中的警告信息</summary>
        public List<string> Warnings { get; set; } = new List<string>();

        public string GetField(string fieldName) =>
            Fields.TryGetValue(fieldName, out var v) ? v : string.Empty;
    }
}
