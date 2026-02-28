using System.Collections.Generic;

namespace DocExtractor.Core.Models
{
    /// <summary>
    /// 值归一化全局选项。
    /// </summary>
    public class ValueNormalizationOptions
    {
        /// <summary>小数分隔符（输出）</summary>
        public string DecimalSeparator { get; set; } = ".";

        /// <summary>千分位分隔符（输出，空字符串表示不使用）</summary>
        public string ThousandsSeparator { get; set; } = string.Empty;

        /// <summary>默认小数位数（null 表示去除尾随 0）</summary>
        public int? DefaultDecimalPlaces { get; set; }

        /// <summary>布尔 true 同义词映射（key 为输入值）</summary>
        public Dictionary<string, string> BooleanTrueAliases { get; set; } =
            new Dictionary<string, string>();

        /// <summary>布尔 false 同义词映射（key 为输入值）</summary>
        public Dictionary<string, string> BooleanFalseAliases { get; set; } =
            new Dictionary<string, string>();

        /// <summary>十六进制前缀</summary>
        public string HexPrefix { get; set; } = "0x";

        /// <summary>十六进制是否大写</summary>
        public bool HexUpperCase { get; set; } = true;
    }
}
