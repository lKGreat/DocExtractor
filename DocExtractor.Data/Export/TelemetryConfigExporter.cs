using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using DocExtractor.Core.Protocol;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace DocExtractor.Data.Export
{
    /// <summary>
    /// Generates a single telemetry configuration Excel file from a <see cref="ProtocolParseResult"/>.
    /// Sync and async telemetry are placed on separate sheets within the same workbook,
    /// together with APID queue settings and formula reference sheets.
    /// </summary>
    public class TelemetryConfigExporter
    {
        private static readonly Color HeaderBg = Color.FromArgb(0, 128, 0);
        private static readonly Color HeaderFg = Color.White;
        private static readonly Color AltRowBg = Color.FromArgb(242, 248, 242);

        public TelemetryConfigExporter()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// Export telemetry config to a single Excel file.
        /// Sync and async telemetry are written as separate sheets within one workbook.
        /// Returns the list containing the single generated file path.
        /// </summary>
        public List<string> Export(ProtocolParseResult result, string outputDir, ExportOptions? options = null)
        {
            var opts = options ?? new ExportOptions();
            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir,
                $"{result.SystemName}_遥测解析配置.xlsx");

            using var package = new ExcelPackage();

            WriteUpdateLogSheet(package);

            var allSyncFields = new List<ProtocolTelemetryField>();
            foreach (var t in result.SyncTables)
                allSyncFields.AddRange(t.Fields);

            var allAsyncFields = new List<ProtocolTelemetryField>();
            foreach (var t in result.AsyncTables)
                allAsyncFields.AddRange(t.Fields);

            var allChannels = new List<ChannelInfo>();

            if (allSyncFields.Count > 0)
            {
                foreach (var channel in result.SyncChannels)
                {
                    string suffix = channel.ChannelLabel.Contains("A") ? "A" : "B";
                    string sheetName = $"{result.SystemName}同步遥测解析-{suffix}";
                    var sheet = package.Workbook.Worksheets.Add(sheetName);
                    WriteDataSheet(sheet, allSyncFields, result, channel, "TB", opts);
                    if (!allChannels.Exists(c => c.FrameIdHex == channel.FrameIdHex))
                        allChannels.Add(channel);
                }
            }

            if (allAsyncFields.Count > 0)
            {
                foreach (var channel in result.AsyncChannels)
                {
                    string suffix = channel.ChannelLabel.Contains("A") ? "A" : "B";
                    string sheetName = $"{result.SystemName}异步遥测解析-{suffix}";
                    var sheet = package.Workbook.Worksheets.Add(sheetName);
                    WriteDataSheet(sheet, allAsyncFields, result, channel, "YB", opts);
                    if (!allChannels.Exists(c => c.FrameIdHex == channel.FrameIdHex))
                        allChannels.Add(channel);
                }
            }

            if (allChannels.Count == 0)
            {
                allChannels.AddRange(result.SyncChannels);
                foreach (var ch in result.AsyncChannels)
                {
                    if (!allChannels.Exists(c => c.FrameIdHex == ch.FrameIdHex))
                        allChannels.Add(ch);
                }
            }

            WriteApidQueueSheet(package, allChannels, result.SystemName);
            WriteFormulaSheet(package);

            var fi = new FileInfo(outputPath);
            if (fi.Directory != null && !fi.Directory.Exists)
                fi.Directory.Create();
            package.SaveAs(fi);

            return new List<string> { outputPath };
        }

        private void WriteDataSheet(
            ExcelWorksheet sheet,
            List<ProtocolTelemetryField> fields,
            ProtocolParseResult result,
            ChannelInfo channel,
            string codeTypeSuffix,
            ExportOptions opts)
        {
            string[] headers = {
                "序号", "所属系统", "APID值", "起始字节", "起始位",
                "字节长度/位长度", "波道名称", "遥测代号", "字节端序",
                "公式类型", "公式系数", "小数位数", "量纲", "枚举解译", "所属包"
            };

            for (int c = 0; c < headers.Length; c++)
                SetHeaderCell(sheet, 1, c + 1, headers[c]);

            int row = 2;
            int seq = 1;
            string prefix = opts.TelemetryCodePrefix ?? result.SystemName;

            foreach (var field in fields)
            {
                if (field.IsHeaderField && !opts.IncludeHeaderFields) continue;
                if (field.IsChecksum && !opts.IncludeChecksum) continue;

                sheet.Cells[row, 1].Value = seq;
                sheet.Cells[row, 2].Value = result.SystemName;
                sheet.Cells[row, 3].Value = channel.FrameIdHex;
                sheet.Cells[row, 4].Value = FormatStartByte(field);
                sheet.Cells[row, 5].Value = FormatBitOffset(field);
                sheet.Cells[row, 6].Value = FormatLength(field);
                sheet.Cells[row, 7].Value = field.FieldName;
                sheet.Cells[row, 8].Value = $"{prefix}{codeTypeSuffix}-{seq:D4}";
                sheet.Cells[row, 9].Value = result.DefaultEndianness;
                sheet.Cells[row, 10].Value = opts.DefaultFormulaType;
                sheet.Cells[row, 11].Value = opts.DefaultFormulaCoeff;
                sheet.Cells[row, 12].Value = "";
                sheet.Cells[row, 13].Value = field.Unit;
                sheet.Cells[row, 14].Value = field.EnumMapping;
                sheet.Cells[row, 15].Value = channel.ChannelLabel;

                if (row % 2 == 1)
                {
                    for (int c = 1; c <= headers.Length; c++)
                    {
                        sheet.Cells[row, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[row, c].Style.Fill.BackgroundColor.SetColor(AltRowBg);
                    }
                }

                row++;
                seq++;
            }

            for (int c = 1; c <= headers.Length; c++)
            {
                double maxWidth = GetDisplayWidth(headers[c - 1]);
                for (int r = 2; r < row; r++)
                {
                    string val = sheet.Cells[r, c].Text ?? "";
                    double w = GetDisplayWidth(val);
                    if (w > maxWidth) maxWidth = w;
                }
                sheet.Column(c).Width = Math.Max(8, Math.Min(maxWidth + 2, 50));
            }

            sheet.View.FreezePanes(2, 1);
            if (row > 2)
                sheet.Cells[1, 1, 1, headers.Length].AutoFilter = true;
        }

        private void WriteApidQueueSheet(ExcelPackage package, List<ChannelInfo> channels, string systemName)
        {
            var sheet = package.Workbook.Worksheets.Add("遥测帧APID队列设置");

            SetHeaderCell(sheet, 1, 1, "首帧APID号\n(16进制CAN帧ID号\n+10进制CAN帧个数)");
            sheet.Cells[2, 1].Value = "往下皆为续帧ID号\n(不包含帧长信息)";
            sheet.Cells[2, 1].Style.WrapText = true;

            int col = 2;
            foreach (var ch in channels)
            {
                string apid = ch.FrameIdHex;
                string firstFrame = !string.IsNullOrEmpty(apid)
                    ? apid + (ch.FrameCount > 0 ? ch.FrameCount.ToString() : "")
                    : "";
                sheet.Cells[1, col].Value = firstFrame;

                for (int r = 2; r <= 10; r++)
                    sheet.Cells[r, col].Value = apid;

                col++;
            }

            sheet.Column(1).Width = 30;
            sheet.Cells[1, 1].Style.WrapText = true;
            for (int c = 2; c <= col; c++)
                sheet.Column(c).Width = 18;
        }

        private void WriteFormulaSheet(ExcelPackage package)
        {
            var sheet = package.Workbook.Worksheets.Add("公式");

            SetHeaderCell(sheet, 1, 1, "公式号");
            SetHeaderCell(sheet, 1, 2, "公式系数");
            SetHeaderCell(sheet, 1, 3, "公式");
            SetHeaderCell(sheet, 1, 4, "说明");

            var formulas = new[]
            {
                new[] { "0", "1/0/", "源码显示", "源码显示" },
                new[] { "5", "A/B/", "X = A × DU + B", "无符号整型线性公式" },
                new[] { "6", "A/B/", "X = A × DI + B", "有符号整型线性公式" },
                new[] { "7", "A/B/", "X = A × DF32 + B", "单精度浮点线性公式" },
                new[] { "8", "A/B/", "X = A × DF64 + B", "双精度浮点线性公式" },
            };

            for (int r = 0; r < formulas.Length; r++)
            {
                for (int c = 0; c < formulas[r].Length; c++)
                    sheet.Cells[r + 2, c + 1].Value = formulas[r][c];
            }

            sheet.Column(1).Width = 10;
            sheet.Column(2).Width = 12;
            sheet.Column(3).Width = 30;
            sheet.Column(4).Width = 25;
        }

        private void WriteUpdateLogSheet(ExcelPackage package)
        {
            var sheet = package.Workbook.Worksheets.Add("更新记录");

            SetHeaderCell(sheet, 1, 1, "日期");
            SetHeaderCell(sheet, 1, 2, "版本");
            SetHeaderCell(sheet, 1, 3, "修改内容");
            SetHeaderCell(sheet, 1, 4, "修改人");

            sheet.Column(1).Width = 16;
            sheet.Column(2).Width = 12;
            sheet.Column(3).Width = 40;
            sheet.Column(4).Width = 14;
        }

        private string FormatStartByte(ProtocolTelemetryField field)
        {
            if (field.BitOffset >= 0 && field.BitLength > 0)
                return "WD" + ExtractByteNumber(field.ByteSequence);
            string seq = field.ByteSequence;
            if (!string.IsNullOrEmpty(seq) && seq.Contains("-"))
                return "W" + ExtractByteNumber(seq);
            return seq;
        }

        private string FormatBitOffset(ProtocolTelemetryField field)
        {
            if (field.BitOffset >= 0 && field.BitLength > 0)
                return field.BitOffset.ToString();
            return "";
        }

        private string FormatLength(ProtocolTelemetryField field)
        {
            if (field.BitLength > 0)
                return field.BitLength.ToString();
            return field.ByteLength > 0 ? field.ByteLength.ToString() : "1";
        }

        private string ExtractByteNumber(string byteSeq)
        {
            if (string.IsNullOrEmpty(byteSeq)) return byteSeq;
            var cleaned = byteSeq.Replace("W", "").Replace("w", "")
                                  .Replace("B", "").Replace("b", "");
            var dashIdx = cleaned.IndexOf('-');
            if (dashIdx > 0)
                cleaned = cleaned.Substring(0, dashIdx);
            return cleaned;
        }

        private void SetHeaderCell(ExcelWorksheet sheet, int row, int col, string text)
        {
            var cell = sheet.Cells[row, col];
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(HeaderBg);
            cell.Style.Font.Color.SetColor(HeaderFg);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        private static double GetDisplayWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            double width = 0;
            foreach (char c in text)
                width += c > 0x7F ? 2 : 1;
            return width;
        }
    }

    /// <summary>
    /// Options for controlling the telemetry config export behavior.
    /// </summary>
    public class ExportOptions
    {
        /// <summary>Override the auto-detected telemetry code prefix (default: use SystemName)</summary>
        public string? TelemetryCodePrefix { get; set; }

        /// <summary>Default formula type number (default: "5" = unsigned linear)</summary>
        public string DefaultFormulaType { get; set; } = "5";

        /// <summary>Default formula coefficients (default: "1/0/")</summary>
        public string DefaultFormulaCoeff { get; set; } = "1/0/";

        /// <summary>Whether to include Dh/Dl header fields in output</summary>
        public bool IncludeHeaderFields { get; set; } = false;

        /// <summary>Whether to include the SUM checksum field in output</summary>
        public bool IncludeChecksum { get; set; } = true;
    }
}
