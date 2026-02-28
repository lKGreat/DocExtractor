using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Splitting
{
    /// <summary>
    /// 子表格展开：检测记录中包含的嵌套子表标识，展开为独立记录
    /// 典型场景：枚举解译列中包含"值|描述"形式的多行内容
    /// </summary>
    public class SubTableExpander : IRecordSplitter
    {
        public SplitType SupportedType => SplitType.SubTableExpand;

        // 子表行分隔符（单元格内多行，解析后以此连接）
        private static readonly string[] _rowSeparators = { "\n", "\\n", "↵" };
        // 子表列分隔符
        private static readonly string[] _colSeparators = { "|", "：", ":" };

        public IEnumerable<ExtractedRecord> Split(ExtractedRecord record, SplitRule rule)
        {
            string fieldName = rule.TriggerColumn;
            if (!record.Fields.TryGetValue(fieldName, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                yield return record;
                yield break;
            }

            // 检测是否为多行子表（包含行分隔符）
            string[]? rows = null;
            foreach (var sep in _rowSeparators)
            {
                var parts = value.Split(new[] { sep }, System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
                if (parts.Length > 1) { rows = parts; break; }
            }

            if (rows == null || rows.Length <= 1)
            {
                yield return record;
                yield break;
            }

            // 展开每行为独立记录
            for (int i = 0; i < rows.Length; i++)
            {
                var newRecord = CloneRecord(record, rule.InheritParentFields);
                newRecord.Id = record.Id + $"_sub{i}";

                // 尝试解析子行的键值对（如 "0x01:推力模式"）
                string subRowText = rows[i];
                bool parsed = false;
                foreach (var colSep in _colSeparators)
                {
                    var cols = subRowText.Split(new[] { colSep }, 2, System.StringSplitOptions.None);
                    if (cols.Length == 2)
                    {
                        newRecord.Fields[fieldName + "_Key"] = cols[0].Trim();
                        newRecord.Fields[fieldName + "_Value"] = cols[1].Trim();
                        newRecord.Fields[fieldName] = subRowText;
                        parsed = true;
                        break;
                    }
                }

                if (!parsed)
                    newRecord.Fields[fieldName] = subRowText;

                newRecord.Warnings.Add($"子表展开: 字段[{fieldName}]第{i + 1}/{rows.Length}行");
                yield return newRecord;
            }
        }

        private static ExtractedRecord CloneRecord(ExtractedRecord src, bool inherit)
        {
            return new ExtractedRecord
            {
                Id = src.Id,
                SourceFile = src.SourceFile,
                SourceTableIndex = src.SourceTableIndex,
                SourceRowIndex = src.SourceRowIndex,
                Fields = inherit
                    ? new Dictionary<string, string>(src.Fields)
                    : new Dictionary<string, string>(),
                RawValues = new Dictionary<string, string>(src.RawValues),
                IsComplete = src.IsComplete,
                Warnings = new List<string>(src.Warnings)
            };
        }
    }
}
