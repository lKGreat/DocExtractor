using System;
using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Schema
{
    public enum TableSchemaType
    {
        Standard,
        MultiHeader,
        SideTitle,
        CrossTable
    }

    public class TableSchemaDetectionResult
    {
        public TableSchemaType SchemaType { get; set; } = TableSchemaType.Standard;
        public int SuggestedHeaderRowCount { get; set; } = 1;
        public double Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表格结构检测器：识别多层表头、侧边标题、交叉表等模式。
    /// </summary>
    public class TableSchemaDetector
    {
        public TableSchemaDetectionResult Detect(RawTable table)
        {
            if (table.IsEmpty || table.RowCount < 1 || table.ColCount < 1)
            {
                return new TableSchemaDetectionResult
                {
                    SchemaType = TableSchemaType.Standard,
                    SuggestedHeaderRowCount = 1,
                    Confidence = 0.5,
                    Reason = "空表或数据不足，使用默认结构"
                };
            }

            var cross = DetectCrossTable(table);
            if (cross != null) return cross;

            var side = DetectSideTitle(table);
            if (side != null) return side;

            var multi = DetectMultiHeader(table);
            if (multi != null) return multi;

            return new TableSchemaDetectionResult
            {
                SchemaType = TableSchemaType.Standard,
                SuggestedHeaderRowCount = 1,
                Confidence = 0.75,
                Reason = "未命中特殊结构特征"
            };
        }

        private static TableSchemaDetectionResult? DetectMultiHeader(RawTable table)
        {
            if (table.RowCount < 2) return null;

            var row0 = GetRow(table, 0);
            var row1 = GetRow(table, 1);
            int row0NonEmpty = row0.Count(v => !string.IsNullOrWhiteSpace(v));
            int row1NonEmpty = row1.Count(v => !string.IsNullOrWhiteSpace(v));

            int duplicateCount = CountDuplicates(row0);
            bool hasParentLikeHeader = duplicateCount > 0 && row1NonEmpty >= row0NonEmpty;
            if (!hasParentLikeHeader) return null;

            return new TableSchemaDetectionResult
            {
                SchemaType = TableSchemaType.MultiHeader,
                SuggestedHeaderRowCount = 2,
                Confidence = 0.82,
                Reason = $"首行存在重复父级表头({duplicateCount})且第二行信息更细"
            };
        }

        private static TableSchemaDetectionResult? DetectSideTitle(RawTable table)
        {
            if (table.RowCount < 4 || table.ColCount < 2) return null;

            var firstCol = new List<string>();
            var secondCol = new List<string>();
            for (int r = 1; r < table.RowCount; r++)
            {
                string a = table.GetValue(r, 0).Trim();
                string b = table.GetValue(r, 1).Trim();
                if (!string.IsNullOrWhiteSpace(a)) firstCol.Add(a);
                if (!string.IsNullOrWhiteSpace(b)) secondCol.Add(b);
            }
            if (firstCol.Count < 3 || secondCol.Count < 3) return null;

            double firstRepetition = RepetitionRatio(firstCol);
            double secondRepetition = RepetitionRatio(secondCol);

            if (firstRepetition >= 0.8 && firstRepetition > secondRepetition)
            {
                return new TableSchemaDetectionResult
                {
                    SchemaType = TableSchemaType.SideTitle,
                    SuggestedHeaderRowCount = 1,
                    Confidence = 0.8,
                    Reason = $"首列重复率 {firstRepetition:P0} 高于次列 {secondRepetition:P0}"
                };
            }

            return null;
        }

        private static TableSchemaDetectionResult? DetectCrossTable(RawTable table)
        {
            if (table.RowCount < 2 || table.ColCount < 2) return null;

            bool topLeftEmpty = string.IsNullOrWhiteSpace(table.GetValue(0, 0));
            if (!topLeftEmpty) return null;

            bool firstRowText = true;
            for (int c = 1; c < table.ColCount; c++)
            {
                if (!IsMostlyText(table.GetValue(0, c)))
                {
                    firstRowText = false;
                    break;
                }
            }

            bool firstColText = true;
            for (int r = 1; r < table.RowCount; r++)
            {
                if (!IsMostlyText(table.GetValue(r, 0)))
                {
                    firstColText = false;
                    break;
                }
            }

            if (firstRowText && firstColText)
            {
                return new TableSchemaDetectionResult
                {
                    SchemaType = TableSchemaType.CrossTable,
                    SuggestedHeaderRowCount = 1,
                    Confidence = 0.78,
                    Reason = "左上角为空且首行首列均为文本轴"
                };
            }

            return null;
        }

        private static List<string> GetRow(RawTable table, int row)
        {
            var list = new List<string>(table.ColCount);
            for (int c = 0; c < table.ColCount; c++)
                list.Add(table.GetValue(row, c).Trim());
            return list;
        }

        private static int CountDuplicates(IReadOnlyList<string> values)
        {
            return values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .GroupBy(v => v)
                .Count(g => g.Count() > 1);
        }

        private static double RepetitionRatio(IReadOnlyList<string> values)
        {
            var normalized = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (normalized.Count == 0) return 0;
            int unique = normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            return 1.0 - (double)unique / normalized.Count;
        }

        private static bool IsMostlyText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            int digits = value.Count(char.IsDigit);
            return digits < Math.Max(1, value.Length / 2);
        }
    }
}
