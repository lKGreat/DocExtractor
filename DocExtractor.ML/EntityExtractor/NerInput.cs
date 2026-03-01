using System;
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

    /// <summary>NER 句级训练样本（空格分词 + BIO 标签数组）用于 NAS-BERT</summary>
    public class NerWordSample
    {
        [ColumnName("Sentence")]
        public string Sentence { get; set; } = "";

        [ColumnName("Label")]
        public string[] Label { get; set; } = Array.Empty<string>();
    }

    /// <summary>NER 推理输入（仅需 Sentence）</summary>
    public class NerWordInput
    {
        [ColumnName("Sentence")]
        public string Sentence { get; set; } = "";
    }

    /// <summary>NER 推理输出</summary>
    public class NerWordOutput
    {
        [ColumnName("PredictedLabel")]
        public string[] PredictedLabel { get; set; } = Array.Empty<string>();
    }

    /// <summary>NER 推理输出（含 softmax 置信度分数，用于主动学习不确定性采样）</summary>
    public class NerWordOutputWithScore
    {
        [ColumnName("PredictedLabel")]
        public string[] PredictedLabel { get; set; } = Array.Empty<string>();

        /// <summary>每个 token 的最高 softmax 分数（即该 token 的预测置信度）</summary>
        [ColumnName("Score")]
        public float[]? Score { get; set; }
    }
}
