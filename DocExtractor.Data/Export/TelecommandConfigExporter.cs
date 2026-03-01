using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using DocExtractor.Core.Protocol;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace DocExtractor.Data.Export
{
    [Flags]
    public enum TelecommandExportFormat
    {
        FormatA = 1,
        FormatB = 2,
        Both = FormatA | FormatB
    }

    public class TelecommandExportOptions
    {
        public TelecommandExportFormat Formats { get; set; } = TelecommandExportFormat.Both;
        public string? CodePrefixOverride { get; set; }
        public bool IncludeUsageSheet { get; set; } = true;
    }

    /// <summary>
    /// Exports telecommand parsing result into two compatible Excel formats.
    /// </summary>
    public class TelecommandConfigExporter
    {
        private static readonly Color HeaderBg = Color.FromArgb(0, 128, 0);
        private static readonly Color HeaderFg = Color.White;

        public TelecommandConfigExporter()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public List<string> Export(TelecommandParseResult result, string outputDir, TelecommandExportOptions? options = null)
        {
            var opts = options ?? new TelecommandExportOptions();
            Directory.CreateDirectory(outputDir);
            var files = new List<string>();
            var analyzer = new TelecommandAnalyzer();

            if (opts.Formats.HasFlag(TelecommandExportFormat.FormatA))
            {
                string pathA = Path.Combine(outputDir, $"{result.SystemName}_遥控指令配置表.xlsx");
                using (var pkg = new ExcelPackage())
                {
                    WriteFormatA(pkg, result, analyzer, opts);
                    pkg.SaveAs(new FileInfo(pathA));
                }
                files.Add(pathA);
            }

            if (opts.Formats.HasFlag(TelecommandExportFormat.FormatB))
            {
                string pathB = Path.Combine(outputDir, $"{result.SystemName}_TelecommandConfig.xlsx");
                using (var pkg = new ExcelPackage())
                {
                    WriteFormatB(pkg, result, analyzer, opts);
                    pkg.SaveAs(new FileInfo(pathB));
                }
                files.Add(pathB);
            }

            return files;
        }

        private void WriteFormatA(ExcelPackage pkg, TelecommandParseResult result, TelecommandAnalyzer analyzer, TelecommandExportOptions opts)
        {
            var update = pkg.Workbook.Worksheets.Add("更新记录");
            update.Cells[1, 1].Value = "更新日期";
            update.Cells[1, 2].Value = "更新人员";
            update.Cells[1, 3].Value = "更新内容";
            update.Column(1).Width = 16;
            update.Column(2).Width = 14;
            update.Column(3).Width = 60;

            WriteFormatAChannelSheet(pkg.Workbook.Worksheets.Add("指令配置-A通道"), result, analyzer, false, opts);
            WriteFormatAChannelSheet(pkg.Workbook.Worksheets.Add("指令配置-B通道"), result, analyzer, true, opts);
            WriteFormatAUploadSheet(pkg.Workbook.Worksheets.Add("上注"), result.SystemName);
        }

        private void WriteFormatAChannelSheet(
            ExcelWorksheet sheet,
            TelecommandParseResult result,
            TelecommandAnalyzer analyzer,
            bool isBChannel,
            TelecommandExportOptions opts)
        {
            string[] headers = new[]
            {
                "所属系统", "遥控指令代号", "遥控指令名称", "遥控指令",
                "起始字节", "起始位", "字节长度/位长度", "量纲",
                "是否显示", "字节端序", "默认内容", "数据类型", "转换规则"
            };

            for (int i = 0; i < headers.Length; i++)
                SetHeaderCell(sheet, 1, i + 1, headers[i]);

            int row = 2;
            string suffix = isBChannel ? "B" : "A";
            string prefix = string.IsNullOrEmpty(opts.CodePrefixOverride) ? result.SystemName : opts.CodePrefixOverride!;
            var aliasCounter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (TelecommandEntry cmd in result.Commands.OrderBy(c => c.CommandCode))
            {
                string aliasBase = string.IsNullOrEmpty(cmd.CodeAlias) ? "CMD" : cmd.CodeAlias;
                int serial = NextSerial(aliasCounter, aliasBase);
                string code = $"{aliasBase}-{serial:00}-{suffix}";

                byte[] frame = analyzer.BuildCommandFrame(cmd, isBChannel, result.FrameInfos);
                sheet.Cells[row, 1].Value = result.SystemName;
                sheet.Cells[row, 2].Value = code;
                sheet.Cells[row, 3].Value = $"{cmd.Name}-{suffix}通道";
                sheet.Cells[row, 4].Value = BytesToHex(frame);
                sheet.Cells[row, 5].Value = cmd.Parameters.Count > 0 ? cmd.Parameters[0].StartByte : "";
                sheet.Cells[row, 6].Value = cmd.Parameters.Count > 0 ? cmd.Parameters[0].StartBit : "";
                sheet.Cells[row, 7].Value = cmd.Parameters.Count > 0 ? cmd.Parameters[0].Length.ToString(CultureInfo.InvariantCulture) : "";
                sheet.Cells[row, 8].Value = cmd.Parameters.Count > 0 ? cmd.Parameters[0].Unit : "";
                sheet.Cells[row, 9].Value = cmd.Parameters.Count > 0 ? "显示" : "";
                sheet.Cells[row, 10].Value = result.DefaultEndianness;
                sheet.Cells[row, 11].Value = cmd.Parameters.Count > 0 ? cmd.Parameters[0].DefaultValue : "";
                sheet.Cells[row, 12].Value = cmd.Parameters.Count > 0 ? cmd.Parameters[0].DataFormat : "";
                sheet.Cells[row, 13].Value = "";
                row++;

                if (cmd.Parameters.Count > 1)
                {
                    for (int pi = 1; pi < cmd.Parameters.Count; pi++)
                    {
                        TelecommandParameter p = cmd.Parameters[pi];
                        sheet.Cells[row, 5].Value = p.StartByte;
                        sheet.Cells[row, 6].Value = p.StartBit;
                        sheet.Cells[row, 7].Value = p.Length.ToString(CultureInfo.InvariantCulture);
                        sheet.Cells[row, 8].Value = p.Unit;
                        sheet.Cells[row, 9].Value = "显示";
                        sheet.Cells[row, 10].Value = result.DefaultEndianness;
                        sheet.Cells[row, 11].Value = p.DefaultValue;
                        sheet.Cells[row, 12].Value = p.DataFormat;
                        row++;
                    }
                }

                foreach (TelecommandPreset preset in cmd.Presets)
                {
                    int presetSerial = NextSerial(aliasCounter, aliasBase);
                    string presetCode = $"{aliasBase}-{presetSerial:00}-{suffix}";
                    byte[] framePreset = analyzer.BuildCommandFrameWithPreset(cmd, preset, isBChannel, result.FrameInfos);

                    sheet.Cells[row, 1].Value = result.SystemName;
                    sheet.Cells[row, 2].Value = presetCode;
                    sheet.Cells[row, 3].Value = preset.Name;
                    sheet.Cells[row, 4].Value = BytesToHex(framePreset);
                    row++;
                }
            }

            ApplyCommonStyle(sheet, row - 1, headers.Length);
        }

        private void WriteFormatAUploadSheet(ExcelWorksheet sheet, string systemName)
        {
            string[] headers = new[]
            {
                "所属系统", "遥控指令代号", "遥控指令名称", "遥控指令",
                "起始字节", "起始位", "字节长度/位长度", "量纲",
                "是否显示", "字节端序", "默认内容", "数据类型", "转换规则"
            };
            for (int i = 0; i < headers.Length; i++)
                SetHeaderCell(sheet, 1, i + 1, headers[i]);
            ApplyCommonStyle(sheet, 1, headers.Length);
        }

        private void WriteFormatB(ExcelPackage pkg, TelecommandParseResult result, TelecommandAnalyzer analyzer, TelecommandExportOptions opts)
        {
            var global = pkg.Workbook.Worksheets.Add("全局参数");
            WriteGlobalParamsSheet(global);

            var commands = pkg.Workbook.Worksheets.Add("指令配置");
            WriteFormatBCommandSheet(commands, result, analyzer);

            if (opts.IncludeUsageSheet)
            {
                var usage = pkg.Workbook.Worksheets.Add("使用说明");
                WriteUsageSheet(usage);
            }
        }

        private void WriteGlobalParamsSheet(ExcelWorksheet sheet)
        {
            string[] headers = { "参数ID", "参数名称", "参数描述", "起始字节", "长度", "输入类型", "数据格式", "默认值", "选项值", "取值范围" };
            for (int i = 0; i < headers.Length; i++)
                SetHeaderCell(sheet, 1, i + 1, headers[i]);

            sheet.Cells[2, 1].Value = "GLOBAL_CHANNEL";
            sheet.Cells[2, 2].Value = "通道选择";
            sheet.Cells[2, 3].Value = "A/B通道切换";
            sheet.Cells[2, 4].Value = "WD4,3";
            sheet.Cells[2, 5].Value = "2";
            sheet.Cells[2, 6].Value = "ComboBox";
            sheet.Cells[2, 7].Value = "Binary";
            sheet.Cells[2, 8].Value = "01";
            sheet.Cells[2, 9].Value = "01:A通道;10:B通道";

            sheet.Cells[3, 1].Value = "GLOBAL_MODE";
            sheet.Cells[3, 2].Value = "指令模式";
            sheet.Cells[3, 3].Value = "遥测请求/间接指令";
            sheet.Cells[3, 4].Value = "WD3,1";
            sheet.Cells[3, 5].Value = "2";
            sheet.Cells[3, 6].Value = "ComboBox";
            sheet.Cells[3, 7].Value = "Binary";
            sheet.Cells[3, 8].Value = "10";
            sheet.Cells[3, 9].Value = "10:遥测请求;01:间接指令";

            ApplyCommonStyle(sheet, 3, headers.Length);
        }

        private void WriteFormatBCommandSheet(ExcelWorksheet sheet, TelecommandParseResult result, TelecommandAnalyzer analyzer)
        {
            string[] headers = new[]
            {
                "指令ID", "指令名称", "指令描述", "基础字节", "分类",
                "参数ID", "参数名称", "起始字节", "长度", "输入类型", "数据格式", "默认值", "选项值", "取值范围",
                "校验类型", "校验起始字节", "校验结束字节", "校验结果位置", "校验结果长度"
            };
            for (int i = 0; i < headers.Length; i++)
                SetHeaderCell(sheet, 1, i + 1, headers[i]);

            int row = 2;
            int paramCounter = 1;
            foreach (TelecommandEntry cmd in result.Commands.OrderBy(c => c.CommandCode))
            {
                byte[] frame = analyzer.BuildCommandFrame(cmd, false, result.FrameInfos);
                string category = ResolveCategory(cmd);
                string cmdId = string.IsNullOrEmpty(cmd.CodeAlias) ? "CMD_" + cmd.CommandCode.ToString("X2") : cmd.CodeAlias;

                if (cmd.Parameters.Count == 0)
                {
                    WriteFormatBRow(
                        sheet, row++, cmdId, cmd.Name, cmd.Remark, BytesToHexWithSpaces(frame), category,
                        $"PARAM_{paramCounter:000}", "", "", "", "", "", "", "",
                        "Sum", "5", "10", "W11", "1");
                    paramCounter++;
                }
                else
                {
                    for (int i = 0; i < cmd.Parameters.Count; i++)
                    {
                        TelecommandParameter p = cmd.Parameters[i];
                        WriteFormatBRow(
                            sheet, row++,
                            i == 0 ? cmdId : "",
                            i == 0 ? cmd.Name : "",
                            i == 0 ? cmd.Remark : "",
                            i == 0 ? BytesToHexWithSpaces(frame) : "",
                            i == 0 ? category : "",
                            $"PARAM_{paramCounter:000}",
                            p.Name,
                            p.StartByte,
                            p.Length.ToString(CultureInfo.InvariantCulture),
                            string.IsNullOrEmpty(p.InputType) ? "TextBox" : p.InputType,
                            string.IsNullOrEmpty(p.DataFormat) ? "Decimal" : p.DataFormat,
                            string.IsNullOrEmpty(p.DefaultValue) ? "0" : p.DefaultValue,
                            p.OptionValues,
                            p.ValueRange,
                            "Sum", "5", "10", "W11", "1");
                        paramCounter++;
                    }
                }

                foreach (TelecommandPreset preset in cmd.Presets)
                {
                    byte[] framePreset = analyzer.BuildCommandFrameWithPreset(cmd, preset, false, result.FrameInfos);
                    WriteFormatBRow(
                        sheet, row++,
                        $"{cmdId}_{paramCounter:000}",
                        preset.Name,
                        preset.Remark,
                        BytesToHexWithSpaces(framePreset),
                        "参数上注指令",
                        $"PARAM_{paramCounter:000}",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "Sum", "5", "10", "W11", "1");
                    paramCounter++;
                }
            }

            ApplyCommonStyle(sheet, row - 1, headers.Length);
        }

        private void WriteUsageSheet(ExcelWorksheet sheet)
        {
            sheet.Cells[1, 1].Value = "遥控指令生成器 - 配置文件使用说明";
            sheet.Cells[3, 1].Value = "1. 全局参数用于所有指令的通道/模式控制。";
            sheet.Cells[4, 1].Value = "2. 指令配置中每一行对应一条指令或参数定义。";
            sheet.Cells[5, 1].Value = "3. 基础字节支持空格分隔 Hex 串，参数可覆盖指定字节。";
            sheet.Cells[6, 1].Value = "4. 校验默认使用 Sum，范围 B5~B10，结果写入 W11。";
            sheet.Column(1).Width = 80;
        }

        private void WriteFormatBRow(
            ExcelWorksheet sheet,
            int row,
            string cmdId,
            string cmdName,
            string desc,
            string baseBytes,
            string category,
            string paramId,
            string paramName,
            string startByte,
            string length,
            string inputType,
            string dataFormat,
            string defaultValue,
            string optionValues,
            string range,
            string checksumType,
            string checksumStart,
            string checksumEnd,
            string checksumPos,
            string checksumLen = "")
        {
            sheet.Cells[row, 1].Value = cmdId;
            sheet.Cells[row, 2].Value = cmdName;
            sheet.Cells[row, 3].Value = desc;
            sheet.Cells[row, 4].Value = baseBytes;
            sheet.Cells[row, 5].Value = category;
            sheet.Cells[row, 6].Value = paramId;
            sheet.Cells[row, 7].Value = paramName;
            sheet.Cells[row, 8].Value = startByte;
            sheet.Cells[row, 9].Value = length;
            sheet.Cells[row, 10].Value = inputType;
            sheet.Cells[row, 11].Value = dataFormat;
            sheet.Cells[row, 12].Value = defaultValue;
            sheet.Cells[row, 13].Value = optionValues;
            sheet.Cells[row, 14].Value = range;
            sheet.Cells[row, 15].Value = checksumType;
            sheet.Cells[row, 16].Value = checksumStart;
            sheet.Cells[row, 17].Value = checksumEnd;
            sheet.Cells[row, 18].Value = checksumPos;
            sheet.Cells[row, 19].Value = checksumLen;
        }

        private string ResolveCategory(TelecommandEntry cmd)
        {
            if (cmd.Type == TelecommandType.TelemetryRequest)
                return "遥测请求";
            if (cmd.Parameters.Count > 0 || cmd.Presets.Count > 0)
                return "参数上注指令";
            return "不带参控制短指令";
        }

        private int NextSerial(Dictionary<string, int> counters, string alias)
        {
            if (!counters.TryGetValue(alias, out int val))
                val = 0;
            val++;
            counters[alias] = val;
            return val;
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
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }

        private void ApplyCommonStyle(ExcelWorksheet sheet, int lastRow, int lastCol)
        {
            if (lastRow < 1) lastRow = 1;
            sheet.Cells[1, 1, 1, lastCol].AutoFilter = true;
            sheet.View.FreezePanes(2, 1);

            for (int c = 1; c <= lastCol; c++)
            {
                int max = (sheet.Cells[1, c].Text ?? "").Length;
                for (int r = 2; r <= lastRow; r++)
                {
                    int len = (sheet.Cells[r, c].Text ?? "").Length;
                    if (len > max) max = len;
                }
                sheet.Column(c).Width = Math.Max(10, Math.Min(max + 4, 60));
            }
        }

        private string BytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            return string.Concat(bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        }

        private string BytesToHexWithSpaces(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            return string.Join(" ", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        }
    }
}
