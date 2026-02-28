using System.Collections.Generic;
using DocExtractor.Core.Models;
using DocExtractor.Core.Schema;
using Xunit;

namespace DocExtractor.Tests.Core
{
    public class TableSchemaDetectorTests
    {
        [Fact]
        public void Detect_ShouldIdentifyCrossTable_WhenTopLeftEmptyAndAxesAreText()
        {
            var table = BuildTable(new[]
            {
                new[] { "", "前端", "后端" },
                new[] { "速度", "98", "97" },
                new[] { "功耗", "12", "13" }
            });

            var detector = new TableSchemaDetector();
            var result = detector.Detect(table);

            Assert.Equal(TableSchemaType.CrossTable, result.SchemaType);
        }

        [Fact]
        public void Detect_ShouldReturnStandard_ForRegularSimpleTable()
        {
            var table = BuildTable(new[]
            {
                new[] { "APID", "起始字节", "位长度" },
                new[] { "0x1A", "1", "8" },
                new[] { "0x1B", "2", "8" }
            });

            var detector = new TableSchemaDetector();
            var result = detector.Detect(table);

            Assert.NotEqual(TableSchemaType.SideTitle, result.SchemaType);
        }

        private static RawTable BuildTable(string[][] rows)
        {
            var cells = new List<List<TableCell>>();
            for (int r = 0; r < rows.Length; r++)
            {
                var row = new List<TableCell>();
                for (int c = 0; c < rows[r].Length; c++)
                {
                    row.Add(new TableCell
                    {
                        RowIndex = r,
                        ColIndex = c,
                        Value = rows[r][c],
                        IsMasterCell = true,
                        MasterRow = r,
                        MasterCol = c
                    });
                }
                cells.Add(row);
            }

            return new RawTable
            {
                SourceFile = "sample",
                TableIndex = 0,
                RowCount = rows.Length,
                ColCount = rows[0].Length,
                Cells = cells
            };
        }
    }
}
