namespace DocExtractor.Core.Interfaces
{
    /// <summary>
    /// 章节标题检测接口（面向 WordDocumentParser 的抽象）
    /// 实现：规则层（SectionHeadingDetector）/ 混合层（HybridSectionHeadingDetector）
    /// </summary>
    public interface ISectionHeadingDetector
    {
        /// <summary>
        /// 检测段落原始文本是否为章节标题
        /// </summary>
        /// <param name="paragraphText">段落纯文本</param>
        /// <param name="isBold">是否加粗</param>
        /// <param name="fontSize">字号（半磅，0=未知）</param>
        /// <param name="hasHeadingStyle">是否有 Word Heading 样式</param>
        /// <param name="outlineLevel">大纲级别（0-8=有效级别，9=正文/无）</param>
        /// <param name="documentPosition">段落在文档中的相对位置（0.0~1.0）</param>
        /// <returns>检测结果；不是章节标题则返回 null</returns>
        SectionHeadingInfo? Detect(
            string paragraphText,
            bool isBold,
            float fontSize,
            bool hasHeadingStyle,
            int outlineLevel,
            float documentPosition);
    }

    /// <summary>
    /// 章节标题检测结果（与 OpenXML 解耦的纯数据模型）
    /// </summary>
    public class SectionHeadingInfo
    {
        /// <summary>章节编号（如 "2.1"），无编号时为 null</summary>
        public string? Number { get; set; }

        /// <summary>章节标题文本（纯文字，不含编号）</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>完整原始文本（含编号）</summary>
        public string FullText { get; set; } = string.Empty;

        /// <summary>检测置信度 0.0-1.0</summary>
        public float Confidence { get; set; }

        /// <summary>检测来源（HeadingStyle / NumberPattern / ML / FontFeature 等）</summary>
        public string Source { get; set; } = string.Empty;
    }
}
