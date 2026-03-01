using System;
using System.Collections.Generic;
using System.Linq;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.EntityExtractor;
using Newtonsoft.Json;

namespace DocExtractor.Data.ActiveLearning
{
    /// <summary>
    /// 不确定性采样器：识别模型"最不确定"的文本，优先推荐给用户标注
    /// 策略：最小置信度（Least Confidence Sampling）
    /// </summary>
    public class UncertaintySampler
    {
        private readonly NerModel _nerModel;

        public UncertaintySampler(NerModel nerModel)
        {
            _nerModel = nerModel;
        }

        /// <summary>
        /// 对一批未标注文本打分，选出最不确定的 topN 条加入队列
        /// </summary>
        public List<NlpUncertainEntry> SelectMostUncertain(
            IEnumerable<string> candidateTexts,
            int scenarioId,
            int topN = 20)
        {
            var scored = new List<(string Text, float Confidence, List<ActiveEntityAnnotation> Predictions)>();

            foreach (var text in candidateTexts)
            {
                if (string.IsNullOrWhiteSpace(text)) continue;

                float conf;
                List<ActiveEntityAnnotation> predictions;

                if (_nerModel.IsLoaded)
                {
                    conf = _nerModel.ComputeTextConfidence(text);
                    var entities = _nerModel.ExtractLabelEntitiesWithConfidence(text);
                    predictions = entities.Select(e => new ActiveEntityAnnotation
                    {
                        StartIndex  = e.StartIndex,
                        EndIndex    = e.EndIndex,
                        EntityType  = e.Label,
                        Text        = e.Text,
                        Confidence  = e.Confidence
                    }).ToList();
                }
                else
                {
                    conf        = 0f;
                    predictions = new List<ActiveEntityAnnotation>();
                }

                scored.Add((text, conf, predictions));
            }

            return scored
                .OrderBy(s => s.Confidence)
                .Take(topN)
                .Select(s => new NlpUncertainEntry
                {
                    ScenarioId      = scenarioId,
                    RawText         = s.Text,
                    PredictionsJson = JsonConvert.SerializeObject(s.Predictions),
                    MinConfidence   = s.Confidence
                })
                .ToList();
        }

        /// <summary>对单条文本计算置信度（0~1，越小越需要标注）</summary>
        public float ScoreText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0f;
            return _nerModel.IsLoaded ? _nerModel.ComputeTextConfidence(text) : 0f;
        }

        /// <summary>按段落批量评分，适合长文章的主动学习</summary>
        public List<(string Sentence, float Confidence)> ScoreByParagraph(string longText, int maxParagraphs = 50)
        {
            if (string.IsNullOrWhiteSpace(longText)) return new List<(string, float)>();

            var lines = longText
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length >= 5)
                .Take(maxParagraphs)
                .ToList();

            return lines.Select(l => (l, ScoreText(l))).ToList();
        }
    }
}
