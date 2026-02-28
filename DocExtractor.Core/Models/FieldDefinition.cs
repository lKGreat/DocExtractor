using System.Collections.Generic;

namespace DocExtractor.Core.Models
{
    /// <summary>字段数据类型</summary>
    public enum FieldDataType
    {
        Text,
        Integer,
        Decimal,
        HexCode,     // 十六进制，如 0x1A
        Enumeration, // 枚举映射
        Formula,     // 公式表达式
        Unit,        // 带单位的数值
        Boolean
    }

    /// <summary>
    /// 目标字段定义：描述要从文档中抽取的一个字段
    /// </summary>
    public class FieldDefinition
    {
        /// <summary>字段规范名称（英文标识符，如 "TelemetryCode"）</summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>字段显示名（中文，如 "遥测代号"）</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>数据类型</summary>
        public FieldDataType DataType { get; set; } = FieldDataType.Text;

        /// <summary>
        /// 已知的列名变体（用于初始化训练数据和规则匹配备用）。
        /// 例：["遥测代号", "参数代号", "代号"]
        /// </summary>
        public List<string> KnownColumnVariants { get; set; } = new List<string>();

        /// <summary>是否必填</summary>
        public bool IsRequired { get; set; }

        /// <summary>默认值（抽取失败时使用）</summary>
        public string? DefaultValue { get; set; }

        /// <summary>枚举映射表（数值→描述），DataType=Enumeration 时有效</summary>
        public Dictionary<string, string> EnumerationMap { get; set; } = new Dictionary<string, string>();

        public override string ToString() => $"{FieldName}({DisplayName}):{DataType}";
    }
}
