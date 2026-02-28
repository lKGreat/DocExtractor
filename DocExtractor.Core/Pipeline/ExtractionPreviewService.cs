using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocExtractor.Core.Exceptions;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Core.Models.Preview;

namespace DocExtractor.Core.Pipeline
{
    /// <summary>
    /// 文档快速预览服务：仅解析文档与列映射，不执行完整抽取。
    /// </summary>
    public class ExtractionPreviewService
    {
        private readonly IReadOnlyList<IDocumentParser> _parsers;
        private readonly IColumnNormalizer _columnNormalizer;

        public ExtractionPreviewService(
            IReadOnlyList<IDocumentParser> parsers,
            IColumnNormalizer columnNormalizer)
        {
            _parsers = parsers;
            _columnNormalizer = columnNormalizer;
        }

        public ExtractionPreviewResult Preview(string filePath, ExtractionConfig config)
        {
            var result = new ExtractionPreviewResult
            {
                SourceFile = filePath
            };

            try
            {
                string ext = Path.GetExtension(filePath);
                var parser = _parsers.FirstOrDefault(p => p.CanHandle(ext));
                if (parser == null)
                {
                    result.ErrorMessage = $"不支持的文件格式: {ext}";
                    result.Success = false;
                    return result;
                }

                IReadOnlyList<RawTable> tables;
                try
                {
                    tables = parser.Parse(filePath);
                }
                catch (ParseException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new ParseException(
                        $"预览解析失败：{ex.Message}",
                        filePath,
                        parser.GetType().Name,
                        ex);
                }

                foreach (var table in tables)
                {
                    var tablePreview = new TablePreviewInfo
                    {
                        TableIndex = table.TableIndex,
                        Title = table.Title,
                        RowCount = table.RowCount,
                        ColCount = table.ColCount
                    };

                    if (table.RowCount > 0)
                    {
                        var headers = new List<string>();
                        for (int c = 0; c < table.ColCount; c++)
                            headers.Add(table.GetValue(0, c));

                        var mappings = _columnNormalizer.NormalizeBatch(headers, config.Fields);
                        for (int i = 0; i < headers.Count; i++)
                        {
                            var mapping = mappings[i];
                            var fieldDef = mapping == null
                                ? null
                                : config.Fields.FirstOrDefault(f => f.FieldName == mapping.CanonicalFieldName);

                            var previewItem = new ColumnPreviewItem
                            {
                                ColumnIndex = i,
                                RawColumnName = headers[i],
                                MappedFieldName = mapping?.CanonicalFieldName,
                                MappedDisplayName = fieldDef?.DisplayName,
                                Confidence = mapping?.Confidence ?? 0f,
                                MatchMethod = mapping?.MatchMethod ?? "none"
                            };

                            if (previewItem.IsLowConfidence)
                            {
                                result.Warnings.Add(
                                    $"表格{table.TableIndex + 1} 列「{previewItem.RawColumnName}」置信度低（{previewItem.Confidence:P0}）");
                            }

                            tablePreview.Columns.Add(previewItem);
                        }
                    }

                    result.Tables.Add(tablePreview);
                }

                result.Success = true;
            }
            catch (DocExtractorException dex)
            {
                result.Success = false;
                result.ErrorMessage = dex.Message;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
    }
}
