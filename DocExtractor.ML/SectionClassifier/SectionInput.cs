using Microsoft.ML.Data;

namespace DocExtractor.ML.SectionClassifier
{
    /// <summary>
    /// 章节标题分类器的输入数据结构
    /// 同时包含文本特征（N-gram）和格式特征（数值型）
    /// </summary>
    public class SectionInput
    {
        /// <summary>段落文本内容</summary>
        [LoadColumn(0)]
        public string Text { get; set; } = string.Empty;

        /// <summary>是否加粗（1=是，0=否）</summary>
        [LoadColumn(1)]
        public float IsBold { get; set; }

        /// <summary>字号大小（半磅单位，0表示未知）</summary>
        [LoadColumn(2)]
        public float FontSize { get; set; }

        /// <summary>是否以数字编号开头（1=是，0=否）</summary>
        [LoadColumn(3)]
        public float HasNumberPrefix { get; set; }

        /// <summary>文本长度（字符数）</summary>
        [LoadColumn(4)]
        public float TextLength { get; set; }

        /// <summary>是否有 Word 内置 Heading 样式（1=是，0=否）</summary>
        [LoadColumn(5)]
        public float HasHeadingStyle { get; set; }

        /// <summary>段落在文档中的相对位置（0.0~1.0）</summary>
        [LoadColumn(6)]
        public float Position { get; set; }

        /// <summary>标签：true = 章节标题，false = 普通段落（训练用）</summary>
        [LoadColumn(7)]
        [ColumnName("Label")]
        public bool IsHeading { get; set; }
    }

    /// <summary>
    /// 章节标题分类器的预测输出
    /// </summary>
    public class SectionPrediction
    {
        [ColumnName("PredictedLabel")]
        public bool IsHeading { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }

        [ColumnName("Score")]
        public float Score { get; set; }
    }
}
