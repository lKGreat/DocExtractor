using System;
using System.Collections.Generic;
using System.Threading;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Interfaces
{
    /// <summary>
    /// 主流水线接口：协调解析→规范化→抽取→拆分的完整流程
    /// </summary>
    public interface IExtractionPipeline
    {
        /// <summary>
        /// 对单个文件执行完整抽取流程
        /// </summary>
        ExtractionResult Execute(
            string filePath,
            ExtractionConfig config,
            IProgress<PipelineProgress>? progress = null);

        /// <summary>批量处理多个文件，支持增量进度报告和取消</summary>
        IReadOnlyList<ExtractionResult> ExecuteBatch(
            IReadOnlyList<string> filePaths,
            ExtractionConfig config,
            IProgress<PipelineProgress>? progress = null,
            CancellationToken cancellation = default);
    }

    public class ExtractionResult
    {
        public string SourceFile { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorStage { get; set; }
        public IReadOnlyList<ExtractedRecord> Records { get; set; } = new List<ExtractedRecord>();
        public int TablesProcessed { get; set; }
        public int RecordsTotal { get; set; }
        public int RecordsComplete { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>按分组条件拆分后的记录分组（GroupByColumn → 子集），用于分 Sheet 导出</summary>
        public Dictionary<string, List<ExtractedRecord>>? GroupedRecords { get; set; }
    }

    public class PipelineProgress
    {
        public string Stage { get; set; } = string.Empty;   // "解析", "列名识别", "抽取", "拆分", "增量结果"
        public string Message { get; set; } = string.Empty;
        public int Percent { get; set; }

        /// <summary>
        /// 每完成一个文件时携带的增量抽取结果。
        /// 非 null 时 UI 应立即追加行到结果表格，而不仅仅更新进度条。
        /// </summary>
        public ExtractionResult? IncrementalResult { get; set; }
    }
}
