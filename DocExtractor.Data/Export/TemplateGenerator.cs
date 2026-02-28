using System;
using System.Collections.Generic;
using System.IO;
using DocExtractor.Core.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace DocExtractor.Data.Export
{
    /// <summary>
    /// 训练数据模板生成器（code-first）
    /// 自动生成 Excel 模板，预填充遥测领域的列名映射和 NER 标注示例
    /// </summary>
    public static class TemplateGenerator
    {
        /// <summary>
        /// 生成列名训练数据模板
        /// </summary>
        /// <param name="outputPath">输出文件路径</param>
        public static void GenerateColumnTrainingTemplate(string outputPath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();

            var sheet = package.Workbook.Worksheets.Add("列名训练数据");

            // 表头
            SetHeader(sheet, 1, 1, "原始列名");
            SetHeader(sheet, 1, 2, "规范字段名");
            SetHeader(sheet, 1, 3, "显示名称（参考）");

            // 预填充遥测领域映射数据
            int row = 2;
            foreach (var mapping in GetTelemetryColumnMappings())
            {
                sheet.Cells[row, 1].Value = mapping.RawColumnName;
                sheet.Cells[row, 2].Value = mapping.CanonicalFieldName;
                sheet.Cells[row, 3].Value = mapping.DisplayName;
                row++;
            }

            // 添加空行供用户填写
            for (int i = 0; i < 20; i++)
            {
                sheet.Cells[row + i, 1].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                sheet.Cells[row + i, 1].Style.Border.Bottom.Color.SetColor(Color.LightGray);
                sheet.Cells[row + i, 2].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                sheet.Cells[row + i, 2].Style.Border.Bottom.Color.SetColor(Color.LightGray);
            }

            // 说明 Sheet
            var helpSheet = package.Workbook.Worksheets.Add("使用说明");
            helpSheet.Cells[1, 1].Value = "列名训练数据模板 — 使用说明";
            helpSheet.Cells[1, 1].Style.Font.Bold = true;
            helpSheet.Cells[1, 1].Style.Font.Size = 14;
            helpSheet.Cells[3, 1].Value = "1. 在「列名训练数据」Sheet 中填写训练样本";
            helpSheet.Cells[4, 1].Value = "2. 每行一条：「原始列名」是文档中实际出现的列标题，「规范字段名」是系统规范化后的字段名";
            helpSheet.Cells[5, 1].Value = "3. 已预填充的遥测领域映射可直接使用，也可修改或删除";
            helpSheet.Cells[6, 1].Value = "4. 建议每个规范字段至少提供 3-5 个不同的原始列名变体";
            helpSheet.Cells[7, 1].Value = "5. 训练至少需要 10 条数据（当前已预填充约 60 条）";
            helpSheet.Cells[8, 1].Value = "6. 填写完成后，在程序的「模型训练」页面导入此文件";
            helpSheet.Column(1).Width = 80;

            // 自动列宽
            sheet.Column(1).AutoFit(15, 40);
            sheet.Column(2).AutoFit(15, 30);
            sheet.Column(3).AutoFit(15, 30);
            sheet.View.FreezePanes(2, 1);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            package.SaveAs(new FileInfo(outputPath));
        }

        /// <summary>
        /// 生成 NER 标注数据模板
        /// </summary>
        public static void GenerateNerTrainingTemplate(string outputPath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();

            var sheet = package.Workbook.Worksheets.Add("NER标注数据");

            SetHeader(sheet, 1, 1, "原文本");
            SetHeader(sheet, 1, 2, "实体标注JSON");
            SetHeader(sheet, 1, 3, "说明");

            // 预填充示例
            var samples = GetNerSamples();
            int row = 2;
            foreach (var sample in samples)
            {
                sheet.Cells[row, 1].Value = sample.Text;
                sheet.Cells[row, 2].Value = sample.EntityJson;
                sheet.Cells[row, 3].Value = sample.Note;
                row++;
            }

            // 说明 Sheet
            var helpSheet = package.Workbook.Worksheets.Add("标注格式说明");
            helpSheet.Cells[1, 1].Value = "NER 标注格式说明";
            helpSheet.Cells[1, 1].Style.Font.Bold = true;
            helpSheet.Cells[1, 1].Style.Font.Size = 14;
            helpSheet.Cells[3, 1].Value = "实体类型: Value(数值), Unit(单位), HexCode(十六进制), Formula(公式), Enum(枚举), Condition(条件)";
            helpSheet.Cells[4, 1].Value = "JSON 格式: [{\"start\":起始位置, \"end\":结束位置, \"type\":\"实体类型\", \"text\":\"实体文本\"}]";
            helpSheet.Cells[5, 1].Value = "位置是 0-based 字符索引（包含 start 和 end）";
            helpSheet.Cells[6, 1].Value = "训练至少需要 20 条标注文本";
            helpSheet.Column(1).Width = 80;

            sheet.Column(1).AutoFit(20, 50);
            sheet.Column(2).AutoFit(30, 80);
            sheet.Column(3).AutoFit(15, 40);
            sheet.View.FreezePanes(2, 1);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            package.SaveAs(new FileInfo(outputPath));
        }

        /// <summary>
        /// 生成空白字段配置模板（含下拉验证和使用说明）
        /// </summary>
        public static void GenerateConfigTemplate(string outputPath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();

            // Sheet 1：字段配置
            var sheet = package.Workbook.Worksheets.Add("字段配置");
            SetHeader(sheet, 1, 1, "字段名（英文）");
            SetHeader(sheet, 1, 2, "显示名（中文）");
            SetHeader(sheet, 1, 3, "数据类型");
            SetHeader(sheet, 1, 4, "必填");
            SetHeader(sheet, 1, 5, "默认值");
            SetHeader(sheet, 1, 6, "列名变体（分号分隔）");

            // 数据类型下拉验证
            var dtValidation = sheet.DataValidations.AddListValidation("C2:C200");
            foreach (var name in Enum.GetNames(typeof(FieldDataType)))
                dtValidation.Formula.Values.Add(name);
            dtValidation.ShowErrorMessage = true;
            dtValidation.ErrorTitle = "无效的数据类型";
            dtValidation.Error = "请从下拉列表中选择数据类型";

            // 必填下拉验证
            var reqValidation = sheet.DataValidations.AddListValidation("D2:D200");
            reqValidation.Formula.Values.Add("是");
            reqValidation.Formula.Values.Add("否");

            // 添加空行底线引导
            for (int i = 2; i <= 21; i++)
            {
                sheet.Cells[i, 1].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                sheet.Cells[i, 1].Style.Border.Bottom.Color.SetColor(Color.LightGray);
                sheet.Cells[i, 2].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                sheet.Cells[i, 2].Style.Border.Bottom.Color.SetColor(Color.LightGray);
            }

            sheet.Column(1).AutoFit(15, 30);
            sheet.Column(2).AutoFit(15, 30);
            sheet.Column(3).Width = 15;
            sheet.Column(4).Width = 8;
            sheet.Column(5).Width = 15;
            sheet.Column(6).AutoFit(20, 60);
            sheet.View.FreezePanes(2, 1);

            // Sheet 2：配置信息
            var metaSheet = package.Workbook.Worksheets.Add("配置信息");
            metaSheet.Cells[1, 1].Value = "配置名称";
            metaSheet.Cells[1, 1].Style.Font.Bold = true;
            metaSheet.Cells[1, 2].Value = "";
            metaSheet.Cells[2, 1].Value = "表头行数";
            metaSheet.Cells[2, 1].Style.Font.Bold = true;
            metaSheet.Cells[2, 2].Value = 1;
            metaSheet.Cells[3, 1].Value = "列名匹配模式";
            metaSheet.Cells[3, 1].Style.Font.Bold = true;
            metaSheet.Cells[3, 2].Value = "HybridMlFirst";
            metaSheet.Cells[4, 1].Value = "启用值归一化";
            metaSheet.Cells[4, 1].Style.Font.Bold = true;
            metaSheet.Cells[4, 2].Value = "是";
            var matchValidation = metaSheet.DataValidations.AddListValidation("B3");
            foreach (var name in Enum.GetNames(typeof(ColumnMatchMode)))
                matchValidation.Formula.Values.Add(name);
            var normValidation = metaSheet.DataValidations.AddListValidation("B4");
            normValidation.Formula.Values.Add("是");
            normValidation.Formula.Values.Add("否");
            metaSheet.Column(1).Width = 20;
            metaSheet.Column(2).Width = 30;

            // Sheet 3：使用说明
            var helpSheet = package.Workbook.Worksheets.Add("使用说明");
            helpSheet.Cells[1, 1].Value = "字段配置模板 — 使用说明";
            helpSheet.Cells[1, 1].Style.Font.Bold = true;
            helpSheet.Cells[1, 1].Style.Font.Size = 14;
            helpSheet.Cells[3, 1].Value = "1. 在「字段配置」Sheet 中填写需要抽取的字段定义";
            helpSheet.Cells[4, 1].Value = "2.「字段名」为英文标识符（如 TelemetryCode），「显示名」为中文名称（如 遥测代号）";
            helpSheet.Cells[5, 1].Value = "3.「数据类型」从下拉菜单选择：Text, Integer, Decimal, HexCode, Enumeration, Formula, Unit, Boolean";
            helpSheet.Cells[6, 1].Value = "4.「列名变体」填写文档中可能出现的列标题变体，用分号隔开，如：遥测代号;参数代号;代号";
            helpSheet.Cells[7, 1].Value = "5. 在「配置信息」Sheet 中填写配置名称（必填）和全局参数";
            helpSheet.Cells[8, 1].Value = "6. 填写完成后，在程序「字段配置」页面点击「从 Excel 导入」按钮导入";
            helpSheet.Column(1).Width = 90;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            package.SaveAs(new FileInfo(outputPath));
        }

        /// <summary>
        /// 将当前配置导出为 Excel 模板文件（带数据）
        /// </summary>
        public static void GenerateConfigTemplateWithData(string outputPath, ExtractionConfig config)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();

            // Sheet 1：字段配置（带数据）
            var sheet = package.Workbook.Worksheets.Add("字段配置");
            SetHeader(sheet, 1, 1, "字段名（英文）");
            SetHeader(sheet, 1, 2, "显示名（中文）");
            SetHeader(sheet, 1, 3, "数据类型");
            SetHeader(sheet, 1, 4, "必填");
            SetHeader(sheet, 1, 5, "默认值");
            SetHeader(sheet, 1, 6, "列名变体（分号分隔）");

            int row = 2;
            foreach (var f in config.Fields)
            {
                sheet.Cells[row, 1].Value = f.FieldName;
                sheet.Cells[row, 2].Value = f.DisplayName;
                sheet.Cells[row, 3].Value = f.DataType.ToString();
                sheet.Cells[row, 4].Value = f.IsRequired ? "是" : "否";
                sheet.Cells[row, 5].Value = f.DefaultValue ?? "";
                sheet.Cells[row, 6].Value = string.Join(";", f.KnownColumnVariants);
                row++;
            }

            // 数据类型下拉验证（覆盖数据行 + 预留空行）
            var dtValidation = sheet.DataValidations.AddListValidation($"C2:C{row + 20}");
            dtValidation.ShowErrorMessage = true;
            dtValidation.ErrorTitle = "无效的数据类型";
            dtValidation.Error = "请从下拉列表中选择数据类型";
            foreach (var name in Enum.GetNames(typeof(FieldDataType)))
                dtValidation.Formula.Values.Add(name);

            // 必填下拉验证
            var reqValidation = sheet.DataValidations.AddListValidation($"D2:D{row + 20}");
            reqValidation.Formula.Values.Add("是");
            reqValidation.Formula.Values.Add("否");

            sheet.Column(1).AutoFit(15, 30);
            sheet.Column(2).AutoFit(15, 30);
            sheet.Column(3).Width = 15;
            sheet.Column(4).Width = 8;
            sheet.Column(5).Width = 15;
            sheet.Column(6).AutoFit(20, 60);
            sheet.View.FreezePanes(2, 1);

            // Sheet 2：配置信息
            var metaSheet = package.Workbook.Worksheets.Add("配置信息");
            metaSheet.Cells[1, 1].Value = "配置名称";
            metaSheet.Cells[1, 1].Style.Font.Bold = true;
            metaSheet.Cells[1, 2].Value = config.ConfigName;
            metaSheet.Cells[2, 1].Value = "表头行数";
            metaSheet.Cells[2, 1].Style.Font.Bold = true;
            metaSheet.Cells[2, 2].Value = config.HeaderRowCount;
            metaSheet.Cells[3, 1].Value = "列名匹配模式";
            metaSheet.Cells[3, 1].Style.Font.Bold = true;
            metaSheet.Cells[3, 2].Value = config.ColumnMatch.ToString();
            metaSheet.Cells[4, 1].Value = "启用值归一化";
            metaSheet.Cells[4, 1].Style.Font.Bold = true;
            metaSheet.Cells[4, 2].Value = config.EnableValueNormalization ? "是" : "否";
            metaSheet.Column(1).Width = 20;
            metaSheet.Column(2).Width = 30;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            package.SaveAs(new FileInfo(outputPath));
        }

        /// <summary>
        /// 确保模板目录存在，若模板文件不存在则生成
        /// </summary>
        public static void EnsureTemplates(string templateDir)
        {
            Directory.CreateDirectory(templateDir);

            string colTemplatePath = Path.Combine(templateDir, "列名训练模板.xlsx");
            if (!File.Exists(colTemplatePath))
                GenerateColumnTrainingTemplate(colTemplatePath);

            string nerTemplatePath = Path.Combine(templateDir, "NER标注模板.xlsx");
            if (!File.Exists(nerTemplatePath))
                GenerateNerTrainingTemplate(nerTemplatePath);

            string configTemplatePath = Path.Combine(templateDir, "字段配置模板.xlsx");
            if (!File.Exists(configTemplatePath))
                GenerateConfigTemplate(configTemplatePath);
        }

        // ── 内部数据 ──────────────────────────────────────────────────────

        private static void SetHeader(ExcelWorksheet sheet, int row, int col, string text)
        {
            var cell = sheet.Cells[row, col];
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
            cell.Style.Font.Color.SetColor(Color.White);
        }

        private static List<ColumnMapping> GetTelemetryColumnMappings()
        {
            return new List<ColumnMapping>
            {
                // Index 序号
                new ColumnMapping("序号", "Index", "序号"),
                new ColumnMapping("No.", "Index", "序号"),
                new ColumnMapping("编号", "Index", "序号"),
                new ColumnMapping("NO", "Index", "序号"),

                // System 所属系统
                new ColumnMapping("所属系统", "System", "所属系统"),
                new ColumnMapping("系统", "System", "所属系统"),
                new ColumnMapping("System", "System", "所属系统"),
                new ColumnMapping("子系统", "System", "所属系统"),

                // APID
                new ColumnMapping("APID值", "APID", "APID值"),
                new ColumnMapping("APID", "APID", "APID值"),
                new ColumnMapping("应用标识", "APID", "APID值"),
                new ColumnMapping("应用过程标识", "APID", "APID值"),

                // StartByte 起始字节
                new ColumnMapping("起始字节", "StartByte", "起始字节"),
                new ColumnMapping("起始字节序号", "StartByte", "起始字节"),
                new ColumnMapping("开始字节", "StartByte", "起始字节"),
                new ColumnMapping("字节偏移", "StartByte", "起始字节"),

                // BitOffset 起始位
                new ColumnMapping("起始位", "BitOffset", "起始位"),
                new ColumnMapping("起始比特", "BitOffset", "起始位"),
                new ColumnMapping("比特偏移", "BitOffset", "起始位"),

                // BitLength 位长度
                new ColumnMapping("位长度", "BitLength", "位长度"),
                new ColumnMapping("字节长度", "BitLength", "位长度"),
                new ColumnMapping("比特数", "BitLength", "位长度"),
                new ColumnMapping("长度", "BitLength", "位长度"),
                new ColumnMapping("数据长度", "BitLength", "位长度"),

                // ChannelName 波道名称
                new ColumnMapping("波道名称", "ChannelName", "波道名称"),
                new ColumnMapping("参数名称", "ChannelName", "波道名称"),
                new ColumnMapping("通道名称", "ChannelName", "波道名称"),
                new ColumnMapping("名称", "ChannelName", "波道名称"),
                new ColumnMapping("遥测参数名称", "ChannelName", "波道名称"),

                // TelemetryCode 遥测代号
                new ColumnMapping("遥测代号", "TelemetryCode", "遥测代号"),
                new ColumnMapping("参数代号", "TelemetryCode", "遥测代号"),
                new ColumnMapping("代号", "TelemetryCode", "遥测代号"),
                new ColumnMapping("标识", "TelemetryCode", "遥测代号"),
                new ColumnMapping("参数标识", "TelemetryCode", "遥测代号"),

                // Endianness 字节端序
                new ColumnMapping("字节端序", "Endianness", "字节端序"),
                new ColumnMapping("端序", "Endianness", "字节端序"),
                new ColumnMapping("大小端", "Endianness", "字节端序"),
                new ColumnMapping("字节序", "Endianness", "字节端序"),

                // FormulaType 公式类型
                new ColumnMapping("公式类型", "FormulaType", "公式类型"),
                new ColumnMapping("转换类型", "FormulaType", "公式类型"),
                new ColumnMapping("类型", "FormulaType", "公式类型"),
                new ColumnMapping("换算方式", "FormulaType", "公式类型"),

                // CoeffA 系数A
                new ColumnMapping("A", "CoeffA", "系数A"),
                new ColumnMapping("系数A", "CoeffA", "系数A"),
                new ColumnMapping("公式系数/A", "CoeffA", "系数A"),
                new ColumnMapping("系数a", "CoeffA", "系数A"),

                // CoeffB 系数B
                new ColumnMapping("B", "CoeffB", "系数B"),
                new ColumnMapping("系数B", "CoeffB", "系数B"),
                new ColumnMapping("公式系数/B", "CoeffB", "系数B"),
                new ColumnMapping("系数b", "CoeffB", "系数B"),

                // Precision 小数位数
                new ColumnMapping("小数位数", "Precision", "小数位数"),
                new ColumnMapping("精度", "Precision", "小数位数"),
                new ColumnMapping("小数位", "Precision", "小数位数"),

                // Unit 量纲
                new ColumnMapping("量纲", "Unit", "量纲"),
                new ColumnMapping("单位", "Unit", "量纲"),
                new ColumnMapping("工程量纲", "Unit", "量纲"),
                new ColumnMapping("物理量纲", "Unit", "量纲"),

                // EnumMap 枚举解译
                new ColumnMapping("枚举解译", "EnumMap", "枚举解译"),
                new ColumnMapping("离散值", "EnumMap", "枚举解译"),
                new ColumnMapping("枚举值", "EnumMap", "枚举解译"),
                new ColumnMapping("状态描述", "EnumMap", "枚举解译"),
                new ColumnMapping("枚举定义", "EnumMap", "枚举解译"),
            };
        }

        private static List<NerSample> GetNerSamples()
        {
            return new List<NerSample>
            {
                new NerSample(
                    "0x1A",
                    "[{\"start\":0,\"end\":3,\"type\":\"HexCode\",\"text\":\"0x1A\"}]",
                    "十六进制值"),
                new NerSample(
                    "3.1415926",
                    "[{\"start\":0,\"end\":8,\"type\":\"Value\",\"text\":\"3.1415926\"}]",
                    "浮点数值"),
                new NerSample(
                    "0.5A±10%",
                    "[{\"start\":0,\"end\":2,\"type\":\"Value\",\"text\":\"0.5\"},{\"start\":3,\"end\":3,\"type\":\"Unit\",\"text\":\"A\"},{\"start\":4,\"end\":7,\"type\":\"Value\",\"text\":\"±10%\"}]",
                    "数值+单位+容差"),
                new NerSample(
                    "A=1.5 B=0.0",
                    "[{\"start\":0,\"end\":4,\"type\":\"Formula\",\"text\":\"A=1.5\"},{\"start\":6,\"end\":10,\"type\":\"Formula\",\"text\":\"B=0.0\"}]",
                    "公式系数"),
                new NerSample(
                    "0=关闭;1=开启;2=待机",
                    "[{\"start\":0,\"end\":18,\"type\":\"Enum\",\"text\":\"0=关闭;1=开启;2=待机\"}]",
                    "枚举映射"),
                new NerSample(
                    "00:正常/01:故障/10:未知",
                    "[{\"start\":0,\"end\":20,\"type\":\"Enum\",\"text\":\"00:正常/01:故障/10:未知\"}]",
                    "枚举映射（冒号分隔）"),
                new NerSample(
                    "28.5 dBm",
                    "[{\"start\":0,\"end\":3,\"type\":\"Value\",\"text\":\"28.5\"},{\"start\":5,\"end\":7,\"type\":\"Unit\",\"text\":\"dBm\"}]",
                    "数值+遥测单位"),
                new NerSample(
                    "1024 字节",
                    "[{\"start\":0,\"end\":3,\"type\":\"Value\",\"text\":\"1024\"},{\"start\":5,\"end\":6,\"type\":\"Unit\",\"text\":\"字节\"}]",
                    "数值+中文单位"),
            };
        }

        private class ColumnMapping
        {
            public string RawColumnName { get; }
            public string CanonicalFieldName { get; }
            public string DisplayName { get; }

            public ColumnMapping(string raw, string canonical, string display)
            {
                RawColumnName = raw;
                CanonicalFieldName = canonical;
                DisplayName = display;
            }
        }

        private class NerSample
        {
            public string Text { get; }
            public string EntityJson { get; }
            public string Note { get; }

            public NerSample(string text, string entityJson, string note)
            {
                Text = text;
                EntityJson = entityJson;
                Note = note;
            }
        }
    }
}
