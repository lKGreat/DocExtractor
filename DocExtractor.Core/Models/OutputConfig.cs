using System.Collections.Generic;

namespace DocExtractor.Core.Models
{
    /// <summary>
    /// 导出输出配置：描述如何将抽取结果写成 Excel
    /// </summary>
    public class OutputConfig
    {
        public string ConfigName { get; set; } = "默认输出";

        /// <summary>字段映射列表（决定导出哪些列、顺序、列名）</summary>
        public List<OutputFieldMapping> FieldMappings { get; set; } = new List<OutputFieldMapping>();

        /// <summary>Sheet 切分规则</summary>
        public OutputSheetRule SheetRule { get; set; } = new OutputSheetRule();

        /// <summary>是否在多 Sheet 时额外生成一个汇总 Sheet</summary>
        public bool IncludeSummarySheet { get; set; } = true;
    }

    /// <summary>
    /// 单字段输出映射：输入字段名 → 输出 Excel 列
    /// </summary>
    public class OutputFieldMapping
    {
        /// <summary>来源字段名（与 FieldDefinition.FieldName 对应）</summary>
        public string SourceFieldName { get; set; } = string.Empty;

        /// <summary>输出到 Excel 的列名（默认等于 FieldDefinition.DisplayName）</summary>
        public string OutputColumnName { get; set; } = string.Empty;

        /// <summary>列顺序（升序排列）</summary>
        public int OutputOrder { get; set; }

        /// <summary>是否导出该列</summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Sheet 切分规则
    /// </summary>
    public class OutputSheetRule
    {
        /// <summary>切分方式</summary>
        public SheetSplitMode SplitMode { get; set; } = SheetSplitMode.BySourceFile;

        /// <summary>SplitMode=ByField 时的切分字段名</summary>
        public string SplitFieldName { get; set; } = string.Empty;

        /// <summary>Sheet 命名模板，{0} 为切分键值（如 "{0}" 或 "明细_{0}"）</summary>
        public string SheetNameTemplate { get; set; } = "{0}";
    }

    public enum SheetSplitMode
    {
        /// <summary>不切分，所有数据写到一个 Sheet</summary>
        None,

        /// <summary>按来源文件分 Sheet（默认，兼容现有行为）</summary>
        BySourceFile,

        /// <summary>按指定字段值分 Sheet</summary>
        ByField
    }
}
