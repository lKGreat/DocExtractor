using System.Collections.Generic;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Interfaces
{
    /// <summary>
    /// 记录拆分接口：将一条记录按规则拆分为多条
    /// </summary>
    public interface IRecordSplitter
    {
        /// <summary>该实现处理的拆分类型</summary>
        SplitType SupportedType { get; }

        /// <summary>
        /// 对记录集应用拆分规则。
        /// 输入一条记录，可能输出多条。
        /// </summary>
        IEnumerable<ExtractedRecord> Split(ExtractedRecord record, SplitRule rule);
    }
}
