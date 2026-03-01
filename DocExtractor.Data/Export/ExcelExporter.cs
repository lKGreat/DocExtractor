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
    /// 将抽取结果导出为 Excel 文件。
    /// 支持通过 OutputConfig 配置输出字段映射和多 Sheet 规则。
    /// </summary>
    public class ExcelExporter
    {
        public ExcelExporter()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// 导出到 Excel（支持 OutputConfig 通用输出配置）
        /// </summary>
        public void Export(
            IReadOnlyList<ExtractedRecord> records,
            IReadOnlyList<FieldDefinition> fields,
            string outputPath,
            OutputConfig? outputConfig = null)
        {
            using var package = new ExcelPackage();

            // 构建实际导出列定义（字段名 → 输出列名）
            var exportColumns = BuildExportColumns(fields, outputConfig);
            var sheetGroups = BuildSheetGroups(records, outputConfig);

            foreach (var group in sheetGroups)
            {
                string sheetName = SanitizeSheetName(group.Key);
                var sheet = package.Workbook.Worksheets.Add(sheetName);
                WriteSheet(sheet, group.Value, exportColumns);
            }

            // 汇总 Sheet（多 Sheet 时且配置启用）
            bool includeSummary = outputConfig?.IncludeSummarySheet ?? true;
            if (sheetGroups.Count > 1 && includeSummary)
            {
                var allSheet = package.Workbook.Worksheets.Add("全部数据");
                WriteSheet(allSheet, records.ToList(), exportColumns);
            }

            package.SaveAs(new FileInfo(outputPath));
        }

        /// <summary>旧版兼容：selectedFieldNames 模式</summary>
        public void Export(
            IReadOnlyList<ExtractedRecord> records,
            IReadOnlyList<FieldDefinition> fields,
            string outputPath,
            IReadOnlyList<string>? selectedFieldNames)
        {
            OutputConfig? cfg = null;
            if (selectedFieldNames != null && selectedFieldNames.Count > 0)
            {
                var selected = new HashSet<string>(selectedFieldNames);
                var mappings = fields
                    .Where(f => selected.Contains(f.FieldName))
                    .Select((f, i) => new OutputFieldMapping
                    {
                        SourceFieldName = f.FieldName,
                        OutputColumnName = string.IsNullOrWhiteSpace(f.DisplayName) ? f.FieldName : f.DisplayName,
                        OutputOrder = i,
                        IsEnabled = true
                    }).ToList();
                cfg = new OutputConfig { FieldMappings = mappings };
            }
            Export(records, fields, outputPath, cfg);
        }

        // ── 辅助：构建导出列 ─────────────────────────────────────────────────

        private List<ExportColumn> BuildExportColumns(
            IReadOnlyList<FieldDefinition> fields,
            OutputConfig? cfg)
        {
            if (cfg == null || cfg.FieldMappings.Count == 0)
            {
                return fields.Select((f, i) => new ExportColumn
                {
                    FieldName = f.FieldName,
                    ColumnHeader = string.IsNullOrWhiteSpace(f.DisplayName) ? f.FieldName : f.DisplayName,
                    Order = i
                }).ToList();
            }

            var fieldDict = fields.ToDictionary(f => f.FieldName, f => f);
            return cfg.FieldMappings
                .Where(m => m.IsEnabled && fieldDict.ContainsKey(m.SourceFieldName))
                .OrderBy(m => m.OutputOrder)
                .Select((m, i) => new ExportColumn
                {
                    FieldName = m.SourceFieldName,
                    ColumnHeader = string.IsNullOrWhiteSpace(m.OutputColumnName) ? m.SourceFieldName : m.OutputColumnName,
                    Order = i
                }).ToList();
        }

        private Dictionary<string, List<ExtractedRecord>> BuildSheetGroups(
            IReadOnlyList<ExtractedRecord> records,
            OutputConfig? cfg)
        {
            var mode = cfg?.SheetRule?.SplitMode ?? SheetSplitMode.BySourceFile;
            var template = cfg?.SheetRule?.SheetNameTemplate ?? "{0}";

            IEnumerable<IGrouping<string, ExtractedRecord>> grouped;

            switch (mode)
            {
                case SheetSplitMode.None:
                    return new Dictionary<string, List<ExtractedRecord>>
                    {
                        { "结果", records.ToList() }
                    };

                case SheetSplitMode.ByField:
                    var fieldName = cfg?.SheetRule?.SplitFieldName ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(fieldName))
                    {
                        grouped = records.GroupBy(r => r.GetField(fieldName));
                        break;
                    }
                    goto default;

                default: // BySourceFile
                    grouped = records.GroupBy(r => Path.GetFileNameWithoutExtension(r.SourceFile));
                    break;
            }

            var result = new Dictionary<string, List<ExtractedRecord>>();
            foreach (var g in grouped)
            {
                string key = string.IsNullOrWhiteSpace(g.Key) ? "其他" : g.Key;
                string baseName;
                try
                {
                    baseName = string.Format(template, key);
                }
                catch
                {
                    // 模板非法时降级为默认模板，避免导出中断
                    baseName = key;
                }

                string uniqueName = baseName;
                int n = 2;
                while (result.ContainsKey(uniqueName))
                {
                    uniqueName = baseName + "_" + n;
                    n++;
                }
                result[uniqueName] = g.ToList();
            }

            if (result.Count == 0)
                result["结果"] = records.ToList();

            return result;
        }

        // ── 写 Sheet ─────────────────────────────────────────────────────────

        private static readonly Color HeaderBlue = Color.FromArgb(68, 114, 196);
        private static readonly Color GroupNameHeaderColor = Color.FromArgb(0, 176, 240);
        private static readonly Color GroupNameCellColor = Color.FromArgb(235, 250, 255);
        private static readonly Color TimeAxisHeaderColor = Color.FromArgb(82, 196, 26);
        private static readonly Color TimeAxisCellColor = Color.FromArgb(237, 255, 230);

        private void WriteSheet(
            ExcelWorksheet sheet,
            List<ExtractedRecord> records,
            List<ExportColumn> columns)
        {
            bool appendGroupName = !columns.Any(c => c.FieldName == "GroupName")
                && records.Any(r => r.Fields.ContainsKey("GroupName") && !string.IsNullOrWhiteSpace(r.Fields["GroupName"]));
            bool appendTimeAxis = !columns.Any(c => c.FieldName == "TimeAxis")
                && records.Any(r => r.Fields.ContainsKey("TimeAxis") && !string.IsNullOrWhiteSpace(r.Fields["TimeAxis"]));

            int totalCols = columns.Count + (appendGroupName ? 1 : 0) + (appendTimeAxis ? 1 : 0);
            int col = 1;

            if (appendGroupName)
            {
                WriteGroupNameHeader(sheet.Cells[1, col]);
                col++;
            }

            foreach (var ec in columns)
            {
                var cell = sheet.Cells[1, col];
                cell.Value = ec.ColumnHeader;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;

                if (ec.FieldName == "GroupName")
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

            if (appendTimeAxis)
            {
                WriteTimeAxisHeader(sheet.Cells[1, col]);
                col++;
            }

            for (int r = 0; r < records.Count; r++)
            {
                col = 1;
                var record = records[r];
                bool isIncomplete = !record.IsComplete;

                if (appendGroupName)
                {
                    var cell = sheet.Cells[r + 2, col];
                    cell.Value = record.GetField("GroupName");
                    ApplyGroupNameCellStyle(cell);
                    col++;
                }

                foreach (var ec in columns)
                {
                    var cell = sheet.Cells[r + 2, col];
                    cell.Value = record.GetField(ec.FieldName);

                    if (ec.FieldName == "GroupName")
                        ApplyGroupNameCellStyle(cell);
                    else if (isIncomplete && string.IsNullOrWhiteSpace(cell.Text))
                    {
                        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 230, 230));
                    }
                    col++;
                }

                if (appendTimeAxis)
                {
                    var cell = sheet.Cells[r + 2, col];
                    cell.Value = record.GetField("TimeAxis");
                    ApplyTimeAxisCellStyle(cell);
                    col++;
                }
            }

            for (int c = 1; c <= totalCols; c++)
            {
                double maxLen = GetDisplayWidth(sheet.Cells[1, c].Text ?? string.Empty);
                for (int r = 0; r < records.Count; r++)
                {
                    string val = sheet.Cells[r + 2, c].Text ?? string.Empty;
                    double len = GetDisplayWidth(val);
                    if (len > maxLen) maxLen = len;
                }
                sheet.Column(c).Width = System.Math.Max(8, System.Math.Min(maxLen + 2, 60));
            }

            sheet.View.FreezePanes(2, 1);
            if (records.Count > 0)
                sheet.Cells[1, 1, 1, totalCols].AutoFilter = true;
        }

        // ── 样式辅助 ─────────────────────────────────────────────────────────

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

        private static void WriteTimeAxisHeader(ExcelRange cell)
        {
            cell.Value = "时间轴";
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(TimeAxisHeaderColor);
            cell.Style.Font.Color.SetColor(Color.White);
        }

        private static void ApplyTimeAxisCellStyle(ExcelRange cell)
        {
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(TimeAxisCellColor);
        }

        private static double GetDisplayWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            double width = 0;
            foreach (char c in text)
                width += c > 0x7F ? 2 : 1;
            return width;
        }

        private static string SanitizeSheetName(string name)
        {
            foreach (var ch in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                name = name.Replace(ch, '_');
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }

        private class ExportColumn
        {
            public string FieldName { get; set; } = string.Empty;
            public string ColumnHeader { get; set; } = string.Empty;
            public int Order { get; set; }
        }
    }
}
