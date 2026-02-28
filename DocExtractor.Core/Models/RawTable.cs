using System.Collections.Generic;
using System.Linq;

namespace DocExtractor.Core.Models
{
    /// <summary>
    /// 从文档解析出的原始表格，保留完整的合并单元格信息
    /// </summary>
    public class RawTable
    {
        /// <summary>表格在文档中的标题（如有）</summary>
        public string? Title { get; set; }

        /// <summary>来源文件路径</summary>
        public string SourceFile { get; set; } = string.Empty;

        /// <summary>表格在文档中的索引（0-based）</summary>
        public int TableIndex { get; set; }

        /// <summary>Sheet 名称（Excel 专用）</summary>
        public string? SheetName { get; set; }

        /// <summary>行数</summary>
        public int RowCount { get; set; }

        /// <summary>列数</summary>
        public int ColCount { get; set; }

        /// <summary>
        /// 所有单元格，按 [row][col] 排列。
        /// 合并单元格的主格有值，影子格的 IsMasterCell=false。
        /// </summary>
        public List<List<TableCell>> Cells { get; set; } = new List<List<TableCell>>();

        /// <summary>
        /// 获取指定位置的实际值（自动跟随合并到主格）
        /// </summary>
        public string GetValue(int row, int col)
        {
            if (row < 0 || row >= RowCount || col < 0 || col >= ColCount)
                return string.Empty;

            var cell = Cells[row][col];
            if (cell.IsMasterCell)
                return cell.Value;

            // 影子格：返回主格的值
            return GetValue(cell.MasterRow, cell.MasterCol);
        }

        /// <summary>获取指定行的所有实际值（按主格）</summary>
        public IEnumerable<string> GetRowValues(int row)
        {
            for (int col = 0; col < ColCount; col++)
                yield return GetValue(row, col);
        }

        /// <summary>是否为空表</summary>
        public bool IsEmpty => RowCount == 0 || ColCount == 0;
    }
}
