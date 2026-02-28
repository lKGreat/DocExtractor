using System.Collections.Generic;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Interfaces
{
    /// <summary>
    /// 列名规范化接口：将原始列名映射到规范字段名
    /// </summary>
    public interface IColumnNormalizer
    {
        /// <summary>
        /// 将原始列名映射到规范 FieldName。
        /// 返回 null 表示无法匹配。
        /// </summary>
        ColumnMappingResult? Normalize(string rawColumnName, IReadOnlyList<FieldDefinition> fields);

        /// <summary>批量映射（优化：整张表头一次处理）</summary>
        IReadOnlyList<ColumnMappingResult?> NormalizeBatch(
            IReadOnlyList<string> rawColumnNames,
            IReadOnlyList<FieldDefinition> fields);
    }

    public class ColumnMappingResult
    {
        public string RawName { get; set; } = string.Empty;
        public string CanonicalFieldName { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public string MatchMethod { get; set; } = string.Empty; // "exact", "rule", "ml"
    }
}
