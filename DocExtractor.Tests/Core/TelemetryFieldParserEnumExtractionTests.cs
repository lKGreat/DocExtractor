using System.Collections.Generic;
using DocExtractor.Core.Models;
using DocExtractor.Core.Protocol;
using Xunit;

namespace DocExtractor.Tests.Core
{
    public class TelemetryFieldParserEnumExtractionTests
    {
        [Fact]
        public void Parse_ShouldExtractHexListFromFieldName_WhenRemarksEmpty()
        {
            var parser = new TelemetryFieldParser();
            var table = BuildTable(
                "W5",
                "b7-b4：辅助电源状态（||-6子批次后配置该功能） 0x01/0x02/0x03",
                "1",
                "");

            var fields = parser.Parse(table);

            Assert.Single(fields);
            var field = fields[0];
            Assert.Equal(4, field.BitOffset);
            Assert.Equal(4, field.BitLength);
            Assert.Contains("辅助电源状态", field.FieldName);
            Assert.Equal("0x01-0x01|0x02-0x02|0x03-0x03", field.EnumMapping);
            Assert.Equal(3, field.EnumEntries.Count);
            Assert.Equal("0x01", field.EnumEntries[0].Value);
            Assert.Equal("0x02", field.EnumEntries[1].Value);
            Assert.Equal("0x03", field.EnumEntries[2].Value);
        }

        [Fact]
        public void Parse_ShouldKeepPairExtraction_WhenRemarksContainDescriptions()
        {
            var parser = new TelemetryFieldParser();
            var table = BuildTable(
                "W5",
                "b7-b4：辅助电源状态",
                "1",
                "0x00:正常|0x01：A正常，B异常|0x02：A异常，B正常|0x03：AB均异常");

            var fields = parser.Parse(table);

            Assert.Single(fields);
            var field = fields[0];
            Assert.Contains("0x01-A正常，B异常", field.EnumMapping);
            Assert.Contains("0x02-A异常，B正常", field.EnumMapping);
            Assert.Contains("0x03-AB均异常", field.EnumMapping);
            Assert.True(field.EnumEntries.Count >= 3);
        }

        private static RawTable BuildTable(string seq, string name, string len, string remarks)
        {
            var rows = new List<List<TableCell>>
            {
                Row(0, "字序", "数据内容", "字节长度", "备注"),
                Row(1, seq, name, len, remarks)
            };

            return new RawTable
            {
                RowCount = rows.Count,
                ColCount = 4,
                Cells = rows
            };
        }

        private static List<TableCell> Row(int r, string c0, string c1, string c2, string c3)
        {
            return new List<TableCell>
            {
                Cell(r, 0, c0),
                Cell(r, 1, c1),
                Cell(r, 2, c2),
                Cell(r, 3, c3)
            };
        }

        private static TableCell Cell(int r, int c, string v)
        {
            return new TableCell
            {
                RowIndex = r,
                ColIndex = c,
                Value = v,
                IsMasterCell = true,
                MasterRow = r,
                MasterCol = c
            };
        }
    }
}
