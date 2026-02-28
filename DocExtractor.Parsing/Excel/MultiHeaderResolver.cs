using System.Collections.Generic;
using System.Linq;
using OfficeOpenXml;

namespace DocExtractor.Parsing.Excel
{
    /// <summary>
    /// 多层合并表头解析器
    /// 将 Excel 中多行合并表头展开为单行列名（父列名/子列名格式）
    /// </summary>
    internal static class MultiHeaderResolver
    {
        /// <summary>
        /// 解析多层表头，返回每列的完整列名（如 "公式系数/A"）
        /// </summary>
        /// <param name="sheet">Excel 工作表</param>
        /// <param name="headerRowCount">表头行数</param>
        /// <param name="startRow">表头起始行（1-based）</param>
        /// <param name="startCol">起始列（1-based）</param>
        /// <param name="colCount">列数</param>
        public static string[] ResolveHeaders(
            ExcelWorksheet sheet,
            int headerRowCount,
            int startRow,
            int startCol,
            int colCount)
        {
            if (headerRowCount <= 1)
            {
                // 单行表头：直接读取
                return Enumerable.Range(startCol, colCount)
                    .Select(c => GetCellText(sheet, startRow, c))
                    .ToArray();
            }

            // 多行表头：先展开合并单元格，再组合层级名称
            // 构建 [headerRow][col] 的文本矩阵（合并单元格已填充主值）
            var headerMatrix = new string[headerRowCount, colCount];

            for (int hr = 0; hr < headerRowCount; hr++)
            {
                int sheetRow = startRow + hr;
                for (int ci = 0; ci < colCount; ci++)
                {
                    int sheetCol = startCol + ci;
                    var cell = sheet.Cells[sheetRow, sheetCol];

                    // EPPlus 的合并单元格：读取时自动返回合并主格的值
                    headerMatrix[hr, ci] = GetCellText(sheet, sheetRow, sheetCol);
                }
            }

            // 组合：去掉重复父级，如 ["公式系数", "公式系数"] + ["A", "B"] → "公式系数/A", "公式系数/B"
            var result = new string[colCount];
            for (int ci = 0; ci < colCount; ci++)
            {
                var parts = new List<string>();
                for (int hr = 0; hr < headerRowCount; hr++)
                {
                    string part = headerMatrix[hr, ci];
                    if (!string.IsNullOrWhiteSpace(part) &&
                        (parts.Count == 0 || parts[parts.Count - 1] != part))
                    {
                        parts.Add(part.Trim());
                    }
                }
                result[ci] = string.Join("/", parts);
            }

            return result;
        }

        private static string GetCellText(ExcelWorksheet sheet, int row, int col)
        {
            var cell = sheet.Cells[row, col];
            if (cell.Value == null) return string.Empty;
            return cell.Text?.Trim() ?? cell.Value.ToString()?.Trim() ?? string.Empty;
        }
    }
}
