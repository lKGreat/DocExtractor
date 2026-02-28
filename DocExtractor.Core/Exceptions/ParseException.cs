using System;

namespace DocExtractor.Core.Exceptions
{
    /// <summary>
    /// 文档解析异常：文档损坏、权限不足、加密或解析器运行失败。
    /// </summary>
    public class ParseException : DocExtractorException
    {
        public string? FilePath { get; }
        public string? ParserName { get; }

        public ParseException(
            string message,
            string? filePath = null,
            string? parserName = null,
            Exception? innerException = null)
            : base(message, "Parse", "PARSE_ERROR", innerException)
        {
            FilePath = filePath;
            ParserName = parserName;
        }
    }
}
