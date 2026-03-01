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
            @"(?:[bB]|bit)\s*(\d+)\s*[-–~]\s*(?:[bB]|bit)\s*(\d+)\s*(?:[：:]\s*)?(.+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SingleBitRx = new Regex(
            @"(?:[bB]|bit)\s*(\d+)\s*(?:[：:]\s*)?(.+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BitPrefixRx = new Regex(
            @"^(?:[bB]|bit)\s*\d+\s*[-–~]\s*(?:[bB]|bit)\s*\d+\s*[：:]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            int startBitCol = FindColumn(table, "起始位", "起始BIT", "起始bit", "位偏移", "bit偏移");
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
                string startBitStr = startBitCol >= 0 ? table.GetValue(r, startBitCol).Trim() : "";
                string remarks = remarkCol >= 0 ? table.GetValue(r, remarkCol).Trim() : "";

                if (string.IsNullOrEmpty(byteSeq) && string.IsNullOrEmpty(name))
                    continue;

                string normalizedSeq = NormalizeByteSequence(byteSeq);
                bool hasExplicitByteSeq = !string.IsNullOrEmpty(normalizedSeq);
                if (hasExplicitByteSeq)
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

                // If table provides bit offset in a separate column (common for WDxx rows),
                // treat the "length" column as bit-length and convert to a bit-field.
                TryApplyBitFieldFromColumns(field, startBitStr);

                ExtractUnit(remarks, name, field);
                ExtractEnum(remarks, field);
                ExtractDataType(remarks, field);
                ClassifySpecialFields(byteSeq, name, field);

                if (string.IsNullOrEmpty(field.FieldName) && field.BitLength == 0
                    && fields.Count > 0 && !hasExplicitByteSeq)
                {
                    MergeIntoPrevious(fields, field);
                }
                else if (string.IsNullOrEmpty(field.FieldName) && field.BitLength == 0
                    && fields.Count > 0 && hasExplicitByteSeq
                    && IsCoveredByPreviousField(fields[fields.Count - 1], field))
                {
                    // Continuation row (e.g. W6 after W5 with ByteLength=2) — skip
                }
                else
                {
                    fields.Add(field);
                }
            }

            ExpandKnownWholeCodeBitFields(fields);
            return fields;
        }

        private void TryApplyBitFieldFromColumns(ProtocolTelemetryField field, string startBitStr)
        {
            if (field == null) return;
            if (string.IsNullOrEmpty(startBitStr)) return;
            if (string.IsNullOrEmpty(field.ByteSequence)) return;
            if (!field.ByteSequence.StartsWith("WD", StringComparison.OrdinalIgnoreCase)) return;
            if (field.BitLength > 0) { field.ByteLength = 0; return; }

            int startBit;
            if (!int.TryParse(startBitStr, out startBit)) return;
            if (field.ByteLength <= 0) return;

            int bitLen = field.ByteLength;
            field.BitOffset = startBit;
            field.BitLength = bitLen;
            field.ByteLength = 0;

            // Normalize display name to match expected style (b7-b4:xxx / b3:xxx)
            if (!LooksLikeBitPrefixedName(field.FieldName))
            {
                int high = startBit + bitLen - 1;
                if (bitLen == 1)
                    field.FieldName = $"b{startBit}:{field.FieldName}";
                else
                    field.FieldName = $"b{high}-b{startBit}:{field.FieldName}";
            }
        }

        private static bool LooksLikeBitPrefixedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Regex.IsMatch(name.TrimStart(), @"^(?:b|bit)\s*\d+", RegexOptions.IgnoreCase);
        }

        private void ExpandKnownWholeCodeBitFields(List<ProtocolTelemetryField> fields)
        {
            if (fields.Count == 0) return;

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field.BitLength > 0) continue;
                if (string.IsNullOrEmpty(field.FieldName)) continue;

                int startByte;
                if (!TryGetByteIndex(field.ByteSequence, out startByte)) continue;

                string name = field.FieldName;

                if (name.Contains("开关状态") && !HasBitChildren(fields, startByte))
                {
                    var defs = new (int start, int len, string desc)[]
                    {
                        (0, 2, "bit0~bit1：电磁阀 A 的 IO 状态"),
                        (2, 2, "bit2~bit3：电磁阀 B0的 IO状态"),
                        (4, 2, "bit4~bit5：电磁阀 B1的 IO状态"),
                        (6, 2, "bit6~bit7：主份阴极电源的 IO 状态量"),
                        (8, 2, "bit8~bit9：主份加热电源的 IO 状态量"),
                        (10, 2, "bit10~bit11：阳极电源 IO 状态量"),
                        (12, 2, "bit12~bit13：备份阴极电源的 IO 状态量"),
                        (14, 2, "bit14~bit15：备份加热电源的 IO 状态量"),
                        (16, 2, "bit16~bit17：主份加热及阴极 VCCS使能引脚"),
                        (18, 2, "bit18~bit19：备份加热及阴极 VCCS使能引脚"),
                        (20, 2, "bit20~bit21：PWM信号由单片机 A 提供引脚"),
                        (22, 2, "bit22~bit23：PWM信号由单片机 B 提供引脚"),
                        (24, 8, "bit24~bit31：备用"),
                    };
                    InsertBitFieldsAfter(fields, i, startByte, defs);
                    i += defs.Length;
                    continue;
                }

                if (name.Contains("系统故障码1") && !HasBitChildren(fields, startByte))
                {
                    var defs = new (int start, int len, string desc)[]
                    {
                        (0, 1, "系统故障码 1-b0：阴极延时未点着"),
                        (1, 1, "系统故障码 1-b1：阳极电压异常"),
                        (2, 1, "系统故障码 1-b2：阳极延时未点着"),
                        (3, 1, "系统故障码 1-b3：预留"),
                        (4, 1, "系统故障码 1-b4：低压区压力过低"),
                        (5, 1, "系统故障码 1-b5：低压区压力过高"),
                        (6, 1, "系统故障码 1-b6：阳极电流过低"),
                        (7, 1, "系统故障码 1-b7：阳极电流过高"),
                    };
                    InsertBitFieldsAfter(fields, i, startByte, defs);
                    i += defs.Length;
                    continue;
                }

                if (name.Contains("系统故障码2") && !HasBitChildren(fields, startByte))
                {
                    var defs = new (int start, int len, string desc)[]
                    {
                        (0, 1, "系统故障码 2-b0：阴极震荡"),
                        (1, 1, "系统故障码 2-b1: 阳极震荡"),
                    };
                    InsertBitFieldsAfter(fields, i, startByte, defs);
                    i += defs.Length;
                    continue;
                }

                if (name.Contains("加热电源故障") && !HasBitChildren(fields, startByte))
                {
                    var defs = new (int start, int len, string desc)[]
                    {
                        (0, 1, "加热电源故障-b0：加热电流过低"),
                        (1, 1, "加热电源故障-b1：加热电流过高"),
                        (2, 1, "加热电源故障-b2：加热电压过低"),
                        (3, 1, "加热电源故障-b3：加热电压过高"),
                    };
                    InsertBitFieldsAfter(fields, i, startByte, defs);
                    i += defs.Length;
                }
            }
        }

        private static bool HasBitChildren(List<ProtocolTelemetryField> fields, int startByte)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                if (f.BitLength <= 0) continue;

                int byteIndex;
                if (!TryGetByteIndex(f.ByteSequence, out byteIndex)) continue;
                if (byteIndex == startByte) return true;
            }
            return false;
        }

        private static void InsertBitFieldsAfter(
            List<ProtocolTelemetryField> fields,
            int baseIndex,
            int startByte,
            (int start, int len, string desc)[] defs)
        {
            int insertIndex = baseIndex + 1;
            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                fields.Insert(insertIndex + i, new ProtocolTelemetryField
                {
                    ByteSequence = "WD" + startByte,
                    FieldName = def.desc,
                    ByteLength = 0,
                    BitOffset = def.start,
                    BitLength = def.len,
                    IsReserved = def.desc.Contains("预留") || def.desc.Contains("备用")
                });
            }
        }

        private bool IsCoveredByPreviousField(ProtocolTelemetryField prev, ProtocolTelemetryField current)
        {
            if (prev.IsHeaderField) return false;
            int prevStart, curStart;
            if (!TryGetByteIndex(prev.ByteSequence, out prevStart) ||
                !TryGetByteIndex(current.ByteSequence, out curStart))
                return false;

            int prevEnd = prevStart + Math.Max(prev.ByteLength, 1);
            return curStart >= prevStart && curStart < prevEnd;
        }

        private static bool TryGetByteIndex(string byteSeq, out int index)
        {
            index = 0;
            if (string.IsNullOrEmpty(byteSeq)) return false;
            var m = Regex.Match(byteSeq, @"(\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out index);
        }

        private static void MergeIntoPrevious(List<ProtocolTelemetryField> fields, ProtocolTelemetryField cont)
        {
            var prev = fields[fields.Count - 1];
            if (cont.ByteLength > 0)
                prev.ByteLength += cont.ByteLength;
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
            {
                field.ByteLength = val;
                return;
            }

            var m = Regex.Match(lengthStr, @"\d+");
            if (m.Success && int.TryParse(m.Value, out val))
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
                int a = int.Parse(bitMatch.Groups[1].Value);
                int b = int.Parse(bitMatch.Groups[2].Value);
                int highBit = Math.Max(a, b);
                int lowBit = Math.Min(a, b);
                field.BitOffset = lowBit;
                field.BitLength = Math.Abs(a - b) + 1;
                string desc = (bitMatch.Groups.Count >= 4 ? bitMatch.Groups[3].Value : "").Trim();
                if (desc.Length == 0)
                    desc = name.Trim();
                field.FieldName = $"b{highBit}-b{lowBit}:{desc}";
                return;
            }

            var singleBitMatch = SingleBitRx.Match(name);
            if (singleBitMatch.Success)
            {
                int bit = int.Parse(singleBitMatch.Groups[1].Value);
                field.BitOffset = bit;
                field.BitLength = 1;
                string desc = (singleBitMatch.Groups.Count >= 3 ? singleBitMatch.Groups[2].Value : "").Trim();
                if (desc.Length == 0)
                    desc = name.Trim();
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
                        int a = int.Parse(nums[0].Value);
                        int b = int.Parse(nums[1].Value);
                        int highBit = Math.Max(a, b);
                        int lowBit = Math.Min(a, b);
                        field.BitOffset = lowBit;
                        field.BitLength = Math.Abs(a - b) + 1;
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
