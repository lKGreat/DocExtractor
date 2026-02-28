namespace DocExtractor.Core.Models
{
    /// <summary>
    /// 表格单元格，保留合并信息和原始位置
    /// </summary>
    public class TableCell
    {
        /// <summary>原始文本值（已做基础清洗）</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>行索引（0-based）</summary>
        public int RowIndex { get; set; }

        /// <summary>列索引（0-based）</summary>
        public int ColIndex { get; set; }

        /// <summary>行合并跨度（1 = 未合并）</summary>
        public int RowSpan { get; set; } = 1;

        /// <summary>列合并跨度（1 = 未合并）</summary>
        public int ColSpan { get; set; } = 1;

        /// <summary>是否是合并单元格的主单元格（有实际值的那个）</summary>
        public bool IsMasterCell { get; set; } = true;

        /// <summary>主单元格的行列（针对被合并的影子格）</summary>
        public int MasterRow { get; set; }
        public int MasterCol { get; set; }

        public override string ToString() => $"[{RowIndex},{ColIndex}] \"{Value}\" (span:{RowSpan}x{ColSpan})";
    }
}
