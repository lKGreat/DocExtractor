using System;
using System.Collections.Generic;
using System.IO;
using DocExtractor.Data.ActiveLearning;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.EntityExtractor;
using Xunit;

namespace DocExtractor.Tests.Data
{
    public class ActiveLearningWorkflowTests
    {
        [Fact]
        public void Queue_ShouldDeduplicateAndTrackSkipState()
        {
            string root = CreateTempDir();
            try
            {
                string dbPath = Path.Combine(root, "docextractor.db");
                string modelsDir = Path.Combine(root, "models");
                Directory.CreateDirectory(modelsDir);

                int scenarioId;
                using (var repo = new ActiveLearningRepository(dbPath))
                {
                    scenarioId = repo.AddScenario(new NlpScenario
                    {
                        Name = "QueueTest",
                        Description = "queue dedup test",
                        EntityTypes = new List<string> { "KeyInfo", "Value", "HexCode" },
                        IsBuiltIn = false
                    });
                }

                var engine = new ActiveLearningEngine(dbPath, modelsDir, new NerModel());
                var inputs = new[]
                {
                    "电压 0x1A，最大值 5V",
                    "电压 0x1A，最大值 5V",
                    "温度 25℃，电流 1.2A",
                    "本文档描述多个字段：长度、宽度、重量、温度。"
                };

                int inserted = engine.EnqueueTextsForReview(inputs, scenarioId);
                Assert.Equal(3, inserted);
                Assert.Equal(3, engine.GetPendingUncertainCount(scenarioId));

                var queue = engine.GetUncertainQueue(scenarioId, 20);
                Assert.Equal(3, queue.Count);

                // Skip 后应从待审核队列移除
                engine.MarkUncertainSkipped(queue[0].Id, "test_skip");
                Assert.Equal(2, engine.GetPendingUncertainCount(scenarioId));

                // 重复导入已出现文本，不应继续堆积
                int insertedAgain = engine.EnqueueTextsForReview(new[] { "电压 0x1A，最大值 5V" }, scenarioId);
                Assert.Equal(0, insertedAgain);
            }
            finally
            {
                SafeDeleteDir(root);
            }
        }

        [Fact]
        public void EndToEnd_ShouldHandleSentenceParagraphAndArticleInputs()
        {
            string root = CreateTempDir();
            try
            {
                string dbPath = Path.Combine(root, "docextractor.db");
                string modelsDir = Path.Combine(root, "models");
                Directory.CreateDirectory(modelsDir);

                NlpScenario scenario;
                using (var repo = new ActiveLearningRepository(dbPath))
                {
                    int scenarioId = repo.AddScenario(new NlpScenario
                    {
                        Name = "E2EValidation",
                        Description = "sentence/paragraph/article validation",
                        EntityTypes = new List<string> { "KeyInfo", "Value", "HexCode" },
                        IsBuiltIn = false
                    });
                    scenario = repo.GetScenarioById(scenarioId)!;
                }

                var engine = new ActiveLearningEngine(dbPath, modelsDir, new NerModel());

                string sentence = "电压范围为 0x1A，最大值 5V。";
                string paragraph = "系统在常温下运行，推荐电压 12V，峰值电流 1.5A，超限时触发告警。";
                string article = "第一段：设备支持多种协议。\n第二段：校准值为 0x2B，建议温度 25℃。\n第三段：若超过阈值，请及时复位。";

                int inserted = engine.EnqueueTextsForReview(new[] { sentence, paragraph, article }, scenario.Id);
                Assert.Equal(3, inserted);

                var queue = engine.GetUncertainQueue(scenario.Id, 10);
                Assert.Equal(3, queue.Count);

                // 模拟人工标注两条样本，形成“导入 -> 校正 -> 入库”闭环
                for (int i = 0; i < 2; i++)
                {
                    var sample = queue[i];
                    var entity = BuildEntity(sample.RawText, "KeyInfo");
                    engine.SubmitCorrection(
                        sample.RawText,
                        new List<ActiveEntityAnnotation> { entity },
                        scenario.Id,
                        originalConfidence: sample.MinConfidence,
                        uncertainEntryId: sample.Id);
                }

                Assert.Equal(2, engine.GetVerifiedCount(scenario.Id));
                Assert.Equal(1, engine.GetPendingUncertainCount(scenario.Id));

                var predictResult = engine.Predict(sentence, scenario);
                Assert.NotNull(predictResult);
                Assert.Equal(sentence, predictResult.RawText);

                var metrics = engine.EvaluateCurrentModel(scenario.Id);
                Assert.True(metrics.SampleCount >= 2);
                Assert.InRange(metrics.F1, 0, 1);
            }
            finally
            {
                SafeDeleteDir(root);
            }
        }

        private static ActiveEntityAnnotation BuildEntity(string rawText, string label)
        {
            // 测试用：标注文本开头 2~4 个字符为 KeyInfo
            int length = Math.Min(4, Math.Max(2, rawText.Length));
            return new ActiveEntityAnnotation
            {
                EntityType = label,
                Text = rawText.Substring(0, length),
                StartIndex = 0,
                EndIndex = length - 1,
                Confidence = 1f,
                IsManual = true
            };
        }

        private static string CreateTempDir()
        {
            string path = Path.Combine(Path.GetTempPath(), "docextractor-active-learning-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void SafeDeleteDir(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
