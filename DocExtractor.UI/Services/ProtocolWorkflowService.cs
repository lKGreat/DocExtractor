using System;
using System.Collections.Generic;
using System.IO;
using DocExtractor.Core.Models;
using DocExtractor.Core.Protocol;
using DocExtractor.Data.Export;
using DocExtractor.Parsing.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocExtractor.UI.Services
{
    /// <summary>
    /// Workflow service that coordinates protocol document analysis:
    /// Word parsing -> telemetry detection -> field extraction -> Excel export.
    /// </summary>
    internal class ProtocolWorkflowService
    {
        private readonly TelemetryConfigExporter _exporter = new TelemetryConfigExporter();
        private readonly TelecommandConfigExporter _telecommandExporter = new TelecommandConfigExporter();

        /// <summary>
        /// Analyze a protocol Word document and return the parse result
        /// (without exporting to Excel yet).
        /// </summary>
        public ProtocolParseResult Analyze(string docxPath)
        {
            if (!File.Exists(docxPath))
                throw new FileNotFoundException("协议文档不存在", docxPath);

            var parser = new WordDocumentParser();
            IReadOnlyList<RawTable> tables = parser.Parse(docxPath);

            var paragraphs = ExtractParagraphTexts(docxPath);
            string title = Path.GetFileNameWithoutExtension(docxPath);

            var analyzer = new ProtocolAnalyzer();
            return analyzer.Analyze(tables, title, paragraphs);
        }

        /// <summary>
        /// Analyze a protocol Word document for telecommand extraction.
        /// </summary>
        public TelecommandParseResult AnalyzeTelecommand(string docxPath)
        {
            if (!File.Exists(docxPath))
                throw new FileNotFoundException("协议文档不存在", docxPath);

            var parser = new WordDocumentParser();
            IReadOnlyList<RawTable> tables = parser.Parse(docxPath);

            var paragraphs = ExtractParagraphTexts(docxPath);
            string title = Path.GetFileNameWithoutExtension(docxPath);

            var analyzer = new TelecommandAnalyzer();
            return analyzer.Analyze(tables, title, paragraphs);
        }

        /// <summary>
        /// Export the parse result to Excel files in the specified directory.
        /// Returns the list of generated file paths.
        /// </summary>
        public List<string> Export(
            ProtocolParseResult result,
            string outputDir,
            ExportOptions? options = null)
        {
            return _exporter.Export(result, outputDir, options);
        }

        /// <summary>
        /// Export telecommand parse result to Excel files.
        /// </summary>
        public List<string> ExportTelecommand(
            TelecommandParseResult result,
            string outputDir,
            TelecommandExportOptions? options = null)
        {
            return _telecommandExporter.Export(result, outputDir, options);
        }

        /// <summary>
        /// One-shot: analyze and export in a single call.
        /// </summary>
        public (ProtocolParseResult Result, List<string> Files) AnalyzeAndExport(
            string docxPath,
            string outputDir,
            ExportOptions? options = null)
        {
            var result = Analyze(docxPath);
            var files = Export(result, outputDir, options);
            return (result, files);
        }

        /// <summary>
        /// Generate the built-in empty telemetry config template.
        /// </summary>
        public void GenerateTemplate(string outputPath)
        {
            TemplateGenerator.GenerateTelemetryConfigTemplate(outputPath);
        }

        private List<string> ExtractParagraphTexts(string docxPath)
        {
            var texts = new List<string>();
            try
            {
                using var doc = WordprocessingDocument.Open(docxPath, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return texts;

                foreach (var element in body.ChildElements)
                {
                    if (element is Paragraph para)
                    {
                        string text = "";
                        foreach (var t in para.Descendants<Text>())
                            text += t.Text;
                        if (!string.IsNullOrWhiteSpace(text))
                            texts.Add(text.Trim());
                    }
                }
            }
            catch
            {
                // Non-critical: system name inference will use document title only
            }
            return texts;
        }
    }
}
