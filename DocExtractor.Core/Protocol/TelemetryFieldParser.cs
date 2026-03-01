using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Protocol
{
    /// <summary>
    /// Parses individual rows from a telemetry definition table (字序/数据内容/字节长度/备注)
    /// into structured <see cref="ProtocolTelemetryField"/> objects.
    /// Handles bit-field decomposition, unit extraction, and enum mapping parsing.
    /// </summary>
    public class TelemetryFieldParser
    {
        private static readonly Regex ByteSeqRangeRx = new Regex(
            @"^[WBw](\d+)\s*[-–~]\s*[WBw]?(\d+)$", RegexOptions.Compiled);

        private static readonly Regex ByteSeqSingleRx = new Regex(
            @"^[WBw](\d+)$", RegexOptions.Compiled);

        private static readonly Regex BitFieldRx = new Regex(
            @"[bB](\d+)\s*[-–~]\s*[bB](\d+)\s*[：:]\s*(.+)", RegexOptions.Compiled);

        private static readonly Regex SingleBitRx = new Regex(
            @"[bB](\d+)\s*[：:]\s*(.+)", RegexOptions.Compiled);

        private static readonly Regex BitPrefixRx = new Regex(
            @"^[bB]\d+\s*[-–~]\s*[bB]\d+\s*[：:]", RegexOptions.Compiled);

        private static readonly Regex UnitRx = new Regex(
            @"单位[：:\s]*([^\s,，;；、\n\r]+)", RegexOptions.Compiled);

        private static readonly Regex InlineUnitRx = new Regex(
            @"(?:当量，|分辨率[：:\s]*\d+[；;]\s*)(?:单位)?[：:\s]*([\w°℃%]+)",
            RegexOptions.Compiled);

        private static readonly Regex EnumRx = new Regex(
            @"(0[xX][\dA-Fa-f]+)\s*[-–:：]\s*([^||\n\r]+)",
            RegexOptions.Compiled);

        private static readonly Regex EnumDecimalRx = new Regex(
            @"(\d+)\s*[-–:：]\s*([^||\n\r;；]+)",
            RegexOptions.Compiled);

        private static readonly Regex DataTypeRx = new Regex(
            @"(UINT\d+|INT\d+|FLOAT\d*|DOUBLE|U?INT_?\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ResolutionUnitRx = new Regex(
            @"分辨率[：:\s]*\d+[；;]?\s*(?:范围)?[^单]*?单位[：:\s]*([\w°℃%]+)",
            RegexOptions.Compiled);

        /// <summary>
        /// Parse all data rows from a telemetry definition table into fields.
        /// </summary>
        public List<ProtocolTelemetryField> Parse(RawTable table)
        {
            int byteSeqCol = FindColumn(table, "字序", "字节", "偏移");
            int nameCol = FindColumn(table, "数据内容", "名称", "参数名");
            int lengthCol = FindColumn(table, "字节长度", "长度", "字节数");
            int remarkCol = FindColumn(table, "备注", "说明", "描述");

            if (byteSeqCol < 0) byteSeqCol = 0;
            if (nameCol < 0) nameCol = Math.Min(1, table.ColCount - 1);
            if (lengthCol < 0) lengthCol = Math.Min(2, table.ColCount - 1);
            if (remarkCol < 0) remarkCol = table.ColCount > 3 ? 3 : -1;

            var fields = new List<ProtocolTelemetryField>();
            int headerRows = DetectHeaderRows(table);
            string lastByteSeq = "";

            for (int r = headerRows; r < table.RowCount; r++)
            {
                string byteSeq = table.GetValue(r, byteSeqCol).Trim();
                string name = table.GetValue(r, nameCol).Trim();
                string lengthStr = table.GetValue(r, lengthCol).Trim();
                string remarks = remarkCol >= 0 ? table.GetValue(r, remarkCol).Trim() : "";

                if (string.IsNullOrEmpty(byteSeq) && string.IsNullOrEmpty(name))
                    continue;

                string normalizedSeq = NormalizeByteSequence(byteSeq);
                if (!string.IsNullOrEmpty(normalizedSeq))
                    lastByteSeq = normalizedSeq;

                var field = new ProtocolTelemetryField
                {
                    ByteSequence = normalizedSeq,
                    Remarks = remarks
                };

                ParseByteLength(lengthStr, field);
                ParseFieldName(name, field);

                if (string.IsNullOrEmpty(field.ByteSequence) && !string.IsNullOrEmpty(lastByteSeq))
                    field.ByteSequence = lastByteSeq;

                ExtractUnit(remarks, name, field);
                ExtractEnum(remarks, field);
                ExtractDataType(remarks, field);
                ClassifySpecialFields(byteSeq, name, field);

                if (string.IsNullOrEmpty(field.FieldName) && field.BitLength == 0
                    && fields.Count > 0)
                {
                    MergeIntoPrevious(fields, field);
                }
                else
                {
                    fields.Add(field);
                }
            }

            return fields;
        }

        private static void MergeIntoPrevious(List<ProtocolTelemetryField> fields, ProtocolTelemetryField cont)
        {
            var prev = fields[fields.Count - 1];
            string contNum = ExtractByteNum(cont.ByteSequence);
            if (!string.IsNullOrEmpty(contNum))
            {
                string prevPrefix = prev.ByteSequence.StartsWith("W") ? "W" : "";
                if (!string.IsNullOrEmpty(prevPrefix))
                    prev.ByteSequence = prevPrefix + ExtractByteNum(prev.ByteSequence)
                                        + "-" + prevPrefix + contNum;
            }
            if (!string.IsNullOrEmpty(cont.Remarks) && string.IsNullOrEmpty(prev.Remarks))
                prev.Remarks = cont.Remarks;
            if (!string.IsNullOrEmpty(cont.Unit) && string.IsNullOrEmpty(prev.Unit))
                prev.Unit = cont.Unit;
        }

        private static string ExtractByteNum(string byteSeq)
        {
            if (string.IsNullOrEmpty(byteSeq)) return "";
            var m = Regex.Match(byteSeq, @"(\d+)$");
            return m.Success ? m.Groups[1].Value : "";
        }

        private int DetectHeaderRows(RawTable table)
        {
            for (int r = 0; r < Math.Min(3, table.RowCount); r++)
            {
                string first = table.GetValue(r, 0).Trim();
                if (first == "字序" || first == "字段" || first == "偏移" || first == "序号")
                    return r + 1;
            }
            return 1;
        }

        private int FindColumn(RawTable table, params string[] keywords)
        {
            int headerRows = Math.Min(2, table.RowCount);
            for (int r = 0; r < headerRows; r++)
            {
                for (int c = 0; c < table.ColCount; c++)
                {
                    string val = table.GetValue(r, c).Trim();
                    foreach (string kw in keywords)
                    {
                        if (val.Contains(kw))
                            return c;
                    }
                }
            }
            return -1;
        }

        private string NormalizeByteSequence(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            raw = raw.Replace(" ", "").Replace("\n", "").Replace("\r", "");

            raw = raw.Replace("w", "W").Replace("b", "B");
            raw = raw.Replace("–", "-").Replace("~", "-");

            if (raw.Contains("-") && !raw.StartsWith("W") && !raw.StartsWith("B"))
                return raw;

            var rangeMatch = Regex.Match(raw, @"^([WB])(\d+)-(\d+)$");
            if (rangeMatch.Success)
            {
                return rangeMatch.Groups[1].Value +
                       rangeMatch.Groups[2].Value + "-" +
                       rangeMatch.Groups[1].Value +
                       rangeMatch.Groups[3].Value;
            }

            return raw;
        }

        private void ParseByteLength(string lengthStr, ProtocolTelemetryField field)
        {
            if (string.IsNullOrEmpty(lengthStr)) return;
            lengthStr = lengthStr.Trim();
            int val;
            if (int.TryParse(lengthStr, out val))
                field.ByteLength = val;
        }

        private void ParseFieldName(string name, ProtocolTelemetryField field)
        {
            if (string.IsNullOrEmpty(name))
            {
                field.FieldName = name;
                return;
            }

            var bitMatch = BitFieldRx.Match(name);
            if (bitMatch.Success)
            {
                int highBit = int.Parse(bitMatch.Groups[1].Value);
                int lowBit = int.Parse(bitMatch.Groups[2].Value);
                field.BitOffset = lowBit;
                field.BitLength = highBit - lowBit + 1;
                string desc = bitMatch.Groups[3].Value.Trim();
                field.FieldName = $"b{highBit}-b{lowBit}:{desc}";
                return;
            }

            var singleBitMatch = SingleBitRx.Match(name);
            if (singleBitMatch.Success)
            {
                int bit = int.Parse(singleBitMatch.Groups[1].Value);
                field.BitOffset = bit;
                field.BitLength = 1;
                string desc = singleBitMatch.Groups[2].Value.Trim();
                field.FieldName = $"b{bit}:{desc}";
                return;
            }

            if (BitPrefixRx.IsMatch(name))
            {
                var colonIdx = name.IndexOfAny(new[] { '：', ':' });
                if (colonIdx > 0)
                {
                    string prefix = name.Substring(0, colonIdx);
                    string desc = name.Substring(colonIdx + 1).Trim();

                    var nums = Regex.Matches(prefix, @"\d+");
                    if (nums.Count >= 2)
                    {
                        int highBit = int.Parse(nums[0].Value);
                        int lowBit = int.Parse(nums[1].Value);
                        field.BitOffset = lowBit;
                        field.BitLength = highBit - lowBit + 1;
                        field.FieldName = $"b{highBit}-b{lowBit}:{desc}";
                    }
                    else
                    {
                        field.FieldName = desc;
                    }
                    return;
                }
            }

            field.FieldName = name;
        }

        private void ExtractUnit(string remarks, string name, ProtocolTelemetryField field)
        {
            if (string.IsNullOrEmpty(remarks) && string.IsNullOrEmpty(name))
                return;

            var match = UnitRx.Match(remarks);
            if (match.Success)
            {
                field.Unit = CleanUnit(match.Groups[1].Value);
                return;
            }

            match = ResolutionUnitRx.Match(remarks);
            if (match.Success)
            {
                field.Unit = CleanUnit(match.Groups[1].Value);
                return;
            }

            match = InlineUnitRx.Match(remarks);
            if (match.Success)
            {
                field.Unit = CleanUnit(match.Groups[1].Value);
                return;
            }

            var directUnitMatch = Regex.Match(remarks,
                @"(?:当量，|，)单位[：:\s]*([\w°℃%.]+)");
            if (directUnitMatch.Success)
            {
                field.Unit = CleanUnit(directUnitMatch.Groups[1].Value);
            }
        }

        private string CleanUnit(string unit)
        {
            if (string.IsNullOrEmpty(unit)) return unit;
            unit = unit.TrimEnd('，', ',', '；', ';', '。', '.', '、');
            unit = Regex.Replace(unit, @"^[\d.]+", "");
            if (string.IsNullOrEmpty(unit)) return unit;
            if (unit.Length > 10) unit = unit.Substring(0, 10);
            return unit;
        }

        private void ExtractEnum(string remarks, ProtocolTelemetryField field)
        {
            if (string.IsNullOrEmpty(remarks)) return;

            if (!remarks.Contains("|") && !remarks.Contains("；") &&
                !Regex.IsMatch(remarks, @"0[xX][\dA-Fa-f]+\s*[-–:：]"))
                return;

            string enumStr = remarks;
            if (remarks.Contains("|"))
            {
                int startIdx = 0;
                var hexStart = Regex.Match(remarks, @"0[xX][\dA-Fa-f]+");
                if (hexStart.Success) startIdx = hexStart.Index;

                int digitStart = -1;
                for (int i = 0; i < remarks.Length; i++)
                {
                    if (char.IsDigit(remarks[i]))
                    {
                        int pipeAfter = remarks.IndexOf('|', i);
                        if (pipeAfter > i)
                        {
                            digitStart = i;
                            break;
                        }
                    }
                }

                if (hexStart.Success)
                    startIdx = hexStart.Index;
                else if (digitStart >= 0)
                    startIdx = digitStart;

                enumStr = remarks.Substring(startIdx);
            }

            var hexMatches = EnumRx.Matches(enumStr);
            if (hexMatches.Count > 0)
            {
                var parts = new List<string>();
                foreach (Match m in hexMatches)
                {
                    string desc = m.Groups[2].Value.Trim();
                    if (!IsValidEnumDescription(desc)) continue;
                    parts.Add(m.Groups[1].Value + "-" + desc);
                }
                if (parts.Count >= 2)
                {
                    field.EnumMapping = string.Join("|", parts);
                    return;
                }
            }

            if (remarks.Contains("|"))
            {
                var decMatches = EnumDecimalRx.Matches(enumStr);
                if (decMatches.Count >= 2)
                {
                    var parts = new List<string>();
                    foreach (Match m in decMatches)
                    {
                        string desc = m.Groups[2].Value.Trim();
                        if (!IsValidEnumDescription(desc)) continue;
                        parts.Add(m.Groups[1].Value + "-" + desc);
                    }
                    if (parts.Count >= 2)
                        field.EnumMapping = string.Join("|", parts);
                }
            }
        }

        private static bool IsValidEnumDescription(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return false;
            if (Regex.IsMatch(desc, @"^0[xX][\dA-Fa-f]+$")) return false;
            if (Regex.IsMatch(desc, @"^[\d.]+$")) return false;
            if (Regex.IsMatch(desc, @"^[\d\s,，.xXA-Fa-f\-–~]+$")) return false;
            if (desc.Contains("取值") || desc.Contains("范围") || desc.Contains("进制"))
                return false;
            return true;
        }

        private void ExtractDataType(string remarks, ProtocolTelemetryField field)
        {
            if (string.IsNullOrEmpty(remarks)) return;
            var match = DataTypeRx.Match(remarks);
            if (match.Success)
                field.DataTypeHint = match.Groups[1].Value.ToUpperInvariant();
        }

        private void ClassifySpecialFields(string byteSeq, string name, ProtocolTelemetryField field)
        {
            string bsUpper = (byteSeq ?? "").Trim().ToUpperInvariant();
            string nmLower = (name ?? "").ToLowerInvariant();

            if (bsUpper == "DH" || bsUpper == "DL")
            {
                field.IsHeaderField = true;
                return;
            }

            if (bsUpper == "SUM" || bsUpper == "校验和")
            {
                field.IsChecksum = true;
                return;
            }

            if (nmLower.Contains("预留") || nmLower.Contains("保留") ||
                nmLower.Contains("备用") || nmLower.Contains("reserved"))
            {
                field.IsReserved = true;
            }
        }
    }
}
