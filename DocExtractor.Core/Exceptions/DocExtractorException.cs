using System;

namespace DocExtractor.Core.Exceptions
{
    /// <summary>
    /// DocExtractor 领域异常基类。
    /// </summary>
    public class DocExtractorException : Exception
    {
        /// <summary>错误阶段（解析/列映射/拆分/模型等）</summary>
        public string Stage { get; }

        /// <summary>错误编码（便于 UI 与日志检索）</summary>
        public string ErrorCode { get; }

        public DocExtractorException(
            string message,
            string stage,
            string errorCode,
            Exception? innerException = null)
            : base(message, innerException)
        {
            Stage = stage;
            ErrorCode = errorCode;
        }
    }
}
