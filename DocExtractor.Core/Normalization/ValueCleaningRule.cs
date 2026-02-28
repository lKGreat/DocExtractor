using System.Collections.Generic;

namespace DocExtractor.Core.Normalization
{
    /// <summary>
    /// 值清洗规则类型。
    /// 分为两类：结构变换（改变值的结构）和核心提取（从注释中提取有意义的值）。
    /// </summary>
    public enum CleaningRuleType
    {
        // ── Legacy types (kept for backward compat with serialized configs) ──

        /// <summary>[旧] 十六进制+描述 → 已由 CoreValueExtraction 统一处理</summary>
        HexWithDescription,
        /// <summary>[旧] 数字+括号十六进制 → 已由 CoreValueExtraction 统一处理</summary>
        NumberWithParenHex,
        /// <summary>带单位范围：-0.5A～0.5A → -0.5/0.5</summary>
        RangeWithUnit,
        /// <summary>整数范围：1-255 → 1/255</summary>
        IntegerRange,
        /// <summary>[旧] 数字+括号注释 → 已由 CoreValueExtraction 统一处理</summary>
        NumberWithParenAnnotation,
        /// <summary>[旧] 数字+单位后缀 → 已由 CoreValueExtraction 统一处理</summary>
        NumberWithUnitSuffix,
        /// <summary>值±公差：220V±10V → 210/230</summary>
        ValueWithTolerance,
        /// <summary>[旧] 十六进制+括号描述 → 已由 CoreValueExtraction 统一处理</summary>
        HexWithParenDescription,
        /// <summary>[旧] 中文文本映射为零 → 已由 CoreValueExtraction 统一处理</summary>
        ChineseTextToZero,

        // ── New universal type ──────────────────────────────────────────────

        /// <summary>
        /// 通用核心值提取：自动从任意注释/单位/描述中提取有意义的数值或十六进制值。
        /// 替代所有旧的逐一提取规则。
        /// </summary>
        CoreValueExtraction
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

        /// <summary>
        /// 返回默认规则集：3 条结构变换 + 1 条通用提取。
        /// </summary>
        public static List<ValueCleaningRule> GetDefaultRules()
        {
            return new List<ValueCleaningRule>
            {
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.ValueWithTolerance,
                    DisplayName = "值±公差 → 范围",
                    Description = "将值±公差转换为范围",
                    Example = "220V±10V → 210/230",
                    IsEnabled = true,
                    Priority = 10
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.RangeWithUnit,
                    DisplayName = "范围+单位 → 范围",
                    Description = "将带单位的范围转换为斜杠分隔",
                    Example = "-0.5A～0.5A → -0.5/0.5",
                    IsEnabled = true,
                    Priority = 20
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.IntegerRange,
                    DisplayName = "整数范围 → 范围",
                    Description = "将整数范围转换为斜杠分隔",
                    Example = "1-255 → 1/255",
                    IsEnabled = true,
                    Priority = 30
                },
                new ValueCleaningRule
                {
                    RuleType = CleaningRuleType.CoreValueExtraction,
                    DisplayName = "通用值提取",
                    Description = "自动提取核心数值/十六进制，去除单位、描述、注释",
                    Example = "0x1-PPU待机模式→0x1, 100,单位秒→100, 复归为1→1",
                    IsEnabled = true,
                    Priority = 40
                }
            };
        }
    }
}
