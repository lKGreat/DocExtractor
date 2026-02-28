using System.Collections.Generic;
using DocExtractor.ML.ColumnClassifier;

namespace DocExtractor.ML.Training
{
    /// <summary>
    /// 列名训练数据增强：生成常见变体提升泛化能力
    /// </summary>
    public static class ColumnDataAugmenter
    {
        // 常见可去除后缀
        private static readonly string[] TrimSuffixes = { "列", "字段", "栏", "项" };

        // 全角→半角映射
        private static readonly (string Full, string Half)[] BracketPairs =
        {
            ("\uff08", "("), ("\uff09", ")"),
            ("\u3010", "["), ("\u3011", "]"),
            ("\u300a", "<"), ("\u300b", ">")
        };

        /// <summary>
        /// 对原始训练数据生成增强样本，返回原始 + 增强的合并列表
        /// </summary>
        public static List<ColumnInput> Augment(IReadOnlyList<ColumnInput> originals)
        {
            var result = new List<ColumnInput>(originals.Count * 3);

            foreach (var item in originals)
            {
                // 保留原始
                result.Add(item);

                string text = item.ColumnText;
                if (string.IsNullOrWhiteSpace(text)) continue;

                // 1. 去空格变体
                string noSpace = text.Replace(" ", "").Replace("\u3000", "");
                if (noSpace != text)
                    result.Add(new ColumnInput { ColumnText = noSpace, Label = item.Label });

                // 2. 全半角括号互换
                string converted = text;
                foreach (var (full, half) in BracketPairs)
                {
                    if (converted.Contains(full))
                        converted = converted.Replace(full, half);
                    else if (converted.Contains(half))
                        converted = converted.Replace(half, full);
                }
                if (converted != text)
                    result.Add(new ColumnInput { ColumnText = converted, Label = item.Label });

                // 3. 去除常见后缀
                foreach (var suffix in TrimSuffixes)
                {
                    if (text.Length > suffix.Length && text.EndsWith(suffix))
                    {
                        string trimmed = text.Substring(0, text.Length - suffix.Length);
                        result.Add(new ColumnInput { ColumnText = trimmed, Label = item.Label });
                        break; // 只去一个后缀
                    }
                }
            }

            return result;
        }
    }
}
