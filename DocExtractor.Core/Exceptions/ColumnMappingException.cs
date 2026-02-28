using System;

namespace DocExtractor.Core.Exceptions
{
    /// <summary>
    /// 列映射异常：表头配置不一致、列名无法识别、映射冲突等。
    /// </summary>
    public class ColumnMappingException : DocExtractorException
    {
        public int? TableIndex { get; }
        public string? TableTitle { get; }
        public string? ColumnName { get; }

        public ColumnMappingException(
            string message,
            int? tableIndex = null,
            string? tableTitle = null,
            string? columnName = null,
            Exception? innerException = null)
            : base(message, "ColumnMapping", "COLUMN_MAPPING_ERROR", innerException)
        {
            TableIndex = tableIndex;
            TableTitle = tableTitle;
            ColumnName = columnName;
        }
    }
}
