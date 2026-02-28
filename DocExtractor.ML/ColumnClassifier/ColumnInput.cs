using Microsoft.ML.Data;

namespace DocExtractor.ML.ColumnClassifier
{
    /// <summary>
    /// ML.NET 列名分类器输入
    /// </summary>
    public class ColumnInput
    {
        /// <summary>原始列名文本（如 "波道名称"、"参数名称"）</summary>
        [ColumnName("ColumnText")]
        public string ColumnText { get; set; } = string.Empty;

        /// <summary>训练时使用的标签（规范字段名，如 "ChannelName"）</summary>
        [ColumnName("Label")]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// ML.NET 列名分类器输出
    /// </summary>
    public class ColumnPrediction
    {
        /// <summary>预测的规范字段名</summary>
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;

        /// <summary>各类别的得分（Softmax 概率）</summary>
        [ColumnName("Score")]
        public float[]? Score { get; set; }
    }
}
