using System;
using System.Collections.Generic;
using System.IO;
using DocExtractor.Core.Models;
using OfficeOpenXml;

namespace DocExtractor.Data.Export
{
    /// <summary>
    /// 从 Excel 模板导入字段配置（ExtractionConfig）
    /// </summary>
    public static class ConfigImporter
    {
        /// <summary>
        /// 从 Excel 文件导入字段配置。
        /// 支持两个 Sheet：「字段配置」和「配置信息」（可选）。
        /// </summary>
        public static ExtractionConfig ImportFromExcel(string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage(new FileInfo(filePath));
            var config = new ExtractionConfig();

            // 读取配置元信息（Sheet 2，可选）
            var metaSheet = FindSheet(package, "配置信息");
            if (metaSheet != null)
            {
                ReadMetadata(metaSheet, config);
            }
            else
            {
                // 从文件名推导配置名
                config.ConfigName = Path.GetFileNameWithoutExtension(filePath);
            }

            // 读取字段定义（Sheet 1）
            var fieldsSheet = FindSheet(package, "字段配置");
            if (fieldsSheet == null)
            {
                // 回退到第一个 Sheet
                if (package.Workbook.Worksheets.Count == 0)
                    throw new InvalidOperationException("Excel 文件中没有工作表");
                fieldsSheet = package.Workbook.Worksheets[0];
            }

            ReadFields(fieldsSheet, config);

            if (config.Fields.Count == 0)
                throw new InvalidOperationException("未找到任何有效的字段定义，请检查 Excel 内容");

            if (string.IsNullOrWhiteSpace(config.ConfigName))
                config.ConfigName = Path.GetFileNameWithoutExtension(filePath);

            return config;
        }

        private static ExcelWorksheet? FindSheet(ExcelPackage package, string name)
        {
            foreach (var ws in package.Workbook.Worksheets)
            {
                if (ws.Name.Trim() == name)
                    return ws;
            }
            return null;
        }

        private static void ReadMetadata(ExcelWorksheet sheet, ExtractionConfig config)
        {
            for (int row = 1; row <= Math.Min(sheet.Dimension?.End.Row ?? 0, 10); row++)
            {
                string key = sheet.Cells[row, 1].Text.Trim();
                string val = sheet.Cells[row, 2].Text.Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) continue;

                if (key == "配置名称")
                    config.ConfigName = val;
                else if (key == "表头行数" && int.TryParse(val, out int hrc))
                    config.HeaderRowCount = hrc;
                else if (key == "列名匹配模式" && Enum.TryParse<ColumnMatchMode>(val, out var cm))
                    config.ColumnMatch = cm;
                else if (key == "启用值归一化")
                    config.EnableValueNormalization = IsTrue(val);
            }
        }

        private static void ReadFields(ExcelWorksheet sheet, ExtractionConfig config)
        {
            if (sheet.Dimension == null) return;

            int endRow = sheet.Dimension.End.Row;

            // 从第 2 行开始（第 1 行为表头）
            for (int row = 2; row <= endRow; row++)
            {
                string fieldName = sheet.Cells[row, 1].Text.Trim();
                if (string.IsNullOrEmpty(fieldName)) continue;

                string displayName = sheet.Cells[row, 2].Text.Trim();
                if (string.IsNullOrEmpty(displayName))
                    displayName = fieldName;

                var field = new FieldDefinition
                {
                    FieldName = fieldName,
                    DisplayName = displayName,
                    IsRequired = false
                };

                // C: 数据类型
                string dataTypeStr = sheet.Cells[row, 3].Text.Trim();
                if (!string.IsNullOrEmpty(dataTypeStr) && Enum.TryParse<FieldDataType>(dataTypeStr, true, out var dt))
                    field.DataType = dt;

                // D: 必填
                string requiredStr = sheet.Cells[row, 4].Text.Trim();
                field.IsRequired = IsTrue(requiredStr);

                // E: 默认值
                string defaultVal = sheet.Cells[row, 5].Text.Trim();
                if (!string.IsNullOrEmpty(defaultVal))
                    field.DefaultValue = defaultVal;

                // F: 列名变体（兼容多种分隔符）
                string variantsStr = sheet.Cells[row, 6].Text.Trim();
                if (!string.IsNullOrEmpty(variantsStr))
                {
                    var parts = variantsStr.Split(new[] { ';', '；', ',', '、' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        string v = part.Trim();
                        if (!string.IsNullOrEmpty(v))
                            field.KnownColumnVariants.Add(v);
                    }
                }

                config.Fields.Add(field);
            }
        }

        private static bool IsTrue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string v = value.Trim().ToLowerInvariant();
            return v == "true" || v == "yes" || v == "y" || v == "1" || v == "是";
        }
    }
}
