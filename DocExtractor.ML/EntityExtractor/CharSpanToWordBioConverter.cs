using System.Collections.Generic;
using System.Linq;

namespace DocExtractor.ML.EntityExtractor
{
    /// <summary>
    /// 将旧格式 NerAnnotation（字符 span 标注）转换为 NAS-BERT 所需的
    /// NerWordSample（空格分隔字符 + BIO 标签数组）
    /// </summary>
    public static class CharSpanToWordBioConverter
    {
        /// <summary>
        /// 将单个标注转换为词级样本
        /// 中文文本按字符拆分，用空格连接作为 "词" 序列
        /// </summary>
        public static NerWordSample Convert(NerAnnotation annotation)
        {
            var chars = annotation.Text.ToCharArray();
            var labels = new string[chars.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = "O";

            foreach (var entity in annotation.Entities)
            {
                for (int i = entity.StartIndex; i <= entity.EndIndex && i < chars.Length; i++)
                {
                    labels[i] = i == entity.StartIndex
                        ? $"B-{entity.EntityType}"
                        : $"I-{entity.EntityType}";
                }
            }

            // 每个字符用空格分隔，形成 "母 线 电 压 : 2 8 V" 格式
            var sentence = string.Join(" ", chars.Select(c => c.ToString()));

            return new NerWordSample
            {
                Sentence = sentence,
                Label = labels
            };
        }

        /// <summary>批量转换</summary>
        public static List<NerWordSample> ConvertAll(IReadOnlyList<NerAnnotation> annotations)
        {
            return annotations.Select(Convert).ToList();
        }
    }
}
