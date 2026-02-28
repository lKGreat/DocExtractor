using System.Collections.Generic;

namespace DocExtractor.Core.Interfaces
{
    /// <summary>
    /// 实体抽取接口：从单元格文本中识别值、单位、十六进制等实体
    /// </summary>
    public interface IEntityExtractor
    {
        /// <summary>从文本中抽取所有命名实体</summary>
        IReadOnlyList<NamedEntity> Extract(string text);

        /// <summary>抽取特定类型的实体</summary>
        IReadOnlyList<NamedEntity> Extract(string text, EntityType type);
    }

    public enum EntityType
    {
        Value,      // 数值
        Unit,       // 单位（V、A、℃、ms 等）
        HexCode,    // 十六进制（0x1A）
        Formula,    // 公式系数表达式
        Enum,       // 枚举描述文本
        Condition   // 条件描述
    }

    public class NamedEntity
    {
        public string Text { get; set; } = string.Empty;
        public EntityType Type { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public float Confidence { get; set; }
    }
}
