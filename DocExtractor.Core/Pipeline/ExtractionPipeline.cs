using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Core.Splitting;

namespace DocExtractor.Core.Pipeline
{
    /// <summary>
    /// 主流水线：协调 解析 → 列名规范化 → 字段抽取 → 拆分 的完整流程
    /// </summary>
    public class ExtractionPipeline : IExtractionPipeline
    {
        private readonly IReadOnlyList<IDocumentParser> _parsers;
        private readonly IColumnNormalizer _columnNormalizer;
        private readonly IEntityExtractor? _entityExtractor;
        private readonly IReadOnlyList<IRecordSplitter> _splitters;

        public ExtractionPipeline(
            IReadOnlyList<IDocumentParser> parsers,
            IColumnNormalizer columnNormalizer,
            IEntityExtractor? entityExtractor = null)
        {
            _parsers = parsers;
            _columnNormalizer = columnNormalizer;
            _entityExtractor = entityExtractor;
            _splitters = new IRecordSplitter[]
            {
                new MergedCellExpander(),
                new MultiValueSplitter(),
                new GroupConditionSplitter(),
                new SubTableExpander()
            };
        }

        public ExtractionResult Execute(
            string filePath,
            ExtractionConfig config,
            IProgress<PipelineProgress>? progress = null)
        {
            var result = new ExtractionResult { SourceFile = filePath };

            try
            {
                // 1. 选择解析器
                string ext = Path.GetExtension(filePath);
                var parser = _parsers.FirstOrDefault(p => p.CanHandle(ext));
                if (parser == null)
                {
                    result.ErrorMessage = $"不支持的文件格式: {ext}";
                    return result;
                }

                Report(progress, "解析",
                    $"正在解析文档... 配置「{config.ConfigName}」含 {config.Fields.Count} 个字段",
                    5);

                // 2. 解析文档 → RawTable 集合
                var rawTables = parser.Parse(filePath);
                result.TablesProcessed = rawTables.Count;

                Report(progress, "解析", $"解析完成，共 {rawTables.Count} 个表格", 20);

                // 诊断：输出前几个表格的表头行
                for (int di = 0; di < Math.Min(rawTables.Count, 5); di++)
                {
                    var dt = rawTables[di];
                    if (dt.RowCount > 0)
                    {
                        var headerCells = new List<string>();
                        for (int c = 0; c < dt.ColCount; c++)
                            headerCells.Add(dt.GetValue(0, c));
                        Report(progress, "诊断",
                            $"表格{di}({dt.RowCount}行{dt.ColCount}列) 表头: [{string.Join(" | ", headerCells)}]",
                            20);
                    }
                }

                // 3. 过滤表格（按配置）
                var selectedTables = FilterTables(rawTables, config);

                if (selectedTables.Count == 0)
                {
                    Report(progress, "完成", "未找到匹配的表格（过滤条件可能过严）", 100);
                    result.Success = true;
                    result.Records = new List<ExtractedRecord>();
                    result.Warnings.Add("未找到匹配的表格，请检查表格过滤条件");
                    return result;
                }

                Report(progress, "列名识别", $"开始处理 {selectedTables.Count} 个表格...", 30);

                var allRecords = new List<ExtractedRecord>();
                int skippedNoMatch = 0;

                for (int ti = 0; ti < selectedTables.Count; ti++)
                {
                    var table = selectedTables[ti];
                    int pct = 30 + (int)(40.0 * ti / selectedTables.Count);

                    // 4. 列名规范化
                    var columnMap = BuildColumnMap(table, config);

                    if (columnMap.Count == 0)
                    {
                        skippedNoMatch++;
                        continue; // 此表格无任何列匹配，跳过
                    }

                    Report(progress, "列名识别",
                        $"表格 {ti + 1}/{selectedTables.Count}: 匹配到 {columnMap.Count} 列" +
                        (table.Title != null ? $" ({table.Title})" : ""),
                        pct);

                    // 5. 按行抽取记录
                    var tableRecords = ExtractRecordsFromTable(table, config, columnMap);
                    allRecords.AddRange(tableRecords);
                }

                if (skippedNoMatch > 0)
                    result.Warnings.Add($"跳过 {skippedNoMatch} 个表格（列名未匹配到任何字段）");

                Report(progress, "拆分", "正在应用拆分规则...", 75);

                // 6. 应用拆分规则（按 Priority 排序）
                var finalRecords = ApplySplitRules(allRecords, config);

                // 7. 如果有分组规则，按 __GroupKey__ 分组（用于后续分 Sheet 导出）
                var groupRule = config.SplitRules
                    .FirstOrDefault(r => r.IsEnabled && r.Type == SplitType.GroupConditionSplit);
                if (groupRule != null && !string.IsNullOrWhiteSpace(groupRule.GroupByColumn))
                {
                    var grouped = Splitting.GroupConditionSplitter.GroupRecords(
                        finalRecords, groupRule.GroupByColumn);
                    result.GroupedRecords = grouped;
                }

                Report(progress, "完成", $"抽取完成，共 {finalRecords.Count} 条记录", 100);

                result.Records = finalRecords;
                result.RecordsTotal = finalRecords.Count;
                result.RecordsComplete = finalRecords.Count(r => r.IsComplete);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public IReadOnlyList<ExtractionResult> ExecuteBatch(
            IReadOnlyList<string> filePaths,
            ExtractionConfig config,
            IProgress<PipelineProgress>? progress = null)
        {
            var results = new List<ExtractionResult>();
            for (int i = 0; i < filePaths.Count; i++)
            {
                var fp = filePaths[i];
                int basePct = (int)(100.0 * i / filePaths.Count);
                Report(progress, "批处理", $"处理文件 {i + 1}/{filePaths.Count}: {Path.GetFileName(fp)}", basePct);
                results.Add(Execute(fp, config, progress));
            }
            return results;
        }

        // ── 私有方法 ─────────────────────────────────────────────────────────────

        private IReadOnlyList<RawTable> FilterTables(IReadOnlyList<RawTable> tables, ExtractionConfig config)
        {
            return config.TableSelection switch
            {
                TableSelectionMode.ByIndex =>
                    tables.Where(t => config.TableIndices.Contains(t.TableIndex)).ToList(),
                TableSelectionMode.ByKeyword =>
                    tables.Where(t => config.TableKeywords.Any(kw =>
                        t.Title?.Contains(kw) == true)).ToList(),
                _ => tables.ToList()
            };
        }

        /// <summary>
        /// 构建 [列索引] → [规范字段名] 的映射
        /// </summary>
        private Dictionary<int, string> BuildColumnMap(RawTable table, ExtractionConfig config)
        {
            var map = new Dictionary<int, string>();
            if (table.RowCount == 0) return map;

            // 读取表头行（第0行）
            var headers = new List<string>();
            for (int c = 0; c < table.ColCount; c++)
                headers.Add(table.GetValue(0, c));

            var mappings = _columnNormalizer.NormalizeBatch(headers, config.Fields);

            for (int i = 0; i < mappings.Count; i++)
            {
                var m = mappings[i];
                if (m != null && !string.IsNullOrEmpty(m.CanonicalFieldName))
                    map[i] = m.CanonicalFieldName;
            }

            return map;
        }

        /// <summary>
        /// 从表格数据行（跳过表头）提取记录
        /// </summary>
        private List<ExtractedRecord> ExtractRecordsFromTable(
            RawTable table,
            ExtractionConfig config,
            Dictionary<int, string> columnMap)
        {
            var records = new List<ExtractedRecord>();
            int dataStart = config.HeaderRowCount; // 跳过表头

            for (int r = dataStart; r < table.RowCount; r++)
            {
                // 跳过空行
                if (config.SkipEmptyRows && IsEmptyRow(table, r)) continue;

                var record = new ExtractedRecord
                {
                    SourceFile = table.SourceFile,
                    SourceTableIndex = table.TableIndex,
                    SourceRowIndex = r
                };

                // 注入章节标题（组名），优先于列映射填充
                if (!string.IsNullOrWhiteSpace(table.SectionHeading))
                    record.Fields["GroupName"] = table.SectionHeading!;

                // 按列映射填充字段
                for (int c = 0; c < table.ColCount; c++)
                {
                    string cellValue = table.GetValue(r, c);
                    record.RawValues[$"col_{c}"] = cellValue;

                    if (columnMap.TryGetValue(c, out var fieldName))
                    {
                        record.Fields[fieldName] = cellValue;
                        record.RawValues[fieldName] = cellValue;
                    }
                }

                // 检查必填字段
                record.IsComplete = config.Fields
                    .Where(f => f.IsRequired)
                    .All(f => record.Fields.ContainsKey(f.FieldName) &&
                              !string.IsNullOrWhiteSpace(record.Fields[f.FieldName]));

                // 应用默认值
                foreach (var field in config.Fields)
                {
                    if (!record.Fields.ContainsKey(field.FieldName) && field.DefaultValue != null)
                        record.Fields[field.FieldName] = field.DefaultValue;
                }

                records.Add(record);
            }

            return records;
        }

        private List<ExtractedRecord> ApplySplitRules(
            List<ExtractedRecord> records,
            ExtractionConfig config)
        {
            var current = records;
            var sortedRules = config.SplitRules
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.Priority)
                .ToList();

            foreach (var rule in sortedRules)
            {
                var splitter = _splitters.FirstOrDefault(s => s.SupportedType == rule.Type);
                if (splitter == null) continue;

                var next = new List<ExtractedRecord>();
                foreach (var record in current)
                    next.AddRange(splitter.Split(record, rule));
                current = next;
            }

            return current;
        }

        private static bool IsEmptyRow(RawTable table, int row)
        {
            for (int c = 0; c < table.ColCount; c++)
            {
                if (!string.IsNullOrWhiteSpace(table.GetValue(row, c)))
                    return false;
            }
            return true;
        }

        private static void Report(IProgress<PipelineProgress>? progress, string stage, string msg, int pct) =>
            progress?.Report(new PipelineProgress { Stage = stage, Message = msg, Percent = pct });
    }
}
