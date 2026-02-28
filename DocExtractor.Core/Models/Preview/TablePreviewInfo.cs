using System.Collections.Generic;

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
        public List<ColumnPreviewItem> Columns { get; set; } = new List<ColumnPreviewItem>();
    }
}
