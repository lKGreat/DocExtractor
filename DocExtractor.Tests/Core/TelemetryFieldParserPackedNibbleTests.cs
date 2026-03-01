using System.Collections.Generic;
using DocExtractor.Core.Models;
using DocExtractor.Core.Protocol;
using Xunit;

namespace DocExtractor.Tests.Core
{
    public class TelemetryFieldParserPackedNibbleTests
    {
        [Fact]
        public void Parse_ShouldSplitPackedNibble_WhenRemarksDescribeHighLowNibble()
        {
            var table = BuildTable();
            var parser = new TelemetryFieldParser();

            var fields = parser.Parse(table);

            Assert.Equal(2, fields.Count);

            Assert.Equal("WD5", fields[0].ByteSequence);
            Assert.Equal(4, fields[0].BitOffset);
            Assert.Equal(4, fields[0].BitLength);
            Assert.Equal("b7-b4:正确指令计数", fields[0].FieldName);

            Assert.Equal("WD5", fields[1].ByteSequence);
            Assert.Equal(0, fields[1].BitOffset);
            Assert.Equal(4, fields[1].BitLength);
            Assert.Equal("b3-b0:错误指令计数", fields[1].FieldName);
        }

        [Fact]
        public void Parse_ShouldSplitPackedNibble_WhenStartBitAndSlashNameSuggestNibble()
        {
            var table = BuildTable_StartBitSlashName();
            var parser = new TelemetryFieldParser();

            var fields = parser.Parse(table);

            Assert.Equal(2, fields.Count);
            Assert.Equal("WD5", fields[0].ByteSequence);
            Assert.Equal(4, fields[0].BitOffset);
            Assert.Equal(4, fields[0].BitLength);
            Assert.Equal("b7-b4:正确指令计数", fields[0].FieldName);

            Assert.Equal("WD5", fields[1].ByteSequence);
            Assert.Equal(0, fields[1].BitOffset);
            Assert.Equal(4, fields[1].BitLength);
            Assert.Equal("b3-b0:错误指令计数", fields[1].FieldName);
        }

        private static RawTable BuildTable()
        {
            // Columns: 字序 | 数据内容 | 字节长度 | 备注
            var rows = new List<List<TableCell>>();

            rows.Add(Row(0, "字序", "数据内容", "字节长度", "备注"));
            rows.Add(Row(1, "W5", "正确指令计数/错误指令计数", "1", "高四位是正确指令计数，低四位是错误指令计数"));

            return new RawTable
            {
                RowCount = rows.Count,
                ColCount = 4,
                Cells = rows
            };
        }

        private static RawTable BuildTable_StartBitSlashName()
        {
            // Columns: 字序 | 起始位 | 字节长度/位长度 | 数据内容
            var rows = new List<List<TableCell>>();
            rows.Add(Row4(0, "字序", "起始位", "字节长度/位长度", "数据内容"));
            rows.Add(Row4(1, "W5", "4", "4", "正确指令计数/错误指令计数"));

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
                Cell(r, 3, c3),
            };
        }

        private static List<TableCell> Row4(int r, string c0, string c1, string c2, string c3)
        {
            return new List<TableCell>
            {
                Cell(r, 0, c0),
                Cell(r, 1, c1),
                Cell(r, 2, c2),
                Cell(r, 3, c3),
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

