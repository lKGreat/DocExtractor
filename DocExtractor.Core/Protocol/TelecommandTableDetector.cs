using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Protocol
{
    /// <summary>
    /// Detects telecommand-related tables from protocol raw tables.
    /// </summary>
    public class TelecommandTableDetector
    {
        private static readonly Regex WordHeaderRx = new Regex(@"^W\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public TelecommandDetectionResult Detect(IReadOnlyList<RawTable> tables)
        {
            var result = new TelecommandDetectionResult();

            for (int i = 0; i < tables.Count; i++)
            {
                RawTable table = tables[i];
                if (table.IsEmpty || table.RowCount < 2)
                    continue;

                TelecommandTableType type = DetectType(table);
                if (type == TelecommandTableType.Unknown)
                    continue;

                result.Tables.Add(new DetectedTelecommandTable
                {
                    Type = type,
                    SourceTableIndex = i,
                    SectionHeading = table.SectionHeading ?? "",
                    TableTitle = table.Title ?? ""
                });
            }

            return result;
        }

        private TelecommandTableType DetectType(RawTable table)
        {
            var headers = GetHeaderCandidates(table);

            if (IsCanIdSummary(headers))
                return TelecommandTableType.CanIdSummary;

            if (IsCommandFrameFormat(headers))
                return TelecommandTableType.CommandFrameFormat;

            if (IsCommandSummary(headers))
                return TelecommandTableType.CommandSummary;

            if (IsParameterDetail(headers))
                return TelecommandTableType.ParameterDetail;

            if (IsDataTypeDefinition(headers))
                return TelecommandTableType.DataTypeDefinition;

            return TelecommandTableType.Unknown;
        }

        private List<string> GetHeaderCandidates(RawTable table)
        {
            var headers = new List<string>();
            int rows = table.RowCount >= 3 ? 3 : table.RowCount;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < table.ColCount; c++)
                {
                    string value = table.GetValue(r, c).Trim();
                    if (value.Length > 0)
                        headers.Add(value);
                }
            }
            return headers;
        }

        private bool IsCommandSummary(List<string> headers)
        {
            return ContainsAny(headers, "指令名称")
                && ContainsAny(headers, "指令码")
                && ContainsAny(headers, "指令参数");
        }

        private bool IsParameterDetail(List<string> headers)
        {
            if (!ContainsAny(headers, "编号") || !ContainsAny(headers, "指令名称"))
                return false;

            if (!ContainsAny(headers, "指令码"))
                return false;

            return headers.Any(h => WordHeaderRx.IsMatch(h.Trim()));
        }

        private bool IsCanIdSummary(List<string> headers)
        {
            int matched = 0;
            if (ContainsAny(headers, "优先级", "P")) matched++;
            if (ContainsAny(headers, "总线标志", "LT")) matched++;
            if (ContainsAny(headers, "数据类型", "DT")) matched++;
            if (ContainsAny(headers, "目的地址", "DA")) matched++;
            if (ContainsAny(headers, "源地址", "SA")) matched++;
            if (ContainsAny(headers, "单/复帧标识", "FT")) matched++;
            if (ContainsAny(headers, "帧计数", "FC")) matched++;
            return matched >= 5;
        }

        private bool IsCommandFrameFormat(List<string> headers)
        {
            int matched = 0;
            if (ContainsAny(headers, "命令")) matched++;
            if (ContainsAny(headers, "B0")) matched++;
            if (ContainsAny(headers, "B1")) matched++;
            if (ContainsAny(headers, "B7")) matched++;
            return matched >= 4;
        }

        private bool IsDataTypeDefinition(List<string> headers)
        {
            return ContainsAny(headers, "数据类型")
                && ContainsAny(headers, "数据含义")
                && ContainsAny(headers, "DT值");
        }

        private bool ContainsAny(List<string> headers, params string[] keywords)
        {
            return headers.Any(h => keywords.Any(k => h.Contains(k)));
        }
    }
}
