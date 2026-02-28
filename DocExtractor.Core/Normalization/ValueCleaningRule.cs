using System.Collections.Generic;

namespace DocExtractor.Core.Normalization
{
    /// <summary>
    /// 值清洗规则类型：每种类型对应一类原始值格式的提取逻辑。
    /// </summary>
    public enum CleaningRuleType
    {
        /// <summary>十六进制+描述：0x1-PPU待机模式 → 0x1</summary>
        HexWithDescription,

        /// <summary>数字+括号十六进制：0 （0x0） → 0</summary>
        NumberWithParenHex,

        /// <summary>带单位范围：-0.5A～0.5A → -0.5/0.5</summary>
        RangeWithUnit,

        /// <summary>整数范围：1-255 → 1/255</summary>
        IntegerRange,

        /// <summary>数字+括号注释：643(4.5A) → 643</summary>
        NumberWithParenAnnotation,

        /// <summary>数字+单位后缀：420s → 420</summary>
        NumberWithUnitSuffix,

        /// <summary>值±公差：220V±10V → 210/230</summary>
        ValueWithTolerance,

        /// <summary>十六进制+括号描述：0x2（当前阴极B） → 0x2</summary>
        HexWithParenDescription,

        /// <summary>中文文本映射为零：不变 → 0</summary>
        ChineseTextToZero
    }

    /// <summary>
    /// 单条值清洗规则定义。
    /// </summary>
    public class ValueCleaningRule
    {
        public CleaningRuleType RuleType { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Example { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; }

        public static List<ValueCleaningRule> GetDefaultRules()
        {
            return new List<ValueCleaningRule>
            {
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.HexWithDescription,
                    DisplayName = "十六进制+描述",
                    Description = "提取十六进制值，去除后续中文描述",
                    Example = "0x1-PPU待机模式 → 0x1",
                    IsEnabled = true,
                    Priority = 10
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.NumberWithParenHex,
                    DisplayName = "数字+括号十六进制",
                    Description = "提取前导数字，去除括号中的十六进制注释",
                    Example = "0 （0x0） → 0",
                    IsEnabled = true,
                    Priority = 20
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.RangeWithUnit,
                    DisplayName = "范围+单位",
                    Description = "将带单位的范围转换为斜杠分隔",
                    Example = "-0.5A～0.5A → -0.5/0.5",
                    IsEnabled = true,
                    Priority = 30
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.IntegerRange,
                    DisplayName = "整数范围",
                    Description = "将整数范围转换为斜杠分隔",
                    Example = "1-255 → 1/255",
                    IsEnabled = true,
                    Priority = 40
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.NumberWithParenAnnotation,
                    DisplayName = "数字+括号注释",
                    Description = "提取前导数字，去除括号中的注释",
                    Example = "643(4.5A) → 643",
                    IsEnabled = true,
                    Priority = 50
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.NumberWithUnitSuffix,
                    DisplayName = "数字+单位后缀",
                    Description = "提取数字，去除尾部单位后缀",
                    Example = "420s → 420",
                    IsEnabled = true,
                    Priority = 60
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.ValueWithTolerance,
                    DisplayName = "值±公差",
                    Description = "将值±公差转换为范围",
                    Example = "220V±10V → 210/230",
                    IsEnabled = true,
                    Priority = 25
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.HexWithParenDescription,
                    DisplayName = "十六进制+括号描述",
                    Description = "提取十六进制值，去除括号中的中文描述",
                    Example = "0x2（当前阴极B） → 0x2",
                    IsEnabled = true,
                    Priority = 15
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.ChineseTextToZero,
                    DisplayName = "中文文本归零",
                    Description = "将「不变」等中文文本映射为 0",
                    Example = "不变 → 0",
                    IsEnabled = true,
                    Priority = 70
                }
            };
        }
    }
}
