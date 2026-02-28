using System;

namespace DocExtractor.Core.Exceptions
{
    /// <summary>
    /// 拆分异常：拆分规则冲突、循环拆分、非法分隔规则等。
    /// </summary>
    public class SplitException : DocExtractorException
    {
        public string? RuleName { get; }
        public string? TriggerColumn { get; }

        public SplitException(
            string message,
            string? ruleName = null,
            string? triggerColumn = null,
            Exception? innerException = null)
            : base(message, "Split", "SPLIT_ERROR", innerException)
        {
            RuleName = ruleName;
            TriggerColumn = triggerColumn;
        }
    }
}
