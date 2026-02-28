using System;
using System.Collections.Generic;
using DocExtractor.Core.Exceptions;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Core.Pipeline;
using Xunit;

namespace DocExtractor.Tests.Pipeline
{
    public class ExtractionPipelineTests
    {
        [Fact]
        public void Execute_ShouldNormalizeIntegerValue_WhenValueNormalizationEnabled()
        {
            var table = CreateTable(
                "sample.docx",
                0,
                new[] { "起始字节" },
                new[] { "123.0" });

            var parser = new FakeParser(".docx", new[] { table });
            var pipeline = new ExtractionPipeline(
                new IDocumentParser[] { parser },
                new PassThroughColumnNormalizer());

            var config = new ExtractionConfig
            {
                HeaderRowCount = 1,
                EnableValueNormalization = true,
                Fields = new List<FieldDefinition>
                {
                    new FieldDefinition
                    {
                        FieldName = "StartByte",
                        DisplayName = "起始字节",
                        DataType = FieldDataType.Integer,
                        IsRequired = true
                    }
                }
            };

            var result = pipeline.Execute("sample.docx", config);

            Assert.True(result.Success);
            Assert.Single(result.Records);
            Assert.Equal("123", result.Records[0].Fields["StartByte"]);
            Assert.Equal("123.0", result.Records[0].RawValues["StartByte"]);
        }

        [Fact]
        public void Execute_ShouldAddWarning_WhenNoColumnsMatched()
        {
            var table = CreateTable(
                "sample.docx",
                0,
                new[] { "未知列" },
                new[] { "abc" });

            var parser = new FakeParser(".docx", new[] { table });
            var pipeline = new ExtractionPipeline(
                new IDocumentParser[] { parser },
                new NullColumnNormalizer());

            var config = new ExtractionConfig
            {
                Fields = new List<FieldDefinition>
                {
                    new FieldDefinition { FieldName = "FieldA", DisplayName = "字段A" }
                }
            };

            var result = pipeline.Execute("sample.docx", config);

            Assert.True(result.Success);
            Assert.Empty(result.Records);
            Assert.NotEmpty(result.Warnings);
        }

        [Fact]
        public void Execute_ShouldReturnParseStageError_WhenParserThrowsParseException()
        {
            var parser = new ThrowingParser(".docx",
                new ParseException("文档损坏", "bad.docx", "TestParser"));
            var pipeline = new ExtractionPipeline(
                new IDocumentParser[] { parser },
                new NullColumnNormalizer());

            var config = new ExtractionConfig();
            var result = pipeline.Execute("bad.docx", config);

            Assert.False(result.Success);
            Assert.Equal("Parse", result.ErrorStage);
            Assert.Equal("PARSE_ERROR", result.ErrorCode);
        }

        private static RawTable CreateTable(
            string sourceFile,
            int tableIndex,
            string[] headers,
            string[] firstDataRow)
        {
            var cells = new List<List<TableCell>>();

            var headerRow = new List<TableCell>();
            for (int i = 0; i < headers.Length; i++)
            {
                headerRow.Add(new TableCell
                {
                    RowIndex = 0,
                    ColIndex = i,
                    Value = headers[i],
                    IsMasterCell = true,
                    MasterRow = 0,
                    MasterCol = i
                });
            }
            cells.Add(headerRow);

            var dataRow = new List<TableCell>();
            for (int i = 0; i < firstDataRow.Length; i++)
            {
                dataRow.Add(new TableCell
                {
                    RowIndex = 1,
                    ColIndex = i,
                    Value = firstDataRow[i],
                    IsMasterCell = true,
                    MasterRow = 1,
                    MasterCol = i
                });
            }
            cells.Add(dataRow);

            return new RawTable
            {
                SourceFile = sourceFile,
                TableIndex = tableIndex,
                RowCount = 2,
                ColCount = headers.Length,
                Cells = cells
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

        private class ThrowingParser : IDocumentParser
        {
            private readonly string _ext;
            private readonly Exception _exception;

            public ThrowingParser(string ext, Exception exception)
            {
                _ext = ext;
                _exception = exception;
            }

            public IReadOnlyList<RawTable> Parse(string filePath) => throw _exception;

            public bool CanHandle(string fileExtension) =>
                string.Equals(fileExtension, _ext, StringComparison.OrdinalIgnoreCase);
        }

        private class PassThroughColumnNormalizer : IColumnNormalizer
        {
            public ColumnMappingResult? Normalize(string rawColumnName, IReadOnlyList<FieldDefinition> fields)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    var f = fields[i];
                    if (f.DisplayName == rawColumnName || f.FieldName == rawColumnName)
                    {
                        return new ColumnMappingResult
                        {
                            RawName = rawColumnName,
                            CanonicalFieldName = f.FieldName,
                            Confidence = 1f,
                            MatchMethod = "exact"
                        };
                    }
                }
                return null;
            }

            public IReadOnlyList<ColumnMappingResult?> NormalizeBatch(IReadOnlyList<string> rawColumnNames, IReadOnlyList<FieldDefinition> fields)
            {
                var list = new List<ColumnMappingResult?>(rawColumnNames.Count);
                for (int i = 0; i < rawColumnNames.Count; i++)
                    list.Add(Normalize(rawColumnNames[i], fields));
                return list;
            }
        }

        private class NullColumnNormalizer : IColumnNormalizer
        {
            public ColumnMappingResult? Normalize(string rawColumnName, IReadOnlyList<FieldDefinition> fields) => null;

            public IReadOnlyList<ColumnMappingResult?> NormalizeBatch(IReadOnlyList<string> rawColumnNames, IReadOnlyList<FieldDefinition> fields)
            {
                var list = new List<ColumnMappingResult?>(rawColumnNames.Count);
                for (int i = 0; i < rawColumnNames.Count; i++)
                    list.Add(null);
                return list;
            }
        }
    }
}
