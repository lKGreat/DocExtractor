using System.Collections.Generic;
using DocExtractor.Core.Schema;

namespace DocExtractor.Core.Models.Preview
{
    /// <summary>
    /// 单个表格的预览信息。
    /// </summary>
    public class TablePreviewInfo
    {
        public int TableIndex { get; set; }
        public string? Title { get; set; }
        public int RowCount { get; set; }
        public int ColCount { get; set; }
        public TableSchemaType SchemaType { get; set; } = TableSchemaType.Standard;
        public int SuggestedHeaderRowCount { get; set; } = 1;
        public double SchemaConfidence { get; set; }
        public string SchemaReason { get; set; } = string.Empty;
        public List<ColumnPreviewItem> Columns { get; set; } = new List<ColumnPreviewItem>();
    }
}
