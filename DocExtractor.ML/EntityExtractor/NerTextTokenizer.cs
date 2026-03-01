using System;
using System.Collections.Generic;
using System.Linq;

namespace DocExtractor.ML.EntityExtractor
{
    /// <summary>
    /// NER 文本字符级 token 化工具：
    /// 1) 保持每个原始字符对应一个 token（与 BIO 标签长度一致）
    /// 2) 对空白字符做显式占位，避免与分隔空格冲突
    /// </summary>
    internal static class NerTextTokenizer
    {
        public static string ToSpaceSeparatedCharTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return string.Join(" ", ToCharTokens(text));
        }

        public static string[] ToCharTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

            var tokens = new List<string>(text.Length);
            foreach (char c in text)
                tokens.Add(MapChar(c));
            return tokens.ToArray();
        }

        private static string MapChar(char c)
        {
            if (c == ' ') return "<SP>";
            if (c == '\t') return "<TAB>";
            if (c == '\r') return "<CR>";
            if (c == '\n') return "<LF>";
            if (char.IsWhiteSpace(c)) return "<WS>";
            return c.ToString();
        }
    }
}
