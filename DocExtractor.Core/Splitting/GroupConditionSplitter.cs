using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Splitting
{
    /// <summary>
    /// 分组条件拆分：不拆分单条记录，而是在 ExtractionPipeline 层面
    /// 按某个字段值对全体记录进行分组。此接口的 Split 用于标记记录所属分组。
    /// </summary>
    public class GroupConditionSplitter : IRecordSplitter
    {
        public SplitType SupportedType => SplitType.GroupConditionSplit;

        public IEnumerable<ExtractedRecord> Split(ExtractedRecord record, SplitRule rule)
        {
            // 分组拆分的核心逻辑在 Pipeline 层（GroupRecordsByField），
            // 此处只做字段值清理和标记
            string groupColumn = rule.GroupByColumn;
            if (!string.IsNullOrWhiteSpace(groupColumn) &&
                record.Fields.TryGetValue(groupColumn, out var groupValue))
            {
                // 在记录上打分组标记（用于 Pipeline 后续聚合）
                record.Fields["__GroupKey__"] = groupValue?.Trim() ?? string.Empty;
            }
            yield return record;
        }

        /// <summary>
        /// 对记录集按分组字段分组，返回每组的记录子集
        /// </summary>
        public static Dictionary<string, List<ExtractedRecord>> GroupRecords(
            IEnumerable<ExtractedRecord> records,
            string groupByField)
        {
            return records
                .GroupBy(r => r.Fields.TryGetValue(groupByField, out var v) ? v : string.Empty)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}
