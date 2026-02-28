using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocExtractor.Core.Exceptions;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Parsing.Common;

namespace DocExtractor.Parsing.Word
{
    /// <summary>
    /// Word (.docx) 文档解析器
    /// 提取文档中所有表格，处理合并单元格（GridSpan + VMerge）
    /// 同时按顺序遍历 Body 子元素，追踪章节标题并将其记录到 RawTable.SectionHeading
    /// </summary>
    public class WordDocumentParser : IDocumentParser
    {
        private readonly ISectionHeadingDetector _headingDetector;

        /// <param name="headingDetector">
        /// 章节标题检测器。
        /// 不传时使用内置纯规则引擎；
        /// 可注入 HybridSectionHeadingDetector（规则 + ML 三层级联）。
        /// </param>
        public WordDocumentParser(ISectionHeadingDetector? headingDetector = null)
        {
            _headingDetector = headingDetector ?? new SectionHeadingDetector();
        }

        public bool CanHandle(string fileExtension) =>
            fileExtension.ToLower() is ".docx" or ".doc";

        public IReadOnlyList<RawTable> Parse(string filePath)
        {
            try
            {
                var result = new List<RawTable>();

                using var doc = WordprocessingDocument.Open(filePath, isEditable: false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return result;

                // 统计 Body 总子元素数（用于计算相对位置）
                int totalElements = body.ChildElements.Count;
                int elementIndex = 0;

                // 按文档顺序遍历 Body 直接子元素，同时追踪当前章节标题
                string? currentSectionHeading = null;
                string? currentSectionNumber = null;
                int tableIndex = 0;

                foreach (OpenXmlElement element in body.ChildElements)
                {
                    float position = totalElements > 0 ? (float)elementIndex / totalElements : 0f;
                    elementIndex++;

                    if (element is Paragraph para)
                    {
                        // 提取段落格式特征（OpenXML 层）
                        var features = ParagraphFeatureExtractor.Extract(para);
                        string paraText = ParagraphFeatureExtractor.ExtractText(para);

                        // 通过接口检测（规则层 or 混合层）
                        var heading = _headingDetector.Detect(
                            paraText,
                            features.IsBold,
                            features.FontSize,
                            features.HasHeadingStyle,
                            features.OutlineLevel,
                            position);

                        if (heading != null)
                        {
                            currentSectionHeading = heading.FullText;
                            currentSectionNumber = heading.Number;
                        }
                    }
                    else if (element is Table table)
                    {
                        var rawTable = ParseTable(table, filePath, tableIndex++);
                        if (!rawTable.IsEmpty)
                        {
                            rawTable.SectionHeading = currentSectionHeading;
                            rawTable.SectionNumber = currentSectionNumber;
                            result.Add(rawTable);
                        }
                    }
                }

                return result;
            }
            catch (ParseException)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                throw new ParseException(
                    $"Word 解析失败：{ex.Message}",
                    filePath,
                    nameof(WordDocumentParser),
                    ex);
            }
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
                    if (!cell.IsMaster) continue;

                    string normalized = CellValueNormalizer.Normalize(cell.Text);
                    builder.SetCell(r, c, normalized, cell.RowSpan, cell.ColSpan);
                }
            }

            var rawTable = builder.Build(sourceFile, tableIndex);

            // 表格标题（表格紧邻的上一段落，通常是"表 X.X-X ..."这类说明行）
            rawTable.Title = TryGetTableTitle(table);

            return rawTable;
        }

        private static string? TryGetTableTitle(Table table)
        {
            var prev = table.PreviousSibling<Paragraph>();
            if (prev == null) return null;

            string text = string.Empty;
            foreach (var t in prev.Descendants<Text>())
                text += t.Text;

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
