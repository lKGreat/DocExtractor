using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocExtractor.Parsing.Word
{
    /// <summary>
    /// Word 表格合并单元格解析器
    /// 处理 GridSpan（列合并）和 VMerge（行合并）
    /// </summary>
    internal static class MergedCellResolver
    {
        /// <summary>
        /// 计算 Word 表格的真实列数（考虑 GridSpan）
        /// </summary>
        public static int GetColumnCount(Table table)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (!rows.Any()) return 0;

            int maxCols = 0;
            foreach (var row in rows)
            {
                int cols = 0;
                foreach (var cell in row.Elements<TableCell>())
                {
                    var gridSpan = cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
                    cols += (int)gridSpan;
                }
                maxCols = Math.Max(maxCols, cols);
            }
            return maxCols;
        }

        /// <summary>
        /// 将 Word 表格解析为二维网格（已展开合并单元格）
        /// grid[row][col] = (text, isMaster, masterRow, masterCol, colSpan, rowSpan)
        /// </summary>
        public static GridCell[,] BuildGrid(Table table)
        {
            var rows = table.Elements<TableRow>().ToList();
            int rowCount = rows.Count;
            int colCount = GetColumnCount(table);

            var grid = new GridCell[rowCount, colCount];
            // 初始化
            for (int r = 0; r < rowCount; r++)
                for (int c = 0; c < colCount; c++)
                    grid[r, c] = new GridCell();

            // 追踪行合并：哪些列的哪些行还在延续中
            // key=col, value=(masterRow, masterCol, remainingRows, text)
            var vertMerge = new Dictionary<int, (int mr, int mc, string text)>();

            for (int r = 0; r < rowCount; r++)
            {
                int col = 0;
                foreach (var cell in rows[r].Elements<TableCell>())
                {
                    // 跳过已被行合并占用的列
                    while (col < colCount && grid[r, col].IsOccupied) col++;
                    if (col >= colCount) break;

                    var props = cell.TableCellProperties;
                    int colSpan = (int)(props?.GridSpan?.Val?.Value ?? 1);
                    var vmerge = props?.VerticalMerge;
                    string text = GetCellText(cell);

                    if (vmerge != null)
                    {
                        if (vmerge.Val == null || vmerge.Val == MergedCellValues.Restart)
                        {
                            // 行合并的起始格（有值）
                            vertMerge[col] = (r, col, text);
                            SetMasterCell(grid, r, col, text, 1, colSpan);

                            // 标记同列其他被合并的列
                            for (int cs = 1; cs < colSpan; cs++)
                            {
                                if (col + cs < colCount)
                                    vertMerge[col + cs] = (r, col + cs, string.Empty);
                            }
                        }
                        else
                        {
                            // 行合并的延续格（无值，引用主格）
                            if (vertMerge.TryGetValue(col, out var master))
                            {
                                SetShadowCell(grid, r, col, master.mr, master.mc, colSpan);
                                // 更新主格的 RowSpan
                                grid[master.mr, master.mc].RowSpan++;
                            }
                        }
                    }
                    else
                    {
                        vertMerge.Remove(col);
                        SetMasterCell(grid, r, col, text, 1, colSpan);
                    }

                    // 处理列合并的影子格
                    for (int cs = 1; cs < colSpan; cs++)
                    {
                        if (col + cs < colCount && !grid[r, col + cs].IsOccupied)
                            SetShadowCell(grid, r, col + cs, r, col, 1);
                    }

                    col += colSpan;
                }
            }

            return grid;
        }

        private static void SetMasterCell(GridCell[,] grid, int r, int c, string text, int rowSpan, int colSpan)
        {
            grid[r, c].Text = text;
            grid[r, c].IsMaster = true;
            grid[r, c].MasterRow = r;
            grid[r, c].MasterCol = c;
            grid[r, c].RowSpan = rowSpan;
            grid[r, c].ColSpan = colSpan;
            grid[r, c].IsOccupied = true;
        }

        private static void SetShadowCell(GridCell[,] grid, int r, int c, int mr, int mc, int colSpan)
        {
            grid[r, c].Text = string.Empty;
            grid[r, c].IsMaster = false;
            grid[r, c].MasterRow = mr;
            grid[r, c].MasterCol = mc;
            grid[r, c].ColSpan = colSpan;
            grid[r, c].IsOccupied = true;
        }

        private static string GetCellText(TableCell cell)
        {
            var texts = new List<string>();
            foreach (var para in cell.Elements<Paragraph>())
            {
                var text = string.Concat(para.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>()
                    .Select(t => t.Text));
                if (!string.IsNullOrWhiteSpace(text))
                    texts.Add(text.Trim());
            }
            return string.Join("\n", texts);
        }
    }

    internal class GridCell
    {
        public string Text { get; set; } = string.Empty;
        public bool IsMaster { get; set; } = true;
        public int MasterRow { get; set; }
        public int MasterCol { get; set; }
        public int RowSpan { get; set; } = 1;
        public int ColSpan { get; set; } = 1;
        public bool IsOccupied { get; set; }
    }
}
