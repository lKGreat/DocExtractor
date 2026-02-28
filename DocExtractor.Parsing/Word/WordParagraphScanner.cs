using System.Collections.Generic;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocExtractor.Parsing.Word
{
    /// <summary>
    /// 段落扫描结果（供 UI 层使用，不依赖 OpenXML）
    /// </summary>
    public class ParagraphScanResult
    {
        /// <summary>段落纯文本</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>是否加粗</summary>
        public bool IsBold { get; set; }

        /// <summary>字号（半磅，0=未知）</summary>
        public float FontSize { get; set; }

        /// <summary>是否有 Word 内置 Heading 样式</summary>
        public bool HasHeadingStyle { get; set; }

        /// <summary>大纲级别（0-8=有效，9=正文/无）</summary>
        public int OutlineLevel { get; set; } = 9;

        /// <summary>段落在文档中的相对位置（0.0~1.0）</summary>
        public float Position { get; set; }

        /// <summary>规则层自动判定是否为章节标题</summary>
        public bool AutoIsHeading { get; set; }

        /// <summary>规则层检测置信度</summary>
        public float AutoConfidence { get; set; }
    }

    /// <summary>
    /// Word 文档段落扫描器
    /// 扫描 Word 文档中的所有段落，提取格式特征，
    /// 并通过规则检测器自动标注章节标题（供训练数据收集使用）
    /// </summary>
    public class WordParagraphScanner
    {
        private readonly SectionHeadingDetector _ruleDetector;

        public WordParagraphScanner()
        {
            _ruleDetector = new SectionHeadingDetector();
        }

        /// <summary>
        /// 扫描 Word 文档，返回所有非空段落及其特征（规则自动标注）
        /// </summary>
        public IReadOnlyList<ParagraphScanResult> Scan(string filePath)
        {
            var results = new List<ParagraphScanResult>();

            using var doc = WordprocessingDocument.Open(filePath, isEditable: false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return results;

            int total = body.ChildElements.Count;
            int idx = 0;

            foreach (var element in body.ChildElements)
            {
                float position = total > 0 ? (float)idx / total : 0f;
                idx++;

                if (!(element is Paragraph para)) continue;

                var features = ParagraphFeatureExtractor.Extract(para);
                string text = ParagraphFeatureExtractor.ExtractText(para);

                // 跳过空段落或过长段落
                if (string.IsNullOrWhiteSpace(text) || text.Length > 120) continue;

                var ruleResult = _ruleDetector.Detect(
                    text, features.IsBold, features.FontSize,
                    features.HasHeadingStyle, features.OutlineLevel, position);

                results.Add(new ParagraphScanResult
                {
                    Text = text,
                    IsBold = features.IsBold,
                    FontSize = features.FontSize,
                    HasHeadingStyle = features.HasHeadingStyle,
                    OutlineLevel = features.OutlineLevel,
                    Position = position,
                    AutoIsHeading = ruleResult != null && ruleResult.Confidence >= 0.85f,
                    AutoConfidence = ruleResult?.Confidence ?? 0f
                });
            }

            return results;
        }
    }
}
