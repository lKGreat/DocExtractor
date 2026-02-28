using System;
using System.Collections.Generic;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Core.Pipeline;
using Xunit;

namespace DocExtractor.Tests.Pipeline
{
    public class ExtractionPreviewServiceTests
    {
        [Fact]
        public void Preview_ShouldReturnLowConfidenceWarning_WhenColumnUnmatched()
        {
            var table = BuildTable(new[] { "未知列" });
            var parser = new FakeParser(".docx", new[] { table });
            var normalizer = new NullColumnNormalizer();
            var service = new ExtractionPreviewService(new IDocumentParser[] { parser }, normalizer);

            var config = new ExtractionConfig
            {
                Fields = new List<FieldDefinition>
                {
                    new FieldDefinition { FieldName = "A", DisplayName = "字段A" }
                }
            };

            var result = service.Preview("sample.docx", config);

            Assert.True(result.Success);
            Assert.Single(result.Tables);
            Assert.NotEmpty(result.Warnings);
            Assert.True(result.Tables[0].Columns[0].IsLowConfidence);
        }

        [Fact]
        public void Preview_ShouldFail_WhenNoParserCanHandleExtension()
        {
            var service = new ExtractionPreviewService(Array.Empty<IDocumentParser>(), new NullColumnNormalizer());
            var result = service.Preview("sample.xyz", new ExtractionConfig());

            Assert.False(result.Success);
            Assert.Contains("不支持", result.ErrorMessage);
        }

        private static RawTable BuildTable(string[] headers)
        {
            var row = new List<TableCell>();
            for (int i = 0; i < headers.Length; i++)
            {
                row.Add(new TableCell
                {
                    RowIndex = 0,
                    ColIndex = i,
                    Value = headers[i],
                    IsMasterCell = true,
                    MasterRow = 0,
                    MasterCol = i
                });
            }

            return new RawTable
            {
                SourceFile = "sample.docx",
                TableIndex = 0,
                RowCount = 1,
                ColCount = headers.Length,
                Cells = new List<List<TableCell>> { row }
            };
        }

        private class FakeParser : IDocumentParser
        {
            private readonly string _ext;
            private readonly IReadOnlyList<RawTable> _tables;

            public FakeParser(string ext, IReadOnlyList<RawTable> tables)
            {
                _ext = ext;
                _tables = tables;
            }

            public IReadOnlyList<RawTable> Parse(string filePath) => _tables;

            public bool CanHandle(string fileExtension) =>
                string.Equals(fileExtension, _ext, StringComparison.OrdinalIgnoreCase);
        }

        private class NullColumnNormalizer : IColumnNormalizer
        {
            public ColumnMappingResult? Normalize(string rawColumnName, IReadOnlyList<FieldDefinition> fields) => null;

            public IReadOnlyList<ColumnMappingResult?> NormalizeBatch(IReadOnlyList<string> rawColumnNames, IReadOnlyList<FieldDefinition> fields)
            {
                var result = new List<ColumnMappingResult?>();
                for (int i = 0; i < rawColumnNames.Count; i++)
                    result.Add(null);
                return result;
            }
        }
    }
}
