using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Models;
using DocExtractor.Core.Splitting;
using Xunit;

namespace DocExtractor.Tests.Splitting
{
    public class SplitterTests
    {
        [Fact]
        public void MultiValueSplitter_ShouldSplitByDelimiter()
        {
            var splitter = new MultiValueSplitter();
            var rule = new SplitRule
            {
                Type = SplitType.MultiValueSplit,
                TriggerColumn = "EnumMap",
                Delimiters = new List<string> { "/" }
            };

            var record = new ExtractedRecord();
            record.Fields["EnumMap"] = "A/B/C";

            var output = splitter.Split(record, rule).ToList();

            Assert.Equal(3, output.Count);
            Assert.Equal("A", output[0].Fields["EnumMap"]);
            Assert.Equal("B", output[1].Fields["EnumMap"]);
            Assert.Equal("C", output[2].Fields["EnumMap"]);
        }

        [Fact]
        public void GroupConditionSplitter_ShouldSetGroupKey()
        {
            var splitter = new GroupConditionSplitter();
            var rule = new SplitRule
            {
                Type = SplitType.GroupConditionSplit,
                GroupByColumn = "GroupName"
            };

            var record = new ExtractedRecord();
            record.Fields["GroupName"] = "组A";

            var output = splitter.Split(record, rule).Single();
            Assert.Equal("组A", output.Fields["__GroupKey__"]);
        }

        [Fact]
        public void SubTableExpander_ShouldExpandRows()
        {
            var splitter = new SubTableExpander();
            var rule = new SplitRule
            {
                Type = SplitType.SubTableExpand,
                TriggerColumn = "EnumMap",
                InheritParentFields = true
            };

            var record = new ExtractedRecord();
            record.Fields["EnumMap"] = "0:关闭\n1:开启";
            record.Fields["ItemName"] = "状态";

            var output = splitter.Split(record, rule).ToList();

            Assert.Equal(2, output.Count);
            Assert.Equal("0", output[0].Fields["EnumMap_Key"]);
            Assert.Equal("关闭", output[0].Fields["EnumMap_Value"]);
            Assert.Equal("1", output[1].Fields["EnumMap_Key"]);
            Assert.Equal("开启", output[1].Fields["EnumMap_Value"]);
        }

        [Fact]
        public void MergedCellExpander_ShouldPassThroughRecord()
        {
            var splitter = new MergedCellExpander();
            var rule = new SplitRule { Type = SplitType.MergedCellExpand };
            var record = new ExtractedRecord();

            var output = splitter.Split(record, rule).Single();
            Assert.Same(record, output);
        }
    }
}
