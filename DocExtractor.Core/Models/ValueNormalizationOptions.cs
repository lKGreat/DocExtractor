using System.Collections.Generic;
using DocExtractor.Core.Normalization;

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

        /// <summary>是否启用值清洗（去除单位/注释/描述等）</summary>
        public bool EnableValueCleaning { get; set; }

        /// <summary>
        /// 值清洗规则列表。为空时使用默认规则集。
        /// </summary>
        public List<ValueCleaningRule> CleaningRules { get; set; } = new List<ValueCleaningRule>();

        /// <summary>
        /// 获取生效的清洗规则：若自定义列表非空则使用它，否则使用默认规则集。
        /// </summary>
        public List<ValueCleaningRule> GetEffectiveCleaningRules()
        {
            return CleaningRules != null && CleaningRules.Count > 0
                ? CleaningRules
                : ValueCleaningRule.GetDefaultRules();
        }

        /// <summary>是否启用时间轴展开（自动检测多步序列/跳变/阈值模式并拆分行）</summary>
        public bool EnableTimeAxisExpand { get; set; }

        /// <summary>时间轴展开的触发字段名（空则自动扫描所有字段）</summary>
        public string TimeAxisTriggerField { get; set; } = string.Empty;

        /// <summary>时间轴展开的默认公差</summary>
        public double TimeAxisDefaultTolerance { get; set; } = 0;

        /// <summary>无法解析时间时的默认时间值</summary>
        public double TimeAxisDefaultTime { get; set; } = 0;
    }
}
