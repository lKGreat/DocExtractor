using System.Collections.Generic;
using DocExtractor.Core.Models;
using DocExtractor.ML.ColumnClassifier;
using DocExtractor.ML.Inference;
using Xunit;

namespace DocExtractor.Tests.Normalization
{
    public class ColumnNormalizerTests
    {
        [Fact]
        public void Normalize_ShouldMatchExactDisplayName()
        {
            var normalizer = new HybridColumnNormalizer(
                new ColumnClassifierModel(),
                ColumnMatchMode.HybridMlFirst);

            var fields = new List<FieldDefinition>
            {
                new FieldDefinition { FieldName = "APID", DisplayName = "APID值" }
            };

            var result = normalizer.Normalize("APID值", fields);

            Assert.NotNull(result);
            Assert.Equal("APID", result!.CanonicalFieldName);
            Assert.Equal("exact", result.MatchMethod);
        }

        [Fact]
        public void Normalize_ShouldMatchKnownVariant()
        {
            var normalizer = new HybridColumnNormalizer(
                new ColumnClassifierModel(),
                ColumnMatchMode.HybridMlFirst);

            var fields = new List<FieldDefinition>
            {
                new FieldDefinition
                {
                    FieldName = "BitLength",
                    DisplayName = "位长度",
                    KnownColumnVariants = new List<string> { "字节长度" }
                }
            };

            var result = normalizer.Normalize("字节长度", fields);

            Assert.NotNull(result);
            Assert.Equal("BitLength", result!.CanonicalFieldName);
            Assert.Equal("rule_exact", result.MatchMethod);
        }

        [Fact]
        public void Normalize_ShouldReturnNull_WhenMlOnlyAndModelNotLoaded()
        {
            var normalizer = new HybridColumnNormalizer(
                new ColumnClassifierModel(),
                ColumnMatchMode.MlOnly);

            var fields = new List<FieldDefinition>
            {
                new FieldDefinition { FieldName = "APID", DisplayName = "APID值" }
            };

            var result = normalizer.Normalize("APID值", fields);
            Assert.Null(result);
        }
    }
}
