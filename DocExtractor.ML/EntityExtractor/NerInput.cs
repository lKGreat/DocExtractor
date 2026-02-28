using Microsoft.ML.Data;

namespace DocExtractor.ML.EntityExtractor
{
    /// <summary>
    /// NER 模型输入：字符级 BIO 标注
    /// 每个 NerInput 对应文本中的一个字符（token）
    /// </summary>
    public class NerInput
    {
        /// <summary>单个字符（中文1个汉字，英文1个字母）</summary>
        [ColumnName("CharToken")]
        public string CharToken { get; set; } = string.Empty;

        /// <summary>字符在文本中的绝对位置（0-based）</summary>
        [ColumnName("Position")]
        public float Position { get; set; }

        /// <summary>上下文窗口左侧字符（-2）</summary>
        [ColumnName("CtxLeft2")]
        public string CtxLeft2 { get; set; } = string.Empty;

        /// <summary>上下文窗口左侧字符（-1）</summary>
        [ColumnName("CtxLeft1")]
        public string CtxLeft1 { get; set; } = string.Empty;

        /// <summary>上下文窗口右侧字符（+1）</summary>
        [ColumnName("CtxRight1")]
        public string CtxRight1 { get; set; } = string.Empty;

        /// <summary>上下文窗口右侧字符（+2）</summary>
        [ColumnName("CtxRight2")]
        public string CtxRight2 { get; set; } = string.Empty;

        /// <summary>训练标签（BIO 标注：B-VALUE, I-VALUE, B-UNIT, I-UNIT, B-HEX, O 等）</summary>
        [ColumnName("Label")]
        public string Label { get; set; } = "O";
    }

    public class NerPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = "O";

        [ColumnName("Score")]
        public float[]? Score { get; set; }
    }
}
