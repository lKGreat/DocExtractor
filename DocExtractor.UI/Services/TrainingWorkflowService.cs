using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocExtractor.Core.Models;
using DocExtractor.Data.Repositories;
using DocExtractor.ML;
using DocExtractor.ML.ColumnClassifier;
using DocExtractor.ML.EntityExtractor;
using DocExtractor.ML.SectionClassifier;
using DocExtractor.ML.Training;
using DocExtractor.Parsing.Word;
using Newtonsoft.Json;

namespace DocExtractor.UI.Services
{
    internal class TrainingWorkflowService
    {
        public TrainingDataBundle LoadTrainingData(string dbPath)
        {
            List<(string ColumnText, string FieldName)> colSamples;
            List<NerAnnotation> nerSamples;
            List<SectionAnnotation> secSamples;

            using (var repo = new TrainingDataRepository(dbPath))
            {
                colSamples = repo.GetColumnSamples();
                nerSamples = repo.GetNerSamples();
                secSamples = repo.GetSectionSamples();
            }

            var colInputs = colSamples.ConvertAll(s => new ColumnInput
            {
                ColumnText = s.ColumnText,
                Label = s.FieldName
            });

            var secInputs = secSamples.ConvertAll(s => new SectionInput
            {
                Text = s.ParagraphText,
                IsBold = s.IsBold ? 1f : 0f,
                FontSize = s.FontSize,
                HasNumberPrefix = s.ParagraphText.Length > 0 && char.IsDigit(s.ParagraphText[0]) ? 1f : 0f,
                TextLength = s.ParagraphText.Length,
                HasHeadingStyle = s.HasHeadingStyle ? 1f : 0f,
                Position = 0f,
                IsHeading = s.IsHeading
            });

            return new TrainingDataBundle
            {
                ColumnInputs = colInputs,
                NerSamples = nerSamples,
                SectionInputs = secInputs
            };
        }

        public int ImportColumnSamples(string dbPath, string filePath)
        {
            int imported = 0;
            using var repo = new TrainingDataRepository(dbPath);

            foreach (var line in File.ReadAllLines(filePath, System.Text.Encoding.UTF8))
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    repo.AddColumnSample(parts[0].Trim(), parts[1].Trim(), filePath);
                    imported++;
                }
            }

