using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using DocExtractor.Core.Models;

namespace DocExtractor.Data.Export
{
    /// <summary>
    /// 将抽取结果导出为 Excel 文件
    /// </summary>
    public class ExcelExporter
    {
        public ExcelExporter()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// 导出到 Excel（每个分组或来源文件一个 Sheet）
        /// </summary>
        public void Export(
            IReadOnlyList<ExtractedRecord> records,
            IReadOnlyList<FieldDefinition> fields,
            string outputPath)
        {
            using var package = new ExcelPackage();

            // 按来源文件分组，每个文件一个Sheet
            var grouped = records
                .GroupBy(r => Path.GetFileNameWithoutExtension(r.SourceFile))
                .ToList();

            if (!grouped.Any())
                grouped = new[] { records.GroupBy(_ => "结果").First() }.ToList();

            foreach (var group in grouped)
            {
                string sheetName = SanitizeSheetName(group.Key);
                var sheet = package.Workbook.Worksheets.Add(sheetName);
                WriteSheet(sheet, group.ToList(), fields);
            }

            // 总表
            if (grouped.Count > 1)
            {
                var allSheet = package.Workbook.Worksheets.Add("全部数据");
                WriteSheet(allSheet, records.ToList(), fields);
            }

            var fi = new FileInfo(outputPath);
            package.SaveAs(fi);
        }

        private void WriteSheet(
            ExcelWorksheet sheet,
            List<ExtractedRecord> records,
            IReadOnlyList<FieldDefinition> fields)
        {
            int col = 1;

            // 写表头
            foreach (var field in fields)
            {
                var cell = sheet.Cells[1, col];
                cell.Value = field.DisplayName.Length > 0 ? field.DisplayName : field.FieldName;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                cell.Style.Font.Color.SetColor(Color.White);
                col++;
            }

            // 写数据行
            for (int r = 0; r < records.Count; r++)
            {
                col = 1;
                var record = records[r];
                bool isIncomplete = !record.IsComplete;

                foreach (var field in fields)
                {
                    var cell = sheet.Cells[r + 2, col];
                    cell.Value = record.GetField(field.FieldName);

                    // 标记不完整记录
                    if (isIncomplete && field.IsRequired && string.IsNullOrWhiteSpace(cell.Text))
                    {
                        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 230, 230));
                    }
                    col++;
                }
            }

            // 自适应列宽（兼容中文）
            for (int c = 1; c <= fields.Count; c++)
            {
                double maxLen = GetDisplayWidth(fields[c - 1].DisplayName);
                for (int r = 0; r < records.Count; r++)
                {
                    string val = sheet.Cells[r + 2, c].Text ?? string.Empty;
                    double len = GetDisplayWidth(val);
                    if (len > maxLen) maxLen = len;
                }
                // 限制在 8~60 之间，+2 留余量
                sheet.Column(c).Width = System.Math.Max(8, System.Math.Min(maxLen + 2, 60));
            }

            // 冻结首行
            sheet.View.FreezePanes(2, 1);

            // 自动筛选
            if (records.Count > 0)
                sheet.Cells[1, 1, 1, fields.Count].AutoFilter = true;
        }

        /// <summary>计算字符串显示宽度（中文字符算 2，其余算 1）</summary>
        private static double GetDisplayWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            double width = 0;
            foreach (char c in text)
            {
                if (c > 0x7F) // 非 ASCII（中文、日文等宽字符）
                    width += 2;
                else
                    width += 1;
            }
            return width;
        }

        private static string SanitizeSheetName(string name)
        {
            foreach (var ch in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                name = name.Replace(ch, '_');
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }
    }
}
