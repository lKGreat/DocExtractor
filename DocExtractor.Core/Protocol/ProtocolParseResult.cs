using System.Collections.Generic;

namespace DocExtractor.Core.Protocol
{
    /// <summary>
    /// Complete result of parsing a satellite communication protocol document
    /// for telemetry configuration extraction.
    /// </summary>
    public class ProtocolParseResult
    {
        /// <summary>System/subsystem name extracted from the document (e.g. "PPU")</summary>
        public string SystemName { get; set; } = "";

        /// <summary>Document title</summary>
        public string DocumentTitle { get; set; } = "";

        /// <summary>Default byte order stated in the protocol (e.g. "大端")</summary>
        public string DefaultEndianness { get; set; } = "大端";

        /// <summary>Detected synchronous telemetry tables with parsed fields</summary>
        public List<DetectedTelemetryTable> SyncTables { get; set; } = new List<DetectedTelemetryTable>();

        /// <summary>Detected asynchronous telemetry tables with parsed fields</summary>
        public List<DetectedTelemetryTable> AsyncTables { get; set; } = new List<DetectedTelemetryTable>();

        /// <summary>Channel info per A/B channel (APID values, frame counts)</summary>
        public List<ChannelInfo> SyncChannels { get; set; } = new List<ChannelInfo>();

        /// <summary>Channel info for async telemetry</summary>
        public List<ChannelInfo> AsyncChannels { get; set; } = new List<ChannelInfo>();

        /// <summary>All detected telemetry tables (including unclassified)</summary>
        public List<DetectedTelemetryTable> AllDetectedTables { get; set; } = new List<DetectedTelemetryTable>();

        /// <summary>Warnings or notes generated during parsing</summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>Total sync field count across all sync tables</summary>
        public int SyncFieldCount
        {
            get
            {
                int count = 0;
                foreach (var t in SyncTables)
                    count += t.Fields.Count;
                return count;
            }
        }

        /// <summary>Total async field count across all async tables</summary>
        public int AsyncFieldCount
        {
            get
            {
                int count = 0;
                foreach (var t in AsyncTables)
                    count += t.Fields.Count;
                return count;
            }
        }
    }
}
