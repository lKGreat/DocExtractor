using DocExtractor.Core.Interfaces;
using DocExtractor.ML.SectionClassifier;

namespace DocExtractor.ML.SectionClassifier
{
    /// <summary>
    /// 混合章节标题检测器：三层级联策略
    ///   Layer1 (置信度 1.0/0.95/0.90)：规则引擎（Heading样式 / 大纲级别 / 编号正则 / 中文章节词）
    ///   Layer2 (置信度 0.7+)           ：ML 二分类器（SectionClassifier）
    ///   Layer3 (置信度 0.50)           ：启发式降级（加粗+短文本但规则和ML都未覆盖）
    ///
    /// 与 HybridColumnNormalizer 保持相同的三层设计风格。
    /// </summary>
    public class HybridSectionHeadingDetector : ISectionHeadingDetector
    {
        private readonly ISectionHeadingDetector _ruleDetector;
        private readonly SectionClassifierModel? _mlModel;

        /// <summary>ML 分类判定为章节标题的概率阈值</summary>
        public float MlThreshold { get; set; } = 0.70f;

        /// <param name="ruleDetector">规则层检测器（通常为 SectionHeadingDetector）</param>
        /// <param name="mlModel">ML 分类器；null 时跳过 Layer2 直接到 Layer3</param>
        public HybridSectionHeadingDetector(
            ISectionHeadingDetector ruleDetector,
            SectionClassifierModel? mlModel = null)
        {
            _ruleDetector = ruleDetector;
            _mlModel = mlModel;
        }

        /// <inheritdoc />
        public SectionHeadingInfo? Detect(
            string paragraphText,
            bool isBold,
            float fontSize,
            bool hasHeadingStyle,
            int outlineLevel,
            float documentPosition)
        {
            if (string.IsNullOrWhiteSpace(paragraphText)) return null;
            if (paragraphText.Length > 120) return null;

            // ── Layer1：规则引擎（高置信度，优先执行）────────────────────────
            var ruleResult = _ruleDetector.Detect(
                paragraphText, isBold, fontSize, hasHeadingStyle, outlineLevel, documentPosition);

            // 规则置信度 >= 0.85 直接采纳（Heading样式/大纲级别/编号正则/中文章节词命中）
            if (ruleResult != null && ruleResult.Confidence >= 0.85f)
                return ruleResult;

            // ── Layer2：ML 分类器（中置信度区域）──────────────────────────────
            if (_mlModel != null && _mlModel.IsLoaded)
            {
                var input = new SectionInput
                {
                    Text = paragraphText,
                    IsBold = isBold ? 1f : 0f,
                    FontSize = fontSize,
                    HasNumberPrefix = HasNumberPrefix(paragraphText) ? 1f : 0f,
                    TextLength = paragraphText.Length,
                    HasHeadingStyle = hasHeadingStyle ? 1f : 0f,
                    Position = documentPosition
                };

                var (isHeading, probability) = _mlModel.Predict(input);

                if (isHeading && probability >= MlThreshold)
                {
                    // ML 确认为章节标题，解析编号和标题
                    var (number, title) = ParseNumberAndTitle(paragraphText);
                    return new SectionHeadingInfo
                    {
                        FullText = paragraphText,
                        Number = number,
                        Title = title,
                        Confidence = probability,
                        Source = $"ML({probability:P0})"
                    };
                }

                // ML 明确否定（概率 < 1-threshold），跳过 Layer3
                if (!isHeading && probability >= MlThreshold)
                    return null;
            }

            // ── Layer3：启发式降级（弱信号，规则/ML 均未高置信命中）──────────
            // 如果规则层有低置信度结果（如 FontFeature 0.60），保留它
            if (ruleResult != null)
                return ruleResult;

            // 纯启发式：加粗 + 短文本（<=30字符），视为可能的章节标题
            if (isBold && paragraphText.Length <= 30)
            {
                var (number, title) = ParseNumberAndTitle(paragraphText);
                return new SectionHeadingInfo
                {
                    FullText = paragraphText,
                    Number = number,
                    Title = title,
                    Confidence = 0.50f,
                    Source = "Heuristic"
                };
            }

            return null;
        }

        private static bool HasNumberPrefix(string text)
        {
            if (text.Length == 0) return false;
            return char.IsDigit(text[0]);
        }

        private static (string? number, string title) ParseNumberAndTitle(string text)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                text, @"^(\d+(?:\.\d+)*)\s*[\.、．\s]\s*(.+)$");
            if (m.Success)
                return (m.Groups[1].Value, m.Groups[2].Value.Trim());
            return (null, text);
        }
    }
}
