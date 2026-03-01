using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Protocol
{
    /// <summary>
    /// Parses telecommand tables into command entries, presets, and CAN frame metadata.
    /// </summary>
    public class TelecommandFieldParser
    {
        private static readonly Regex WordHeaderRx = new Regex(@"^W(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex HexByteRx = new Regex(@"^0[xX]([0-9A-Fa-f]{1,2})$", RegexOptions.Compiled);
        private static readonly Regex RangeRx = new Regex(@"范围[：:\s【\[]*([0-9\-~～]+)", RegexOptions.Compiled);
        private static readonly Regex UnitRx = new Regex(@"单位[：:\s]*([^\s，,；;。]+)", RegexOptions.Compiled);
        private static readonly Regex DigitRx = new Regex(@"\d+", RegexOptions.Compiled);

        public List<TelecommandEntry> ParseCommandSummaryTable(RawTable table)
        {
            var results = new List<TelecommandEntry>();
            int headerRow = DetectHeaderRow(table, "指令名称", "指令码");
            int nameCol = FindColumn(table, headerRow, "指令名称");
            int codeCol = FindColumn(table, headerRow, "指令码");
            int paramCol = FindColumn(table, headerRow, "指令参数");
            int remarkCol = FindColumn(table, headerRow, "备注");

            if (nameCol < 0 || codeCol < 0)
                return results;

            for (int r = headerRow + 1; r < table.RowCount; r++)
            {
                string name = table.GetValue(r, nameCol).Trim();
                string code = table.GetValue(r, codeCol).Trim();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
                    continue;

                if (!TryParseHexByte(code, out byte codeByte))
                    continue;

                var entry = new TelecommandEntry
                {
                    Name = name,
                    Code = NormalizeHexCode(code),
                    CommandCode = codeByte,
                    ParamDesc = paramCol >= 0 ? table.GetValue(r, paramCol).Trim() : "",
                    Remark = remarkCol >= 0 ? table.GetValue(r, remarkCol).Trim() : "",
                    Type = GuessType(name, paramCol >= 0 ? table.GetValue(r, paramCol).Trim() : ""),
                    CodeAlias = BuildAlias(name)
                };

                results.Add(entry);
            }

            return results;
        }

        public List<TelecommandEntry> ParseCommandFrameTable(RawTable table)
        {
            var results = new List<TelecommandEntry>();
            int headerRow = DetectHeaderRow(table, "命令", "B0");
            int cmdCol = FindColumn(table, headerRow, "命令");
            int b0Col = FindColumn(table, headerRow, "B0");
            if (cmdCol < 0 || b0Col < 0)
                return results;

            var byteCols = new List<int>();
            for (int c = 0; c < table.ColCount; c++)
            {
                string h = table.GetValue(headerRow, c).Trim();
                if (Regex.IsMatch(h, @"^B[1-7]$", RegexOptions.IgnoreCase))
                    byteCols.Add(c);
            }
            byteCols.Sort();

            for (int r = headerRow + 1; r < table.RowCount; r++)
            {
                string name = table.GetValue(r, cmdCol).Trim();
                string code = table.GetValue(r, b0Col).Trim();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
                    continue;

                if (!TryParseHexByte(code, out byte codeByte))
                    continue;

                var entry = new TelecommandEntry
                {
                    Name = name,
                    Code = NormalizeHexCode(code),
                    CommandCode = codeByte,
                    Type = GuessType(name, ""),
                    CodeAlias = BuildAlias(name)
                };

                var paramBytes = new byte[7];
                for (int i = 0; i < 7; i++)
                    paramBytes[i] = 0x00;

                for (int i = 0; i < byteCols.Count && i < 7; i++)
                {
                    string token = table.GetValue(r, byteCols[i]).Trim();
                    if (TryParseHexByte(token, out byte val))
                        paramBytes[i] = val;
                    else
                        paramBytes[i] = 0x00;
                }

                entry.DefaultParameterBytes = paramBytes;
                results.Add(entry);
            }

            return results;
        }

        public List<(string CommandCode, TelecommandPreset Preset, List<TelecommandParameter> Parameters)> ParseParameterDetailTable(RawTable table)
        {
            var results = new List<(string CommandCode, TelecommandPreset Preset, List<TelecommandParameter> Parameters)>();
            int headerRow = DetectHeaderRow(table, "编号", "指令名称");
            int nameCol = FindColumn(table, headerRow, "指令名称");
            int codeCol = FindColumn(table, headerRow, "指令码");
            int remarkCol = FindColumn(table, headerRow, "备注");
            if (nameCol < 0 || codeCol < 0)
                return results;

            var wordCols = new List<(int WordIndex, int ColIndex)>();
            for (int c = 0; c < table.ColCount; c++)
            {
                string header = table.GetValue(headerRow, c).Trim();
                Match m = WordHeaderRx.Match(header);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int idx))
                    wordCols.Add((idx, c));
            }
            wordCols = wordCols.OrderBy(x => x.WordIndex).ToList();
            if (wordCols.Count == 0)
                return results;

            for (int r = headerRow + 1; r < table.RowCount; r++)
            {
                string name = table.GetValue(r, nameCol).Trim();
                string cmdCode = table.GetValue(r, codeCol).Trim();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(cmdCode))
                    continue;

                string remark = remarkCol >= 0 ? table.GetValue(r, remarkCol).Trim() : "";
                var preset = new TelecommandPreset
                {
                    Name = name,
                    Remark = remark,
                    ParameterBytes = new byte[7]
                };

                for (int i = 0; i < 7; i++)
                    preset.ParameterBytes[i] = 0x00;

                // W0 对应参数字节索引 0，即整帧中的 W6。
                foreach (var item in wordCols)
                {
                    if (item.WordIndex > 6)
                        continue;

                    string token = table.GetValue(r, item.ColIndex).Trim();
                    if (TryParseHexByte(token, out byte val))
                        preset.ParameterBytes[item.WordIndex] = val;
                }

                List<TelecommandParameter> parameters = BuildEditableParameters(wordCols, table, r, remark);
                results.Add((NormalizeHexCode(cmdCode), preset, parameters));
            }

            return results;
        }

        public List<CanFrameInfo> ParseCanIdSummaryTable(RawTable table)
        {
            var infos = new List<CanFrameInfo>();
            int headerRow = DetectHeaderRow(table, "优先级", "总线标志");
            int frameCol = FindColumn(table, headerRow, "命令");
            int pCol = FindColumn(table, headerRow, "优先级");
            int ltCol = FindColumn(table, headerRow, "总线标志");
            int dtCol = FindColumn(table, headerRow, "数据类型");
            int daCol = FindColumn(table, headerRow, "目的地址");
            int saCol = FindColumn(table, headerRow, "源地址");
            int ftCol = FindColumn(table, headerRow, "单/复帧标识");
            int fcCol = FindColumn(table, headerRow, "帧计数");
            int channelCol = table.ColCount - 1;

            if (pCol < 0 || ltCol < 0 || dtCol < 0 || daCol < 0 || saCol < 0 || ftCol < 0 || fcCol < 0)
                return infos;

            for (int r = headerRow + 1; r < table.RowCount; r++)
            {
                string frameType = frameCol >= 0 ? table.GetValue(r, frameCol).Trim() : "";
                string p = table.GetValue(r, pCol).Trim();
                string lt = table.GetValue(r, ltCol).Trim();
                string dt = table.GetValue(r, dtCol).Trim();
                string da = table.GetValue(r, daCol).Trim();
                string sa = table.GetValue(r, saCol).Trim();
                string ft = table.GetValue(r, ftCol).Trim();
                string fc = table.GetValue(r, fcCol).Trim();
                string channel = table.GetValue(r, channelCol).Trim();

                if (string.IsNullOrEmpty(frameType))
                    continue;

                if (!TryParseBinaryField(p, out int pVal)) continue;
                if (!TryParseBinaryField(lt, out int ltVal)) continue;
                if (!TryParseBinaryField(dt, out int dtVal)) continue;
                if (!TryParseBinaryField(da, out int daVal)) continue;
                if (!TryParseBinaryField(sa, out int saVal)) continue;
                if (!TryParseBinaryField(ft, out int ftVal)) continue;
                if (!TryParseBinaryField(fc, out int fcVal)) continue;

                infos.Add(new CanFrameInfo
                {
                    FrameType = frameType,
                    Channel = ExtractChannel(channel, ltVal),
                    Priority = pVal,
                    BusFlag = ltVal,
                    DataType = dtVal,
                    DestAddr = daVal,
                    SrcAddr = saVal,
                    FrameFlag = ftVal,
                    FrameCount = fcVal,
                    HeaderBytes = TelecommandAnalyzer.BuildFrameHeaderBytes(pVal, ltVal, dtVal, daVal, saVal, ftVal, fcVal)
                });
            }

            return infos;
        }

        public string BuildAlias(string name)
        {
            if (name.Contains("同步遥测请求")) return "YCQQ-TB";
            if (name.Contains("异步遥测请求")) return "YCQQ-YB";
            if (name.Contains("复位A")) return "FWA";
            if (name.Contains("复位B")) return "FWB";
            if (name.Contains("复位AB")) return "FWAB";
            if (name.Contains("开机")) return "KJ";
            if (name.Contains("关机")) return "GJ";
            if (name.Contains("阴极激活")) return "YJJH";
            if (name.Contains("除气")) return "CQ";
            if (name.Contains("二次点火")) return "ECDH";
            if (name.Contains("自检")) return "ZJ";
            if (name.Contains("单步使能")) return "DBSN";
            if (name.Contains("单步")) return "DB";
            if (name.Contains("参数配置")) return "CSPZ";
            if (name.Contains("KB") && name.Contains("配置")) return "KBPZ";
            if (name.Contains("清零")) return "QL";
            return "CMD";
        }

        private List<TelecommandParameter> BuildEditableParameters(
            List<(int WordIndex, int ColIndex)> wordCols,
            RawTable table,
            int row,
            string remark)
        {
            var parameters = new List<TelecommandParameter>();
            string range = ExtractRange(remark);
            string unit = ExtractUnit(remark);

            foreach (var word in wordCols)
            {
                if (word.WordIndex > 6)
                    continue;

                string token = table.GetValue(row, word.ColIndex).Trim();
                if (!IsEditableToken(token))
                    continue;

                parameters.Add(new TelecommandParameter
                {
                    Name = $"W{word.WordIndex}",
                    StartByte = "W" + (6 + word.WordIndex).ToString(CultureInfo.InvariantCulture),
                    StartBit = "",
                    Length = 1,
                    InputType = "TextBox",
                    DataFormat = "Hex",
                    DefaultValue = "00",
                    OptionValues = "",
                    ValueRange = range,
                    Unit = unit,
                    Remark = remark
                });
            }

            if (parameters.Count > 1)
            {
                // 合并连续字节参数为一条更符合 TelecommandConfig 的配置行。
                int minWord = int.MaxValue;
                int maxWord = int.MinValue;
                foreach (var word in wordCols)
                {
                    string token = table.GetValue(row, word.ColIndex).Trim();
                    if (!IsEditableToken(token)) continue;
                    if (word.WordIndex < minWord) minWord = word.WordIndex;
                    if (word.WordIndex > maxWord) maxWord = word.WordIndex;
                }

                if (minWord <= maxWord && minWord >= 0 && maxWord <= 6)
                {
                    parameters = new List<TelecommandParameter>
                    {
                        new TelecommandParameter
                        {
                            Name = $"W{minWord}-W{maxWord}",
                            StartByte = "W" + (6 + minWord).ToString(CultureInfo.InvariantCulture),
                            StartBit = "",
                            Length = maxWord - minWord + 1,
                            InputType = "TextBox",
                            DataFormat = "Decimal",
                            DefaultValue = "0",
                            OptionValues = "",
                            ValueRange = range,
                            Unit = unit,
                            Remark = remark
                        }
                    };
                }
            }

            return parameters;
        }

        private TelecommandType GuessType(string name, string paramDesc)
        {
            if (name.Contains("遥测请求")) return TelecommandType.TelemetryRequest;
            if (name.Contains("复位")) return TelecommandType.Reset;
            if (paramDesc.Contains("W") && CountWordRefs(paramDesc) > 7) return TelecommandType.Long;
            if (paramDesc.Contains("W")) return TelecommandType.Short;
            return TelecommandType.Short;
        }

        private int CountWordRefs(string text)
        {
            return Regex.Matches(text ?? "", @"W\d+", RegexOptions.IgnoreCase).Count;
        }

        private int DetectHeaderRow(RawTable table, params string[] keywords)
        {
            int scanRows = table.RowCount >= 4 ? 4 : table.RowCount;
            for (int r = 0; r < scanRows; r++)
            {
                int matched = 0;
                for (int k = 0; k < keywords.Length; k++)
                {
                    if (RowContainsKeyword(table, r, keywords[k]))
                        matched++;
                }
                if (matched >= keywords.Length - 1)
                    return r;
            }
            return 0;
        }

        private bool RowContainsKeyword(RawTable table, int row, string keyword)
        {
            for (int c = 0; c < table.ColCount; c++)
            {
                if (table.GetValue(row, c).Trim().Contains(keyword))
                    return true;
            }
            return false;
        }

        private int FindColumn(RawTable table, int headerRow, params string[] keywords)
        {
            for (int c = 0; c < table.ColCount; c++)
            {
                string val = table.GetValue(headerRow, c).Trim();
                if (keywords.Any(k => val.Contains(k)))
                    return c;
            }
            return -1;
        }

        private bool TryParseHexByte(string token, out byte value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            token = token.Trim();
            Match m = HexByteRx.Match(token);
            if (m.Success)
                return byte.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

            token = token.Replace("H", "").Replace("h", "");
            if (token.Length == 2 && byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return true;

            return false;
        }

        private bool TryParseBinaryField(string token, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            token = token.Trim();
            var m = DigitRx.Match(token);
            if (!m.Success)
                return false;

            string bits = m.Value;
            if (bits.Any(ch => ch != '0' && ch != '1'))
                return false;

            try
            {
                value = Convert.ToInt32(bits, 2);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string NormalizeHexCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "";

            if (TryParseHexByte(code, out byte val))
                return "0x" + val.ToString("X2", CultureInfo.InvariantCulture);

            return code.Trim();
        }

        private bool IsEditableToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            string t = token.Trim();
            if (t == "值" || t == "value")
                return true;

            if (t.Contains("值"))
                return true;

            return false;
        }

        private string ExtractRange(string remark)
        {
            if (string.IsNullOrWhiteSpace(remark))
                return "";

            Match m = RangeRx.Match(remark);
            if (!m.Success)
                return "";

            string r = m.Groups[1].Value.Replace("～", "-").Replace("~", "-");
            return r;
        }

        private string ExtractUnit(string remark)
        {
            if (string.IsNullOrWhiteSpace(remark))
                return "";

            Match m = UnitRx.Match(remark);
            if (!m.Success)
                return "";

            return m.Groups[1].Value.Trim();
        }

        private string ExtractChannel(string channelText, int lt)
        {
            if (!string.IsNullOrEmpty(channelText))
            {
                if (channelText.Contains("A")) return "A通道";
                if (channelText.Contains("B")) return "B通道";
            }
            return lt == 0 ? "A通道" : "B通道";
        }
    }
}
