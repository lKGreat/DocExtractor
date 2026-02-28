using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Normalization
{
    /// <summary>
    /// 默认值归一化实现：按 FieldDataType 统一值格式。
    /// </summary>
    public class DefaultValueNormalizer : IValueNormalizer
    {
        private static readonly Regex MultiWhitespace = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex UnitPattern = new Regex(
            @"^\s*([+\-]?\d[\d\s,\.]*)\s*([^\d\s].*)$",
            RegexOptions.Compiled);

        private static readonly HashSet<string> BuiltInTrueValues =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "true", "1", "y", "yes", "on", "是", "真", "√"
            };

        private static readonly HashSet<string> BuiltInFalseValues =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "false", "0", "n", "no", "off", "否", "假", "×"
            };

        public string Normalize(
            string rawValue,
            FieldDefinition field,
            ValueNormalizationOptions? options = null)
        {
            if (rawValue == null)
                return string.Empty;

            string input = rawValue.Trim();
            if (input.Length == 0)
                return string.Empty;

            options ??= new ValueNormalizationOptions();

            return field.DataType switch
            {
                FieldDataType.Integer => NormalizeInteger(input),
                FieldDataType.Decimal => NormalizeDecimal(input, options),
                FieldDataType.HexCode => NormalizeHex(input, options),
                FieldDataType.Unit => NormalizeUnit(input, options),
                FieldDataType.Boolean => NormalizeBoolean(input, options),
                FieldDataType.Enumeration => NormalizeEnumeration(input),
                FieldDataType.Formula => NormalizeWhitespace(input),
                _ => NormalizeWhitespace(input)
            };
        }

        private static string NormalizeInteger(string raw)
        {
            if (!TryParseDecimal(raw, out var parsed))
                return NormalizeWhitespace(raw);

            long truncated = (long)decimal.Truncate(parsed);
            return truncated.ToString(CultureInfo.InvariantCulture);
        }

        private static string NormalizeDecimal(string raw, ValueNormalizationOptions options)
        {
            if (!TryParseDecimal(raw, out var parsed))
                return NormalizeWhitespace(raw);

            string normalized;
            if (options.DefaultDecimalPlaces.HasValue)
            {
                int places = Math.Max(0, options.DefaultDecimalPlaces.Value);
                decimal rounded = decimal.Round(parsed, places, MidpointRounding.AwayFromZero);
                normalized = rounded.ToString("F" + places, CultureInfo.InvariantCulture);
            }
            else
            {
                normalized = parsed.ToString("0.############################", CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrEmpty(options.DecimalSeparator) && options.DecimalSeparator != ".")
                normalized = normalized.Replace(".", options.DecimalSeparator);

            return normalized;
        }

        private static string NormalizeHex(string raw, ValueNormalizationOptions options)
        {
            string text = raw.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);
            if (text.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(0, text.Length - 1);

            if (text.Length == 0)
                return NormalizeWhitespace(raw);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool isHex = (c >= '0' && c <= '9')
                             || (c >= 'a' && c <= 'f')
                             || (c >= 'A' && c <= 'F');
                if (!isHex)
                    return NormalizeWhitespace(raw);
            }

            string normalized = options.HexUpperCase
                ? text.ToUpperInvariant()
                : text.ToLowerInvariant();

            string prefix = options.HexPrefix ?? "0x";
            return prefix + normalized;
        }

        private string NormalizeUnit(string raw, ValueNormalizationOptions options)
        {
            var match = UnitPattern.Match(raw);
            if (!match.Success)
                return NormalizeWhitespace(raw);

            string numberRaw = match.Groups[1].Value;
            string unitRaw = match.Groups[2].Value;

            string number = NormalizeDecimal(numberRaw, options);
            string unit = NormalizeWhitespace(unitRaw);

            return string.IsNullOrWhiteSpace(unit) ? number : number + " " + unit;
        }

        private static string NormalizeBoolean(string raw, ValueNormalizationOptions options)
        {
            string key = NormalizeWhitespace(raw).ToLowerInvariant();

            if (options.BooleanTrueAliases.TryGetValue(key, out var trueAlias))
                return string.IsNullOrWhiteSpace(trueAlias) ? "true" : trueAlias;
            if (options.BooleanFalseAliases.TryGetValue(key, out var falseAlias))
                return string.IsNullOrWhiteSpace(falseAlias) ? "false" : falseAlias;

            if (BuiltInTrueValues.Contains(key))
                return "true";
            if (BuiltInFalseValues.Contains(key))
                return "false";

            return NormalizeWhitespace(raw);
        }

        private static string NormalizeEnumeration(string raw)
        {
            string text = raw.Replace("\r\n", ";")
                .Replace("\n", ";")
                .Replace("；", ";")
                .Replace("、", ";")
                .Replace("/", ";");

            var parts = text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();

            var normalizedParts = new List<string>(parts.Count);
            foreach (var part in parts)
            {
                string kv = part.Replace("：", ":");
                int eq = kv.IndexOf('=');
                int colon = kv.IndexOf(':');
                int splitPos = eq >= 0 ? eq : colon;
                if (splitPos > 0 && splitPos < kv.Length - 1)
                {
                    string key = NormalizeWhitespace(kv.Substring(0, splitPos));
                    string value = NormalizeWhitespace(kv.Substring(splitPos + 1));
                    normalizedParts.Add(key + "=" + value);
                }
                else
                {
                    normalizedParts.Add(NormalizeWhitespace(kv));
                }
            }

            return string.Join(";", normalizedParts);
        }

        private static string NormalizeWhitespace(string value)
        {
            return MultiWhitespace.Replace(value.Trim(), " ");
        }

        private static bool TryParseDecimal(string raw, out decimal value)
        {
            value = 0m;
            string s = NormalizeWhitespace(raw).Replace(" ", string.Empty);
            if (s.Length == 0) return false;

            int dotIndex = s.LastIndexOf('.');
            int commaIndex = s.LastIndexOf(',');

            if (dotIndex >= 0 && commaIndex >= 0)
            {
                // 同时存在 . 和 , ：最后出现者作为小数分隔符
                if (commaIndex > dotIndex)
                {
                    s = s.Replace(".", string.Empty);
                    s = s.Replace(",", ".");
                }
                else
                {
                    s = s.Replace(",", string.Empty);
                }
            }
            else if (commaIndex >= 0)
            {
                // 仅有逗号：启发式判断小数还是千分位
                int commaCount = s.Count(ch => ch == ',');
                if (commaCount == 1)
                {
                    int decimals = s.Length - commaIndex - 1;
                    bool decimalCandidate = decimals > 0 && decimals <= 2;
                    s = decimalCandidate ? s.Replace(",", ".") : s.Replace(",", string.Empty);
                }
                else
                {
                    s = s.Replace(",", string.Empty);
                }
            }

            return decimal.TryParse(
                s,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
