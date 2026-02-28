using System.Collections.Generic;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Splitting
{
    /// <summary>
    /// 合并单元格展开：将主格值复制到每个子行（已由解析器完成，此类作为后处理补充）
    /// 场景：解析后记录集中，某字段为空但父行同字段有值（跨表合并）
    /// </summary>
    public class MergedCellExpander : IRecordSplitter
    {
        public SplitType SupportedType => SplitType.MergedCellExpand;

        public IEnumerable<ExtractedRecord> Split(ExtractedRecord record, SplitRule rule)
        {
            // 合并单元格展开在解析层（RawTableBuilder）已处理
            // 此处仅做透传，真正的展开逻辑在 ExtractionPipeline 的 RawTable 处理阶段
            yield return record;
        }
    }
}
