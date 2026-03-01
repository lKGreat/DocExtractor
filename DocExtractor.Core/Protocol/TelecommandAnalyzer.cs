using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Protocol
{
    /// <summary>
    /// Analyzes protocol tables for telecommand extraction.
    /// </summary>
    public class TelecommandAnalyzer
    {
        private readonly TelecommandTableDetector _detector = new TelecommandTableDetector();
        private readonly TelecommandFieldParser _parser = new TelecommandFieldParser();

        public TelecommandParseResult Analyze(
            IReadOnlyList<RawTable> tables,
            string documentTitle,
            IReadOnlyList<string>? paragraphTexts = null)
        {
            var result = new TelecommandParseResult
            {
                DocumentTitle = documentTitle,
                SystemName = InferSystemName(documentTitle, paragraphTexts),
                DefaultEndianness = InferEndianness(paragraphTexts)
            };

            TelecommandDetectionResult detection = _detector.Detect(tables);
            result.DetectedTables = detection.Tables;

            var commandMap = new Dictionary<string, TelecommandEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (DetectedTelecommandTable detected in detection.Tables)
            {
                if (detected.SourceTableIndex < 0 || detected.SourceTableIndex >= tables.Count)
                    continue;

                RawTable table = tables[detected.SourceTableIndex];
                if (detected.Type == TelecommandTableType.CommandSummary)
                {
                    MergeEntries(commandMap, _parser.ParseCommandSummaryTable(table));
                }
                else if (detected.Type == TelecommandTableType.CommandFrameFormat)
                {
                    MergeEntries(commandMap, _parser.ParseCommandFrameTable(table));
                }
                else if (detected.Type == TelecommandTableType.ParameterDetail)
                {
                    var parsed = _parser.ParseParameterDetailTable(table);
                    foreach (var item in parsed)
                    {
                        if (!TryFindByCode(commandMap, item.CommandCode, out TelecommandEntry? entry) || entry == null)
                            continue;

                        entry.Presets.Add(item.Preset);
                        foreach (TelecommandParameter p in item.Parameters)
                        {
                            if (!entry.Parameters.Any(ep =>
                                string.Equals(ep.StartByte, p.StartByte, StringComparison.OrdinalIgnoreCase) &&
                                ep.Length == p.Length))
                            {
                                entry.Parameters.Add(p);
                            }
                        }
                    }
                }
                else if (detected.Type == TelecommandTableType.CanIdSummary)
                {
                    List<CanFrameInfo> infos = _parser.ParseCanIdSummaryTable(table);
                    foreach (CanFrameInfo info in infos)
                        result.FrameInfos.Add(info);
                }
            }

            result.Commands = commandMap.Values
                .OrderBy(c => c.CommandCode)
                .ThenBy(c => c.Name)
                .ToList();

            // Ensure each command has channel headers and a default payload.
            foreach (TelecommandEntry command in result.Commands)
            {
                if (command.DefaultParameterBytes == null || command.DefaultParameterBytes.Length != 7)
                    command.DefaultParameterBytes = new byte[7];

                if (string.IsNullOrEmpty(command.CodeAlias))
                    command.CodeAlias = _parser.BuildAlias(command.Name);
            }

            if (result.Commands.Count == 0)
                result.Warnings.Add("未检测到遥控指令表，请检查文档格式或章节内容。");

            if (result.FrameInfos.Count == 0)
                result.Warnings.Add("未检测到 CAN ID 汇总表，导出时将使用默认帧头规则。");

            return result;
        }

        public static byte[] BuildFrameHeaderBytes(int p, int lt, int dt, int da, int sa, int ft, int fc)
        {
            uint canId = ((uint)p << 26)
                | ((uint)lt << 25)
                | ((uint)dt << 20)
                | ((uint)da << 15)
                | ((uint)sa << 10)
                | ((uint)ft << 8)
                | (uint)fc;

            return new[]
            {
                (byte)0x88, // IDE=1, RTR=0, DLC=8
                (byte)(canId >> 24),
                (byte)(canId >> 16),
                (byte)(canId >> 8),
                (byte)(canId & 0xFF)
            };
        }

        public byte[] BuildCommandFrame(TelecommandEntry command, bool isBChannel, List<CanFrameInfo> frameInfos)
        {
            byte[] header = ResolveHeader(command, isBChannel, frameInfos);
            var frame = new byte[13];
            Array.Copy(header, 0, frame, 0, 5);
            frame[5] = command.CommandCode;
            for (int i = 0; i < 7; i++)
                frame[6 + i] = command.DefaultParameterBytes[i];
            return frame;
        }

        public byte[] BuildCommandFrameWithPreset(TelecommandEntry command, TelecommandPreset preset, bool isBChannel, List<CanFrameInfo> frameInfos)
        {
            byte[] header = ResolveHeader(command, isBChannel, frameInfos);
            var frame = new byte[13];
            Array.Copy(header, 0, frame, 0, 5);
            frame[5] = command.CommandCode;
            for (int i = 0; i < 7; i++)
                frame[6 + i] = preset.ParameterBytes != null && i < preset.ParameterBytes.Length ? preset.ParameterBytes[i] : (byte)0x00;
            return frame;
        }

        private byte[] ResolveHeader(TelecommandEntry command, bool isBChannel, List<CanFrameInfo> frameInfos)
        {
            string channel = isBChannel ? "B通道" : "A通道";
            int dtFallback = ResolveDtFallback(command);
            byte[] fallback = BuildFrameHeaderBytes(0b011, isBChannel ? 1 : 0, dtFallback, 0b01011, 0, 0b00, 0x01);

            if (frameInfos == null || frameInfos.Count == 0)
                return fallback;

            foreach (CanFrameInfo info in frameInfos)
            {
                if (!IsSameChannel(info.Channel, channel))
                    continue;

                if (IsFrameTypeMatch(command, info.FrameType))
                    return info.HeaderBytes;
            }

            return fallback;
        }

        private int ResolveDtFallback(TelecommandEntry command)
        {
            if (command.Type == TelecommandType.TelemetryRequest)
                return 0b00000;
            if (command.Type == TelecommandType.Reset)
                return 0b00001;
            if (command.Type == TelecommandType.Long)
                return 0b00100;
            return 0b00010;
        }

        private bool IsFrameTypeMatch(TelecommandEntry command, string frameType)
        {
            string ft = frameType ?? "";
            if (command.Type == TelecommandType.TelemetryRequest)
                return ft.Contains("遥测请求");
            if (command.Type == TelecommandType.Reset)
                return ft.Contains("复位");
            if (command.Type == TelecommandType.Long)
                return ft.Contains("控制长");

            // Default short-control class
            return ft.Contains("控制短") || ft.Contains("控制指令") || ft.Contains("控制");
        }

        private bool IsSameChannel(string left, string right)
        {
            bool leftB = (left ?? "").Contains("B");
            bool rightB = (right ?? "").Contains("B");
            return leftB == rightB;
        }

        private void MergeEntries(Dictionary<string, TelecommandEntry> map, List<TelecommandEntry> incoming)
        {
            foreach (TelecommandEntry entry in incoming)
            {
                string key = entry.Code;
                if (string.IsNullOrEmpty(key))
                    key = "0x" + entry.CommandCode.ToString("X2");

                if (!map.TryGetValue(key, out TelecommandEntry? existing))
                {
                    map[key] = entry;
                    continue;
                }

                if (string.IsNullOrEmpty(existing.Name) && !string.IsNullOrEmpty(entry.Name))
                    existing.Name = entry.Name;

                if (string.IsNullOrEmpty(existing.ParamDesc) && !string.IsNullOrEmpty(entry.ParamDesc))
                    existing.ParamDesc = entry.ParamDesc;

                if (string.IsNullOrEmpty(existing.Remark) && !string.IsNullOrEmpty(entry.Remark))
                    existing.Remark = entry.Remark;

                if ((existing.DefaultParameterBytes == null || existing.DefaultParameterBytes.All(b => b == 0))
                    && entry.DefaultParameterBytes != null && entry.DefaultParameterBytes.Any(b => b != 0))
                {
                    existing.DefaultParameterBytes = entry.DefaultParameterBytes;
                }

                if (existing.Type == TelecommandType.Unknown && entry.Type != TelecommandType.Unknown)
                    existing.Type = entry.Type;

                if (string.IsNullOrEmpty(existing.CodeAlias) && !string.IsNullOrEmpty(entry.CodeAlias))
                    existing.CodeAlias = entry.CodeAlias;
            }
        }

        private bool TryFindByCode(Dictionary<string, TelecommandEntry> map, string code, out TelecommandEntry? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(code))
                return false;

            string normalized = NormalizeHexCode(code);
            if (map.TryGetValue(normalized, out TelecommandEntry? v1))
            {
                entry = v1;
                return true;
            }

            byte codeByte;
            if (!TryParseHexByte(code, out codeByte))
                return false;

            string hex = "0x" + codeByte.ToString("X2");
            if (map.TryGetValue(hex, out TelecommandEntry? v2))
            {
                entry = v2;
                return true;
            }

            return false;
        }

        private string NormalizeHexCode(string code)
        {
            if (TryParseHexByte(code, out byte value))
                return "0x" + value.ToString("X2");
            return (code ?? "").Trim();
        }

        private bool TryParseHexByte(string token, out byte value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            string t = token.Trim();
            Match m = Regex.Match(t, @"0[xX]([0-9A-Fa-f]{1,2})");
            if (m.Success)
                return byte.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out value);

            t = t.Replace("H", "").Replace("h", "");
            if (t.Length == 2)
                return byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out value);

            return false;
        }

        private string InferSystemName(string title, IReadOnlyList<string>? paragraphs)
        {
            var knownSystems = new Dictionary<string, string>
            {
                { "霍尔电推", "PPU" },
                { "PPU", "PPU" },
                { "星务", "OBC" },
                { "电源", "EPS" },
                { "姿控", "ADCS" },
                { "测控", "TTC" },
                { "数传", "DTS" },
                { "载荷", "PL" },
                { "热控", "TCS" },
                { "推进", "PROP" },
            };

            string searchText = title ?? "";
            if (paragraphs != null)
            {
                int limit = paragraphs.Count < 30 ? paragraphs.Count : 30;
                for (int i = 0; i < limit; i++)
                    searchText += " " + paragraphs[i];
            }

            foreach (var kvp in knownSystems)
            {
                if (searchText.Contains(kvp.Key))
                    return kvp.Value;
            }

            Match abbrevMatch = Regex.Match(title ?? "", @"([A-Z]{2,6})\s*(GEN|V|v|组件|单机|系统)");
            if (abbrevMatch.Success)
                return abbrevMatch.Groups[1].Value;

            return "SYS";
        }

        private string InferEndianness(IReadOnlyList<string>? paragraphs)
        {
            if (paragraphs == null) return "大端";

            foreach (string p in paragraphs)
            {
                if (p.Contains("高字节在前") || p.Contains("大端") || p.Contains("big-endian"))
                    return "大端";
                if (p.Contains("低字节在前") || p.Contains("小端") || p.Contains("little-endian"))
                    return "小端";
            }

            return "大端";
        }
    }
}
