using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.EntityExtractor;
using DocExtractor.ML.Training;
using Newtonsoft.Json;

namespace DocExtractor.Data.ActiveLearning
{
    /// <summary>
    /// 主动学习闭环核心引擎
    /// 职责：预测 → 接收校正 → 增量训练 → 质量评估 → 模型更新
    /// </summary>
    public class ActiveLearningEngine
    {
        private readonly string _dbPath;
        private readonly string _modelsDir;
        private readonly NerModel _nerModel;
        private readonly QualityEvaluator _evaluator;
        private readonly UncertaintySampler _sampler;

        /// <summary>最小训练样本阈值（每个唯一文本算 1 条）</summary>
        public int MinSamplesForTraining { get; set; } = 3;

        /// <summary>每次新增多少条样本后建议训练</summary>
        public int TrainTriggerBatchSize { get; set; } = 20;

        /// <summary>质量告警目标：用于提示，不阻断替换</summary>
        public double QualityGateTargetF1 { get; set; } = 0.95;

        /// <summary>最小提升阈值：新模型相对当前模型至少提升该值才应用</summary>
        public double QualityGateMinDelta { get; set; } = 0.0;

        public ActiveLearningEngine(string dbPath, string modelsDir, NerModel nerModel)
        {
            _dbPath    = dbPath;
            _modelsDir = modelsDir;
            _nerModel  = nerModel;
            _evaluator = new QualityEvaluator();
            _sampler   = new UncertaintySampler(nerModel);
        }

        // ── 预测 ──────────────────────────────────────────────────────────────

        public TextPredictionResult Predict(string text, NlpScenario scenario)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new TextPredictionResult { RawText = text, Entities = new List<ActiveEntityAnnotation>() };

            var entities = _nerModel.ExtractLabelEntitiesWithConfidence(text);

            var scenarioTypes = new HashSet<string>(scenario.EntityTypes, StringComparer.OrdinalIgnoreCase);
            var filtered = entities
                .Where(e => scenarioTypes.Count == 0 || scenarioTypes.Contains(e.Label))
                .Select(e => new ActiveEntityAnnotation
                {
                    StartIndex = e.StartIndex,
                    EndIndex   = e.EndIndex,
                    EntityType = e.Label,
                    Text       = e.Text,
                    Confidence = e.Confidence
                })
                .ToList();

            float avgConf = filtered.Count > 0 ? filtered.Average(e => e.Confidence) : 0f;

            return new TextPredictionResult
            {
                RawText       = text,
                Entities      = filtered,
                AvgConfidence = avgConf,
                ModelLoaded   = _nerModel.IsLoaded
            };
        }

        // ── 校正提交 ─────────────────────────────────────────────────────────

        public int SubmitCorrection(
            string rawText,
            List<ActiveEntityAnnotation> correctedEntities,
            int scenarioId,
            float originalConfidence = 0f,
            int? uncertainEntryId = null)
        {
            string annotationsJson = JsonConvert.SerializeObject(correctedEntities);

            using var repo = new ActiveLearningRepository(_dbPath);

            var existing = repo.GetAnnotatedTexts(scenarioId)
                .FirstOrDefault(t => t.RawText.Equals(rawText, StringComparison.Ordinal));

            int id;
            if (existing != null)
            {
                repo.UpdateAnnotation(existing.Id, annotationsJson, isVerified: true);
                id = existing.Id;
            }
            else
            {
                id = repo.AddAnnotatedText(new NlpAnnotatedText
                {
                    ScenarioId      = scenarioId,
                    RawText         = rawText,
                    AnnotationsJson = annotationsJson,
                    Source          = "manual_correction",
                    AnnotationMode  = AnnotationMode.SpanEntity.ToString(),
                    StructuredAnnotationsJson = "{}",
                    ConfidenceScore = originalConfidence,
                    IsVerified      = true
                });
            }

            if (uncertainEntryId.HasValue)
                repo.MarkUncertainReviewed(uncertainEntryId.Value, isSkipped: false);

            return id;
        }

