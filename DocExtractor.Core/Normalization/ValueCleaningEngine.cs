using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocExtractor.Core.Normalization
{
    /// <summary>
    /// 值清洗引擎：两阶段清洗。
    /// Phase 1 — 结构变换：范围、公差等模式改变值的结构。
    /// Phase 2 — 通用提取：从任意注释/单位/描述中提取核心数值或十六进制值。
    /// </summary>
    public static class ValueCleaningEngine
    {
        // ── Phase 1: Structural transforms ───────────────────────────────────

        // 值±公差：220V±10V, 24.5V±0.5V
        private static readonly Regex ValueTolerancePattern = new Regex(
            @"^\s*([+-]?\d+\.?\d*)\s*[a-zA-Z]*\s*[±]\s*(\d+\.?\d*)\s*[a-zA-Z]*\s*$",
            RegexOptions.Compiled);

        // 带单位范围：-0.5A～0.5A, 0～0.9Mpa
        private static readonly Regex RangeUnitPattern = new Regex(
            @"^\s*([+-]?\d[\d.]*)\s*[a-zA-Z]*\s*[～~]\s*([+-]?\d[\d.]*)\s*[a-zA-Z]*\s*$",
            RegexOptions.Compiled);

        // 整数范围：1-255
        private static readonly Regex IntRangePattern = new Regex(
            @"^\s*(\d+)\s*-\s*(\d+)\s*$",
            RegexOptions.Compiled);

        // ── Phase 2: Universal core value extraction ─────────────────────────

        // Leading hex: 0x1-PPU, 0x55..., 0x2（...）
        private static readonly Regex LeadingHexPattern = new Regex(
            @"^\s*(0x[0-9a-fA-F]+)",
            RegexOptions.Compiled);

        // Leading number: 0 （0x0）, 643(4.5A), 420s, 100,单位秒, +1
        private static readonly Regex LeadingNumberPattern = new Regex(
            @"^\s*([+-]?\d+\.?\d*)",
            RegexOptions.Compiled);

        // Hex anywhere: 使能打开0x55,(85)
        private static readonly Regex AnyHexPattern = new Regex(
            @"(0x[0-9a-fA-F]+)",
            RegexOptions.Compiled);

        // Number anywhere: 复归为1
        private static readonly Regex AnyNumberPattern = new Regex(
            @"([+-]?\d+\.?\d*)",
            RegexOptions.Compiled);

        // Chinese text → zero: 不变, 无变化, etc.
        private static readonly Regex ChineseZeroPattern = new Regex(
            @"^\s*(不变|无变化|保持不变|维持)\s*$",
            RegexOptions.Compiled);

        /// <summary>
        /// 按优先级依次尝试清洗规则，第一个匹配即返回清洗结果。
        /// 若无规则匹配则返回原始值。
        /// </summary>
        public static string Clean(string rawValue, IReadOnlyList<ValueCleaningRule> rules)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return rawValue;

            string input = rawValue.Trim();
            if (input.Length == 0)
                return rawValue;

            var ordered = rules.Where(r => r.IsEnabled).OrderBy(r => r.Priority);

            foreach (var rule in ordered)
            {
                string result = TryApply(rule.RuleType, input);
                if (result != null)
                    return result;
            }

            return rawValue;
        }

        private static string TryApply(CleaningRuleType ruleType, string input)
        {
            switch (ruleType)
            {
                // Structural transforms
                case CleaningRuleType.ValueWithTolerance:
                    return TryValueWithTolerance(input);
                case CleaningRuleType.RangeWithUnit:
                    return TryRangeWithUnit(input);
                case CleaningRuleType.IntegerRange:
                    return TryIntegerRange(input);

                // Universal extraction (new)
                case CleaningRuleType.CoreValueExtraction:
                    return TryCoreValueExtraction(input);

                // Legacy types → delegate to universal extraction for backward compat
                case CleaningRuleType.HexWithDescription:
                case CleaningRuleType.NumberWithParenHex:
                case CleaningRuleType.NumberWithParenAnnotation:
                case CleaningRuleType.NumberWithUnitSuffix:
                case CleaningRuleType.HexWithParenDescription:
                case CleaningRuleType.ChineseTextToZero:
                    return TryCoreValueExtraction(input);

                default:
                    return null;
            }
        }

        // ── Structural transforms ───────────────────────────────────────────

        private static string TryValueWithTolerance(string input)
        {
            var m = ValueTolerancePattern.Match(input);
            if (!m.Success) return null;

            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double center) ||
                !double.TryParse(m.Groups[2].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double tol))
                return null;

            return FormatNum(center - tol) + "/" + FormatNum(center + tol);
        }

        private static string TryRangeWithUnit(string input)
        {
            var m = RangeUnitPattern.Match(input);
            return m.Success ? m.Groups[1].Value + "/" + m.Groups[2].Value : null;
        }

        private static string TryIntegerRange(string input)
        {
            var m = IntRangePattern.Match(input);
            if (!m.Success) return null;

            if (int.TryParse(m.Groups[1].Value, out int left) &&
                int.TryParse(m.Groups[2].Value, out int right) &&
                left < right)
            {
                return m.Groups[1].Value + "/" + m.Groups[2].Value;
            }
            return null;
        }

        // ── Universal core value extraction ─────────────────────────────────

        /// <summary>
        /// 通用核心值提取：从任意带注释/单位/描述的字符串中提取有意义的值。
        /// 优先级：开头hex > 开头number > 任意hex > 任意number > 中文文本映射。
        /// </summary>
        private static string TryCoreValueExtraction(string input)
        {
            // Skip if the value is already a pure number or hex (no cleaning needed)
            if (IsPureValue(input))
                return null;

            // 1. Leading hex: 0x1-PPU待机模式, 0x2（当前阴极B）
            var m = LeadingHexPattern.Match(input);
            if (m.Success && input.Length > m.Groups[1].Length)
                return m.Groups[1].Value;

            // 2. Leading number: 0 （0x0）, 643(4.5A), 420s, 100,单位秒
            m = LeadingNumberPattern.Match(input);
            if (m.Success && input.Length > m.Groups[1].Length)
                return m.Groups[1].Value;

            // 3. Hex anywhere: 使能打开0x55,(85)
            m = AnyHexPattern.Match(input);
            if (m.Success)
                return m.Groups[1].Value;

            // 4. Number anywhere: 复归为1
            m = AnyNumberPattern.Match(input);
            if (m.Success)
                return m.Groups[1].Value;

            // 5. Chinese text → zero: 不变
            m = ChineseZeroPattern.Match(input);
            if (m.Success)
                return "0";

            return null;
        }

        /// <summary>
        /// 判断是否已经是纯值（不需要清洗）。
        /// 纯数字、纯十六进制、带符号数字、已有斜杠范围格式。
        /// </summary>
        private static bool IsPureValue(string input)
        {
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsDigit(c) || c == '.' || c == '+' || c == '-' || c == '/')
                    continue;
                if (c == 'x' || c == 'X')
                    continue;
                if (c >= 'a' && c <= 'f') continue;
                if (c >= 'A' && c <= 'F') continue;
                return false;
            }
            return true;
        }

        private static string FormatNum(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
