using System.Collections.Generic;

namespace DocExtractor.Core.Protocol
{
    public enum TelecommandType
    {
        Short,
        Long,
        Reset,
        TelemetryRequest,
        Unknown
    }

    public enum TelecommandTableType
    {
        Unknown,
        CommandSummary,
        ParameterDetail,
        CanIdSummary,
        CommandFrameFormat,
        DataTypeDefinition
    }

    public class DetectedTelecommandTable
    {
        public TelecommandTableType Type { get; set; } = TelecommandTableType.Unknown;
        public string SectionHeading { get; set; } = "";
        public string TableTitle { get; set; } = "";
        public int SourceTableIndex { get; set; }
    }

    public class TelecommandDetectionResult
    {
        public List<DetectedTelecommandTable> Tables { get; set; } = new List<DetectedTelecommandTable>();
    }

    public class TelecommandParameter
    {
        public string ParameterId { get; set; } = "";
        public string Name { get; set; } = "";
        public string StartByte { get; set; } = "";
        public string StartBit { get; set; } = "";
        public int Length { get; set; }
        public string InputType { get; set; } = "";
        public string DataFormat { get; set; } = "";
        public string DefaultValue { get; set; } = "";
        public string OptionValues { get; set; } = "";
        public string ValueRange { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Remark { get; set; } = "";
    }

    public class TelecommandPreset
    {
        public string Name { get; set; } = "";
        public string Remark { get; set; } = "";
        public byte[] ParameterBytes { get; set; } = new byte[7];
    }

    public class TelecommandEntry
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public byte CommandCode { get; set; }
        public string ParamDesc { get; set; } = "";
        public string Remark { get; set; } = "";
        public TelecommandType Type { get; set; } = TelecommandType.Unknown;
        public string CodeAlias { get; set; } = "";
        public byte[] DefaultParameterBytes { get; set; } = new byte[7];
        public List<TelecommandParameter> Parameters { get; set; } = new List<TelecommandParameter>();
        public List<TelecommandPreset> Presets { get; set; } = new List<TelecommandPreset>();
    }

    public class CanFrameInfo
    {
        public string FrameType { get; set; } = "";
        public string Channel { get; set; } = "";
        public int Priority { get; set; }
        public int BusFlag { get; set; }
        public int DataType { get; set; }
        public int DestAddr { get; set; }
        public int SrcAddr { get; set; }
        public int FrameFlag { get; set; }
        public int FrameCount { get; set; }
        public byte[] HeaderBytes { get; set; } = new byte[5];
    }

    public class TelecommandParseResult
    {
        public string SystemName { get; set; } = "";
        public string DocumentTitle { get; set; } = "";
        public string DefaultEndianness { get; set; } = "大端";
        public List<TelecommandEntry> Commands { get; set; } = new List<TelecommandEntry>();
        public List<CanFrameInfo> FrameInfos { get; set; } = new List<CanFrameInfo>();
        public List<DetectedTelecommandTable> DetectedTables { get; set; } = new List<DetectedTelecommandTable>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