        public int SubmitStructuredCorrection(
            string rawText,
            int scenarioId,
            AnnotationMode mode,
            string structuredAnnotationsJson,
            float originalConfidence = 0f,
            int? uncertainEntryId = null)
        {
            string safeRaw = rawText ?? string.Empty;
            string safeStructured = string.IsNullOrWhiteSpace(structuredAnnotationsJson)
                ? "{}"
                : structuredAnnotationsJson;

            using var repo = new ActiveLearningRepository(_dbPath);

            var existing = repo.GetAnnotatedTexts(scenarioId)
                .FirstOrDefault(t => t.RawText.Equals(safeRaw, StringComparison.Ordinal));

            int id;
            if (existing != null)
            {
                repo.UpdateStructuredAnnotation(existing.Id, mode.ToString(), safeStructured, isVerified: true);
                id = existing.Id;
            }
            else
            {
                id = repo.AddAnnotatedText(new NlpAnnotatedText
                {
                    ScenarioId = scenarioId,
                    RawText = safeRaw,
                    AnnotationsJson = "[]",
                    Source = "manual_structured",
                    AnnotationMode = mode.ToString(),
                    StructuredAnnotationsJson = safeStructured,
                    ConfidenceScore = originalConfidence,
                    IsVerified = true
                });
            }

            if (uncertainEntryId.HasValue)
                repo.MarkUncertainReviewed(uncertainEntryId.Value, isSkipped: false);

            return id;
        }

        // ── 不确定性采样 ─────────────────────────────────────────────────────

