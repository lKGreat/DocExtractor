using System.Collections.Generic;

namespace DocExtractor.Core.Models
{
    /// <summary>
    /// 抽取配置：定义一次抽取任务的全部参数
    /// </summary>
    public class ExtractionConfig
    {
        public string ConfigName { get; set; } = "默认配置";

        /// <summary>目标字段定义列表（按顺序）</summary>
        public List<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();

        /// <summary>拆分规则列表（按 Priority 排序执行）</summary>
        public List<SplitRule> SplitRules { get; set; } = new List<SplitRule>();

        /// <summary>表格选择策略：所有表格 / 按索引 / 按关键字</summary>
        public TableSelectionMode TableSelection { get; set; } = TableSelectionMode.All;

        /// <summary>当 TableSelection=ByIndex 时的索引列表（0-based）</summary>
        public List<int> TableIndices { get; set; } = new List<int>();

        /// <summary>当 TableSelection=ByKeyword 时，表格标题包含此关键字则选中</summary>
        public List<string> TableKeywords { get; set; } = new List<string>();

        /// <summary>表头行数（1=单行表头，2=双层表头等）</summary>
        public int HeaderRowCount { get; set; } = 1;

        /// <summary>是否跳过空行</summary>
        public bool SkipEmptyRows { get; set; } = true;

        /// <summary>列名匹配模式：优先使用 ML 模型 / 仅用规则 / 混合</summary>
        public ColumnMatchMode ColumnMatch { get; set; } = ColumnMatchMode.HybridMlFirst;

        /// <summary>Excel Sheet 选择（null = 所有）</summary>
        public List<string>? TargetSheets { get; set; }

        /// <summary>是否启用值归一化</summary>
        public bool EnableValueNormalization { get; set; } = true;

        /// <summary>值归一化选项（可选）</summary>
        public ValueNormalizationOptions? NormalizationOptions { get; set; }
    }

    public enum TableSelectionMode
    {
        All,
        ByIndex,
        ByKeyword
    }

    public enum ColumnMatchMode
    {
        RuleOnly,
        MlOnly,
        HybridMlFirst,   // ML 优先，置信度低时降级到规则
        HybridRuleFirst  // 规则优先，匹配不到时用 ML
    }
}
