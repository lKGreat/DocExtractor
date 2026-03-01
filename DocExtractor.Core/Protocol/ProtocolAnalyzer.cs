using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Protocol
{
    /// <summary>
    /// Orchestrates protocol document analysis: detects telemetry tables,
    /// parses fields, extracts channel info, and produces a complete result.
    /// Works on already-parsed RawTable arrays (no OpenXml dependency).
    /// </summary>
    public class ProtocolAnalyzer
    {
        private readonly TelemetryTableDetector _detector = new TelemetryTableDetector();
        private readonly TelemetryFieldParser _parser = new TelemetryFieldParser();

        /// <summary>
        /// Analyze parsed tables and document metadata to produce a complete telemetry extraction result.
        /// </summary>
        /// <param name="tables">All tables parsed from the document</param>
        /// <param name="documentTitle">Document title (from file name or title page)</param>
        /// <param name="paragraphTexts">Paragraph texts from the document for metadata extraction</param>
        public ProtocolParseResult Analyze(
            IReadOnlyList<RawTable> tables,
            string documentTitle,
            IReadOnlyList<string>? paragraphTexts = null)
        {
            var result = new ProtocolParseResult
            {
                DocumentTitle = documentTitle
            };

            result.SystemName = InferSystemName(documentTitle, paragraphTexts);
            result.DefaultEndianness = InferEndianness(paragraphTexts);

            var detection = _detector.Detect(tables);
            result.AllDetectedTables = detection.TelemetryTables;

            foreach (var detected in detection.TelemetryTables)
            {
                if (detected.SourceTableIndex >= tables.Count) continue;
                var rawTable = tables[detected.SourceTableIndex];
                detected.Fields = _parser.Parse(rawTable);

                switch (detected.Type)
                {
                    case TelemetryType.Sync:
                        result.SyncTables.Add(detected);
                        break;
                    case TelemetryType.Async:
                        result.AsyncTables.Add(detected);
                        break;
                    default:
                        result.SyncTables.Add(detected);
                        result.Warnings.Add(
                            $"表格 '{detected.TableTitle}' 类型未确定，默认归类为同步遥测");
                        break;
                }
            }

            result.SyncChannels = _detector.ExtractChannelInfo(
                tables, detection.CanIdSummaryTableIndices, TelemetryType.Sync);
            result.AsyncChannels = _detector.ExtractChannelInfo(
                tables, detection.CanIdSummaryTableIndices, TelemetryType.Async);

            if (result.SyncChannels.Count == 0)
                GenerateDefaultChannels(result.SyncChannels);
            if (result.AsyncChannels.Count == 0)
                GenerateDefaultChannels(result.AsyncChannels);

            if (result.SyncTables.Count == 0 && result.AsyncTables.Count == 0)
                result.Warnings.Add("未检测到遥测定义表格，请检查文档格式是否符合协议规范");

            return result;
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
                { "飞轮", "RW" },
                { "磁力矩", "MTQ" },
                { "太阳敏感器", "SS" },
                { "星敏", "STR" },
                { "GNSS", "GNSS" },
                { "GPS", "GPS" },
            };

            string searchText = title ?? "";
            if (paragraphs != null)
            {
                int limit = Math.Min(paragraphs.Count, 30);
                for (int i = 0; i < limit; i++)
                    searchText += " " + paragraphs[i];
            }

            foreach (var kvp in knownSystems)
            {
                if (searchText.Contains(kvp.Key))
                    return kvp.Value;
            }

            var abbrevMatch = Regex.Match(title ?? "",
                @"([A-Z]{2,6})\s*(GEN|V|v|组件|单机|系统)");
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

        private void GenerateDefaultChannels(List<ChannelInfo> channels)
        {
            channels.Add(new ChannelInfo
            {
                ChannelLabel = "A通道",
                FrameIdHex = "",
                FrameCount = 0
            });
            channels.Add(new ChannelInfo
            {
                ChannelLabel = "B通道",
                FrameIdHex = "",
                FrameCount = 0
            });
        }
    }
}
