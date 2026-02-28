using System.Collections.Generic;

namespace DocExtractor.Core.Models
{
    /// <summary>拆分类型</summary>
    public enum SplitType
    {
        /// <summary>合并单元格展开：主格值复制到每个子行</summary>
        MergedCellExpand,
        /// <summary>多值字段拆分：按分隔符拆分单元格中多个值</summary>
        MultiValueSplit,
        /// <summary>分组条件拆分：按某列的值分组，生成多个结果集</summary>
        GroupConditionSplit,
        /// <summary>子表格展开：将嵌套子表格展开为独立记录</summary>
        SubTableExpand,
        /// <summary>时间轴展开：检测多步序列/跳变/阈值模式，拆分为多行并注入时间轴字段</summary>
        TimeAxisExpand
    }

    /// <summary>
    /// 拆分规则配置
    /// </summary>
    public class SplitRule
    {
        public string RuleName { get; set; } = string.Empty;

        public SplitType Type { get; set; }

        /// <summary>触发该规则的列的规范名（FieldName）</summary>
        public string TriggerColumn { get; set; } = string.Empty;

        /// <summary>多值分隔符，支持多个（按顺序尝试）。MultiValueSplit 专用</summary>
        public List<string> Delimiters { get; set; } = new List<string> { "/", ";", "、", "\n" };

        /// <summary>分组列名。GroupConditionSplit 专用</summary>
        public string GroupByColumn { get; set; } = string.Empty;

        /// <summary>展开子行时，是否从父行继承所有字段值</summary>
        public bool InheritParentFields { get; set; } = true;

        /// <summary>执行优先级（数字越小越先执行）</summary>
        public int Priority { get; set; } = 0;

        /// <summary>是否启用</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>时间轴字段名。TimeAxisExpand 专用</summary>
        public string TimeAxisFieldName { get; set; } = "TimeAxis";

        /// <summary>多档序列的默认公差（如 0.5 表示 ±0.5）。TimeAxisExpand 专用</summary>
        public double DefaultTolerance { get; set; } = 0;

        /// <summary>最后一档无时间时的默认时间值。TimeAxisExpand 专用</summary>
        public double DefaultTimeValue { get; set; } = 0;
    }
}
