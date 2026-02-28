using System.Collections.Generic;
using DocExtractor.Core.Models;

namespace DocExtractor.Parsing.Common
{
    /// <summary>
    /// 辅助工具：构建 RawTable（确保 Cells 数组按 [row][col] 正确初始化）
    /// </summary>
    public class RawTableBuilder
    {
        private readonly int _rowCount;
        private readonly int _colCount;
        private readonly List<List<TableCell>> _cells;

        public RawTableBuilder(int rowCount, int colCount)
        {
            _rowCount = rowCount;
            _colCount = colCount;

            // 初始化所有单元格为空
            _cells = new List<List<TableCell>>(rowCount);
            for (int r = 0; r < rowCount; r++)
            {
                var row = new List<TableCell>(colCount);
                for (int c = 0; c < colCount; c++)
                {
                    row.Add(new TableCell
                    {
                        RowIndex = r,
                        ColIndex = c,
                        Value = string.Empty,
                        IsMasterCell = true,
                        MasterRow = r,
                        MasterCol = c
                    });
                }
                _cells.Add(row);
            }
        }

        /// <summary>设置主单元格（有实际值的格）</summary>
        public void SetCell(int row, int col, string value, int rowSpan = 1, int colSpan = 1)
        {
            if (!IsValid(row, col)) return;

            var cell = _cells[row][col];
            cell.Value = CellValueNormalizer.Normalize(value);
            cell.RowSpan = rowSpan;
            cell.ColSpan = colSpan;
            cell.IsMasterCell = true;
            cell.MasterRow = row;
            cell.MasterCol = col;

            // 标记被合并覆盖的影子格
            for (int r = row; r < row + rowSpan && r < _rowCount; r++)
            {
                for (int c = col; c < col + colSpan && c < _colCount; c++)
                {
                    if (r == row && c == col) continue; // 跳过主格本身
                    var shadow = _cells[r][c];
                    shadow.IsMasterCell = false;
                    shadow.MasterRow = row;
                    shadow.MasterCol = col;
                    shadow.Value = string.Empty;
                }
            }
        }

        public RawTable Build(string sourceFile, int tableIndex)
        {
            return new RawTable
            {
                SourceFile = sourceFile,
                TableIndex = tableIndex,
                RowCount = _rowCount,
                ColCount = _colCount,
                Cells = _cells
            };
        }

        private bool IsValid(int r, int c) =>
            r >= 0 && r < _rowCount && c >= 0 && c < _colCount;
    }
}
