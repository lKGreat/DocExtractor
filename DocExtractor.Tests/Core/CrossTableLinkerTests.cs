using System.Collections.Generic;
using DocExtractor.Core.Linking;
using DocExtractor.Core.Models;
using Xunit;

namespace DocExtractor.Tests.Core
{
    public class CrossTableLinkerTests
    {
        [Fact]
        public void Link_ShouldFillMissingFields_FromRelatedTableByOverlapKey()
        {
            var records = new List<ExtractedRecord>
            {
                new ExtractedRecord
                {
                    SourceTableIndex = 0,
                    Fields = new Dictionary<string, string>
                    {
                        ["ChannelName"] = "通道A",
                        ["ItemName"] = "状态"
                    }
                },
                new ExtractedRecord
                {
                    SourceTableIndex = 0,
                    Fields = new Dictionary<string, string>
                    {
                        ["ChannelName"] = "通道B",
                        ["ItemName"] = "模式"
                    }
                },
                new ExtractedRecord
                {
                    SourceTableIndex = 1,
                    Fields = new Dictionary<string, string>
                    {
                        ["ChannelName"] = "通道A",
                        ["EnumMap"] = "0=关闭;1=开启"
                    }
                },
                new ExtractedRecord
                {
                    SourceTableIndex = 1,
                    Fields = new Dictionary<string, string>
                    {
                        ["ChannelName"] = "通道B",
                        ["EnumMap"] = "0=待机;1=工作"
                    }
                }
            };

            var linker = new CrossTableLinker();
            var linked = linker.Link(records, 0.6);

            Assert.Equal("0=关闭;1=开启", linked[0].Fields["EnumMap"]);
            Assert.Equal("0=待机;1=工作", linked[1].Fields["EnumMap"]);
        }
    }
}
