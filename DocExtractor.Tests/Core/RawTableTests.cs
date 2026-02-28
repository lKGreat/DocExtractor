using System.Collections.Generic;
using DocExtractor.Core.Models;
using Xunit;

namespace DocExtractor.Tests.Core
{
    public class RawTableTests
    {
        [Fact]
        public void GetValue_ShouldReturnMasterValue_ForMasterCell()
        {
            var table = BuildTable();
            Assert.Equal("A", table.GetValue(0, 0));
        }

        [Fact]
        public void GetValue_ShouldFollowMaster_ForShadowCell()
        {
            var table = BuildTable();
            Assert.Equal("A", table.GetValue(0, 1));
        }

        [Fact]
        public void GetValue_ShouldReturnEmpty_WhenOutOfRange()
        {
            var table = BuildTable();
            Assert.Equal(string.Empty, table.GetValue(99, 0));
        }

        [Fact]
        public void IsEmpty_ShouldBeTrue_WhenNoRowsOrCols()
        {
            var table = new RawTable { RowCount = 0, ColCount = 0 };
            Assert.True(table.IsEmpty);
        }

        private static RawTable BuildTable()
        {
            return new RawTable
            {
                RowCount = 1,
                ColCount = 2,
                Cells = new List<List<TableCell>>
                {
                    new List<TableCell>
                    {
                        new TableCell
                        {
                            RowIndex = 0,
                            ColIndex = 0,
                            Value = "A",
                            IsMasterCell = true,
                            MasterRow = 0,
                            MasterCol = 0
                        },
                        new TableCell
                        {
                            RowIndex = 0,
                            ColIndex = 1,
                            Value = string.Empty,
                            IsMasterCell = false,
                            MasterRow = 0,
                            MasterCol = 0
                        }
                    }
                }
            };
        }
    }
}
