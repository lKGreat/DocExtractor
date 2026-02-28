using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Parsing.Common;

namespace DocExtractor.Parsing.Excel
{
    /// <summary>
    /// Excel (.xlsx) 文档解析器
    /// 支持多 Sheet、多层合并表头、合并单元格自动填充
    /// </summary>
    public class ExcelDocumentParser : IDocumentParser
    {
        private readonly int _headerRowCount;
        private readonly List<string>? _targetSheets;

        /// <param name="headerRowCount">表头行数，默认 1</param>
        /// <param name="targetSheets">要处理的 Sheet 名称列表，null = 全部</param>
        public ExcelDocumentParser(int headerRowCount = 1, List<string>? targetSheets = null)
        {
            _headerRowCount = headerRowCount;
            _targetSheets = targetSheets;

            // EPPlus 5.x 非商业许可
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public bool CanHandle(string fileExtension) =>
            fileExtension.ToLower() is ".xlsx" or ".xls";

        public IReadOnlyList<RawTable> Parse(string filePath)
        {
            var result = new List<RawTable>();

            using var package = new ExcelPackage(new FileInfo(filePath));
            int tableIndex = 0;

            foreach (var sheet in package.Workbook.Worksheets)
            {
                if (_targetSheets != null && !_targetSheets.Contains(sheet.Name))
                    continue;

                var rawTable = ParseSheet(sheet, filePath, tableIndex++);
                if (!rawTable.IsEmpty)
                    result.Add(rawTable);
            }

            return result;
        }

        private RawTable ParseSheet(ExcelWorksheet sheet, string sourceFile, int tableIndex)
        {
            var dim = sheet.Dimension;
            if (dim == null)
                return new RawTable { SourceFile = sourceFile, TableIndex = tableIndex, SheetName = sheet.Name };

            int startRow = dim.Start.Row;
            int startCol = dim.Start.Column;
            int endRow = dim.End.Row;
            int endCol = dim.End.Column;

            int colCount = endCol - startCol + 1;
            int dataStartRow = startRow + _headerRowCount;
            int dataRowCount = endRow - dataStartRow + 1;

            if (dataRowCount <= 0 || colCount <= 0)
                return new RawTable { SourceFile = sourceFile, TableIndex = tableIndex, SheetName = sheet.Name };

            int totalRows = _headerRowCount + dataRowCount;
            var builder = new RawTableBuilder(totalRows, colCount);

            // 解析表头
            var headers = MultiHeaderResolver.ResolveHeaders(
                sheet, _headerRowCount, startRow, startCol, colCount);

            for (int ci = 0; ci < colCount; ci++)
                builder.SetCell(0, ci, headers[ci]);

            // 合并单元格信息
            var mergedCells = BuildMergedCellMap(sheet, startRow + _headerRowCount, startCol, endRow, endCol, _headerRowCount);

            // 解析数据行
            for (int r = 0; r < dataRowCount; r++)
            {
                int sheetRow = dataStartRow + r;
                int tableRow = _headerRowCount + r;

                for (int ci = 0; ci < colCount; ci++)
                {
                    int sheetCol = startCol + ci;
                    string cellText = GetCellText(sheet, sheetRow, sheetCol);

                    if (mergedCells.TryGetValue((sheetRow, sheetCol), out var mergeInfo))
                    {
                        if (mergeInfo.IsMaster)
                        {
                            int rs = mergeInfo.MasterRow + mergeInfo.RowSpan - sheetRow;
                            int cs = mergeInfo.MasterCol + mergeInfo.ColSpan - sheetCol;
                            builder.SetCell(tableRow, ci, cellText,
                                rowSpan: mergeInfo.RowSpan,
                                colSpan: mergeInfo.ColSpan);
                        }
                        // 影子格：SetCell 已在主格设置时标记
                    }
                    else
                    {
                        builder.SetCell(tableRow, ci, cellText);
                    }
                }
            }

            var rawTable = builder.Build(sourceFile, tableIndex);
            rawTable.SheetName = sheet.Name;
            return rawTable;
        }

        private string GetCellText(ExcelWorksheet sheet, int row, int col)
        {
            var cell = sheet.Cells[row, col];
            if (cell.Value == null) return string.Empty;
            string raw = cell.Text?.Trim() ?? cell.Value.ToString()?.Trim() ?? string.Empty;
            return CellValueNormalizer.Normalize(raw);
        }

        /// <summary>
        /// 构建合并单元格映射：(row, col) → MergeInfo
        /// </summary>
        private Dictionary<(int, int), MergeInfo> BuildMergedCellMap(
            ExcelWorksheet sheet,
            int startRow, int startCol,
            int endRow, int endCol,
            int headerOffset)
        {
            var map = new Dictionary<(int, int), MergeInfo>();

            foreach (var mergedCell in sheet.MergedCells)
            {
                var range = sheet.Cells[mergedCell];
                int mr = range.Start.Row;
                int mc = range.Start.Column;
                int rowSpan = range.End.Row - mr + 1;
                int colSpan = range.End.Column - mc + 1;

                for (int r = range.Start.Row; r <= range.End.Row; r++)
                {
                    for (int c = range.Start.Column; c <= range.End.Column; c++)
                    {
                        if (r < startRow || r > endRow || c < startCol || c > endCol) continue;

                        map[(r, c)] = new MergeInfo
                        {
                            IsMaster = (r == mr && c == mc),
                            MasterRow = mr,
                            MasterCol = mc,
                            RowSpan = rowSpan,
                            ColSpan = colSpan
                        };
                    }
                }
            }

            return map;
        }

        private class MergeInfo
        {
            public bool IsMaster { get; set; }
            public int MasterRow { get; set; }
            public int MasterCol { get; set; }
            public int RowSpan { get; set; }
            public int ColSpan { get; set; }
        }
    }
}
