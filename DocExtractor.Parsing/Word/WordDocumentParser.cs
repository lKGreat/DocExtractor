using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Parsing.Common;

namespace DocExtractor.Parsing.Word
{
    /// <summary>
    /// Word (.docx) 文档解析器
    /// 提取文档中所有表格，处理合并单元格（GridSpan + VMerge）
    /// </summary>
    public class WordDocumentParser : IDocumentParser
    {
        public bool CanHandle(string fileExtension) =>
            fileExtension.ToLower() is ".docx" or ".doc";

        public IReadOnlyList<RawTable> Parse(string filePath)
        {
            var result = new List<RawTable>();

            using var doc = WordprocessingDocument.Open(filePath, isEditable: false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return result;

            int tableIndex = 0;
            foreach (var table in body.Descendants<Table>())
            {
                var rawTable = ParseTable(table, filePath, tableIndex++);
                if (!rawTable.IsEmpty)
                    result.Add(rawTable);
            }

            return result;
        }

        private RawTable ParseTable(Table table, string sourceFile, int tableIndex)
        {
            var grid = MergedCellResolver.BuildGrid(table);
            int rowCount = grid.GetLength(0);
            int colCount = grid.GetLength(1);

            if (rowCount == 0 || colCount == 0)
                return new RawTable { SourceFile = sourceFile, TableIndex = tableIndex };

            var builder = new RawTableBuilder(rowCount, colCount);

            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    var cell = grid[r, c];
                    if (!cell.IsMaster) continue; // 影子格由 builder.SetCell 内部标记

                    string normalized = CellValueNormalizer.Normalize(cell.Text);
                    builder.SetCell(r, c, normalized, cell.RowSpan, cell.ColSpan);
                }
            }

            var rawTable = builder.Build(sourceFile, tableIndex);

            // 尝试提取表格标题（表格前一个段落）
            rawTable.Title = TryGetTableTitle(table);

            return rawTable;
        }

        private string? TryGetTableTitle(Table table)
        {
            // Word 中表格前的段落通常是表格标题
            var prev = table.PreviousSibling<Paragraph>();
            if (prev == null) return null;

            string text = string.Empty;
            foreach (var t in prev.Descendants<Text>())
                text += t.Text;

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
