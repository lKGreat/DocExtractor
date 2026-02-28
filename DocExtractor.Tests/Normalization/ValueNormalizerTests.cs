using DocExtractor.Core.Models;
using DocExtractor.Core.Normalization;
using Xunit;

namespace DocExtractor.Tests.Normalization
{
    public class ValueNormalizerTests
    {
        private readonly DefaultValueNormalizer _normalizer = new DefaultValueNormalizer();

        [Fact]
        public void Normalize_Integer_ShouldTrimDecimalPart()
        {
            var field = new FieldDefinition { FieldName = "A", DataType = FieldDataType.Integer };
            var result = _normalizer.Normalize("123.0", field);
            Assert.Equal("123", result);
        }

        [Fact]
        public void Normalize_Decimal_ShouldNormalizeCommaDecimal()
        {
            var field = new FieldDefinition { FieldName = "B", DataType = FieldDataType.Decimal };
            var result = _normalizer.Normalize("12,50", field);
            Assert.Equal("12.5", result);
        }

        [Fact]
        public void Normalize_HexCode_ShouldNormalizePrefixAndCase()
        {
            var field = new FieldDefinition { FieldName = "C", DataType = FieldDataType.HexCode };
            var result = _normalizer.Normalize("1a", field);
            Assert.Equal("0x1A", result);
        }

        [Fact]
        public void Normalize_Boolean_ShouldMapChineseAliases()
        {
            var field = new FieldDefinition { FieldName = "D", DataType = FieldDataType.Boolean };
            Assert.Equal("true", _normalizer.Normalize("是", field));
            Assert.Equal("false", _normalizer.Normalize("否", field));
        }

        [Fact]
        public void Normalize_Enumeration_ShouldUnifySeparators()
        {
            var field = new FieldDefinition { FieldName = "E", DataType = FieldDataType.Enumeration };
            var result = _normalizer.Normalize("0:关闭/1=开启", field);
            Assert.Equal("0=关闭;1=开启", result);
        }
    }
}
