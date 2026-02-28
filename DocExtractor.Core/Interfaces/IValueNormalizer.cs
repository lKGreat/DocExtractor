using DocExtractor.Core.Models;

namespace DocExtractor.Core.Interfaces
{
    /// <summary>
    /// 抽取值归一化器：按字段类型将原始值规范化为统一格式。
    /// </summary>
    public interface IValueNormalizer
    {
        /// <summary>
        /// 将原始值按字段定义归一化。
        /// </summary>
        /// <param name="rawValue">原始单元格文本（已做基础清洗）</param>
        /// <param name="field">字段定义（包含 DataType）</param>
        /// <param name="options">可选归一化策略覆盖</param>
        /// <returns>归一化值；若无法解析则返回原始值</returns>
        string Normalize(
            string rawValue,
            FieldDefinition field,
            ValueNormalizationOptions? options = null);
    }
}
