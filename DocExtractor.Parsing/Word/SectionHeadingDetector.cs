using System.Text.RegularExpressions;
using DocExtractor.Core.Interfaces;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocExtractor.Parsing.Word
{
    /// <summary>
    /// 章节标题检测器（纯规则引擎层）
    /// 实现 ISectionHeadingDetector，通过传入的格式特征+文本运行检测逻辑，
    /// 无需直接引用 OpenXML；OpenXML 特征由 WordDocumentParser 提前提取。
    /// </summary>
    public class SectionHeadingDetector : ISectionHeadingDetector
    {
        // 匹配 "1." "2.1" "2.2" "3.1.2" 等数字编号开头的标题
        private static readonly Regex SectionNumberPattern =
            new Regex(@"^(\d+(?:\.\d+)*)\s*[\.、．\s]\s*(.+)$", RegexOptions.Compiled);

        // 中文章节词："第X章"/"第X节"等
        private static readonly Regex ChineseChapterPattern =
            new Regex(@"^第\s*[一二三四五六七八九十百\d]+\s*[章节条款项]\s*(.*)$", RegexOptions.Compiled);

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

            // 文本过长不可能是标题（超过 120 字符）
            if (paragraphText.Length > 120) return null;

            // Layer1a：Word 内置 Heading 样式（置信度 1.0）
            if (hasHeadingStyle)
            {
                var parsed = ParseNumberAndTitle(paragraphText);
                return new SectionHeadingInfo
                {
                    FullText = paragraphText,
                    Number = parsed.number,
                    Title = parsed.title,
                    Confidence = 1.0f,
                    Source = "HeadingStyle"
                };
            }

            // Layer1b：Word 大纲级别（置信度 0.95）
            if (outlineLevel >= 0 && outlineLevel < 9)
            {
                var parsed = ParseNumberAndTitle(paragraphText);
                return new SectionHeadingInfo
                {
                    FullText = paragraphText,
                    Number = parsed.number,
                    Title = parsed.title,
                    Confidence = 0.95f,
                    Source = $"OutlineLevel{outlineLevel + 1}"
                };
            }

            // Layer1c：数字编号正则（置信度 0.90）
            var numberResult = TryDetectByNumberPattern(paragraphText);
            if (numberResult != null) return numberResult;

            // Layer1d：中文章节词（置信度 0.90）
            var chineseResult = TryDetectByChinesePattern(paragraphText);
            if (chineseResult != null) return chineseResult;

            // Layer1e：字体特征（加粗 + 短文本）（置信度 0.60）
            if (isBold && paragraphText.Length <= 40)
            {
                var parsed = ParseNumberAndTitle(paragraphText);
                return new SectionHeadingInfo
                {
                    FullText = paragraphText,
                    Number = parsed.number,
                    Title = parsed.title,
                    Confidence = 0.60f,
                    Source = "FontFeature"
                };
            }

            return null;
        }

        // ── 规则实现 ─────────────────────────────────────────────────────────

        private SectionHeadingInfo? TryDetectByNumberPattern(string fullText)
        {
            var m = SectionNumberPattern.Match(fullText);
            if (!m.Success) return null;

            string number = m.Groups[1].Value;
            string title = m.Groups[2].Value.Trim();
            if (string.IsNullOrWhiteSpace(title)) return null;

            return new SectionHeadingInfo
            {
                FullText = fullText,
                Number = number,
                Title = title,
                Confidence = 0.90f,
                Source = "NumberPattern"
            };
        }

        private SectionHeadingInfo? TryDetectByChinesePattern(string fullText)
        {
            var m = ChineseChapterPattern.Match(fullText);
            if (!m.Success) return null;

            string title = m.Groups[1].Value.Trim();
            return new SectionHeadingInfo
            {
                FullText = fullText,
                Number = null,
                Title = string.IsNullOrWhiteSpace(title) ? fullText : title,
                Confidence = 0.90f,
                Source = "ChineseChapter"
            };
        }

        // ── 辅助方法 ─────────────────────────────────────────────────────────

        private static (string? number, string title) ParseNumberAndTitle(string text)
        {
            var m = SectionNumberPattern.Match(text);
            if (m.Success)
                return (m.Groups[1].Value, m.Groups[2].Value.Trim());
            return (null, text);
        }
    }

    /// <summary>
    /// OpenXML 段落特征提取辅助类（供 WordDocumentParser 使用）
    /// </summary>
    public static class ParagraphFeatureExtractor
    {
        private static readonly string[] HeadingStyleIds =
        {
            "heading1", "heading2", "heading3", "heading4", "heading5",
            "1", "2", "3", "4", "5",
            "标题1", "标题2", "标题3", "标题4"
        };

        /// <summary>提取段落的格式特征</summary>
        public static ParagraphFeatures Extract(Paragraph para)
        {
            var features = new ParagraphFeatures();
            var props = para.ParagraphProperties;

            if (props != null)
            {
                // Heading 样式检测
                var styleId = props.ParagraphStyleId?.Val?.Value ?? string.Empty;
                foreach (var hid in HeadingStyleIds)
                {
                    if (styleId.Equals(hid, System.StringComparison.OrdinalIgnoreCase))
                    {
                        features.HasHeadingStyle = true;
                        break;
                    }
                }

                // 大纲级别
                var outlineLevel = props.OutlineLevel;
                if (outlineLevel != null)
                    features.OutlineLevel = outlineLevel.Val?.Value ?? 9;
            }

            // 加粗检测（检查第一个 Run 的 RunProperties）
            features.IsBold = IsParaBold(para);

            // 字号检测（取第一个 Run 的字号，单位：半磅）
            features.FontSize = GetFontSize(para);

            return features;
        }

        /// <summary>提取段落纯文本</summary>
        public static string ExtractText(Paragraph para)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var run in para.Descendants<Run>())
            {
                foreach (var t in run.Descendants<Text>())
                    sb.Append(t.Text);
            }
            return sb.ToString().Trim();
        }

        private static bool IsParaBold(Paragraph para)
        {
            // 段落级别加粗
            var pPr = para.ParagraphProperties?.ParagraphMarkRunProperties;
            if (pPr?.GetFirstChild<Bold>() != null) return true;

            // 检查第一个非空 Run
            foreach (var run in para.Descendants<Run>())
            {
                var rPr = run.RunProperties;
                if (rPr == null) continue;
                var bold = rPr.Bold;
                if (bold == null) return false;
                // Val 为 null 表示 <w:b/>（无属性，默认启用）
                return bold.Val?.Value ?? true;
            }
            return false;
        }

        private static float GetFontSize(Paragraph para)
        {
            foreach (var run in para.Descendants<Run>())
            {
                var sz = run.RunProperties?.FontSize;
                if (sz?.Val != null && int.TryParse(sz.Val.Value, out int halfPoints))
                    return halfPoints;
            }
            return 0f;
        }
    }

    /// <summary>OpenXML 段落格式特征（与 OpenXML 解耦后传给检测器）</summary>
    public class ParagraphFeatures
    {
        public bool HasHeadingStyle { get; set; }
        public int OutlineLevel { get; set; } = 9;
        public bool IsBold { get; set; }
        public float FontSize { get; set; }
    }
}
