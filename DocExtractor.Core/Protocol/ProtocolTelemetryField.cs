using System.Collections.Generic;

namespace DocExtractor.Core.Protocol
{
    /// <summary>
    /// Represents a single telemetry field parsed from a protocol document table.
    /// Each row in a telemetry definition table (字序/数据内容/字节长度/备注) maps to one instance.
    /// </summary>
    public class ProtocolTelemetryField
    {
        /// <summary>Raw byte-sequence notation from the document (e.g. "W0", "W7-W8", "Dh")</summary>
        public string ByteSequence { get; set; } = "";

        /// <summary>Field / channel name (数据内容 column)</summary>
        public string FieldName { get; set; } = "";

        /// <summary>Byte length as declared in the table</summary>
        public int ByteLength { get; set; }

        /// <summary>Bit offset within the byte (for sub-byte fields like b7-b4); -1 if not applicable</summary>
        public int BitOffset { get; set; } = -1;

        /// <summary>Bit length for sub-byte fields; 0 if full-byte</summary>
        public int BitLength { get; set; }

        /// <summary>Raw remarks text from the document</summary>
        public string Remarks { get; set; } = "";

        /// <summary>Physical unit extracted from remarks (V, A, mA, MPa, etc.)</summary>
        public string Unit { get; set; } = "";

        /// <summary>Enum mapping extracted from remarks (e.g. "0x55-点火未成功|0xAA-点火成功")</summary>
        public string EnumMapping { get; set; } = "";

        /// <summary>Whether this field is a header/length field (Dh, Dl) rather than data</summary>
        public bool IsHeaderField { get; set; }

        /// <summary>Whether this is the checksum field (SUM)</summary>
        public bool IsChecksum { get; set; }

        /// <summary>Whether this is a reserved/padding field</summary>
        public bool IsReserved { get; set; }

        /// <summary>Data type hint extracted from remarks (UINT16, UINT8, etc.)</summary>
        public string DataTypeHint { get; set; } = "";
    }

    /// <summary>
    /// Telemetry type classification
    /// </summary>
    public enum TelemetryType
    {
        Sync,
        Async,
        Unknown
    }

    /// <summary>
    /// A detected telemetry table with its classification and parsed fields.
    /// </summary>
    public class DetectedTelemetryTable
    {
        public TelemetryType Type { get; set; } = TelemetryType.Unknown;

        /// <summary>Section heading under which this table was found</summary>
        public string SectionHeading { get; set; } = "";

        /// <summary>Table title (caption preceding the table)</summary>
        public string TableTitle { get; set; } = "";

        /// <summary>Index in the original RawTable array</summary>
        public int SourceTableIndex { get; set; }

        /// <summary>Parsed telemetry fields from this table</summary>
        public List<ProtocolTelemetryField> Fields { get; set; } = new List<ProtocolTelemetryField>();
    }

    /// <summary>
    /// CAN channel information (APID, frame IDs) extracted from summary tables.
    /// </summary>
    public class ChannelInfo
    {
        /// <summary>Channel label ("A通道" or "B通道")</summary>
        public string ChannelLabel { get; set; } = "";

        /// <summary>Computed CAN frame ID in hex (used as APID value)</summary>
        public string FrameIdHex { get; set; } = "";

        /// <summary>Number of CAN frames in the composite message</summary>
        public int FrameCount { get; set; }
    }
}
