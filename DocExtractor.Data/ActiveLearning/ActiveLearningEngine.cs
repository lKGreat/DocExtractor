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

        /// <summary>最小训练样本阈值</summary>
        public int MinSamplesForTraining { get; set; } = 20;

        /// <summary>每次新增多少条样本后建议训练</summary>
        public int TrainTriggerBatchSize { get; set; } = 20;

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

            var entities = _nerModel.ExtractWithConfidence(text);

            var scenarioTypes = new HashSet<string>(scenario.EntityTypes, StringComparer.OrdinalIgnoreCase);
            var filtered = entities
                .Where(e => scenarioTypes.Count == 0 || scenarioTypes.Contains(e.Type.ToString()))
                .Select(e => new ActiveEntityAnnotation
                {
                    StartIndex = e.StartIndex,
                    EndIndex   = e.EndIndex,
                    EntityType = e.Type.ToString(),
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
                    ConfidenceScore = originalConfidence,
                    IsVerified      = true
                });
            }

            if (uncertainEntryId.HasValue)
                repo.MarkUncertainReviewed(uncertainEntryId.Value);

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
            foreach (var entry in uncertain)
                repo.AddUncertainEntry(entry);
            return uncertain.Count;
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

            var nerAnnotations = ConvertToNerAnnotations(samples);

            progress?.Report(("评估", "记录训练前基线质量...", 5));
            var metricsBefore = EvaluateCurrentModel(samples);
            result.MetricsBefore = metricsBefore;

            progress?.Report(("训练", "开始增量训练 NER 模型...", 10));
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
                var metricsAfter = _evaluator.EvaluateAll(samples, predictor);
                result.MetricsAfter = metricsAfter;

                bool improved = metricsAfter.F1 >= metricsBefore.F1;
                result.IsImproved = improved;

                if (improved || metricsBefore.SampleCount == 0)
                {
                    string finalPath = Path.Combine(_modelsDir, "ner_model.zip");
                    if (File.Exists(finalPath)) File.Delete(finalPath);
                    File.Move(tempModelPath, finalPath);
                    _nerModel.Load(finalPath);
                    result.Success = true;
                    result.Message = $"训练成功！F1: {metricsBefore.F1:P1} → {metricsAfter.F1:P1}";
                }
                else
                {
                    result.Success    = true;
                    result.IsImproved = false;
                    result.Message    = $"训练完成但质量未提升（F1: {metricsAfter.F1:P1} < {metricsBefore.F1:P1}），已回滚";
                }

                repo.SaveLearningSession(new NlpLearningSession
                {
                    ScenarioId        = scenarioId,
                    SampleCountBefore = samples.Count,
                    SampleCountAfter  = samples.Count,
                    MetricsBeforeJson = JsonConvert.SerializeObject(metricsBefore),
                    MetricsAfterJson  = JsonConvert.SerializeObject(metricsAfter),
                    DurationSeconds   = sw.Elapsed.TotalSeconds,
                    IsImproved        = result.IsImproved
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
                result.Message = $"训练失败: {ex.Message}";
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
            return _evaluator.EvaluateOnAnnotations(samples, MakePredictor(_nerModel));
        }

        // ── 统计 ─────────────────────────────────────────────────────────────

        public int GetAnnotatedCount(int scenarioId)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetAnnotatedTextCount(scenarioId);
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

        public List<NlpLearningSession> GetLearningSessions(int scenarioId)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetLearningSessions(scenarioId);
        }

        // ── 工具方法 ─────────────────────────────────────────────────────────

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
            text => model.ExtractWithConfidence(text)
                .Select(e => new ActiveEntityAnnotation
                {
                    StartIndex = e.StartIndex,
                    EndIndex   = e.EndIndex,
                    EntityType = e.Type.ToString(),
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
        public string Message { get; set; } = string.Empty;
        public NlpQualityMetrics? MetricsBefore { get; set; }
        public NlpQualityMetrics? MetricsAfter { get; set; }
        public double DurationSeconds { get; set; }
        public DateTime StartedAt { get; set; }
    }
}
