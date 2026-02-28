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
            string outputPath,
            IReadOnlyList<string>? selectedFieldNames = null)
        {
            using var package = new ExcelPackage();

            IReadOnlyList<FieldDefinition> exportFields = fields;
            if (selectedFieldNames != null && selectedFieldNames.Count > 0)
            {
                var selected = new HashSet<string>(selectedFieldNames);
                var filtered = fields.Where(f => selected.Contains(f.FieldName)).ToList();
                if (filtered.Count > 0)
                    exportFields = filtered;
            }

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
                WriteSheet(sheet, group.ToList(), exportFields);
            }

            // 总表
            if (grouped.Count > 1)
            {
                var allSheet = package.Workbook.Worksheets.Add("全部数据");
                WriteSheet(allSheet, records.ToList(), exportFields);
            }

            var fi = new FileInfo(outputPath);
            package.SaveAs(fi);
        }

        // 普通字段表头颜色
        private static readonly Color HeaderBlue = Color.FromArgb(68, 114, 196);
        // 组名字段表头颜色（蓝绿，区分结构字段）
        private static readonly Color GroupNameHeaderColor = Color.FromArgb(0, 176, 240);
        // 组名数据行背景
        private static readonly Color GroupNameCellColor = Color.FromArgb(235, 250, 255);

        private void WriteSheet(
            ExcelWorksheet sheet,
            List<ExtractedRecord> records,
            IReadOnlyList<FieldDefinition> fields)
        {
            // 若配置字段中没有 GroupName，但记录中有注入值，则追加兜底列
            bool hasGroupNameField = fields.Any(f => f.FieldName == "GroupName");
            bool hasGroupNameData = records.Any(r => r.Fields.ContainsKey("GroupName") &&
                                                      !string.IsNullOrWhiteSpace(r.Fields["GroupName"]));
            bool appendGroupName = !hasGroupNameField && hasGroupNameData;

            // 确定最终列数
            int totalCols = fields.Count + (appendGroupName ? 1 : 0);
            int col = 1;

            // 组名兜底列排在最前
            if (appendGroupName)
            {
                WriteGroupNameHeader(sheet.Cells[1, col]);
                col++;
            }

            // 写表头
            foreach (var field in fields)
            {
                var cell = sheet.Cells[1, col];
                cell.Value = field.DisplayName.Length > 0 ? field.DisplayName : field.FieldName;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;

                if (field.FieldName == "GroupName")
                {
                    cell.Style.Fill.BackgroundColor.SetColor(GroupNameHeaderColor);
                    cell.Style.Font.Color.SetColor(Color.White);
                }
                else
                {
                    cell.Style.Fill.BackgroundColor.SetColor(HeaderBlue);
                    cell.Style.Font.Color.SetColor(Color.White);
                }
                col++;
            }

            // 写数据行
            for (int r = 0; r < records.Count; r++)
            {
                col = 1;
                var record = records[r];
                bool isIncomplete = !record.IsComplete;

                // 兜底 GroupName 列
                if (appendGroupName)
                {
                    var cell = sheet.Cells[r + 2, col];
                    cell.Value = record.GetField("GroupName");
                    ApplyGroupNameCellStyle(cell);
                    col++;
                }

                foreach (var field in fields)
                {
                    var cell = sheet.Cells[r + 2, col];
                    cell.Value = record.GetField(field.FieldName);

                    if (field.FieldName == "GroupName")
                        ApplyGroupNameCellStyle(cell);
                    else if (isIncomplete && field.IsRequired && string.IsNullOrWhiteSpace(cell.Text))
                    {
                        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 230, 230));
                    }
                    col++;
                }
            }

            // 自适应列宽（兼容中文）
            for (int c = 1; c <= totalCols; c++)
            {
                // 计算该列所有内容的最大显示宽度
                double maxLen = GetDisplayWidth(sheet.Cells[1, c].Text ?? string.Empty);
                for (int r = 0; r < records.Count; r++)
                {
                    string val = sheet.Cells[r + 2, c].Text ?? string.Empty;
                    double len = GetDisplayWidth(val);
                    if (len > maxLen) maxLen = len;
                }
                sheet.Column(c).Width = System.Math.Max(8, System.Math.Min(maxLen + 2, 60));
            }

            // 冻结首行
            sheet.View.FreezePanes(2, 1);

            // 自动筛选
            if (records.Count > 0)
                sheet.Cells[1, 1, 1, totalCols].AutoFilter = true;
        }

        private static void WriteGroupNameHeader(ExcelRange cell)
        {
            cell.Value = "组名";
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(GroupNameHeaderColor);
            cell.Style.Font.Color.SetColor(Color.White);
        }

        private static void ApplyGroupNameCellStyle(ExcelRange cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(GroupNameCellColor);
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