            return imported;
        }

        public int SaveManualColumnMappings(
            string dbPath,
            IReadOnlyList<(string RawColumn, string FieldName)> mappings)
        {
            if (mappings.Count == 0) return 0;

            int saved = 0;
            using var repo = new TrainingDataRepository(dbPath);
            foreach (var m in mappings
                         .Where(x => !string.IsNullOrWhiteSpace(x.RawColumn)
                                     && !string.IsNullOrWhiteSpace(x.FieldName))
                         .Distinct())
            {
                repo.AddColumnSample(
                    m.RawColumn.Trim(),
                    m.FieldName.Trim(),
                    "ManualPreview",
                    isVerified: true);
                saved++;
            }

            return saved;
        }

        public int GetVerifiedManualSampleCount(string dbPath)
        {
            using var repo = new TrainingDataRepository(dbPath);
            return repo.GetVerifiedColumnSampleCount("ManualPreview");
        }

        public List<ColumnErrorAnalysisItem> BuildColumnErrorAnalysis(
            string dbPath,
            ColumnClassifierModel columnModel,
            int maxRows = 200)
        {
            var errors = new List<ColumnErrorAnalysisItem>();
            if (!columnModel.IsLoaded) return errors;

            using var repo = new TrainingDataRepository(dbPath);
            var samples = repo.GetColumnSamples(verifiedOnly: true);
            if (samples.Count == 0)
                samples = repo.GetColumnSamples();

            foreach (var sample in samples)
            {
                var (predicted, confidence) = columnModel.Predict(sample.ColumnText);
                string actual = sample.FieldName;
                if (string.IsNullOrWhiteSpace(predicted))
                    continue;

                if (!string.Equals(predicted, actual, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ColumnErrorAnalysisItem
                    {
                        RawColumnName = sample.ColumnText,
                        PredictedField = predicted,
                        ActualField = actual,
                        Confidence = confidence
                    });
                }
            }

            return errors
                .OrderByDescending(e => e.Confidence)
                .Take(maxRows)
                .ToList();
        }

        public SectionImportResult ImportSectionSamples(string dbPath, IReadOnlyList<string> filePaths)
        {
            int imported = 0;
            var perFile = new List<(string fileName, int count)>();

            var scanner = new WordParagraphScanner();
            using var repo = new TrainingDataRepository(dbPath);

            foreach (var filePath in filePaths)
            {
                var paragraphs = scanner.Scan(filePath);

                foreach (var p in paragraphs)
                {
                    repo.AddSectionSample(
                        p.Text,
                        p.AutoIsHeading,
                        p.IsBold,
                        p.FontSize,
                        p.HasHeadingStyle,
                        p.OutlineLevel,
                        filePath);
                    imported++;
                }

                perFile.Add((Path.GetFileName(filePath), paragraphs.Count));
            }

            return new SectionImportResult
            {
                ImportedCount = imported,
                PerFileCounts = perFile
            };
        }

        public (int colAdded, int secPosAdded, int secNegAdded) GenerateFromKnowledge(string dbPath, ExtractionConfig[] configs)
        {
            using var repo = new TrainingDataRepository(dbPath);
            return repo.GenerateFromKnowledge(configs);
        }

        public UnifiedTrainingResult TrainUnified(
            TrainingDataBundle bundle,
            string modelsDir,
            TrainingParameters parameters,
            IProgress<(string Stage, string Detail, double Percent)> progress,
            System.Threading.CancellationToken cancellationToken)
        {
            var trainer = new UnifiedModelTrainer();
            return trainer.TrainAll(
                bundle.ColumnInputs,
                bundle.NerSamples,
                bundle.SectionInputs,
                modelsDir,
                parameters,
                progress,
                cancellationToken);
        }

        public void ReloadModels(
            string modelsDir,
            ColumnClassifierModel columnModel,
            NerModel nerModel,
            SectionClassifierModel sectionModel)
        {
            string colModelPath = Path.Combine(modelsDir, "column_classifier.zip");
            string nerModelPath = Path.Combine(modelsDir, "ner_model.zip");
            string sectionModelPath = Path.Combine(modelsDir, "section_classifier.zip");
            if (File.Exists(colModelPath)) columnModel.Reload(colModelPath);
            if (File.Exists(nerModelPath)) nerModel.Load(nerModelPath);
            if (File.Exists(sectionModelPath)) sectionModel.Reload(sectionModelPath);
        }

        public void SaveTrainingHistory(
            string dbPath,
            UnifiedTrainingResult result,
            TrainingDataBundle bundle,
            TrainingParameters parameters)
        {
            using var repo = new TrainingDataRepository(dbPath);
            if (result.ColumnEval != null)
            {
                repo.SaveTrainingRecord(
                    "ColumnClassifier",
                    bundle.ColumnInputs.Count,
                    result.ColumnEval.ToString(),
                    JsonConvert.SerializeObject(parameters));
            }

            if (result.NerEval != null)
            {
                repo.SaveTrainingRecord(
                    "NER",
                    bundle.NerSamples.Count,
                    result.NerEval.ToString(),
                    JsonConvert.SerializeObject(parameters));
            }

            if (result.SectionEval != null)
            {
                repo.SaveTrainingRecord(
                    "SectionClassifier",
                    bundle.SectionInputs.Count,
                    result.SectionEval.ToString(),
                    JsonConvert.SerializeObject(parameters));
            }
        }
    }

    internal class TrainingDataBundle
    {
        public List<ColumnInput> ColumnInputs { get; set; } = new List<ColumnInput>();
        public List<NerAnnotation> NerSamples { get; set; } = new List<NerAnnotation>();
        public List<SectionInput> SectionInputs { get; set; } = new List<SectionInput>();
    }

    internal class SectionImportResult
    {
        public int ImportedCount { get; set; }
        public List<(string fileName, int count)> PerFileCounts { get; set; } =
            new List<(string fileName, int count)>();
    }

    internal class ColumnErrorAnalysisItem
    {
        public string RawColumnName { get; set; } = string.Empty;
        public string PredictedField { get; set; } = string.Empty;
        public string ActualField { get; set; } = string.Empty;
        public float Confidence { get; set; }
    }
}
