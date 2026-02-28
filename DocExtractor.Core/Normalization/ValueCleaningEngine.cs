using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocExtractor.Core.Normalization
{
    /// <summary>
    /// 值清洗引擎：按优先级应用清洗规则，第一个匹配的规则生效。
    /// </summary>
    public static class ValueCleaningEngine
    {
        // ── HexWithDescription ──────────────────────────────────────────────
        // 0x1-PPU待机模式, 0x1：当前阴极A, 0x1定时点火, 0x55，使能, 0xAA为不使能, 0x00:正常
        private static readonly Regex HexDescPattern = new Regex(
            @"^\s*(0x[0-9a-fA-F]+)\s*[-\-：:，,]?\s*[\u4e00-\u9fff\w]",
            RegexOptions.Compiled);

        // ── NumberWithParenHex ──────────────────────────────────────────────
        // 0 （0x0）, 3 （0x3）, 5(0x5), 10(0xA)
        private static readonly Regex NumParenHexPattern = new Regex(
            @"^\s*(\d+)\s*[（(\s]\s*0x[0-9a-fA-F]+\s*[）)]\s*$",
            RegexOptions.Compiled);

        // ── RangeWithUnit ───────────────────────────────────────────────────
        // -0.5A～0.5A, 0～0.9Mpa, -1.0V～0.5V, -2.0V～1.0V
        private static readonly Regex RangeUnitPattern = new Regex(
            @"^\s*([+-]?\d[\d.]*)\s*[a-zA-Z]*\s*[～~]\s*([+-]?\d[\d.]*)\s*[a-zA-Z]*\s*$",
            RegexOptions.Compiled);

        // ── IntegerRange ────────────────────────────────────────────────────
        // 1-255 (both sides are non-negative integers, no hex prefix)
        private static readonly Regex IntRangePattern = new Regex(
            @"^\s*(\d+)\s*-\s*(\d+)\s*$",
            RegexOptions.Compiled);

        // ── NumberWithParenAnnotation ───────────────────────────────────────
        // 643(4.5A), 385（240V）, 250（对应220V）, 1（min）
        private static readonly Regex NumParenAnnotPattern = new Regex(
            @"^\s*(\d+)\s*[（(].*[）)]\s*$",
            RegexOptions.Compiled);

        // ── NumberWithUnitSuffix ────────────────────────────────────────────
        // 420s, 10s, 235s, 0A, 0V
        private static readonly Regex NumUnitSuffixPattern = new Regex(
            @"^\s*(\d+)\s*[a-zA-Z]+\s*$",
            RegexOptions.Compiled);

        // ── ValueWithTolerance ───────────────────────────────────────────────
        // 220V±10V, 24.5V±0.5V, 1.5A±0.1A
        private static readonly Regex ValueTolerancePattern = new Regex(
            @"^\s*([+-]?\d+\.?\d*)\s*[a-zA-Z]*\s*[±]\s*(\d+\.?\d*)\s*[a-zA-Z]*\s*$",
            RegexOptions.Compiled);

        // ── HexWithParenDescription ──────────────────────────────────────────
        // 0x2（当前阴极B）, 0x01(待机模式)
        private static readonly Regex HexParenDescPattern = new Regex(
            @"^\s*(0x[0-9a-fA-F]+)\s*[（(][\u4e00-\u9fff\w].*[）)]\s*$",
            RegexOptions.Compiled);

        // ── ChineseTextToZero ────────────────────────────────────────────────
        // 不变
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
                case CleaningRuleType.HexWithDescription:
                    return TryHexWithDescription(input);
                case CleaningRuleType.NumberWithParenHex:
                    return TryNumberWithParenHex(input);
                case CleaningRuleType.RangeWithUnit:
                    return TryRangeWithUnit(input);
                case CleaningRuleType.IntegerRange:
                    return TryIntegerRange(input);
                case CleaningRuleType.NumberWithParenAnnotation:
                    return TryNumberWithParenAnnotation(input);
                case CleaningRuleType.NumberWithUnitSuffix:
                    return TryNumberWithUnitSuffix(input);
                case CleaningRuleType.ValueWithTolerance:
                    return TryValueWithTolerance(input);
                case CleaningRuleType.HexWithParenDescription:
                    return TryHexWithParenDescription(input);
                case CleaningRuleType.ChineseTextToZero:
                    return TryChineseTextToZero(input);
                default:
                    return null;
            }
        }

        private static string TryHexWithDescription(string input)
        {
            var m = HexDescPattern.Match(input);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string TryNumberWithParenHex(string input)
        {
            var m = NumParenHexPattern.Match(input);
            return m.Success ? m.Groups[1].Value : null;
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

            // Avoid false positives: left must be < right (e.g. 1-255, not a subtraction)
            if (int.TryParse(m.Groups[1].Value, out int left) &&
                int.TryParse(m.Groups[2].Value, out int right) &&
                left < right)
            {
                return m.Groups[1].Value + "/" + m.Groups[2].Value;
            }
            return null;
        }

        private static string TryNumberWithParenAnnotation(string input)
        {
            var m = NumParenAnnotPattern.Match(input);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string TryNumberWithUnitSuffix(string input)
        {
            var m = NumUnitSuffixPattern.Match(input);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string TryValueWithTolerance(string input)
        {
            var m = ValueTolerancePattern.Match(input);
            if (!m.Success) return null;

            if (!double.TryParse(m.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double center) ||
                !double.TryParse(m.Groups[2].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double tol))
                return null;

            double lo = center - tol;
            double hi = center + tol;
            return FormatNum(lo) + "/" + FormatNum(hi);
        }

        private static string TryHexWithParenDescription(string input)
        {
            var m = HexParenDescPattern.Match(input);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string TryChineseTextToZero(string input)
        {
            var m = ChineseZeroPattern.Match(input);
            return m.Success ? "0" : null;
        }

        private static string FormatNum(double value)
        {
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
