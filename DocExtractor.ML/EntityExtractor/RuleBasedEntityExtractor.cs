using System.Collections.Generic;
using System.Text.RegularExpressions;
using DocExtractor.Core.Interfaces;

namespace DocExtractor.ML.EntityExtractor
{
    /// <summary>
    /// 规则引擎实体抽取器（正则表达式）
    /// 处理高置信度的结构化模式，作为 ML 模型的前置过滤
    /// </summary>
    internal class RuleBasedEntityExtractor
    {
        // 十六进制：0x1A、0X1a、1Ah
        private static readonly Regex _hexRegex = new Regex(
            @"(?:0[xX][0-9A-Fa-f]+|[0-9A-Fa-f]+[Hh]\b)",
            RegexOptions.Compiled);

        // 数值（含小数、负数、科学计数）
        private static readonly Regex _valueRegex = new Regex(
            @"-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?",
            RegexOptions.Compiled);

        // 单位（常见物理单位，含中文 + 遥测领域单位）
        private static readonly Regex _unitRegex = new Regex(
            @"\b(?:V|mV|kV|A|mA|μA|W|mW|kW|Hz|kHz|MHz|GHz|s|ms|μs|us|ns|℃|°C|°|deg|rpm|Ω|ohm|bit|byte|Byte|KB|MB|GB|TB|dB|dBm|dBW|rad|bps|kbps|Mbps|Gbps|bit/s|字节|帧|码元|ppm)\b|[%‰]",
            RegexOptions.Compiled);

        // 公式系数：A=xxx, B=xxx
        private static readonly Regex _formulaRegex = new Regex(
            @"[A-Da-d]\s*=\s*-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?",
            RegexOptions.Compiled);

        // 枚举映射：0=关闭;1=开启 或 00:正常/01:故障 格式
        private static readonly Regex _enumRegex = new Regex(
            @"\d+\s*[:=]\s*[^\s;/,]+(?:\s*[;/,]\s*\d+\s*[:=]\s*[^\s;/,]+)+",
            RegexOptions.Compiled);

        public IReadOnlyList<NamedEntity> Extract(string text)
        {
            var entities = new List<NamedEntity>();

            // 优先级：HexCode > Enum > Formula > Unit > Value
            AddMatches(entities, _hexRegex, text, EntityType.HexCode, 0.99f);
            AddMatches(entities, _enumRegex, text, EntityType.Enum, 0.97f);
            AddMatches(entities, _formulaRegex, text, EntityType.Formula, 0.98f);
            AddMatches(entities, _unitRegex, text, EntityType.Unit, 0.95f);
            AddMatches(entities, _valueRegex, text, EntityType.Value, 0.90f);

            return entities;
        }

        private static void AddMatches(
            List<NamedEntity> result,
            Regex regex,
            string text,
            EntityType type,
            float confidence)
        {
            foreach (Match m in regex.Matches(text))
            {
                // 避免与已有实体重叠
                bool overlaps = false;
                foreach (var existing in result)
                {
                    if (m.Index <= existing.EndIndex && m.Index + m.Length - 1 >= existing.StartIndex)
                    { overlaps = true; break; }
                }
                if (!overlaps)
                {
                    result.Add(new NamedEntity
                    {
                        Text = m.Value,
                        Type = type,
                        StartIndex = m.Index,
                        EndIndex = m.Index + m.Length - 1,
                        Confidence = confidence
                    });
                }
            }
        }
    }
}
