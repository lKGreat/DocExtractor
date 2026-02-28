using System;
using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Splitting
{
    /// <summary>
    /// 多值字段拆分：一个单元格中有多个值（如 "0x01/0x02"、"A通道;B通道"）
    /// 拆分为多条独立记录，其余字段继承原记录
    /// </summary>
    public class MultiValueSplitter : IRecordSplitter
    {
        public SplitType SupportedType => SplitType.MultiValueSplit;

        public IEnumerable<ExtractedRecord> Split(ExtractedRecord record, SplitRule rule)
        {
            string fieldName = rule.TriggerColumn;
            if (!record.Fields.TryGetValue(fieldName, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                yield return record;
                yield break;
            }

            // 尝试各分隔符，取第一个能拆分出多个非空值的
            string[]? parts = null;
            string? usedDelimiter = null;

            foreach (var delimiter in rule.Delimiters)
            {
                var split = value.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();

                if (split.Length > 1)
                {
                    parts = split;
                    usedDelimiter = delimiter;
                    break;
                }
            }

            if (parts == null || parts.Length <= 1)
            {
                // 无法拆分，原样返回
                yield return record;
                yield break;
            }

            // 生成多条记录
            for (int i = 0; i < parts.Length; i++)
            {
                var newRecord = CloneRecord(record);
                newRecord.Id = record.Id + $"_split{i}";
                newRecord.Fields[fieldName] = parts[i];
                newRecord.Warnings.Add($"多值拆分: 字段[{fieldName}]使用分隔符\"{usedDelimiter}\"拆分第{i + 1}/{parts.Length}条");
                yield return newRecord;
            }
        }

        private static ExtractedRecord CloneRecord(ExtractedRecord src)
        {
            return new ExtractedRecord
            {
                Id = src.Id,
                SourceFile = src.SourceFile,
                SourceTableIndex = src.SourceTableIndex,
                SourceRowIndex = src.SourceRowIndex,
                Fields = new Dictionary<string, string>(src.Fields),
                RawValues = new Dictionary<string, string>(src.RawValues),
                IsComplete = src.IsComplete,
                Warnings = new List<string>(src.Warnings)
            };
        }
    }
}
