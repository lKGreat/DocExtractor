using System;
using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Protocol
{
    /// <summary>
    /// Heuristic detector that identifies telemetry definition tables within a set of
    /// parsed RawTables and classifies them as sync or async telemetry.
    /// Designed for satellite communication protocol documents with varying table formats.
    /// </summary>
    public class TelemetryTableDetector
    {
        private static readonly string[][] HeaderPatterns = new[]
        {
            new[] { "字序", "数据内容", "字节长度" },
            new[] { "字序", "数据内容", "长度" },
            new[] { "字段", "名称", "字节长度" },
            new[] { "偏移", "名称", "长度" },
            new[] { "序号", "参数名称", "字节数" },
            new[] { "字节", "内容", "长度" },
        };

        private static readonly string[] SyncKeywords =
            { "遥测数据返回", "遥测应答", "同步遥测", "遥测数据详细", "遥测包" };

        private static readonly string[] AsyncKeywords =
            { "异步遥测", "异步数据返回", "异步遥测请求命令应答" };

        private static readonly string[] CanIdSummaryHeaders =
            { "ID28", "P", "LT", "DT", "DA", "SA", "FT", "FC" };

        /// <summary>
        /// Detect all telemetry definition tables and CAN ID summary tables from the document.
        /// </summary>
        public DetectionResult Detect(IReadOnlyList<RawTable> tables)
        {
            var result = new DetectionResult();

            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                if (table.IsEmpty || table.RowCount < 3) continue;

                if (IsTelemetryDefinitionTable(table))
                {
                    var detected = new DetectedTelemetryTable
                    {
                        SourceTableIndex = i,
                        SectionHeading = table.SectionHeading ?? "",
                        TableTitle = table.Title ?? "",
                        Type = ClassifyTelemetryType(table)
                    };
                    result.TelemetryTables.Add(detected);
                }
                else if (IsCanIdSummaryTable(table))
                {
                    result.CanIdSummaryTableIndices.Add(i);
                }
            }

            ResolveAmbiguousTypes(result);
            return result;
        }

        /// <summary>
        /// Extract channel (A/B) APID information from CAN ID summary tables.
        /// </summary>
        public List<ChannelInfo> ExtractChannelInfo(
            IReadOnlyList<RawTable> tables,
            IReadOnlyList<int> summaryIndices,
            TelemetryType targetType)
        {
            var channels = new List<ChannelInfo>();
            string typeKeyword = targetType == TelemetryType.Async ? "异步" : "遥测";
            string responseKeyword = targetType == TelemetryType.Async ? "异步遥测返回" : "遥测返回";

            foreach (int idx in summaryIndices)
            {
                if (idx >= tables.Count) continue;
                var table = tables[idx];

                int cmdCol = FindColumnIndex(table, 0, "命令");
                int lastCol = table.ColCount - 1;
                if (cmdCol < 0) continue;

                for (int r = 1; r < table.RowCount; r++)
                {
                    string cmdText = table.GetValue(r, cmdCol).Trim();
                    if (string.IsNullOrEmpty(cmdText)) continue;

                    bool isMatch = false;
                    if (targetType == TelemetryType.Async)
                        isMatch = cmdText.Contains("异步") && cmdText.Contains("返回");
                    else
                        isMatch = (cmdText.Contains("遥测") && cmdText.Contains("返回"))
                                  && !cmdText.Contains("异步");

                    if (!isMatch) continue;

                    string channelLabel = table.GetValue(r, lastCol).Trim();
                    if (string.IsNullOrEmpty(channelLabel)) continue;
                    int slashIdx = channelLabel.IndexOf('/');
                    if (slashIdx > 0) channelLabel = channelLabel.Substring(0, slashIdx);

                    string frameId = BuildFrameId(table, r);
                    if (!string.IsNullOrEmpty(frameId))
                    {
                        channels.Add(new ChannelInfo
                        {
                            ChannelLabel = channelLabel,
                            FrameIdHex = frameId
                        });
                    }
                }
            }

            return channels;
        }

        private bool IsTelemetryDefinitionTable(RawTable table)
        {
            var headerValues = new List<string>();
            int headerRows = Math.Min(2, table.RowCount);
            for (int r = 0; r < headerRows; r++)
            {
                for (int c = 0; c < table.ColCount; c++)
                    headerValues.Add(table.GetValue(r, c).Trim());
            }

            foreach (var pattern in HeaderPatterns)
            {
                int matched = 0;
                foreach (string keyword in pattern)
                {
                    if (headerValues.Any(h => h.Contains(keyword)))
                        matched++;
                }
                if (matched >= pattern.Length)
                    return true;
            }

            if (HasByteSequenceColumn(table))
                return true;

            return false;
        }

        private bool HasByteSequenceColumn(RawTable table)
        {
            int checkRows = Math.Min(10, table.RowCount);
            int byteSeqCount = 0;

            for (int r = 1; r < checkRows; r++)
            {
                string val = table.GetValue(r, 0).Trim();
                if (IsByteSequenceValue(val))
                    byteSeqCount++;
            }

            return byteSeqCount >= 3;
        }

        private bool IsByteSequenceValue(string val)
        {
            if (string.IsNullOrEmpty(val)) return false;
            val = val.Trim();
            if (val.StartsWith("W", StringComparison.OrdinalIgnoreCase) && val.Length > 1)
            {
                string rest = val.Substring(1).Replace("-", "").Replace("W", "").Replace("w", "");
                int dummy;
                return rest.Length > 0 && int.TryParse(rest.Substring(0, 1), out dummy);
            }
            if (val == "Dh" || val == "Dl" || val == "SUM" || val == "Sum")
                return true;
            if (val.StartsWith("B", StringComparison.OrdinalIgnoreCase) && val.Length > 1)
            {
                int dummy;
                return int.TryParse(val.Substring(1, 1), out dummy);
            }
            return false;
        }

        private TelemetryType ClassifyTelemetryType(RawTable table)
        {
            string context = (table.SectionHeading ?? "") + " " + (table.Title ?? "");

            foreach (string kw in AsyncKeywords)
            {
                if (context.Contains(kw))
                    return TelemetryType.Async;
            }

            foreach (string kw in SyncKeywords)
            {
                if (context.Contains(kw))
                    return TelemetryType.Sync;
            }

            return TelemetryType.Unknown;
        }

        private void ResolveAmbiguousTypes(DetectionResult result)
        {
            var unknown = result.TelemetryTables.Where(t => t.Type == TelemetryType.Unknown).ToList();
            if (unknown.Count == 0) return;

            bool hasSync = result.TelemetryTables.Any(t => t.Type == TelemetryType.Sync);
            bool hasAsync = result.TelemetryTables.Any(t => t.Type == TelemetryType.Async);

            foreach (var t in unknown)
            {
                if (!hasSync) { t.Type = TelemetryType.Sync; hasSync = true; }
                else if (!hasAsync) { t.Type = TelemetryType.Async; hasAsync = true; }
                else { t.Type = TelemetryType.Sync; }
            }
        }

        private bool IsCanIdSummaryTable(RawTable table)
        {
            if (table.ColCount < 6) return false;

            var headerValues = new List<string>();
            int headerRows = Math.Min(3, table.RowCount);
            for (int r = 0; r < headerRows; r++)
            {
                for (int c = 0; c < table.ColCount; c++)
                    headerValues.Add(table.GetValue(r, c).Trim());
            }

            int matched = 0;
            foreach (string kw in CanIdSummaryHeaders)
            {
                if (headerValues.Any(h => h == kw || h.Contains(kw)))
                    matched++;
            }

            return matched >= 5;
        }

        private string BuildFrameId(RawTable table, int row)
        {
            var bits = new List<string>();
            for (int c = 1; c < table.ColCount - 1; c++)
            {
                string val = table.GetValue(row, c).Trim();
                if (!string.IsNullOrEmpty(val) && val.Length <= 10 &&
                    IsBinaryLike(val))
                {
                    bits.Add(val);
                }
            }

            if (bits.Count == 0) return "";

            string combined = string.Join("", bits);
            combined = combined.Replace(" ", "");

            if (combined.Length < 8) return "";

            try
            {
                long frameIdValue = Convert.ToInt64(combined, 2);
                return frameIdValue.ToString("X10");
            }
            catch
            {
                return "";
            }
        }

        private bool IsBinaryLike(string val)
        {
            foreach (char c in val)
            {
                if (c != '0' && c != '1' && c != ' ')
                    return false;
            }
            return true;
        }

        private int FindColumnIndex(RawTable table, int headerRow, string keyword)
        {
            for (int c = 0; c < table.ColCount; c++)
            {
                if (table.GetValue(headerRow, c).Trim().Contains(keyword))
                    return c;
            }
            return -1;
        }
    }

    /// <summary>
    /// Result from the table detection phase.
    /// </summary>
    public class DetectionResult
    {
        public List<DetectedTelemetryTable> TelemetryTables { get; set; } = new List<DetectedTelemetryTable>();
        public List<int> CanIdSummaryTableIndices { get; set; } = new List<int>();
    }
}