        public List<NlpUncertainEntry> GetUncertainQueue(int scenarioId, int topN = 20)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetUncertainQueue(scenarioId, topN);
        }

        public int EnqueueTextsForReview(IEnumerable<string> texts, int scenarioId)
        {
            var uncertain = _sampler.SelectMostUncertain(texts, scenarioId, topN: 50);
            using var repo = new ActiveLearningRepository(_dbPath);
            int before = repo.GetPendingUncertainCount(scenarioId);
            foreach (var entry in uncertain)
                repo.AddUncertainEntry(entry);
            int after = repo.GetPendingUncertainCount(scenarioId);
            return Math.Max(0, after - before);
        }

        // ── 增量训练 ─────────────────────────────────────────────────────────

        public LearningSessionResult TrainIncremental(
            int scenarioId,
            TrainingParameters? parameters = null,
            IProgress<(string Stage, string Detail, double Percent)>? progress = null,
            CancellationToken cancellation = default)
        {
            var p  = parameters ?? TrainingParameters.Standard();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var repo = new ActiveLearningRepository(_dbPath);
            var samples = repo.GetAnnotatedTexts(scenarioId, verifiedOnly: true);

            var result = new LearningSessionResult
            {
                ScenarioId  = scenarioId,
                SampleCount = samples.Count,
                StartedAt   = DateTime.Now
            };

            if (samples.Count < MinSamplesForTraining)
            {
                result.Success = false;
                result.Message = $"样本不足（{samples.Count}/{MinSamplesForTraining}），请继续标注更多文本";
                return result;
            }

            var split = BuildHoldoutSplit(samples, p.Seed, p.TestFraction);
            var trainingSamples = split.TrainSamples.Concat(split.ValidationSamples).ToList();
            var testSamples = split.TestSamples;

            if (trainingSamples.Count == 0 || testSamples.Count == 0)
            {
                result.Success = false;
                result.Message = "样本切分失败（训练集或测试集为空），请增加样本后重试";
                return result;
            }

            var nerAnnotations = ConvertToNerAnnotations(trainingSamples);

            progress?.Report(("评估", $"记录训练前基线质量（测试集 {testSamples.Count} 条）...", 5));
            var metricsBefore = EvaluateCurrentModel(testSamples);
            result.MetricsBefore = metricsBefore;

            progress?.Report(("训练", $"开始增量训练 NER 模型（训练+验证集 {trainingSamples.Count} 条）...", 10));
            string tempModelPath = Path.Combine(_modelsDir, $"_tmp_nlplab_ner_{Guid.NewGuid():N}.zip");

            try
            {
                var trainer = new NerTrainer();
                var trainingProgress = new Progress<string>(msg =>
                    progress?.Report(("训练", msg, 50)));

                trainer.Train(nerAnnotations, tempModelPath, trainingProgress, p, cancellation);

                progress?.Report(("评估", "训练完成，评估新模型质量...", 80));

                var tempModel = new NerModel();
                tempModel.Load(tempModelPath);

                var predictor   = MakePredictor(tempModel);
                var metricsAfter = _evaluator.EvaluateAll(testSamples, predictor);
                result.MetricsAfter = metricsAfter;

                bool improved = metricsAfter.F1 > (metricsBefore.F1 + QualityGateMinDelta);
                bool reachedTarget = metricsAfter.F1 >= QualityGateTargetF1;
                bool modelApplied = false;
                string appliedAt = string.Empty;
                string modelTag = BuildModelTag();

                result.IsImproved = improved;
                result.PassedComparison = improved;
                result.ReachedTarget = reachedTarget;

                if (improved)
                {
                    string finalPath = Path.Combine(_modelsDir, "ner_model.zip");
                    if (File.Exists(finalPath)) File.Delete(finalPath);
                    File.Move(tempModelPath, finalPath);
                    _nerModel.Load(finalPath);
                    modelApplied = true;
                    appliedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    modelTag = BuildModelTag();
                    result.Success = true;
                    result.Message = reachedTarget
                        ? $"训练成功并已应用新模型。F1: {metricsBefore.F1:P1} → {metricsAfter.F1:P1}（达到目标）"
                        : $"训练成功并已应用新模型。F1: {metricsBefore.F1:P1} → {metricsAfter.F1:P1}（未达目标，仅提示）";
                }
                else
                {
                    result.Success    = true;
                    result.Message    = $"训练完成但未优于当前模型（目标提示 {QualityGateTargetF1:P0}，当前 {metricsAfter.F1:P1}），未应用";
                }

                result.ModelApplied = modelApplied;
                result.AppliedAt = appliedAt;
                result.ModelTag = modelTag;

                repo.SaveLearningSession(new NlpLearningSession
                {
                    ScenarioId        = scenarioId,
                    SampleCountBefore = samples.Count,
                    SampleCountAfter  = samples.Count,
                    MetricsBeforeJson = JsonConvert.SerializeObject(metricsBefore),
                    MetricsAfterJson  = JsonConvert.SerializeObject(metricsAfter),
                    DurationSeconds   = sw.Elapsed.TotalSeconds,
                    IsImproved        = result.IsImproved,
                    PassedComparison  = result.PassedComparison,
                    ModelApplied      = result.ModelApplied,
                    AppliedAt         = result.AppliedAt,
                    ModelTag          = result.ModelTag
                });
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Message = "训练已取消";
            }
            catch (Exception ex)
            {
                result.Success = false;
                var detail = ex.InnerException != null
                    ? $"{ex.Message} | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"
                    : ex.Message;
                result.Message = $"训练失败: {ex.GetType().Name}: {detail}";
            }
            finally
            {
                TryDelete(tempModelPath);
            }

            sw.Stop();
            result.DurationSeconds = sw.Elapsed.TotalSeconds;
            return result;
        }

        // ── 质量评估 ─────────────────────────────────────────────────────────

        public NlpQualityMetrics EvaluateCurrentModel(int scenarioId)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            var samples = repo.GetAnnotatedTexts(scenarioId, verifiedOnly: true);
            return EvaluateCurrentModel(samples);
        }

        private NlpQualityMetrics EvaluateCurrentModel(List<NlpAnnotatedText> samples)
        {
            if (samples.Count == 0) return new NlpQualityMetrics();
            return _evaluator.EvaluateAll(samples, MakePredictor(_nerModel));
        }

        // ── 统计 ─────────────────────────────────────────────────────────────

        public int GetAnnotatedCount(int scenarioId)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetAnnotatedTextCount(scenarioId);
        }

        public List<NlpAnnotatedText> GetAnnotatedTexts(int scenarioId, bool verifiedOnly = false)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetAnnotatedTexts(scenarioId, verifiedOnly);
        }

        public void DeleteAnnotatedText(int id)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            repo.DeleteAnnotatedText(id);
        }

        public int GetVerifiedCount(int scenarioId)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetVerifiedCount(scenarioId);
        }

        public int GetPendingUncertainCount(int scenarioId)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetPendingUncertainCount(scenarioId);
        }

        public void MarkUncertainSkipped(int queueId, string reason = "manual_skip")
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            repo.MarkUncertainReviewed(queueId, isSkipped: true, skipReason: reason);
        }

        public List<NlpLearningSession> GetLearningSessions(int scenarioId)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetLearningSessions(scenarioId);
        }

        public NlpLearningSession? GetLatestAppliedLearningSession(int scenarioId)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetLearningSessions(scenarioId)
                .LastOrDefault(s => s.ModelApplied);
        }

        public string GetCurrentModelTag()
        {
            return BuildModelTag();
        }

        // ── 工具方法 ─────────────────────────────────────────────────────────

        private static HoldoutSplit BuildHoldoutSplit(
            List<NlpAnnotatedText> samples,
            int seed,
            double testFraction)
        {
            var rng = new Random(seed);
            var randomized = samples
                .OrderBy(_ => rng.Next())
                .ToList();

            int total = randomized.Count;
            double boundedTestFraction = testFraction < 0.1 ? 0.1 : (testFraction > 0.4 ? 0.4 : testFraction);
            int testCount = Math.Max(1, (int)Math.Round(total * boundedTestFraction));
            if (testCount >= total) testCount = Math.Max(1, total - 1);

            int remain = total - testCount;
            int validationCount = remain >= 10 ? Math.Max(1, (int)Math.Round(remain * 0.1)) : 0;
            if (validationCount >= remain) validationCount = Math.Max(0, remain - 1);

            var testSamples = randomized.Take(testCount).ToList();
            var rest = randomized.Skip(testCount).ToList();
            var validationSamples = rest.Take(validationCount).ToList();
            var trainSamples = rest.Skip(validationCount).ToList();

            return new HoldoutSplit
            {
                TrainSamples = trainSamples,
                ValidationSamples = validationSamples,
                TestSamples = testSamples
            };
        }

        private static List<NerAnnotation> ConvertToNerAnnotations(List<NlpAnnotatedText> samples)
        {
            var result = new List<NerAnnotation>();
            foreach (var s in samples)
            {
                var entities = DeserializeAnnotations(s.AnnotationsJson);
                result.Add(new NerAnnotation
                {
                    Text     = s.RawText,
                    Entities = entities.Select(e => new EntityAnnotation
                    {
                        StartIndex = e.StartIndex,
                        EndIndex   = e.EndIndex,
                        EntityType = e.EntityType,
                        Text       = e.Text
                    }).ToList()
                });
            }
            return result;
        }

        private static Func<string, List<ActiveEntityAnnotation>> MakePredictor(NerModel model) =>
            text => model.ExtractLabelEntitiesWithConfidence(text)
                .Select(e => new ActiveEntityAnnotation
                {
                    StartIndex = e.StartIndex,
                    EndIndex   = e.EndIndex,
                    EntityType = e.Label,
                    Text       = e.Text,
                    Confidence = e.Confidence
                }).ToList();

        private static List<ActiveEntityAnnotation> DeserializeAnnotations(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<ActiveEntityAnnotation>();
            try { return JsonConvert.DeserializeObject<List<ActiveEntityAnnotation>>(json) ?? new List<ActiveEntityAnnotation>(); }
            catch { return new List<ActiveEntityAnnotation>(); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private string BuildModelTag()
        {
            string finalPath = Path.Combine(_modelsDir, "ner_model.zip");
            if (!File.Exists(finalPath))
                return "ner_model.zip (missing)";
            var fi = new FileInfo(finalPath);
            return $"ner_model.zip@{fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
        }

        private sealed class HoldoutSplit
        {
            public List<NlpAnnotatedText> TrainSamples { get; set; } = new List<NlpAnnotatedText>();
            public List<NlpAnnotatedText> ValidationSamples { get; set; } = new List<NlpAnnotatedText>();
            public List<NlpAnnotatedText> TestSamples { get; set; } = new List<NlpAnnotatedText>();
        }
    }

    /// <summary>单条文本预测结果</summary>
    public class TextPredictionResult
    {
        public string RawText { get; set; } = string.Empty;
        public List<ActiveEntityAnnotation> Entities { get; set; } = new List<ActiveEntityAnnotation>();
        public float AvgConfidence { get; set; }
        public bool ModelLoaded { get; set; }
    }

    /// <summary>一次增量训练的结果</summary>
    public class LearningSessionResult
    {
        public int ScenarioId { get; set; }
        public int SampleCount { get; set; }
        public bool Success { get; set; }
        public bool IsImproved { get; set; }
        public bool PassedComparison { get; set; }
        public bool ReachedTarget { get; set; }
        public bool ModelApplied { get; set; }
        public string AppliedAt { get; set; } = string.Empty;
        public string ModelTag { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NlpQualityMetrics? MetricsBefore { get; set; }
        public NlpQualityMetrics? MetricsAfter { get; set; }
        public double DurationSeconds { get; set; }
        public DateTime StartedAt { get; set; }
    }
}
