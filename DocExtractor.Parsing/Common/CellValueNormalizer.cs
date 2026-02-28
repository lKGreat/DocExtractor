using System.Text.RegularExpressions;

namespace DocExtractor.Parsing.Common
{
    /// <summary>
    /// 单元格值基础清洗工具（在进入 ML 管道前的预处理）
    /// </summary>
    public static class CellValueNormalizer
    {
        // 匹配全角空格、零宽空格、BOM 等不可见字符
        private static readonly Regex _invisibleChars =
            new Regex(@"[\u00A0\u200B\u200C\u200D\uFEFF\u3000]", RegexOptions.Compiled);

        // 全角转半角映射范围：！(0xFF01) ~ ～(0xFF5E)
        public static string Normalize(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            // 1. 移除 BOM 和不可见字符
            string s = _invisibleChars.Replace(raw, " ");

            // 2. 全角→半角（字母、数字、常用符号）
            s = FullWidthToHalf(s);

            // 3. 合并连续空白为单个空格
            s = Regex.Replace(s, @"\s+", " ");

            // 4. 去除首尾空白
            return s.Trim();
        }

        private static string FullWidthToHalf(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                // 全角字符范围 ！～（排除中文标点）
                if (c >= '\uFF01' && c <= '\uFF5E')
                    sb.Append((char)(c - 0xFEE0));
                else if (c == '\u3000') // 全角空格
                    sb.Append(' ');
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>去除单元格中的换行，替换为分号（用于多值检测）</summary>
        public static string CollapseNewlines(string s) =>
            Regex.Replace(s, @"[\r\n]+", ";").Trim(';');
    }
}
